/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Summary description for PerfGenerateChorusRequests
    /// </summary>
    [TestClass]
    public class PerfGenerateChorusRequests : AbstractFunctionalTest
    {
        public const string CHORUS_DATASET_URL = "http://proteome.gs.washington.edu/~nicksh/chorus/SkylineFiles/";

        public static readonly IList<ChorusDataSet> ChorusDataSets = new ReadOnlyCollection<ChorusDataSet>(new[]
        {
            new ChorusDataSet
            {
                Name = "Agilent_DDA",
                SkyZipUrl = CHORUS_DATASET_URL + "Agilent/BSA_Agilent_DDA.sky.zip",
                MsDataZipUrl = CHORUS_DATASET_URL + "Agilent/Agilent-DDA.zip",
            },
            new ChorusDataSet
            {
                Name = "Agilent_DIA-profile",
                SkyZipUrl = CHORUS_DATASET_URL + "Agilent/BSA_Agilent_DIA-profile.sky.zip",
                MsDataZipUrl = CHORUS_DATASET_URL + "Agilent/DIA-profile.zip"
            },
            new ChorusDataSet
            {
                Name = "Bruker_DDA",
                SkyZipUrl = CHORUS_DATASET_URL + "Bruker/Bruker%20DDA.sky.zip"
            },
            new ChorusDataSet
            {
                Name = "Bruker_MSe",
                SkyZipUrl = CHORUS_DATASET_URL + "Bruker/Bruker_MSe.sky.zip",
            },
            new ChorusDataSet
            {
                Name = "Bruker_SWATH",
                SkyZipUrl = CHORUS_DATASET_URL + "Bruker/Bruker%20SWATH.sky.zip",
            },
            new ChorusDataSet
            {
                Name = "Thermo_DDA",
                SkyZipUrl = CHORUS_DATASET_URL + "Thermo/Hoofnagle_QE_DDA_targeted.sky.zip",
            },
            new ChorusDataSet
            {
                Name="Thermo_DIA",
                SkyZipUrl = CHORUS_DATASET_URL + "Thermo/Hoofnagle_QE_DIA_targeted.sky.zip"
            },
            new ChorusDataSet
            {
                Name="Waters_MSe",
                SkyZipUrl = CHORUS_DATASET_URL + "Waters/Hoofnagle_MSe_targeted.sky.zip",
            }
        });
        [TestMethod]
        public void TestGenerateChorusRequests()
        {
            List<string> testFilesZipPaths = new List<string>();
            foreach (ChorusDataSet chorusDataSet in ChorusDataSets)
            {
                testFilesZipPaths.Add(chorusDataSet.SkyZipUrl);
            }
            TestFilesZipPaths = testFilesZipPaths.ToArray();
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            string chorusRequestOutputDirectory;
            if (null != TestContext.TestResultsDirectory)
            {
                chorusRequestOutputDirectory = Path.Combine(TestContext.TestResultsDirectory, "chorusrequests");
                Directory.CreateDirectory(chorusRequestOutputDirectory);
            }
            else
            {
                // When running unit tests under resharper, we have no TestResultsDirectory, so we only send
                // the output to Console.Out.
                chorusRequestOutputDirectory = null;
            }
            var xmlSerializer = new XmlSerializer(typeof(pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode.ChromatogramRequestDocument));

            foreach (var chorusDataSet in ChorusDataSets)
            {
                TestFilesDir testFilesDir = TestFilesDirForUrl(chorusDataSet.SkyZipUrl);
                string skyFileName = Path.GetFileName(testFilesDir.FullPath);
                Assert.IsNotNull(skyFileName, "skyFileName != null");
                StringAssert.EndsWith(skyFileName, ".sky");
                skyFileName = Uri.UnescapeDataString(skyFileName);
                string skyFilePath = Path.Combine(testFilesDir.FullPath, skyFileName);
                RunUI(()=>SkylineWindow.OpenFile(skyFilePath));
                WaitForDocumentLoaded();
                SrmDocument document = null;
                RunUI(()=> { document = SkylineWindow.DocumentUI; });
                Assert.IsNotNull(document);
                SpectrumFilter spectrumFilterData = new SpectrumFilter(document, MsDataFileUri.Parse(""), null);
                var chorusRequestDocument = spectrumFilterData.ToChromatogramRequestDocument();
                Console.Out.WriteLine("***BEGIN {0}.chorusrequest.xml***", chorusDataSet.Name);
                using (var xmlWriter = XmlWriter.Create(Console.Out, new XmlWriterSettings {Encoding = Encoding.UTF8}))
                {
                    xmlSerializer.Serialize(xmlWriter, chorusRequestDocument);
                }
                Console.Out.WriteLine("***END {0}.chorusrequest.xml***", chorusDataSet.Name);
                if (null != chorusRequestOutputDirectory)
                {
                    string outputFile = Path.Combine(chorusRequestOutputDirectory,
                        chorusDataSet.Name + ".chorusrequest.xml");
                    using (var stream = new FileStream(outputFile, FileMode.Create))
                    {
                        xmlSerializer.Serialize(stream, chorusRequestDocument);
                    }
                }
            }
        }

        protected TestFilesDir TestFilesDirForUrl(string url)
        {
            string zipFileName = url.Substring(url.LastIndexOf("/", StringComparison.InvariantCulture) + 1);
            
            foreach (var testFilesDir in TestFilesDirs)
            {
                if (Path.GetFileName(testFilesDir.PersistentFilesDir) + ".zip" == zipFileName)
                {
                    return testFilesDir;
                }
            }
            throw new ArgumentException("Unable to find test files dir for " + url);
        }

        public struct ChorusDataSet
        {
            public string Name;
            public string SkyZipUrl;
            public string MsDataZipUrl;
        }
    }
}
