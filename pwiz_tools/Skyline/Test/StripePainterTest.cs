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
using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Clustering;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class StripePainterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSimplePaintStripe()
        {
            Bitmap bmp = new Bitmap(1, 3);
            Graphics graphics = Graphics.FromImage(bmp);
            var stripePainter = new StripePainter(graphics, 0, 1);
            var color1 = Color.FromArgb(100, 150, 175);
            var color2 = Color.FromArgb(75, 100, 120);
            stripePainter.PaintStripe(0, 1.5, color1);
            stripePainter.PaintStripe(1.5, 3, color2);
            stripePainter.PaintLastStripe();
            var averageColor = Color.FromArgb((int) Math.Sqrt((color1.R * color1.R + color2.R * color2.R) / 2.0),
                (int) Math.Sqrt((color1.G * color1.G + color2.G * color2.G) / 2.0),
                (int) Math.Sqrt((color1.B * color1.B + color2.B * color2.B) / 2.0));
            Assert.AreEqual(color1, bmp.GetPixel(0, 0));
            Assert.AreEqual(averageColor, bmp.GetPixel(0, 1));
            Assert.AreEqual(color2, bmp.GetPixel(0, 2));
        }
    }
}
