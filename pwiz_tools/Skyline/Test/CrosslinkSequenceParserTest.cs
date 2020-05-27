using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CrosslinkSequenceParserTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCrosslinkSequenceParser()
        {
            CrosslinkLibraryKey libKey =
                CrosslinkSequenceParser.ParseCrosslinkLibraryKey("VTIAQGGVLPNIQAVLLPKK-TESHHKACGK-[+138.0681@19,6]", 1);
            Assert.AreEqual(2, libKey.PeptideLibraryKeys.Count);
            Assert.AreEqual("VTIAQGGVLPNIQAVLLPKK", libKey.PeptideLibraryKeys[0].ModifiedSequence);
            Assert.AreEqual("TESHHKACGK", libKey.PeptideLibraryKeys[1].ModifiedSequence);
            Assert.AreEqual(1, libKey.Crosslinks.Count);
            Assert.AreEqual("+138.0681", libKey.Crosslinks[0].Name);
            Assert.AreEqual(2, libKey.Crosslinks[0].Positions.Count);
            Assert.AreEqual(ImmutableList.Singleton(19), libKey.Crosslinks[0].Positions[0]);
            Assert.AreEqual(ImmutableList.Singleton(6), libKey.Crosslinks[0].Positions[1]);
        }

        [TestMethod]
        public void TestCrosslinkWithModifications()
        {
            CrosslinkLibraryKey libKey = CrosslinkSequenceParser.ParseCrosslinkLibraryKey(
                "C[+57.02146]C[+57.02146]TKPESER-EKVLTSSAR-[+138.0681@4,2]", 1);
            Assert.AreEqual(2, libKey.PeptideLibraryKeys.Count);
            Assert.AreEqual("C[+57.02146]C[+57.02146]TKPESER", libKey.PeptideLibraryKeys[0].ModifiedSequence);
            Assert.AreEqual("EKVLTSSAR", libKey.PeptideLibraryKeys[1].ModifiedSequence);
            Assert.AreEqual("+138.0681", libKey.Crosslinks[0].Name);
            Assert.AreEqual(2, libKey.Crosslinks[0].Positions.Count);
            Assert.AreEqual(ImmutableList.Singleton(4), libKey.Crosslinks[0].Positions[0]);
            Assert.AreEqual(ImmutableList.Singleton(2), libKey.Crosslinks[0].Positions[1]);
        }
    }
}
