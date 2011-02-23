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
using IDPicker.DataModel;
using IDPicker.Forms;

namespace IDPicker
{
    class LayoutManager
    {
        NHibernate.ISession _session;
        private List<LayoutProperty> _userLayoutList;
        private IDPickerForm mainForm;
        private PeptideTableForm peptideTableForm;
        private ProteinTableForm proteinTableForm;
        private SpectrumTableForm spectrumTableForm;
        private DockPanel dockPanel;

        public LayoutManager(IDPickerForm mainForm, PeptideTableForm peptideTableForm, ProteinTableForm proteinTableForm, SpectrumTableForm spectrumTableForm, DockPanel dockPanel)
        {
            this.mainForm = mainForm;
            this.peptideTableForm = peptideTableForm;
            this.proteinTableForm = proteinTableForm;
            this.spectrumTableForm = spectrumTableForm;
            this.dockPanel = dockPanel;

            RefreshUserLayoutList();

            TryResetUserLayoutSettings();
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
            var location = Properties.Settings.Default.IDPickerFormLocation;
            var size = Properties.Settings.Default.IDPickerFormSize;

            if (size.IsEmpty)
            {
                size = SystemInformation.PrimaryMonitorMaximizedWindowSize;
                size = new Size(size.Width - 8, size.Height - 8);
            }

            mainForm.Location = location;
            mainForm.Size = size;
        }

        /// <summary>
        /// Saves the current bounds of the main form to the user's persistent settings.
        /// </summary>
        public void SaveMainFormSettings ()
        {
            Properties.Settings.Default.IDPickerFormLocation = mainForm.Location;
            Properties.Settings.Default.IDPickerFormSize = mainForm.Size;
            Properties.Settings.Default.Save();
        }

        /// <summary>
        /// Populates local list of user layouts with internally stored values. 
        /// Default values are created if no values are stored.
        /// </summary>
        private void RefreshUserLayoutList()
        {
            var retrievedList = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.UserLayouts));
            _userLayoutList = new List<LayoutProperty>();

            //stick with an empty list if not in the correct format
            if (retrievedList.Count == 0 || !retrievedList[0].StartsWith("System Default"))
                return;

