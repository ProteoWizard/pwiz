using Pwiz.Util.Misc;

namespace Pwiz.Util.Tests.Misc;

[TestClass]
public class IntegerSetTests
{
    [TestMethod]
    public void Insert_SingleValue_Contains()
    {
        var s = new IntegerSet();
        s.Insert(5);
        Assert.IsTrue(s.Contains(5));
        Assert.IsFalse(s.Contains(4));
    }

    [TestMethod]
    public void Insert_Interval_ContainsRange()
    {
        var s = new IntegerSet();
        s.Insert(3, 7);
        Assert.IsTrue(s.Contains(3));
        Assert.IsTrue(s.Contains(5));
        Assert.IsTrue(s.Contains(7));
        Assert.IsFalse(s.Contains(2));
        Assert.IsFalse(s.Contains(8));
    }

    [TestMethod]
    public void Insert_OverlappingIntervals_Coalesce()
    {
        var s = new IntegerSet();
        s.Insert(1, 5);
        s.Insert(4, 10);
        Assert.AreEqual(1, s.IntervalCount);
        var iv = s.Intervals[0];
        Assert.AreEqual(1, iv.Begin);
        Assert.AreEqual(10, iv.End);
    }

    [TestMethod]
    public void Insert_AdjacentIntervals_Coalesce()
    {
        var s = new IntegerSet();
        s.Insert(1, 5);
        s.Insert(6, 10);
        Assert.AreEqual(1, s.IntervalCount);
        Assert.AreEqual(new IntegerSet.Interval(1, 10), s.Intervals[0]);
    }

    [TestMethod]
    public void Insert_DisjointIntervals_StayDisjoint()
    {
        var s = new IntegerSet();
        s.Insert(1, 5);
        s.Insert(10, 15);
        Assert.AreEqual(2, s.IntervalCount);
    }

    [TestMethod]
    public void Insert_ReversedArgs_SwapsToAscending()
    {
        var s = new IntegerSet();
        s.Insert(10, 3);
        Assert.AreEqual(new IntegerSet.Interval(3, 10), s.Intervals[0]);
    }

    [TestMethod]
    public void Enumeration_Ordered()
    {
        var s = new IntegerSet();
        s.Insert(3, 5);
        s.Insert(8, 9);
        CollectionAssert.AreEqual(new[] { 3, 4, 5, 8, 9 }, s.ToList());
    }

    [TestMethod]
    public void Count_SumsAllIntervals()
    {
        var s = new IntegerSet();
        s.Insert(1, 5);   // 5
        s.Insert(10, 12); // 3
        Assert.AreEqual(8L, s.Count);
    }

    [TestMethod]
    public void HasUpperBound_WorksOnEmpty_AndBounded()
    {
        Assert.IsTrue(new IntegerSet().HasUpperBound(0));

        var s = new IntegerSet(5, 10);
        Assert.IsTrue(s.HasUpperBound(10));
        Assert.IsTrue(s.HasUpperBound(100));
        Assert.IsFalse(s.HasUpperBound(9));
    }

    // ---- Parsing ----

    [TestMethod]
    public void Parse_SingleInteger()
    {
        var s = new IntegerSet();
        s.Parse("5");
        Assert.IsTrue(s.Contains(5));
        Assert.AreEqual(1, s.IntervalCount);
    }

    [TestMethod]
    public void Parse_Range()
    {
        var s = new IntegerSet();
        s.Parse("3-7");
        CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7 }, s.ToList());
    }

    [TestMethod]
    public void Parse_MixedTokens()
    {
        var s = new IntegerSet();
        s.Parse("[-3,2] 5 8-9");
        CollectionAssert.AreEqual(new[] { -3, -2, -1, 0, 1, 2, 5, 8, 9 }, s.ToList());
    }

    [TestMethod]
    public void Parse_OpenEndedRange_UsesIntMax()
    {
        var s = new IntegerSet();
        s.Parse("10-");
        Assert.IsTrue(s.Contains(10));
        Assert.IsTrue(s.Contains(1_000_000));
        Assert.IsTrue(s.Contains(int.MaxValue));
    }

    [TestMethod]
    public void Parse_WhitespaceVariations_Works()
    {
        var s = new IntegerSet();
        s.Parse("  1-3   7  [10,12]  ");
        CollectionAssert.AreEqual(new[] { 1, 2, 3, 7, 10, 11, 12 }, s.ToList());
    }

    // ---- Predefined sets ----

    [TestMethod]
    public void Predefined_Empty_IsEmpty()
    {
        Assert.IsTrue(IntegerSet.Empty.IsEmpty);
    }

    [TestMethod]
    public void Predefined_Positive_ExcludesZero()
    {
        Assert.IsFalse(IntegerSet.Positive.Contains(0));
        Assert.IsTrue(IntegerSet.Positive.Contains(1));
        Assert.IsTrue(IntegerSet.Positive.Contains(1_000_000));
    }

    [TestMethod]
    public void Predefined_Whole_IncludesZero()
    {
        Assert.IsTrue(IntegerSet.Whole.Contains(0));
        Assert.IsTrue(IntegerSet.Whole.Contains(42));
        Assert.IsFalse(IntegerSet.Whole.Contains(-1));
    }

    [TestMethod]
    public void Predefined_Negative_ExcludesZero()
    {
        Assert.IsFalse(IntegerSet.Negative.Contains(0));
        Assert.IsTrue(IntegerSet.Negative.Contains(-1));
        Assert.IsTrue(IntegerSet.Negative.Contains(int.MinValue));
    }
}
