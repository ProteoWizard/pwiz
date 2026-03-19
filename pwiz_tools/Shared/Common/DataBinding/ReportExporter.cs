using System.IO;

namespace pwiz.Common.DataBinding
{
    public interface IReportExporter
    {
        void Export(Stream stream, RowItemEnumerator rowItemEnumerator);
    }

    public class DsvReportExporter : IReportExporter
    {
        public DsvReportExporter(DsvWriter dsvWriter)
        {
            DsvWriter = dsvWriter;
        }

        public DsvWriter DsvWriter { get; }

        public void Export(Stream stream, RowItemEnumerator rowItemEnumerator)
        {
            using var writer = new StreamWriter(stream);
            ExportToTextWriter(writer, rowItemEnumerator);
        }

        public void ExportToTextWriter(TextWriter writer, RowItemEnumerator rowItemEnumerator)
        {
            DsvWriter.ColumnFormats = rowItemEnumerator.ColumnFormats;
            WriteRows(writer, rowItemEnumerator);
        }

        protected virtual void WriteRows(TextWriter writer, RowItemEnumerator rowItemEnumerator)
        {
            DsvWriter.WriteHeaderRow(writer, rowItemEnumerator.ItemProperties);
            while (rowItemEnumerator.MoveNext())
            {
                DsvWriter.WriteDataRow(writer, rowItemEnumerator.Current, rowItemEnumerator.ItemProperties);
            }
        }
    }
}
