using System.Data.SQLite;
using Pwiz.Data.Common.Cv;

#pragma warning disable CA1707

namespace Pwiz.Vendor.Bruker;

/// <summary>Bruker <c>chromatography-data.sqlite</c> trace types.</summary>
public enum TraceType
{
    /// <summary>Uninitialized / placeholder.</summary>
    NoneTrace = 0,
    /// <summary>Derived from MS data (BPC, TIC).</summary>
    ChromMS = 1,
    /// <summary>UV / absorbance trace.</summary>
    ChromUV = 3,
    /// <summary>LC pump pressure.</summary>
    ChromPressure = 4,
    /// <summary>Solvent mix fraction (e.g. "% A").</summary>
    ChromSolventMix = 5,
    /// <summary>LC flow rate.</summary>
    ChromFlow = 6,
    /// <summary>Oven / tray temperature.</summary>
    ChromTemperature = 7,
    /// <summary>Instrument-specific user-defined trace.</summary>
    ChromUserDefined = 9999,
}

/// <summary>Bruker <c>chromatography-data.sqlite</c> trace units (mirror of C++ <c>TraceUnit</c>).</summary>
public enum TraceUnit
{
#pragma warning disable CS1591
    NoneUnit = 0,
    Length_nm = 1,
    Flow_mul_min = 2,
    Pressure_bar = 3,
    Percent = 4,
    Temperature_C = 5,
    Intensity = 6,
    UnknownUnit = 7,
    Absorbance_AU = 8,
    Absorbance_mAU = 9,
    Counts = 10,
    Current_A = 11,
    Current_mA = 12,
    Current_muA = 13,
    Flow_ml_min = 14,
    Flow_nl_min = 15,
    Length_cm = 16,
    Length_mm = 17,
    Length_mum = 18,
    Luminescence = 20,
    Molarity_mM = 21,
    Power_W = 22,
    Power_mW = 23,
    Pressure_mbar = 24,
    Pressure_kPa = 25,
    Pressure_MPa = 26,
    Pressure_psi = 27,
    RefractiveIndex = 28,
    Temperature_F = 30,
    Time_h = 31,
    Time_min = 32,
    Time_s = 33,
    Time_ms = 34,
    Time_mus = 35,
    Viscosity_cP = 36,
    Voltage_kV = 37,
    Voltage_V = 38,
    Voltage_mV = 39,
    Volume_l = 40,
    Volume_ml = 41,
    Volume_mul = 42,
    Energy_J = 43,
    Energy_mJ = 44,
    Energy_muJ = 45,
    Length_Angstrom = 46,
#pragma warning restore CS1591
}

/// <summary>
/// One LC trace pulled from <c>chromatography-data.sqlite</c>: a <see cref="TraceType"/> /
/// <see cref="TraceUnit"/> pair plus pre-concatenated time / intensity arrays (chunks merged in
/// ROWID order, <c>TimeOffset</c> already applied).
/// </summary>
public sealed class LcTrace
{
    /// <summary>Raw trace description string (used to distinguish BPC from TIC within <see cref="TraceType.ChromMS"/>).</summary>
    public string Description { get; set; } = string.Empty;
    /// <summary>Instrument that produced the trace (e.g. "Elute Pump").</summary>
    public string Instrument { get; set; } = string.Empty;
    /// <summary>Trace type enum (controls chromatogram CVID).</summary>
    public TraceType Type { get; set; }
    /// <summary>Unit enum (controls unit CVID; also dictates value scaling).</summary>
    public TraceUnit Unit { get; set; }
    /// <summary>Retention times in seconds (TimeOffset already applied).</summary>
    public List<double> Times { get; } = new();
    /// <summary>Intensity / unit values (already scaled to fit the emitted <see cref="UnitCvid"/>).</summary>
    public List<double> Intensities { get; } = new();
    /// <summary>Chromatogram CVID derived from <see cref="Type"/> + <see cref="Description"/>.</summary>
    public CVID ChromatogramCvid { get; set; } = CVID.CVID_Unknown;
    /// <summary>Unit CVID derived from <see cref="Unit"/> after any required scaling.</summary>
    public CVID UnitCvid { get; set; } = CVID.CVID_Unknown;
}

