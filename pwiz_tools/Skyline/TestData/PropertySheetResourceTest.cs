using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Files;
using pwiz.Skyline.Model.PropertySheets;

namespace pwiz.SkylineTestData
{
    [TestClass]
    public class PropertySheetResourceTest
    {
        [TestMethod]
        public void TestFileNodePropertySheetResources()
        {
            // for all FileNode-derived classes, make a DynamicPropertyObject and call AddPropertiesFromAnnotatedObject on a new instance of the class
            var fileNodeTypes = new[]
            {
                typeof(BackgroundProteome),
                typeof(BackgroundProteomeFolder),
                typeof(IonMobilityLibrary),
                typeof(IonMobilityLibraryFolder),
                typeof(OptimizationLibrary),
                typeof(OptimizationLibraryFolder),
                typeof(RTCalc),
                typeof(RTCalcFolder),
                typeof(Replicate),
                typeof(ReplicateSampleFile),
                typeof(ReplicatesFolder),
                typeof(SkylineAuditLog),
                typeof(SkylineChromatogramCache),
                typeof(SkylineFile),
                typeof(SkylineViewFile),
                typeof(SpectralLibrary),
                typeof(SpectralLibrariesFolder),
            };

            foreach (var fileNodeType in fileNodeTypes)
            {
                var fileNode = Activator.CreateInstance(fileNodeType, new object[] { null });
                var dynamicPropertyObject = new DynamicPropertyObject();
                try
                {
                    dynamicPropertyObject.AddPropertiesFromAnnotatedObject(fileNode, null);//PropertySheetResources.ResourceManager);
                }
                catch (InvalidOperationException e)
                {
                    Assert.Fail(e.Message);
                }
            }
        }
    }
}
