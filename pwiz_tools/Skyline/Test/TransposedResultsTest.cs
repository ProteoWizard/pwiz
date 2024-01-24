using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class TransposedResultsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestTransposedResults()
        {
            var transitionChromInfo1 = new TransitionChromInfo(new ChromFileInfoId(), 1, 2, 3, 4, 5,
                IonMobilityFilter.EMPTY, 6, 7, 8, 9, false, true, 10, PeakIdentification.ALIGNED, 11, 12,
                Annotations.EMPTY, UserSet.FALSE, false, null);
            var transitionChromInfo2 = new TransitionChromInfo(new ChromFileInfoId(), 13, 14, 15, 16, 17,
                IonMobilityFilter.EMPTY, 18, 19, 20, 21, true, false, 22, PeakIdentification.TRUE, 23, 24,
                Annotations.EMPTY, UserSet.IMPORTED, true, null);
            var transposedResults = (TransposedTransitionChromInfos) TransposedTransitionChromInfos.EMPTY.ChangeColumns(
                TransitionChromInfo.TRANSPOSER.ToColumns(new[] { transitionChromInfo1, transitionChromInfo2 }));
            AssertEx.AreEqual(transitionChromInfo1, transposedResults.GetRow(0));
            AssertEx.AreEqual(transitionChromInfo2, transposedResults.GetRow(1));
            var array = new[] { transposedResults };
            TransitionChromInfo.TRANSPOSER.EfficientlyStore(array);
            AssertEx.AreEqual(transitionChromInfo1, array[0].ToRows(0, 1).GetValue(0));
            AssertEx.AreEqual(transitionChromInfo2, array[0].ToRows(1, 1).GetValue(0));
        }
    }
}
