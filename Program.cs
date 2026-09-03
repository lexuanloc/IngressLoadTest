using System.Text;

namespace IngressLoadTest;

internal static class Program
{
    private static async Task<int> Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        ErrorLogger.Initialize();
        RegisterGlobalExceptionHandlers();

        using var stopCts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            ErrorLogger.LogMessage("Ctrl+C received. Stopping.", flushToDisk: true);
            stopCts.Cancel();
        };

        try
        {
            return await RunAsync(stopCts.Token);
        }
        catch (OperationCanceledException) when (stopCts.IsCancellationRequested)
        {
            ErrorLogger.LogMessage("Application stopped by user.", flushToDisk: true);
            return 0;
        }
        catch (Exception ex)
        {
            ErrorLogger.LogFatal("Program.Main", ex, isTerminating: true);
            return 100;
        }
        finally
        {
            ErrorLogger.LogMessage(
                $"Process exiting. ExitCode={Environment.ExitCode}",
                flushToDisk: true);
        }
    }

    private static async Task<int> RunAsync(CancellationToken cancellationToken)
    {
        (LoadTestOptions options, PreparedPayload[] payloads) =
            await LoadConfigurationAsync();

        Console.WriteLine($"Error log: {ErrorLogger.FilePath}");

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var tester = new LoadTester(options, payloads);
                await tester.RunAsync(cancellationToken);

                // Kết thúc bình thường theo DurationSeconds.
                return 0;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return 0;
            }
            catch (Exception ex)
            {
                ErrorLogger.LogException("Top-level LoadTester", ex, force: true);

                Console.WriteLine();
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Unexpected error: {ex.Message}");
                Console.WriteLine($"Đã ghi vào {ErrorLogger.FilePath}");

                if (!options.RestartOnUnexpectedError)
                {
                    return 101;
                }

                Console.WriteLine(
                    $"Tự khởi động lại test sau {options.RestartDelaySeconds} giây...");

                await Task.Delay(
                    TimeSpan.FromSeconds(options.RestartDelaySeconds),
                    cancellationToken);
            }
        }

        return 0;
    }

    private static async Task<(LoadTestOptions Options, PreparedPayload[] Payloads)>
        LoadConfigurationAsync()
    {
        string baseDirectory = AppContext.BaseDirectory;
        string configPath = Path.Combine(baseDirectory, "appsettings.json");

        LoadTestOptions options = LoadTestOptions.Load(configPath);

        string payloadPath =
            ResolvePath(
                baseDirectory,
                options.PayloadFile);

        byte[] templatePayload = await File.ReadAllBytesAsync(payloadPath);

        if (string.IsNullOrWhiteSpace(options.ClientDataFile))
        {
            Console.WriteLine(
                "ClientDataFile không được cấu hình. " +
                "Sử dụng nguyên payload.json cho tất cả request.");

            PreparedPayload[] singlePayload =
                PayloadFactory.CreatePayloadsWithoutClientOverride(
                    templatePayload);

            return (options, singlePayload);
        }

        string clientDataPath =
            ResolvePath(
                baseDirectory,
                options.ClientDataFile);

        ClientIdentity[] clients =
            ClientIdentityFile.Load(clientDataPath);

        PreparedPayload[] payloads =
            PayloadFactory.CreatePayloads(
                templatePayload,
                clients);

        Console.WriteLine(
            $"Đã tạo {payloads.Length:N0} payload từ {Path.GetFileName(clientDataPath)}.");

        return (options, payloads);
    }

    private static string ResolvePath(
        string baseDirectory,
        string fileName)
    {
        return
            Path.IsPathRooted(fileName)
                ? fileName
                : Path.Combine(baseDirectory, fileName);
    }

    private static void RegisterGlobalExceptionHandlers()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                ErrorLogger.LogFatal(
                    "AppDomain.CurrentDomain.UnhandledException",
                    ex,
                    e.IsTerminating);
            }
            else
            {
                ErrorLogger.LogMessage(
                    $"UnhandledException object: {e.ExceptionObject}, IsTerminating={e.IsTerminating}",
                    flushToDisk: true);
            }
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            ErrorLogger.LogException(
                "TaskScheduler.UnobservedTaskException",
                e.Exception,
                force: true);

            e.SetObserved();
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) =>
        {
            ErrorLogger.LogMessage(
                $"AppDomain.ProcessExit. ExitCode={Environment.ExitCode}",
                flushToDisk: true);
        };
    }
}
