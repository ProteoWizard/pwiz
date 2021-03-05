using System;
using System.Collections.Generic;
using System.Drawing;
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
                form.Bounds = GetFloatingRectangleForNewWindow(dockPanel);
                dockableForm.Show(dockPanel, DockState.Floating);
                return;
            }
            form.Show(FormUtil.FindTopLevelOwner(PrimaryForm));
        }

        public static FormGroup FromControl(Control control)
        {
            return new FormGroup(control.FindForm());
        }

        public static Rectangle GetFloatingRectangleForNewWindow(DockPanel dockPanel)
        {
            var rectFloat = dockPanel.Bounds;
            rectFloat = dockPanel.RectangleToScreen(rectFloat);
            rectFloat.X += rectFloat.Width / 4;
            rectFloat.Y += rectFloat.Height / 3;
            rectFloat.Width = Math.Max(600, rectFloat.Width / 2);
            rectFloat.Height = Math.Max(440, rectFloat.Height / 2);
            if (Program.SkylineOffscreen)
            {
                var offscreenPoint = FormEx.GetOffscreenPoint();
                rectFloat.X = offscreenPoint.X;
                rectFloat.Y = offscreenPoint.Y;
            }
            else
            {
                // Make sure it is on the screen.
                var screen = Screen.FromControl(dockPanel);
                var rectScreen = screen.WorkingArea;
                rectFloat.X = Math.Max(rectScreen.X, Math.Min(rectScreen.Width - rectFloat.Width, rectFloat.X));
                rectFloat.Y = Math.Max(rectScreen.Y, Math.Min(rectScreen.Height - rectFloat.Height, rectFloat.Y));
            }
            return rectFloat;
        }

    }
}
