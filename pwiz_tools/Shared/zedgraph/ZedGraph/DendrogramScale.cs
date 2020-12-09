//============================================================================
//ZedGraph Class Library - A Flexible Line Graph/Bar Graph Library in C#
//Copyright © 2005  John Champion
//
//This library is free software; you can redistribute it and/or
//modify it under the terms of the GNU Lesser General Public
//License as published by the Free Software Foundation; either
//version 2.1 of the License, or (at your option) any later version.
//
//This library is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
//Lesser General Public License for more details.
//
//You should have received a copy of the GNU Lesser General Public
//License along with this library; if not, write to the Free Software
//Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
//=============================================================================

using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Drawing;
using System.Runtime.Serialization;
using System.Security.Permissions;
using System.Linq;
using System.Windows.Forms;

namespace ZedGraph
{
    /// <summary>
    /// The Dendrogram class inherits from the <see cref="Scale" /> class, and implements
    /// the features specific to <see cref="AxisType.Dendrogram" />.
    /// </summary>
    /// <remarks>
    /// Dendrogram is the normal, default cartesian axis.
    /// </remarks>
    /// 
    /// <author> John Champion  </author>
    /// <version> $Revision: 1.10 $ $Date: 2007-04-16 00:03:02 $ </version>
    [Serializable]
    public class DendrogramScale : Scale, ISerializable //, ICloneable
    {
        private List<DendrogramFormat> formats;
        private bool _rectilinearLines = true;
        private DockStyle _dendrogramLocation = DockStyle.Top;
        private float Height = 50;
        private float Width = 300;

        #region constructors

        /// <summary>
        /// Default constructor that defines the owner <see cref="Axis" />
        /// (containing object) for this new object.
        /// </summary>
        /// <param name="owner">The owner, or containing object, of this instance</param>
        public DendrogramScale(Axis owner)
            : base(owner)
        {
        }

        /// <summary>
        /// The Copy Constructor
        /// </summary>
        /// <param name="rhs">The <see cref="DendrogramScale" /> object from which to copy</param>
        /// <param name="owner">The <see cref="Axis" /> object that will own the
        /// new instance of <see cref="DendrogramScale" /></param>
        public DendrogramScale(Scale rhs, Axis owner)
            : base(rhs, owner)
        {
        }


        /// <summary>
        /// Create a new clone of the current item, with a new owner assignment
        /// </summary>
        /// <param name="owner">The new <see cref="Axis" /> instance that will be
        /// the owner of the new Scale</param>
        /// <returns>A new <see cref="Scale" /> clone.</returns>
        public override Scale Clone(Axis owner)
        {
            return new DendrogramScale(this, owner);
        }
        #endregion

        #region properties

        /// <summary>
        /// Return the <see cref="AxisType" /> for this <see cref="Scale" />, which is
        /// <see cref="AxisType.Dendrogram" />.
        /// </summary>
        public override AxisType Type
        {
            get { return AxisType.Dendrogram; }
        }

        #endregion

        #region methods

        public override SizeF GetScaleMaxSpace(Graphics g, GraphPane pane, float scaleFactor,
            bool applyAngle)
        {
            return new SizeF(Width, 100);
        }

        public void SetDendrograms(List<DendrogramFormat> formats, DockStyle location)
        {
            this.formats = formats;
            _dendrogramLocation = location;
        }

        private PointF CoordinatesToPoint(double location, double yFraction, GraphPane pane, float xAxisHeight, float yAxisWidth)
        {
            switch (_dendrogramLocation)
            {
                case DockStyle.Top:
                    return new PointF(-(float)location + Width + yAxisWidth, (float)(Height * yFraction));
                default:
                case DockStyle.Bottom:
                    return new PointF(-(float)location + Width + yAxisWidth, (float)(Height - Height * yFraction));
                case DockStyle.Left:
                    return new PointF((float)(Width - Width * yFraction), (float)location);
                case DockStyle.Right:
                    return new PointF(-(float)location + pane.Rect.Height - xAxisHeight, (float)(Width * yFraction));
            }
        }

