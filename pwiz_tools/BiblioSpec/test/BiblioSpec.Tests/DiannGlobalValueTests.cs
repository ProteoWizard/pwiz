using System.Data.SQLite;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// The DIA-NN reader writes each precursor's protein group as a <c>Proteins</c> row with the
/// report's <c>Global.PG.Q.Value</c> in <c>Proteins.QValue</c>. The golden <c>.check</c> files do
/// not dump the Proteins table, so these tests compare it with the report directly.
/// </summary>
[TestClass]
public class DiannGlobalValueTests
{
    [TestMethod]
    public void Diann_TsvReport_WritesProteinGroupQValues()
    {
        new BuildTests().Diann_SpecLib_DiaPasef();
        AssertProteinGroups("diann-hela-diapasef.blib", "diann-hela-diapasef.tsv");
    }

    [TestMethod]
    public void Diann_ParquetReport_WritesProteinGroupQValues()
    {
        new BuildTests().Diann2_Parquet();
        AssertProteinGroups("diann2-parquet.blib", "diann2-parquet/rep.parquet");
    }

    private static void AssertProteinGroups(string blibName, string reportName)
    {
        var fixture = GoldenFileFixture.Instance!;
        var groupQValues = ReadGroupQValues(fixture.InputFile(reportName));

        using var connection = new SQLiteConnection($"Data Source={fixture.OutputFile(blibName)};Pooling=false;Read Only=true");
        connection.Open();

        int groupCount = 0;
        using (var command = new SQLiteCommand("SELECT accession, QValue FROM Proteins", connection))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                groupCount++;
                var group = reader.GetString(0);
                Assert.IsTrue(groupQValues.TryGetValue(group, out var expected), $"Protein group '{group}' is not in the report.");
                Assert.AreEqual((double)expected, reader.GetDouble(1), $"Wrong QValue for '{group}'.");
            }
        }
        Assert.IsTrue(groupCount > 0, "No protein groups were written.");

        using var unlinked = new SQLiteCommand(
            "SELECT COUNT(*) FROM RefSpectra WHERE id NOT IN (SELECT RefSpectraId FROM RefSpectraProteins)", connection);
        Assert.AreEqual(0L, unlinked.ExecuteScalar(), "Every spectrum should be linked to its protein group.");
    }

    /// <summary>
    /// Reads each protein group's Global.PG.Q.Value, asserting it is the same on every row. Uses
    /// the reader's own report parser, whose float parsing can differ from float.Parse in the last bit.
    /// </summary>
    private static Dictionary<string, float> ReadGroupQValues(string reportPath)
    {
        var result = new Dictionary<string, float>(StringComparer.Ordinal);
        foreach (var row in DiaNNSpecLibReader.ReadDiannReport(reportPath))
        {
            Assert.IsTrue(row.GlobalPgQValue.HasValue, "The report has no Global.PG.Q.Value column.");
            var qValue = row.GlobalPgQValue.Value;
            if (result.TryGetValue(row.ProteinGroup, out var existing))
                Assert.AreEqual(existing, qValue, $"Global.PG.Q.Value differs between rows of '{row.ProteinGroup}'.");
            else
                result.Add(row.ProteinGroup, qValue);
        }
        return result;
    }
}
