/*
 * Original author: Henry Sanford <henrytsanford .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.GroupComparison;
using ZedGraph;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;

namespace pwiz.Skyline.Controls.Graphs
{
    public abstract class DotPlotUtil
    {
        private const int LABEL_BACKGROUND_OPACITY = 150; // 0 is transparent, 250 is opaque
        private const double LABEL_POINT_DISTANCE = 0.001; // Fraction of the Y Axis to separate the point and label by
        public static FontSpec CreateFontSpec(Color color, float size, bool label = false)
        {
            if (label)
            {
                return new FontSpec(@"Arial", size, color, false, false, false, 
                    Color.FromArgb(LABEL_BACKGROUND_OPACITY, Color.White) , new SolidBrush(Color.FromArgb(LABEL_BACKGROUND_OPACITY, Color.White)), FillType.Solid)
                {
                    Border = { IsVisible = false }
                };
            }
            return new FontSpec(@"Arial", size, color, false, false, false, 
                Color.Empty, null, FillType.None)
            {
                Border = { IsVisible = false }
            };
        }
        public static SymbolType PointSymbolToSymbolType(PointSymbol symbol)
        {
            switch (symbol)
            {
                case PointSymbol.Circle:
                    return SymbolType.Circle;
                case PointSymbol.Square:
                    return SymbolType.Square;
                case PointSymbol.Triangle:
                    return SymbolType.Triangle;
                case PointSymbol.TriangleDown:
                    return SymbolType.TriangleDown;
                case PointSymbol.Diamond:
                    return SymbolType.Diamond;
                case PointSymbol.XCross:
                    return SymbolType.XCross;
                case PointSymbol.Plus:
                    return SymbolType.Plus;
                case PointSymbol.Star:
                    return SymbolType.Star;
                default:
                    return SymbolType.Circle;
            }
        }

        public static bool HasOutline(PointSymbol pointSymbol)
        {
            return pointSymbol == PointSymbol.Circle || pointSymbol == PointSymbol.Square ||
                   pointSymbol == PointSymbol.Triangle || pointSymbol == PointSymbol.TriangleDown ||
                   pointSymbol == PointSymbol.Diamond;
        }

        public static float PointSizeToFloat(PointSize pointSize)
        {
            //return 12.0f + 2.0f * ((int) pointSize - 2);
            return ((GraphFontSize[])GraphFontSize.FontSizes)[(int)pointSize].PointSize;
        }

        public static TextObj CreateLabel(PointPair point, Protein protein, Peptide peptide, Color color, float size, Scale scale)
        {
            var text = MatchExpression.GetRowDisplayText(protein, peptide);
            float adjustedY;
            if (scale.Type == AxisType.Log)
            {
                var scaleRange = Math.Log10(scale.Max) - Math.Log10(scale.Min);
                var yLinear = Math.Log10(point.Y);
                var exponent = scaleRange * LABEL_POINT_DISTANCE + yLinear;
                adjustedY = (float)Math.Pow(10, exponent);
            }
            else
            {
                var scaleRange = scale.Max - scale.Min;
                adjustedY = (float)(point.Y + scaleRange * LABEL_POINT_DISTANCE);
            }
            var textObj = new TextObj(text, point.X, adjustedY, CoordType.AxisXYScale, AlignH.Center, AlignV.Bottom)
            {
                IsClippedToChartRect = true,
                FontSpec = CreateFontSpec(color, size, true),
                ZOrder = ZOrder.A_InFront
            };

            return textObj;
        }

        public class LabeledPoint
        {
            public LabeledPoint(PointPair point, TextObj label, bool isSelected)
            {
                Point = point;
                Label = label;
                IsSelected = isSelected;
            }

            public PointPair Point { get; private set; }
            public TextObj Label { get; private set; }

            public bool IsSelected { get; private set; }
        }

        public static bool IsPathSelected(IdentityPath selectedPath, IdentityPath identityPath)
        {
            return selectedPath != null && identityPath != null &&
                   selectedPath.Depth <= (int)SrmDocument.Level.Molecules && identityPath.Depth <= (int)SrmDocument.Level.Molecules &&
                   (selectedPath.Depth >= identityPath.Depth && Equals(selectedPath.GetPathTo(identityPath.Depth), identityPath) ||
                    selectedPath.Depth <= identityPath.Depth && Equals(identityPath.GetPathTo(selectedPath.Depth), selectedPath));
        }

        public static void Select(SkylineWindow window, IdentityPath identityPath)
        {
            var skylineWindow = window;
            if (skylineWindow == null)
                return;

            var alreadySelected = IsPathSelected(skylineWindow.SelectedPath, identityPath);
            if (alreadySelected)
                skylineWindow.SequenceTree.SelectedNode = null;

            skylineWindow.SelectedPath = identityPath;
            skylineWindow.UpdateGraphPanes();
        }

        public static void MultiSelect(SkylineWindow skylineWindow, IdentityPath identityPath)
        {
            if (skylineWindow == null)
            {
                return;
            }

            var list = skylineWindow.SequenceTree.SelectedPaths;
            if (GetSelectedPath(skylineWindow, identityPath) == null)
            {
                list.Insert(0, identityPath);
                skylineWindow.SequenceTree.SelectedPaths = list;
                if (!IsPathSelected(skylineWindow.SelectedPath, identityPath))
                    skylineWindow.SequenceTree.SelectPath(identityPath);
            }
            skylineWindow.UpdateGraphPanes();
        }

        public static IdentityPath GetSelectedPath(SkylineWindow skylineWindow, IdentityPath identityPath)
        {
            return skylineWindow != null ? skylineWindow.SequenceTree.SelectedPaths.FirstOrDefault(p => IsPathSelected(p, identityPath)) : null;
        }

        public static bool IsSelected(SkylineWindow skylineWindow, Peptide peptide, Protein protein)
        {
            var docNode = peptide ?? (SkylineDocNode)protein;
            return skylineWindow != null && GetSelectedPath(skylineWindow, docNode.IdentityPath) != null;
        }
    }
}
