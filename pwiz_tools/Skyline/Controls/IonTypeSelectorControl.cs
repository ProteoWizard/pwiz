using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using ContentAlignment = System.Drawing.ContentAlignment;

namespace pwiz.Skyline.Controls
{
    public interface IControlSize
    {
        Size ExpectedSize { get; }
        Size ButtonSize { get; }
        void Update(GraphSpectrumSettings set, PeptideSettings peptideSet);
    }

    public class MenuControl<T> : ToolStripControlHost where T : Panel, IControlSize, new()
    {
        public MenuControl(GraphSpectrumSettings set, PeptideSettings peptideSet) : base(new T())
        {
            AutoSize = false;
            HostedControl.ResumeLayout();
            HostedControl.SuspendLayout();
            HostedControl.Update(set, peptideSet);
            HostedControl.ResumeLayout();
            var buttonSize = HostedControl.ButtonSize;
            HostedControl.Size = HostedControl.ExpectedSize + new Size(buttonSize.Width/4, buttonSize.Height/4);
        }

        public T HostedControl
        {
            get { return Control as T; }
        }

        public void Update(GraphSpectrumSettings set, PeptideSettings peptideSet)
        {
            HostedControl.SuspendLayout();
            HostedControl.Update(set, peptideSet);
            HostedControl.ResumeLayout();
            HostedControl.Size = HostedControl.ExpectedSize;
        }
    }

    public class ChargeSelectionPanel : FlowLayoutPanel, IControlSize
    {
        public event Action<bool> OnCharge1Changed;
        public event Action<bool> OnCharge2Changed;
        public event Action<bool> OnCharge3Changed;
        public event Action<bool> OnCharge4Changed;

        public ChargeSelectionPanel()
        {
            for (int i = 1; i <= 4; i++)
            {
                var cb = new CheckBox()
                {
                    Text = i.ToString(CultureInfo.CurrentCulture),
                    Appearance = Appearance.Button,
                    AutoSize = true,
                    Tag = i
                };
                cb.CheckedChanged += button_Click;
                this.Controls.Add(cb);
            }
        }

        private bool GetButtonState(int charge, GraphSpectrumSettings set)
        {
            switch (charge)
            {
                case 1:
                    return set.ShowCharge1;
                case 2:
                    return set.ShowCharge2;
                case 3:
                    return set.ShowCharge3;
                case 4:
                    return set.ShowCharge4;
                default:
                    return false;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!this.IsDisposed && disposing)
            {
                foreach (var button in Controls.OfType<CheckBox>())
                    button.CheckedChanged -= button_Click;
            }
        }

        public void Update(GraphSpectrumSettings set, PeptideSettings peptideSet)
        {
            foreach (var button in Controls.OfType<CheckBox>())
                button.Checked = GetButtonState((int)button.Tag, set);
        }

        public Size ExpectedSize
        {
            get
            {
                if (Controls.Count == 0)
                    return Size;
                var rightmostButton = Controls.OfType<CheckBox>().Last();
                
                return new Size((rightmostButton.Bounds.Right + rightmostButton.Margin.Right),
                    (rightmostButton.Bounds.Bottom + rightmostButton.Margin.Bottom));
            }
        }
        public Size ButtonSize
        {
            get { return Controls.OfType<CheckBox>().First().Size; }
        }

        public void button_Click(object sender, EventArgs e)
        {
            if (sender is CheckBox button)
            {
                if (1.Equals(button.Tag))
                    OnCharge1Changed?.Invoke(button.Checked);
                if (2.Equals(button.Tag))
                    OnCharge2Changed?.Invoke(button.Checked);
                if (3.Equals(button.Tag))
                    OnCharge3Changed?.Invoke(button.Checked);
                if (4.Equals(button.Tag))
                    OnCharge4Changed?.Invoke(button.Checked);
            }
        }
    }

    public class IonTypeSelectionPanel : TableLayoutPanel, IControlSize
    {

        public event Action<IonType, bool> IonTypeChanged;
        public event Action<string[]> LossChanged;

        private ToolTip _panelToolTip;
        private Label _lossesLabel;
        private bool _disposed;

