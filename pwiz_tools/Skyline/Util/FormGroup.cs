/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Represents a group of forms which all have the same DockPanel, or whose DockPanel is null.
    /// </summary>
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

                return FormUtil.OpenForms.Where(form=>(form as DockableForm)?.DockPanel == null);
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
