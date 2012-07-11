using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ZedGraph;
using pwiz.Common.Graph;

namespace pwiz.Topograph.Test.Graph
{
    /// <summary>
    /// Summary description for GraphDataTest
    /// </summary>
    [TestClass]
    public class GraphDataTest
    {
        [TestMethod]
        public void TestBarItem()
        {
            var zedGraphControl = new ZedGraphControl();
            var points = new PointPairList(new double[] {3, 4, 5}, new double[] {103, 104, 105});
            var barItem = zedGraphControl.GraphPane.AddBar("Bar", points, Color.Black);
            zedGraphControl.GraphPane.Title.Text = "Graph Pane Title";
            zedGraphControl.GraphPane.AxisChange();
            VerifyLines(
                new[]
                    {
                        "Graph Pane Title",
                        "Bar",
                        "X Axis\tY Axis",
                        "3\t103",
                        "4\t104",
                        "5\t105"
                    },
                GraphData.GetGraphData(zedGraphControl.MasterPane).ToString());
            zedGraphControl.GraphPane.BarSettings.Base = BarBase.Y;

            VerifyLines(
                new []
                    {
                        "Graph Pane Title",
                        "Bar",
                        "Y Axis\tX Axis",
                        "103\t3", 
                        "104\t4", 
                        "105\t5"
                    }, 
                GraphData.GetGraphData(zedGraphControl.MasterPane).ToString());
            zedGraphControl.GraphPane.YAxis.Type = AxisType.Text; 
            zedGraphControl.GraphPane.YAxis.Scale.TextLabels = new[] {"Three", "Four", "Five"};
            zedGraphControl.GraphPane.AxisChange();
            VerifyLines(
                new[]
                    {
                        "Graph Pane Title",
                        "Bar",
                        "Y Axis\tX Axis",
                        "Three\t3",
                        "Four\t4",
                        "Five\t5",
                    }, 
                GraphData.GetGraphData(zedGraphControl.MasterPane).ToString());
        }
        
        public static void VerifyLines(IList<string> expectedLines, string text)
        {
            var textReader = new StringReader(text);
            for (int iLine = 0; iLine < expectedLines.Count; iLine++)
            {
                var actualLine = textReader.ReadLine();
                Assert.AreEqual(expectedLines[iLine], actualLine, "Mismatch on line {0}", iLine);
            }
            var endText = textReader.ReadToEnd();
            Assert.AreEqual("", endText);
        }
    }
}