        public IonTypeSelectionPanel()
        {
            SuspendLayout();
            LayoutSettings.ColumnCount = 6;
            LayoutSettings.RowCount = 2;
            _panelToolTip = new ToolTip();
            int colNumber = 0;
            var rowLabel = new Label()
            {
                Text = Resources.IonTypeSelector_NTermLabel,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            rowLabel.Padding = new Padding(2, rowLabel.Height / 4, 0, rowLabel.Height / 4);
            Controls.Add(rowLabel);
            SetCellPosition(rowLabel, new TableLayoutPanelCellPosition(colNumber++, 0));
            foreach (var ionType in IonTypeExtension.GetFragmentList().FindAll(type => type.IsNTerminal()))
            {
                var cb = CreateIonTypeCheckBox(ionType);
                cb.CheckedChanged += ionTypeButton_Click;
                Controls.Add(cb);
                SetCellPosition(cb, new TableLayoutPanelCellPosition(colNumber++, 0));
            }
            rowLabel.Margin = new Padding()
                {Top = ((Controls.OfType<CheckBox>().Last().Height - rowLabel.Height) / 2)};

            colNumber = 0;
            rowLabel = new Label()
            {
                Text = Resources.IonTypeSelector_CTermLabel,
                AutoSize = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            rowLabel.Padding = new Padding(2, rowLabel.Height / 4, 0, rowLabel.Height / 4);
            Controls.Add(rowLabel);
            SetCellPosition(rowLabel, new TableLayoutPanelCellPosition(colNumber++, 1));
            foreach (var ionType in IonTypeExtension.GetFragmentList().FindAll(type => type.IsCTerminal()))
            {
                var cb = CreateIonTypeCheckBox(ionType);
                Controls.Add(cb);
                cb.CheckedChanged += ionTypeButton_Click;
                SetCellPosition(cb, new TableLayoutPanelCellPosition(colNumber++, 1));
            }
            rowLabel.Margin = new Padding()
                { Top = ((Controls.OfType<CheckBox>().Last().Height - rowLabel.Height) / 2) };
            //ResumeLayout();
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _panelToolTip.Dispose();
                _lossesLabel.Dispose();
                foreach (var cb in Controls.OfType<CheckBox>().ToList().FindAll(cb => cb.Tag is FragmentLoss).ToList())
                {
                    if (cb.Tag is FragmentLoss)
                    {
                        cb.CheckedChanged -= LossButton_Click;
                        cb.MouseHover -= LossButton_MouseHover;
                    }

                    if (cb.Tag is IonType)
                    {
                        cb.CheckedChanged -= ionTypeButton_Click;
                    }
                }
            }

            _disposed = true;
        }

        CheckBox CreateIonTypeCheckBox(IonType ionType)
        {
            return new CheckBox()
            {
                Text = ionType.GetLocalizedString().ToUpper(),
                Appearance = Appearance.Button,
                AutoSize = true,
                Tag = ionType,
                TextAlign = ContentAlignment.MiddleCenter
            };
        }

        public Size ExpectedSize
        {
            get
            {
                if (Controls.Count == 0)
                    return Size;

                var rightBound = (int) new Statistics(Controls.OfType<Control>().Select(c => (double)c.Bounds.Right + c.Margin.Right)).Max();
                var bottomBound = (int)new Statistics(Controls.OfType<Control>().Select(c => (double)c.Bounds.Bottom+ c.Margin.Bottom)).Max();
                return new Size((int)(rightBound),(int)(bottomBound));
            }
        }

        public Size ButtonSize
        {
            get { return Controls.OfType<CheckBox>().First().Size; }
        }

        public void Update(GraphSpectrumSettings set, PeptideSettings peptideSet)
        {
            var modLosses = peptideSet.Modifications.StaticModifications.SelectMany(mod => mod.Losses??(new List<FragmentLoss>())).ToList();
            //Deduplicate the losses on formula
            modLosses = modLosses.GroupBy(loss => loss.Formula, loss => loss, (formula, losses) => losses.FirstOrDefault()).ToList();

            //remove the buttons for losses
            foreach (var cb in Controls.OfType<CheckBox>().ToList().FindAll(cb => cb.Tag is FragmentLoss).ToList())
            {
                Controls.Remove(cb);
                cb.CheckedChanged -= LossButton_Click;
                cb.MouseHover -= LossButton_MouseHover;
            }

            var tableWidth = 6;
            if (modLosses.Any())
            {
                var modRowNumber = (int)Math.Ceiling((double)modLosses.Count / (tableWidth - 1)); 
                if (LayoutSettings.RowCount == 2)
                {
                    LayoutSettings.RowCount = 2 + modRowNumber;
                    _lossesLabel = new Label()
                    {
                        Text = Resources.IonTypeSelector_LossesLabel,
                        AutoSize = true,
                        TextAlign = ContentAlignment.MiddleLeft
                    };
                    _lossesLabel.Padding = new Padding(2, _lossesLabel.Height/4, 0, _lossesLabel.Height/4);
                    Controls.Add(_lossesLabel);
                    SetCellPosition(_lossesLabel, new TableLayoutPanelCellPosition(0, 2));

                }
                //Update loss buttons
                var lossButtonStates = set.ShowLosses;
                var colCount = 0;
                var rowCount = 2;
                foreach (var loss in modLosses)
                {
                    //Add the buttons that are not there yet
                    if (!Controls.OfType<CheckBox>().Any(cb => loss.Equals(cb.Tag)))
                    {
                        var cb = new CheckBox()
                        {
                            Text = string.Format(CultureInfo.CurrentCulture, @"-{0:F0}", loss.AverageMass),
                            AutoSize = true,
                            Tag = loss,
                            Appearance = Appearance.Button,
                            Checked = lossButtonStates.Contains(loss.Formula)
                        };
                        cb.CheckedChanged += LossButton_Click;
                        cb.MouseHover += LossButton_MouseHover;
                        Controls.Add(cb);
                        if (colCount < tableWidth)
                            colCount++;
                        else
                        {
                            colCount = 1;
                            rowCount++;
                        }
                        SetCellPosition(cb, new TableLayoutPanelCellPosition(colCount, rowCount));
                    }
                }
            }
            else
            {
                LayoutSettings.RowCount = 2;
                if (_lossesLabel != null)
                {
                    Controls.Remove(_lossesLabel);
                    _lossesLabel = null;
                }
            }

            //Update ion type button states
            var showIonTypeDict = set.GetShowIonTypeSettings();
            foreach (var cb in Controls.OfType<CheckBox>().ToList().FindAll(cb => cb.Tag is IonType))
            {
                cb.CheckedChanged -= ionTypeButton_Click;
                cb.Checked = showIonTypeDict[(IonType)cb.Tag];
                cb.CheckedChanged += ionTypeButton_Click;
            }
        }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            AlignColumns();
        }

