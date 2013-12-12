using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void RunTests(object sender, EventArgs e)
        {
            if (!ToggleRunButtons(tabTest))
                return;

            Tabs.SelectTab(tabOutput);

            var args = new StringBuilder();

            args.Append("offscreen=");
            args.Append(Offscreen.Checked);

            if (!RunIndefinitely.Checked)
            {
                int loop;
                if (!int.TryParse(RunLoopsCount.Text, out loop))
                    loop = 1;
                args.Append(" loop=");
                args.Append(loop);
            }

            var cultures = new List<CultureInfo>();
            if (CultureEnglish.Checked || !CultureFrench.Checked)
                cultures.Add(new CultureInfo("en-US"));
            if (CultureFrench.Checked)
                cultures.Add(new CultureInfo("fr-FR"));

            args.Append(" culture=");
            args.Append(string.Join(",", cultures));
            if (PauseTestsScreenShots.Checked)
                args.Append(" pause=-1");

            var testList = new List<string>();
            foreach (TreeNode node in TestsTree.Nodes)
                GetCheckedTests(node, testList, SkipCheckedTests.Checked);
            args.Append(" test=");
            args.Append(string.Join(",", testList));

            RunTestRunner(args.ToString());
        }

        private void checkAll_Click(object sender, EventArgs e)
        {
            foreach (var node in TestsTree.Nodes)
            {
                ((TreeNode)node).Checked = true;
                CheckAllChildNodes((TreeNode)node, true);
            }
        }

        private void uncheckAll_Click(object sender, EventArgs e)
        {
            foreach (var node in TestsTree.Nodes)
            {
                ((TreeNode)node).Checked = false;
                CheckAllChildNodes((TreeNode)node, false);
            }
        }

        private void pauseTestsForScreenShots_CheckedChanged(object sender, EventArgs e)
        {
            if (PauseTestsScreenShots.Checked)
                Offscreen.Checked = false;
        }

        private void offscreen_CheckedChanged(object sender, EventArgs e)
        {
            if (Offscreen.Checked)
                PauseTestsScreenShots.Checked = false;
        }

    }
}
