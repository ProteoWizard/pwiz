using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ChromatogramGroupIdsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestConvertFromTextIdBytes()
        {
            var targets = new[]
            {
                new Target("ELVIS"),
                new Target(new CustomMolecule("H2O", "Water"))
            };
            var textIdBytes = new List<byte>();
            var textIdLocations = new List<KeyValuePair<int, ushort>>();
            foreach (var target in targets)
            {
                var bytes = Encoding.UTF8.GetBytes(target.ToSerializableString());
                textIdLocations.Add(new KeyValuePair<int, ushort>(textIdBytes.Count, (ushort) bytes.Length));
                textIdBytes.AddRange(bytes);
            }

            var legacyChromGroupHeaderInfos = new[]
            {
                new ChromGroupHeaderInfo16()
                {
                    _textIdIndex = textIdLocations[1].Key,
                    _textIdLen = textIdLocations[1].Value,
                },
                new ChromGroupHeaderInfo16() {
                    _textIdIndex = -1
                },
                new ChromGroupHeaderInfo16()
                {
                    _textIdIndex = textIdLocations[0].Key, 
                    _textIdLen = textIdLocations[0].Value
                }
            };
            var chromatogramGroupIds = new ChromatogramGroupIds();
            var newChromGroupHeaderInfos = chromatogramGroupIds.ConvertFromTextIdBytes(textIdBytes.ToArray(), legacyChromGroupHeaderInfos)
                .ToList();
            Assert.AreEqual(3, newChromGroupHeaderInfos.Count);
            Assert.AreEqual(0, newChromGroupHeaderInfos[0].TextIdIndex);
            Assert.AreEqual(-1, newChromGroupHeaderInfos[1].TextIdIndex);
            Assert.AreEqual(1, newChromGroupHeaderInfos[2].TextIdIndex);
        }
    }
}
