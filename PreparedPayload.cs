using System.Buffers.Text;

namespace IngressLoadTest;

/// <summary>
/// Payload đã được chuẩn bị sẵn ở startup.
///
/// Phần JSON trước và sau giá trị Time là immutable.
/// Khi gửi request, worker chỉ ghép:
///
/// Prefix + UnixTimeSeconds + Suffix
///
/// vào buffer riêng của worker.
/// </summary>
public sealed class PreparedPayload
{
    private const int MaxUnixTimestampBytes = 20;

    public PreparedPayload(
        byte[] prefix,
        byte[] suffix)
    {
        Prefix = prefix;
        Suffix = suffix;
    }

    public byte[] Prefix { get; }

    public byte[] Suffix { get; }

    public int MaxLength =>
        Prefix.Length +
        MaxUnixTimestampBytes +
        Suffix.Length;

    public int GetLength(long unixTimeSeconds)
    {
        Span<byte> timeBuffer = stackalloc byte[MaxUnixTimestampBytes];

        if (!Utf8Formatter.TryFormat(
            unixTimeSeconds,
            timeBuffer,
            out int timeLength))
        {
            throw new InvalidOperationException(
                "Không format được Unix timestamp.");
        }

        return Prefix.Length + timeLength + Suffix.Length;
    }

    public int WriteTo(
        Span<byte> destination,
        long unixTimeSeconds)
    {
        Prefix.AsSpan().CopyTo(destination);

        Span<byte> timeDestination =
            destination.Slice(
                Prefix.Length,
                MaxUnixTimestampBytes);

        if (!Utf8Formatter.TryFormat(
            unixTimeSeconds,
            timeDestination,
            out int timeLength))
        {
            throw new InvalidOperationException(
                "Không format được Unix timestamp.");
        }

        int suffixOffset = Prefix.Length + timeLength;

        Suffix.AsSpan().CopyTo(
            destination.Slice(suffixOffset));

        return suffixOffset + Suffix.Length;
    }
}
