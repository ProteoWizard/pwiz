using System;

namespace pwiz.Skyline.Model.Hibernate
{
    [QueryTable(TableType = TableType.summary)]
    [DatabindingTable(RootTable = typeof(Databinding.Entities.Transition), Property = "ResultSummary")]
    public class DbTransitionResultSummary : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbTransitionResultSummary); }
        }
        public virtual DbTransition Transition { get; set; }
        [DatabindingColumn(Name = "Transition.Precursor.ResultSummary")]
        public virtual DbPrecursorResultSummary PrecursorResultSummary { get; set; }
        [QueryColumn(IsHidden = true)]
        public virtual string ReplicatePath { get; set; }
        // Retention Time
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "RetentionTime.Min")]
        public virtual double? MinRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "RetentionTime.Max")]
        public virtual double? MaxRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "RetentionTime.Range")]
        public virtual double? RangeRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "RetentionTime.Mean")]
        public virtual double? MeanRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "RetentionTime.Stdev")]
        public virtual double? StdevRetentionTime { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "RetentionTime.Cv")]
        public virtual double? CvRetentionTime { get; set; }
        // FWHM
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "Fwhm.Mean")]
        public virtual double? MeanFwhm { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        [DatabindingColumn(Name = "Fwhm.Stdev")]
        public virtual double? StdevFwhm { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "Fwhm.Cv")]
        public virtual double? CvFwhm { get; set; }
        // Area
        [QueryColumn(Format = Formats.PEAK_AREA)]
        [DatabindingColumn(Name = "Area.Mean")]
        public virtual double? MeanArea { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        [DatabindingColumn(Name = "Area.Stdev")]
        public virtual double? StdevArea { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "Area.Cv")]
        public virtual double? CvArea { get; set; }
        // Ratio
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        [DatabindingColumn(Name = "AreaRatio.Mean")]
        public virtual double? MeanAreaRatio { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        [DatabindingColumn(Name = "AreaRatio.Stdev")]
        public virtual double? StdevAreaRatio { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "AreaRatio.Cv")]
        public virtual double? CvAreaRatio { get; set; }
        // Area Normalized (Area / Total Replicate Group Area)
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        [DatabindingColumn(Name = "AreaNormalized.Mean")]
        public virtual double? MeanAreaNormalized { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        [DatabindingColumn(Name = "AreaNormalized.Stdev")]
        public virtual double? StdevAreaNormalized { get; set; }
        [QueryColumn(Format = Formats.CV)]
        [DatabindingColumn(Name = "AreaNormalized.Cv")]
        public virtual double? CvAreaNormalized { get; set; }
    }
}