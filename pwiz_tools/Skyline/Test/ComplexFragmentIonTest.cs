using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Crosslinking;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ComplexFragmentIonTest
    {
        [TestMethod]
        public void TestNeutralFragmentIonCompareTo()
        {
            var random = new Random((int) DateTime.UtcNow.Ticks);
            var ionChains = new[]
            {
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Precursor),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Y(7)),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Y(5)),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.B(5)),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.B(7)),
                IonChain.FromIons(IonOrdinal.Precursor, IonOrdinal.Empty),

            };
            var neutralFragmentIons = ionChains.OrderBy(chain => random.Next())
                .Select(chain => new NeutralFragmentIon(chain, null)).ToList();
            neutralFragmentIons.Sort();
            var sortedIonChains = neutralFragmentIons.Select(ion => ion.IonChain).ToList();
            AssertSameOrder(ionChains, sortedIonChains);
        }

        public static void AssertSameOrder<T>(IEnumerable<T> expected, IEnumerable<T> actual)
        {
            var expectedList = expected.ToList();
            var actualList = actual.ToList();
            CollectionAssert.AreEqual(expectedList, actualList, "Expected: {0} Actual: {1}", string.Join(",", expectedList), string.Join(",", actualList));

        }
    }
}
