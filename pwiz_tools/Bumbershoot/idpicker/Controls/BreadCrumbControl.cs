//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;
using PopupControl;

namespace IDPicker.Controls
{
    internal class BreadCrumbToolStrip : ToolStrip
    {
        /*protected ContentAlignment RtlTranslateAlignment (ContentAlignment align)
        {
            return ContentAlignment.MiddleRight;
        }

        protected HorizontalAlignment RtlTranslateAlignment (HorizontalAlignment align)
        {
            return HorizontalAlignment.Right;
        }

        protected LeftRightAlignment RtlTranslateAlignment (LeftRightAlignment align)
        {
            return LeftRightAlignment.Right;
        }

        protected internal ContentAlignment RtlTranslateContent (ContentAlignment align)
        {
            return ContentAlignment.MiddleRight;
        }

        protected HorizontalAlignment RtlTranslateHorizontal (HorizontalAlignment align)
        {
            return HorizontalAlignment.Right;
        }

        protected LeftRightAlignment RtlTranslateLeftRight (LeftRightAlignment align)
        {
            return LeftRightAlignment.Right;
        }*/
    }

    public class BreadCrumb
    {
        public string Text { get; private set; }
        public object Tag { get; private set; }

        public BreadCrumb (string text, object tag)
        {
            Tag = tag;
            Text = text.Contains("(1)") ? SingleToShown(text) : text;
        }

        private string SingleToShown(string text)
        {
            if (text.Contains("Modified site (1)"))
                text = text.Replace("Modified site (1)", "Modified site: " + ((DataFilter) Tag).ModifiedSite.First());
            if (text.Contains("Mass shift (1)"))
                text = text.Replace("Mass shift (1)", "Mass shift: " + Math.Round(((DataFilter)Tag).Modifications.First().MonoMassDelta));
            else if (text.Contains("Cluster (1)"))
                text = text.Replace(" (1)", ": " + ((DataFilter)Tag).Cluster.First());
            else if (text.Contains("Protein (1)"))
            {
                var accession = ((DataFilter) Tag).Protein.First().Accession.Length > 8
                                    ? ((DataFilter) Tag).Protein.First().Accession.Remove(8) + "..."
                                    : ((DataFilter) Tag).Protein.First().Accession;
                text = text.Replace(" (1)", ": " + accession);
            }
            else if (text.Contains("Peptide (1)"))
            {
                var sequence = ((DataFilter) Tag).Peptide.First().Sequence.Length > 8
                                   ? ((DataFilter) Tag).Peptide.First().Sequence.Remove(8) + "..."
                                   : ((DataFilter) Tag).Peptide.First().Sequence;
                text = text.Replace(" (1)", ": " + sequence);
            }
            else if (text.Contains("Interpretation (1)"))
            {
                var sequence = ((DataFilter) Tag).DistinctPeptideKey.First().Sequence.Length > 8
                                   ? ((DataFilter) Tag).DistinctPeptideKey.First().Sequence.Remove(8) + "..."
                                   : ((DataFilter) Tag).DistinctPeptideKey.First().Sequence;
                text = text.Replace(" (1)", ": " + sequence);
            }
            else if (text.Contains("Group (1)"))
                text = text.Replace(" (1)", ": " + ((DataFilter)Tag).SpectrumSourceGroup.First().Name);
            else if (text.Contains("Source (1)"))
                text = text.Replace(" (1)", ": " + ((DataFilter)Tag).SpectrumSource.First().Name);
            else if (text.Contains("Spectrum (1)"))
                text = text.Replace(" (1)", ": " + ((DataFilter)Tag).Spectrum.First().NativeID);
            else if (text.Contains("Analysis (1)"))
                text = text.Replace(" (1)", ": " + ((DataFilter)Tag).Analysis.First().Name);
            else if (text.Contains("Charge (1)"))
                text = text.Replace(" (1)", ": " + ((DataFilter)Tag).Charge.First());

            return text;
        }

        public void ReloadTextFromTag()
        {
            Text = Tag.ToString().Contains("(1)") ? SingleToShown(Tag.ToString()) : Tag.ToString();
        }
    }

    public class BreadCrumbClickedEventArgs : EventArgs
    {
        internal BreadCrumbClickedEventArgs (BreadCrumb breadCrumb) { BreadCrumb = breadCrumb; }
        public BreadCrumb BreadCrumb { get; private set; }
    }

    public class BreadCrumbControl : UserControl
    {
        private BreadCrumbToolStrip breadCrumbToolStrip;
        private BindingList<BreadCrumb> breadCrumbs;

        public IList<BreadCrumb> BreadCrumbs { get { return breadCrumbs; } }

        public event EventHandler<BreadCrumbClickedEventArgs> BreadCrumbClicked;

