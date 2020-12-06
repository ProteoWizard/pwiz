using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.Controls.Clustering
{
    public partial class DendrogramControl : UserControl
    {
        private ImmutableList<DendrogramFormat> _dendrogramFormats =
            ImmutableList<DendrogramFormat>.EMPTY;

        private DockStyle _dendrogramLocation;
        private bool _rectilinearLines;
        public DendrogramControl()
        {
            InitializeComponent();
        }

        public DockStyle DendrogramLocation {
            get
            {
                return _dendrogramLocation;
            }
            set
            {
                if (_dendrogramLocation == value)
                {
                    return;
                }
                _dendrogramLocation = value;
                Invalidate();
            }
        }

        public bool RectilinearLines
        {
            get { return _rectilinearLines; }
            set
            {
                if (value != _rectilinearLines)
                {
                    _rectilinearLines = value;
                    Invalidate();
                }
            }
        }

        public void SetDendrogramDatas(IEnumerable<DendrogramFormat> datas)
        {
            _dendrogramFormats = ImmutableList.ValueOfOrEmpty(datas);
            Invalidate();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (DesignMode)
            {
                double availableSpace = IsTreeVertical ? Width : Height;
                var leafLocations = Enumerable.Range(0, 3).Select(i =>
                    new KeyValuePair<double, double>(i * availableSpace / 4, (i + 1) * availableSpace / 4)).ToList();
                var dendrogramData = new DendrogramData(new int[,]{{0,1},{2,3}}, Enumerable.Repeat(1.0, 2).ToArray());
                var colors = new[] {Color.Red, Color.Green, Color.Blue}.Select(color => new[] {color});
                DrawDendrogram(e.Graphics, new DendrogramFormat(dendrogramData, leafLocations, colors));
                return;
            }

            foreach (var data in _dendrogramFormats)
            {
                DrawDendrogram(e.Graphics, data);
            }
        }

        private void DrawDendrogram(Graphics graphics, DendrogramFormat format)
        {
            var locations = format.LeafLocations.Select(kvp=>(kvp.Key + kvp.Value) / 2).ToList();
            var lines = format.Data.GetLines(locations, RectilinearLines).ToList();
            var pen = new Pen(Color.Black, 1);
            var maxHeight = lines.Max(line => line.Item4);
            if (maxHeight == 0)
            {
                // TODO: Draw a degenerate tree where all nodes connect at the same point
                return;
            }

            var colorFraction = GetFractionForColors(format);
            var treeFraction = 1 - colorFraction;
            if (treeFraction > 0)
            {
                var denominator = maxHeight / treeFraction;
                var offset = denominator - maxHeight;
                foreach (var line in lines)
                {
                    var start = CoordinatesToPoint(line.Item1, (offset + line.Item2) / denominator);
                    var end = CoordinatesToPoint(line.Item3, (offset + line.Item4) / denominator);
                    DrawLine(graphics, pen, start, end);
                }
            }

            if (colorFraction > 0)
            {
                for (int colorLevel = 0; colorLevel < format.ColorLevelCount; colorLevel++)
                {
                    for (int iLeaf = 0; iLeaf < format.Colors.Count; iLeaf++)
                    {
                        var color = format.Colors[iLeaf][colorLevel];
                        var left = format.LeafLocations[iLeaf].Key;
                        var right = format.LeafLocations[iLeaf].Value;

                        var bottom = colorLevel * colorFraction / format.ColorLevelCount;
                        var top = (colorLevel + 1) * colorFraction / format.ColorLevelCount;
                        var topLeft = CoordinatesToPoint(left, top);
                        var bottomRight = CoordinatesToPoint(right, bottom);

                        graphics.FillRectangle(new SolidBrush(color), new RectangleF(topLeft, new SizeF(bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y)));
                    }
                }
            }
        }

        private void DrawLine(Graphics graphics, Pen pen, PointF start, PointF end)
        {
            var points = new List<PointF>();
            points.Add(start);
            points.Add(end);
            for (int i = 1; i < points.Count; i++)
            {
                graphics.DrawLine(pen, points[i-1], points[i]);
            }
        }

        private PointF CoordinatesToPoint(double location, double yFraction)
        {
            switch (DendrogramLocation)
            {
                case DockStyle.Top:
                    return new PointF((float) location, (float)(Height - Height * yFraction));
                default:
                case DockStyle.Bottom:
                    return new PointF((float)location, (float)(Height * yFraction));
                case DockStyle.Left:
                    return new PointF((float)(Width - Width * yFraction), (float)location);
                case DockStyle.Right:
                    return new PointF((float)(Width * yFraction), (float)location);
            }
        }

        private double GetFractionForColors(DendrogramFormat dendrogramFormat)
        {
            if (dendrogramFormat.Colors == null || dendrogramFormat.Colors.Count == 0)
            {
                return 0;
            }

            var totalSpace = GetTotalSpace();
            var spaceForColors = Math.Max(totalSpace / 10, dendrogramFormat.ColorLevelCount * 5);
            if (spaceForColors >= totalSpace)
            {
                return 1;
            }

            return spaceForColors / totalSpace;
        }

        private double GetTotalSpace()
        {
            switch (DendrogramLocation)
            {
                case DockStyle.Top:
                case DockStyle.Bottom:
                default:
                    return Height;
                case DockStyle.Left:
                case DockStyle.Right:
                    return Width;
            }
        }

        public bool IsTreeVertical
        {
            get
            {
                switch (DendrogramLocation)
                {
                    case DockStyle.Left:
                    case DockStyle.Right:
                        return false;
                }

                return true;
            }
        }

        public class DendrogramFormat
        {
            public DendrogramFormat(DendrogramData data, IEnumerable<KeyValuePair<double, double>> leafLocations,
                IEnumerable<IEnumerable<Color>> colors)
            {
                Data = data;
                LeafLocations = ImmutableList.ValueOf(leafLocations);
                if (colors != null)
                {
                    Colors = ImmutableList.ValueOf(colors.Select(ImmutableList.ValueOf));
                    if (Colors.Count != LeafLocations.Count)
                    {
                        throw new ArgumentException(@"Wrong number of colors", nameof(colors));
                    }

                    if (Colors.Count > 0)
                    {
                        ColorLevelCount = Colors[0].Count;
                        if (Colors.Any(c => c.Count != ColorLevelCount))
                        {
                            throw new ArgumentException(@"Inconsistent number of colors", nameof(colors));
                        }
                    }
                }
                else
                {
                    Colors = ImmutableList<ImmutableList<Color>>.EMPTY;
                }
            }

            public DendrogramData Data { get; private set; }

            public ImmutableList<KeyValuePair<double, double>> LeafLocations { get; private set; }
            public ImmutableList<ImmutableList<Color>> Colors { get; private set; }
            public int ColorLevelCount { get; private set; }
        }
    }
}
