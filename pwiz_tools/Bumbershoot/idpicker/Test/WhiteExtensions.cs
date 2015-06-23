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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2014 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestStack.White;
using TestStack.White.Factory;
using TestStack.White.Configuration;
using TestStack.White.UIItems;
using TestStack.White.UIItems.WindowItems;
using TestStack.White.UIItems.WindowStripControls;
using TestStack.White.UIItems.MenuItems;
using TestStack.White.UIItems.TreeItems;
using TestStack.White.UIItems.TableItems;
using TestStack.White.UIItems.ListBoxItems;
using TestStack.White.UIItems.Finders;
using TestStack.White.UIItems.Container;
using TestStack.White.UIItems.Actions;
using TestStack.White.UIItems.Custom;
using TestStack.White.AutomationElementSearch;

namespace Test
{
    public class AutomationElementTreeNode
    {
        public AutomationElement.AutomationElementInformation Current { get; private set; }
        public List<AutomationElementTreeNode> Children { get; private set; }

        public override string ToString()
        {
            return String.Format("Id:{0} Name:{1} Children:{2}", Current.AutomationId ?? "null", Current.Name ?? "null", Children == null ? 0 : Children.Count);
        }

        private AutomationElementTreeNode(AutomationElement e, int maxDepth, int currentDepth)
        {
            Current = e.Current;

            if (maxDepth == currentDepth)
                return;

            Children = new List<AutomationElementTreeNode>();
            foreach (var child in e.FindAll(TreeScope.Children, Condition.TrueCondition).OfType<AutomationElement>())
                Children.Add(new AutomationElementTreeNode(child, maxDepth, currentDepth + 1));
        }

        public AutomationElementTreeNode(AutomationElement e, int maxDepth = 5) : this(e, maxDepth, 1)
        {
        }
    }

    public class IDPickerAllSettings
    {
        public IDPickerAllSettings()
        {
            GeneralSettings = new IDPicker.Properties.Settings();
            GUISettings = new IDPicker.Properties.GUI.Settings();
        }

        public IDPicker.Properties.Settings GeneralSettings { get; private set; }
        public IDPicker.Properties.GUI.Settings GUISettings { get; private set; }
    }

    /// <summary>
    /// A subclass of the White Table UIItem that is customized to work with AutomationDataGridView; this was necessary to get around efficiency problems with System.Windows.Automation.
    /// </summary>
    [ControlTypeMapping(CustomUIItemType.Table)]
    public class FastTable : Table
    {
        private TableRows rows;
        private TableHeader header;
        private readonly AutomationElementFinder finder;
        private readonly FastTableRowFactory tableRowFactory;

        public FastTable(AutomationElement element, ActionListener listener)
            : base(element, listener)
        {
            finder = new AutomationElementFinder(automationElement);
            tableRowFactory = new FastTableRowFactory(finder);
        }

        protected FastTable() { }

        public override TableRows Rows
        {
            get
            {
                return rows ?? (rows = tableRowFactory.CreateRows(actionListener, Header ?? new NullTableHeader()));
            }
        }

        public override TableHeader Header
        {
            get
            {
                if (header == null)
                {
                    AutomationElement headerElement = finder.Descendant(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Header));
                    if (headerElement == null) return null;
                    header = new FastTableHeader(headerElement, actionListener);
                }
                return header;
            }
        }

        public override void Refresh()
        {
            rows = null;
        }

        #region Nested classes that override some inappropriate White behavior intended to work with stock DataGridViews instead of AutomationDataGridView
        public class FastTableHeader : TableHeader
        {
            protected FastTableHeader() { }

            public FastTableHeader(AutomationElement automationElement, ActionListener actionListener) : base(automationElement, actionListener) { }

            public override TableColumns Columns
            {
                get
                {
                    var headerItems = new AutomationElementFinder(automationElement).Descendants(AutomationSearchCondition.ByControlType(ControlType.HeaderItem));
                    return new TableColumns(headerItems, actionListener);
                }
            }
        }

        public class FastTableRowFactory
        {
            private readonly AutomationElementFinder automationElementFinder;

            public FastTableRowFactory(AutomationElementFinder automationElementFinder)
            {
                this.automationElementFinder = automationElementFinder;
            }

            public TableRows CreateRows(ActionListener actionListener, TableHeader tableHeader)
            {
                var rowElements = GetRowElements();
                return new TableRows(rowElements, actionListener, tableHeader, new FastTableCellFactory(automationElementFinder.AutomationElement, actionListener));
            }

