using System.Diagnostics;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using SkylineTool;

namespace SortProteins
{
    public class ProteinSorter(RemoteClient client)
    {
        public IEnumerable<string> GetProteinLocators(string? orderBy)
        {
            var rows = ReadRows(orderBy).ToList();
            if (!string.IsNullOrEmpty(orderBy))
            {
                if (rows.All(row => row.NumberValue.HasValue))
                {
                    rows = rows.OrderBy(row => row.NumberValue).ToList();
                }
                else
                {
                    rows = rows.OrderBy(row => row.TextValue, StringComparer.InvariantCultureIgnoreCase).ToList();
                }
            }

            return rows.Select(row => row.Locator);
        }

        private IEnumerable<Row> ReadRows(string? column)
        {
            var queryDef = CreateQueryDef();
            if (!string.IsNullOrEmpty(column))
            {
                queryDef.AddColumn(column);
            }
            var csvText = (string)client.RemoteCallName(nameof(IToolService.GetReportFromDefinition), [queryDef.ToString()]);

            using var reader = new StringReader(csvText);
            var config = new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true };
            using var csv = new CsvReader(reader, config);
            csv.Read();
            csv.ReadHeader();
            while (csv.Read())
            {
                var row = new Row(csv.GetField<string>(0)!);
                if (string.IsNullOrEmpty(column))
                {
                    yield return row;
                }
                else
                {
                    var sortColumn = csv.GetField<string>(1);
                    if (string.IsNullOrEmpty(sortColumn))
                    {
                        continue;
                    }

                    row = row with { TextValue = sortColumn };
                    if (double.TryParse(sortColumn, CultureInfo.InvariantCulture, out var doubleValue))
                    {
                        row = row with { NumberValue = doubleValue };
                    }

                    yield return row;
                }
            }
        }

        public void SetProteinOrder(IEnumerable<string> newOrder)
        {
            client.RemoteCallName(nameof(IToolService.ReorderElements), [newOrder.ToArray()]);
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

        private record Row(string Locator)
        {
            public string? TextValue { get; init; }
            public double? NumberValue { get; init; }
        }
    }
}
