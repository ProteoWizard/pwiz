/*
 * Original author: Nicholas Shulman <nicksh .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

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
            // Console.Out.WriteLine(stringWriter.ToString());
            var roundTrip = (CalibrationCurveOptions)serializer.Deserialize(new StringReader(stringWriter.ToString()));
            Assert.AreEqual(calibrationCurveOptions.LogYAxis, roundTrip.LogYAxis);
            Assert.AreEqual(calibrationCurveOptions.LogXAxis, roundTrip.LogXAxis);
            Assert.AreEqual(calibrationCurveOptions, roundTrip);
        }
    }
}
