using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.Design;
using MathNet.Numerics.Properties;
using pwiz.Skyline.Model;
using Resources = pwiz.Skyline.Properties.Resources;

namespace pwiz.Skyline.Controls.Graphs
{
    partial class DetectionsToolbar : GraphSummaryToolbar
    {
        private Timer _timer;
        private static Bitmap _emptyBitmap = new Bitmap(1, 1);

        private Dictionary<ToolStripDropDown, ToolStripItem> _selectedItems =
            new Dictionary<ToolStripDropDown, ToolStripItem>(4);

        public override bool Visible => true;

        public DetectionsToolbar(GraphSummary graphSummary) : base(graphSummary)
        {
            InitializeComponent();
            _timer = new Timer { Interval = 100 };
            _timer.Tick += new EventHandler(Timer_OnTick);

            this.toolStrip1.RenderMode = ToolStripRenderMode.Professional;
            this.toolStrip1.Renderer = new MyCustomRenderer();
            UpdateUI(DetectionsPlotPane.DefaultSettings, DetectionsPlotPane.DefaultMaxRepCount);
        }

        private void SelectItem<T>(ToolStripDropDown dropDown, T value)
        {
            foreach (ToolStripItem item in dropDown.Items)
            {
                if (item.Tag != null && item.Tag.Equals(value))
                {
                    SelectItem(item);
                    return;
                }

            }
        }

        private void SelectItem(ToolStripItem item)
        {
            foreach (ToolStripItem i in item.Owner.Items)
                i.Image = _emptyBitmap;

            item.Image = Resources.tick_small;
            if (item.Owner is ToolStripDropDown menu)
            {
                menu.OwnerItem.Text = (string)menu.OwnerItem.Tag + @" " + item.Text;
                _selectedItems[menu] = item;
            }
            _timer.Stop();
            _timer.Start();
        }

        private void Timer_OnTick(object sender, EventArgs e)
        {
            _graphSummary.UpdateUIWithoutToolbar();
            _timer.Stop();
        }

        public DetectionsPlotPane.Settings GetSettings()
        {

            var targetType = DetectionsPlotPane.DefaultSettings.TargetType;
            var item = _selectedItems[toolStripDropDownButtonLevel.DropDown];
            if (item?.Tag != null)
                targetType = (DetectionsPlotPane.TargetType)item.Tag;

            float cutoffValue = DetectionsPlotPane.DefaultSettings.QValueCutoff;
            item = _selectedItems[toolStripDropDownButtonQCutoff.DropDown];
            if (item != null)
                float.TryParse(item.Text, out cutoffValue);


            item = _selectedItems[toolStripDropDownButtonMultiple.DropDown];
            DetectionsPlotPane.YScaleFactorType yScale = DetectionsPlotPane.DefaultSettings.YScaleFactor;
            if (item?.Tag != null)
                yScale = (DetectionsPlotPane.YScaleFactorType)item.Tag;

            int repCutoff = DetectionsPlotPane.DefaultSettings.RepCount;
            item = _selectedItems[toolStripDropDownButtonRepCount.DropDown];
            if (item != null)
                int.TryParse(item.Text, out repCutoff);

            return new DetectionsPlotPane.Settings(cutoffValue, targetType, yScale, repCutoff);
        }

        private void DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            SelectItem(e.ClickedItem);
        }

        private ToolStripItem getSelectedItem(ToolStripDropDown dropDown)
        {
            foreach (ToolStripItem item in dropDown.Items)
                if (item.Image.Equals(Resources.tick_small))
                    return item;
            return null;
        }

        private void toolStripDropDownButtonRepCount_ValueUpdated(int newValue)
        {
            toolStripDropDownButtonRepCount.Text = "Replicate Count: " + newValue.ToString();

        }

        public override void OnDocumentChanged(SrmDocument oldDocument, SrmDocument newDocument)
        {
        }

