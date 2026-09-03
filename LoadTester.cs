using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Channels;

namespace IngressLoadTest;

public sealed class LoadTester : IDisposable
{
    private readonly LoadTestOptions _options;
    private readonly byte[] _payload;

    private readonly HttpClient _httpClient;
    private readonly Uri[] _targets;

    public LoadTester(LoadTestOptions options, byte[] payload)
    {
        _options = options;
        _payload = payload;

        var handler = new SocketsHttpHandler
        {
            MaxConnectionsPerServer = options.MaxConnectionsPerServer,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.None,
            ConnectTimeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };

        _httpClient = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds),
            DefaultRequestVersion = HttpVersion.Version11,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
        };

        _targets = new Uri[options.PortCount];

        for (int i = 0; i < options.PortCount; i++)
        {
            int port = options.FirstPort + i;
            _targets[i] = new Uri($"http://{options.Server}:{port}{options.Path}");
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        PrintConfiguration();

        if (_options.WarmupSeconds > 0)
        {
            Console.WriteLine();
            Console.WriteLine($"Warm-up {_options.WarmupSeconds} giây...");

            await RunPhaseAsync(
                _options.WarmupSeconds,
                showProgress: false,
                cancellationToken);

            Console.WriteLine("Warm-up hoàn tất.");
        }

        Console.WriteLine();
        Console.WriteLine($"Bắt đầu test {_options.DurationSeconds} giây...");
        Console.WriteLine();
        Console.WriteLine("Time   Sent/s   Success/s   Error/s");
        Console.WriteLine("-----------------------------------");

        Stopwatch testWatch = Stopwatch.StartNew();

        LoadStats stats = await RunPhaseAsync(
            _options.DurationSeconds,
            showProgress: true,
            cancellationToken);

        testWatch.Stop();

        PrintFinalResult(stats.CreateFinalResult(testWatch.Elapsed));
    }

    private async Task<LoadStats> RunPhaseAsync(
        int durationSeconds,
        bool showProgress,
        CancellationToken cancellationToken)
    {
        var stats = new LoadStats(_options.FirstPort, _options.PortCount);

        var channel = Channel.CreateBounded<int>(
            new BoundedChannelOptions(_options.ChannelCapacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false,
                AllowSynchronousContinuations = false
            });

        using var phaseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        phaseCts.CancelAfter(TimeSpan.FromSeconds(durationSeconds));

        CancellationToken phaseToken = phaseCts.Token;

        Task[] workers = new Task[_options.WorkerCount];

        for (int i = 0; i < workers.Length; i++)
        {
            int workerId = i;

            workers[i] = WorkerAsync(
                workerId,
                channel.Reader,
                stats,
                cancellationToken);
        }

        int[] rpsByPort = CalculateRpsByPort();
        long phaseStart = Stopwatch.GetTimestamp();

        Task[] producers = new Task[_options.PortCount];

        for (int i = 0; i < producers.Length; i++)
        {
            int portIndex = i;

            producers[i] = ProducerAsync(
                portIndex,
                rpsByPort[portIndex],
                phaseStart,
                channel.Writer,
                phaseToken);
        }

        CancellationTokenSource? monitorCts = null;
        Task? monitorTask = null;

        if (showProgress)
        {
            monitorCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            monitorTask = MonitorAsync(stats, monitorCts.Token);
        }

        try
        {
            await Task.WhenAll(producers);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Hết thời gian phase.
        }
        catch (Exception ex)
        {
            ErrorLogger.LogException("Producer group", ex);
        }
        finally
        {
            channel.Writer.TryComplete();
            monitorCts?.Cancel();
        }

        if (monitorTask != null)
        {
            try
            {
                await monitorTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException("Monitor", ex);
            }
        }

        try
        {
            await Task.WhenAll(workers);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            ErrorLogger.LogException("Worker group", ex);
        }
        finally
        {
            monitorCts?.Dispose();
        }

        return stats;
    }

    private async Task ProducerAsync(
        int portIndex,
        int targetRps,
        long phaseStart,
        ChannelWriter<int> writer,
        CancellationToken cancellationToken)
    {
        if (targetRps <= 0)
        {
            return;
        }

        double intervalTicks = Stopwatch.Frequency / (double)targetRps;

        double nextTick =
            phaseStart +
            intervalTicks * portIndex / _options.PortCount;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                long now = Stopwatch.GetTimestamp();
                double remainingTicks = nextTick - now;

                if (remainingTicks > 0)
                {
                    double delayMs = remainingTicks * 1000.0 / Stopwatch.Frequency;
                    int delay = Math.Max(1, (int)Math.Ceiling(delayMs));

                    await Task.Delay(delay, cancellationToken);
                    continue;
                }

                await writer.WriteAsync(portIndex, cancellationToken);

                nextTick += intervalTicks;

                long current = Stopwatch.GetTimestamp();
                double lateTicks = current - nextTick;

                if (lateTicks > Stopwatch.Frequency * 0.25)
                {
                    nextTick = current + intervalTicks;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException(
                    $"Producer port={_options.FirstPort + portIndex}",
                    ex);

                try
                {
                    await Task.Delay(1000, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task WorkerAsync(
        int workerId,
        ChannelReader<int> reader,
        LoadStats stats,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                bool canRead = await reader.WaitToReadAsync(cancellationToken);

                if (!canRead)
                {
                    break;
                }

                while (reader.TryRead(out int portIndex))
                {
                    await SendOneSafeAsync(
                        workerId,
                        portIndex,
                        stats,
                        cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (ChannelClosedException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException($"Worker #{workerId}", ex);

                try
                {
                    await Task.Delay(250, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task SendOneSafeAsync(
        int workerId,
        int portIndex,
        LoadStats stats,
        CancellationToken cancellationToken)
    {
        stats.RecordSent(portIndex);

        long start = Stopwatch.GetTimestamp();
        bool success = false;

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                _targets[portIndex]);

            request.Version = HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;

            var content = new ByteArrayContent(_payload);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            request.Content = content;

            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            await response.Content.CopyToAsync(Stream.Null, cancellationToken);

            success = response.IsSuccessStatusCode;

            if (!success)
            {
                string key = $"HTTP|{portIndex}|{(int)response.StatusCode}";

                ErrorLogger.LogRateLimited(
                    key,
                    $"HTTP {(int)response.StatusCode} {response.ReasonPhrase} - {_targets[portIndex]}");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorLogger.LogException(
                $"HTTP worker={workerId}, port={_options.FirstPort + portIndex}",
                ex);

            success = false;
        }
        finally
        {
            long latency = Stopwatch.GetTimestamp() - start;
            stats.RecordCompleted(portIndex, success, latency);
        }
    }

    private async Task MonitorAsync(
        LoadStats stats,
        CancellationToken cancellationToken)
    {
        int seconds = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(1000, cancellationToken);

                seconds++;

                SecondSnapshot snapshot = stats.TakeSecondSnapshot();

                Console.WriteLine(
                    $"{seconds,4}s " +
                    $"{snapshot.Sent,8} " +
                    $"{snapshot.Success,11} " +
                    $"{snapshot.Errors,9}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException("Monitor tick", ex);
            }
        }
    }

    private int[] CalculateRpsByPort()
    {
        var result = new int[_options.PortCount];

        int baseRps = _options.TargetRps / _options.PortCount;
        int remainder = _options.TargetRps % _options.PortCount;

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = baseRps + (i < remainder ? 1 : 0);
        }

        return result;
    }

    private void PrintConfiguration()
    {
        int lastPort = _options.FirstPort + _options.PortCount - 1;

        Console.WriteLine("========================================");
        Console.WriteLine("IngressHost Load Test");
        Console.WriteLine("========================================");
        Console.WriteLine($"Server       : {_options.Server}");
        Console.WriteLine($"Ports        : {_options.FirstPort} - {lastPort}");
        Console.WriteLine($"Endpoint     : {_options.Path}");
        Console.WriteLine("HTTP         : HTTP/1.1");
        Console.WriteLine($"Target RPS   : {_options.TargetRps:N0}");
        Console.WriteLine($"RPS/port     : ~{_options.TargetRps / (double)_options.PortCount:F1}");
        Console.WriteLine($"Payload      : {_payload.Length:N0} bytes");
        Console.WriteLine($"Workers      : {_options.WorkerCount}");
        Console.WriteLine($"Max conn/host: {_options.MaxConnectionsPerServer}");
        Console.WriteLine($"Timeout      : {_options.RequestTimeoutSeconds} s");
        Console.WriteLine($"Error log    : {ErrorLogger.FilePath}");
        Console.WriteLine("========================================");
    }

    private void PrintFinalResult(FinalResult result)
    {
        double averageRps =
            result.ActualDuration.TotalSeconds <= 0
                ? 0
                : result.TotalSent / result.ActualDuration.TotalSeconds;

        Console.WriteLine();
        Console.WriteLine("================ RESULT ================");
        Console.WriteLine($"Duration        : {result.ActualDuration.TotalSeconds:F1} sec");
        Console.WriteLine($"Requests        : {result.TotalSent:N0}");
        Console.WriteLine($"Success         : {result.Success:N0}");
        Console.WriteLine($"Errors          : {result.Errors:N0}");
        Console.WriteLine($"Average RPS     : {averageRps:N1}");
        Console.WriteLine($"Average latency : {result.AverageLatencyMs:F3} ms");
        Console.WriteLine($"P50 approx      : {result.P50Ms:F3} ms");
        Console.WriteLine($"P95 approx      : {result.P95Ms:F3} ms");
        Console.WriteLine($"P99 approx      : {result.P99Ms:F3} ms");
        Console.WriteLine($"Max             : {result.MaxLatencyMs:F3} ms");

        Console.WriteLine();
        Console.WriteLine("Per port:");
        Console.WriteLine("Port       Sent      Success       Error");
        Console.WriteLine("----------------------------------------");

        foreach (PortResult port in result.Ports)
        {
            Console.WriteLine(
                $"{port.Port,4} " +
                $"{port.Sent,10:N0} " +
                $"{port.Success,12:N0} " +
                $"{port.Errors,11:N0}");
        }

        Console.WriteLine("========================================");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
