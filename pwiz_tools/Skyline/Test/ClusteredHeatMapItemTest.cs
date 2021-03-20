using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Clustering;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ClusteredHeatMapItemTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSimpleFillIntervals()
        {
            Bitmap bmp = new Bitmap(1, 3);
            Graphics graphics = Graphics.FromImage(bmp);
            ClusteredHeatMapItem.FillIntervals(graphics, 0, 1, new []
            {
                Tuple.Create(0.0, 1.5, Color.FromArgb(255, 255, 255)),
                Tuple.Create(1.5, 3.0, Color.FromArgb(0, 0, 0)),
            });
            var average = (int) Math.Sqrt(255.0 * 255 / 2);
            Assert.AreEqual(Color.FromArgb(255, 255, 255), bmp.GetPixel(0, 0));
            Assert.AreEqual(Color.FromArgb(average, average, average), bmp.GetPixel(0, 1));
            Assert.AreEqual(Color.FromArgb(0, 0, 0), bmp.GetPixel(0, 2));
        }
    }
}
