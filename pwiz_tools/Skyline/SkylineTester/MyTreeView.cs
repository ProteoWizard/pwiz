
using System.Windows.Forms;

// Fix incorrect handling of double click in TreeView.
namespace SkylineTester
{
    public partial class MyTreeView : TreeView
    {
        protected override void WndProc(ref Message m)
        {
            // Filter WM_LBUTTONDBLCLK
            if (m.Msg != 0x203) base.WndProc(ref m);
        }
    }
}
