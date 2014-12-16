/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Topograph.Enrichment;

namespace pwiz.Topograph.Test
{
    /// <summary>
    /// Summary description for PrecursorPoolTest
    /// </summary>
    [TestClass]
    public class PrecursorPoolTest : BaseTest
    {
        [TestMethod]
        public void Test58Percent()
        {
            var path = Path.Combine(TestContext.TestDir, Guid.NewGuid().ToString() + ".tpg");
            var workspace = CreateWorkspace(path, TracerDef.GetD3LeuEnrichment());
            var turnoverCalculator = new TurnoverCalculator(workspace, "LL");
            var dict = new Dictionary<TracerFormula, double>();
            // initialize the dictionary with 70% newly synthesized from 58% precursor pool
            dict.Add(TracerFormula.Parse("Tracer2"), Math.Pow(.58, 2) * .7);
            dict.Add(TracerFormula.Parse("Tracer1"), 2 * .58 * .42 * .7);
            dict.Add(TracerFormula.Empty, 1- dict.Values.Sum());
            double turnover;
            double turnoverScore;
            IDictionary<TracerFormula, double> bestMatch;
            var precursorEnrichment = turnoverCalculator.ComputePrecursorEnrichmentAndTurnover(dict, out turnover, out turnoverScore, out bestMatch);
            Assert.AreEqual(.7, turnover);
            Assert.AreEqual(58, precursorEnrichment["Tracer"]);
        }
    }
}
