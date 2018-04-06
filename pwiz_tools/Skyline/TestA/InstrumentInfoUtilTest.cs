/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for InstrumentInfoUtilTest
    /// </summary>
    [TestClass]
    public class InstrumentInfoUtilTest : AbstractUnitTest
    {

        [TestMethod]
        public void TestParse()
        {
            var instrumentConfigList = InstrumentInfoUtil.GetInstrumentInfo(null).ToList();
            Assert.AreEqual(0, instrumentConfigList.Count());

            instrumentConfigList = InstrumentInfoUtil.GetInstrumentInfo("").ToList();
            Assert.AreEqual(0, instrumentConfigList.Count());


            var str = "\n\n";
            instrumentConfigList = InstrumentInfoUtil.GetInstrumentInfo(str).ToList();
            Assert.AreEqual(0, instrumentConfigList.Count(), str);

            str = "\n" + InstrumentInfoUtil.MODEL;
            instrumentConfigList = InstrumentInfoUtil.GetInstrumentInfo(str).ToList();
            Assert.AreEqual(0, instrumentConfigList.Count(), str);


            instrumentConfigList = InstrumentInfoUtil.GetInstrumentInfo(INFO1).ToList();
            Assert.AreEqual(0, instrumentConfigList.Count(), INFO1);

            
            instrumentConfigList = InstrumentInfoUtil.GetInstrumentInfo(INFO2).ToList();
            Assert.AreEqual(1, instrumentConfigList.Count(), INFO2);

            // check instrument model
            Assert.AreEqual("MS_TSQ_Vantage", instrumentConfigList[0].Model);
            // check ionization type
            Assert.AreEqual("", instrumentConfigList[0].Ionization);
            // check analyzer
            Assert.AreEqual("", instrumentConfigList[0].Analyzer);
            // check detector
            Assert.AreEqual("", instrumentConfigList[0].Detector);

            instrumentConfigList = InstrumentInfoUtil.GetInstrumentInfo(INFO3).ToList();
            Assert.AreEqual(1, instrumentConfigList.Count(), INFO3);

            instrumentConfigList = InstrumentInfoUtil.GetInstrumentInfo(INFO4).ToList();
            Assert.AreEqual(2, instrumentConfigList.Count(), INFO4);

            // check instrument model 1
            Assert.AreEqual("MS_TSQ_Vantage", instrumentConfigList[0].Model);

            // check instrument model 2
            Assert.AreEqual("MS_LTQ_FT", instrumentConfigList[1].Model);

            // check ionization type 1
            Assert.AreEqual("", instrumentConfigList[0].Ionization);

            // check ionization type 2
            Assert.AreEqual("MS_ionization_type", instrumentConfigList[1].Ionization);

            // check analyzer 1
            Assert.AreEqual("", instrumentConfigList[0].Analyzer);

            // check analyzer 2
            Assert.AreEqual("", instrumentConfigList[1].Analyzer);

            // check detector 1
            Assert.AreEqual("", instrumentConfigList[0].Detector);

            // check detector 2
            Assert.AreEqual("", instrumentConfigList[1].Detector);
        }

        [TestMethod]
        public void TestInvalidInfo()
        {
            try
            {
                InstrumentInfoUtil.GetInstrumentInfo(INFO5);
                Assert.Fail("Expected IOException parsing invalid instrument info string");
            }
            catch (IOException e)
            {
                AssertEx.AreComparableStrings(Resources.InstrumentInfoUtil_ReadInstrumentConfig_Unexpected_line_in_instrument_config__0__, e.Message, 1);
            }
        }

        [TestMethod]
        public void TestConvert()
        {
            Assert.AreEqual("", InstrumentInfoUtil.GetInstrumentInfoString(null));

            List<MsInstrumentConfigInfo> instrumentInfoList = new List<MsInstrumentConfigInfo>();

            Assert.AreEqual("", InstrumentInfoUtil.GetInstrumentInfoString(instrumentInfoList));

            instrumentInfoList.Add(new MsInstrumentConfigInfo("MS_TSQ_Vantage ", null, null, null));
            // trailing white space should have been removed from model name.
            Assert.AreEqual(INFO1_WRITE, InstrumentInfoUtil.GetInstrumentInfoString(instrumentInfoList));

            // Add an empty instrument info
            instrumentInfoList.Add(new MsInstrumentConfigInfo("  ", null, null, null));
            // The empty instrument info should not have been written
            Assert.AreEqual(INFO1_WRITE, InstrumentInfoUtil.GetInstrumentInfoString(instrumentInfoList));

            // Add another instrument info
            instrumentInfoList.Add(new MsInstrumentConfigInfo("MS_LTQ_FT", "MS ionization\ntype", null, null));
            // Internal \n in ionization type string should habe been removed
            Assert.AreEqual(INFO2_WRITE, InstrumentInfoUtil.GetInstrumentInfoString(instrumentInfoList));
        }

        private const string INFO1 =
            InstrumentInfoUtil.MODEL +
            "\n" +
            InstrumentInfoUtil.IONIZATION +
            "\n";

        private const string INFO2 =
            InstrumentInfoUtil.MODEL + "MS_TSQ_Vantage" +
            "\n" +
            InstrumentInfoUtil.IONIZATION +
            "\n" +
            InstrumentInfoUtil.ANALYZER + " " +
            "\n";

       

        private const string INFO3 =
            INFO2 +
            "\n" +
            InstrumentInfoUtil.MODEL +
            "\n" +
            InstrumentInfoUtil.IONIZATION +
            "\n" +
            InstrumentInfoUtil.ANALYZER +
            "\n";

        private const string INFO4 =
            INFO3 +
            "\n" +
            InstrumentInfoUtil.MODEL + "MS_LTQ_FT" +
            "\n" +
            InstrumentInfoUtil.IONIZATION + "MS_ionization_type" +
            "\n" +
            InstrumentInfoUtil.ANALYZER +
            "\n" +
            InstrumentInfoUtil.DETECTOR;

        private const string INFO5 =
            "\n" +
            InstrumentInfoUtil.MODEL + "MS_LTQ_FT" +
            "\n" +
            InstrumentInfoUtil.IONIZATION + "MS_ionization_type" +
            "\nInvalid line" +
            "\n" +
            InstrumentInfoUtil.ANALYZER + "MS analyzer" +
            "\n" +
            InstrumentInfoUtil.DETECTOR + "MS detector";

        private const string INFO1_WRITE =
           InstrumentInfoUtil.MODEL + "MS_TSQ_Vantage\n";


        private const string INFO2_WRITE =
            INFO1_WRITE +
            "\n"+
            InstrumentInfoUtil.MODEL + "MS_LTQ_FT" +
            "\n" +
            InstrumentInfoUtil.IONIZATION + "MS ionization type" +
            "\n";
    }
}