        public override void Draw(Graphics graphics, GraphPane pane, float scaleFactor, float shiftPos)
        {
            if (formats == null)
                return;
            var yAxisWidth = pane.YAxis.CalcSpace(graphics, pane, scaleFactor, out float fixedSpaceY);
            var xAxisHeight = pane.XAxis.CalcSpace(graphics, pane, scaleFactor, out float fixedSpaceX);
            if (_dendrogramLocation == DockStyle.Right)
            {
                Height = pane.CalcChartRect(graphics).Height;
                Width = 100;
            }
            else
            {
                Width = pane.CalcChartRect(graphics).Width;
                Height = 100;
            }
            foreach (var format in formats)
            {
            
                var locations = format.LeafLocations.Select(kvp => (kvp.Key + kvp.Value) / 2).ToList();
                var lines = format.Data.GetLines(locations, _rectilinearLines).ToList();
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
                        var start = CoordinatesToPoint(line.Item1, (offset + line.Item2) / denominator, pane, xAxisHeight, yAxisWidth);
                        var end = CoordinatesToPoint(line.Item3, (offset + line.Item4) / denominator, pane, xAxisHeight, yAxisWidth);
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
                            var topLeft = CoordinatesToPoint(left, top, pane, xAxisHeight, yAxisWidth);
                            var bottomRight = CoordinatesToPoint(right, bottom, pane, xAxisHeight, yAxisWidth);
                            var rectangle = new RectangleF(
                                Math.Min(topLeft.X, bottomRight.X),
                                Math.Min(topLeft.Y, bottomRight.Y),
                                Math.Abs(topLeft.X - bottomRight.X),
                                Math.Abs(topLeft.Y - bottomRight.Y));

                            graphics.FillRectangle(new SolidBrush(color), rectangle);
                        }
                    }
                }

                var x2AxisHeight = pane.X2Axis.CalcSpace(graphics, pane, scaleFactor, out float fixedSpaceX2);
                var y2AxisWidth = pane.Y2Axis.CalcSpace(graphics, pane, scaleFactor, out float fixedSpaceY2);

                if (_dendrogramLocation == DockStyle.Top)
                {
                    var leftRectangle = new RectangleF(
                        pane.Rect.Width - yAxisWidth - y2AxisWidth, 0,
                        yAxisWidth * (float)1.1,
                        x2AxisHeight);
                    graphics.FillRectangle(new SolidBrush(Color.White), leftRectangle);

                    var cornerRectangle = new RectangleF(
                        -y2AxisWidth, 0,
                        y2AxisWidth,
                        x2AxisHeight);
                    graphics.FillRectangle(new SolidBrush(Color.White), cornerRectangle);
                }


                if (_dendrogramLocation == DockStyle.Right)
                {
                    var cornerRectangle = new RectangleF(
                        pane.Rect.Height - xAxisHeight - x2AxisHeight, 0,
                        x2AxisHeight * (float)1.2,
                        y2AxisWidth);
                    graphics.FillRectangle(new SolidBrush(Color.White), cornerRectangle);

                    var bottomRectangle = new RectangleF(
                        -x2AxisHeight, 0,
                        x2AxisHeight,
                        y2AxisWidth);
                    graphics.FillRectangle(new SolidBrush(Color.White), bottomRectangle);
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
                graphics.DrawLine(pen, points[i - 1], points[i]);
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
            switch (_dendrogramLocation)
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


        


        #endregion

        #region Serialization
        /// <summary>
        /// Current schema value that defines the version of the serialized file
        /// </summary>
        public const int schema2 = 10;

        /// <summary>
        /// Constructor for deserializing objects
        /// </summary>
        /// <param name="info">A <see cref="SerializationInfo"/> instance that defines the serialized data
        /// </param>
        /// <param name="context">A <see cref="StreamingContext"/> instance that contains the serialized data
        /// </param>
        protected DendrogramScale(SerializationInfo info, StreamingContext context) : base(info, context)
        {
            // The schema value is just a file version parameter.  You can use it to make future versions
            // backwards compatible as new member variables are added to classes
            int sch = info.GetInt32("schema2");

        }
        /// <summary>
        /// Populates a <see cref="SerializationInfo"/> instance with the data needed to serialize the target object
        /// </summary>
        /// <param name="info">A <see cref="SerializationInfo"/> instance that defines the serialized data</param>
        /// <param name="context">A <see cref="StreamingContext"/> instance that contains the serialized data</param>
        [SecurityPermissionAttribute(SecurityAction.Demand, SerializationFormatter = true)]
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
            info.AddValue("schema2", schema2);
        }
        #endregion

    }
}
