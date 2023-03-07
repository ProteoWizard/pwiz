using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Resources;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Hibernate;

namespace pwiz.Skyline.Model
{
    public class FullScanProperties : GlobalizedObject
    {
        [TypeConverter(typeof(ExpandableObjectConverter))]
        public class InstrumentInfo : GlobalizedObject
        {
            [TypeConverter(typeof(ExpandableObjectConverter))]
            public class InstrumentComponentsInfo : GlobalizedObject
            {
                protected override ResourceManager GetResourceManager()
                {
                    return FullScanPropertiesRes.ResourceManager;
                }
                [NotifyParentProperty(true)][Category("InstrumentInfo")] public string Ionization { get; set; }
                [NotifyParentProperty(true)][Category("InstrumentInfo")] public string Analyzer { get; set; }
                [NotifyParentProperty(true)][Category("InstrumentInfo")] public string Detector { get; set; }

                public override string ToString()
                {
                    return Analyzer;
                }
            }

            protected override ResourceManager GetResourceManager()
            {
                return FullScanPropertiesRes.ResourceManager;
            }
            [NotifyParentProperty(true)][Category("InstrumentInfo")] public string InstrumentSerialNumber { get; set; }
            [NotifyParentProperty(true)][Category("InstrumentInfo")] public string InstrumentModel { get; set; }
            [NotifyParentProperty(true)][Category("InstrumentInfo")] public string InstrumentManufacturer { get; set; }
            [NotifyParentProperty(true)][Category("InstrumentInfo")] public InstrumentComponentsInfo InstrumentComponents { get; set; }

            public override string ToString()
            {
                if(InstrumentModel != null)
                    return InstrumentModel;
                return base.ToString();
            }
        } 
        protected override ResourceManager GetResourceManager()
        {
            return FullScanPropertiesRes.ResourceManager;
        }

        public static FullScanProperties CreateProperties(MsDataSpectrum spectrum)
        {
            if(spectrum == null)
                return null;
            var res = new FullScanProperties();
            res.FilePath = spectrum.SourceFilePath;
            res.FileName = Path.GetFileName(spectrum.SourceFilePath);
            if (spectrum.PrecursorsByMsLevel.Any())
            {
                var precursor = spectrum.Precursors.FirstOrDefault();
                res.PrecursorMz = precursor.PrecursorMz.HasValue ? precursor.PrecursorMz.Value.Value.ToString(Formats.Mz) : null;
                res.Charge = spectrum.NegativeCharge ? @"-" : @"+" + precursor.ChargeState;
            }

            res.RetentionTime = spectrum.RetentionTime.HasValue ? spectrum.RetentionTime.Value.ToString(Formats.RETENTION_TIME) : null;

            res.MSStage = spectrum.Level.ToString();
            res.ScanId = spectrum.Id;

            if (spectrum.InstrumentInfo != null)
            {
                res.Instrument = new InstrumentInfo();
                res.Instrument.InstrumentModel = spectrum.InstrumentInfo.Model;
                if (new[]
                    {
                        spectrum.InstrumentInfo.Ionization, 
                        spectrum.InstrumentInfo.Analyzer,
                        spectrum.InstrumentInfo.Detector
                    }.Any( s => !string.IsNullOrEmpty(s)))
                {
                    res.Instrument.InstrumentComponents = new InstrumentInfo.InstrumentComponentsInfo();
                    res.Instrument.InstrumentComponents.Ionization = spectrum.InstrumentInfo.Ionization;
                    res.Instrument.InstrumentComponents.Analyzer = spectrum.InstrumentInfo.Analyzer;
                    res.Instrument.InstrumentComponents.Detector = spectrum.InstrumentInfo.Detector;
                }
            }

            res.Instrument.InstrumentSerialNumber = spectrum.InstrumentSerialNumber;
            return res;
        }
        [Category("FileInfo")] public string FileName { get; set; }
        // need to exclude the file path from test assertions because it is machine-dependent
        [UseToCompare(false)] [Category("FileInfo")] public string FilePath { get; set; }
        [Category("FileInfo")] public string ReplicateName { get; set; }
        [Category("PrecursorInfo")] public string PrecursorMz { get; set; }
        [Category("PrecursorInfo")] public string Charge { get; set; }
        [Category("PrecursorInfo")] public string Label { get; set; }
        [Category("PrecursorInfo")] public string RetentionTime { get; set; }
        [Category("PrecursorInfo")] public string CCS { get; set; }
        [Category("PrecursorInfo")] public string IonMobility { get; set; }
        [Category("AcquisitionInfo")] public string IonMobilityRange { get; set; }
        [Category("AcquisitionInfo")] public string IonMobilityFilterRange { get; set; }
        [Category("AcquisitionInfo")] public string ScanId { get; set; }
        [Category("AcquisitionInfo")] public string CE { get; set; }
        [Category("AcquisitionInfo")] public string MSStage { get; set; }
        [Category("AcquisitionInfo")] public InstrumentInfo Instrument { get; set; }
        [Category("AcquisitionInfo")] public string IsolationWindow { get; set; }
        [Category("AcquisitionInfo")] public int? DataPoints { get; set; }
        [Category("AcquisitionInfo")] public int? MzCount { get; set; }
        [Category("AcquisitionInfo")] public int? IonMobilityCount { get; set; }
        [Category("AcquisitionInfo")] public string InjectionTime { get; set; }
        [Category("MatchInfo")] public double? dotp { get; set; }
        [Category("MatchInfo")] public double? idotp { get; set; }
        [Category("MatchInfo")] public double? rdotp { get; set; }

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