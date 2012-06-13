using System;
using System.Text;
using System.Windows.Forms;
using pwiz.Skyline.Model.Lib;
using System.Linq;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

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

                Height += Math.Max(0, 100);  
            } 
        }

        private void SetLibraryLinks(LibraryDetails libraryDetails)
        {
            linkSpecLibLinks.Text = "";

            if(libraryDetails.LibLinks.Any())
            {
                string labelStr = libraryDetails.LibLinks.Count() == 1 ? "Library source: " : "Library sources: ";

                foreach(LibraryLink link in libraryDetails.LibLinks)
                {
                    linkSpecLibLinks.Links.Add(labelStr.Length, link.Name.Length, link.Link);
                    labelStr += link.Name + "  ";
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
                fileList.AppendLine(filename);
            }
            textBoxDataFiles.Text = fileList.ToString();
        }

        private void SetDetailsText(LibraryDetails libraryDetails)
        {
            const string numFormat = "#,0";

            string detailsText = "";

            detailsText += string.Format("{0} library\n", libraryDetails.Format);
            if(!string.IsNullOrEmpty(libraryDetails.Id))
            {
                detailsText += string.Format(("ID: {0}\n"), libraryDetails.Id);
            }
            if (!string.IsNullOrEmpty(libraryDetails.Revision))
            {
                detailsText += string.Format(("Revision: {0}\n"), libraryDetails.Revision);
            }
            if (!string.IsNullOrEmpty(libraryDetails.Version))
            {
                detailsText += string.Format(("Version: {0}\n"), libraryDetails.Version);
            }
            detailsText += string.Format(("Unique peptides: {0}\n"), libraryDetails.PeptideCount.ToString(numFormat));


            if (libraryDetails.TotalPsmCount > 0)
            {
                detailsText += string.Format(("Matched spectra: {0}\n"), libraryDetails.TotalPsmCount.ToString(numFormat));
            }
            if (libraryDetails.DataFiles.Any())
            {
                detailsText += string.Format(("Data files: {0}\n"), libraryDetails.DataFiles.Count());
            }
            labelLibInfo.Text = detailsText;
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
