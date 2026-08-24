namespace Pwiz.Data.MsData.MSn;

/// <summary>
/// 16 fixed-size header lines emitted at the start of every binary MSn file (BMS1/CMS1/BMS2/CMS2).
/// Port of pwiz::msdata::MSnHeader (a <c>char[16][128]</c> in cpp).
/// </summary>
/// <remarks>
/// Each slot is exactly 128 bytes on disk; cpp populates the first four lines with build info
/// (CreationDate, Extractor, Extractor version, Source file) and leaves the rest zeroed. We
/// read 16 * 128 bytes off the wire and decode each slot up to the first NUL.
/// </remarks>
public sealed class MSnHeader
{
    /// <summary>Number of header slots written.</summary>
    public const int SlotCount = 16;

    /// <summary>Bytes per header slot.</summary>
    public const int SlotBytes = 128;

    /// <summary>Total header size on disk (<see cref="SlotCount"/> × <see cref="SlotBytes"/>).</summary>
    public const int TotalBytes = SlotCount * SlotBytes;

    /// <summary>The 16 fixed-size header lines. Indexes past last-populated are empty strings.</summary>
    public string[] Lines { get; } = new string[SlotCount];

    /// <summary>Creates an empty header (all slots = "").</summary>
    public MSnHeader()
    {
        for (int i = 0; i < SlotCount; i++) Lines[i] = string.Empty;
    }

    /// <summary>Reads the binary header from <paramref name="r"/>.</summary>
    public static MSnHeader Read(BinaryReader r)
    {
        ArgumentNullException.ThrowIfNull(r);
        byte[] buf = r.ReadBytes(TotalBytes);
        if (buf.Length < TotalBytes)
            throw new EndOfStreamException("Truncated MSn binary header.");
        var header = new MSnHeader();
        for (int i = 0; i < SlotCount; i++)
        {
            int start = i * SlotBytes;
            int len = 0;
            while (len < SlotBytes && buf[start + len] != 0) len++;
            header.Lines[i] = System.Text.Encoding.ASCII.GetString(buf, start, len);
        }
        return header;
    }

    /// <summary>Writes 16 × 128 = 2048 bytes of header, padding each slot with NUL.</summary>
    public void Write(BinaryWriter w)
    {
        ArgumentNullException.ThrowIfNull(w);
        byte[] slot = new byte[SlotBytes];
        for (int i = 0; i < SlotCount; i++)
        {
            Array.Clear(slot, 0, SlotBytes);
            string s = Lines[i] ?? string.Empty;
            int n = Math.Min(System.Text.Encoding.ASCII.GetByteCount(s), SlotBytes - 1);
            if (n > 0)
                System.Text.Encoding.ASCII.GetBytes(s, 0, Math.Min(s.Length, n), slot, 0);
            w.Write(slot, 0, SlotBytes);
        }
    }
}
