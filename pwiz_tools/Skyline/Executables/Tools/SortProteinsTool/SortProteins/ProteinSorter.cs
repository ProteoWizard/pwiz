using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using SkylineTool;

namespace SortProteins
{
    public class ProteinSorter
    {
        private RemoteClient _client;
        public ProteinSorter(RemoteClient client)
        {
            _client = client;
        }

        public IEnumerable<string> GetProteinLocators(string[] orderBy)
        {
            var rows = ReadRows(orderBy).ToList();
            for (int iCol = orderBy.Length - 1; iCol >= 0; iCol--)
            {
                rows = SortRows(rows, iCol).ToList();
            }

            return rows.Select(row => row.Locator);
        }

        public IEnumerable<Row> ReadRows(string[] columns)
        {
            var queryDef = CreateQueryDef();
            foreach (var column in columns)
            {
                queryDef.AddColumn(column);
            }
            var csvText = (string)_client.RemoteCallName(nameof(IToolService.GetReportFromDefinition), [queryDef.ToString()]);

            using var reader = new StringReader(csvText);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var csv = new CsvReader(reader, config);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var columnValues = Enumerable.Range(1, columns.Length).Select(csv.GetField<string>).ToImmutableList();
                yield return new Row(csv.GetField<string>(0)!) { Columns = columnValues };
            }
        }

        public void SetProteinOrder(IEnumerable<string> newOrder)
        {
            _client.RemoteCallName(nameof(IToolService.ReorderElements), [newOrder.ToArray()]);
        }

        private ReportDefinition CreateQueryDef()
        {
            var queryDef = new ReportDefinition();
            var type = typeof(ProteinSorter);
            using var stream = type.Assembly.GetManifestResourceStream(type, "ProteinLocators.skyr");
            Debug.Assert(stream != null);
            queryDef.ReadDefinition(stream);
            return queryDef;
        }

        public record Row(string Locator)
        {
            public ImmutableList<string?> Columns { get; init; } = [];
        }

        public IEnumerable<Row> SortRows(IList<Row> rows, int columnIndex)
        {
            if (rows.All(row =>
                {
                    var c = row.Columns[columnIndex];
                    return string.IsNullOrEmpty(c) || double.TryParse(c, CultureInfo.InvariantCulture, out _);
                }))
            {
                return rows.OrderBy(row =>
                {
                    var c = row.Columns[columnIndex];
                    return string.IsNullOrEmpty(c) ? default(double?) : double.Parse(c, CultureInfo.InvariantCulture);
                });
            }

            return rows.OrderBy(row => row.Columns[columnIndex], StringComparer.InvariantCultureIgnoreCase);
        }
    }
}
