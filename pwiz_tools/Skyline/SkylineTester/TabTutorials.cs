using System;
using System.Collections.Generic;
using System.Text;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void RunTutorials(object sender, EventArgs e)
        {
            if (!ToggleRunButtons(tabTutorials))
                return;

            Tabs.SelectTab(tabOutput);

            var testList = new List<string>();
            GetCheckedTests(TutorialsTree.TopNode, testList);

            var args = new StringBuilder("offscreen=off loop=1 culture=en-US");
            if (TutorialsDemoMode.Checked)
                args.Append(" demo=on");
            else
            {
                int pauseSeconds = -1;
                if (!PauseTutorialsScreenShots.Checked && !int.TryParse(PauseTutorialsSeconds.Text, out pauseSeconds))
                    pauseSeconds = 0;
                args.Append(" pause=");
                args.Append(pauseSeconds);
            }
            args.Append(" test=");
            args.Append(string.Join(",", testList));

            RunTestRunner(args.ToString());
        }
    }
}
