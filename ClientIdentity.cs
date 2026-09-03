namespace IngressLoadTest;

public sealed class ClientIdentity
{
    public string MXN { get; init; } = string.Empty;
    public string BKS { get; init; } = string.Empty;
    public string IMEI { get; init; } = string.Empty;

    public override string ToString()
    {
        return $"{MXN}|{BKS}|{IMEI}";
    }
}

public static class ClientIdentityFile
{
    public static ClientIdentity[] Load(string fileName)
    {
        string[] lines = File.ReadAllLines(fileName);

        var result = new List<ClientIdentity>();
        var imeis = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < lines.Length; i++)
        {
            int lineNumber = i + 1;
            string line = lines[i].Trim();

            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            string[] parts = SplitLine(line);

            if (IsHeader(parts))
            {
                continue;
            }

            if (parts.Length != 3)
            {
                throw new InvalidOperationException(
                    $"ClientDataFile dòng {lineNumber} không hợp lệ. " +
                    "Định dạng phải là MXN|BKS|IMEI.");
            }

            string mxn = parts[0].Trim();
            string bks = parts[1].Trim();
            string imei = parts[2].Trim();

            if (string.IsNullOrWhiteSpace(mxn))
            {
                throw new InvalidOperationException(
                    $"ClientDataFile dòng {lineNumber}: MXN rỗng.");
            }

            if (string.IsNullOrWhiteSpace(bks))
            {
                throw new InvalidOperationException(
                    $"ClientDataFile dòng {lineNumber}: BKS rỗng.");
            }

            if (string.IsNullOrWhiteSpace(imei))
            {
                throw new InvalidOperationException(
                    $"ClientDataFile dòng {lineNumber}: IMEI rỗng.");
            }

            if (!imeis.Add(imei))
            {
                throw new InvalidOperationException(
                    $"ClientDataFile dòng {lineNumber}: IMEI bị trùng: {imei}");
            }

            result.Add(
                new ClientIdentity
                {
                    MXN = mxn,
                    BKS = bks,
                    IMEI = imei
                });
        }

        if (result.Count == 0)
        {
            throw new InvalidOperationException(
                "ClientDataFile không chứa client hợp lệ.");
        }

        return result.ToArray();
    }

    private static string[] SplitLine(string line)
    {
        char separator;

        if (line.Contains('|'))
        {
            separator = '|';
        }
        else if (line.Contains('\t'))
        {
            separator = '\t';
        }
        else if (line.Contains(';'))
        {
            separator = ';';
        }
        else
        {
            separator = ',';
        }

        return line.Split(
            separator,
            StringSplitOptions.TrimEntries);
    }

    private static bool IsHeader(string[] parts)
    {
        if (parts.Length != 3)
        {
            return false;
        }

        return
            parts[0].Equals("MXN", StringComparison.OrdinalIgnoreCase) &&
            parts[1].Equals("BKS", StringComparison.OrdinalIgnoreCase) &&
            parts[2].Equals("IMEI", StringComparison.OrdinalIgnoreCase);
    }
}
