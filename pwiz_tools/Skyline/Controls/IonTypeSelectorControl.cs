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

namespace pwiz.Skyline.Controls
{
    public interface IControlSize
    {
        Size ExpectedSize { get; }
        void Update(GraphSpectrumSettings set, PeptideSettings peptideSet);
    }

    public class MenuControl<T> : ToolStripControlHost where T : Panel, IControlSize, new()
    {
        public MenuControl(GraphSpectrumSettings set, PeptideSettings peptideSet) : base(new T())
        {
            AutoSize = false;
            HostedControl.Update(set, peptideSet);
            HostedControl.Size = HostedControl.ExpectedSize;
        }

        public T HostedControl
        {
            get { return Control as T; }
        }

        public void Update(GraphSpectrumSettings set, PeptideSettings peptideSet)
        {
            HostedControl.Update(set, peptideSet);
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
        public void button_Click(object sender, EventArgs e)
        {
            if (sender is CheckBox button)
            {
                if (1.Equals(button.Tag) && OnCharge1Changed != null)
                    OnCharge1Changed(button.Checked);
                if (2.Equals(button.Tag) && OnCharge2Changed != null)
                    OnCharge2Changed(button.Checked);
                if (3.Equals(button.Tag) && OnCharge3Changed != null)
                    OnCharge3Changed(button.Checked);
                if (4.Equals(button.Tag) && OnCharge4Changed != null)
                    OnCharge4Changed(button.Checked);
            }
        }
    }

    public class IonTypeSelectionPanel : TableLayoutPanel, IControlSize
    {

        public event Action<IonType, bool> IonTypeChanged;
        public event Action<string[]> LossChanged;

        private ToolTip _panelToolTip;
        private FlowLayoutPanel _lossesPanel;
        private Label _lossesLabel;

        private const int PANEL_MARGIN = 3;

        public IonTypeSelectionPanel()
        {
            LayoutSettings.ColumnCount = 6;
            LayoutSettings.RowCount = 2;
            _panelToolTip = new ToolTip();
            int colNumber = 0;
            var rowLabel = new Label()
            {
                Text = "N-Term:",
                AutoSize = true
            };
            Controls.Add(rowLabel);
            SetCellPosition(rowLabel, new TableLayoutPanelCellPosition(colNumber++, 0));
            foreach (var ionType in IonTypeExtension.GetFragmentList().FindAll(type => type.IsNTerminal()))
            {
                var cb = CreateIonTypeCheckBox(ionType);
                cb.CheckedChanged += ionTypeButton_Click;
                Controls.Add(cb);
                SetCellPosition(cb, new TableLayoutPanelCellPosition(colNumber++, 0));
            }
            colNumber = 0;
            rowLabel = new Label()
            {
                Text = "C-Term:",
                AutoSize = true
            };
            Controls.Add(rowLabel);
            SetCellPosition(rowLabel, new TableLayoutPanelCellPosition(colNumber++, 1));
            foreach (var ionType in IonTypeExtension.GetFragmentList().FindAll(type => type.IsCTerminal()))
            {
                var cb = CreateIonTypeCheckBox(ionType);
                Controls.Add(cb);
                cb.CheckedChanged += ionTypeButton_Click;
                SetCellPosition(cb, new TableLayoutPanelCellPosition(colNumber++, 1));
            }
        }

        CheckBox CreateIonTypeCheckBox(IonType ionType)
        {
            return new CheckBox()
            {
                Text = ionType.GetLocalizedString().ToUpper(),
                Appearance = Appearance.Button,
                AutoSize = true,
                Tag = ionType
            };
        }

        public Size ExpectedSize
        {
            get
            {
                if (Controls.Count == 0)
                    return Size;

                var rightBound = 0.0f;
                var bottomBound = 0.0f;
                foreach (var cb in Controls.OfType<Control>())
                {
                    if (cb.Bounds.Right + cb.Margin.Right > rightBound)
                        rightBound = cb.Bounds.Right + cb.Margin.Right;
                    if (cb.Bounds.Bottom + cb.Margin.Bottom > bottomBound)
                        bottomBound = cb.Bounds.Bottom + cb.Margin.Bottom;
                }

                return new Size((int)(rightBound + PANEL_MARGIN),(int)(bottomBound + PANEL_MARGIN));
            }
        }

        public void Update(GraphSpectrumSettings set, PeptideSettings peptideSet)
        {
            var modLosses = peptideSet.Modifications.StaticModifications.SelectMany(mod => mod.Losses??(new List<FragmentLoss>()));
            //Deduplicate the losses on formula
            modLosses = modLosses?.GroupBy(loss => loss.Formula, loss => loss, (formula, losses) => losses.FirstOrDefault()).ToList();

            if (_lossesPanel != null)
            {
                //remove the buttons for losses
                foreach (var cb in _lossesPanel.Controls.OfType<CheckBox>().ToList())
                {
                    _lossesPanel.Controls.Remove(cb);
                    cb.CheckedChanged -= LossButton_Click;
                    cb.MouseHover -= LossButton_MouseHover;
                }
            }

            if (modLosses.Any())
            {
                if (LayoutSettings.RowCount != 3)
                {
                    LayoutSettings.RowCount = 3;

                    _lossesLabel = new Label()
                    {
                        Text = "Losses:",
                        AutoSize = true,
                        Tag = @"LossesLabel"
                    };
                    Controls.Add(_lossesLabel);
                    SetCellPosition(_lossesLabel, new TableLayoutPanelCellPosition(0, 2));
                    _lossesPanel = new FlowLayoutPanel()
                    {
                        AutoSize = true
                    };
                    Controls.Add(_lossesPanel);
                    SetCellPosition(_lossesPanel, new TableLayoutPanelCellPosition(1, 2));
                    SetColumnSpan(_lossesPanel, ColumnCount - 1);
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
                if (_lossesPanel != null)
                {
                    Controls.Remove(_lossesPanel);
                    _lossesPanel = null;
                }

            }

            //Update loss buttons
            var lossButtonStates = set.ShowLosses;
            foreach (var loss in modLosses)
            {
                //Add the buttons that are not there yet
                if (!Controls.OfType<CheckBox>().Any(cb => loss.Equals(cb.Tag)))
                {
                    var cb = new CheckBox()
                    {
                        Text = string.Format("-{0:F0}", loss.AverageMass),
                        AutoSize = true,
                        Tag = loss,
                        Appearance = Appearance.Button,
                        Checked = lossButtonStates.Contains(loss.Formula)
                    };
                    cb.CheckedChanged += LossButton_Click;
                    cb.MouseHover += LossButton_MouseHover;
                    _lossesPanel?.Controls.Add(cb);
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
                    var losses = _lossesPanel.Controls.OfType<CheckBox>().ToList().FindAll(cbox => cbox.Checked)
                        .Select(cbox => ((FragmentLoss) cbox.Tag).Formula).ToArray();
                    LossChanged(losses);
                }
            }
        }

        public void LossButton_MouseHover(object sender, EventArgs e)
        {
            if(sender is CheckBox cb)
                _panelToolTip.Show( 
                    string.Format("Formula: {0}", (cb.Tag as FragmentLoss).Formula),
                    cb);
        }
    }
}
