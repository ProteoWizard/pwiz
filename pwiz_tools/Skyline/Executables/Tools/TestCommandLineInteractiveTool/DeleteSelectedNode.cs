using SkylineTool;

namespace TestCommandLineInteractiveTool
{
    public class DeleteSelectedNode : AbstractCommand
    {
        public DeleteSelectedNode(SkylineToolClient skylineToolClient) : base(skylineToolClient)
        {
        }

        public override void RunCommand()
        {
            string locator = SkylineToolClient.GetSelectedElementLocator("Transition") ??
                             SkylineToolClient.GetSelectedElementLocator("Precursor") ??
                             SkylineToolClient.GetSelectedElementLocator("Molecule") ??
                             SkylineToolClient.GetSelectedElementLocator("MoleculeGroup");
            if (locator != null)
            {
                SkylineToolClient.DeleteElements(new []{locator});
            }
        }
    }
}
