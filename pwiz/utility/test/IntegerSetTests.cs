using Pwiz.Util.Misc;

namespace Pwiz.Util.Tests.Misc;

[TestClass]
public class IntegerSetTests
{
    [TestMethod]
    public void Insert_BasicMembership()
    {
        // Single value: contains exactly that value.
        var single = new IntegerSet();
        single.Insert(5);
        Assert.IsTrue(single.Contains(5));
        Assert.IsFalse(single.Contains(4));

        // Range: closed inclusive on both ends.
        var range = new IntegerSet();
        range.Insert(3, 7);
        Assert.IsTrue(range.Contains(3) && range.Contains(5) && range.Contains(7));
        Assert.IsFalse(range.Contains(2) || range.Contains(8));
    }

    [TestMethod]
    public void Insert_IntervalMerging()
    {
        // Overlapping intervals coalesce.
        var overlap = new IntegerSet();
        overlap.Insert(1, 5);
        overlap.Insert(4, 10);
        Assert.AreEqual(1, overlap.IntervalCount, "overlapping should coalesce");
        Assert.AreEqual(new IntegerSet.Interval(1, 10), overlap.Intervals[0]);

        // Adjacent intervals (touching at the integer boundary) also coalesce.
        var adjacent = new IntegerSet();
        adjacent.Insert(1, 5);
        adjacent.Insert(6, 10);
        Assert.AreEqual(1, adjacent.IntervalCount, "adjacent should coalesce");
        Assert.AreEqual(new IntegerSet.Interval(1, 10), adjacent.Intervals[0]);

        // Disjoint intervals stay disjoint.
        var disjoint = new IntegerSet();
        disjoint.Insert(1, 5);
        disjoint.Insert(10, 15);
        Assert.AreEqual(2, disjoint.IntervalCount, "disjoint should not coalesce");

        // Reversed begin/end is normalized to ascending order.
        var reversed = new IntegerSet();
        reversed.Insert(10, 3);
        Assert.AreEqual(new IntegerSet.Interval(3, 10), reversed.Intervals[0]);
    }

    [TestMethod]
    public void Enumeration_Count_HasUpperBound()
    {
        // Enumeration is in ascending order across all intervals.
        var s = new IntegerSet();
        s.Insert(3, 5);
        s.Insert(8, 9);
        CollectionAssert.AreEqual(new[] { 3, 4, 5, 8, 9 }, s.ToList());

        // Count sums all intervals' element counts.
        var sized = new IntegerSet();
        sized.Insert(1, 5);    // 5
        sized.Insert(10, 12);  // 3
        Assert.AreEqual(8L, sized.Count);

        // HasUpperBound: empty set has trivial upper bound; bounded set's max is its actual max.
        Assert.IsTrue(new IntegerSet().HasUpperBound(0), "empty bounded by anything");
        var bounded = new IntegerSet(5, 10);
        Assert.IsTrue(bounded.HasUpperBound(10), "exact max");
        Assert.IsTrue(bounded.HasUpperBound(100), "above max");
        Assert.IsFalse(bounded.HasUpperBound(9), "below max");
    }

    [TestMethod]
    public void Parse_AllFormatVariants()
    {
        var cases = new[]
        {
            ("5",                new[] { 5 }),
            ("3-7",              new[] { 3, 4, 5, 6, 7 }),
            ("[-3,2] 5 8-9",     new[] { -3, -2, -1, 0, 1, 2, 5, 8, 9 }),
            ("  1-3   7  [10,12]  ", new[] { 1, 2, 3, 7, 10, 11, 12 }),
        };
        foreach (var (input, expected) in cases)
        {
            var s = new IntegerSet();
            s.Parse(input);
            CollectionAssert.AreEqual(expected, s.ToList(), $"Parse(\"{input}\")");
        }

        // "N-" is shorthand for [N, int.MaxValue].
        var openEnded = new IntegerSet();
        openEnded.Parse("10-");
        Assert.IsTrue(openEnded.Contains(10));
        Assert.IsTrue(openEnded.Contains(1_000_000));
        Assert.IsTrue(openEnded.Contains(int.MaxValue));
    }

    [TestMethod]
    public void Predefined_Sets()
    {
        Assert.IsTrue(IntegerSet.Empty.IsEmpty);

        Assert.IsFalse(IntegerSet.Positive.Contains(0), "Positive excludes 0");
        Assert.IsTrue(IntegerSet.Positive.Contains(1));
        Assert.IsTrue(IntegerSet.Positive.Contains(1_000_000));

        Assert.IsTrue(IntegerSet.Whole.Contains(0), "Whole includes 0");
        Assert.IsTrue(IntegerSet.Whole.Contains(42));
        Assert.IsFalse(IntegerSet.Whole.Contains(-1));

        Assert.IsFalse(IntegerSet.Negative.Contains(0), "Negative excludes 0");
        Assert.IsTrue(IntegerSet.Negative.Contains(-1));
        Assert.IsTrue(IntegerSet.Negative.Contains(int.MinValue));
    }
}
