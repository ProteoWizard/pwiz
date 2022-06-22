/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class FeatureTooltipTest : AbstractUnitTest
    {
        /// <summary>
        /// Ensures that all peak feature calculators have an entry in FeatureTooltips.resx
        /// </summary>
        [TestMethod]
        public void TestFeatureTooltips()
        {
            var missingTooltips = new List<string>();
            foreach (var calculator in PeakFeatureCalculator.Calculators.OrderBy(c => c.FullyQualifiedName,
                         StringComparer.InvariantCultureIgnoreCase))
            {
                var tooltip = calculator.Tooltip;
                if (string.IsNullOrEmpty(tooltip))
                {
                    Console.Out.WriteLine("<data name=\"{0}\" xml:space=\"preserve\"><value></value></data>",
                        calculator.FullyQualifiedName);
                    missingTooltips.Add(calculator.HeaderName);
                }
            }

            Assert.AreEqual(0, missingTooltips.Count, "Missing tooltips for {0}", string.Join(",", missingTooltips));
        }
    }
}
