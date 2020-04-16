using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CrosslinkerDefTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCrosslinkerDefSerialization()
        {
            var crosslinkerSettings = CrosslinkingSettings.DEFAULT;
            VerifyRoundTrip(crosslinkerSettings);
            crosslinkerSettings = crosslinkerSettings.ChangeMaxFragmentations(10);
            VerifyRoundTrip(crosslinkerSettings);
            crosslinkerSettings = AssertEx.RoundTrip(crosslinkerSettings);
            Assert.AreEqual(10, crosslinkerSettings.MaxFragmentations);
            var testCrosslinkers = new[]
            {
                new CrosslinkerDef("test", null),
                new CrosslinkerDef("disulfide", new FormulaMass("-H2")),
                new CrosslinkerDef("justmasses", new FormulaMass(null, Math.E, Math.PI)),
                new CrosslinkerDef("formulaandmasses", new FormulaMass("-H2", 1, 2))
            };

            for (int iCrosslinker = 0; iCrosslinker < testCrosslinkers.Length; iCrosslinker++)
            {
                var crosslinker = testCrosslinkers[iCrosslinker];
                VerifyRoundTrip(crosslinker);
                crosslinkerSettings =
                    crosslinkerSettings.ChangeCrosslinkers(crosslinkerSettings.Crosslinkers.Append(crosslinker));
                VerifyRoundTrip(crosslinkerSettings);
            }
            
        }
        private void VerifyRoundTrip<T>(T obj) where T : class
        {
            var roundTrip = AssertEx.RoundTrip(obj);
            Assert.AreEqual(obj, roundTrip);
        }
    }
}