            private List<AutomationElement> GetRowElements()
            {
                // this will find only first level children of our element - rows
                var dataItems = automationElementFinder.Children(AutomationSearchCondition.ByControlType(ControlType.DataItem));
                return dataItems;
            }

            public int NumberOfRows
            {
                get { return GetRowElements().Count; }
            }
        }

        public class FastTableCellFactory : TableCellFactory
        {
            private readonly AutomationElement tableElement;
            private readonly ActionListener actionListener;

            public FastTableCellFactory(AutomationElement tableElement, ActionListener actionListener)
                : base(tableElement, actionListener)
            {
                this.tableElement = tableElement;
                this.actionListener = actionListener;
            }

            public override TableCells CreateCells(TableHeader tableHeader, AutomationElement rowElement)
            {
                List<AutomationElement> tableCellElements = new AutomationElementFinder(rowElement).Children(AutomationSearchCondition.ByControlType(ControlType.DataItem));
                return new TableCells(tableCellElements, tableHeader, actionListener);
            }
        }
        #endregion
    }

    public static class WhiteExtensionMethods
    {
        /// <summary>
        /// Retrieves a White element using RawElementBasedSearch with the given MaxElementSearchDepth, which can be much faster than a normal search for large control trees.
        /// </summary>
        public static T RawGet<T>(this Window window, SearchCriteria criteria, int searchDepth) where T : UIItem
        {
            using(CoreAppXmlConfiguration.Instance.ApplyTemporarySetting(config =>
            {
                config.RawElementBasedSearch = true;
                config.MaxElementSearchDepth = searchDepth;
            }))
            {
                return window.Get<T>(criteria);
            }
        }

        /// <summary>
        /// Retrieves a White element using RawElementBasedSearch with the given MaxElementSearchDepth, which can be much faster than a normal search for large control trees.
        /// </summary>
        public static T RawGet<T>(this AutomationElement element, SearchCriteria criteria, int searchDepth) where T : UIItem
        {
            using (CoreAppXmlConfiguration.Instance.ApplyTemporarySetting(config =>
            {
                config.RawElementBasedSearch = true;
                config.MaxElementSearchDepth = searchDepth;
            }))
            {
                var l = new NullActionListener();
                var t = typeof(T);
                var factory = new PrimaryUIItemFactory(new AutomationElementFinder(element));
                var e = factory.Create(criteria, l);
                return e as T;
            }
        }

        /// <summary>
        /// Retrieves a White element using RawElementBasedSearch with the given MaxElementSearchDepth, which can be much faster than a normal search for large control trees.
        /// </summary>
        public static T RawGet<T>(this CustomUIItem item, SearchCriteria criteria, int searchDepth = 3) where T : UIItem
        {
            return RawGet<T>(item.AutomationElement, criteria, searchDepth);
        }

        /// <summary>
        /// Retrieves a White element using RawElementBasedSearch with the given MaxElementSearchDepth, which can be much faster than a normal search for large control trees.
        /// </summary>
        public static T RawGet<T>(this CustomUIItem item, string id, int searchDepth = 3) where T : UIItem
        {
            return RawGet<T>(item.AutomationElement, SearchCriteria.ByAutomationId(id), searchDepth);
        }

        public static Table GetFastTable(this UIItem item, string id, int searchDepth = 3)
        {
            var element = item.GetElement(SearchCriteria.ByAutomationId(id).AndControlType(ControlType.Table));
            if (element == null)
                return null;
            return new FastTable(element, new NullActionListener());
        }

        public class TableRowHeaderCell : TableCell
        {
            public TableRowHeaderCell(AutomationElement e, ActionListener l) : base(e, l) { }

            public override System.Windows.Point ClickablePoint
            {
                get
                {
                    return new System.Windows.Point(Bounds.X + Bounds.Width / 2, Bounds.Y + Bounds.Height / 2);
                }
            }
        }

        /// <summary>
        /// Retrieves the header cell of a TableRow, since White does not make this accessible.
        /// </summary>
        /// <param name="row"></param>
        /// <returns></returns>
        public static TableRowHeaderCell GetHeaderCell(this TableRow row)
        {
            var header = row.AutomationElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.HeaderItem));
            if (header != null)
                return new TableRowHeaderCell(header, new NullActionListener());
            return null;
        }

        /// <summary>
        /// Waits the specified time (default 15 seconds) for the given TextBox to show "Ready"
        /// </summary>
        public static void WaitForReady(this TextBox statusTextBox, int maxMillisecondsToWait = 15000)
        {
            const int msToWait = 200;

            // FIXME: depends on English text
            for (int i = 0; i < maxMillisecondsToWait; i += msToWait)
            {
                if (statusTextBox.Text == "Ready")
                    return;

                Thread.Sleep(msToWait);
            }

            throw new TimeoutException("timeout waiting for status to return to 'Ready'");
        }

        public static void ClickAndWaitWhileBusy(this UIItem item, Window waitOnWindow)
        {
            item.Click();
            waitOnWindow.Mouse.Location = waitOnWindow.TitleBar.Location;
            waitOnWindow.WaitWhileBusy();
            Thread.Sleep(500);
        }

        /// <summary>
        /// Clears any existing text in the (editable) TextBox and enters the new text.
        /// </summary>
        public static void ClearAndEnter(this TextBox editTextBox, string text)
        {
            editTextBox.DoubleClick(); // select all
            editTextBox.KeyIn(TestStack.White.WindowsAPI.KeyboardInput.SpecialKeys.DELETE); // clear
            editTextBox.Enter(text);
        }

        /// <summary>
        /// Toggles a TreeNode's check state. If the node is not checkable, it does nothing.
        /// </summary>
        public static void Toggle(this TreeNode checkableTreeNode)
        {
            object togglePattern;
            if (!checkableTreeNode.AutomationElement.TryGetCurrentPattern(TogglePatternIdentifiers.Pattern, out togglePattern))
                return; // TODO: log not toggleable
            (togglePattern as TogglePattern).Toggle();
        }

        /// <summary>
        /// Selects a TreeNode and presses SPACE on it to toggle it and trigger any events based on that. If the node is not checkable or selectable, it does nothing.
        /// </summary>
        public static void SelectAndToggle(this TreeNode checkableTreeNode)
        {
            object selectPattern;
            if (!checkableTreeNode.AutomationElement.TryGetCurrentPattern(SelectionItemPatternIdentifiers.Pattern, out selectPattern))
                return; // TODO: log not toggleable
            (selectPattern as SelectionItemPattern).Select();
            checkableTreeNode.KeyIn(TestStack.White.WindowsAPI.KeyboardInput.SpecialKeys.SPACE); // clear
        }

        /// <summary>
        /// Toggles a TreeNode's check state. If the node is not checkable, it does nothing.
        /// </summary>
        public static bool IsChecked(this TreeNode checkableTreeNode)
        {
            object togglePattern;
            if (!checkableTreeNode.AutomationElement.TryGetCurrentPattern(TogglePatternIdentifiers.Pattern, out togglePattern))
                return false; // TODO: log not toggleable
            return (togglePattern as TogglePattern).Current.ToggleState == ToggleState.On;
        }

        /// <summary>
        /// Returns the values of the row's cells as a concatenated string delimited by the given character; empty cells use the given placeholder, and the row header can be optionally included;
        /// if forceRefresh is true, then the cell values are retrieved individually rather than using the UIAutomation ValuePattern.
        /// </summary>
        public static string GetValuesAsString(this TableRow row, string emptyStringPlaceholder = "", string cellDelimiter = ";", bool includeRowHeader = false)
        {
            var rowString = row.AutomationElement.GetCurrentPropertyValue(ValuePatternIdentifiers.ValueProperty).ToString();
            if (rowString.Length == 0)
                rowString = row.AutomationElement.Current.Name;
            var rowValues = rowString.Split(';').Select(o => o == String.Empty ? emptyStringPlaceholder : o);
            if (!includeRowHeader && rowValues.Count() > row.Cells.Count)
                rowValues = rowValues.Skip(1);
            return String.Join(cellDelimiter, rowValues);
        }

        /// <summary>
        /// Get the values from the options menu of an existing IDPicker Application instance (the Settings are not directly accessible from a separate process)
        /// </summary>
        public static IDPickerAllSettings GetSettings(this Application app, Stack<Window> windowStack)
        {
            var settings = new IDPickerAllSettings();

            var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
            windowStack.Push(window);

            var menu = window.RawGet<MenuBar>(SearchCriteria.ByAutomationId("menuStrip1"), 2);
            //menu.MenuItemBy(SearchCriteria.ByAutomationId("toolsToolStripMenuItem"), SearchCriteria.ByAutomationId("optionsToolStripMenuItem")).Click();
            menu.MenuItem("Tools", "Options...").RaiseClickEvent(); // FIXME: not localized, but the AutomationIds aren't being set properly so the above line won't work

            var options = window.ModalWindow(SearchCriteria.ByAutomationId("DefaultSettingsManagerForm"), InitializeOption.WithCache);
            windowStack.Push(options);

            settings.GeneralSettings.DefaultMinSpectraPerDistinctMatch = Convert.ToInt32(options.Get<TextBox>("minSpectraPerMatchTextBox").Text);
            settings.GeneralSettings.DefaultMinSpectraPerDistinctPeptide = Convert.ToInt32(options.Get<TextBox>("minSpectraPerPeptideTextBox").Text);
            settings.GeneralSettings.DefaultMaxProteinGroupsPerPeptide = Convert.ToInt32(options.Get<TextBox>("maxProteinGroupsTextBox").Text);
            settings.GeneralSettings.DefaultMinSpectra = Convert.ToInt32(options.Get<TextBox>("minSpectraTextBox").Text);
            settings.GeneralSettings.DefaultMinDistinctPeptides = Convert.ToInt32(options.Get<TextBox>("minDistinctPeptidesTextBox").Text);
            settings.GeneralSettings.DefaultMinAdditionalPeptides = Convert.ToInt32(options.Get<TextBox>("minAdditionalPeptidesTextBox").Text);
            settings.GeneralSettings.DefaultMaxRank = Convert.ToInt32(options.Get<TextBox>("maxImportRankTextBox").Text);

            settings.GeneralSettings.DefaultMaxFDR = Convert.ToDouble(options.Get<ComboBox>("maxQValueComboBox").EditableText) / 100;
            settings.GeneralSettings.DefaultMaxImportFDR = Convert.ToDouble(options.Get<ComboBox>("maxImportFdrComboBox").EditableText) / 100;

            settings.GeneralSettings.DefaultDecoyPrefix = options.Get<TextBox>("defaultDecoyPrefixTextBox").Text;
            settings.GeneralSettings.DefaultIgnoreUnmappedPeptides = options.Get<CheckBox>("ignoreUnmappedPeptidesCheckBox").Checked;

            settings.GeneralSettings.DefaultGeneLevelFiltering = options.Get<CheckBox>("filterByGeneCheckBox").Checked;
            settings.GeneralSettings.DefaultChargeIsDistinct = options.Get<CheckBox>("chargeIsDistinctCheckBox").Checked;
            settings.GeneralSettings.DefaultAnalysisIsDistinct = options.Get<CheckBox>("analysisIsDistinctCheckBox").Checked;
            settings.GeneralSettings.DefaultModificationsAreDistinct = options.Get<CheckBox>("modificationsAreDistinctCheckbox").Checked;
            settings.GeneralSettings.DefaultModificationRoundToNearest = Convert.ToDecimal(options.Get<TextBox>("modificationRoundToMassTextBox").Text);

            //settings.GeneralSettings.FastaPaths.Clear(); settings.GeneralSettings.FastaPaths.AddRange(lbFastaPaths.Items.OfType<string>().ToArray());
            //settings.GeneralSettings.SourcePaths.Clear(); settings.GeneralSettings.SourcePaths.AddRange(lbSourcePaths.Items.OfType<string>().ToArray());

            settings.GeneralSettings.SourceExtensions = options.Get<TextBox>("sourceExtensionsTextBox").Text;

            settings.GUISettings.WarnAboutNonFixedDrive = options.Get<CheckBox>("nonFixedDriveWarningCheckBox").Checked;
            settings.GUISettings.WarnAboutNoGeneMetadata = options.Get<CheckBox>("embedGeneMetadataWarningCheckBox").Checked;

            options.Get<Button>("btnOk").RaiseClickEvent();
            windowStack.Pop();

            return settings;
        }

        public static void SetSettings(this Application app, Stack<Window> windowStack, IDPickerAllSettings settings)
        {
            var window = app.GetWindow(SearchCriteria.ByAutomationId("IDPickerForm"), InitializeOption.NoCache);
            windowStack.Push(window);

            var menu = window.RawGet<MenuBar>(SearchCriteria.ByAutomationId("menuStrip1"), 2);
            //menu.MenuItemBy(SearchCriteria.ByAutomationId("toolsToolStripMenuItem"), SearchCriteria.ByAutomationId("optionsToolStripMenuItem")).Click();
            menu.MenuItem("Tools", "Options...").RaiseClickEvent(); // FIXME: not localized, but the AutomationIds aren't being set properly so the above line won't work

            var options = window.ModalWindow(SearchCriteria.ByAutomationId("DefaultSettingsManagerForm"), InitializeOption.WithCache);
            windowStack.Push(options);

            options.Get<TextBox>("minSpectraPerMatchTextBox").Text = settings.GeneralSettings.DefaultMinSpectraPerDistinctMatch.ToString();
            options.Get<TextBox>("minSpectraPerPeptideTextBox").Text = settings.GeneralSettings.DefaultMinSpectraPerDistinctPeptide.ToString();
            options.Get<TextBox>("maxProteinGroupsTextBox").Text = settings.GeneralSettings.DefaultMaxProteinGroupsPerPeptide.ToString();
            options.Get<TextBox>("minSpectraTextBox").Text = settings.GeneralSettings.DefaultMinSpectra.ToString();
            options.Get<TextBox>("minDistinctPeptidesTextBox").Text = settings.GeneralSettings.DefaultMinDistinctPeptides.ToString();
            options.Get<TextBox>("minAdditionalPeptidesTextBox").Text = settings.GeneralSettings.DefaultMinAdditionalPeptides.ToString();
            options.Get<TextBox>("maxImportRankTextBox").Text = settings.GeneralSettings.DefaultMaxRank.ToString();

            options.Get<ComboBox>("maxQValueComboBox").EditableText = (settings.GeneralSettings.DefaultMaxFDR * 100).ToString();
            options.Get<ComboBox>("maxImportFdrComboBox").EditableText = (settings.GeneralSettings.DefaultMaxImportFDR * 100).ToString();

            options.Get<TextBox>("defaultDecoyPrefixTextBox").Text = settings.GeneralSettings.DefaultDecoyPrefix;
            options.Get<CheckBox>("ignoreUnmappedPeptidesCheckBox").Checked = settings.GeneralSettings.DefaultIgnoreUnmappedPeptides;

            options.Get<CheckBox>("filterByGeneCheckBox").Checked = settings.GeneralSettings.DefaultGeneLevelFiltering;
            options.Get<CheckBox>("chargeIsDistinctCheckBox").Checked = settings.GeneralSettings.DefaultChargeIsDistinct;
            options.Get<CheckBox>("analysisIsDistinctCheckBox").Checked = settings.GeneralSettings.DefaultAnalysisIsDistinct;
            options.Get<CheckBox>("modificationsAreDistinctCheckbox").Checked = settings.GeneralSettings.DefaultModificationsAreDistinct;
            options.Get<TextBox>("modificationRoundToMassTextBox").Text = settings.GeneralSettings.DefaultModificationRoundToNearest.ToString();

            //settings.GeneralSettings.FastaPaths.Clear(); settings.GeneralSettings.FastaPaths.AddRange(lbFastaPaths.Items.OfType<string>().ToArray());
            //settings.GeneralSettings.SourcePaths.Clear(); settings.GeneralSettings.SourcePaths.AddRange(lbSourcePaths.Items.OfType<string>().ToArray());

            options.Get<TextBox>("sourceExtensionsTextBox").Text = settings.GeneralSettings.SourceExtensions;

            options.Get<CheckBox>("nonFixedDriveWarningCheckBox").Checked = settings.GUISettings.WarnAboutNonFixedDrive;
            options.Get<CheckBox>("embedGeneMetadataWarningCheckBox").Checked = settings.GUISettings.WarnAboutNoGeneMetadata;

            options.Get<Button>("btnOk").RaiseClickEvent();
            windowStack.Pop();
        }

        /// <summary>
        /// Launch IDPicker without any arguments and return the values from the options menu (the Settings are not directly accessible from a separate process)
        /// </summary>
        public static IDPickerAllSettings GetSettings(this TestContext testContext)
        {
            var settings = new IDPickerAllSettings();

            testContext.LaunchAppTest("IDPicker.exe", "", (app, windowStack) => { settings = app.GetSettings(windowStack); });

            return settings;
        }

        /// <summary>
        /// Launch IDPicker without any arguments and set up the options menu according to the given settings parameter (the Settings are not directly accessible from a separate process)
        /// </summary>
        public static void SetSettings(this TestContext testContext, IDPickerAllSettings settings)
        {
            testContext.LaunchAppTest("IDPicker.exe", "", (app, windowStack) => { app.SetSettings(windowStack, settings); });
        }

        public static IDPickerAllSettings GetAndSetTestSettings(this Application app, Stack<Window> windowStack)
        {
            var settings = new IDPickerAllSettings();
            SetSettings(app, windowStack, settings);
            return settings;
        }

        public static IDPickerAllSettings GetAndSetTestSettings(this TestContext testContext)
        {
            IDPickerAllSettings settings = null;
            testContext.LaunchAppTest("IDPicker.exe", "", (app, windowStack) => { settings = app.GetAndSetTestSettings(windowStack); });
            return settings;
        }
    }
}
