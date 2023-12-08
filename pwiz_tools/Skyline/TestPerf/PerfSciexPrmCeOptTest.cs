/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfSciexPrmCeOptTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSciexPrmCeOptimization()
        {
            TestFilesZip = @"https://panoramaweb.org/_webdav/MacCoss/software/%40files/perftests/SciexPrmCeOptTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("doc2.sky"));
            });
            RunDlg<ImportResultsDlg>(SkylineWindow.ImportResults, importResultsDlg=>
            
            {
                importResultsDlg.NamedPathSets = new[]
                {
                    new KeyValuePair<string, MsDataFileUri[]>("110922 PCM_40f RT CEOpt A1",
                        new[] { new MsDataFilePath(TestFilesDir.GetTestPath("110922 PCM_40f RT CEOpt A1.wiff2")) })
                };
                importResultsDlg.OptimizationName = ExportOptimize.CE;
                importResultsDlg.OkDialog();
            });
            WaitForDocumentLoaded();
            var expectedResults = new[]
            {
                Tuple.Create("LVGTPAEER", -1),
                Tuple.Create("AGLIVAEGVTK", 0),
                Tuple.Create("VFTPLEVDVAK", -1),
                Tuple.Create("SGGLLWQLVR", 0)
            };
            var transitionGroups = SkylineWindow.Document.MoleculeTransitionGroups.ToList();
            Assert.AreEqual(transitionGroups.Count, expectedResults.Length);
            for (int i = 0; i < transitionGroups.Count; i++)
            {
                var transitionGroup = transitionGroups[i];
                Assert.AreEqual(expectedResults[i].Item1, transitionGroup.Peptide.Sequence, "Wrong peptide sequence at index {0}", i);
                Assert.AreEqual(expectedResults[i].Item2, GetBestOptimizationStep(transitionGroup), "Wrong best optimization step for {0}", transitionGroup);
            }
        }

        private int? GetBestOptimizationStep(TransitionGroupDocNode transitionGroupDocNode)
        {
            if (!transitionGroupDocNode.HasResults)
            {
                return null;
            }

            return transitionGroupDocNode.Results.SelectMany(r => r).OrderByDescending(chromInfo => chromInfo.Area)
                .First().OptimizationStep;
        }
    }
}
