using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MultiplexMatrixTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMultiplexMatrixSerialization()
        {
            var multiplexMatrix = new MultiplexMatrix("MyScheme", new[]
            {
                MakeReplicate("126", new[] { "TMT-126", "TMT-127H" }, new[] { 100, 9.09 }),
                MakeReplicate("127N", new[] { "TMT-126", "TMT-127L", "TMT-128L" }, new[] { 0.57, 100, 9.79 }),
                MakeReplicate("127C", new[] { "TMT-126, TMT-127H", "TMT-128H" }, new[] { .84, 100, 8.4 }),
            });
            AssertSerializable(multiplexMatrix);
            var quantificationSettings =
                new QuantificationSettings(RegressionWeighting.NONE).ChangeMultiplexMatrix(multiplexMatrix);
            AssertSerializable(quantificationSettings);
        }

        private MultiplexMatrix.Replicate MakeReplicate(string name, string[] weightNames, double[] weightValues)
        {
            return new MultiplexMatrix.Replicate(name,
                weightNames.Zip(weightValues,
                    (weightName, weight) => new MultiplexMatrix.Weighting(weightName, 0, weight)));
        }

        private void AssertSerializable<T>(T value)
        {
            var xmlSerializer = new XmlSerializer(typeof(T));
            var stringWriter = new StringWriter();
            xmlSerializer.Serialize(stringWriter, value);
            var roundTrip = (T)xmlSerializer.Deserialize(new StringReader(stringWriter.ToString()));
            Assert.AreEqual(value, roundTrip);
        }

        [TestMethod]
        public void TestReadMultiplexMatrixFromMsf()
        {
            TestFilesDir = new TestFilesDir(TestContext, @"Test\MultiplexMatrixTest.zip");
            var msfFilePath = TestFilesDir.GetTestPath("JustAnalysisDefinition.msf");
            var measuredIonList = new MeasuredIonList();
            measuredIonList.AddDefaults();
            var customIons = measuredIonList.Where(ion => ion.IsCustom).ToList();

            var msfMultiplexReader = new MsfMultiplexReader(customIons);
            var matrix = msfMultiplexReader.ReadMultiplexMatrix(msfFilePath);
            Assert.AreEqual(10, matrix.Replicates.Count);
            foreach (var replicate in matrix.Replicates)
            {
                Assert.IsTrue(replicate.Weights.Any(kvp => kvp.Weight == 100));
            }
        }


        [TestMethod]
        public void TestGetReplicateQuantities()
        {
            var multiplexMatrix = new MultiplexMatrix("test", new[]
            {
                MakeReplicate("A", new[] { "a" }, new[] { 1.0 }),
                MakeReplicate("B", new[] { "b", "c" }, new[] { 2.0, 3.0 })
            });
            var observations = new Dictionary<string, double>
            {
                { "a", 2 },
                { "b", 6 },
                { "c", 9 }
            };
            // TODO
            // var result = multiplexMatrix.GetMultiplexAreas(observations);
            // Assert.AreEqual(2.0, result[0]);
            // Assert.AreEqual(3.0, result[1]);
        }
    }
}