        public void AlignColumns()
        {
            //get the max width of a control in each column
            var widthList = Controls.OfType<CheckBox>().GroupBy(cb => GetCellPosition(cb).Column, cb => (double)cb.Width,
                (col, widths) => new { column = col, width = (int)new Statistics(widths).Max() }).ToDictionary(keySelector: pair => pair.column);
            //set same control width in each column
            foreach (var checkBox in Controls.OfType<CheckBox>())
                checkBox.Width = widthList[GetColumn(checkBox)].width;
        }

        public void ionTypeButton_Click(object sender, EventArgs e)
        {
            if (IonTypeChanged != null && sender is CheckBox cb)
            {
                if (cb.Tag is IonType type)
                    IonTypeChanged(type, cb.Checked);
            }
        }
        public void LossButton_Click(object sender, EventArgs e)
        {
            if (LossChanged != null && sender is CheckBox cb)
            {
                if (cb.Tag is FragmentLoss loss)
                {
                    //get the list of formulas for the selected losses
                    var losses = Controls.OfType<CheckBox>().ToList().FindAll(cbox => cbox.Tag is FragmentLoss && cbox.Checked)
                        .Select(cbox => ((FragmentLoss) cbox.Tag).Formula).ToArray();
                    LossChanged(losses);
                }
            }
        }

        public void LossButton_MouseHover(object sender, EventArgs e)
        {
            if(sender is CheckBox cb)
                _panelToolTip.Show( 
                    string.Format(Resources.IonTypeSelector_LossesTooltip, (cb.Tag as FragmentLoss)?.Formula),
                    cb);
        }
    }
}
