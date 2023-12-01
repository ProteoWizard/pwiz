using System.Drawing;
using pwiz.Skyline.Model.GroupComparison;
using ZedGraph;

namespace pwiz.Skyline.Controls.Graphs
{
    public static class DotPlotUtil
    {
        private const int LABEL_BACKGROUND_OPACITY = 150; // 0 is transparent, 250 is opaque
        public static FontSpec CreateFontSpec(Color color, float size, bool label = false)
        {
            if (label)
            {
                return new FontSpec(@"Arial", size, color, false, false, false, Color.FromArgb(LABEL_BACKGROUND_OPACITY, Color.White) , new SolidBrush(Color.FromArgb(LABEL_BACKGROUND_OPACITY, Color.White)), FillType.Solid)
                {
                    Border = { IsVisible = false }
                };
            }
            return new FontSpec(@"Arial", size, color, false, false, false, Color.Empty, null, FillType.None)
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
    }
}
