// Port of pwiz_tools/BiblioSpec/src/SpecData.h

namespace Pwiz.Tools.BiblioSpec;

/// <summary>
/// Lean spectrum holder used by <see cref="ISpecFileReader"/> implementations to return
/// peak data plus precursor metadata. Unlike <see cref="Spectrum"/>, this type is a
/// flat data carrier (no processing, no caches) and uses parallel arrays for peaks so
/// they can be handed straight to the SQLite blob writer.
/// </summary>
/// <remarks>
/// <para>Port of <c>BiblioSpec::SpecData</c> (SpecData.h:29). The cpp struct uses raw
/// pointers (<c>double*</c>, <c>float*</c>) and a manual destructor; we use C# arrays
/// instead, with <c>null</c> standing in for the cpp <c>NULL</c> sentinel — in
/// particular, <see cref="ProductIonMobilities"/> is <c>null</c> for non-Waters data.</para>
/// <para>cpp parity: <see cref="NumPeaks"/> defaults to <c>-1</c> (cpp SpecData.h:46),
/// distinct from "spectrum found but empty" (<c>0</c>).</para>
/// </remarks>
public class SpecData
{
    /// <summary>Spectrum identifier (e.g. scan number; reader-specific meaning).</summary>
    public int Id { get; set; }

    /// <summary>Precursor ion mobility.</summary>
    public float IonMobility { get; set; }

    /// <summary>Units of <see cref="IonMobility"/>.</summary>
    public IonMobilityType IonMobilityType { get; set; } = IonMobilityType.None;

    /// <summary>Collisional cross section.</summary>
    public float Ccs { get; set; }

    /// <summary>Retention time in minutes.</summary>
    public double RetentionTime { get; set; }

    /// <summary>Start of the RT window for this spectrum (DIA).</summary>
    public double StartTime { get; set; }

    /// <summary>End of the RT window for this spectrum (DIA).</summary>
    public double EndTime { get; set; }

    /// <summary>Sum of intensities across all peaks.</summary>
    public double TotalIonCurrent { get; set; }

    /// <summary>Precursor m/z (Th).</summary>
    public double Mz { get; set; }

    /// <summary>Precursor charge state. For multi-charge queries (e.g. MS2 with two Z lines)
    /// this is the first; use <see cref="Charges"/> for the full set.</summary>
    public int Charge { get; set; }

    /// <summary>All possible charges from the source file (cpp parity: Spectrum::getPossibleCharges).
    /// Populated by spec readers that surface multiple MS_possible_charge_state CV params;
    /// empty when only a single charge_state is present (BlibSearch falls back to <see cref="Charge"/>).</summary>
    public List<int> Charges { get; } = new();

    /// <summary>Number of peaks, or <c>-1</c> for "not yet populated" (cpp SpecData.h:46 default).</summary>
    public int NumPeaks { get; set; } = -1;

    /// <summary>Peak m/z values. <c>null</c> until <see cref="NumPeaks"/> &gt; 0.</summary>
    public double[]? Mzs { get; set; }

    /// <summary>Peak intensities (parallel to <see cref="Mzs"/>).</summary>
    public float[]? Intensities { get; set; }

    /// <summary>
    /// Per-product-ion ion mobilities (Waters Mse — product ions are accelerated after
    /// the drift tube). <c>null</c> for instruments that don't supply per-peak drift offsets.
    /// </summary>
    public float[]? ProductIonMobilities { get; set; }

    /// <summary>
    /// In Waters machines, product ions have kinetic energy added after the drift tube and
    /// thus fly slightly faster than the precursor from there to the detector. Returns
    /// the average product ion mobility minus the precursor's ion mobility, or 0 if no
    /// product-ion mobilities are available.
    /// </summary>
    /// <remarks>cpp parity: SpecData.h:93 <c>getIonMobilityHighEnergyOffset</c>.</remarks>
    public double GetIonMobilityHighEnergyOffset()
    {
        if (ProductIonMobilities != null && NumPeaks > 0)
        {
            double sum = 0;
            for (var i = 0; i < NumPeaks; i++)
                sum += ProductIonMobilities[i];
            if (sum > 0)
                return (sum / NumPeaks) - IonMobility;
        }
        return 0;
    }

    /// <summary>
    /// Copy the fields and (deeply) the peak arrays from <paramref name="other"/> into this instance.
    /// </summary>
    /// <remarks>
    /// cpp parity: SpecData.h:58 <c>operator=</c>. The cpp version frees existing arrays and
    /// re-allocates fresh ones; in C# we just replace the array references (GC handles the rest).
    /// </remarks>
    public void CopyFrom(SpecData other)
    {
        ArgumentNullException.ThrowIfNull(other);
        Id = other.Id;
        IonMobility = other.IonMobility;
        IonMobilityType = other.IonMobilityType;
        Ccs = other.Ccs;
        RetentionTime = other.RetentionTime;
        StartTime = other.StartTime;
        EndTime = other.EndTime;
        TotalIonCurrent = other.TotalIonCurrent;
        Mz = other.Mz;
        Charge = other.Charge;
        NumPeaks = other.NumPeaks;

        if (NumPeaks > 0)
        {
            Mzs = new double[NumPeaks];
            Intensities = new float[NumPeaks];
            ProductIonMobilities = other.ProductIonMobilities is null ? null : new float[NumPeaks];
            for (var i = 0; i < NumPeaks; i++)
            {
                Mzs[i] = other.Mzs![i];
                Intensities[i] = other.Intensities![i];
                if (other.ProductIonMobilities != null)
                    ProductIonMobilities![i] = other.ProductIonMobilities[i];
            }
        }
        else
        {
            Mzs = null;
            Intensities = null;
            ProductIonMobilities = null;
        }
    }
}