        public BreadCrumbControl ()
        {
            breadCrumbToolStrip = new BreadCrumbToolStrip()
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden
            };
            breadCrumbToolStrip.OverflowButton.Alignment = ToolStripItemAlignment.Left;

            Controls.Add(breadCrumbToolStrip);

            breadCrumbs = new BindingList<BreadCrumb>()
            {
                RaiseListChangedEvents = true
            };

            breadCrumbs.ListChanged += new ListChangedEventHandler(breadCrumbs_ListChanged);
        }

        void breadCrumbs_ListChanged (object sender, ListChangedEventArgs e)
        {
            RemoveStaleBread();

            SuspendLayout();
            breadCrumbToolStrip.Items.Clear();

            if (breadCrumbs.Count > 0)
            {
                foreach (var breadCrumb in breadCrumbs)
                {
                    ToolStripItemOverflow overflow = breadCrumb == breadCrumbs.Last() ? ToolStripItemOverflow.Never
                                                                                      : ToolStripItemOverflow.AsNeeded;

                    breadCrumb.ReloadTextFromTag();

                    var breadCrumbSeparator = new ToolStripLabel()
                    {
                        Text = ">",
                        TextAlign = ContentAlignment.MiddleLeft,
                        AutoSize = true,
                        Overflow = overflow,
                    };
                    breadCrumbSeparator.IsLink = true;
                    breadCrumbSeparator.LinkBehavior = LinkBehavior.HoverUnderline;
                    breadCrumbSeparator.ActiveLinkColor = Color.Black;
                    breadCrumbSeparator.LinkColor = Color.Black;
                    breadCrumbSeparator.Click += breadCrumbSeparator_Click;
                    breadCrumbSeparator.MouseLeave += breadCrumbSeparator_MouseLeave;
                    breadCrumbToolStrip.Items.Add(breadCrumbSeparator);

                    var breadCrumbLabel = new ToolStripStatusLabel()
                    {
                        Text = breadCrumb.Text,
                        TextAlign = ContentAlignment.MiddleRight,
                        Overflow = overflow,
                        Tag = breadCrumb.Tag
                    };
                    breadCrumbLabel.IsLink = true;
                    breadCrumbLabel.LinkBehavior = LinkBehavior.HoverUnderline;
                    breadCrumbLabel.ActiveLinkColor = Color.Black;
                    breadCrumbLabel.LinkColor = Color.Black;
                    breadCrumbLabel.Click += breadCrumbLabel_Click;
                    breadCrumbToolStrip.Items.Add(breadCrumbLabel);

                    var breadCrumbLinkLabel = new ToolStripLabel()
                    {
                        Text = "(x)",
                        TextAlign = ContentAlignment.MiddleLeft,
                        IsLink = true,
                        LinkBehavior = LinkBehavior.AlwaysUnderline,
                        AutoSize = true,
                        BackColor = Color.Red,
                        Overflow = overflow,
                        Tag = breadCrumb
                    };
                    breadCrumbLinkLabel.Click += new EventHandler(itemLinkLabel_LinkClicked);
                    breadCrumbToolStrip.Items.Add(breadCrumbLinkLabel);
                }
            }

            ResumeLayout();
            Refresh();
        }

        private void breadCrumbSeparator_MouseLeave(object sender, EventArgs e)
        {
            ((ToolStripLabel)sender).LinkColor = Color.Black;
            ((ToolStripLabel)sender).LinkColor = Color.Black;
        }

        private void breadCrumbSeparator_Click(object sender, EventArgs e)
        {
            if (((ToolStripLabel)sender).LinkColor == Color.OrangeRed)
            {
                int deletedIndex = breadCrumbToolStrip.Items.IndexOf(sender as ToolStripLabel);
                for (int x = breadCrumbToolStrip.Items.Count - 1; x > deletedIndex; x -= 3)
                    OnLinkClicked(breadCrumbToolStrip.Items[x].Tag as BreadCrumb);
            }

            ((ToolStripLabel)sender).LinkColor = Color.OrangeRed;
            ((ToolStripLabel) sender).LinkColor = Color.OrangeRed;
        }

        private Popup _subItemPopup;

