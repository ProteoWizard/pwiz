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
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class QcTraceNameTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestQcTraceName()
        {
            TestFilesZip = @"TestFunctional\QcTraceNameTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PressureTrace1.sky"));
            });
            ImportResults(TestFilesDir.GetTestPath("PressureTrace1.wiff"));
            var qcTraceNames = SkylineWindow.Document.Settings.MeasuredResults.QcTraceNames.ToList();
            var expectedQcTraceNames = new List<string>
            {
                "Column Pressure (channel 1)",
                "Pump A Flowrate (channel 2)",
                "Pump B Flowrate (channel 3)",
                "Column Pressure (channel 4)",
                "Pump A Flowrate (channel 5)",
                "Pump B Flowrate (channel 6)",
            };
            qcTraceNames.Sort(StringComparer.Ordinal);
            expectedQcTraceNames.Sort(StringComparer.Ordinal);
            Assert.AreEqual(expectedQcTraceNames.Count, qcTraceNames.Count);
            for (int i = 0; i < expectedQcTraceNames.Count; i++)
            {
                var qcTraceName = qcTraceNames[i];
                Assert.AreEqual(expectedQcTraceNames[i], qcTraceName, "Mismatch at position {0}", i);
                RunUI(()=>SkylineWindow.ShowQc(qcTraceName));
                WaitForGraphs();
            }
        }
    }
}