            for (var x = 0; x < retrievedList.Count; x++)
            {
                var items = retrievedList[x].Split('|');
                var customColumnList = new List<ColumnProperty>();
                if (bool.Parse(items[2]))
                {
                    //ProteinForm
                    customColumnList.AddRange(
                        ColumnSettingStringToIdpColumnPropertyList(
                        Util.StringCollectionToStringArray(Properties.Settings.Default.ProteinTableFormSettings),
                        "ProteinTableForm", x)
                        );

                    //PeptideForm
                    customColumnList.AddRange(
                        ColumnSettingStringToIdpColumnPropertyList(
                        Util.StringCollectionToStringArray(Properties.Settings.Default.PeptideTableFormSettings),
                        "PeptideTableForm", x)
                        );

                    //SpectrumForm
                    customColumnList.AddRange(
                        ColumnSettingStringToIdpColumnPropertyList(
                        Util.StringCollectionToStringArray(Properties.Settings.Default.SpectrumTableFormSettings),
                        "SpectrumTableForm", x)
                        );
                }


                var newLayout = new LayoutProperty
                {
                    Name = items[0],
                    PaneLocations = items[1],
                    HasCustomColumnSettings = bool.Parse(items[2]),
                    SettingsList = customColumnList
                };
                foreach (var item in newLayout.SettingsList)
                    item.Layout = newLayout;


                _userLayoutList.Add(newLayout);
            }
        }

        private void TryResetUserLayoutSettings()
        {
            if (_userLayoutList.Count < 2 || _userLayoutList[1].Name != "User Default")
            {
                _userLayoutList = new List<LayoutProperty>();
                var customColumnList = new List<ColumnProperty>();
                customColumnList.AddRange(proteinTableForm.GetCurrentProperties(false));
                customColumnList.AddRange(peptideTableForm.GetCurrentProperties(false));
                customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());

                SaveNewLayout("System Default", true, false, customColumnList);
                SaveNewLayout("User Default", true, false, customColumnList);
                RefreshUserLayoutList();
            }
        }

        internal void SaveUserLayoutList()
        {
            Properties.Settings.Default.UserLayouts.Clear();
            Properties.Settings.Default.ProteinTableFormSettings.Clear();
            Properties.Settings.Default.PeptideTableFormSettings.Clear();
            Properties.Settings.Default.SpectrumTableFormSettings.Clear();

            //Layout properties will be in format:
            //"(string)Name|(string)XML|(bool)CustomColumns"
            for (var x = 0; x < _userLayoutList.Count; x++)
            {
                //Save Layout
                Properties.Settings.Default.UserLayouts.Add(string.Format("{0}|{1}|{2}{3}",
                    _userLayoutList[x].Name, _userLayoutList[x].PaneLocations,
                    _userLayoutList[x].HasCustomColumnSettings, Environment.NewLine));
                Properties.Settings.Default.Save();

                //Save column settings
                if (_userLayoutList[x].HasCustomColumnSettings)
                {
                    //Protein Form
                    var columnSettings = _userLayoutList[x].SettingsList.Where(o => o.Scope == "ProteinTableForm");
                    SaveUserColumnSettings(columnSettings.ToList(), x, "ProteinTableForm");

                    //Peptide Form
                    columnSettings = _userLayoutList[x].SettingsList.Where(o => o.Scope == "PeptideTableForm");
                    SaveUserColumnSettings(columnSettings.ToList(), x, "PeptideTableForm");

                    //Spectrum Form
                    columnSettings = _userLayoutList[x].SettingsList.Where(o => o.Scope == "SpectrumTableForm");
                    SaveUserColumnSettings(columnSettings.ToList(), x, "SpectrumTableForm");
                }
            }
        }


        private static IEnumerable<ColumnProperty> ColumnSettingStringToIdpColumnPropertyList(IEnumerable<string> settings, string associatedForm, int associatedLayout)
        {
            //User properties will be in format:
            //"(int)LayoutIndex
            //(string)Column1Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column2Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column3Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column4Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(int)BackColorCode
            //(int)TextColorCode"

            var columnList = new List<ColumnProperty>();

            foreach (var setting in settings)
            {
                var lines = setting.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                if (int.Parse(lines[0]) == associatedLayout)
                {
                    for (var x = 1; x < lines.Count() - 2; x++)
                    {
                        var items = lines[x].Split('|');
                        bool tempbool;
                        var canParse = bool.TryParse(items[5], out tempbool);

                        columnList.Add(new ColumnProperty
                        {
                            Name = items[0],
                            Type = items[1],
                            DecimalPlaces = int.Parse(items[2]),
                            ColorCode = int.Parse(items[3]),
                            Visible = bool.Parse(items[4]),
                            Locked = canParse ? bool.Parse(items[5]) : (bool?)null,
                            Scope = associatedForm
                        });
                    }

                    columnList.Add(new ColumnProperty
                    {
                        Name = "BackColor",
                        Type = "GlobalSetting",
                        DecimalPlaces = -1,
                        ColorCode = int.Parse(lines[lines.Count() - 2]),
                        Visible = false,
                        Locked = null,
                        Scope = associatedForm
                    });

                    columnList.Add(new ColumnProperty
                    {
                        Name = "TextColor",
                        Type = "GlobalSetting",
                        DecimalPlaces = -1,
                        ColorCode = int.Parse(lines[lines.Count() - 1]),
                        Visible = false,
                        Locked = null,
                        Scope = associatedForm
                    });

                    break;
                }
            }
            return columnList;
        }

        private LayoutProperty SaveNewLayout(string layoutName, bool saveColumns, bool isDatabase, IList<ColumnProperty> customColumnList)
        {
            //var customColumnList = new List<ColumnProperty>();
            //if (saveColumns)
            //{
            //    customColumnList.AddRange(proteinTableForm.GetCurrentProperties());
            //    customColumnList.AddRange(peptideTableForm.GetCurrentProperties());
            //    customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
            //}

            var tempLayout = new LayoutProperty
            {
                Name = layoutName,
                PaneLocations = GetPanelLocations(),
                HasCustomColumnSettings = saveColumns,
            };
            foreach (var item in customColumnList)
                item.Layout = tempLayout;

            tempLayout.SettingsList = customColumnList;

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
                tempLayout.SettingsList = customColumnList;
                _userLayoutList.Add(tempLayout);
                SaveUserLayoutList();
            }

            return tempLayout;
        }


        private void UpdateLayout(LayoutProperty layoutProperty, bool saveColumns, bool isDatabase, IList<ColumnProperty> customColumnList)
        {
            //var customColumnList = new List<ColumnProperty>();
            //if (saveColumns)
            //{
            //    customColumnList.AddRange(proteinTableForm.GetCurrentProperties());
            //    customColumnList.AddRange(peptideTableForm.GetCurrentProperties());
            //    customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
            //}

            layoutProperty.PaneLocations = GetPanelLocations();
            layoutProperty.HasCustomColumnSettings = saveColumns;
            foreach (var item in customColumnList)
                item.Layout = layoutProperty;

            if (isDatabase)
            {
                lock (_session)
                {
                    if (layoutProperty.SettingsList.Count > 0)
                        foreach (var item in layoutProperty.SettingsList)
                            _session.Delete(item);

                    layoutProperty.SettingsList = customColumnList;
                    _session.Save(layoutProperty);
                    _session.Flush();
                }
            }
            else
            {
                layoutProperty.SettingsList = customColumnList;
                SaveUserLayoutList();
            }
        }

        private string GetPanelLocations()
        {
            var tempFilepath = Path.GetTempFileName();
            dockPanel.SaveAsXml(tempFilepath);
            var locationXml = File.ReadAllText(tempFilepath);
            File.Delete(tempFilepath);
            return locationXml;
        }

        internal List<ToolStripItem> LoadLayoutMenu()
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
                var newOption = new ToolStripMenuItem { Text = item.Name };
                newOption.Click += (s, e) => mainForm.LoadLayout(tempItem);
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
                    var newOption = new ToolStripMenuItem { Text = item.Name };
                    newOption.Click += (s, e) => mainForm.LoadLayout(tempItem);
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
                var newOption = new ToolStripMenuItem { Text = item.Name };
                newOption.Click += (s, e) =>
                {
                    var saveColumns = false;
                    var customColumnList = new List<ColumnProperty>();
                    if (MessageBox.Show("Save column settings as well?", "Save", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        saveColumns = true;
                        customColumnList.AddRange(proteinTableForm.GetCurrentProperties(false));
                        customColumnList.AddRange(peptideTableForm.GetCurrentProperties(false));
                        customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
                    }
                    UpdateLayout(tempItem, saveColumns, false, customColumnList);
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
                            var customColumnList = new List<ColumnProperty>();
                            if (textInput.GetCheckState())
                            {
                                customColumnList.AddRange(proteinTableForm.GetCurrentProperties(false));
                                customColumnList.AddRange(peptideTableForm.GetCurrentProperties(false));
                                customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
                            }
                            SaveNewLayout(textInput.GetText(), textInput.GetCheckState(), false, customColumnList);
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
                    var newOption = new ToolStripMenuItem { Text = item.Name };
                    LayoutProperty tempItem = item;
                    newOption.Click += (s, e) =>
                    {
                        var saveColumns = false;
                        var customColumnList = new List<ColumnProperty>();
                        if (MessageBox.Show("Save column settings as well?", "Save", MessageBoxButtons.YesNo) == DialogResult.Yes)
                        {
                            saveColumns = true;
                            customColumnList.AddRange(proteinTableForm.GetCurrentProperties(true));
                            customColumnList.AddRange(peptideTableForm.GetCurrentProperties(true));
                            customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
                        }
                        UpdateLayout(tempItem, saveColumns, true, customColumnList);
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
                                var customColumnList = new List<ColumnProperty>();
                                if (textInput.GetCheckState())
                                {
                                    customColumnList.AddRange(proteinTableForm.GetCurrentProperties(true));
                                    customColumnList.AddRange(peptideTableForm.GetCurrentProperties(true));
                                    customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
                                }
                                SaveNewLayout(textInput.GetText(), textInput.GetCheckState(), true, customColumnList);
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

        private static void SaveUserColumnSettings(IEnumerable<ColumnProperty> columnList, int layoutIndex, string targetForm)
        {
            //User properties will be in format:
            //"(int)LayoutIndex
            //(string)Column1Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column2Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column3Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(string)Column4Name|(string)Type|(int)DecimalPlaces|(int)CellColorCode|(bool)Visible|(bool)Locked
            //(int)BackColorCode
            //(int)TextColorCode"

            var setting = new StringBuilder(layoutIndex + Environment.NewLine);
            foreach (var item in columnList)
            {
                if (item.Name == "BackColor" || item.Name == "TextColor")
                    continue;
                setting.AppendFormat("{0}|", item.Name);
                setting.AppendFormat("{0}|", item.Type);
                setting.AppendFormat("{0}|", item.DecimalPlaces);
                setting.AppendFormat("{0}|", item.ColorCode);
                setting.AppendFormat("{0}|", item.Visible);
                if (item.Locked == null)
                    setting.AppendFormat("{0}{1}", "null", Environment.NewLine);
                else
                    setting.AppendFormat("{0}{1}", item.Locked, Environment.NewLine);
            }

            var backColor = columnList.Where(x => x.Name == "BackColor").SingleOrDefault();
            var textColor = columnList.Where(x => x.Name == "TextColor").SingleOrDefault();

            setting.AppendFormat("{0}{1}", backColor.ColorCode, Environment.NewLine);
            setting.AppendFormat("{0}{1}", textColor.ColorCode, Environment.NewLine);

            switch (targetForm)
            {
                case "ProteinTableForm":
                    Properties.Settings.Default.ProteinTableFormSettings.Add(setting.ToString());
                    Properties.Settings.Default.Save();
                    break;
                case "PeptideTableForm":
                    Properties.Settings.Default.PeptideTableFormSettings.Add(setting.ToString());
                    Properties.Settings.Default.Save();
                    break;
                case "SpectrumTableForm":
                    Properties.Settings.Default.SpectrumTableFormSettings.Add(setting.ToString());
                    Properties.Settings.Default.Save();
                    break;
            }
        }

        internal LayoutProperty GetCurrentDefault()
        {
            if (_userLayoutList.Count < 2 || _userLayoutList[1].Name != "User Default")
                return null;
            if (_session == null)
                return _userLayoutList[1];
            
            var databaseDefault = _session.QueryOver<LayoutProperty>().Where(x => x.Name == "Database Default").SingleOrDefault();
            if (databaseDefault == null)
            {
                var customColumnList = new List<ColumnProperty>();
                customColumnList.AddRange(proteinTableForm.GetCurrentProperties(true));
                customColumnList.AddRange(peptideTableForm.GetCurrentProperties(true));
                customColumnList.AddRange(spectrumTableForm.GetCurrentProperties());
                databaseDefault = SaveNewLayout("Database Default", true, true, customColumnList);
            }

            return databaseDefault;
        }
    }
}
