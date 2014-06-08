/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public partial class GraphDriftTime : DockableFormEx, IGraphContainer
    {
        public GraphDriftTime()
        {
            InitializeComponent();

            Load += CreateChart;
        }

        public void UpdateUI(bool selectionChanged = true)
        {
        }

        public void LockYAxis(bool lockY)
        {
        }

        // Call this method from the Form_Load method, passing your ZedGraphControl
        private void CreateChart(object sender, EventArgs eventArgs)
        {
            GraphPane myPane = graphControl.GraphPane;

            // Set the titles
            myPane.Title.Text = "Scatter Plot Demo"; // Not L10N
            myPane.XAxis.Title.Text = "Pressure, Atm"; // Not L10N
            myPane.YAxis.Title.Text = "Flow Rate, cc/hr"; // Not L10N

            // Get a random number generator
            Random rand = new Random();

            // Populate a PointPairList with a log-based function and some random variability
            PointPairList list = new PointPairList();
            for (int i = 0; i < 200; i++)
            {
                double x = rand.NextDouble() * 20.0 + 1;
                double y = Math.Log(10.0 * (x - 1.0) + 1.0) * (rand.NextDouble() * 0.2 + 0.9);
                list.Add(x, y, x);
            }
            list.Sort();

            // Add the curve
            LineItem myCurve = myPane.AddCurve("Performance", list, Color.Black, SymbolType.Circle); // Not L10N
            // Don't display the line (This makes a scatter plot)
            myCurve.Line.IsVisible = false;
            // Hide the symbol outline
            myCurve.Symbol.Border.IsVisible = false;
            myCurve.Symbol.Size = 25f;

            // Fill the symbol interior with color
            var brush = new LinearGradientBrush(new Point(0, 0), new Point(1, 0), Color.Black, Color.White)
            {
                InterpolationColors = new ColorBlend
                {
                    Colors = new[] {Color.FromArgb(0, 0, 140), Color.FromArgb(0, 190, 0), Color.Red},
                    Positions = new[] {0.0f, 0.5f, 1.0f}
                }
            };
            myCurve.Symbol.Fill = new Fill(brush)
            {
                Type = FillType.GradientByZ,
                RangeMin = 0,
                RangeMax = 22
            };

            // Fill the background of the chart rect and pane
            //myPane.Chart.Fill = new Fill(Color.White, Color.LightGoldenrodYellow, 45.0f);
            //myPane.Fill = new Fill(Color.White, Color.SlateGray, 45.0f);

            graphControl.AxisChange();
        }
    }
}