        public override void UpdateUI()
        {
            var plots = _graphSummary.GraphPanes.OfType<DetectionsPlotPane>().ToList();
            if (plots.Count > 0)
                UpdateUI(plots[0].settings, plots[0].MaxRepCount);
            else
            {
                UpdateUI(DetectionsPlotPane.DefaultSettings, DetectionsPlotPane.DefaultMaxRepCount);
            }
        }

        public void UpdateUI(DetectionsPlotPane.Settings settings, int maxRepCount)
        {
            SelectItem(toolStripDropDownButtonLevel.DropDown, settings.TargetType);
            SelectItem(toolStripDropDownButtonMultiple.DropDown, settings.YScaleFactor);
            switch (settings.QValueCutoff)
            {
                case 0.01f:
                    SelectItem(toolStripMenuItem01);
                    break;
                case 0.05f:
                    SelectItem(toolStripMenuItem05);
                    break;
                default:
                    toolStripMenuItemCustom.Text = settings.QValueCutoff.ToString();
                    SelectItem(toolStripMenuItemCustom);
                    break;
            }

            toolStripMenuItemRepCount.Maximum = maxRepCount;
            int defaultRepCount = (int)Math.Round(toolStripMenuItemRepCount.Maximum / 2.0, 0);
            toolStripMenuItemRepCountDefault.Text = defaultRepCount.ToString();
            toolStripMenuItemRepCountDefault.Tag = defaultRepCount;
            toolStripMenuItemRepCount.Value = settings.RepCount;
            if (settings.RepCount == defaultRepCount)
                SelectItem(toolStripMenuItemRepCountDefault);
            else
                SelectItem(toolStripMenuItemRepCount);
        }

