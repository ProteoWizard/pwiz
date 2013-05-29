using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;
using NHibernate;
using pwiz.CLI.analysis;

namespace IDPicker.Forms
{
    public partial class ExportLibrarySettings : Form
    {
        public ExportLibrarySettings()
        {
            InitializeComponent();
        }

        private void ExportLibrarySettings_Load(object sender, EventArgs e)
        {
            methodBox.Text = "Dot Product Compare";
            outputFormatBox.Text = ".sptxt";
        }

        public ExportForm.LibraryExportOptions GetSettings()
        {
            return new ExportForm.LibraryExportOptions
                                     {
                                         precursorMzTolerance = (double) Math.Round(PrecursorNumBox.Value*2)/2,
                                         fragmentMzTolerance = (double) Math.Round(FragmentNumBox.Value*2)/2,
                                         minimumSpectra = (int) Math.Round(SpectrumNumBox.Value),
                                         method = methodBox.Text,
                                         outputFormat = outputFormatBox.Text,
                                         decoys = decoysBox.Checked,
                                         crossPeptide = crossBox.Checked
                                     };
        }
    }
}
