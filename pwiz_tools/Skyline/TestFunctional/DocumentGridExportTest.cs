using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DocumentGridExportTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDocumentGridExport()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>
            {
                SkylineWindow.Paste("ELVISK");
                SkylineWindow.ShowDocumentGrid(true);
            });
            var documentGrid = FindOpenForm<DocumentGridForm>();
            RunUI(() =>
            {
                documentGrid.DataboundGridControl.ChooseView(
                    ViewGroup.BUILT_IN.Id.ViewName(Resources.SkylineViewContext_GetDocumentGridRowSources_Precursors));
            });
            WaitForCondition(() => documentGrid.IsComplete);
            const string chargeFormat = "0.#%";
            RunDlg<ChooseFormatDlg>(() => documentGrid.DataboundGridControl.ShowFormatDialog(
                documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Precursor.Charge)))), chooseFormatDlg =>
            {
                chooseFormatDlg.FormatText = chargeFormat;
                chooseFormatDlg.DialogResult = DialogResult.OK;
            });
            const string precursorMzFormat = "$0.00";
            RunDlg<ChooseFormatDlg>(() => documentGrid.DataboundGridControl.ShowFormatDialog(
                documentGrid.FindColumn(PropertyPath.Root.Property(nameof(Precursor.Mz)))), chooseFormatDlg =>
            {
                chooseFormatDlg.DialogResult = DialogResult.OK;
                chooseFormatDlg.FormatText = precursorMzFormat;
            });
            RunUI(() =>
            {
                documentGrid.NavBar.ViewContext.CopyAll(documentGrid.NavBar, documentGrid.BindingListSource);
            });
            
            var clipboardText = ClipboardEx.GetText();
            var tsvReader = new DsvFileReader(new StringReader(clipboardText), '\t');
            Assert.IsNotNull(tsvReader.ReadLine());
            var firstPrecursor = SkylineWindow.Document.Molecules.First().TransitionGroups.First();
            Assert.AreEqual(firstPrecursor.PrecursorCharge.ToString(chargeFormat, CultureInfo.CurrentCulture), tsvReader.GetFieldByName(ColumnCaptions.PrecursorCharge));
            Assert.AreEqual(firstPrecursor.PrecursorMz.ToString(precursorMzFormat, CultureInfo.CurrentCulture), tsvReader.GetFieldByName(ColumnCaptions.PrecursorMz));
            var csvFilePath = TestContext.GetTestPath("DocumentGridExportTest.csv");
            RunUI(()=>documentGrid.NavBar.ViewContext.ExportToFile(documentGrid.NavBar, documentGrid.BindingListSource, csvFilePath, TextUtil.CsvSeparator));
            var csvReader = new CsvFileReader(csvFilePath);
            Assert.IsNotNull(csvReader.ReadLine());
            Assert.AreEqual(firstPrecursor.PrecursorCharge.ToString(chargeFormat, CultureInfo.CurrentCulture), csvReader.GetFieldByName(ColumnCaptions.PrecursorCharge));
            Assert.AreEqual(firstPrecursor.PrecursorMz.ToString(precursorMzFormat, CultureInfo.CurrentCulture), csvReader.GetFieldByName(ColumnCaptions.PrecursorMz));

        }
    }
}
