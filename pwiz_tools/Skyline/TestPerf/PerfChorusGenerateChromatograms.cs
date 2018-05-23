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
using System.Threading;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    /// <summary>
    /// Summary description for PerfChorusGenerateChromatograms
    /// </summary>
    [TestClass]
    public class PerfChorusGenerateChromatograms : AbstractUnitTest
    {
        private static readonly ChorusAccount TEST_ACCOUNT = new ChorusAccount("https://chorusproject.org", "pavel.kaplin@gmail.com", "pwd");
// Disabled 20170131 because Skyline Chorus API is offline
//        [TestMethod] TODO(nicksh) re-enable when Chorus is reliable
        public void TestThermoDIA()
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global "allow perf tests" flag is set

            var xmlSerializer = new XmlSerializer(typeof (ChromatogramRequestDocument));
            var stream = typeof (PerfChorusGenerateChromatograms).Assembly.GetManifestResourceStream(
                typeof (PerfChorusGenerateChromatograms), "ThermoDIA.ChorusRequest.xml");
            Assert.IsNotNull(stream);
            var chromatogramRequestDocument = (ChromatogramRequestDocument) xmlSerializer.Deserialize(stream);
            var chorusUrl = TEST_ACCOUNT.GetChorusUrl().SetFileId(28836);
            var srmDocument = new SrmDocument(SrmSettingsList.GetDefault());
            DateTime startTime = DateTime.Now;
            ChromTaskList chromTaskList = new ChromTaskList(()=>{}, srmDocument, TEST_ACCOUNT, chorusUrl, new[]{chromatogramRequestDocument});
            chromTaskList.SetMinimumSimultaneousTasks(2);
            while (chromTaskList.PercentComplete < 100)
            {
                Thread.Sleep(100);
            }
            DateTime endTime = DateTime.Now;
            AssertEx.AreNoExceptions(chromTaskList.ListExceptions());
            Console.Out.WriteLine("Elapsed time {0}", endTime.Subtract(startTime));
        }
// Disabled 20170131 because Skyline Chorus API is offline
//        [TestMethod] TODO(nicksh) re-enable when Chorus is reliable
        public void TestThermoDIAChunked()
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global "allow perf tests" flag is set

            var xmlSerializer = new XmlSerializer(typeof(ChromatogramRequestDocument));
            var stream = typeof(PerfChorusGenerateChromatograms).Assembly.GetManifestResourceStream(
                typeof(PerfChorusGenerateChromatograms), "ThermoDIA.ChorusRequest.xml");
            Assert.IsNotNull(stream);
            var chromatogramRequestDocument = (ChromatogramRequestDocument)xmlSerializer.Deserialize(stream);
            var chorusUrl = TEST_ACCOUNT.GetChorusUrl().SetFileId(28836);
            var srmDocument = new SrmDocument(SrmSettingsList.GetDefault());
            foreach (int chunkChromatogramCount in new[] { 50, 100, 150, 200, 300 })
            {
                ChromTaskList chromTaskList = new ChromTaskList(() => { }, srmDocument, TEST_ACCOUNT, chorusUrl, ChromTaskList.ChunkChromatogramRequest(chromatogramRequestDocument, chunkChromatogramCount));
                chromTaskList.SetMinimumSimultaneousTasks(30);
                DateTime startTime = DateTime.Now;
                while (chromTaskList.PercentComplete < 100)
                {
                    Thread.Sleep(100);
                }
                DateTime endTime = DateTime.Now;
                AssertEx.AreNoExceptions(chromTaskList.ListExceptions());
                Console.Out.WriteLine("*******************************************");
                Console.Out.WriteLine("Chromatograms per chunk: {0}", chunkChromatogramCount);
                Console.Out.WriteLine("Number of chunks: {0}", chromTaskList.TaskCount);
                Console.Out.WriteLine("Elapsed time {0}", endTime.Subtract(startTime));
                Console.Out.WriteLine("*******************************************");
            }
        }
    }
}
