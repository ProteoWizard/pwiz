using Pwiz.Data.Common.Index;

namespace Pwiz.Data.Common.Tests.Index;

[TestClass]
public class BinaryIndexStreamTests
{
    private static MemoryStream WriteIndex(List<IndexEntry> entries)
    {
        var ms = new MemoryStream();
        using (var writer = new BinaryIndexStream(ms, leaveOpen: true))
            writer.Create(entries);
        ms.Position = 0;
        return ms;
    }

    [TestMethod]
    public void RoundTrip_CountAndOrdinalLookup()
    {
        // Write 4 entries, reopen, check count + ordinal lookup.
        var entries = new List<IndexEntry>
        {
            new("alpha", 0, 100),
            new("beta", 1, 200),
            new("gamma", 2, 300),
            new("delta", 3, 400),
        };
        using var ms = WriteIndex(entries);
        using var reader = new BinaryIndexStream(ms, leaveOpen: true);
        Assert.AreEqual(4, reader.Count);

        var byOrdinal = reader.Find(1);
        Assert.IsNotNull(byOrdinal);
        Assert.AreEqual("beta", byOrdinal.Id);
        Assert.AreEqual(1u, byOrdinal.Index);
        Assert.AreEqual(200L, byOrdinal.Offset);
    }

    [TestMethod]
    public void FindById_BinarySearch_AllEntriesAndMissing()
    {
        // 50 sorted entries: every one must be findable, plus a known-missing returns null.
        var entries = new List<IndexEntry>();
        for (int i = 0; i < 50; i++)
            entries.Add(new($"scan_{i:D4}", (ulong)i, i * 1000L));
        using var ms = WriteIndex(entries);
        using var reader = new BinaryIndexStream(ms, leaveOpen: true);

        foreach (var original in entries)
        {
            var found = reader.Find(original.Id);
            Assert.IsNotNull(found, $"id {original.Id} should be found");
            Assert.AreEqual(original.Offset, found.Offset);
        }

        Assert.IsNull(reader.Find("nonexistent"));
    }

    [TestMethod]
    public void EmptyStream_NoEntries_ReturnsZeroAndNull()
    {
        using var ms = new MemoryStream();
        using var reader = new BinaryIndexStream(ms, leaveOpen: true);
        Assert.AreEqual(0, reader.Count);
        Assert.IsNull(reader.Find("anything"));
        Assert.IsNull(reader.Find(0));
    }
}