        private class MyCustomRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
                base.OnRenderImageMargin(e);
                e.ToolStrip.Items.OfType<ToolStripControlHost>()
                    .ToList().ForEach(item =>
                    {
                        if (item.Image != null)
                        {
                            var size = item.GetCurrentParent().ImageScalingSize;
                            var location = item.Bounds.Location;
                            location = new Point(5, location.Y + 4);
                            var imageRectangle = new Rectangle(location, size);
                            e.Graphics.DrawImage(item.Image, imageRectangle,
                                new Rectangle(Point.Empty, item.Image.Size),
                                GraphicsUnit.Pixel);
                        }
                    });
            }

        }
    }

    public class LabeledControlMenuItem<T> : ToolStripControlHost where T : Control, new()
    {
        protected Label _label;
        protected T _data;
        protected Button _caret;
        protected FlowLayoutPanel _panel;
        protected bool _mouseIn = false;

        public event ToolStripItemClickedEventHandler ValueChanged;
        private Color _menuHighLightColor = Color.FromArgb(8, SystemColors.MenuHighlight);

        public override string Text
        {
            get { return _data.Text; }
            set { _data.Text = value; }
        }

        public LabeledControlMenuItem() : base(new FlowLayoutPanel())
        {
            this._panel = (FlowLayoutPanel)this.Control;
            _label = new Label();
            _panel.Controls.Add(_label);
            _data = new T();
            _panel.Controls.Add(_data);
            _caret = new Button();
            _panel.Controls.Add(_caret);
            _panel.BackColor = Color.Transparent;
            _panel.Padding = new Padding(5, 2, 2, 2);

            EventHandler mouseEnterHandler = (sender, args) =>
            {
                if (!_mouseIn)
                {
                    _mouseIn = true;
                    _panel.BackColor = _menuHighLightColor;
                    this.Invalidate();
                }
            };
            EventHandler mouseLeaveHandler = (sender, args) =>
            {
                if (!_panel.ClientRectangle.Contains(_panel.PointToClient(Cursor.Position)))
                {
                    _mouseIn = false;
                    _panel.BackColor = Color.Transparent;
                    this.Owner.Invalidate();
                }
            };
            _panel.Paint += panel_Paint;
            _panel.MouseEnter += mouseEnterHandler;
            _panel.MouseLeave += mouseLeaveHandler;
            foreach (Control c in _panel.Controls)
            {
                c.MouseEnter += mouseEnterHandler;
                c.MouseLeave += mouseLeaveHandler;
            }

            _caret.Image = Resources.Caret_icon_small;
            _caret.Width = 20;
            _caret.BackColor = SystemColors.ControlLightLight;
            _label.Text = "Custom:";
            _label.AutoSize = true;
            _label.BackColor = Color.Transparent;
            _label.TextAlign = ContentAlignment.MiddleLeft;
            _label.Margin = new Padding(1, 6, 3, 0);
            _data.Text = @"0.01";
            using (var g = _data.CreateGraphics())
            {
                var textSize = g.MeasureString(_data.Text, _data.Font);
                _data.Width = (int)Math.Round(textSize.Width * 1.5);
            }

            _data.KeyPress += text_KeyPressed;
            _caret.Click += (obj, args) => Select();
            _panel.Click += (obj, args) => Select();
            _label.Click += (obj, args) => Select();
            _panel.AutoSize = true;
            this.AutoSize = true;
        }

        protected virtual void Select()
        {
            base.Select();
            var evt = new ToolStripItemClickedEventArgs(this);
            ValueChanged?.Invoke(this, evt);
            _mouseIn = false;
            this.Owner.Hide();
        }

        private void text_KeyPressed(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar.Equals((char)13))
                this.Select();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (_mouseIn)
            {
                Rectangle bounds = new Rectangle(2, Bounds.Top, Owner.Width - 3, Bounds.Height);
                using (Brush backBrush = new SolidBrush(_menuHighLightColor))
                {
                    var graph = this.Owner.CreateGraphics();
                    graph.FillRectangle(backBrush, bounds);
                    graph.Dispose();
                }
            }
        }

        private void panel_Paint(object sender, PaintEventArgs e)
        {
            using (Brush backBrush = new SolidBrush(_menuHighLightColor))
            {
                e.Graphics.FillRectangle(backBrush, _panel.ClientRectangle);
            }
            if (_mouseIn)
                ControlPaint.DrawBorder(e.Graphics, _panel.ClientRectangle, Color.Blue, ButtonBorderStyle.Solid);
            else
                ControlPaint.DrawBorder(e.Graphics, _panel.ClientRectangle, Color.Blue, ButtonBorderStyle.None);
        }

    }


    [ToolStripItemDesignerAvailability(ToolStripItemDesignerAvailability.MenuStrip |
                                       ToolStripItemDesignerAvailability.ContextMenuStrip |
                                       ToolStripItemDesignerAvailability.ToolStrip)]
    public class LabeledTextMenuItem : LabeledControlMenuItem<TextBox>
    {
        protected override void Select()
        {
            float val = 0;
            if (float.TryParse(_data.Text, out val)) //Validate the numeric value
                base.Select();
        }
    }

    public class LabeledTrackBarMenuItem : LabeledControlMenuItem<TrackBar>
    {

        private ToolTip _toolTip;

        private void trackBar_ValueUpdated(object sender, EventArgs e)
        {
            _toolTip.SetToolTip(_data, _data.Value.ToString());
        }

        public int Value
        {
            get => _data.Value;
            set => _data.Value = value;
        }

        public override string Text => _data.Value.ToString();

        public int Maximum
        {
            get => _data.Maximum;
            set => _data.Maximum = value;
        }

        public LabeledTrackBarMenuItem()
        {
            _data.Width = 150;
            _data.Height = 20;
            _data.TickStyle = TickStyle.None;
            _data.Minimum = 0;
            _data.ValueChanged += new EventHandler(trackBar_ValueUpdated);
            _toolTip = new ToolTip();
            _toolTip.SetToolTip(_data, _data.Value.ToString());
            _data.MouseDown += (sender, evt) => { _toolTip.ShowAlways = true; };
            _data.MouseUp += (sender, evt) => { _toolTip.ShowAlways = false; };
        }
    }
}
