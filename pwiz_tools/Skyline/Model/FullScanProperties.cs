using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;

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
                if(InstrumentModel != null || InstrumentManufacturer != null)
                    return TextUtil.SpaceSeparate(InstrumentManufacturer, InstrumentModel);
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
                if(precursor.ChargeState != null)
                    res.Charge = spectrum.NegativeCharge ? @"-" : @"+" + precursor.ChargeState;
                if(precursor.IsolationMz != null && precursor.IsolationWindowLower != null && precursor.IsolationWindowUpper != null)
                    res.IsolationWindow = string.Format(@"{0}:{1} (-{2}:+{3})", 
                        (precursor.IsolationMz - precursor.IsolationWindowLower).Value.RawValue.ToString(Formats.Mz),
                        (precursor.IsolationMz + precursor.IsolationWindowUpper).Value.RawValue.ToString(Formats.Mz),
                        precursor.IsolationWindowLower.Value.ToString(Formats.Mz), 
                        precursor.IsolationWindowUpper.Value.ToString(Formats.Mz));
            }

            res.RetentionTime = spectrum.RetentionTime.HasValue ? spectrum.RetentionTime.Value.ToString(Formats.RETENTION_TIME) : null;

            res.MSLevel = spectrum.Level.ToString();
            res.ScanId = spectrum.Id;

            res.Instrument = new InstrumentInfo();
            res.Instrument.InstrumentManufacturer = spectrum.InstrumentVendor;
            if (spectrum.InstrumentInfo != null)
            {
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

            res.IsCentroided = spectrum.Centroided.ToString(CultureInfo.CurrentCulture);
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
        [Category("PrecursorInfo")] public string IsolationWindow { get; set; }
        [Category("AcquisitionInfo")] public string IonMobilityRange { get; set; }
        [Category("AcquisitionInfo")] public string IonMobilityFilterRange { get; set; }
        [Category("PrecursorInfo")] public string HighEnergyOffset { get; set; }
        [Category("AcquisitionInfo")] public string ScanId { get; set; }
        [Category("AcquisitionInfo")] public string CE { get; set; }
        [Category("AcquisitionInfo")] public string MSLevel { get; set; }
        [Category("AcquisitionInfo")] public InstrumentInfo Instrument { get; set; }
        [Category("AcquisitionInfo")] public string DataPoints { get; set; }
        [Category("AcquisitionInfo")] public string MzCount { get; set; }
        [Category("AcquisitionInfo")] public string IonMobilityCount { get; set; }
        [Category("AcquisitionInfo")] public string InjectionTime { get; set; }
        [Category("AcquisitionInfo")] public string IsCentroided { get; set; }
        [Category("MatchInfo")] public string dotp { get; set; }
        [Category("MatchInfo")] public string idotp { get; set; }
        [Category("MatchInfo")] public string rdotp { get; set; }

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