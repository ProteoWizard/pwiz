using System;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Fragment tolerance configuration for LibCosine scoring.
    /// LibCosine uses direct peak matching with a tolerance window, NOT binning.
    /// Maps to osprey-core/src/config.rs FragmentToleranceConfig.
    /// </summary>
    public class FragmentToleranceConfig
    {
        /// <summary>Fragment m/z tolerance value.</summary>
        public double Tolerance { get; set; } = 10.0;

        /// <summary>Tolerance unit (ppm or Da).</summary>
        public ToleranceUnit Unit { get; set; } = ToleranceUnit.Ppm;

        /// <summary>Create default configuration (10 ppm for HRAM data).</summary>
        public static FragmentToleranceConfig Default()
        {
            return new FragmentToleranceConfig();
        }

        /// <summary>Create configuration for HRAM data (ppm-based).</summary>
        public static FragmentToleranceConfig Hram(double ppm)
        {
            return new FragmentToleranceConfig { Tolerance = ppm, Unit = ToleranceUnit.Ppm };
        }

        /// <summary>Create configuration for unit resolution data (Da-based).</summary>
        public static FragmentToleranceConfig UnitResolution(double da)
        {
            return new FragmentToleranceConfig { Tolerance = da, Unit = ToleranceUnit.Mz };
        }

        /// <summary>Calculate tolerance in Da for a given m/z.</summary>
        public double ToleranceDa(double mz)
        {
            return Unit == ToleranceUnit.Ppm ? mz * Tolerance / 1e6 : Tolerance;
        }

        /// <summary>Check if observed m/z is within tolerance of library m/z.</summary>
        public bool WithinTolerance(double libMz, double obsMz)
        {
            return Math.Abs(libMz - obsMz) <= ToleranceDa(libMz);
        }

        /// <summary>Calculate mass error in the configured unit.</summary>
        public double MassError(double libMz, double obsMz)
        {
            return Unit == ToleranceUnit.Ppm
                ? (obsMz - libMz) / libMz * 1e6
                : obsMz - libMz;
        }
    }
}
