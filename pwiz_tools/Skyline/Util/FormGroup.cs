using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    public class FormGroup
    {
        public FormGroup(Form primaryForm)
        {
            PrimaryForm = primaryForm;
        }
        public Form PrimaryForm { get; private set; }

        public DockPanel DockPanel
        {
            get
            {
                return (PrimaryForm as DockableForm)?.DockPanel;
            }
        }

        public IEnumerable<Form> SiblingForms
        {
            get
            {
                var dockPanel = DockPanel;
                if (dockPanel != null)
                {
                    return dockPanel.Contents.OfType<Form>();
                }

                return FormUtil.OpenForms;
            }
        }

        public void ShowSibling(Form form)
        {
            var dockPanel = DockPanel;
            if (dockPanel != null && form is DockableForm dockableForm)
            {
                dockableForm.Show(dockPanel, DockState.Floating);
                return;
            }
            form.Show(FormUtil.FindTopLevelOwner(PrimaryForm));
        }

        public static FormGroup FromControl(Control control)
        {
            return new FormGroup(control.FindForm());
        }
    }
}
