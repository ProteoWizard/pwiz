using System;
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Hibernate
{
    [QueryTable(TableType = TableType.summary)]
    [DatabindingTable(RootTable = typeof(Precursor), Property = "ResultSummary")]
    public class DbPrecursorResultSummary : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof (DbPrecursorResultSummary); }
        }
        public virtual DbPrecursor Precursor { get; set; }
        [QueryColumn(IsHidden = true)]
        public virtual string ReplicatePath { get; set; }
        // RetentionTime
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "BestRetentionTime.Min")]
        public virtual double? MinBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "BestRetentionTime.Max")]
        public virtual double? MaxBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "BestRetentionTime.Range")]
        public virtual double? RangeBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "BestRetentionTime.Mean")]
        public virtual double? MeanBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "BestRetentionTime.Stdev")]
        public virtual double? StdevBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "BestRetentionTime.Cv")]
        public virtual double? CvBestRetentionTime { get; set; }
        // FWHM
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "MaxFwhm.Mean")]
        public virtual double? MeanMaxFwhm { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "MaxFwhm.Stdev")]
        public virtual double? StdevMaxFwhm { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "MaxFwhm.Cv")]
        public virtual double? CvMaxFwhm { get; set; }
        // Area
        [QueryColumn(Format = Formats.PEAK_AREA)]
        [DatabindingColumn(Name = "TotalArea.Mean")]
        public virtual double? MeanTotalArea { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        [DatabindingColumn(Name = "TotalArea.Stdev")]
        public virtual double? StdevTotalArea { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "TotalArea.Cv")]
        public virtual double? CvTotalArea { get; set; }
        // Ratio
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        [DatabindingColumn(Name = "TotalAreaRatio.Mean")]
        public virtual double? MeanTotalAreaRatio { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        [DatabindingColumn(Name = "TotalAreaRatio.Stdev")]
        public virtual double? StdevTotalAreaRatio { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "TotalAreaRatio.Cv")]
        public virtual double? CvTotalAreaRatio { get; set; }
        // Area Normalized (Area / Total Replicate Group Area)
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        [DatabindingColumn(Name = "TotalAreaNormalized.Mean")]
        public virtual double? MeanTotalAreaNormalized { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        [DatabindingColumn(Name = "TotalAreaNormalized.Stdev")]
        public virtual double? StdevTotalAreaNormalized { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "TotalAreaNormalized.Cv")]
        public virtual double? CvTotalAreaNormalized { get; set; }
        // Height
        [QueryColumn(Format = Formats.PEAK_AREA)]
        [DatabindingColumn(Name="MaxHeight.Mean")]
        public virtual double? MeanMaxHeight { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        [DatabindingColumn(Name="MaxHeight.Stdev")]
        public virtual double? StdevMaxHeight { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name="MaxHeight.Cv")]
        public virtual double? CvMaxHeight { get; set; }
    }
}