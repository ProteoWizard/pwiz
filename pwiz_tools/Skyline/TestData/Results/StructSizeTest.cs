/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestData.Results
{
    [TestClass]
    public class StructSizeTest : AbstractUnitTest
    {
        /// <summary>
        /// Verifies that the offsets of fields within the struct ChromPeak have not changed
        /// over the versions.
        /// </summary>
        [TestMethod]
        public void TestChromPeakOffsets()
        {
            Assert.AreEqual((IntPtr) 0, Marshal.OffsetOf<ChromPeak>("_retentionTime"));
            Assert.AreEqual((IntPtr) 4, Marshal.OffsetOf<ChromPeak>("_startTime"));
            Assert.AreEqual((IntPtr) 8, Marshal.OffsetOf<ChromPeak>("_endTime"));
            Assert.AreEqual((IntPtr)12, Marshal.OffsetOf<ChromPeak>("_area"));
            Assert.AreEqual((IntPtr)16, Marshal.OffsetOf<ChromPeak>("_backgroundArea"));
            Assert.AreEqual((IntPtr)20, Marshal.OffsetOf<ChromPeak>("_height"));
            Assert.AreEqual((IntPtr)24, Marshal.OffsetOf<ChromPeak>("_fwhm"));
            Assert.AreEqual((IntPtr)28, Marshal.OffsetOf<ChromPeak>("_flagValues"));
            Assert.AreEqual((IntPtr)30, Marshal.OffsetOf<ChromPeak>("_massError"));
            Assert.AreEqual((IntPtr)32, Marshal.OffsetOf<ChromPeak>("_pointsAcross"));
            Assert.AreEqual(32, ChromPeak.GetStructSize(CacheFormatVersion.Eleven));
            Assert.AreEqual(36, ChromPeak.GetStructSize(CacheFormatVersion.Twelve));
            Assert.AreEqual(Marshal.SizeOf<ChromPeak>(), ChromPeak.GetStructSize(CacheFormatVersion.CURRENT));
        }
    }
}
