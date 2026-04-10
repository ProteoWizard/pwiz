using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using SkylineTool;

namespace SortProteins
{
    public class ProteinSorter(IJsonToolService client)
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
            var columns = new List<string> { "ProteinLocator" };
            if (!string.IsNullOrEmpty(column))
            {
                columns.Add(column);
            }

            var definition = new ReportDefinition
            {
                Select = columns.ToArray(),
                Uimode = "proteomic",
                DataSource = "document_grid"
            };

            var tempFile = Path.GetTempFileName();
            tempFile = Path.ChangeExtension(tempFile, ".csv");
            try
            {
                client.ExportReportFromDefinition(definition, tempFile, JsonToolConstants.CULTURE_INVARIANT);
                var csvText = File.ReadAllText(tempFile);
                return ParseCsv(csvText, column);
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        private static List<Row> ParseCsv(string csvText, string? column)
        {
            var rows = new List<Row>();
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
                    rows.Add(row);
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

                    rows.Add(row);
                }
            }
            return rows;
        }

        public void SetProteinOrder(IEnumerable<string> newOrder)
        {
            client.ReorderElements(newOrder.Distinct().ToArray());
        }

        private record Row(string Locator)
        {
            public string? TextValue { get; init; }
            public double? NumberValue { get; init; }
        }
    }
}
