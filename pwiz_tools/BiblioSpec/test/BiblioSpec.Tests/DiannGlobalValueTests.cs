using System.Data.SQLite;

using pwiz.BiblioSpec.IO;

namespace Pwiz.Tools.BiblioSpec.Tests;

/// <summary>
/// The DIA-NN reader writes each precursor's protein group as a <c>Proteins</c> row with the
/// report's <c>Global.PG.Q.Value</c> in <c>Proteins.QValue</c>. The golden <c>.check</c> files do
/// not dump the Proteins table, so these tests compare it with the report directly.
/// </summary>
[TestClass]
public class DiannGlobalValueTests
{
    private const string PD_RESULT = "230807_P1_Neo_ES904_TMTProPrecision_1ug_DIA1Th_HCD30_Survey.pdResult";
    private const string PD_LEGACY_RESULT = "example.pdResult";

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

    /// <summary>
    /// Two inputs are two experiments, whose global values mean nothing next to each other, so the
    /// library gets none of the optional columns. Uses Proteome Discoverer results, because the
    /// DIA-NN reader cannot add a second input to a library.
    /// </summary>
    [TestMethod]
    public void PdResult_TwoInputs_WritesNoGlobalValues()
    {
        var outputPath = RunBlibBuild("pd-two-inputs.blib", true, PD_RESULT, PD_LEGACY_RESULT);
        AssertNoGlobalValues(outputPath);
    }

    /// <summary>
    /// Appending another experiment to a library that has global values removes them.
    /// </summary>
    [TestMethod]
    public void PdResult_Append_RemovesGlobalValues()
    {
        var outputPath = RunBlibBuild("pd-append.blib", true, PD_RESULT);
        using (var connection = OpenReadOnly(outputPath))
            Assert.IsTrue(GetColumnNames(connection, "RefSpectra").Contains("PeptideQValue"), "The first build should have global values.");

        RunBlibBuild("pd-append.blib", false, PD_LEGACY_RESULT);
        AssertNoGlobalValues(outputPath);
    }

    private static string RunBlibBuild(string blibName, bool overwrite, params string[] inputNames)
    {
        var fixture = GoldenFileFixture.Instance!;
        var outputPath = fixture.OutputFile(blibName);
        if (overwrite && File.Exists(outputPath))
            File.Delete(outputPath);
        var args = new List<string> { "-c", "0" };
        if (overwrite)
            args.Add("-o");
        args.AddRange(inputNames.Select(fixture.InputFile));
        args.Add("--out=" + outputPath);
        int exitCode = ExecuteBlib.Execute(BlibTool.BlibBuild, args.ToArray(), fixture.OutputDir,
            out string stdout, out string stderr);
        Assert.AreEqual(0, exitCode, $"stdout:\n{stdout}\n\nstderr:\n{stderr}");
        return outputPath;
    }

    private static void AssertNoGlobalValues(string blibPath)
    {
        using var connection = OpenReadOnly(blibPath);
        using (var count = new SQLiteCommand("SELECT COUNT(*) FROM RefSpectra", connection))
            Assert.IsTrue((long)count.ExecuteScalar()! > 0, "No spectra were written.");
        var optionalColumns = BlibSchema.OptionalRefSpectraColumns.Concat(BlibSchema.OptionalProteinsColumns);
        foreach (var column in optionalColumns)
            Assert.IsFalse(GetColumnNames(connection, column.Table).Contains(column.Name), $"{column.Table}.{column.Name} should not exist.");
    }

    private static HashSet<string> GetColumnNames(SQLiteConnection connection, string table)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        using var command = new SQLiteCommand($"PRAGMA table_info({table})", connection);
        using var reader = command.ExecuteReader();
        while (reader.Read())
            names.Add(reader.GetString(1));
        return names;
    }

    private static SQLiteConnection OpenReadOnly(string path)
    {
        var connection = new SQLiteConnection($"Data Source={path};Pooling=false;Read Only=true");
        connection.Open();
        return connection;
    }

    private static void AssertProteinGroups(string blibName, string reportName)
    {
        var fixture = GoldenFileFixture.Instance!;
        var groupQValues = ReadGroupQValues(fixture.InputFile(reportName));

        using var connection = OpenReadOnly(fixture.OutputFile(blibName));

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
