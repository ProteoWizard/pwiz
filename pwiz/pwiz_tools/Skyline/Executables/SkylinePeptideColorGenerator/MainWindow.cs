using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace ColorGenerator
{
    public partial class MainWindow : Form
    {
        private const int Rows = 16;
        private const int Cols = 16;
        private const int TileSize = 30;

        public MainWindow()
        {
            InitializeComponent();

            dataGridView1.RowCount = Rows;
            dataGridView1.ColumnCount = Cols;

            for (int i = 0; i < dataGridView1.ColumnCount; i++)
                dataGridView1.Columns[i].Width = TileSize;
            for (int i = 0; i < dataGridView1.RowCount; i++)
                dataGridView1.Rows[i].Height = TileSize;
        }

        private void Form1_Shown(object sender, EventArgs e)
        {
            dataGridView1.ClearSelection();
            
            // Fill grid with colors.
            var colorGenerator = new ColorGenerator();
            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Cols; col++)
                {
                    var color = colorGenerator.GetColor();

                    // Write colors to trace output.  I copy these to the color table in Skyline's
                    // ColorGenerator class.
                    Trace.WriteLine(string.Format("Color.FromArgb({0},{1},{2}),", color.R, color.G, color.B));
                    dataGridView1.Rows[row].Cells[col].Style.BackColor = color;
                }
            }
        }
    }

    public class ColorGenerator
    {
        private const double MinL = 30; // Lower values for darker colors.
        private const double MaxL = 75; // Don't push this too far or colors are generated outside RGB space.
        private const double MinU = -100;
        private const double MaxU = 100;
        private const double MinV = -100;
        private const double MaxV = 100;
        private const int Trials = 100;

        private readonly Random _random = new Random(397);
        private readonly List<LuvColor.Luv> _colors = new List<LuvColor.Luv>(); 

        public Color GetColor()
        {
            LuvColor.Luv bestLuv = null;
            double bestDistance = 0;
            for (int i = 0; i < Trials; i++)
            {
                // Generate a new color in LUV space (good for human color perception).
                var luv = new LuvColor.Luv
                {
                    L = GetRandom(MinL, MaxL),
                    U = GetRandom(MinU, MaxU),
                    V = GetRandom(MinV, MaxV)
                };

                if (bestLuv == null)
                {
                    bestLuv = luv;
                }
                else
                {
                    // Calculate distance in LUV space from all previous colors.
                    double minDistance = double.MaxValue;
                    foreach (var color in _colors)
                    {
                        double distanceL = color.L - luv.L;
                        double distanceU = color.U - luv.U;
                        double distanceV = color.V - luv.V;
                        double distance = distanceL*distanceL + distanceU*distanceU + distanceV*distanceV;
                        minDistance = Math.Min(distance, minDistance);
                    }

                    // If this color's closest neighbor is more distant than previous colors,
                    // this becomes our new best color candidate.
                    if (bestDistance < minDistance)
                    {
                        bestDistance = minDistance;
                        bestLuv = luv;
                    }
                }
            }

            _colors.Add(bestLuv);

            // Convert to RGB.
            var rgb = LuvColor.LuvConverter.ToColor(bestLuv);
            return Color.FromArgb((byte)rgb.R, (byte)rgb.G, (byte)rgb.B);
        }

        private double GetRandom(double min, double max)
        {
            return min + _random.NextDouble()*(max - min);
        }
    }
}
