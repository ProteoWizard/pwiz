using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Summary description for PerfGenerateChorusRequests
    /// </summary>
    [TestClass]
    public class PerfGenerateChorusRequests : AbstractFunctionalTest
    {
        public static readonly string CHORUS_DATASET_URL = "http://proteome.gs.washington.edu/~nicksh/chorus/SkylineFiles/";

        public static IList<ChorusDataSet> ChorusDataSets = new ReadOnlyCollection<ChorusDataSet>(new[]
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
            string chorusRequestOutputDirectory = Path.Combine(TestContext.TestResultsDirectory, "chorusrequests");
            Directory.CreateDirectory(chorusRequestOutputDirectory);
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
                string outputFile = Path.Combine(chorusRequestOutputDirectory, chorusDataSet.Name + ".chorusrequest.xml");
                var chorusRequestDocument = spectrumFilterData.ToChromatogramRequestDocument();
                using (var stream = new FileStream(outputFile, FileMode.Create))
                {
                    xmlSerializer.Serialize(stream, chorusRequestDocument);
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
