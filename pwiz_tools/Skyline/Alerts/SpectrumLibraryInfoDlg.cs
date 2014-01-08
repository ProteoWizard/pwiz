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
using System.IO;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Model.Lib;
using System.Linq;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Alerts
{
    public partial class SpectrumLibraryInfoDlg : FormEx
    {
        public SpectrumLibraryInfoDlg(LibraryDetails libraryDetails)
        {
            InitializeComponent();

            Icon = Resources.Skyline;

            // library details
            SetDetailsText(libraryDetails);

            // links to library source(s)
            SetLibraryLinks(libraryDetails);

            // list of data files, if available
            if(libraryDetails.DataFiles.Any())
            {
                SetDataFileList(libraryDetails);
                textBoxDataFiles.Show();
                 
                // TODO:  This makes no sense.  This always adds 100 pixels to the form height.
                Height += Math.Max(0, 100);  
            } 
        }

        private void SetLibraryLinks(LibraryDetails libraryDetails)
        {
            linkSpecLibLinks.Text = string.Empty;

            if(libraryDetails.LibLinks.Any())
            {
                string labelStr = libraryDetails.LibLinks.Count() == 1
                                      ? Resources.SpectrumLibraryInfoDlg_SetLibraryLinks_Library_source
                                      : Resources.SpectrumLibraryInfoDlg_SetLibraryLinks_Library_sources;

                foreach(LibraryLink link in libraryDetails.LibLinks)
                {
                    labelStr += TextUtil.SEPARATOR_SPACE;
                    linkSpecLibLinks.Links.Add(labelStr.Length, link.Name.Length, link.Link);
                    labelStr += link.Name + "  "; // Not L10N
                }
                
                linkSpecLibLinks.Text = labelStr;
            }
        }

        private void SetDataFileList(LibraryDetails libraryDetails)
        {
            if(!libraryDetails.DataFiles.Any())
                return;

            var fileList = new StringBuilder();
            foreach (var filename in libraryDetails.DataFiles)
            {
                fileList.AppendLine(Path.GetFileName(filename));
            }
            textBoxDataFiles.Text = fileList.ToString();
        }

        private void SetDetailsText(LibraryDetails libraryDetails)
        {
            const string numFormat = "#,0"; // Not L10N

            var detailsText = new StringBuilder();

            detailsText.AppendLine(string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText__0__library, libraryDetails.Format));
            if(!string.IsNullOrEmpty(libraryDetails.Id))
            {
                detailsText.AppendLine(string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText_ID__0__, libraryDetails.Id));
            }
            if (!string.IsNullOrEmpty(libraryDetails.Revision))
            {
                detailsText.AppendLine(string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText_Revision__0__, libraryDetails.Revision));
            }
            if (!string.IsNullOrEmpty(libraryDetails.Version))
            {
                detailsText.AppendLine(string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText_Version__0__, libraryDetails.Version));
            }
            detailsText.AppendLine(string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText_Unique_peptides__0__,
                                                 libraryDetails.PeptideCount.ToString(numFormat)));


            if (libraryDetails.TotalPsmCount > 0)
            {
                detailsText.AppendLine(
                    string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText_Matched_spectra__0__,
                                  libraryDetails.TotalPsmCount.ToString(numFormat)));
            }
            if (libraryDetails.DataFiles.Any())
            {
                detailsText.AppendLine(string.Format(Resources.SpectrumLibraryInfoDlg_SetDetailsText_Data_files___0_,
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
    }
}
