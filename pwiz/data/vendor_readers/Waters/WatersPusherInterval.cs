using System;
using System.Globalization;
using System.IO;

namespace Pwiz.Vendor.Waters;

/// <summary>
/// Derives the Waters MSE ion-mobility pusher interval (ms) from a <c>.raw</c> directory, for
/// BiblioSpec's WatersMseReader when no pusher interval was forced on the command line. Mirrors
/// pwiz C++ <c>WatersMseReader.cpp</c>: open the sibling <c>.raw</c> and compute
/// <c>1000 / TRANSPORT_RF</c>, where TRANSPORT_RF is a per-function scan stat. Kept as a small
/// public entry point so BiblioSpec does not need the internal <see cref="WatersRawFile"/>.
/// </summary>
public static class WatersPusherInterval
{
    // MassLynxScanItem::TRANSPORT_RF = SCAN_ITEM_BASE(400) + 1 (LINEAR_DETECTOR_VOLTAGE) + 75,
    // per the Waters SDK MassLynxRawDefs.h.
    private const int TransportRfScanItem = 476;

    /// <summary>
    /// Returns the pusher interval in ms derived from the <c>.raw</c>'s TRANSPORT_RF scan stat, or
    /// 0 if the raw cannot be read (e.g. the vendor SDK is unavailable) or the stat is missing.
    /// </summary>
    public static double FromRawFile(string rawPath)
    {
        try
        {
            if (string.IsNullOrEmpty(rawPath) || !Directory.Exists(rawPath))
                return 0;
            using var raw = new WatersRawFile(rawPath);
            var functions = raw.FunctionIndices;
            if (functions.Count == 0)
                return 0;
            var value = raw.GetScanItem(functions[0], 0, TransportRfScanItem);
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var transportRf)
                && transportRf != 0)
                return 1000.0 / transportRf;
            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
