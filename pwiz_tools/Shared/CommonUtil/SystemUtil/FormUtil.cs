/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;
using System.Windows.Forms;
using pwiz.Common.Collections;

namespace pwiz.Common.SystemUtil
{
    public static class FormUtil
    {
        /// <summary>
        /// Shows a dialog box.
        /// If the owner of the dialog is a popup window, then this method uses <see cref="FormUtil.FindTopLevelOwner"/> 
        /// to find the appropriate main window to own the dialog, and after the dialog is closed, sets the focus back 
        /// to the correct control.
        /// </summary>
        public static DialogResult ShowDialog(Control owner, Form dialog)
        {
            Form ownerForm = null;
            if (null != owner)
            {
                ownerForm = owner.FindForm();
            }
            var topLevelOwner = FindTopLevelOwner(owner);
            Control activeControl = null;
            if (null != ownerForm && ownerForm.ContainsFocus)
            {
                activeControl = ownerForm.ActiveControl;
            }
            var dialogResult = dialog.ShowDialog(topLevelOwner);
            if (null != activeControl)
            {
                if (ownerForm != topLevelOwner)
                {
                    // Put the focus first on the window which was the owner of the dialog box.
                    // Otherwise when the ownerForm is closed, the focus will go to a different application
                    topLevelOwner.Focus();
                    // Then put the focus on the control which had the focus before the dialog came up
                    activeControl.Focus();
                }
            }
            return dialogResult;
        }

        /// <summary>
        /// Moves the control's X and Y coordinates according to the X and Y values.
        /// </summary>
        public static void Offset(this Control control, int x = 0, int y = 0)
        {
            var loc = control.Location;
            loc.Offset(x, y);
            control.Location = loc;
        }

        /// <summary>
        /// Returns a point with its X and Y coordinates offset according to the given X and Y values.
        /// </summary>
        public static System.Drawing.Point Offset(this System.Drawing.Point point, int x = 0, int y = 0)
        {
            point.Offset(x, y);
            return point;
        }

        /// <summary>
        /// Finds the top level form which is suitable to pass to <see cref="Form.ShowDialog(IWin32Window)"/>.
        /// This function looks for a form for which ShowInTaskBar is true.  When dialogs are shown that are owned
        /// by a popup form which is not ShowInTaskBar, it often prevents the user from Alt-Tabbing back to 
        /// the application.
        /// </summary>
        public static Control FindTopLevelOwner(Control control)
        {
            if (null == control)
            {
                return null;
            }
            var topLevelForm = control.TopLevelControl as Form;
            if (null == topLevelForm)
            {
                return control;
            }
            if (IsSuitableDialogOwner(topLevelForm))
            {
                return topLevelForm;
            }
            for (var formOwner = topLevelForm.Owner; null != formOwner; formOwner = formOwner.Owner)
            {
                if (IsSuitableDialogOwner(formOwner))
                {
                    return formOwner;
                }
            }
            return topLevelForm;
        }

        public static bool IsSuitableDialogOwner(Form form)
        {
            return form.ShowInTaskbar || form.Modal;
        }

        public static Form FindTopLevelOpenForm(Func<Form, bool> skipForm = null)
        {
            Form[] openForms = OpenForms;
            for (int i = openForms.Length - 1; i >= 0; i--)
            {
                Form form = openForms[i];
                if (skipForm != null && skipForm(form))
                    continue;
                if (form.IsDisposed)
                    continue;
                return form;
            }
            // Should never happen
            return null;
        }

        public static T FindParentOfType<T>(Control control) where T : class
        {
            while (control != null && !(control is T))
                control = control.Parent;
            return control as T;
        }

        /// <summary>
        /// Returns all open forms in the application.
        /// Thread-safe version of <see cref="Application.OpenForms"/>.
        /// </summary>
        public static Form[] OpenForms
        {
            get
            {
                while (true)
                {
                    try
                    {
                        return Application.OpenForms.OfType<Form>().ToArray();
                    }
                    catch (InvalidOperationException)
                    {
                        // Collection was modified. Try again.
                    }
                }
            }
        }
        /// <summary>
        /// Set the tooltips for the control and all of its children to null.
        /// The ToolTip control sometimes gets confused if any of the tooltips belong to
        /// controls that are no longer part of the form.
        /// (ToolTip.TopLevelControl sometimes gets set to a bogus value)
        /// </summary>
        public static void PurgeTooltips(IList<ToolTip> toolTipControls, Control control)
        {
            foreach (var toolTipControl in toolTipControls)
            {
                toolTipControl.SetToolTip(control, null);
            }

            foreach (var child in control.Controls.OfType<Control>())
            {
                PurgeTooltips(toolTipControls, child);
            }
        }

        /// <summary>
        /// Removes the tab page, and nulls out the tooltips that may have been set.
        /// </summary>
        public static void RemoveTabPage(TabPage tabPage, IList<ToolTip> toolTipControls)
        {
            ((TabControl) tabPage.Parent).TabPages.Remove(tabPage);
            if (toolTipControls != null && toolTipControls.Count > 0)
            {
                PurgeTooltips(toolTipControls, tabPage);
            }
        }

        public static void RemoveTabPage(TabPage tabPage, ToolTip toolTipControl)
        {
            RemoveTabPage(tabPage, ImmutableList.Singleton(toolTipControl));
        }

        public static Control GetFocus(this Control control)
        {
            if (control.Focused)
                return control;
            return (
                from Control childControl in control.Controls
                select GetFocus(childControl)).FirstOrDefault(focus => focus != null);
        }

        /// <summary>
        /// Creates a folder browser showing the classic "Browse For Folder" tree on every framework we build for.
        /// .NET Framework only ever shows that dialog, while .NET 8 defaults AutoUpgradeEnabled to true and shows
        /// the newer IFileDialog folder picker instead - so without this the same build would present a different
        /// dialog depending on which runtime it ran under. The newer picker also ignores BFFM_SETSELECTION, which
        /// is how the classic tree is driven, so it silently returns the folder it opened on rather than the one
        /// it was asked for (see NativeFolderBrowserDialog).
        ///
        /// TODO: revisit. The newer picker is the better dialog - an address bar, a path you can paste into, the
        /// places bar - and adopting it is a deliberate UI change plus a rewrite of the folder-browser automation,
        /// not something to inherit silently from a framework upgrade.
        /// </summary>
        public static FolderBrowserDialog CreateFolderBrowserDialog()
        {
            var dlg = new FolderBrowserDialog();
            dlg.AutoUpgradeEnabled = false;
            return dlg;
        }
    }
}
