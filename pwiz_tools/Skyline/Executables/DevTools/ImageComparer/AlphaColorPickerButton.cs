/*
 * Original authors: Brendan MacLean <brendanx .at. uw.edu>
 *                   MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace ImageComparer
{
    public class AlphaColorPickerButton : ToolStripDropDownButton
    {
        private Color _selectedColor;
        private int _alpha;
        private Timer _colorChangeTimer;
        private bool _pendingColorChange;
        private TrackBar _alphaTrackBar;
        private Label _alphaLabelValue;

        public event EventHandler ColorChanged;

        public Color SelectedColor
        {
            get => Color.FromArgb(_alpha, _selectedColor);
            set
            {
                _selectedColor = Color.FromArgb(value.R, value.G, value.B);
                _alpha = value.A;
                UpdateControls();
                TriggerColorChange();
            }
        }

        public AlphaColorPickerButton()
        {
            InitializeDropDown();
            InitializeTimer();
            UpdateButtonAppearance();
        }

        private void InitializeTimer()
        {
            _colorChangeTimer = new Timer { Interval = 300 }; // 300ms delay
            _colorChangeTimer.Tick += (s, e) =>
            {
                _colorChangeTimer.Stop();
                if (_pendingColorChange)
                {
                    _pendingColorChange = false;
                    OnColorChanged(EventArgs.Empty);
                }
            };
        }

        private void InitializeDropDown()
        {
            if (DesignMode)
                return;

            var predefinedColors = new[]
            {
                Color.Red, Color.Green, Color.Blue, Color.Yellow,
                Color.Orange, Color.Purple, Color.Cyan, Color.Magenta
            };

            var panel = new Panel { Width = 8*30, Height = 120, BackColor = SystemColors.Control };

            var colorPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 24,
                FlowDirection = FlowDirection.LeftToRight
            };

            foreach (var color in predefinedColors)
            {
                var colorButton = new Button
                {
                    BackColor = color,
                    Width = 20,
                    Height = 20,
                    Margin = new Padding(5, 2, 5, 2),
                    FlatStyle = FlatStyle.Flat
                };
                colorButton.Click += (s, e) =>
                {
                    _selectedColor = color;
                    UpdateButtonAppearance();
                    OnColorChanged(EventArgs.Empty);
                };
                colorPanel.Controls.Add(colorButton);
            }

            var alphaLabelText = new Label
            {
                Text = @"Alpha:",
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleRight,
                Width = panel.Width/2,
            };
            _alphaLabelValue = new Label
            {
                Text = _alpha.ToString(),
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleRight,
                Width = 25,
                Margin = new Padding(0),
            };
            var alphaContainer = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                FlowDirection = FlowDirection.LeftToRight,
                Margin = new Padding(0)
            };
            alphaContainer.Controls.Add(alphaLabelText);
            alphaContainer.Controls.Add(_alphaLabelValue);

            _alphaTrackBar = new TrackBar
            {
                Minimum = 0,
                Maximum = 255,
                Value = _alpha,
                Dock = DockStyle.Bottom
            };
            _alphaTrackBar.Scroll += (s, e) =>
            {
                _alpha = _alphaTrackBar.Value;
                _alphaLabelValue.Text = _alpha.ToString();
                UpdateButtonAppearance();
                TriggerColorChange();
            };

            var moreColorsButton = new Button
            {
                Text = @"More Colors...",
                Dock = DockStyle.Bottom
            };
            moreColorsButton.Click += (s, e) =>
            {
                using var dialog = new ColorDialog();
                dialog.Color = _selectedColor;
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    _selectedColor = dialog.Color;
                    UpdateButtonAppearance();
                    OnColorChanged(EventArgs.Empty);
                }
            };

            panel.Controls.Add(colorPanel);
            panel.Controls.Add(alphaContainer);
            panel.Controls.Add(_alphaTrackBar);
            panel.Controls.Add(moreColorsButton);

            var host = new ToolStripControlHost(panel)
            {
                AutoSize = false,
                Size = panel.Size
            };

            DropDownItems.Add(host);
        }

        private void UpdateControls()
        {
            _alphaTrackBar.Value = _alpha;
            _alphaLabelValue.Text = _alpha.ToString();
            UpdateButtonAppearance();
        }

        private void UpdateButtonAppearance()
        {
            if (!DesignMode)
            {
                Image = CreateColorSwatch(SelectedColor, new Size(16, 16));
                ToolTipText = $@"Selected Color: {SelectedColor}";
            }
        }

        private Image CreateColorSwatch(Color color, Size size)
        {
            var bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bitmap);
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, 0, 0, size.Width, size.Height);
            g.DrawRectangle(Pens.Black, 0, 0, size.Width - 1, size.Height - 1);
            return bitmap;
        }

        private void TriggerColorChange()
        {
            _pendingColorChange = true;
            _colorChangeTimer.Stop();
            _colorChangeTimer.Start();
        }

        protected virtual void OnColorChanged(EventArgs e)
        {
            ColorChanged?.Invoke(this, e);
        }
    }
}
