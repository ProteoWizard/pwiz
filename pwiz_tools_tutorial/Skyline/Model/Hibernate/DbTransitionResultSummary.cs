using System;

namespace pwiz.Skyline.Model.Hibernate
{
    [QueryTable(TableType = TableType.summary)]
    public class DbTransitionResultSummary : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbTransitionResultSummary); }
        }
        public virtual DbTransition Transition { get; set; }
        public virtual DbPrecursorResultSummary PrecursorResultSummary { get; set; }
        [QueryColumn(IsHidden = true)]
        public virtual string ReplicatePath { get; set; }
        // Retention Time
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MinRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MaxRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? RangeRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MeanRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? StdevRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? CvRetentionTime { get; set; }
        // FWHM
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MeanFwhm { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? StdevFwhm { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvFwhm { get; set; }
        // Area
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? MeanArea { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? StdevArea { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvArea { get; set; }
        // Ratio
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? MeanAreaRatio { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? StdevAreaRatio { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvAreaRatio { get; set; }
        // Area Normalized (Area / Total Replicate Group Area)
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        public virtual double? MeanAreaNormalized { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        public virtual double? StdevAreaNormalized { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvAreaNormalized { get; set; }
    }
}