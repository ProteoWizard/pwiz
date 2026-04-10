using System;
using System.IO;
using Newtonsoft.Json;

namespace pwiz.OspreySharp.Chromatography
{
    /// <summary>
    /// Calibration file I/O for saving and loading calibration parameters as JSON.
    /// Maps to osprey-chromatography/src/calibration/io.rs.
    /// </summary>
    public static class CalibrationIO
    {
        /// <summary>
        /// Save calibration parameters to a JSON file.
        /// </summary>
        /// <param name="calibration">Calibration parameters to save.</param>
        /// <param name="path">Path to output JSON file.</param>
        public static void SaveCalibration(CalibrationParams calibration, string path)
        {
            if (calibration == null)
                throw new ArgumentNullException("calibration");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path must not be null or empty", "path");

            string json = JsonConvert.SerializeObject(calibration, Formatting.Indented);
            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Load calibration parameters from a JSON file.
        /// </summary>
        /// <param name="path">Path to calibration JSON file.</param>
        /// <returns>Loaded calibration parameters.</returns>
        public static CalibrationParams LoadCalibration(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path must not be null or empty", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException("Calibration file not found: " + path, path);

            string json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<CalibrationParams>(json);
        }

        /// <summary>
        /// Generate calibration filename from base output name.
        /// Example: "results" -> "results.calibration.json"
        /// </summary>
        public static string CalibrationFilename(string baseName)
        {
            return baseName + ".calibration.json";
        }

        /// <summary>
        /// Generate calibration filename from input file path.
        /// Example: "/data/sample.mzML" -> "sample.calibration.json"
        /// </summary>
        public static string CalibrationFilenameForInput(string inputPath)
        {
            string stem = Path.GetFileNameWithoutExtension(inputPath);
            if (string.IsNullOrEmpty(stem))
                stem = "unknown";
            return stem + ".calibration.json";
        }

        /// <summary>
        /// Get the full path for a calibration file based on input file and output directory.
        /// </summary>
        public static string CalibrationPathForInput(string inputPath, string outputDir)
        {
            string filename = CalibrationFilenameForInput(inputPath);
            return Path.Combine(outputDir, filename);
        }
    }
}
