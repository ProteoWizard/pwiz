/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using LogEntryMessages = pwiz.SkylineTestUtil.AuditLogUtil.LogEntryMessages;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class AuditLogTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestAuditLogLocalization()
        {
            // Verify localized string parsing

            // Same name but different resource file
            VerifyStringLocalization(PropertyNames.ViewSpecList_Views, "{0:ViewSpecList_Views}");
            VerifyStringLocalization(PropertyElementNames.ViewSpecList_Views, "{1:ViewSpecList_Views}");

            // Empty curly braces
            VerifyStringLocalization("{}", "{}");

            // Multiple strings
            VerifyStringLocalization(
                PropertyNames.Settings + AuditLogStrings.PropertySeparator + PropertyNames.SrmSettings_TransitionSettings,
                "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}");

            // UI mode awareness
            foreach (SrmDocument.DOCUMENT_TYPE modeUI in Enum.GetValues(typeof(SrmDocument.DOCUMENT_TYPE)))
            {
                var local = PropertyNames.SrmSettings_PeptideSettings + AuditLogStrings.TabSeparator +
                            PropertyNames.PeptideSettings_Libraries + AuditLogStrings.PropertySeparator +
                            PropertyNames.PeptideLibraries_LibrarySpecs;
                local = Helpers.PeptideToMoleculeTextMapper.Translate(local, modeUI);
                VerifyStringLocalization(
                    local,
                    "{0:SrmSettings_PeptideSettings}{2:TabSeparator}{0:PeptideSettings_Libraries}{2:PropertySeparator}{0:PeptideLibraries_LibrarySpecs}", modeUI);

                // Verify that certain log entries are ui mode invariate
                var entry = AuditLogEntry.CreateSingleMessageEntry(
                    new MessageInfo(MessageType.added_new_peptide_group_from_background_proteome,
                        modeUI, "foo"), "bar");
                Assume.AreEqual("Added peptide group foo from background proteome in the Targets window", entry.AllInfo.First().EnExpanded);
            }

            // Non existent resource name
            VerifyStringLocalization("{0:SEttings}", "{0:SEttings}");

            var unlocalizedMessageTypes = GetUnlocalizedMessageTypes();
            if (unlocalizedMessageTypes.Any())
                Assert.Fail("The following properties are unlocalized:\n" + string.Join("\n", unlocalizedMessageTypes));

            //var unlocalized = GetUnlocalizedProperties(RootProperty.Create(typeof(SrmSettings), "Settings"), PropertyPath.Root);
            var unlocalized = GetAllUnlocalizedProperties(typeof(AuditLogEntry))
                .Concat(GetAllUnlocalizedProperties(typeof(RowItem)))
                .Concat(GetAllUnlocalizedProperties(typeof(ImmutableList))).ToList();
            if (unlocalized.Any())
                Assert.Fail("The following properties are unlocalized:\n" + string.Join("\n", unlocalized));
        }

        private void VerifyStringLocalization(string expected, string unlocalized, SrmDocument.DOCUMENT_TYPE modeUI = SrmDocument.DOCUMENT_TYPE.none)
        {
            Assert.AreEqual(expected, LogMessage.ParseLogString(unlocalized, LogLevel.all_info, modeUI));
        }

        private class EnumNameSuggestionPair
        {
            public EnumNameSuggestionPair(string name, string suggestion = null)
            {
                Name = name;

                if (suggestion != null)
                {
                    Suggestion = suggestion;
                    return;
                }

                var index = name.IndexOf('_');
                var value = name.Substring(index + 1);

                Suggestion = string.Join(" ",
                    value.Split('_').Select(v =>
                    {
                        if (v.Length > 0)
                            return v[0].ToString().ToUpper() + v.Substring(1);

                        return v;
                    }));

                for (var i = 1; i < Suggestion.Length; ++i)
                {
                    if (char.IsUpper(Suggestion[i]) && Suggestion[i - 1] != ' ')
                    {
                        Suggestion = Suggestion.Substring(0, i) + ' ' + Suggestion.Substring(i);
                        ++i;
                    }
                }

                Suggestion = Suggestion.Trim();
            }

            public string Name { get; private set; }
            public string Suggestion { get; private set; }
        }

        public List<UnlocalizedProperty> GetAllUnlocalizedProperties(Type typeInAssembly)
        {
            var unlocalizedProperties = new List<UnlocalizedProperty>();

            var types = Assembly.GetAssembly(typeInAssembly).GetTypes();

            foreach (var classType in types)
            {
                if (classType.ContainsGenericParameters)
                    continue;

                try
                {
                    try
                    {
                        if (typeof(AuditLogOperationSettings<>).MakeGenericType(classType).IsAssignableFrom(classType))
                        {
                            var localized = PropertyNames.ResourceManager.GetString(classType.Name);
                            if (localized == null)
                                unlocalizedProperties.Add(new UnlocalizedProperty(classType.Name));
                        }
                    }
                    catch(ArgumentException)
                    {
                        // ignored
                    }

                    var enumNameSuggestionPairs = new List<EnumNameSuggestionPair>();

                    if (classType.BaseType != null && classType.BaseType.GenericTypeArguments.Length == 1)
                    {
                        var namedValues =
                            typeof(LabeledValues<>).MakeGenericType(classType.BaseType
                                .GenericTypeArguments[0]);

                        if (namedValues.IsAssignableFrom(classType))
                        {
                            var fields = classType.GetFields(BindingFlags.Public | BindingFlags.Static)
                                .Where(field => field.FieldType == classType).ToArray();

                            for (var j = 0; j < fields.Length; j++)
                            {
                                var val = fields[j].GetValue(null);

                                if (!(bool)classType.GetProperty("RequiresAuditLogLocalization", BindingFlags.Public | BindingFlags.Instance).GetValue(val))
                                    continue;

                                var invariantName = (string)
                                    classType.GetProperty("InvariantName",
                                        BindingFlags.NonPublic | BindingFlags.Instance).GetValue(val);
                                var suggestion = (string) classType.GetProperty("Label", BindingFlags.Public | BindingFlags.Instance).GetValue(val);
                                enumNameSuggestionPairs.Add(new EnumNameSuggestionPair(val.GetType().Name + '_' + invariantName, suggestion));
                            }
                        }
                    }

                    var properties = Reflector.GetProperties(classType);
                    if (properties == null)
                        continue;

                    for (var i = 0; i < properties.Count; i++)
                    {
                        var property = properties[i];

                        var names = new List<string>();
                        var localizer = property.CustomLocalizer;
                        if (localizer != null)
                        {
                            names.AddRange(localizer.PossibleResourceNames.Select(name => property.DeclaringType.Name + '_' + name));
                        }
                        else if (!property.IgnoreName)
                        {
                            names.Add(property.DeclaringType.Name + '_' + property.PropertyName);
                        }

                        if (property.PropertyType.IsEnum)
                        {
                            var ignoreEnumValues = (Attribute.GetCustomAttributes(property.PropertyType,
                                                       typeof(IgnoreEnumValuesAttribute), true).FirstOrDefault() as IgnoreEnumValuesAttribute) ?? IgnoreEnumValuesAttribute.NONE;

                            enumNameSuggestionPairs.AddRange(property.PropertyType.GetEnumValues().OfType<object>()
                                .Where(v => !ignoreEnumValues.ShouldIgnore(v))
                                .Select(name => new EnumNameSuggestionPair(property.PropertyType.Name + '_' + name.ToString())));
                        }

                        foreach (var name in names)
                        {
                            var localized = PropertyNames.ResourceManager.GetString(name);
                            if (localized == null)
                                unlocalizedProperties.Add(new UnlocalizedProperty(name));
                        }
                    }

                    foreach (var enumPair in enumNameSuggestionPairs)
                    {
                        var localized = EnumNames.ResourceManager.GetString(enumPair.Name);
                        if (localized == null)
                            unlocalizedProperties.Add(new UnlocalizedEnumValue(enumPair));
                    }
                }
                catch (Exception ex)
                {
                    Assert.Fail(ex.Message + Environment.NewLine + ex.StackTrace);
                }
            }

            return unlocalizedProperties;
        }

        private List<UnlocalizedProperty> GetUnlocalizedMessageTypes()
        {
            var result = new List<UnlocalizedProperty>();
            var values = Enum.GetValues(typeof(MessageType)).Cast<MessageType>().Skip(1); // Skip "none"
            foreach (var enumVal in values)
            {
                var str = AuditLogStrings.ResourceManager.GetString(enumVal.ToString());
                if (str == null)
                    result.Add(new UnlocalizedProperty(enumVal.ToString()));
            }

            return result;
        }

        /// <summary>
        /// Verifies that all diff properties of T are localized, unless their name can be ignored
        /// or a custom localizer is provided
        /// </summary>
        /*private List<UnlocalizedProperty> GetUnlocalizedProperties(Property prop, PropertyPath path)
        {
            var T = prop.GetPropertyType(ObjectPair<object>.Create(null, null));
            var properties = Reflector.GetProperties(T);

            var unlocalizedProperties = new List<UnlocalizedProperty>();
            for (var i = 0; i < properties.Count; i++)
            {
                var property = properties[i];
                var subPath = path.Property(property.PropertyName);
                if (!property.IgnoreName)
                {
                    string[] names;
                    var localizer = property.CustomLocalizer;
                    if (localizer != null)
                    {
                        names = localizer.PossibleResourceNames;
                    }
                    else
                    {
                        names = new[] {property.DeclaringType.Name + "_" + property.PropertyName};
                    }

                    foreach (var name in names)
                    {
                        var localized = PropertyNames.ResourceManager.GetString(name);
                        if (localized == null)
                        {
                            var propPath = property.CustomLocalizer != null ? PropertyPath.Parse(name) : subPath;
                            unlocalizedProperties.Add(new UnlocalizedProperty(name, propPath));
                        }
                    }
                }

                var type = property.GetPropertyType(ObjectPair<object>.Create(null, null));
                var collection = Reflector.GetCollectionInfo(type, ObjectPair<object>.Create(null, null));
                if (collection != null)
                {
                    type = collection.Info.ElementValueType;
                    property = property.ChangeTypeOverride(type);
                }

                // The reflector will fail with a non class type because of the type restriction
                // on Reflector<T>
                if (type.IsClass)
                    unlocalizedProperties.AddRange(GetUnlocalizedProperties(property, subPath));
            }

            return unlocalizedProperties;
        }*/

        public class UnlocalizedProperty
        {
            public UnlocalizedProperty(string unlocalizedString, PropertyPath propertyPath = null)
            {
                UnlocalizedString = unlocalizedString;
                PropertyPath = propertyPath;
            }

            public string UnlocalizedString { get; private set; }
            public PropertyPath PropertyPath { get; private set; }  

            public override string ToString()
            {
                if (PropertyPath != null)
                    return string.Format("{0} ({1})", UnlocalizedString, PropertyPath);
                else
                    return UnlocalizedString;
            }
        }

        private class UnlocalizedEnumValue : UnlocalizedProperty
        {
            private readonly string _suggestion;

            public UnlocalizedEnumValue(EnumNameSuggestionPair enumPair, PropertyPath propertyPath = null) : base(
                enumPair.Name, propertyPath)
            {
                _suggestion = enumPair.Suggestion;
            }

            public override string ToString()
            {
                return "Enum: " + base.ToString() + " Suggestion: " + _suggestion;
            }
        }

        [TestMethod]
        public void TestAuditLog()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        private static bool IsRecordMode { get { return false; } }

        protected override void DoTest()
        {
            OpenDocument(@"Rat_plasma.sky");
            // Test audit log messages
            LogEntry.ResetLogEntryCount();
            CollectionUtil.ForEach(LOG_ENTRIES, e => { e.Verify(); });

            if(IsRecordMode)
                Assert.Fail("Successfully recorded data");

            /*// Test audit log clear
            Assert.AreEqual(LOG_ENTRY_MESSAGESES.Length, LogEntry.GetAuditLogEntryCount());
            RunUI(() => SkylineWindow.ClearAuditLog());
            Assert.AreEqual(1, LogEntry.GetAuditLogEntryCount());
            // Clearing the audit log can be undone
            RunUI(() => SkylineWindow.Undo());
            Assert.AreEqual(LOG_ENTRY_MESSAGESES.Length, LogEntry.GetAuditLogEntryCount());*/
                
            // Test UI
            RunUI(() => SkylineWindow.ShowAuditLog());
            var auditLogForm = WaitForOpenForm<AuditLogForm>();
            Assert.IsNotNull(auditLogForm);
            RunUI(() => auditLogForm.ChooseView(AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo));
            AuditLogUtil.WaitForAuditLogForm(auditLogForm);

            // Make sure built in views are set up correctly
            ViewSpec[] builtInViews = null;
            RunUI(() => builtInViews = ((AbstractViewContext)auditLogForm.BindingListSource.ViewContext).BuiltInViews.ToArray());
            var expectedViewNames = new[] { AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo, AuditLogStrings.AuditLogForm_MakeAuditLogForm_Summary, AuditLogStrings.AuditLogForm_MakeAuditLogForm_All_Info };
            var expectedColumns = new[]
            {
                new[] {"Time", "UndoRedoMessage", "Reason"},
                new[] {"Time", "SummaryMessage", "Reason"},
                new[] {"Time", "Details!*.AllInfoMessage", "Reason" }
            };
            Assert.AreEqual(expectedViewNames.Length, builtInViews.Length);

            for (var i = 0; i < expectedViewNames.Length; ++i)
            {
                Assert.AreEqual(expectedViewNames[i], builtInViews[i].Name);
                Assert.AreEqual(expectedColumns[i].Length, builtInViews[i].Columns.Count);

                for (var j = 0; j < builtInViews[i].Columns.Count; ++j)
                    Assert.AreEqual(expectedColumns[i][j], builtInViews[i].Columns[j].Name);
            }

            AuditLogUtil.WaitForAuditLogForm(auditLogForm);
            // Verify that the audit log rows in the grid correspond to the audit log entries
            RunUI(() =>
            {
                Assert.AreEqual(LOG_ENTRY_MESSAGESES.Length, auditLogForm.BindingListSource.Count);
                for (var i = 0; i < auditLogForm.BindingListSource.Count; i++)
                {
                    var rowItem = auditLogForm.BindingListSource[i] as RowItem;
                    Assert.IsNotNull(rowItem);
                    var row = rowItem.Value as AuditLogRow;
                    Assert.IsNotNull(row);


                    var logEntryMsg = LOG_ENTRY_MESSAGESES[ReverseRowIndex(auditLogForm, i)];
                    Assert.AreEqual(logEntryMsg.ExpectedSummary.ToString(), row.SummaryMessage.Text);
                    Assert.AreEqual(logEntryMsg.ExpectedUndoRedo.ToString(), row.UndoRedoMessage.Text);

                    if (logEntryMsg.ExpectedAllInfo.Length != row.Details.Count)
                    {
                        Assert.Fail("Expected: " +
                                    string.Join(",\n", logEntryMsg.ExpectedAllInfo.Select(l => l.ToString())) +
                                    "\nActual: " + string.Join(",\n", row.Details.Select(d => d.AllInfoMessage)));
                    }

                    for (var j = 0; j < row.Details.Count; ++j)
                        Assert.AreEqual(logEntryMsg.ExpectedAllInfo[j].ToString(), row.Details[j].AllInfoMessage.Text);

                }
            });

            // Change to a view that shows the reason
            RunUI(() =>
            {
                var descriptor = ColumnDescriptor.RootColumn(auditLogForm.ViewInfo.DataSchema, typeof(AuditLogRow));
                auditLogForm.ViewInfo = new ViewInfo(descriptor,
                    auditLogForm.ViewInfo.ViewSpec.SetColumns(new[]
                    {
                        new ColumnSpec(PropertyPath.Parse("Id")),
                        new ColumnSpec(PropertyPath.Parse("UndoRedoMessage")),
                        new ColumnSpec(PropertyPath.Parse("Reason")),
                        new ColumnSpec(PropertyPath.Parse("Details!*.DetailReason"))
                    }).SetName("Reason View"));
            });

            AuditLogUtil.WaitForAuditLogForm(auditLogForm);

            RunUI(() =>
            {
                var propertyDescriptor =
                    auditLogForm.DataboundGridControl.GetPropertyDescriptor(FindDocumentGridColumn(auditLogForm, "Id"));
                auditLogForm.DataboundGridControl.SetSortDirection(propertyDescriptor, ListSortDirection.Descending);
            });

            AuditLogUtil.WaitForAuditLogForm(auditLogForm);

            // Verify that changing the reason of a row correctly modifies the audit log entries in the document
            RunUI(() =>
            {
                // (Precursor mass changed to "Average" row) Changing the reason of this row should change the reason of its detail row and vice versa
                ChangeReason(auditLogForm, "Reason", 1, "Reason 1");
            });

            AuditLogUtil.WaitForAuditLogForm(auditLogForm);
            RunUI(() =>
            {
                var entry = GetAuditLogEntryFromRow(auditLogForm, 1);
                Assert.AreEqual("Reason 1", entry.Reason);
                ChangeReason(auditLogForm, "Details!*.DetailReason", 1, "Reason 2");
                
            });
            AuditLogUtil.WaitForAuditLogForm(auditLogForm);
            RunUI(() =>
            {
                Assert.AreEqual("Reason 2", GetAuditLogEntryFromRow(auditLogForm, 1).Reason);
                // (Collision Energy changed from Thermo to Thermo TSQ Q.) Changing the reason of this row should not change the reason of its detail row and vice versa
                ChangeReason(auditLogForm, "Reason", 3, "Reason 3");

            });
            AuditLogUtil.WaitForAuditLogForm(auditLogForm);
            RunUI(() =>
            {
                Assert.AreEqual("Reason 3", GetAuditLogEntryFromRow(auditLogForm, 3).Reason);
                Assert.IsTrue(GetAuditLogEntryFromRow(auditLogForm, 3).AllInfo
                    .All(l => string.IsNullOrEmpty(l.Reason)));
                ChangeReason(auditLogForm, "Details!*.DetailReason", 3, "Reason 4");

            });
            AuditLogUtil.WaitForAuditLogForm(auditLogForm);
            RunUI(() =>
            {
                Assert.AreEqual("Reason 3", GetAuditLogEntryFromRow(auditLogForm, 3).Reason);
                Assert.AreEqual("Reason 4", GetAuditLogEntryFromRow(auditLogForm, 3).AllInfo[1].Reason);
            });
        }

        private static AuditLogEntry GetAuditLogEntryFromRow(AuditLogForm form, int row)
        {
            if (form.BindingListSource[row] is RowItem rowItem)
            {
                switch (rowItem.Value)
                {
                    case AuditLogRow logRow:
                        return logRow.Entry;
                    case AuditLogDetailRow detailRow:
                        return detailRow.AuditLogRow.Entry;
                }
            }
            
            return null;
        }

        private static int ReverseRowIndex(AuditLogForm form, int row)
        {
            // Warning: only useful if row count matches audit log entry count
            return form.BindingListSource.Count - row - 1;
        }

        private void ChangeReason(AuditLogForm form, string columnName, int row, string reason)
        {
            var reasonCol = form.FindColumn(columnName);
            var cell = form.DataGridView.Rows[row].Cells[reasonCol.Index];
            form.DataGridView.CurrentCell = cell;
            form.DataGridView.BeginEdit(true);
            form.DataGridView.EditingControl.Text = reason;
            Assert.IsTrue(form.DataGridView.EndEdit());
        }

        public class LogEntry
        {
            private static int _expectedAuditLogEntryCount;

            public LogEntry(Action settingsChange, LogEntryMessages messages)
            {
                SettingsChange = settingsChange;
                ExpectedMessages = messages;
            }

            public static int GetAuditLogEntryCount()
            {
                var count = -1;
                RunUI(() => count = SkylineWindow.DocumentUI.AuditLog.AuditLogEntries.Count);
                return count;
            }

            public static void ResetLogEntryCount()
            {
                _expectedAuditLogEntryCount = 0;
            }

            public static AuditLogEntry GetNewestEntry()
            {
                var count = GetAuditLogEntryCount();
                if (count == 0)
                    return null;

                AuditLogEntry result = null;
                RunUI(() => result = SkylineWindow.DocumentUI.AuditLog.AuditLogEntries);
                return result;
            }

            public void Verify()
            {
                SettingsChange();

                var newestEntry = GetNewestEntry();
                //PauseTest(newestEntry.UndoRedo.ToString());

                if (IsRecordMode)
                {
                    Console.WriteLine(AuditLogUtil.AuditLogEntryToCode(newestEntry));
                    return;
                }

                if (ExpectedMessages == null)
                    return;

                ++_expectedAuditLogEntryCount;
                Assert.AreEqual(_expectedAuditLogEntryCount, GetAuditLogEntryCount());
                Assert.IsNotNull(newestEntry);

                ExpectedMessages.AssertEquals(newestEntry);

                // Undo-Redo doesn't affect these messages
                if (ExpectedMessages.ExpectedUndoRedo.Type != MessageType.log_enabled &&
                    ExpectedMessages.ExpectedUndoRedo.Type != MessageType.log_disabled)
                {
                    // Test Undo-Redo
                    RunUI(() => SkylineWindow.Undo());
                    Assert.AreEqual(_expectedAuditLogEntryCount - 1, GetAuditLogEntryCount());
                    RunUI(() => SkylineWindow.Redo());
                    Assert.AreEqual(_expectedAuditLogEntryCount, GetAuditLogEntryCount());
                    Assert.IsTrue(ReferenceEquals(newestEntry, GetNewestEntry()));
                }
            }

            public Action SettingsChange { get; set; }
            public LogEntryMessages ExpectedMessages { get; set; }
        }

        private static LogEntry[] CreateLogEnries()
        {
            return new [] {
                // Basic property change
                new LogEntry(() => RunUI(() => SkylineWindow.ChangeSettings(
                        SkylineWindow.DocumentUI.Settings.ChangeTransitionPrediction(p =>
                            p.ChangePrecursorMassType(MassType.Average)), true)), LOG_ENTRY_MESSAGESES[0]),

                // Collection change: named to named
                new LogEntry(() => RunUI(() => SkylineWindow.ChangeSettings(
                    SkylineWindow.DocumentUI.Settings.ChangeTransitionPrediction(p =>
                        p.ChangeCollisionEnergy(Settings.Default.CollisionEnergyList.First(c => c.Name == "Thermo TSQ Quantiva"))), true)), LOG_ENTRY_MESSAGESES[1]),

                // Collection change: null to named
                new LogEntry(() => RunUI(() => SkylineWindow.ChangeSettings(
                    SkylineWindow.DocumentUI.Settings.ChangeTransitionPrediction(p =>
                        p.ChangeDeclusteringPotential(Settings.Default.DeclusterPotentialList.First(c => c.Name == "SCIEX"))), true)), LOG_ENTRY_MESSAGESES[2]),

                // Collection change: multiple named elements with sub properties added
                new LogEntry(() => RunUI(() => SkylineWindow.ChangeSettings(
                    SkylineWindow.DocumentUI.Settings.ChangeTransitionFilter(p =>
                        p.ChangeMeasuredIons(new[] { Settings.Default.MeasuredIonList[0], Settings.Default.MeasuredIonList[1] })), true)), LOG_ENTRY_MESSAGESES[3]),

                // Custom localizer 1
                // Removed for now due to localization of "Default" string. This gets checked by one of the later functions
                // anyways
                /*new LogEntry(() =>
                    {
                        var settings = SkylineWindow.DocumentUI.Settings.ChangeTransitionPrediction(p =>
                            p.ChangePrecursorMassType(MassType.Monoisotopic)); // Need monoisotopic masses for next change
                        settings = settings.ChangeTransitionFullScan(p =>
                            p.ChangePrecursorResolution(FullScanMassAnalyzerType.centroided, 10, null));

                        SkylineWindow.ChangeSettings(settings, true);
                    }, LOG_ENTRY_MESSAGESES[5]),
            
                // Custom localizer 2 and named to null change
                new LogEntry(() =>
                    {
                        SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeTransitionFullScan(f => f.ChangePrecursorResolution(FullScanMassAnalyzerType.qit, 0.7, null)), true);
                    }, LOG_ENTRY_MESSAGESES[6]),
                */
                // Undo redo shortened names removed
                new LogEntry(() =>
                    {
                        RunUI(() => SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeAnnotationDefs(l =>
                        {
                            var newList = new List<AnnotationDef>(l);
                            newList.RemoveAt(0);
                            return newList;
                        }), true));
                    }, LOG_ENTRY_MESSAGESES[4]),

                // Undo redo shortened names added
                new LogEntry(() =>
                    {
                        RunUI(() => SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeAnnotationDefs(l =>
                        {
                            var newList = new List<AnnotationDef>(l);
                            newList.Insert(0, Settings.Default.AnnotationDefList[0]);
                            return newList;
                        }), true));
                    }, LOG_ENTRY_MESSAGESES[5]),
            
                // Add Mixed Transition List
                /*new LogEntry(() =>
                    {
                        SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeDataSettings(d =>
                        {
                            var settingsViewSpecList = Settings.Default.PersistedViews.GetViewSpecList(PersistedViews.MainGroup.Id);
                            Assert.IsNotNull(settingsViewSpecList);
                            var viewSpecs = new List<ViewSpec>(d.ViewSpecList.ViewSpecs);
                            var viewLayouts = new List<ViewLayoutList>(d.ViewSpecList.ViewLayouts);
                        
                            var mixedTransitionList = settingsViewSpecList.ViewSpecs.FirstOrDefault(v => v.Name == Resources.SkylineViewContext_GetTransitionListReportSpec_Mixed_Transition_List);
                            Assert.IsNotNull(mixedTransitionList);
                            viewSpecs.Add(mixedTransitionList);
                            var newList = new ViewSpecList(viewSpecs, viewLayouts);
                            return d.ChangeViewSpecList(newList);
                        }), true);
                    }, LOG_ENTRY_MESSAGESES[7]),
                */
                // Isolation Scheme (also tests custom localizer)
                new LogEntry(() =>
                    {
                        RunUI(() => SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeTransitionSettings(t =>
                            {
                                return t.ChangeFullScan(t.FullScan.ChangeAcquisitionMethod(FullScanAcquisitionMethod.DIA,
                                    Settings.Default.IsolationSchemeList.First(i => i.Name == "SWATH (VW 64)")));
                            }), true));
                    }, LOG_ENTRY_MESSAGESES[6]),
            };
        }

        //Has to be defined prior to LOG_ENTRIES
        #region DATA
        private static readonly LogEntryMessages[] LOG_ENTRY_MESSAGESES =
        {
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_PrecursorMassType}",
                    "\"{6:MassType_Average}\""),
                new LogMessage(LogLevel.summary, MessageType.changed_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_PrecursorMassType}",
                    "\"{6:MassType_Average}\""),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.changed_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_PrecursorMassType}",
                        "\"{6:MassType_Average}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_PrecursorMassType}",
                        "\"{6:MassType_Monoisotopic}\"",
                        "\"{6:MassType_Average}\""),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}",
                    "\"Thermo\"",
                    "\"Thermo TSQ Quantiva\""),
                new LogMessage(LogLevel.summary, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}",
                    "\"Thermo\"",
                    "\"Thermo TSQ Quantiva\""),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}",
                        "\"Thermo\"",
                        "\"Thermo TSQ Quantiva\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}",
                        "\"Thermo\"",
                        "\"Thermo TSQ Quantiva\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}{2:PropertySeparator}{0:CollisionEnergyRegression_Conversions}",
                        "{ {0:ChargeRegressionLine_Charge} = {3:2}, {0:ChargeRegressionLine_Slope} = {3:0.0339}, {0:ChargeRegressionLine_Intercept} = {3:2.3597} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullCollisionEnergy}{2:PropertySeparator}{0:CollisionEnergyRegression_Conversions}",
                        "{ {0:ChargeRegressionLine_Charge} = {3:3}, {0:ChargeRegressionLine_Slope} = {3:0.0295}, {0:ChargeRegressionLine_Intercept} = {3:1.5123} }"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullDeclusteringPotential}",
                    "\"{2:None}\"",
                    "\"SCIEX\""),
                new LogMessage(LogLevel.summary, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullDeclusteringPotential}",
                    "\"{2:None}\"",
                    "\"SCIEX\""),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullDeclusteringPotential}",
                        "\"{2:None}\"",
                        "\"SCIEX\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullDeclusteringPotential}",
                        "\"{2:None}\"",
                        "\"SCIEX\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullDeclusteringPotential}{2:PropertySeparator}{0:NamedRegressionLine_Intercept}",
                        "{3:80}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullDeclusteringPotential}{2:PropertySeparator}{0:OptimizableRegression_StepSize}",
                        "{3:10}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_NonNullDeclusteringPotential}{2:PropertySeparator}{0:OptimizableRegression_StepCount}",
                        "{3:3}"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}"),
                new LogMessage(LogLevel.summary, MessageType.changed, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}"),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.changed, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}",
                        "\"N-terminal to Proline\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_Fragment}",
                        "\"P\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_Restrict}",
                        "{2:Missing}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_Terminus}",
                        "\"{6:SequenceTerminus_N}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_MinFragmentLength}",
                        "{3:3}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_IsFragment}",
                        "{3:True}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_SettingsCustomIon}",
                        "{2:Missing}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}",
                        "\"C-terminal to Glu or Asp\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_Fragment}",
                        "\"ED\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_Restrict}",
                        "{2:Missing}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_Terminus}",
                        "\"{6:SequenceTerminus_C}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_MinFragmentLength}",
                        "{3:3}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_IsFragment}",
                        "{3:True}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_SettingsCustomIon}",
                        "{2:Missing}"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.removed_from, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{1:DataSettings_AnnotationDefs}",
                    "\"SubjectId\""),
                new LogMessage(LogLevel.summary, MessageType.removed_from, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                    "\"SubjectId\""),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.removed_from, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{1:DataSettings_AnnotationDefs}",
                        "\"SubjectId\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.removed_from, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                        "\"SubjectId\""),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{1:DataSettings_AnnotationDefs}",
                    "\"SubjectId\""),
                new LogMessage(LogLevel.summary, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                    "\"SubjectId\""),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{1:DataSettings_AnnotationDefs}",
                        "\"SubjectId\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.added_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                        "\"SubjectId\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"SubjectId\"{2:PropertySeparator}{0:AnnotationDef_AnnotationTargets}",
                        "\"replicate\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.is_, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"SubjectId\"{2:PropertySeparator}{0:AnnotationDef_Type}",
                        "\"{6:AnnotationType_text}\""),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}"),
                new LogMessage(LogLevel.summary, MessageType.changed, SrmDocument.DOCUMENT_TYPE.proteomic, false,
                    "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}"),
                new[]
                {
                    new DetailLogMessage(LogLevel.undo_redo, MessageType.changed, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_AcquisitionMethod}",
                        "\"{6:FullScanAcquisitionMethod_None}\"",
                        "\"{6:FullScanAcquisitionMethod_DIA}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}",
                        "{2:Missing}",
                        "\"SWATH (VW 64)\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:400}, {0:IsolationWindow_End} = {3:409}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:409}, {0:IsolationWindow_End} = {3:416}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:416}, {0:IsolationWindow_End} = {3:423}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:423}, {0:IsolationWindow_End} = {3:430}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:430}, {0:IsolationWindow_End} = {3:437}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:437}, {0:IsolationWindow_End} = {3:444}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:444}, {0:IsolationWindow_End} = {3:451}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:451}, {0:IsolationWindow_End} = {3:458}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:458}, {0:IsolationWindow_End} = {3:465}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:465}, {0:IsolationWindow_End} = {3:471}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:471}, {0:IsolationWindow_End} = {3:477}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:477}, {0:IsolationWindow_End} = {3:483}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:483}, {0:IsolationWindow_End} = {3:489}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:489}, {0:IsolationWindow_End} = {3:495}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:495}, {0:IsolationWindow_End} = {3:501}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:501}, {0:IsolationWindow_End} = {3:507}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:507}, {0:IsolationWindow_End} = {3:514}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:514}, {0:IsolationWindow_End} = {3:521}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:521}, {0:IsolationWindow_End} = {3:528}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:528}, {0:IsolationWindow_End} = {3:535}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:535}, {0:IsolationWindow_End} = {3:542}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:542}, {0:IsolationWindow_End} = {3:549}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:549}, {0:IsolationWindow_End} = {3:556}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:556}, {0:IsolationWindow_End} = {3:563}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:563}, {0:IsolationWindow_End} = {3:570}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:570}, {0:IsolationWindow_End} = {3:577}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:577}, {0:IsolationWindow_End} = {3:584}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:584}, {0:IsolationWindow_End} = {3:591}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:591}, {0:IsolationWindow_End} = {3:598}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:598}, {0:IsolationWindow_End} = {3:605}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:605}, {0:IsolationWindow_End} = {3:612}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:612}, {0:IsolationWindow_End} = {3:619}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:619}, {0:IsolationWindow_End} = {3:626}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:626}, {0:IsolationWindow_End} = {3:633}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:633}, {0:IsolationWindow_End} = {3:640}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:640}, {0:IsolationWindow_End} = {3:647}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:647}, {0:IsolationWindow_End} = {3:654}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:654}, {0:IsolationWindow_End} = {3:663}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:663}, {0:IsolationWindow_End} = {3:672}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:672}, {0:IsolationWindow_End} = {3:681}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:681}, {0:IsolationWindow_End} = {3:690}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:690}, {0:IsolationWindow_End} = {3:699}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:699}, {0:IsolationWindow_End} = {3:708}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:708}, {0:IsolationWindow_End} = {3:722}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:722}, {0:IsolationWindow_End} = {3:736}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:736}, {0:IsolationWindow_End} = {3:750}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:750}, {0:IsolationWindow_End} = {3:764}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:764}, {0:IsolationWindow_End} = {3:778}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:778}, {0:IsolationWindow_End} = {3:792}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:792}, {0:IsolationWindow_End} = {3:806}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:5} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:806}, {0:IsolationWindow_End} = {3:825}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:825}, {0:IsolationWindow_End} = {3:844}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:844}, {0:IsolationWindow_End} = {3:863}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:863}, {0:IsolationWindow_End} = {3:882}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:882}, {0:IsolationWindow_End} = {3:901}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:901}, {0:IsolationWindow_End} = {3:920}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:920}, {0:IsolationWindow_End} = {3:939}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:939}, {0:IsolationWindow_End} = {3:968}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:968}, {0:IsolationWindow_End} = {3:997}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:997}, {0:IsolationWindow_End} = {3:1026}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:1026}, {0:IsolationWindow_End} = {3:1075}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:1075}, {0:IsolationWindow_End} = {3:1124}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:1124}, {0:IsolationWindow_End} = {3:1173}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.contains, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, true,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start} = {3:1173}, {0:IsolationWindow_End} = {3:1249}, {0:IsolationWindow_StartMargin} = {3:0.5}, {0:IsolationWindow_CERange} = {3:10} }"),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_ProductMassAnalyzer}",
                        "\"{6:FullScanMassAnalyzerType_none}\"",
                        "\"{6:FullScanMassAnalyzerType_qit}\""),
                    new DetailLogMessage(LogLevel.all_info, MessageType.changed_from_to, SrmDocument.DOCUMENT_TYPE.proteomic, string.Empty, false,
                        "{0:Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_Resolution}",
                        "{2:Missing}",
                        "{3:0.7}"),
                }),
        };
        #endregion

        private static readonly LogEntry[] LOG_ENTRIES = CreateLogEnries();
    }
}