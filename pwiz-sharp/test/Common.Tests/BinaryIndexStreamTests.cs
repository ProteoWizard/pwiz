using Pwiz.Data.Common.Index;

namespace Pwiz.Data.Common.Tests.Index;

[TestClass]
public class BinaryIndexStreamTests
{
    [TestMethod]
    public void Create_ThenReopen_PreservesCount()
    {
        using var ms = new MemoryStream();
        var entries = new List<IndexEntry>
        {
            new("s0000000", 0, 1000),
            new("s0000001", 1, 2000),
            new("s0000002", 2, 3000),
            new("s0000003", 3, 4000),
        };

        using (var writer = new BinaryIndexStream(ms, leaveOpen: true))
            writer.Create(entries);

        ms.Position = 0;
        using var reader = new BinaryIndexStream(ms, leaveOpen: true);
        Assert.AreEqual(4, reader.Count);
    }

    [TestMethod]
    public void FindByOrdinal_ReturnsExpectedEntry()
    {
        using var ms = new MemoryStream();
        var entries = new List<IndexEntry>
        {
            new("alpha", 0, 100),
            new("beta", 1, 200),
            new("gamma", 2, 300),
        };
        using (var writer = new BinaryIndexStream(ms, leaveOpen: true))
            writer.Create(entries);

        ms.Position = 0;
        using var reader = new BinaryIndexStream(ms, leaveOpen: true);
        var e = reader.Find(1);
        Assert.IsNotNull(e);
        Assert.AreEqual("beta", e.Id);
        Assert.AreEqual(1u, e.Index);
        Assert.AreEqual(200L, e.Offset);
    }

    [TestMethod]
    public void FindById_BinarySearch_FindsAllEntries()
    {
        using var ms = new MemoryStream();
        var entries = new List<IndexEntry>();
        for (int i = 0; i < 50; i++)
            entries.Add(new($"scan_{i:D4}", (ulong)i, i * 1000L));

        using (var writer = new BinaryIndexStream(ms, leaveOpen: true))
            writer.Create(entries);

        ms.Position = 0;
        using var reader = new BinaryIndexStream(ms, leaveOpen: true);
        foreach (var original in entries)
        {
            var found = reader.Find(original.Id);
            Assert.IsNotNull(found, $"id {original.Id} should be found");
            Assert.AreEqual(original.Offset, found.Offset);
        }
    }

    [TestMethod]
    public void FindById_Missing_ReturnsNull()
    {
        using var ms = new MemoryStream();
        var entries = new List<IndexEntry> { new("foo", 0, 0), new("bar", 1, 100) };
        using (var writer = new BinaryIndexStream(ms, leaveOpen: true))
            writer.Create(entries);

        ms.Position = 0;
        using var reader = new BinaryIndexStream(ms, leaveOpen: true);
        Assert.IsNull(reader.Find("nonexistent"));
    }

    [TestMethod]
    public void EmptyStream_Count_IsZero()
    {
        using var ms = new MemoryStream();
        using var reader = new BinaryIndexStream(ms, leaveOpen: true);
        Assert.AreEqual(0, reader.Count);
        Assert.IsNull(reader.Find("anything"));
        Assert.IsNull(reader.Find(0));
    }
}
