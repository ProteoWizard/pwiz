using System;
using System.Windows.Forms;
using pwiz.Skyline.Model.Lib;

namespace pwiz.Skyline.Alerts
{
    public partial class SpectrumLibraryInfoDlg : Form
    {
        public SpectrumLibraryInfoDlg(LibraryDetails libraryDetails)
        {
            InitializeComponent();

            SetLibraryLinks(libraryDetails);

            int height = labelLibInfo.Height;
            SetDetailsText(libraryDetails);
            Height += Math.Max(0, labelLibInfo.Height - height*3);
        }

        private void SetLibraryLinks(LibraryDetails libraryDetails)
        {
            linkSpecLibLinks.Text = "";

            if(libraryDetails.LibLinks.Count > 0)
            {
                string labelStr = libraryDetails.LibLinks.Count == 1 ? "Library source: " : "Library sources: ";

                foreach(LibraryLink link in libraryDetails.LibLinks)
                {
                    linkSpecLibLinks.Links.Add(labelStr.Length, link.Name.Length, link.Link);
                    labelStr += link.Name + "  ";
                }
                
                linkSpecLibLinks.Text = labelStr;
            }
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

            if (libraryDetails.DataFileCount > 0)
            {
                detailsText += string.Format(("Data files: {0}\n"), libraryDetails.DataFileCount);
            }

            detailsText += string.Format(("Unique peptides: {0}\n"), libraryDetails.PeptideCount.ToString(numFormat));


            if (libraryDetails.TotalPsmCount > 0)
            {
                detailsText += string.Format(("Matched spectra: {0}\n"), libraryDetails.TotalPsmCount.ToString(numFormat));
            }

            labelLibInfo.Text = detailsText;
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Determine which link was clicked within the LinkLabel.
            linkSpecLibLinks.Links[linkSpecLibLinks.Links.IndexOf(e.Link)].Visited = true;

            // Display the appropriate link based on the value of the 
            // LinkData property of the Link object.
            string target = e.Link.LinkData as string;

            System.Diagnostics.Process.Start(target);
        }
    }
}
