/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
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

using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    [TestClass]
    public class NormalizeProductIonTest : AbstractUnitTest
    {
        [TestMethod]
        public void NormalizationTest()
        {
            string dot = LocalizationHelper.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var ions = new Dictionary<string, string>
            {
                {"this is not an ion", "this is not an ion"},
                {"a2", "a2"},
                {"  B24  ", "b24"},
                {"c2 -98", "c2 -98"},
                {"x2 -98"+dot+"0", "x2 -98"},
                {"y2 -98"+dot+"44", "y2 -98"+dot+"4"},
                {"z2 -98"+dot+"5", "z2 -98"+dot+"5"},
                {"y2 -98"+dot+"64", "y2 -98"+dot+"6"},
                {"y2-98"+dot+"44", "y2 -98"+dot+"4"},
                {"y2- 98"+dot+"44", "y2 -98"+dot+"4"},
                {"  y2  -  98"+dot+"44  ", "y2 -98"+dot+"4"},
                {"precursor", "precursor"},
                {"PRECURSOR", "precursor"},
                {"precursor -98", "precursor -98"},
                {"precursor-98"+dot+"44", "precursor -98"+dot+"4"},
                {"precursor  -  98"+dot+"44", "precursor -98"+dot+"4"},
            };

            foreach (var ion in ions)
                Assert.AreEqual(ion.Value, EditOptimizationLibraryDlg.LibraryGridViewDriver.NormalizeProductIon(ion.Key));
        }
    }
}
