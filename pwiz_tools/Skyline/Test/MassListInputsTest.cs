/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class MassListInputsTest : AbstractUnitTest
    {
        private static readonly string[] HEADERS = { "ProteinName", "PeptideSequence", "PrecursorMz", "ProductMz" };
        private static readonly object[] VALUES = { "BOV_ALB", "PEPSTIDER", 524.73, 615.87 };

        /// <summary>
        /// Test that various invariant and international formats initialize correctly
        /// regardless of the current culture.
        /// </summary>
        [TestMethod]
        public void MassListInputsFormatTest()
        {
            var culture = (CultureInfo) CultureInfo.InvariantCulture.Clone();
            ValidateFormats(culture);   // comma with period decimal separator
            var cultureEu = (CultureInfo) CultureInfo.GetCultureInfo("fr-FR").Clone();
            ValidateFormats(cultureEu);   // semi-colon with comma decimal separator
            culture.TextInfo.ListSeparator = "\t";
            ValidateFormats(culture);   // tab with period decimal separator
            cultureEu.TextInfo.ListSeparator = "\t";
            ValidateFormats(cultureEu);   // tab with comma decimal separator
            culture.TextInfo.ListSeparator = ";";
            ValidateFormats(culture);   // semi-colon with period decimal separator
        }

        private void ValidateFormats(CultureInfo culture)
        {
            var sep = culture.TextInfo.ListSeparator;
            string headerLine = string.Join(sep, HEADERS);
            string valueLine = string.Join(sep, VALUES.Select(v => string.Format(culture, "{0}", v)));
            ValidateFormats(culture, valueLine);
            ValidateFormats(culture, TextUtil.LineSeparate(headerLine, valueLine), true);
        }

        private void ValidateFormats(CultureInfo culture, string text, bool hasHeader = false)
        {
            try
            {
                var inputs = new MassListInputs(text, true);
                var lines = inputs.ReadLines(new SilentProgressMonitor());
                Assert.AreEqual(hasHeader ? 2 : 1, lines.Count);
                var columns = lines.Last().Split(inputs.Separator);
                Assert.AreEqual(VALUES[2], double.Parse(columns[2], culture));
                Assert.AreEqual(VALUES[3], double.Parse(columns[3], culture));
            }
            catch (Exception e)
            {
                Assert.Fail("Unexpected exception thrown " + e);
            }
        }
    }
}
