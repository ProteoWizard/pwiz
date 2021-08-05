//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Jay Holman.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using IDPicker.Controls;
using IDPicker.DataModel;
using IDPicker.Forms;
using NHibernate.Linq;

namespace IDPicker
{
    class LayoutManager
    {
        public LayoutProperty CurrentLayout
        {
            get { return _currentLayout; }
            set
            {
                if (_currentLayout == value) return;
                _currentLayout = value;
                foreach(var form in dockPanel.Contents.OfType<DockableForm>())
                    if (!_currentLayout.PaneLocations.Contains(form.Name))
                    {
                        // TODO: log this message MessageBox.Show("No entry in layout for form \"" + form.Name + "\"");
                        _currentLayout.PaneLocations = getPaneLocationXML();
                        break;
                    }
                mainForm.LoadLayout(_currentLayout);
            }
        }

        public LayoutProperty DefaultSystemLayout { get { return _userLayoutList.Single(o => o.Name == "System Default"); } }
        public LayoutProperty DefaultUserLayout { get { return _userLayoutList.Single(o => o.Name == "User Default"); } }

        private LayoutProperty _currentLayout;
        private NHibernate.ISession _session;
        private List<LayoutProperty> _userLayoutList;
        private IDPickerForm mainForm;
        private IList<IPersistentForm> _persistentForms;
        private DockPanel dockPanel;

        public LayoutManager(IDPickerForm mainForm, DockPanel dockPanel, IList<IPersistentForm> persistentForms)
        {
            this.mainForm = mainForm;
            _persistentForms = persistentForms;
            this.dockPanel = dockPanel;

            refreshUserLayoutList();

            tryResetUserLayoutSettings();
        }

        /// <summary>
        /// Informs the layout manager how to access the database in current use
        /// </summary>
        /// <param name="newSession"></param>
        public void SetSession(NHibernate.ISession newSession)
        {
            _session = newSession;
        }

        /// <summary>
        /// If IDPicker has been run before, sets the bounds of the main form to the last used bounds.
        /// Otherwise, sets defaults appropriate for the current display settings.
        /// </summary>
        public void LoadMainFormSettings ()
        {
            mainForm.Location = Properties.GUI.Settings.Default.IDPickerFormLocation;
            mainForm.Size = Properties.GUI.Settings.Default.IDPickerFormSize;
            mainForm.WindowState = Properties.GUI.Settings.Default.IDPickerFormWindowState;
        }

        /// <summary>
        /// Saves the current bounds of the main form to the user's persistent settings.
        /// </summary>
        public void SaveMainFormSettings ()
        {
            if (mainForm.WindowState == FormWindowState.Normal)
            {
                Properties.GUI.Settings.Default.IDPickerFormLocation = mainForm.Location;
                Properties.GUI.Settings.Default.IDPickerFormSize = mainForm.Size;
            }
            Properties.GUI.Settings.Default.IDPickerFormWindowState = mainForm.WindowState;
            Properties.GUI.Settings.Default.Save();
        }

        public void SaveUserLayoutList ()
        {
            Properties.Settings.Default.UserLayouts.Clear();

            //Layout properties will be in format:
            //"(string)Name|(string)PaneLocationsXML|(string)FormPropertiesXML"
            for (var x = 0; x < _userLayoutList.Count; x++)
            {
                //Save Layout
                string formProperties = String.Empty;
                if (_userLayoutList[x].HasCustomColumnSettings)
                {
                    var userType = new FormPropertiesUserType();
                    formProperties = (string) userType.Disassemble(_userLayoutList[x].FormProperties);
                }

                Properties.Settings.Default.UserLayouts.Add(String.Format("{0}|{1}|{2}{3}",
                                                            _userLayoutList[x].Name,
                                                            _userLayoutList[x].PaneLocations,
                                                            formProperties,
                                                            Environment.NewLine));
            }
            Properties.Settings.Default.Save();
        }

