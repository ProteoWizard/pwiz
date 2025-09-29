using System.ComponentModel;
using JetBrains.Annotations;
using System.Resources;

namespace pwiz.Skyline.Model
{
    public class PeptideDocNodeProperties : GlobalizedObject
    {
        protected override ResourceManager GetResourceManager()
        {
            return PropertyGridDocNodeResources.ResourceManager;
        }

        public PeptideDocNodeProperties()
        {
            Property = "Value";
        }

        [Category("Category")] public string Property { get; }

        // Test Support - enforced by code check
        // Invoked via reflection in InspectPropertySheetResources in CodeInspectionTest
        [UsedImplicitly]
        private static ResourceManager ResourceManager() => PropertyGridDocNodeResources.ResourceManager;
    }
}
