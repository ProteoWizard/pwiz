/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Spectra;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests how the full-scan viewer renders the mzML CV/user parameters that Skyline does not
    /// interpret into its own fields: a value whose unit Skyline has a display convention for takes
    /// that convention, and anything else is shown exactly as the file wrote it.
    /// </summary>
    [TestClass]
    public class OtherMetadataInfoTest : AbstractUnitTest
    {
        private const string MZ = "MS:1000040";
        private const string DETECTOR_COUNTS = "MS:1000131";
        private const string INTENSITY = "MS:1000043";
        private const string PPM = "UO:0000169";
        private const string SECOND = "UO:0000010";
        private const string HERTZ = "UO:0000106";

        [TestMethod]
        public void TestOtherMetadataFormatting()
        {
            // A value whose unit has a Skyline convention is shown in that convention, and keeps its unit.
            AssertDisplayed(FormatCurrent(422.2507, Formats.Mz) + @" m/z",
                Term(@"base peak m/z", @"422.250671386719", @"m/z", MZ));
            AssertDisplayed(FormatCurrent(65579108, Formats.PEAK_AREA) + @" number of detector counts",
                Term(@"base peak intensity", @"6.5579108e07", @"number of detector counts", DETECTOR_COUNTS));

            // Values the convention would round away to zero are shown as the file wrote them: an
            // intensity is displayed as whole counts, so a relative intensity would otherwise read "0".
            AssertDisplayed(@"0.4213 intensity unit",
                Term(@"base peak intensity", @"0.4213", @"intensity unit", INTENSITY));
            AssertDisplayed(@"0.03 parts per million",
                Term(@"mass error", @"0.03", @"parts per million", PPM));

            // A value that really is zero still shows as zero.
            AssertDisplayed(FormatCurrent(0, Formats.PEAK_AREA) + @" number of detector counts",
                Term(@"base peak intensity", @"0", @"number of detector counts", DETECTOR_COUNTS));

            // No convention for the unit, no unit at all, or a value that is not a number: shown as written.
            AssertDisplayed(@"9.4 hertz", Term(@"resonance frequency", @"9.4", @"hertz", HERTZ));
            AssertDisplayed(@"20.5108862", Term(@"mass resolving power", @"20.5108862", null, null));
            AssertDisplayed(@"FTMS + p NSI Full ms", Term(@"filter string", @"FTMS + p NSI Full ms", null, null));
            AssertDisplayed(@"not a number m/z", Term(@"odd term", @"not a number", @"m/z", MZ));

            // The second is deliberately left unformatted: Skyline's time convention is in minutes.
            AssertDisplayed(@"0.004 second", Term(@"dwell time", @"0.004", @"second", SECOND));

            // A flag term carries no value, so its row is empty - the term name says it all.
            AssertDisplayed(string.Empty, Term(@"MS1 spectrum", string.Empty, null, null));
        }

        private static SpectrumMetadataTerm Term(string name, string value, string unit, string unitAccession)
        {
            return new SpectrumMetadataTerm(@"MS:0000000", name, value, unit, unitAccession);
        }

        private static string FormatCurrent(double value, string format)
        {
            return value.ToString(format, CultureInfo.CurrentCulture);
        }

        private static void AssertDisplayed(string expected, SpectrumMetadataTerm term)
        {
            var info = new OtherMetadataInfo(new[] { term });
            var descriptor = info.GetProperties().Cast<PropertyDescriptor>().First();
            Assert.AreEqual(expected, descriptor.GetValue(info), TextUtil.SpaceSeparate(@"term:", term.Name));
        }
    }
}
