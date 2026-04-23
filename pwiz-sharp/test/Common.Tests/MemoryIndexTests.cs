using Pwiz.Data.Common.Index;

namespace Pwiz.Data.Common.Tests.Index;

[TestClass]
public class MemoryIndexTests
{
    [TestMethod]
    public void Create_AddsEntries_CountMatches()
    {
        var index = new MemoryIndex();
        var entries = new List<IndexEntry>
        {
            new("s1", 0, 100),
            new("s2", 1, 200),
            new("s3", 2, 300),
        };
        index.Create(entries);
        Assert.AreEqual(3, index.Count);
    }

    [TestMethod]
    public void FindById_Existing_ReturnsEntry()
    {
        var index = new MemoryIndex();
        var entries = new List<IndexEntry>
        {
            new("spectrum=1", 0, 100),
            new("spectrum=2", 1, 250),
        };
        index.Create(entries);

        var found = index.Find("spectrum=2");
        Assert.IsNotNull(found);
        Assert.AreEqual(1u, found.Index);
        Assert.AreEqual(250L, found.Offset);
    }

    [TestMethod]
    public void FindById_Missing_ReturnsNull()
    {
        var index = new MemoryIndex();
        index.Create(new List<IndexEntry> { new("s1", 0, 0) });
        Assert.IsNull(index.Find("does-not-exist"));
    }

    [TestMethod]
    public void FindByOrdinal_InBounds_ReturnsEntry()
    {
        var index = new MemoryIndex();
        index.Create(new List<IndexEntry>
        {
            new("a", 0, 10),
            new("b", 1, 20),
            new("c", 2, 30),
        });

        var found = index.Find(1);
        Assert.IsNotNull(found);
        Assert.AreEqual("b", found.Id);
    }

    [TestMethod]
    public void FindByOrdinal_OutOfRange_ReturnsNull()
    {
        var index = new MemoryIndex();
        index.Create(new List<IndexEntry> { new("a", 0, 0) });
        Assert.IsNull(index.Find(99));
        Assert.IsNull(index.Find(-1));
    }

    [TestMethod]
    public void Create_Recreate_ReplacesEntries()
    {
        var index = new MemoryIndex();
        index.Create(new List<IndexEntry> { new("old", 0, 0) });
        index.Create(new List<IndexEntry> { new("new1", 0, 0), new("new2", 1, 0) });
        Assert.AreEqual(2, index.Count);
        Assert.IsNull(index.Find("old"));
        Assert.IsNotNull(index.Find("new1"));
    }
}