        public List<ToolStripItem> LoadLayoutMenu ()
        {
            var noDatabase = _session == null;
            var currentMenuLevel = new List<ToolStripItem>();
            ToolStripMenuItem saveMenu;
            ToolStripMenuItem loadMenu;
            ToolStripMenuItem deleteMenu = null;
            var menuList = new List<ToolStripItem>();

            #region Load Options
            //set up user load options
            foreach (var item in _userLayoutList)
            {
                var tempItem = item;
                var newOption = new ToolStripMenuItem { Text = item.Name + (CurrentLayout.Name == item.Name ? " (current)" : "") };
                newOption.Click += (s, e) => CurrentLayout = tempItem;
                currentMenuLevel.Add(newOption);
            }

            //check if more needs to be done
            if (noDatabase)
                loadMenu = new ToolStripMenuItem("Load", null, currentMenuLevel.ToArray());
            else
            {
                currentMenuLevel.Add(new ToolStripSeparator());
                IList<LayoutProperty> databaseLayouts;
                lock (_session)
                    databaseLayouts = _session.QueryOver<LayoutProperty>().List();

                foreach (var item in databaseLayouts)
                {
                    var tempItem = item;
                    var newOption = new ToolStripMenuItem { Text = item.Name + (CurrentLayout.Name == item.Name ? " (current)" : "") };
                    newOption.Click += (s, e) => CurrentLayout = tempItem;
                    currentMenuLevel.Add(newOption);
                }


                loadMenu = new ToolStripMenuItem("Load", null, currentMenuLevel.ToArray());
            }
            #endregion

            #region Save Options

            //create user save list
            currentMenuLevel = new List<ToolStripItem>();
            foreach (var item in _userLayoutList)
            {
                var tempItem = item;
                var newOption = new ToolStripMenuItem { Text = item.Name + (CurrentLayout.Name == item.Name ? " (current)" : "") };
                newOption.Click += (s, e) =>
                {
                    var saveColumns = false;
                    var formProperties = new Dictionary<string, FormProperty>();
                    if (MessageBox.Show("Save column settings as well?", "Save", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        saveColumns = true;
                        foreach (var form in _persistentForms)
                            formProperties[form.Name] = form.GetCurrentProperties(false);
                    }
                    updateLayout(tempItem, saveColumns, false, formProperties);
                };
                currentMenuLevel.Add(newOption);
            }

            //replace system default (not editable) with new layout option
            {
                var newLayout = new ToolStripMenuItem("New Local Layout");
                newLayout.Click +=
                    (s, e) =>
                    {
                        var textInput = new TextInputPrompt("Layout Name", true, string.Empty);
                        if (textInput.ShowDialog() == DialogResult.OK)
                        {
                            var formProperties = new Dictionary<string, FormProperty>();
                            if (textInput.GetCheckState())
                                foreach (var form in _persistentForms)
                                    formProperties[form.Name] = form.GetCurrentProperties(false);
                            saveNewLayout(textInput.GetText(), textInput.GetCheckState(), false, formProperties);
                        }
                    };
                currentMenuLevel.RemoveAt(0);
                currentMenuLevel.Insert(0, newLayout);
            }

            //check if more needs to be done
            if (noDatabase)
                saveMenu = new ToolStripMenuItem("Save", null, currentMenuLevel.ToArray());
            else
            {
                var currentDatabaseMenuLevel = new List<ToolStripItem>();
                currentMenuLevel.Add(new ToolStripSeparator());

                IList<LayoutProperty> databaseLayouts;
                lock (_session)
                    databaseLayouts = _session.QueryOver<LayoutProperty>().List();

                foreach (var item in databaseLayouts)
                {
                    var newOption = new ToolStripMenuItem { Text = item.Name + (CurrentLayout.Name == item.Name ? " (current)" : "") };
                    LayoutProperty tempItem = item;
                    newOption.Click += (s, e) =>
                    {
                        var saveColumns = false;
                        var formProperties = new Dictionary<string, FormProperty>();
                        if (MessageBox.Show("Save column settings as well?", "Save", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            saveColumns = true;
                            foreach (var form in _persistentForms)
                                formProperties[form.Name] = form.GetCurrentProperties(false);
                        }
                        updateLayout(tempItem, saveColumns, true, formProperties);
                    };
                    currentDatabaseMenuLevel.Add(newOption);
                }

                //Add new layout option
                {
                    var newLayout = new ToolStripMenuItem("New Database Layout");
                    newLayout.Click +=
                        (s, e) =>
                        {
                            var textInput = new TextInputPrompt("Layout Name", true, string.Empty);
                            if (textInput.ShowDialog() == DialogResult.OK)
                            {
                                var formProperties = new Dictionary<string, FormProperty>();
                                if (textInput.GetCheckState())
                                    foreach (var form in _persistentForms)
                                        formProperties[form.Name] = form.GetCurrentProperties(false);
                                saveNewLayout(textInput.GetText(), textInput.GetCheckState(), true, formProperties);
                            }
                        };
                    currentDatabaseMenuLevel.Insert(0, newLayout);
                }
                currentMenuLevel.AddRange(currentDatabaseMenuLevel);

                saveMenu = new ToolStripMenuItem("Save", null, currentMenuLevel.ToArray());
            }

            #endregion

            #region Delete Options
            //set up user delete options
            currentMenuLevel = new List<ToolStripItem>();
            foreach (var item in _userLayoutList)
            {
                var tempItem = item;
                var newOption = new ToolStripMenuItem { Text = item.Name };
                newOption.Click += (s, e) =>
                {
                    if (MessageBox.Show(string.Format("Are you sure you want to delete '{0}'?", tempItem.Name), "Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        _userLayoutList.Remove(tempItem);
                        SaveUserLayoutList();
                    }
                };
                currentMenuLevel.Add(newOption);
            }
            //Dont allow user to delete defaults
            currentMenuLevel.RemoveRange(0, 2);
            //dont delete if nothing to delete, but check for database first
            if (noDatabase)
                deleteMenu = currentMenuLevel.Count > 0 ?
                    new ToolStripMenuItem("Delete", null, currentMenuLevel.ToArray()) :
                    null;
            else
            {
                var currentDatabaseMenuLevel = new List<ToolStripItem>();
                if (currentMenuLevel.Count > 0)
                    currentMenuLevel.Add(new ToolStripSeparator());

                IList<LayoutProperty> databaseLayouts;
                lock (_session)
                    databaseLayouts = _session.QueryOver<LayoutProperty>().List();

                foreach (var item in databaseLayouts)
                {
                    var tempItem = item;
                    var newOption = new ToolStripMenuItem { Text = item.Name };
                    newOption.Click += (s, e) =>
                    {
                        if (MessageBox.Show(string.Format("Are you sure you want to delete '{0}'?", tempItem.Name), "Delete", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            _session.Delete(tempItem);
                            _session.Flush();
                        }
                    };
                    currentDatabaseMenuLevel.Add(newOption);
                }
                currentDatabaseMenuLevel.RemoveAt(0);

                currentMenuLevel.AddRange(currentDatabaseMenuLevel);

                if (currentMenuLevel.Count > 0)
                    deleteMenu = new ToolStripMenuItem("Delete", null, currentMenuLevel.ToArray());

            }

            #endregion

            menuList.Add(saveMenu);
            menuList.Add(loadMenu);
            if (deleteMenu != null)
                menuList.Add(deleteMenu);

            return menuList;
        }

        public LayoutProperty GetCurrentDefault ()
        {
            if (_userLayoutList.Count < 2 || _userLayoutList[1].Name != "User Default")
                return null;
            if (_session == null)
            {
                var userDefault = _userLayoutList[1];
                if (!userDefault.FormProperties.Any())
                {
                    var formProperties = new Dictionary<string, FormProperty>();
                    foreach (var form in _persistentForms)
                        formProperties[form.Name] = form.GetCurrentProperties(false);
                    updateLayout(userDefault, true, false, formProperties);
                }

                return userDefault;
            }
            else
            {
                var databaseDefault = _session.Query<LayoutProperty>().SingleOrDefault(x => x.Name == "Database Default");
                if (databaseDefault == null || !databaseDefault.FormProperties.Any())
                {
                    var formProperties = new Dictionary<string, FormProperty>();
                    foreach (var form in _persistentForms)
                        formProperties[form.Name] = form.GetCurrentProperties(false);
                    databaseDefault = saveNewLayout("Database Default", true, true, formProperties);
                }

                return databaseDefault;
            }
        }

        /// <summary>
        /// Populates local list of user layouts with internally stored values. 
        /// Default values are created if no values are stored.
        /// </summary>
        private void refreshUserLayoutList()
        {
            var retrievedList = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.UserLayouts));
            _userLayoutList = new List<LayoutProperty>();

            //stick with an empty list if not in the correct format
            if (retrievedList.Count == 0 || !retrievedList[0].StartsWith("System Default"))
                return;

            for (var x = 0; x < retrievedList.Count; x++)
            {
                var items = retrievedList[x].Split('|');

                var newLayout = new LayoutProperty
                {
                    Name = items[0],
                    PaneLocations = items[1]
                };

                if (!String.IsNullOrEmpty(items[2]))
                {
                    try
                    {
                        var userType = new FormPropertiesUserType();
                        newLayout.FormProperties = (userType.Assemble(items[2], null) ?? new Dictionary<string, FormProperty>()) as Dictionary<string, FormProperty>;
                        newLayout.HasCustomColumnSettings = true;
                    }
                    catch (Exception ex)
                    {
                        Program.HandleException(ex);
                    }
                }

                _userLayoutList.Add(newLayout);
            }
        }

        private void tryResetUserLayoutSettings()
        {
            if (_userLayoutList.Count < 2 || _userLayoutList[1].Name != "User Default")
            {
                _userLayoutList = new List<LayoutProperty>();
                var formProperties = new Dictionary<string, FormProperty>();
                foreach (var form in _persistentForms)
                    formProperties[form.Name] = form.GetCurrentProperties(false);

                saveNewLayout("System Default", true, false, formProperties);
                saveNewLayout("User Default", true, false, formProperties);
                refreshUserLayoutList();
            }
        }

        private LayoutProperty saveNewLayout(string layoutName, bool saveColumns, bool isDatabase, IDictionary<string, FormProperty> formProperties)
        {
            var tempLayout = new LayoutProperty
            {
                Name = layoutName,
                PaneLocations = getPaneLocationXML(),
                HasCustomColumnSettings = saveColumns,
            };

            tempLayout.FormProperties = formProperties;

            if (isDatabase)
            {
                lock (_session)
                {
                    _session.Save(tempLayout);
                    _session.Flush();
                }
            }
            else
            {
                _userLayoutList.Add(tempLayout);
                SaveUserLayoutList();
            }

            return tempLayout;
        }

        private void updateLayout(LayoutProperty layoutProperty, bool saveColumns, bool isDatabase, IDictionary<string, FormProperty> formProperties)
        {
            layoutProperty.PaneLocations = getPaneLocationXML();
            layoutProperty.HasCustomColumnSettings = saveColumns;
            layoutProperty.FormProperties = formProperties;

            if (isDatabase)
            {
                lock (_session)
                {
                    _session.Save(layoutProperty);
                    _session.Flush();
                }
            }
            else
                SaveUserLayoutList();
        }

        private string getPaneLocationXML()
        {
            var tempFilepath = Path.GetTempFileName();
            dockPanel.SaveAsXml(tempFilepath);
            var locationXml = File.ReadAllText(tempFilepath);
            File.Delete(tempFilepath);
            return locationXml;
        }
    }
}
