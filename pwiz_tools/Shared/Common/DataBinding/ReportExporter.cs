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
            DsvWriter.ColumnFormats = rowItemEnumerator.ColumnFormats;
            Write(writer, rowItemEnumerator);
        }

        public virtual void Write(TextWriter writer, RowItemEnumerator rowItemEnumerator)
        {
            DsvWriter.WriteHeaderRow(writer, rowItemEnumerator.ItemProperties);
            while (rowItemEnumerator.MoveNext())
            {
                DsvWriter.WriteDataRow(writer, rowItemEnumerator.Current, rowItemEnumerator.ItemProperties);
            }

        }

    }
}