/// <summary>
/// Reader for Bruker <c>chromatography-data.sqlite</c> — emits one <see cref="LcTrace"/> per
/// <c>TraceSources</c> row with all <c>TraceChunks</c> concatenated. Silently returns an empty
/// list when the database is missing or unreadable (match pwiz C++ try/catch).
/// </summary>
/// <remarks>Port of pwiz::msdata::detail::readChromatographyDataSqlite.</remarks>
public static class ChromatographyDataSqlite
{
    /// <summary>Reads all LC traces from <c>chromatography-data.sqlite</c> under <paramref name="dotDdirectory"/>.</summary>
    public static List<LcTrace> ReadAll(string dotDdirectory)
    {
        var results = new List<LcTrace>();
        string path = Path.Combine(dotDdirectory, "chromatography-data.sqlite");
        if (!File.Exists(path)) return results;

        try
        {
            using var conn = new SQLiteConnection($"Data Source={path};Read Only=True");
            conn.Open();

            var sourceIds = new List<int>();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "SELECT Id FROM TraceSources ORDER BY Id";
                using var r = cmd.ExecuteReader();
                while (r.Read()) sourceIds.Add(r.GetInt32(0));
            }

            foreach (int traceId in sourceIds)
                results.Add(ReadTrace(conn, traceId));
        }
        catch
        {
            // ignore errors, return whatever we collected — matches C++ behavior.
        }
        return results;
    }

    private static LcTrace ReadTrace(SQLiteConnection conn, int traceId)
    {
        var trace = new LcTrace();

        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText =
                "SELECT Type, Unit, Description, Instrument, TimeOffset FROM TraceSources WHERE Id=$id";
            cmd.Parameters.AddWithValue("$id", traceId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return trace;

            trace.Type = (TraceType)r.GetInt32(0);
            int rawUnit = r.GetInt32(1);
            trace.Description = r.IsDBNull(2) ? string.Empty : r.GetString(2);
            trace.Instrument = r.IsDBNull(3) ? string.Empty : r.GetString(3);
            double timeOffset = r.GetDouble(4);

            // Default NoneUnit → Intensity, with special-case for pressure [psi]/[bar] hints in description.
            if (rawUnit == (int)TraceUnit.NoneUnit)
            {
                rawUnit = (int)TraceUnit.Intensity;
                if (trace.Type == TraceType.ChromPressure)
                {
                    if (trace.Description.Contains("[psi]", StringComparison.Ordinal))
                        rawUnit = (int)TraceUnit.Pressure_psi;
                    else if (trace.Description.Contains("[bar]", StringComparison.OrdinalIgnoreCase))
                        rawUnit = (int)TraceUnit.Pressure_bar;
                }
            }
            trace.Unit = (TraceUnit)rawUnit;

            // Pull in all chunks (in ROWID order) before we commit to the final scaled series.
            LoadChunks(conn, traceId, trace.Times, trace.Intensities);

            if (timeOffset != 0.0)
                for (int i = 0; i < trace.Times.Count; i++) trace.Times[i] += timeOffset;
        }

        // traceUnitToCVID both resolves the unit CVID and scales the intensity list in place.
        trace.UnitCvid = TranslateTraceUnit(trace.Unit, trace.Intensities);
        trace.ChromatogramCvid = TranslateTraceType(trace.Type, trace.Unit, trace.Description);
        return trace;
    }

    private static void LoadChunks(SQLiteConnection conn, int traceId, List<double> times, List<double> intensities)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Times, Intensities FROM TraceChunks WHERE Trace=$id ORDER BY ROWID";
        cmd.Parameters.AddWithValue("$id", traceId);
        using var r = cmd.ExecuteReader();
        while (r.Read())
        {
            if (r.IsDBNull(0) || r.IsDBNull(1)) continue;
            long timesLen = r.GetBytes(0, 0, null, 0, 0);
            long intensitiesLen = r.GetBytes(1, 0, null, 0, 0);
            if (timesLen <= 0 || intensitiesLen <= 0) continue;

            var timeBytes = new byte[timesLen];
            _ = r.GetBytes(0, 0, timeBytes, 0, timeBytes.Length);
            var intensityBytes = new byte[intensitiesLen];
            _ = r.GetBytes(1, 0, intensityBytes, 0, intensityBytes.Length);

            int numTimes = timeBytes.Length / sizeof(double);
            int numIntensities = intensityBytes.Length / sizeof(float);

            for (int i = 0; i < numTimes; i++)
                times.Add(BitConverter.ToDouble(timeBytes, i * sizeof(double)));
            for (int i = 0; i < numIntensities; i++)
                intensities.Add(BitConverter.ToSingle(intensityBytes, i * sizeof(float)));
        }
    }

    private static CVID TranslateTraceType(TraceType type, TraceUnit unit, string description) => type switch
    {
        TraceType.NoneTrace => CVID.CVID_Unknown,
        TraceType.ChromMS => description.StartsWith("BPC", StringComparison.Ordinal)
            ? CVID.MS_basepeak_chromatogram
            : CVID.MS_TIC_chromatogram,
        TraceType.ChromUV => CVID.MS_absorption_chromatogram,
        TraceType.ChromPressure => CVID.MS_pressure_chromatogram,
        TraceType.ChromSolventMix => CVID.MS_chromatogram,
        TraceType.ChromFlow => CVID.MS_flow_rate_chromatogram,
        TraceType.ChromTemperature => CVID.MS_temperature_chromatogram,
        TraceType.ChromUserDefined => unit is TraceUnit.Temperature_C or TraceUnit.Temperature_F
            ? CVID.MS_temperature_chromatogram
            : CVID.MS_chromatogram,
        _ => CVID.CVID_Unknown,
    };

    private static CVID TranslateTraceUnit(TraceUnit unit, List<double> values)
    {
        switch (unit)
        {
            case TraceUnit.NoneUnit: return CVID.CVID_Unknown;
            case TraceUnit.Length_nm: return CVID.UO_nanometer;
            case TraceUnit.Flow_mul_min: return CVID.UO_microliters_per_minute;
            case TraceUnit.Pressure_bar: return CVID.UO_bar;
            case TraceUnit.Percent: return CVID.UO_percent;
            case TraceUnit.Temperature_C: return CVID.UO_degree_Celsius;
            case TraceUnit.Intensity: return CVID.MS_number_of_detector_counts;
            case TraceUnit.UnknownUnit: return CVID.CVID_Unknown;
            case TraceUnit.Absorbance_AU: return CVID.UO_absorbance_unit;
            case TraceUnit.Absorbance_mAU: Scale(values, 1.0 / 1000); return CVID.UO_absorbance_unit;
            case TraceUnit.Counts: return CVID.MS_number_of_detector_counts;
            case TraceUnit.Current_A: return CVID.UO_ampere;
            case TraceUnit.Current_mA: return CVID.UO_milliampere;
            case TraceUnit.Current_muA: return CVID.UO_microampere;
            case TraceUnit.Flow_ml_min: Scale(values, 1000.0); return CVID.UO_microliters_per_minute;
            case TraceUnit.Flow_nl_min: Scale(values, 1.0 / 1000); return CVID.UO_microliters_per_minute;
            case TraceUnit.Length_cm: return CVID.UO_centimeter;
            case TraceUnit.Length_mm: return CVID.UO_millimeter;
            case TraceUnit.Length_mum: return CVID.UO_micrometer;
            case TraceUnit.Luminescence: return CVID.CVID_Unknown;
            case TraceUnit.Molarity_mM: return CVID.UO_millimolar;
            case TraceUnit.Power_W: return CVID.UO_watt;
            case TraceUnit.Power_mW: Scale(values, 1.0 / 1000); return CVID.UO_watt;
            case TraceUnit.Pressure_mbar: Scale(values, 1.0 / 1000); return CVID.UO_bar;
            case TraceUnit.Pressure_kPa: Scale(values, 1000.0); return CVID.UO_pascal;
            case TraceUnit.Pressure_MPa: Scale(values, 1_000_000.0); return CVID.UO_pascal;
            case TraceUnit.Pressure_psi: return CVID.UO_pounds_per_square_inch;
            case TraceUnit.RefractiveIndex: return CVID.CVID_Unknown;
            case TraceUnit.Temperature_F: return CVID.UO_degree_Fahrenheit;
            case TraceUnit.Time_h: return CVID.UO_hour;
            case TraceUnit.Time_min: return CVID.UO_minute;
            case TraceUnit.Time_s: return CVID.UO_second;
            case TraceUnit.Time_ms: return CVID.UO_millisecond;
            case TraceUnit.Time_mus: return CVID.UO_microsecond;
            case TraceUnit.Viscosity_cP: Scale(values, 1.0 / 100); return CVID.UO_poise;
            case TraceUnit.Voltage_kV: return CVID.UO_kilovolt;
            case TraceUnit.Voltage_V: return CVID.UO_volt;
            case TraceUnit.Voltage_mV: return CVID.UO_millivolt;
            case TraceUnit.Volume_l: return CVID.UO_liter;
            case TraceUnit.Volume_ml: return CVID.UO_milliliter;
            case TraceUnit.Volume_mul: return CVID.UO_microliter;
            case TraceUnit.Energy_J: return CVID.UO_joule;
            case TraceUnit.Energy_mJ: Scale(values, 1.0 / 1000); return CVID.UO_joule;
            case TraceUnit.Energy_muJ: Scale(values, 1.0 / 1_000_000); return CVID.UO_joule;
            case TraceUnit.Length_Angstrom: return CVID.UO_angstrom;
            default: return CVID.CVID_Unknown;
        }
    }

    private static void Scale(List<double> values, double factor)
    {
        for (int i = 0; i < values.Count; i++) values[i] *= factor;
    }
}
