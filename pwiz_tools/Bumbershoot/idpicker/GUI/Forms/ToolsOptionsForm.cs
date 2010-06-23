//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Configuration;
using System.IO;

using IdPickerGui.BLL;

namespace IdPickerGui
{
    public partial class ToolsOptionsForm : Form
    {
        /// <summary>
        /// Default constructor
        /// </summary>
        public ToolsOptionsForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Log Exceptions (and inner exceptions) to file. Show ExceptionsDialogForm.
        /// </summary>
        /// <param name="exc"></param>
        private void HandleExceptions(Exception exc)
        {
            ExceptionsDialogForm excForm = new ExceptionsDialogForm();
            StringBuilder sbDetails = new StringBuilder();

            try
            {
                ExceptionManager.logExceptionsByFormToFile(this, exc, DateTime.Now);

                Exception subExc = exc.InnerException;
                sbDetails.Append(exc.Message);

                while (subExc != null)
                {
                    sbDetails.Append(subExc.Message + "\r\n");
                    subExc = subExc.InnerException;
                }

                excForm.Details = sbDetails.ToString();
                excForm.Msg = "An error has occurred in the application.\r\n\r\n";
                excForm.loadForm(ExceptionsDialogForm.ExceptionType.Error);

                excForm.ShowDialog(this);
            }
            catch
            {
                throw exc;
            }
        }

        /// <summary>
        /// Open FolderBrowserDialog and return dir result when selected
        /// </summary>
        /// <param name="sDefaultDir">Default highlited dir</param>
        /// <returns>Selected dir</returns>
        private string openBrowseDialog(string sPrevDir, Boolean newFolderOption)
        {
            try
            {
                FolderBrowserDialog dlgBrowseSource = new FolderBrowserDialog();

                if (!sPrevDir.Equals(string.Empty))
                {
                    dlgBrowseSource.SelectedPath = sPrevDir;
                }
                else
                {
                    dlgBrowseSource.SelectedPath = "c:\\";
                }

                dlgBrowseSource.ShowNewFolderButton = newFolderOption;

                DialogResult result = dlgBrowseSource.ShowDialog();

                if (result == DialogResult.OK)
                {
                    return dlgBrowseSource.SelectedPath;
                }

                return string.Empty;

            }
            catch (Exception exc)
            {
                throw new Exception("Error opening file dialog\r\n", exc);
            }

        }

        /// <summary>
        /// Save fields on form to properties
        /// </summary>
        private void saveFormDefaults()
        {
            // output location of user.config file
            //Configuration config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            //MessageBox.Show(config.FilePath, "IDPicker", MessageBoxButtons.OK, MessageBoxIcon.Information);

            try
            {
                if (!tbResultsDir.Text.Equals(string.Empty) && Directory.Exists(tbResultsDir.Text))
                {
                    Properties.Settings.Default.ResultsDir = tbResultsDir.Text;
                }
                else
                {
                    Properties.Settings.Default.ResultsDir = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\My IDPicker Reports";
                }

                IDPicker.Properties.Settings.Default.DecoyPrefix = tbDecoyPrefix.Text;
                IDPicker.Properties.Settings.Default.SourceExtensions = tbSourceExtensions.Text;

                IDPicker.Properties.Settings.Default.FastaPaths.Clear();

                foreach (string s in lbFastaPaths.Items)
                {
                    IDPicker.Properties.Settings.Default.FastaPaths.Add(s);
                }

                IDPicker.Properties.Settings.Default.SourcePaths.Clear();

                foreach (string s in lbSourcePaths.Items)
                {
                    IDPicker.Properties.Settings.Default.SourcePaths.Add(s);
                }

                IDPicker.Properties.Settings.Default.SearchPaths.Clear();

                foreach (string s in lbSearchPaths.Items)
                {
                    IDPicker.Properties.Settings.Default.SearchPaths.Add(s);
                }

                IDPicker.Properties.Settings.Default.Save();


            }
            catch (Exception exc)
            {
                throw new Exception("Error saving values from ToolsOptionsForm\r\n", exc);
            }

        }

