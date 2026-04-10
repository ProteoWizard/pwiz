using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// Configuration settings for an Osprey analysis run.
    /// Maps to osprey-core/src/config.rs OspreyConfig.
    /// </summary>
    public class OspreyConfig
    {
        /// <summary>Input mzML file paths.</summary>
        public List<string> InputFiles { get; set; } = new List<string>();

        /// <summary>Spectral library source.</summary>
        public LibrarySource LibrarySource { get; set; }

        /// <summary>Primary output: blib for Skyline.</summary>
        public string OutputBlib { get; set; }

        /// <summary>Optional: TSV report output path.</summary>
        public string OutputReport { get; set; }

        /// <summary>Resolution mode for binning.</summary>
        public ResolutionMode ResolutionMode { get; set; } = ResolutionMode.Auto;

        /// <summary>Fragment tolerance for LibCosine scoring.</summary>
        public FragmentToleranceConfig FragmentTolerance { get; set; } = FragmentToleranceConfig.Default();

        /// <summary>Precursor tolerance for MS1 matching.</summary>
        public FragmentToleranceConfig PrecursorTolerance { get; set; } = FragmentToleranceConfig.Default();

        /// <summary>RT calibration configuration.</summary>
        public RTCalibrationConfig RtCalibration { get; set; } = new RTCalibrationConfig();

        /// <summary>Run-level FDR threshold.</summary>
        public double RunFdr { get; set; } = 0.01;

        /// <summary>Experiment-level FDR threshold.</summary>
        public double ExperimentFdr { get; set; } = 0.01;

        /// <summary>Decoy generation method.</summary>
        public DecoyMethod DecoyMethod { get; set; } = DecoyMethod.Reverse;

        /// <summary>Whether library already contains decoys.</summary>
        public bool DecoysInLibrary { get; set; }

        /// <summary>FDR method: native Percolator (default), external mokapot, or simple target-decoy.</summary>
        public FdrMethod FdrMethod { get; set; } = FdrMethod.Percolator;

        /// <summary>Write PIN files for external tools.</summary>
        public bool WritePin { get; set; }

        /// <summary>Inter-replicate peak reconciliation settings.</summary>
        public ReconciliationConfig Reconciliation { get; set; } = new ReconciliationConfig();

        /// <summary>Enable the coelution signal pre-filter.</summary>
        public bool PrefilterEnabled { get; set; } = true;

        /// <summary>Protein-level FDR threshold (enables protein parsimony and picked-protein FDR).</summary>
        public double? ProteinFdr { get; set; }

        /// <summary>How to handle shared peptides for protein inference.</summary>
        public SharedPeptideMode SharedPeptides { get; set; } = SharedPeptideMode.All;

        /// <summary>FDR filtering level for output.</summary>
        public FdrLevel FdrLevel { get; set; } = FdrLevel.Both;

        /// <summary>Number of threads to use.</summary>
        public int NThreads { get; set; } = Environment.ProcessorCount;

        /// <summary>
        /// Compute SHA-256 hash of parameters that affect first-pass scoring.
        /// If this hash changes, cached .scores.parquet files are invalid.
        /// </summary>
        public string SearchParameterHash()
        {
            using (var sha256 = SHA256.Create())
            {
                var sb = new StringBuilder();
                sb.AppendFormat("resolution_mode:{0}\n", ResolutionMode);
                sb.AppendFormat("fragment_tolerance:{0},{1}\n", FragmentTolerance.Tolerance, FragmentTolerance.Unit);
                sb.AppendFormat("precursor_tolerance:{0},{1}\n", PrecursorTolerance.Tolerance, PrecursorTolerance.Unit);
                sb.AppendFormat("prefilter_enabled:{0}\n", PrefilterEnabled);
                sb.AppendFormat("decoy_method:{0}\n", DecoyMethod);
                sb.AppendFormat("decoys_in_library:{0}\n", DecoysInLibrary);
                sb.AppendFormat("rt_cal.enabled:{0}\n", RtCalibration.Enabled);
                sb.AppendFormat("rt_cal.fallback_rt_tolerance:{0}\n", RtCalibration.FallbackRtTolerance);
                sb.AppendFormat("rt_cal.rt_tolerance_factor:{0}\n", RtCalibration.RtToleranceFactor);
                sb.AppendFormat("rt_cal.min_rt_tolerance:{0}\n", RtCalibration.MinRtTolerance);
                sb.AppendFormat("rt_cal.max_rt_tolerance:{0}\n", RtCalibration.MaxRtTolerance);
                sb.AppendFormat("rt_cal.loess_bandwidth:{0}\n", RtCalibration.LoessBandwidth);
                sb.AppendFormat("rt_cal.min_calibration_points:{0}\n", RtCalibration.MinCalibrationPoints);
                sb.AppendFormat("rt_cal.calibration_sample_size:{0}\n", RtCalibration.CalibrationSampleSize);
                sb.AppendFormat("rt_cal.calibration_retry_factor:{0}\n", RtCalibration.CalibrationRetryFactor);
                sb.AppendFormat("reconciliation.top_n_peaks:{0}\n", Reconciliation.TopNPeaks);

                byte[] hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                var result = new StringBuilder(64);
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    result.Append(hashBytes[i].ToString("x2"));
                }
                return result.ToString();
            }
        }
    }
}
