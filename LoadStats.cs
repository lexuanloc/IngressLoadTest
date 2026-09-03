using System.Diagnostics;

namespace IngressLoadTest;

public sealed class LoadStats
{
    private static readonly double[] LatencyBucketUpperMs =
    [
        0.5, 1, 2, 3, 5, 10, 20, 50, 100, 200, 500, 1000, 2000, 5000,
        double.PositiveInfinity
    ];

    private readonly int _firstPort;

    private readonly long[] _sentByPort;
    private readonly long[] _successByPort;
    private readonly long[] _errorByPort;
    private readonly long[] _latencyBuckets = new long[LatencyBucketUpperMs.Length];

    private long _totalSent;
    private long _totalSuccess;
    private long _totalError;

    private long _secondSent;
    private long _secondSuccess;
    private long _secondError;

    private long _totalLatencyTicks;
    private long _completedCount;
    private long _maxLatencyTicks;

    public LoadStats(int firstPort, int portCount)
    {
        _firstPort = firstPort;
        _sentByPort = new long[portCount];
        _successByPort = new long[portCount];
        _errorByPort = new long[portCount];
    }

    public void RecordSent(int portIndex)
    {
        Interlocked.Increment(ref _sentByPort[portIndex]);
        Interlocked.Increment(ref _totalSent);
        Interlocked.Increment(ref _secondSent);
    }

    public void RecordCompleted(int portIndex, bool success, long latencyTicks)
    {
        if (success)
        {
            Interlocked.Increment(ref _successByPort[portIndex]);
            Interlocked.Increment(ref _totalSuccess);
            Interlocked.Increment(ref _secondSuccess);
        }
        else
        {
            Interlocked.Increment(ref _errorByPort[portIndex]);
            Interlocked.Increment(ref _totalError);
            Interlocked.Increment(ref _secondError);
        }

        Interlocked.Add(ref _totalLatencyTicks, latencyTicks);
        Interlocked.Increment(ref _completedCount);

        UpdateMax(latencyTicks);
        ObserveLatency(latencyTicks);
    }

    public SecondSnapshot TakeSecondSnapshot()
    {
        return new SecondSnapshot
        {
            Sent = Interlocked.Exchange(ref _secondSent, 0),
            Success = Interlocked.Exchange(ref _secondSuccess, 0),
            Errors = Interlocked.Exchange(ref _secondError, 0)
        };
    }

    public FinalResult CreateFinalResult(TimeSpan actualDuration)
    {
        long completed = Interlocked.Read(ref _completedCount);
        long totalLatency = Interlocked.Read(ref _totalLatencyTicks);

        return new FinalResult
        {
            ActualDuration = actualDuration,
            TotalSent = Interlocked.Read(ref _totalSent),
            Success = Interlocked.Read(ref _totalSuccess),
            Errors = Interlocked.Read(ref _totalError),

            AverageLatencyMs =
                completed == 0
                    ? 0
                    : TicksToMilliseconds(totalLatency / (double)completed),

            P50Ms = GetPercentileMs(0.50),
            P95Ms = GetPercentileMs(0.95),
            P99Ms = GetPercentileMs(0.99),
            MaxLatencyMs = TicksToMilliseconds(Interlocked.Read(ref _maxLatencyTicks)),
            Ports = CreatePortResults()
        };
    }

    private void ObserveLatency(long latencyTicks)
    {
        double latencyMs = TicksToMilliseconds(latencyTicks);
        int bucketIndex = LatencyBucketUpperMs.Length - 1;

        for (int i = 0; i < LatencyBucketUpperMs.Length; i++)
        {
            if (latencyMs <= LatencyBucketUpperMs[i])
            {
                bucketIndex = i;
                break;
            }
        }

        Interlocked.Increment(ref _latencyBuckets[bucketIndex]);
    }

    private double GetPercentileMs(double percentile)
    {
        long total = Interlocked.Read(ref _completedCount);

        if (total <= 0)
        {
            return 0;
        }

        long target = (long)Math.Ceiling(total * percentile);
        long cumulative = 0;

        for (int i = 0; i < _latencyBuckets.Length; i++)
        {
            cumulative += Interlocked.Read(ref _latencyBuckets[i]);

            if (cumulative >= target)
            {
                double upper = LatencyBucketUpperMs[i];

                return double.IsPositiveInfinity(upper)
                    ? TicksToMilliseconds(Interlocked.Read(ref _maxLatencyTicks))
                    : upper;
            }
        }

        return TicksToMilliseconds(Interlocked.Read(ref _maxLatencyTicks));
    }

    private PortResult[] CreatePortResults()
    {
        var result = new PortResult[_sentByPort.Length];

        for (int i = 0; i < result.Length; i++)
        {
            result[i] = new PortResult
            {
                Port = _firstPort + i,
                Sent = Interlocked.Read(ref _sentByPort[i]),
                Success = Interlocked.Read(ref _successByPort[i]),
                Errors = Interlocked.Read(ref _errorByPort[i])
            };
        }

        return result;
    }

    private void UpdateMax(long value)
    {
        while (true)
        {
            long current = Interlocked.Read(ref _maxLatencyTicks);

            if (value <= current)
            {
                return;
            }

            long oldValue = Interlocked.CompareExchange(ref _maxLatencyTicks, value, current);

            if (oldValue == current)
            {
                return;
            }
        }
    }

    private static double TicksToMilliseconds(double ticks)
    {
        return ticks * 1000.0 / Stopwatch.Frequency;
    }
}

public sealed class SecondSnapshot
{
    public long Sent { get; init; }
    public long Success { get; init; }
    public long Errors { get; init; }
}

public sealed class FinalResult
{
    public TimeSpan ActualDuration { get; init; }

    public long TotalSent { get; init; }
    public long Success { get; init; }
    public long Errors { get; init; }

    public double AverageLatencyMs { get; init; }
    public double P50Ms { get; init; }
    public double P95Ms { get; init; }
    public double P99Ms { get; init; }
    public double MaxLatencyMs { get; init; }

    public PortResult[] Ports { get; init; } = [];
}

public sealed class PortResult
{
    public int Port { get; init; }
    public long Sent { get; init; }
    public long Success { get; init; }
    public long Errors { get; init; }
}
