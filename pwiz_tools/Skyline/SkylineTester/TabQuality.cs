using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SkylineTester
{
    public partial class SkylineTesterWindow
    {
        private void RunQuality(object sender, EventArgs e)
        {
            if (!ToggleRunButtons(tabQuality))
                return;
            Tabs.SelectTab(tabOutput);
        }
    }
}
