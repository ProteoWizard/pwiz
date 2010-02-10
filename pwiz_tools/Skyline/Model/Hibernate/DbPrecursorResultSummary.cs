using System;

namespace pwiz.Skyline.Model.Hibernate
{
    [QueryTable(TableType = TableType.summary)]
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
        public virtual double? MinBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MaxBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? RangeBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MeanBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? StdevBestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvBestRetentionTime { get; set; }
        // FWHM
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MeanMaxFwhm { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? StdevMaxFwhm { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvMaxFwhm { get; set; }
        // Area
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? MeanTotalArea { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? StdevTotalArea { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvTotalArea { get; set; }
        // Ratio
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? MeanTotalAreaRatio { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? StdevTotalAreaRatio { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvTotalAreaRatio { get; set; }
        // Area Normalized (Area / Total Replicate Group Area)
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        public virtual double? MeanTotalAreaNormalized { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        public virtual double? StdevTotalAreaNormalized { get; set; }
        [QueryColumn(Format = Formats.CV)]
        public virtual double? CvTotalAreaNormalized { get; set; }
    }
}