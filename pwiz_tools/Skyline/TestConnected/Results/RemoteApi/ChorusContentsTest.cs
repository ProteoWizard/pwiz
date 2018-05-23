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
using pwiz.Skyline.Model.Results.RemoteApi.Chorus;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestConnected.Results.RemoteApi
{
    [TestClass]
    public class ChorusContentsTest : AbstractUnitTest
    {
        private static readonly ChorusAccount TEST_ACCOUNT = new ChorusAccount("https://chorusproject.org", "pavel.kaplin@gmail.com", "pwd");
        //[TestMethod]
        public void TestAuthenticate()
        {
            CookieContainer cookieContainer = new CookieContainer();
            ChorusSession chorusSession = new ChorusSession(TEST_ACCOUNT);
            Assert.AreEqual(0, cookieContainer.Count);
            chorusSession.Login(TEST_ACCOUNT, cookieContainer);
            Assert.AreEqual(1, cookieContainer.Count);
        }

        // Disabled 20170123 because Skyline Chorus API is offline
        //[TestMethod]
        public void TestChorusContents()
        {
            ChorusSession chorusSession = new ChorusSession(TEST_ACCOUNT);
            ChorusContents chorusContents = chorusSession.FetchContents(new Uri(TEST_ACCOUNT.ServerUrl + "/skyline/api/contents/my/projects"));
            Assert.IsNotNull(chorusContents);
        }

        /// <summary>
        /// Tests that all instrument models are identified as something by ChorusSession.GetFileTypeFromInstrumentModel
        /// </summary>
        // Disabled 20170123 because Skyline Chorus API is offline
        //[TestMethod]
        public void TestChorusInstrumentModels()
        {
            var accounts = new[]
            {
                new ChorusAccount("https://chorusproject.org", "pavel.kaplin@gmail.com", "pwd"),
            };
            ChorusSession chorusSession = new ChorusSession(TEST_ACCOUNT);
            var instrumentModels = new HashSet<string>();
            foreach (var account in accounts)
            {
                ChorusContents chorusContents;
                for (int retry = 4;; retry--)
                {
                    try
                    {
                        chorusContents = chorusSession.FetchContents(
                            new Uri(account.ServerUrl + "/skyline/api/contents/my/files"));
                        break;
                    }
                    catch (WebException webException)
                    {
                        if (retry == 0 || webException.Status != WebExceptionStatus.Timeout)
                        {
                            throw;
                        }
                    }
                }
                Assert.IsNotNull(chorusContents);
                foreach (var file in ListAllFiles(chorusContents))
                {
                    instrumentModels.Add(file.instrumentModel);
                }
            }
            Assert.AreNotEqual(0, instrumentModels.Count);
            var unknownInstrumentModels = new List<string>();
            foreach (var instrumentModel in instrumentModels)
            {
                if (null == ChorusSession.GetFileTypeFromInstrumentModel(instrumentModel))
                {
                    unknownInstrumentModels.Add(instrumentModel);
                }
            }
            if (0 != unknownInstrumentModels.Count)
            {
                String message = string.Format("Unknown instrument models {0}", string.Join(",", unknownInstrumentModels));

                try
                {
                    TestContext.WriteLine(message);
                }
                catch (Exception)
                {
                    Console.Error.WriteLine(message);
                }
            }
        }

        IEnumerable<ChorusContents.File> ListAllFiles(ChorusContents chorusContents)
        {
            return SafeList(chorusContents.myFiles)
                .Concat(SafeList(chorusContents.myExperiments).SelectMany(ListAllFiles))
                .Concat(SafeList(chorusContents.myProjects).SelectMany(ListAllFiles))
                .Concat(SafeList(chorusContents.sharedFiles))
                .Concat(SafeList(chorusContents.sharedExperiments).SelectMany(ListAllFiles))
                .Concat(SafeList(chorusContents.sharedProjects).SelectMany(ListAllFiles))
                .Concat(SafeList(chorusContents.publicFiles)
                .Concat(SafeList(chorusContents.publicExperiments).SelectMany(ListAllFiles))
                .Concat(SafeList(chorusContents.publicProjects).SelectMany(ListAllFiles)))
                .Concat(SafeList(chorusContents.files));
        }

        IEnumerable<ChorusContents.File> ListAllFiles(ChorusContents.Project project)
        {
            return project.experiments.SelectMany(ListAllFiles);
        }

        IEnumerable<ChorusContents.File> ListAllFiles(ChorusContents.Experiment experiment)
        {
            return experiment.files;
        }
        private static IEnumerable<T> SafeList<T>(IEnumerable<T> list)
        {
            return list ?? new T[0];
        }
    }
}
