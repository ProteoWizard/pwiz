/*
 * Original author: Rita Chupalov <ritach .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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

using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// DpiUtil.PrepareIcons finds the resource name of a designer image by its pixels, so
    /// every small Resources.resx bitmap must map back to its own name - or to a twin with
    /// identical pixels, listed here so a new ambiguity is caught when an icon is added.
    /// </summary>
    [TestClass]
    public class DpiIconLookupTest : AbstractUnitTest
    {
        private const int ICON_NATIVE_MAX = 16;

        // Pairs whose natives are pixel-identical: the lookup returns the ordinal-first name
        private static readonly Dictionary<string, string> PIXEL_TWINS = new Dictionary<string, string>
        {
            { @"PeakBlank", @"Blank" },
            { @"ReplicateMissing", @"FileMissing" }
        };

        [TestMethod]
        public void TestDpiIconLookup()
        {
            using (var save = Resources.Save)
                Assert.AreEqual(nameof(Resources.Save), DpiUtil.GetIconName(save));
            using (var statusButton = Resources.AllIonsStatusButton)   // 14 px native
                Assert.AreEqual(nameof(Resources.AllIonsStatusButton), DpiUtil.GetIconName(statusButton));
            using (var unknown = new Bitmap(ICON_NATIVE_MAX, ICON_NATIVE_MAX))
                Assert.IsNull(DpiUtil.GetIconName(unknown));
            using (var large = Resources.ProteoWizard)
                Assert.IsNull(DpiUtil.GetIconName(large));
            // A ToolStripItem makes its ImageTransparentColor transparent in the bitmap it is
            // given, which is how the designer's keyed natives arrive at PrepareIcons
            foreach (var key in new[] { Color.Magenta, Color.Fuchsia, Color.White, Color.Transparent })
            {
                using (var button = new ToolStripButton())
                {
                    button.ImageTransparentColor = key;
                    button.Image = Resources.Save;
                    Assert.AreEqual(nameof(Resources.Save), DpiUtil.GetIconName(button.Image, key), key.Name);
                }
            }

            var checkedIcons = 0;
            var resourceSet = Resources.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, true, true);
            foreach (DictionaryEntry entry in resourceSet)
            {
                if (!(entry.Value is Bitmap bitmap))
                    continue;
                using (bitmap)
                {
                    if (bitmap.Width > ICON_NATIVE_MAX || bitmap.Height > ICON_NATIVE_MAX)
                        continue;
                    var name = (string) entry.Key;
                    var found = DpiUtil.GetIconName(bitmap);
                    if (PIXEL_TWINS.TryGetValue(name, out var twin))
                        Assert.AreEqual(twin, found, $@"{name} should resolve to its pixel twin {twin}");
                    else
                        Assert.AreEqual(name, found, $@"{name} resolves to {found ?? @"null"}: identical pixels under two names?");
                    checkedIcons++;
                }
            }
            Assert.IsTrue(checkedIcons > 100, $@"Expected the small icons of Resources.resx, found {checkedIcons}");
        }
    }
}
