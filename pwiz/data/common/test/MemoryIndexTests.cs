using Pwiz.Data.Common.Index;

namespace Pwiz.Data.Common.Tests.Index;

[TestClass]
public class MemoryIndexTests
{
    [TestMethod]
    public void Create_AndCount_AndRecreate()
    {
        var index = new MemoryIndex();
        index.Create(new List<IndexEntry>
        {
            new("s1", 0, 100), new("s2", 1, 200), new("s3", 2, 300),
        });
        Assert.AreEqual(3, index.Count);

        // Re-Create replaces the previous entries (doesn't append).
        index.Create(new List<IndexEntry> { new("new1", 0, 0), new("new2", 1, 0) });
        Assert.AreEqual(2, index.Count, "Create replaces entries");
        Assert.IsNull(index.Find("s1"), "old entry gone after recreate");
        Assert.IsNotNull(index.Find("new1"));
    }

    [TestMethod]
    public void FindById_PresentOrMissing()
    {
        var index = new MemoryIndex();
        index.Create(new List<IndexEntry>
        {
            new("spectrum=1", 0, 100),
            new("spectrum=2", 1, 250),
        });

        var found = index.Find("spectrum=2");
        Assert.IsNotNull(found);
        Assert.AreEqual(1u, found.Index);
        Assert.AreEqual(250L, found.Offset);

        Assert.IsNull(index.Find("does-not-exist"));
    }

    [TestMethod]
    public void FindByOrdinal_InBoundsAndOut()
    {
        var index = new MemoryIndex();
        index.Create(new List<IndexEntry>
        {
            new("a", 0, 10), new("b", 1, 20), new("c", 2, 30),
        });

        var found = index.Find(1);
        Assert.IsNotNull(found);
        Assert.AreEqual("b", found.Id);

        Assert.IsNull(index.Find(99), "above range");
        Assert.IsNull(index.Find(-1), "below range");
    }
}
