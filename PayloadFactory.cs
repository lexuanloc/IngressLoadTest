using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace IngressLoadTest;

public static class PayloadFactory
{
    private const long TimeMarker = 9223372036854775807L;

    private static readonly byte[] TimeMarkerPrefix =
        Encoding.UTF8.GetBytes("\"Time\":");

    private static readonly byte[] TimeMarkerValue =
        Encoding.UTF8.GetBytes(TimeMarker.ToString());

    private static readonly byte[] TimeMarkerFull =
        Encoding.UTF8.GetBytes(
            $"\"Time\":{TimeMarker}");

    public static PreparedPayload[] CreatePayloads(
        byte[] templatePayload,
        ClientIdentity[] clients)
    {
        JsonNode? rootNode = JsonNode.Parse(templatePayload);

        if (rootNode is not JsonObject template)
        {
            throw new InvalidOperationException(
                "payload.json phải là một JSON object.");
        }

        var payloads = new PreparedPayload[clients.Length];

        for (int i = 0; i < clients.Length; i++)
        {
            ClientIdentity client = clients[i];

            JsonObject payload =
                (JsonObject)template.DeepClone();

            payload["MXN"] = client.MXN;
            payload["BKS"] = client.BKS;
            payload["IMEI"] = client.IMEI;

            // Time luôn được thay bằng marker ở startup.
            // Runtime sẽ thay marker bằng Unix timestamp hiện tại
            // mà không parse/serialize lại JSON.
            payload["Time"] = TimeMarker;

            byte[] serialized =
                JsonSerializer.SerializeToUtf8Bytes(payload);

            payloads[i] =
                SplitAroundTimeMarker(serialized);
        }

        return payloads;
    }


    public static PreparedPayload[] CreatePayloadsWithoutClientOverride(
        byte[] templatePayload)
    {
        JsonNode? rootNode = JsonNode.Parse(templatePayload);

        if (rootNode is not JsonObject payload)
        {
            throw new InvalidOperationException(
                "payload.json phải là một JSON object.");
        }

        payload["Time"] = TimeMarker;

        byte[] serialized =
            JsonSerializer.SerializeToUtf8Bytes(payload);

        return
        [
            SplitAroundTimeMarker(serialized)
        ];
    }

    private static PreparedPayload SplitAroundTimeMarker(
        byte[] serialized)
    {
        ReadOnlySpan<byte> json = serialized;

        int markerIndex =
            json.IndexOf(TimeMarkerFull);

        if (markerIndex < 0)
        {
            throw new InvalidOperationException(
                "Không xác định được trường Time trong payload đã chuẩn bị.");
        }

        int timeValueOffset =
            markerIndex +
            TimeMarkerPrefix.Length;

        int suffixOffset =
            timeValueOffset +
            TimeMarkerValue.Length;

        byte[] prefix =
            json[..timeValueOffset].ToArray();

        byte[] suffix =
            json[suffixOffset..].ToArray();

        return new PreparedPayload(
            prefix,
            suffix);
    }
}
