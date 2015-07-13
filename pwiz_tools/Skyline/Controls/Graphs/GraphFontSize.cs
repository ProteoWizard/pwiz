/*
 * Original author: Alex MacLean <alex .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Controls.Graphs
{
    public class GraphFontSize
    {
        private Func<string> _getLabelFunc; 
        private GraphFontSize(float pointSize, Func<string> getLabelFunc)
        {
            PointSize = pointSize;
            _getLabelFunc = getLabelFunc;
        }
        public float PointSize { get; private set; }
        public override string ToString()
        {
            return _getLabelFunc();
        }

        public static readonly GraphFontSize XSMALL = new GraphFontSize(8, () => Resources.FontSize_XSMALL_x_small);
        public static readonly GraphFontSize SMALL = new GraphFontSize(10, () => Resources.FontSize_SMALL_small);
        public static readonly GraphFontSize NORMAL = new GraphFontSize(12, () => Resources.FontSize_NORMAL_normal);
        public static readonly GraphFontSize LARGE = new GraphFontSize(14, () => Resources.FontSize_LARGE_large);
        public static readonly GraphFontSize XLARGE = new GraphFontSize(16, () => Resources.FontSize_XLARGE_x_large);

        public static IEnumerable<GraphFontSize> FontSizes
        {
            get { return new[] {XSMALL, SMALL, NORMAL, LARGE, XLARGE}; }
        }

        public static void PopulateCombo(ComboBox comboBox, float currentSize)
        {
            comboBox.Items.Clear();
            foreach (var fontSize in FontSizes)
            {
                comboBox.Items.Add(fontSize);
                if (Equals(fontSize.PointSize, currentSize))
                {
                    comboBox.SelectedIndex = comboBox.Items.Count - 1;
                }
            }
        }

        public static GraphFontSize GetFontSize(ComboBox comboBox)
        {
            return comboBox.SelectedItem as GraphFontSize ?? NORMAL;
        }
    }
}
