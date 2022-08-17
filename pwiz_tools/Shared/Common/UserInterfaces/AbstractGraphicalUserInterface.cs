using System.Windows.Forms;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.UserInterfaces
{
    public abstract class AbstractGraphicalUserInterface : AbstractUserInterface
    {
        public AbstractGraphicalUserInterface(Control parent)
        {
            Parent = parent;
        }

        private Control Parent { get; }

        public override void DisplayMessage(Control owner, string message)
        {
            MessageBox.Show(GetParentControl(owner), message);
        }

        public Control GetParentControl(Control owner)
        {
            return FormUtil.FindTopLevelOwner(owner ?? Parent);
        }
    }
}