        void breadCrumbLabel_Click(object sender, EventArgs e)
        {
            var test = new DataFilter();
            var arrow = breadCrumbToolStrip.Items[breadCrumbToolStrip.Items.IndexOf(sender as ToolStripStatusLabel) - 1];
            if (_subItemPopup != null && _subItemPopup.Tag == ((ToolStripStatusLabel) sender).Tag)
            {
                _subItemPopup.Tag = null;
                _subItemPopup.Close();
            }
            else if (((ToolStripStatusLabel) sender).Tag is DataFilter)
            {
                breadCrumbToolStrip.Items[breadCrumbToolStrip.Items.IndexOf(sender as ToolStripStatusLabel) - 1].Text =
                    "v";

                var SubItemControl = new UserControl {AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink};
                var ContainerBox = CreateContainerBox(breadCrumbToolStrip.Items[breadCrumbToolStrip.Items.IndexOf((ToolStripStatusLabel)sender)+1].Tag as BreadCrumb);
                SubItemControl.Controls.Add(ContainerBox);
                SubItemControl.Size = ContainerBox.Size;
                _subItemPopup = new Popup(SubItemControl);
                _subItemPopup.AutoClose = true;
                _subItemPopup.FocusOnOpen = true;
                _subItemPopup.Tag = (sender as ToolStripStatusLabel).Tag;
                _subItemPopup.Closed += delegate
                                           {
                                               breadCrumbToolStrip.Items[
                                                   breadCrumbToolStrip.Items.IndexOf(sender as ToolStripStatusLabel) - 1
                                                   ].Text = ">";
                                               _subItemPopup.Tag = null;
                                           };

                _subItemPopup.Show(PointToScreen(new Point(arrow.Bounds.Left, arrow.Bounds.Bottom)));
            }
        }

