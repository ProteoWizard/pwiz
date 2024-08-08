/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Model.Lib;
using System.Linq;
using pwiz.BiblioSpec;
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.Common.DataBinding;

namespace pwiz.Skyline.Alerts
{
    public partial class SpectrumLibraryInfoDlg : FormEx
    {
        public SpectrumLibraryInfoDlg(LibraryDetails libraryDetails)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            libraryGridView.AutoGenerateColumns = false;
            libraryGridView.DataSource = new SortableBindingList<Row>();

            cutoffScoreCol.DefaultCellStyle.NullValue = TextUtil.EXCEL_NA;
            scoreTypeCol.DefaultCellStyle.NullValue = TextUtil.EXCEL_NA;

            // library details
            SetDetailsText(libraryDetails);

            // links to library source(s)
            SetLibraryLinks(libraryDetails);

            // Data grid view
            var dataGridViewHeight = 0;
            if (libraryDetails.DataFiles.Any())
            {
                PopulateScoreGrid(libraryDetails);

                const int maxDisplayRows = 10;
                dataGridViewHeight = Math.Min(libraryGridView.Rows.Count, maxDisplayRows) * libraryGridView.Rows[0].Height +
                                    libraryGridView.ColumnHeadersHeight + libraryGridView.Rows[0].Height/2;
            }
            else
            {
                libraryGridView.Hide();
            }

            Height = labelLibInfo.Height + linkSpecLibLinks.Height + dataGridViewHeight + btnOk.Height + 70;
        }

        private BindingList<Row> Rows => libraryGridView.DataSource as BindingList<Row>;

        private void PopulateScoreGrid(LibraryDetails libraryDetails)
        {
            // Populates DataGridView with files and their given scores
            foreach (var file in libraryDetails.DataFiles)
            {
                if (file.ScoreThresholds.Any())
                {
                    Rows.AddRange(file.ScoreThresholds.Select(threshold => new Row(file, threshold.Key)));
                }
                else
                {
                    Rows.Add(new Row(file));
                }
            }
        }

        private void SetLibraryLinks(LibraryDetails libraryDetails)
        {
            linkSpecLibLinks.Text = string.Empty;

            if(libraryDetails.LibLinks.Any())
            {
                string labelStr = libraryDetails.LibLinks.Count() == 1
                                      ? AlertsResources.SpectrumLibraryInfoDlg_SetLibraryLinks_Library_source
                                      : AlertsResources.SpectrumLibraryInfoDlg_SetLibraryLinks_Library_sources;

                foreach(LibraryLink link in libraryDetails.LibLinks)
                {
                    labelStr += TextUtil.SEPARATOR_SPACE;
                    linkSpecLibLinks.Links.Add(labelStr.Length, link.Name.Length, link.Link);
                    labelStr += link.Name + @"  ";
                }
                
                linkSpecLibLinks.Text = labelStr;
            }
        }

        private void SetDetailsText(LibraryDetails libraryDetails)
        {
            const string numFormat = "#,0";

            var detailsText = new StringBuilder();

            detailsText.AppendLine(string.Format(AlertsResources.SpectrumLibraryInfoDlg_SetDetailsText__0__library, libraryDetails.Format));
            if(!string.IsNullOrEmpty(libraryDetails.Id))
            {
                detailsText.AppendLine(string.Format(AlertsResources.SpectrumLibraryInfoDlg_SetDetailsText_ID__0__, libraryDetails.Id));
            }
            if (!string.IsNullOrEmpty(libraryDetails.Revision))
            {
                detailsText.AppendLine(string.Format(AlertsResources.SpectrumLibraryInfoDlg_SetDetailsText_Revision__0__, libraryDetails.Revision));
            }
            if (!string.IsNullOrEmpty(libraryDetails.Version))
            {
                detailsText.AppendLine(string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText_Version__0__, libraryDetails.Version));
            }
            if (libraryDetails.UniquePeptideCount > 0)
            {
                detailsText.AppendLine(string.Format(AlertsResources.SpectrumLibraryInfoDlg_SetDetailsText_Unique_peptides__0__, libraryDetails.UniquePeptideCount.ToString(numFormat)));
            }
            detailsText.AppendLine(string.Format(AlertsResources.SpectrumLibraryInfoDlg_SetDetailsText_Unique_Precursors___0_,
                                                 libraryDetails.SpectrumCount.ToString(numFormat)));

            if (libraryDetails.TotalPsmCount > 0)
            {
                detailsText.AppendLine(
                    string.Format(AlertsResources.SpectrumLibraryInfoDlg_SetDetailsText_Matched_spectra__0__,
                                  libraryDetails.TotalPsmCount.ToString(numFormat)));
            }
            if (libraryDetails.DataFiles.Any())
            {
                detailsText.AppendLine(string.Format(AlertsResources.SpectrumLibraryInfoDlg_SetDetailsText_Data_files___0_,
                                                     libraryDetails.DataFiles.Count()));
            }
            labelLibInfo.Text = detailsText.ToString();
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Determine which link was clicked within the LinkLabel.
            linkSpecLibLinks.Links[linkSpecLibLinks.Links.IndexOf(e.Link)].Visited = true;

            // Display the appropriate link based on the value of the 
            // LinkData property of the Link object.
            string target = e.Link.LinkData.ToString();

            WebHelpers.OpenLink(this, target);
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        public void OkDialog()
        {
            DialogResult = DialogResult.OK;
        }

        // Currently only used by test
        public IEnumerable<SpectrumSourceFileDetails> SpectrumSourceFileDetails => Rows.Select(row => row.FileDetails);

        private void libraryGridView_CellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex >= 0 && e.ColumnIndex == cutoffScoreCol.Index)
            {
                var row = libraryGridView.Rows[e.RowIndex];
                row.Cells[e.ColumnIndex].ToolTipText = ((Row)row.DataBoundItem).ScoreType?.ThresholdDescription;
            }
        }

        public class Row
        {
            public SpectrumSourceFileDetails FileDetails { get; }
            // ReSharper disable once MemberCanBePrivate.Local
            public ScoreType ScoreType { get; }

            public Row(SpectrumSourceFileDetails fileDetails, ScoreType scoreType = null)
            {
                FileDetails = fileDetails;
                ScoreType = scoreType;
            }

            // Properties for GridView
            // ReSharper disable UnusedMember.Local
            public string FileName => Path.GetFileName(FileDetails.FilePath);
            public double? ScoreThreshold => ScoreType != null && FileDetails.ScoreThresholds.TryGetValue(ScoreType, out var threshold) ? threshold : null;
            public int SpectrumCount => FileDetails.BestSpectrum;
            public int MatchedCount => FileDetails.MatchedSpectrum;
            // ReSharper restore UnusedMember.Local
        }
    }
}
