/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using System.Globalization;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfOpenSwathConvert : AbstractUnitTest 
    {
        private static readonly string[] INDIVIDUAL_OUTPUT =
        {
            "OpenSwathT20131126_Study9_2_SWATH_sampleA_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleB_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleC_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleD_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleE_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleF_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleG_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleH_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleI_01.mzXML.gz.tsv",
            "OpenSwathT20131126_Study9_2_SWATH_sampleJ_01.mzXML.gz.tsv",
        };

        private const string TRANSITION_GROUP = "transition_group_id";
        private const string FILE_NAME = "filename";
        private const string RUN_ID = "run_id";
        private const string MS_FILE_TYPE = ".wiff";
        private const char SEPARATOR = TextUtil.SEPARATOR_TSV;

        // [TestMethod]  // disabling this since the test file URLs don't exist
        public void ConvertOpenSwathPerf()
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global "allow perf tests" flag is set

            TestFilesZip =
                @"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikProcessing.zip"; 
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZip, null, TestFilesPersistent);                 


            var inFiles = INDIVIDUAL_OUTPUT.Select(testFilesDir.GetTestPath);
            string outFile = testFilesDir.GetTestPath("Spectronaut.csv");
            RunConversion(inFiles, outFile);
        }

        public void RunConversion(IEnumerable<string> individualInput, string combinedOutput)
        {
            using (var fs = new FileSaver(combinedOutput))
            using (var writer = new StreamWriter(fs.SafeName))
            {
                bool first = true;
                var fields = new List<string>();
                int currentFileCount = 0;
                foreach (var inputFile in individualInput)
                {
                    using (var reader = new StreamReader(inputFile))
                    {
                        fields = TranscribeAndModifyFile(writer, reader, fields, first, currentFileCount);
                    }
                    first = false;
                    ++currentFileCount;
                }
                writer.Close();
                fs.Commit();
            }
        }

        public List<string> TranscribeAndModifyFile(StreamWriter writer, TextReader reader, List<string> fields, bool first, int currentFileCount)
        {
            var fileReader = new DsvFileReader(reader, SEPARATOR);
            if (first)
            {
                fields = fileReader.FieldNames;
                for (int i = 0; i < fields.Count; ++i)
                {
                    if (i > 0)
                        writer.Write(SEPARATOR);
                    writer.WriteDsvField(fileReader.FieldNames[i], SEPARATOR);
                }
                writer.WriteLine();
            }

            Assert.AreEqual(fileReader.NumberOfFields, fields.Count);
            for (int i = 0; i < fields.Count; ++i)
            {
                Assert.AreEqual(fileReader.FieldNames[i], fields[i]);
            }

            while (fileReader.ReadLine() != null)
            {
                for (int i = 0; i < fields.Count; ++i)
                {
                    string modifiedField = fileReader.GetFieldByIndex(i);
                    switch (fileReader.FieldNames[i])
                    {
                        case FILE_NAME:
                            modifiedField = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(modifiedField)) + MS_FILE_TYPE;
                            break;
                        case TRANSITION_GROUP:
                            modifiedField = modifiedField + currentFileCount;
                            break;
                        case RUN_ID:
                            modifiedField = currentFileCount.ToString(CultureInfo.CurrentCulture);
                            break;
                    }
                    if (i > 0)
                        writer.Write(SEPARATOR);
                    writer.WriteDsvField(modifiedField, SEPARATOR);
                }
                writer.WriteLine();
            }
            return fields;
        }
    }
}