        private TableLayoutPanel CreateContainerBox(BreadCrumb crumb)
        {
            var dataFilter = (DataFilter)crumb.Tag;
            var tableLayoutPanel = new TableLayoutPanel
            {
                AutoSize = true,
                Padding = new Padding(5),
                Size = new Size(0,0),
                //BorderStyle = BorderStyle.Fixed3D,
                //CellBorderStyle = TableLayoutPanelCellBorderStyle.InsetDouble
            };


            if (dataFilter.Cluster != null)
            {
                for (var x = 0; x < dataFilter.Cluster.Count;x++)
                {
                    var savedItem = dataFilter.Cluster[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.ToString(), AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.Cluster.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.Cluster.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.Protein != null)
            {
                for (var x = 0; x < dataFilter.Protein.Count; x++)
                {
                    var savedItem = dataFilter.Protein[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.Accession, AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.Protein.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.Protein.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.Peptide != null)
            {
                for (var x = 0; x < dataFilter.Peptide.Count; x++)
                {
                    var savedItem = dataFilter.Peptide[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.Sequence, AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.Peptide.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.Peptide.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.DistinctPeptideKey != null)
            {
                for (var x = 0; x < dataFilter.DistinctPeptideKey.Count; x++)
                {
                    var savedItem = dataFilter.DistinctPeptideKey[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.Sequence, AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.DistinctPeptideKey.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.DistinctPeptideKey.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.SpectrumSourceGroup != null)
            {
                for (var x = 0; x < dataFilter.SpectrumSourceGroup.Count; x++)
                {
                    var savedItem = dataFilter.SpectrumSourceGroup[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.Name, AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.SpectrumSourceGroup.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.SpectrumSourceGroup.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.SpectrumSource != null)
            {
                for (var x = 0; x < dataFilter.SpectrumSource.Count; x++)
                {
                    var savedItem = dataFilter.SpectrumSource[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.Name, AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.SpectrumSource.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.SpectrumSource.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.Spectrum != null)
            {
                for (var x = 0; x < dataFilter.Spectrum.Count; x++)
                {
                    var savedItem = dataFilter.Spectrum[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.NativeID, AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.Spectrum.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.Spectrum.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.Analysis != null)
            {
                for (var x = 0; x < dataFilter.Analysis.Count; x++)
                {
                    var savedItem = dataFilter.Analysis[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.Name, AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.Analysis.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.Analysis.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.Charge != null)
            {
                for (var x = 0; x < dataFilter.Charge.Count; x++)
                {
                    var savedItem = dataFilter.Charge[x];
                    var itemNumber = x;
                    var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    tableLayoutPanel.Controls.Add(arrow, 0, x);

                    var description = new Label { Text = savedItem.ToString(), AutoSize = true, Margin = new Padding(5) };
                    tableLayoutPanel.Controls.Add(description, 1, x);

                    var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                    deleteLink.Click += delegate
                                            {
                                                dataFilter.Charge.RemoveAt(itemNumber);
                                                _subItemPopup.Close();
                                                if (dataFilter.Charge.Count == 0)
                                                    BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                else
                                                {
                                                    BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                    breadCrumbs_ListChanged(null, null);
                                                }
                                            };
                    tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                }
            }
            else if (dataFilter.ModifiedSite != null || dataFilter.Modifications != null)
            {
                if (dataFilter.ModifiedSite != null)
                {

                    for (var x = 0; x < dataFilter.ModifiedSite.Count; x++)
                    {
                        var savedItem = dataFilter.ModifiedSite[x];
                        var itemNumber = x;
                        var arrow = new Label {Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5)};
                        tableLayoutPanel.Controls.Add(arrow, 0, x);

                        var description = new Label
                                              {Text = savedItem.ToString(), AutoSize = true, Margin = new Padding(5)};
                        tableLayoutPanel.Controls.Add(description, 1, x);

                        var deleteLink = new LinkLabel {Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5)};
                        deleteLink.Click += delegate
                                                {
                                                    dataFilter.ModifiedSite.RemoveAt(itemNumber);
                                                    _subItemPopup.Close();
                                                    if (dataFilter.ModifiedSite.Count == 0)
                                                    {
                                                        dataFilter.ModifiedSite = null;
                                                        if (dataFilter.Modifications == null)
                                                            BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                    }
                                                    else
                                                    {
                                                        BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                        breadCrumbs_ListChanged(null, null);
                                                    }
                                                };
                        tableLayoutPanel.Controls.Add(deleteLink, 2, x);
                    }
                }
                var filledRows = tableLayoutPanel.Controls.Count/3;
                if (dataFilter.Modifications != null)
                {
                    var distinctModMasses = (from mod in dataFilter.Modifications
                                             select Math.Round(mod.MonoMassDelta).ToString())
                    .Distinct().ToList();

                    for (var x = 0; x < distinctModMasses.Count; x++)
                    {
                        var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                        tableLayoutPanel.Controls.Add(arrow, 0, x + filledRows);

                        var description = new Label { Text = distinctModMasses[x], AutoSize = true, Margin = new Padding(5) };
                        tableLayoutPanel.Controls.Add(description, 1, x + filledRows);

                        var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                        int listLocation = x;
                        deleteLink.Click += delegate
                                                {
                                                    var targetedMods = (from mod in dataFilter.Modifications
                                                                        where Math.Round(mod.MonoMassDelta).ToString() == distinctModMasses[listLocation]
                                                                            select mod).Distinct().ToList();
                                                    foreach (var mod in targetedMods)
                                                        dataFilter.Modifications.Remove(mod);
                                                    _subItemPopup.Close();
                                                    if (dataFilter.Modifications.Count == 0)
                                                    {
                                                        dataFilter.Modifications = null;
                                                        BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                                                    }
                                                    else
                                                    {
                                                        BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                                                        breadCrumbs_ListChanged(null, null);
                                                    }
                                                };
                        tableLayoutPanel.Controls.Add(deleteLink, 2, x + filledRows);
                    }
                }
            }

            return tableLayoutPanel;
        }

        private void RemoveStaleBread()
        {
            var overallData = new DataFilter();

            for (int x = breadCrumbs.Count-1; x >= 0; x--)
            {
                var item = (DataFilter) breadCrumbs[x].Tag;

                if (breadCrumbs[x].Text.Contains("Q-value"))
                    breadCrumbs.RemoveAt(x); //Sometimes bugs and gets extra-long breadcrumb
                else if (item.Cluster != null)
                {
                    if (overallData.Cluster == null)
                        overallData.Cluster = item.Cluster;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.Protein != null)
                {
                    if (overallData.Protein == null)
                        overallData.Protein = item.Protein;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.Peptide != null)
                {
                    if (overallData.Peptide == null)
                        overallData.Peptide = item.Peptide;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.DistinctPeptideKey != null)
                {
                    if (overallData.DistinctPeptideKey == null)
                        overallData.DistinctPeptideKey = item.DistinctPeptideKey;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.SpectrumSourceGroup != null)
                {
                    if (overallData.SpectrumSourceGroup == null)
                        overallData.SpectrumSourceGroup = item.SpectrumSourceGroup;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.SpectrumSource != null)
                {
                    if (overallData.SpectrumSource == null)
                        overallData.SpectrumSource = item.SpectrumSource;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.Spectrum != null)
                {
                    if (overallData.Spectrum == null)
                        overallData.Spectrum = item.Spectrum;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.Analysis != null)
                {
                    if (overallData.Analysis == null)
                        overallData.Analysis = item.Analysis;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.Charge != null)
                {
                    if (overallData.Charge == null)
                        overallData.Charge = item.Charge;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else if (item.ModifiedSite == null && item.Modifications.Count == 0)
                {
                    if (overallData.MaximumQValue != (decimal)-3.1415926535)
                        overallData.MaximumQValue = (decimal)-3.1415926535;
                    else
                        breadCrumbs.RemoveAt(x);
                }
                else
                {
                    if (overallData.ModifiedSite == null)
                        overallData.ModifiedSite = new List<char?>();
                    else
                        breadCrumbs.RemoveAt(x);
                }
            }
        }

        protected void OnLinkClicked (BreadCrumb sender)
        {
            if (BreadCrumbClicked != null)
                BreadCrumbClicked(sender, new BreadCrumbClickedEventArgs(sender));
        }

        void itemLinkLabel_LinkClicked (object sender, EventArgs e)
        {
            OnLinkClicked((sender as ToolStripItem).Tag as BreadCrumb);
        }
    }
}
