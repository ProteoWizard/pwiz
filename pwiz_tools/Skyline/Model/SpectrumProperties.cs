using System.ComponentModel;
using System.IO;
using System.Resources;

namespace pwiz.Skyline.Model
{
    public class SpectrumProperties : GlobalizedObject
    {
        protected override ResourceManager GetResourceManager()
        {
            return SpectrumPropertiesResx.ResourceManager;
        }

        [Category("FileInfo")] public string IdFileName { get; set; }
        [Category("FileInfo")] public string FileName { get; set; }
        // need to exclude the file path from test assertions because it is machine-dependent
        [UseToCompare(false)] [Category("FileInfo")] public string FilePath { get; set; }
        [Category("FileInfo")] public string LibraryName { get; set; }
        [Category("PrecursorInfo")] public string PrecursorMz { get; set; }
        [Category("PrecursorInfo")] public int? Charge { get; set; }
        [Category("PrecursorInfo")] public string Label { get; set; }
        [Category("PrecursorInfo")] public string Adduct { get; set; } // Only shown for non-proteomic entries
        [Category("PrecursorInfo")] public string Formula { get; set; }  // Only shown for non-proteomic entries
        [Category("AcquisitionInfo")] public string RetentionTime { get; set; }
        [Category("AcquisitionInfo")] public string CCS { get; set; }
        [Category("AcquisitionInfo")] public string IonMobility { get; set; }
        [Category("AcquisitionInfo")] public string SpecIdInFile { get; set; }
        [Category("MatchInfo")] public double? Score { get; set; }
        [Category("MatchInfo")] public string ScoreType { get; set; }
        [Category("MatchInfo")] public int? SpectrumCount { get; set; }

        public void SetFileName(string fileName)
        {
            if (string.IsNullOrEmpty(Path.GetDirectoryName(fileName)))
                FileName = fileName;
            else
            {
                FilePath = fileName;
                FileName = Path.GetFileName(fileName);
            }
        }
    }
}