/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.Skyline.Model.DocSettings;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The search settings page collects a maximum number of variable modifications per peptide and
    /// hands it to every search engine through SetModifications. MS Amanda takes it as MaxNoDynModifs
    /// in the settings XML the wrapper generates, and nothing else in the pipeline reads that value
    /// back, so a settings XML built without it would look correct everywhere but the search.
    /// </summary>
    [TestClass]
    public class MSAmandaSearchSettingsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestMaxVariableModsReachesSettingsXml()
        {
            var mods = new[]
            {
                new StaticMod(@"Carbamidomethyl (C)", @"C", null, @"C2H3ON"),
                new StaticMod(@"Oxidation (M)", @"M", null, @"O").ChangeVariable(true)
            };

            using (var searchEngine = new MSAmandaSearchWrapper())
            {
                // One parameter, one control: MaxNoDynModifs is driven by the max variable mods
                // setting, so it must not also appear in the additional settings grid.
                Assert.IsFalse(searchEngine.AdditionalSettings.ContainsKey(@"MaxNoDynModifs"),
                    "MaxNoDynModifs should not be an additional setting");

                foreach (int maxVariableMods in new[] { 0, 2, 9 })
                {
                    searchEngine.SetModifications(mods, maxVariableMods);
                    AssertEx.Contains(searchEngine.BuildSettingsXml(), string.Format(CultureInfo.InvariantCulture,
                        @"<MaxNoDynModifs>{0}</MaxNoDynModifs>", maxVariableMods));
                }
            }
        }
    }
}
