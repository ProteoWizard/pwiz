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
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results.RemoteApi;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfChorusContentsTest : AbstractUnitTest
    {
        private static readonly ChorusAccount TEST_ACCOUNT = new ChorusAccount("https://dev.chorusproject.org", "pavel.kaplin@gmail.com", "pwd");
        [TestMethod]
        public void TestAuthenticate()
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global "allow perf tests" flag is set
            CookieContainer cookieContainer = new CookieContainer();
            ChorusSession chorusSession = new ChorusSession();
            Assert.AreEqual(0, cookieContainer.Count);
            chorusSession.Login(TEST_ACCOUNT, cookieContainer);
            Assert.AreEqual(1, cookieContainer.Count);
        }

        [TestMethod]
        public void TestContents()
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global "allow perf tests" flag is set
            ChorusSession chorusSession = new ChorusSession();
            ChorusContents chorusContents = chorusSession.FetchContents(TEST_ACCOUNT, new Uri(TEST_ACCOUNT.ServerUrl + "/skyline/api/contents"));
            Assert.IsNotNull(chorusContents);
        }

        /// <summary>
        /// Tests that all instrument models are identified as something by ChorusSession.GetFileTypeFromInstrumentModel
        /// </summary>
        [TestMethod]
        public void TestInstrumentModels()
        {
            if (!RunPerfTests)
                return; // PerfTests only run when the global "allow perf tests" flag is set
            var accounts = new[]
            {
                new ChorusAccount("https://dev.chorusproject.org", "pavel.kaplin@gmail.com", "pwd"),
                new ChorusAccount("https://chorusproject.org", "pavel.kaplin@gmail.com", "pwd"),
            };
            ChorusSession chorusSession = new ChorusSession();
            var instrumentModels = new HashSet<string>();
            foreach (var account in accounts)
            {
                ChorusContents chorusContents = chorusSession.FetchContents(account, new Uri(account.ServerUrl + "/skyline/api/contents"));
                Assert.IsNotNull(chorusContents);
                foreach (var file in ListAllFiles(chorusContents))
                {
                    instrumentModels.Add(file.instrumentModel);
                }
            }
            var unknownInstrumentModels = new List<string>();
            foreach (var instrumentModel in instrumentModels)
            {
                if (null == ChorusSession.GetFileTypeFromInstrumentModel(instrumentModel))
                {
                    unknownInstrumentModels.Add(instrumentModel);
                }
            }
            Assert.AreEqual(0, unknownInstrumentModels.Count, "Unknown instrument models {0}", string.Join(",", unknownInstrumentModels));
        }

        IEnumerable<ChorusContents.File> ListAllFiles(ChorusContents chorusContents)
        {
            return chorusContents.myFiles
                .Concat(chorusContents.myExperiments.SelectMany(ListAllFiles))
                .Concat(chorusContents.myProjects.SelectMany(ListAllFiles))
                .Concat(chorusContents.sharedFiles)
                .Concat(chorusContents.sharedExperiments.SelectMany(ListAllFiles))
                .Concat(chorusContents.sharedProjects.SelectMany(ListAllFiles))
                .Concat(chorusContents.publicFiles)
                .Concat(chorusContents.publicExperiments.SelectMany(ListAllFiles))
                .Concat(chorusContents.publicProjects.SelectMany(ListAllFiles));
        }

        IEnumerable<ChorusContents.File> ListAllFiles(ChorusContents.Project project)
        {
            return project.experiments.SelectMany(ListAllFiles);
        }

        IEnumerable<ChorusContents.File> ListAllFiles(ChorusContents.Experiment experiment)
        {
            return experiment.files;
        }
    }

}
