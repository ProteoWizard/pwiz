using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DigitalRune.Windows.Docking;

namespace pwiz.Topograph.ui.Controls
{
    /// <summary>
    /// Subclass of DockPanel which works around a memory leak.
    /// There is a ToolTip in this object that does not get cleaned up properly.
    /// To prevent this from causing all of the contained windows to be leaked,
    /// this class makes a new DockableForm the active window.
    /// </summary>
    public class DockPanelEx : DockPanel
    {
        protected override void OnHandleDestroyed(EventArgs e)
        {
            base.OnHandleDestroyed(e);
            foreach (var doc in DocumentsToArray())
            {
                var form = doc as DockableForm;
                if (form != null && form.Pane != null)
                {
                    new DockableForm { Name = "MemoryLeakWorkaround"}.Show(form.Pane, null);
                }
            }
        }
    }
}
