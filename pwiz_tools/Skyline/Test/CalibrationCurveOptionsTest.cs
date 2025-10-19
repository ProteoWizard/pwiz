using System;
using System.IO;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class CalibrationCurveOptionsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCalibrationCurveOptionsSerialization()
        {
            var calibrationCurveOptions = CalibrationCurveOptions.DEFAULT.ChangeLogXAxis(true).ChangeLogYAxis(false);
            var serializer = new XmlSerializer(typeof(CalibrationCurveOptions));
            var stringWriter = new StringWriter();
            serializer.Serialize(stringWriter, calibrationCurveOptions);
            Console.Out.WriteLine(stringWriter.ToString());
            var roundTrip = (CalibrationCurveOptions)serializer.Deserialize(new StringReader(stringWriter.ToString()));
            Assert.AreEqual(calibrationCurveOptions.LogYAxis, roundTrip.LogYAxis);
            Assert.AreEqual(calibrationCurveOptions.LogXAxis, roundTrip.LogXAxis);
            Assert.AreEqual(calibrationCurveOptions, roundTrip);
        }
    }
}
