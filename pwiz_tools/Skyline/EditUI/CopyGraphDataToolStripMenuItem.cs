/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using pwiz.MSGraph;
using pwiz.Skyline.Util;
using ZedGraph;

namespace pwiz.Skyline.EditUI
{
    /// <summary>
    /// Menu item to copy the data from a ZedGraph to the clipboard as tab separated values
    /// </summary>
    public class CopyGraphDataToolStripMenuItem : ToolStripMenuItem
    {
        public CopyGraphDataToolStripMenuItem(ZedGraphControl zedGraphControl)
        {
            ZedGraphControl = zedGraphControl;
            Text = "Copy Data";
            Click += CopyGraphDataToolStripMenuItem_Click;
        }
        
        public ZedGraphControl ZedGraphControl { get; private set; }

        void CopyGraphDataToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CopyGraphData(ZedGraphControl);
        }

        /// <summary>
        /// Copy the data from the curves in the ZedGraphControl to the clipboard. 
        /// </summary>
        public static void CopyGraphData(ZedGraphControl zedGraphControl)
        {
            var list = new List<string>();
            foreach (var graphPane in zedGraphControl.MasterPane.PaneList)
            {
                var tsvData = GetGraphDataTsv(graphPane);
                if (tsvData != null)
                {
                    list.Add(tsvData);
                }
            }
            if (list.Count == 0)
            {
                return;
            }
            var allText = string.Join("\r\n", list.ToArray());
            try
            {
                Clipboard.Clear();
                Clipboard.SetText(allText);
            }
            catch (ExternalException)
            {
                MessageBox.Show(ClipboardHelper.GetOpenClipboardMessage("Failed setting data to clipboard."), Program.Name);
            }
        }

        private static string GetGraphDataTsv(GraphPane graphPane)
        {
            var curves = new List<CurveItem>();
            foreach (var curve in graphPane.CurveList)
            {
                if (curve.IsX2Axis)
                {
                    continue;
                }
                curves.Add(curve);
            }
            if (curves.Count == 0)
            {
                return null;
            }
            double xMin = graphPane.XAxis.Scale.Min;
            double xMax = graphPane.XAxis.Scale.Max;
            // Dictionary from X value to array of Y values.
            // Since there may be multiple Y values for a given X value, keep track of a list of Y value arrays.
            // The Key of the dictionary is either the X value, or, for ordinal axes, the integer index of the point.
            // The Key in the dictionary's Value is either the X value itself, or the ordinal, or the text label.
            var rows = new Dictionary<object, KeyValuePair<object, IList<double?[]>>>();
            for (int iCurve = 0; iCurve < curves.Count; iCurve++)
            {
                var curve = curves[iCurve];
                IPointList pointList = curve.Points;
                if (pointList is MSPointList)
                {
                    pointList = ((MSPointList)pointList).FullList;
                }
                for (int iPt = 0; iPt < pointList.Count; iPt++)
                {
                    object label = null;
                    object key;
                    if (graphPane.XAxis.Scale.IsOrdinal || graphPane.XAxis.Scale.IsText)
                    {
                        key = iPt;
                    }
                    else
                    {
                        if (pointList[iPt].X < xMin || pointList[iPt].X > xMax)
                        {
                            continue;
                        }
                        label = key = pointList[iPt].X;
                    }
                    if (graphPane.XAxis.Scale.IsText)
                    {
                        if (iPt < graphPane.XAxis.Scale.TextLabels.Count())
                        {
                            label = graphPane.XAxis.Scale.TextLabels[iPt];
                        }
                    }
                    KeyValuePair<object, IList<double?[]>> valueEntry;
                    if (!rows.TryGetValue(key, out valueEntry))
                    {
                        valueEntry = new KeyValuePair<object, IList<double?[]>>(label, new List<double?[]>());
                        rows.Add(key, valueEntry);
                    }
                    bool added = false;
                    // Find the first array that has a null value for this curve
                    for (int iValue = 0; iValue < valueEntry.Value.Count(); iValue++)
                    {
                        if (!valueEntry.Value[iValue][iCurve].HasValue)
                        {
                            valueEntry.Value[iValue][iCurve] = pointList[iPt].Y;
                            added = true;
                            break;
                        }
                    }
                    // Add another array to the list if we couldn't find a spot to put the value
                    if (!added)
                    {
                        var values = new double?[curves.Count];
                        values[iCurve] = pointList[iPt].Y;
                        valueEntry.Value.Add(values);
                    }
                }
            }
            return ToTsv(graphPane, curves, rows);
        }

        private static string ToTsv(GraphPane graphPane, IEnumerable<CurveItem> curves, IDictionary<object, KeyValuePair<object, IList<double?[]>>> dict)
        {
            var sb = new StringBuilder();
            sb.Append(graphPane.XAxis.Title.Text ?? "");
            foreach (var curveItem in curves)
            {
                sb.Append("\t");
                sb.Append(curveItem.Label.Text);
            }
            sb.Append("\r\n");
            var keys = dict.Keys.ToArray();
            Array.Sort(keys);
            foreach (var key in keys)
            {
                var valueEntry = dict[key];
                foreach (var values in valueEntry.Value)
                {
                    sb.Append(valueEntry.Key);
                    foreach (var value in values)
                    {
                        sb.Append("\t");
                        sb.Append(value);
                    }
                    sb.Append("\r\n");
                }
            }
            return sb.ToString();
        }
    }
}
