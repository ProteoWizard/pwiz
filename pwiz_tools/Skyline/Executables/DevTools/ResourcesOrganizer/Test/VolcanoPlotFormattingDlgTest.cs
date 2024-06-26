using ResourcesOrganizer.ResourcesModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Test
{
    [TestClass]
    public class VolcanoPlotFormattingDlgTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestVolcanoPlotFormattingDlg()
        {
            string runDirectory = TestContext.TestRunDirectory!;
            SaveManifestResources(typeof(VolcanoPlotFormattingDlgTest), runDirectory);
            var oldResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v23.VolcanoPlotFormattingDlg.resx"));
            var oldDatabase = ResourcesDatabase.Empty;
            oldDatabase = oldDatabase with { ResourcesFiles = oldDatabase.ResourcesFiles.SetItem("VolcanoPlotFormattingDlg.resx", oldResourcesFile) };
            var newResourcesFile = ResourcesFile.Read(Path.Combine(runDirectory, "v24.VolcanoPlotFormattingDlg.resx"));
            var newDatabase = ResourcesDatabase.Empty;
            newDatabase = newDatabase with { ResourcesFiles = newDatabase.ResourcesFiles.SetItem("VolcanoPlotFormattingDlg.resx", newResourcesFile) };
            var withImportedTranslations = newDatabase.ImportTranslations(oldDatabase, Languages);

        }
    }
}
