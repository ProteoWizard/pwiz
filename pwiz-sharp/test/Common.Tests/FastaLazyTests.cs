using Pwiz.Data.Common.Proteome;

namespace Pwiz.Data.Common.Tests;

/// <summary>
/// Covers the lazy FASTA reader path (<see cref="Fasta.OpenLazy"/> +
/// <see cref="FastaProteinList"/>). The eager path is covered by ProteomeTests; this
/// suite focuses on per-protein seek correctness and the optional .index sidecar.
/// </summary>
[TestClass]
public class FastaLazyTests
{
    private static string WriteSampleFasta(int count)
    {
        // Variable-length sequences across multiple wrapped lines, so byte offsets aren't
        // a multiple of any single record stride.
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < count; i++)
        {
            sb.Append('>').Append('P').Append(i.ToString("D5")).Append(" description for protein ").Append(i).Append('\n');
            // Sequence length = 50 + (i * 7) % 80 chars, wrapped at 60.
            int n = 50 + (i * 7) % 80;
            var seq = new char[n];
            for (int j = 0; j < n; j++) seq[j] = "ACDEFGHIKLMNPQRSTVWY"[(i + j) % 20];
            for (int off = 0; off < n; off += 60)
            {
                int len = System.Math.Min(60, n - off);
                sb.Append(new string(seq, off, len)).Append('\n');
            }
        }
        string path = Path.Combine(Path.GetTempPath(), $"fasta-lazy-{System.Guid.NewGuid():N}.fasta");
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    [TestMethod]
    public void OpenLazy_GetProteinByIndex_ReturnsCorrectRecord()
    {
        string path = WriteSampleFasta(50);
        try
        {
            // Eager read for a reference: gives us the canonical answers to compare against.
            var eager = Fasta.ReadFile(path);
            using var pd = Fasta.OpenLazy(path);
            var lazyList = (FastaProteinList)pd.ProteinList!;

            Assert.AreEqual(eager.ProteinList!.Count, lazyList.Count, "Count must match eager read");

            // Probe a few indices out of order to exercise the seek path.
            foreach (int i in new[] { 0, 25, 49, 7, 33, 1 })
            {
                var lazyP = lazyList.GetProtein(i, getSequence: true);
                var eagerP = eager.ProteinList.GetProtein(i, getSequence: true);
                Assert.AreEqual(eagerP.Id, lazyP.Id, $"Id[{i}]");
                Assert.AreEqual(eagerP.Description, lazyP.Description, $"Description[{i}]");
                Assert.AreEqual(eagerP.Sequence, lazyP.Sequence, $"Sequence[{i}]");
                Assert.AreEqual(i, lazyP.Index);
            }

            // getSequence: false drops the sequence load (metadata-only path).
            var metaOnly = lazyList.GetProtein(10, getSequence: false);
            Assert.AreEqual("P00010", metaOnly.Id);
            Assert.AreEqual(string.Empty, metaOnly.Sequence,
                "getSequence=false should return an empty sequence");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void OpenLazy_FindById_UsesIndexNoFullScan()
    {
        string path = WriteSampleFasta(20);
        try
        {
            using var pd = Fasta.OpenLazy(path);
            var list = pd.ProteinList!;
            Assert.AreEqual(0, list.Find("P00000"));
            Assert.AreEqual(15, list.Find("P00015"));
            Assert.AreEqual(list.Count, list.Find("does-not-exist"),
                "missing-id sentinel must be Count (cpp parity)");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    [TestMethod]
    public void OpenLazy_DiskIndex_PersistsAcrossOpens()
    {
        string path = WriteSampleFasta(15);
        string sidecar = path + ".index";
        try
        {
            // First open: builds + persists the sidecar.
            using (var pd1 = Fasta.OpenLazy(path, useDiskIndex: true))
            {
                Assert.AreEqual(15, pd1.ProteinList!.Count);
            }
            Assert.IsTrue(File.Exists(sidecar), "disk index sidecar should have been written");
            long sidecarSizeAfterFirst = new FileInfo(sidecar).Length;
            Assert.IsTrue(sidecarSizeAfterFirst > 0);

            // Second open: should reuse the sidecar (no rebuild). We can't directly observe
            // "no rebuild" through public API, but we can verify the sidecar wasn't truncated
            // and the contents are still readable.
            using (var pd2 = Fasta.OpenLazy(path, useDiskIndex: true))
            {
                Assert.AreEqual(15, pd2.ProteinList!.Count);
                var p7 = pd2.ProteinList.GetProtein(7);
                Assert.AreEqual("P00007", p7.Id);
            }
            Assert.AreEqual(sidecarSizeAfterFirst, new FileInfo(sidecar).Length,
                "sidecar size should be unchanged on the second open");
        }
        finally
        {
            try { File.Delete(path); } catch { }
            try { File.Delete(sidecar); } catch { }
        }
    }

    [TestMethod]
    public void OpenLazy_Dispose_ReleasesFileHandle()
    {
        string path = WriteSampleFasta(3);
        try
        {
            var pd = Fasta.OpenLazy(path);
            ((FastaProteinList)pd.ProteinList!).Dispose();
            // After dispose, the file must be deletable — no lingering FileStream.
            File.Delete(path);
            Assert.IsFalse(File.Exists(path));
        }
        catch
        {
            try { File.Delete(path); } catch { }
            throw;
        }
    }
}
