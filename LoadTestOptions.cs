using System.Text.Json;

namespace IngressLoadTest;

public sealed class LoadTestOptions
{
    public string Server { get; set; } = "127.0.0.1";
    public int FirstPort { get; set; } = 2030;
    public int PortCount { get; set; } = 20;
    public string Path { get; set; } = "/v1/receive";

    public int TargetRps { get; set; } = 6000;
    public int WarmupSeconds { get; set; } = 10;
    public int DurationSeconds { get; set; } = 3600;

    public int WorkerCount { get; set; } = 512;
    public int MaxConnectionsPerServer { get; set; } = 128;
    public int RequestTimeoutSeconds { get; set; } = 5;
    public int ChannelCapacity { get; set; } = 100000;

    public bool RestartOnUnexpectedError { get; set; } = true;
    public int RestartDelaySeconds { get; set; } = 5;

    public string PayloadFile { get; set; } = "payload.json";

    public static LoadTestOptions Load(string fileName)
    {
        string json = File.ReadAllText(fileName);

        LoadTestOptions? options = JsonSerializer.Deserialize<LoadTestOptions>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

        if (options == null)
        {
            throw new InvalidOperationException("Không đọc được appsettings.json.");
        }

        options.Validate();
        return options;
    }

    private void Validate()
    {
        if (string.IsNullOrWhiteSpace(Server))
        {
            throw new InvalidOperationException("Server không hợp lệ.");
        }

        if (FirstPort <= 0 || FirstPort > 65535)
        {
            throw new InvalidOperationException("FirstPort không hợp lệ.");
        }

        if (PortCount <= 0 || FirstPort + PortCount - 1 > 65535)
        {
            throw new InvalidOperationException("PortCount không hợp lệ.");
        }

        if (!Path.StartsWith('/'))
        {
            Path = "/" + Path;
        }

        if (TargetRps <= 0)
        {
            throw new InvalidOperationException("TargetRps phải > 0.");
        }

        if (WarmupSeconds < 0)
        {
            throw new InvalidOperationException("WarmupSeconds phải >= 0.");
        }

        if (DurationSeconds <= 0)
        {
            throw new InvalidOperationException("DurationSeconds phải > 0.");
        }

        if (WorkerCount <= 0)
        {
            throw new InvalidOperationException("WorkerCount phải > 0.");
        }

        if (MaxConnectionsPerServer <= 0)
        {
            throw new InvalidOperationException("MaxConnectionsPerServer phải > 0.");
        }

        if (RequestTimeoutSeconds <= 0)
        {
            throw new InvalidOperationException("RequestTimeoutSeconds phải > 0.");
        }

        if (ChannelCapacity <= 0)
        {
            throw new InvalidOperationException("ChannelCapacity phải > 0.");
        }

        if (RestartDelaySeconds < 1)
        {
            throw new InvalidOperationException("RestartDelaySeconds phải >= 1.");
        }

        if (string.IsNullOrWhiteSpace(PayloadFile))
        {
            throw new InvalidOperationException("PayloadFile không hợp lệ.");
        }
    }
}
