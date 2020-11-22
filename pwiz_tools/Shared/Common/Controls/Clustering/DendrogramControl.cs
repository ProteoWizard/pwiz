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
        private ImmutableList<KeyValuePair<DendrogramData, ImmutableList<double>>> _dendrogramDatas =
            ImmutableList<KeyValuePair<DendrogramData, ImmutableList<double>>>.EMPTY;

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

        public void SetDendrogramDatas(IEnumerable<KeyValuePair<DendrogramData, ImmutableList<double>>> datas)
        {
            _dendrogramDatas = ImmutableList.ValueOfOrEmpty(datas);
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
                var availableSpace = IsTreeVertical ? Width : Height;
                var leafLocations = Enumerable.Range(0, 3).Select(i => (i + 1.0) * availableSpace / 4).ToArray();
                var dendrogramData = new DendrogramData(new int[,]{{0,1},{2,3}}, Enumerable.Repeat(1.0, 2).ToArray());
                DrawDendrogram(e.Graphics, dendrogramData, leafLocations);
                return;
            }

            foreach (var data in _dendrogramDatas)
            {
                DrawDendrogram(e.Graphics, data.Key, data.Value);
            }
        }

        private void DrawDendrogram(Graphics graphics, DendrogramData dendrogramData, IList<double> locations)
        {
            var lines = dendrogramData.GetLines(locations, RectilinearLines).ToList();
            var pen = new Pen(Color.Black, 1);
            var maxHeight = lines.Max(line => line.Item4);
            if (maxHeight == 0)
            {
                // TODO: Draw a degenerate tree where all nodes connect at the same point
                return;
            }
            foreach (var line in lines)
            {
                var start = CoordinatesToPoint(line.Item1, line.Item2 / maxHeight);
                var end = CoordinatesToPoint(line.Item3, line.Item4 / maxHeight);
                DrawLine(graphics, pen, start, end);
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
    }
}