        /// <summary>
        /// Load values to form from properties
        /// </summary>
        private void loadDefaults()
        {
            try
            {
                string resultsDir = Properties.Settings.Default.ResultsDir;

                tbResultsDir.Text = resultsDir;
                tbDecoyPrefix.Text = IDPicker.Properties.Settings.Default.DecoyPrefix;
                tbSourceExtensions.Text = IDPicker.Properties.Settings.Default.SourceExtensions;

                foreach (string s in IDPicker.Properties.Settings.Default.FastaPaths)
                {
                    lbFastaPaths.Items.Add(s);
                }

                foreach (string s in IDPicker.Properties.Settings.Default.SourcePaths)
                {
                    lbSourcePaths.Items.Add(s);
                }

                foreach (string s in IDPicker.Properties.Settings.Default.SearchPaths)
                {
                    lbSearchPaths.Items.Add(s);
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Error loading default values\r\n", exc);
            }
        }

        /// <summary>
        /// Save values on form to properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOk_Click(object sender, EventArgs e)
        {
            try
            {
                saveFormDefaults();
            }
            catch (Exception exc)
            {
                HandleExceptions(exc);
            }
        }

        /// <summary>
        /// Load form values from properties
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ToolsOptionsForm_Load(object sender, EventArgs e)
        {
            try
            {
                loadDefaults();
            }
            catch (Exception exc)
            {
                HandleExceptions(exc);
            }
        }

        /// <summary>
        /// Open browse dir dialog
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowseDestDir_Click(object sender, EventArgs e)
        {
            string selDir = openBrowseDialog(tbResultsDir.Text, true);

            try
            {
                if (!selDir.Equals(string.Empty))
                {
                    tbResultsDir.Text = selDir;
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc);
            }

            
        }

        /// <summary>
        /// Check result dir is valid path when leaving text box
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void tbResultsDir_Leave(object sender, EventArgs e)
        {
            try
            {
                if (!Directory.Exists(tbResultsDir.Text))
                {
                    throw new Exception("The result directory " + tbResultsDir.Text + " does not exist\r\n");
                }
            }
            catch (Exception exc)
            {
                HandleExceptions(exc);
            }
        }

        /// <summary>
        /// Reset results dir to default my doc/my idpicker reports
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnResetResultsDir_Click(object sender, EventArgs e)
        {
            try
            {
                tbResultsDir.Text = Environment.GetFolderPath(Environment.SpecialFolder.Personal) + "\\My IDPicker Reports";
            }
            catch (Exception exc)
            {
                HandleExceptions(exc);
            }
        }

        /// <summary>
        /// Only alow select items in one lb at time since clear,remove,add buttons
        /// apply to each of the three listboxes.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void lbSearchPaths_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                lbFastaPaths.ClearSelected();
                lbSourcePaths.ClearSelected();
                lbSearchPaths.ClearSelected();
            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error selecting search path\r\n", exc));
            }
        }

        /// <summary>
        /// Clear listbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnClear_Click(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Forms.Control.ControlCollection selTabsControls = tabSearchPaths.SelectedTab.Controls;

                foreach (Control c in selTabsControls)
                {
                    if (c.GetType().Equals(typeof(ListBox)))
                    {
                        ListBox lbSearchPaths = c as ListBox;

                        if (lbSearchPaths.Items.Count > 0)
                        {
                            if (DialogResult.Yes == MessageBox.Show(this, "Are you sure you wish to remove all current paths from this list?", "Options", MessageBoxButtons.YesNo, MessageBoxIcon.Information))
                            {
                                lbSearchPaths.Items.Clear();
                            }
                        }

                        break;
                    }
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error clearing search paths\r\n", exc));
            }


        }

        /// <summary>
        /// Open browse dialog, add sel dir to listbox in focus
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnBrowse_Click(object sender, EventArgs e)
        {
            try
            {
                string dir = openBrowseDialog(@"c:\", true);

                if (!dir.Equals(string.Empty))
                {
                    Control.ControlCollection selTabsControls = tabSearchPaths.SelectedTab.Controls;

                    foreach (Control c in selTabsControls)
                    {
                        if (c.GetType().Equals(typeof(ListBox)))
                        {
                            ListBox lbSearchPaths = c as ListBox;

                            if (!lbSearchPaths.Items.Contains(dir))
                            {
                                lbSearchPaths.Items.Add(dir);
                            }

                            break;
                        }
                    }
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error adding search paths\r\n", exc));
            }
        }


        /// <summary>
        /// Add a relative path with macro support to listbox with focus
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnAddRelative_Click( object sender, EventArgs e )
        {
            AddPathDialog dialog = new AddPathDialog();
            if( dialog.ShowDialog() == DialogResult.OK )
            {
                Control.ControlCollection selTabsControls = tabSearchPaths.SelectedTab.Controls;

                foreach( Control c in selTabsControls )
                {
                    if( c.GetType().Equals( typeof( ListBox ) ) )
                    {
                        ListBox lbSearchPaths = c as ListBox;

                        if( !lbSearchPaths.Items.Contains( dialog.Path ) )
                        {
                            lbSearchPaths.Items.Add( dialog.Path );
                        }

                        break;
                    }
                }
            }
        }


        /// <summary>
        /// Remove sel path from listbox with focus
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnRemove_Click(object sender, EventArgs e)
        {
            try
            {
                System.Windows.Forms.Control.ControlCollection selTabsControls = tabSearchPaths.SelectedTab.Controls;

                foreach (Control c in selTabsControls)
                {
                    if (c.GetType().Equals(typeof(ListBox)))
                    {
                        ListBox lbSearchPaths = c as ListBox;

                        if (lbSearchPaths.Items.Count > 0)
                        {
                            for (int i = lbSearchPaths.Items.Count; i >= 0; i--)
                            {
                                lbSearchPaths.Items.Remove(lbSearchPaths.SelectedItem);
                            }
                        }

                        break;
                    }
                }

            }
            catch (Exception exc)
            {
                HandleExceptions(new Exception("Error adding search paths\r\n", exc));
            }


        }

        /// <summary>
        /// Display help html doc with anchor link
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void cmWhatsThis_Click(object sender, EventArgs e)
        {
            try
            {
                Control control = (sender as ContextMenuStrip).SourceControl;
                string anchor = control.Text.Substring(0, control.Text.Length - 1);

                Uri uri = new Uri( Common.docFilePath + "#" + Common.getAnchorNameByControlName(control.Name));

                HtmlHelpForm form = new HtmlHelpForm(uri);

                form.Show();
            }
            catch (Exception exc)
            {
                HandleExceptions(exc);
            }
        }       
    }
}