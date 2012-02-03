//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
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
    }

    public class BreadCrumb
    {
        public string Text { get; private set; }
        public object Tag { get; private set; }

        public BreadCrumb (string text, object tag)
        {
            Tag = tag;
            Text = text;
        }

        public void ReloadTextFromTag()
        {
            Text = Tag.ToString();
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
        private Popup _subItemPopup;
        private Dictionary<int, string> _modMassList;
        private Size _tableSize;

        public IList<BreadCrumb> BreadCrumbs { get { return breadCrumbs; } }

        public event EventHandler<BreadCrumbClickedEventArgs> BreadCrumbClicked;

        public BreadCrumbControl ()
        {
            breadCrumbToolStrip = new BreadCrumbToolStrip()
            {
                Dock = DockStyle.Fill,
                GripStyle = ToolStripGripStyle.Hidden,
                Cursor = DefaultCursor
            };
            breadCrumbToolStrip.OverflowButton.Alignment = ToolStripItemAlignment.Left;

            Controls.Add(breadCrumbToolStrip);

            breadCrumbs = new BindingList<BreadCrumb>()
            {
                RaiseListChangedEvents = true
            };

            breadCrumbs.ListChanged += new ListChangedEventHandler(breadCrumbs_ListChanged);
        }

        public void ClearBreadcrumbs()
        {
            breadCrumbs.Clear();
            breadCrumbToolStrip.Items.Clear();
        }

        void breadCrumbs_ListChanged (object sender, ListChangedEventArgs e)
        {
            //get rid of duplicate breadcrumbs
            RemoveStaleBread();

            //prepare visible breadcrumb list for update
            SuspendLayout();
            breadCrumbToolStrip.Items.Clear();

            //go through each bread crumb and create menu items for them
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
            //Reset separator to unclicked state
            ((ToolStripLabel)sender).LinkColor = Color.Black;
            ((ToolStripLabel)sender).LinkColor = Color.Black;
        }

        private void breadCrumbSeparator_Click(object sender, EventArgs e)
        {
            //If separator has been clicked once clear all
            if (((ToolStripLabel)sender).LinkColor == Color.OrangeRed)
            {
                int deletedIndex = breadCrumbToolStrip.Items.IndexOf(sender as ToolStripLabel);
                for (int x = breadCrumbToolStrip.Items.Count - 1; x > deletedIndex; x -= 3)
                    OnLinkClicked(breadCrumbToolStrip.Items[x].Tag as BreadCrumb);
            }

            //Indicate that separator has been clicked once
            ((ToolStripLabel)sender).LinkColor = Color.OrangeRed;
            ((ToolStripLabel) sender).LinkColor = Color.OrangeRed;
        }

        void breadCrumbLabel_Click(object sender, EventArgs e)
        {
            var arrow = breadCrumbToolStrip.Items[breadCrumbToolStrip.Items.IndexOf(sender as ToolStripStatusLabel) - 1];
            if (_subItemPopup != null && _subItemPopup.Tag == ((ToolStripStatusLabel) sender).Tag)
            {
                _subItemPopup.Tag = null;
                _subItemPopup.Close();
            }
            else if (((ToolStripStatusLabel) sender).Tag is DataFilter)
            {
                breadCrumbToolStrip.Items[breadCrumbToolStrip.Items.IndexOf(sender as ToolStripStatusLabel) - 1].Text = "v";

                var ContainerBox = CreateContainerBox(breadCrumbToolStrip.Items[breadCrumbToolStrip.Items.IndexOf((ToolStripStatusLabel)sender)+1].Tag as BreadCrumb);
                _subItemPopup = new Popup(ContainerBox)
                                    {
                                        AutoSize = true,
                                        Size = _tableSize,
                                        AutoClose = true,
                                        FocusOnOpen = true,
                                        Tag = (sender as ToolStripStatusLabel).Tag
                                    };
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
            var dataFilter = (DataFilter) crumb.Tag;
            var crumbTable = new TableLayoutPanel
                                       {
                                           AutoSize = true,
                                           Padding = new Padding(5),
                                           Size = new Size(0, 0),
                                           AutoSizeMode = AutoSizeMode.GrowAndShrink
                                       };
            var containerBook = CreateContainerBook(dataFilter);

            PopulateContainerBox(dataFilter, containerBook, 0, crumbTable, crumb);

            return crumbTable;
        }

        private List<List<KeyValuePair<string, int>>> CreateContainerBook(DataFilter dataFilter)
        {
            List<KeyValuePair<string, int>> contents = CreateContainerContents(dataFilter);
            var page = new List<KeyValuePair<string, int>>();
            var book = new List<List<KeyValuePair<string, int>>>();

            //from the full list of breadcrumbs in the popup create multiple sets of 10
            for (int x = 0; x < contents.Count; x++)
            {
                page.Add(contents[x]);
                if ((x + 1) % 10 == 0)
                {
                    book.Add(page);
                    page = new List<KeyValuePair<string, int>>();
                }
            }
            if (page.Count > 0)
                book.Add(page);
            return book;
        }

        private List<KeyValuePair<string, int>> CreateContainerContents(DataFilter dataFilter)
        {
            var contents = new List<KeyValuePair<string, int>>();

            //go through the arduous process of figuring out what the data type is
            //and creating a list of all values present
            if (dataFilter.Cluster != null)
            {
                for (var x = 0; x < dataFilter.Cluster.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.Cluster[x].ToString(), x));
            }
            else if (dataFilter.ProteinGroup != null)
            {
                for (var x = 0; x < dataFilter.ProteinGroup.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.ProteinGroup[x].ToString(), x));
            }
            else if (dataFilter.Protein != null)
            {
                for (var x = 0; x < dataFilter.Protein.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.Protein[x].Accession, x));
            }
            else if (dataFilter.Peptide != null)
            {
                for (var x = 0; x < dataFilter.Peptide.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.Peptide[x].Sequence, x));
            }
            else if (dataFilter.DistinctMatchKey != null)
            {
                for (var x = 0; x < dataFilter.DistinctMatchKey.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.DistinctMatchKey[x].ToString(), x));
            }
            else if (dataFilter.SpectrumSourceGroup != null)
            {
                for (var x = 0; x < dataFilter.SpectrumSourceGroup.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.SpectrumSourceGroup[x].Name, x));
            }
            else if (dataFilter.SpectrumSource != null)
            {
                for (var x = 0; x < dataFilter.SpectrumSource.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.SpectrumSource[x].Name, x));
            }
            else if (dataFilter.Spectrum != null)
            {
                for (var x = 0; x < dataFilter.Spectrum.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.Spectrum[x].NativeID, x));
            }
            else if (dataFilter.Analysis != null)
            {
                for (var x = 0; x < dataFilter.Analysis.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.Analysis[x].Name, x));
            }
            else if (dataFilter.Charge != null)
            {
                for (var x = 0; x < dataFilter.Charge.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.Charge[x].ToString(), x));
            }
            else if (dataFilter.AminoAcidOffset != null)
            {
                for (var x = 0; x < dataFilter.AminoAcidOffset.Count; x++)
                    contents.Add(new KeyValuePair<string, int>(dataFilter.AminoAcidOffset[x].ToString(), x));
            }
            else if (dataFilter.ModifiedSite != null || dataFilter.Modifications != null)
            {
                //modifications is a special case, containing two differnt filter types
                if (dataFilter.Modifications != null)
                {
                    //mod masses have to be stored in a separate list due to
                    //the way they have are removed from the breadcrumb trail
                    _modMassList = new Dictionary<int, string>();
                    var distinctModMasses = (from mod in dataFilter.Modifications
                                             select Math.Round(mod.MonoMassDelta).ToString())
                    .Distinct().ToList();

                    for (var x = 0; x < distinctModMasses.Count; x++)
                    {
                        //to distinguish mass indexes from site indexes,
                        //mass indexes are stored as negative numbers
                        var translatedIndex = (x + 1) * -1;
                        _modMassList.Add(translatedIndex, distinctModMasses[x]);
                        contents.Add(new KeyValuePair<string, int>("Mass: " + distinctModMasses[x], translatedIndex));
                    }
                }
                if (dataFilter.ModifiedSite != null)
                {
                    //modified sites can be added as normal
                    for (var x = 0; x < dataFilter.ModifiedSite.Count; x++)
                        contents.Add(new KeyValuePair<string, int>("Residue: " + dataFilter.ModifiedSite[x].ToString(), x));
                }
            }

            //sort everything into aplhebitical order to aid in finding specific filters
            contents.Sort((a, b) => string.Compare(a.Key, b.Key));

            return contents;
        }

        private void PopulateContainerBox(DataFilter dataFilter, List<List<KeyValuePair<string, int>>> containerBook, int pageIndex, TableLayoutPanel crumbTable, BreadCrumb crumb)
        {
            var firstColumnSize = 0;
            var lastColumnSize = 0;
            var largestDescription = 0;
            var dataSet = containerBook[pageIndex];

            //clear current table and populate with the crumbs present in the current page
            crumbTable.Controls.Clear();
            for (var x = 0; x < dataSet.Count; x++)
            {
                var kvp = dataSet[x];
                var arrow = new Label { Text = ">", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                crumbTable.Controls.Add(arrow, 0, x);
                if (arrow.Width > 0)
                    firstColumnSize = arrow.Width;

                var description = new Label { Text = kvp.Key, AutoSize = true, Margin = new Padding(5) };
                crumbTable.Controls.Add(description, 1, x);
                if (description.Width > largestDescription)
                    largestDescription = description.Width;

                var deleteLink = new LinkLabel { Text = "(x)", AutoSize = true, Margin = new Padding(0, 5, 0, 5) };
                deleteLink.Click += delegate
                {
                    var allGone = RemoveItemInContainer(dataFilter, kvp.Value);
                    _subItemPopup.Close();
                    if (allGone)
                        BreadCrumbClicked(crumb, new BreadCrumbClickedEventArgs(crumb));
                    else
                    {
                        BreadCrumbClicked(null, new BreadCrumbClickedEventArgs(null));
                        breadCrumbs_ListChanged(null, null);
                    }
                };
                crumbTable.Controls.Add(deleteLink, 2, x);
                if (deleteLink.Width > 0)
                    lastColumnSize = deleteLink.Width;
            }

            //if there exists more than one page add navigation row
            if (containerBook.Count > 1)
            {
                //forward and back buttons
                if (pageIndex > 0)
                {
                    var backButton = new Button { Text = "<-", Size = new Size(30, 20) };
                    backButton.Click += (x, y) =>
                                            {
                                                PopulateContainerBox(dataFilter, containerBook, pageIndex - 1,
                                                                     crumbTable, crumb);
                                                if (_subItemPopup != null)
                                                    _subItemPopup.Size = _tableSize;
                                            };
                    crumbTable.Controls.Add(backButton, 0, dataSet.Count);
                    if (backButton.Width > firstColumnSize)
                        firstColumnSize = backButton.Width;
                    
                }
                if (pageIndex < containerBook.Count - 1)
                {
                    var forwardButton = new Button { Text = "->", Size = new Size(30, 20) };
                    forwardButton.Click += (x, y) =>
                                               {
                                                   PopulateContainerBox(dataFilter, containerBook, pageIndex + 1,
                                                                        crumbTable, crumb);
                                                   if (_subItemPopup != null)
                                                       _subItemPopup.Size = _tableSize;
                                               };
                    crumbTable.Controls.Add(forwardButton, 2, dataSet.Count);
                    if (forwardButton.Width > lastColumnSize)
                        lastColumnSize = forwardButton.Width;
                    
                }

                //middle page reporting / goto page control
                var cellPanel = new Panel {Size = new Size(largestDescription, 23)};
                var pageLabel = new Label {Text = "Page ", AutoSize = true};
                pageLabel.Font = new Font(pageLabel.Font.ToString(),10.0f);
                var pageSelectBox = new TextBox {Text = (pageIndex + 1).ToString(), TextAlign = HorizontalAlignment.Center};
                pageSelectBox.KeyPress += (x, y) =>
                                              {
                                                  //if enter is pressed go to selected page (or reset to current page if no valid page is selected)
                                                  if (y.KeyChar == (char)13)
                                                  {
                                                      y.Handled = true;
                                                      int tempInt;
                                                      if (!int.TryParse(pageSelectBox.Text, out tempInt) ||
                                                          tempInt <= 0 || tempInt > containerBook.Count)
                                                          pageSelectBox.Text = (pageIndex + 1).ToString();
                                                      else
                                                      {
                                                          PopulateContainerBox(dataFilter, containerBook, tempInt - 1,
                                                                               crumbTable, crumb);
                                                          if (_subItemPopup != null)
                                                              _subItemPopup.Size = _tableSize;
                                                      }
                                                  }
                                             };
                pageSelectBox.Leave += (x, y) => { pageSelectBox.Text = (pageIndex + 1).ToString(); };
                var totalLabel = new Label { Text = " of " + containerBook.Count, AutoSize = true };
                totalLabel.Font = new Font(totalLabel.Font.ToString(), 10.0f);

                //add all three controls to the cell panel
                cellPanel.Controls.Add(pageLabel);
                cellPanel.Controls.Add(pageSelectBox);
                cellPanel.Controls.Add(totalLabel);

                //make sure boxes are wide enough
                pageSelectBox.Width = totalLabel.Width-10;
                var totalCellWidth = pageLabel.Width + pageSelectBox.Width + totalLabel.Width;
                if (totalCellWidth > largestDescription)
                {
                    largestDescription = totalCellWidth;
                    cellPanel.Width = totalCellWidth;
                }

                //reposition controls to final locations
                var startlocation = (largestDescription - totalCellWidth) / 2;
                pageLabel.Location = new Point(startlocation, 0);
                pageSelectBox.Location = new Point(startlocation + pageLabel.Width, 0);
                totalLabel.Location = new Point(startlocation+pageLabel.Width + pageSelectBox.Width,0);
                crumbTable.Controls.Add(cellPanel, 1, dataSet.Count);
            }

            //Hack: sadly the obvious .GetRowHeights() and .getColumnWidths
            //dont give consistantly valid results, as such proper dimentions have
            //to be calculated as the table is formed.
            var rowHeights = (crumbTable.GetRowHeights().Length) * 24;
            var columnWidths = firstColumnSize + lastColumnSize + 25 +largestDescription;
            _tableSize = new Size(columnWidths +5, rowHeights +5);
        }

        /// <summary>
        /// Returns true if all elements have been removed
        /// </summary>
        /// <param name="dataFilter"></param>
        /// <param name="itemNumber"></param>
        /// <returns></returns>
        private bool RemoveItemInContainer(DataFilter dataFilter, int itemNumber)
        {
            if (dataFilter.Cluster != null)
            {
                dataFilter.Cluster.RemoveAt(itemNumber);
                if (dataFilter.Cluster.Count > 0)
                    return false;
            }
            else if (dataFilter.ProteinGroup != null)
            {
                dataFilter.ProteinGroup.RemoveAt(itemNumber);
                if (dataFilter.ProteinGroup.Count > 0)
                    return false;
            }
            else if (dataFilter.Protein != null)
            {
                dataFilter.Protein.RemoveAt(itemNumber);
                if (dataFilter.Protein.Count > 0)
                    return false;
            }
            else if (dataFilter.Peptide != null)
            {
                dataFilter.Peptide.RemoveAt(itemNumber);
                if (dataFilter.Peptide.Count > 0)
                    return false;
            }
            else if (dataFilter.DistinctMatchKey != null)
            {
                dataFilter.DistinctMatchKey.RemoveAt(itemNumber);
                if (dataFilter.DistinctMatchKey.Count > 0)
                    return false;
            }
            else if (dataFilter.SpectrumSourceGroup != null)
            {
                dataFilter.SpectrumSourceGroup.RemoveAt(itemNumber);
                if (dataFilter.SpectrumSourceGroup.Count > 0)
                    return false;
            }
            else if (dataFilter.SpectrumSource != null)
            {
                dataFilter.SpectrumSource.RemoveAt(itemNumber);
                if (dataFilter.SpectrumSource.Count > 0)
                    return false;
            }
            else if (dataFilter.Spectrum != null)
            {
                dataFilter.Spectrum.RemoveAt(itemNumber);
                if (dataFilter.Spectrum.Count > 0)
                    return false;
            }
            else if (dataFilter.Analysis != null)
            {
                dataFilter.Analysis.RemoveAt(itemNumber);
                if (dataFilter.Analysis.Count > 0)
                    return false;
            }
            else if (dataFilter.Charge != null)
            {
                dataFilter.Charge.RemoveAt(itemNumber);
                if (dataFilter.Charge.Count > 0)
                    return false;
            }
            else if (dataFilter.AminoAcidOffset != null)
            {
                dataFilter.AminoAcidOffset.RemoveAt(itemNumber);
                if (dataFilter.AminoAcidOffset.Count > 0)
                    return false;
            }
            else if (dataFilter.ModifiedSite != null || dataFilter.Modifications != null)
            {
                if (itemNumber < 0 && dataFilter.Modifications != null)
                {
                    var targetedMods = (from mod in dataFilter.Modifications
                                        where Math.Round(mod.MonoMassDelta).ToString() == _modMassList[itemNumber]
                                        select mod).Distinct().ToList();
                    foreach (var mod in targetedMods)
                        dataFilter.Modifications.Remove(mod);
                }
                else if (dataFilter.ModifiedSite != null)
                    dataFilter.ModifiedSite.RemoveAt(itemNumber);
                if (dataFilter.Modifications != null && dataFilter.Modifications.Count > 0)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Gets rid of breadcrumbs that are have been updated to avoid duplication
        /// </summary>
        private void RemoveStaleBread()
        {
            var overallData = new DataFilter();

            for (int x = breadCrumbs.Count-1; x >= 0; x--)
            {
                var item = (DataFilter) breadCrumbs[x].Tag;

                if (breadCrumbs[x].Text.Contains("Q-value"))
                    breadCrumbs.RemoveAt(x); //Sometimes bugs and gets extra-long breadcrumb
                else if (item.Cluster != null || item.ProteinGroup != null || item.Protein != null)
                {
                    if (overallData.Cluster != null || overallData.ProteinGroup != null || overallData.Protein != null)
                        breadCrumbs.RemoveAt(x);
                    else
                    {
                        overallData.Cluster = overallData.Cluster ?? item.Cluster;
                        overallData.ProteinGroup = overallData.ProteinGroup ?? item.ProteinGroup;
                        overallData.Protein = overallData.Protein ?? item.Protein;
                    }
                }
                else if (item.PeptideGroup != null || item.Peptide != null || item.DistinctMatchKey != null)
                {
                    if (overallData.PeptideGroup != null || overallData.Peptide != null || overallData.DistinctMatchKey != null)
                        breadCrumbs.RemoveAt(x);
                    else
                    {
                        overallData.PeptideGroup = overallData.PeptideGroup ?? item.PeptideGroup;
                        overallData.Peptide = overallData.Peptide ?? item.Peptide;
                        overallData.DistinctMatchKey = overallData.DistinctMatchKey ?? item.DistinctMatchKey;
                    }
                }
                else if (item.SpectrumSourceGroup != null || item.SpectrumSource != null || item.Spectrum != null)
                {
                    if (overallData.SpectrumSourceGroup != null || overallData.SpectrumSource != null || overallData.Spectrum != null)
                        breadCrumbs.RemoveAt(x);
                    else
                    {
                        overallData.SpectrumSourceGroup = overallData.SpectrumSourceGroup ?? item.SpectrumSourceGroup;
                        overallData.SpectrumSource = overallData.SpectrumSource ?? item.SpectrumSource;
                        overallData.Spectrum = overallData.Spectrum ?? item.Spectrum;
                    }
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
                else if (item.ModifiedSite != null || item.Modifications != null)
                {
                    if (overallData.Modifications != null || overallData.ModifiedSite != null)
                        breadCrumbs.RemoveAt(x);
                    else
                    {
                        overallData.ModifiedSite = overallData.ModifiedSite ?? item.ModifiedSite;
                        overallData.Modifications = overallData.Modifications ?? item.Modifications;
                    }
                }
                //else
                //    breadCrumbs.RemoveAt(x);
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
