/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Globalization;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public class PeptideRegressionTipProvider : ITipProvider
    {
        public PeptideRegressionTipProvider(PeptideDocNode docNode, string xLabel, string yLabel, PointD point)
        {
            DocNode = docNode;
            XLabel = xLabel;
            YLabel = yLabel;
            Point = point;
        }

        public PeptideDocNode DocNode { get; private set; }
        public string XLabel { get; private set; }
        public string YLabel { get; private set; }
        public PointD Point { get; private set; }

        public bool HasTip
        {
            get { return true; }
        }

        public Size RenderTip(Graphics g, Size sizeMax, bool draw)
        {
            var table = new TableDesc();
            using (var rt = new RenderTools())
            {
                table.AddDetailRow(Resources.PeptideRegressionTipProvider_RenderTip_Peptide, DocNode.ModifiedSequence, rt);

                table.AddDetailRow(XLabel, Point.X.ToString(CultureInfo.CurrentCulture), rt);
                table.AddDetailRow(YLabel, Point.Y.ToString(CultureInfo.CurrentCulture), rt);

                var size = table.CalcDimensions(g);

                if (draw)
                    table.Draw(g);

                return new Size((int)size.Width + 2, (int)size.Height + 2);
            }
        }
    }
}
