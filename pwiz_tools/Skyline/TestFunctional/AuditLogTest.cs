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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.AuditLog;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.AuditLog.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

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
                PropertyNames.SrmDocument_Settings + AuditLogStrings.PropertySeparator + PropertyNames.SrmSettings_TransitionSettings,
                "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}");

            // string in quotes
            VerifyStringLocalization("\"{0:Settings}\"", "\"{0:Settings}\"");

            // Non existen resource name
            VerifyStringLocalization("{0:SEttings}", "{0:SEttings}");

            var unlocalized = GetUnlocalizedProperties(typeof(SrmDocument), PropertyPath.Root);
            if (unlocalized.Any())
                Assert.Fail("The following properties are unlocalized:\n" + string.Join("\n", unlocalized));
        }

        private void VerifyStringLocalization(string expected, string unlocalized)
        {
            Assert.AreEqual(expected, LogMessage.LocalizeLogStringProperties(unlocalized));
        }

        /// <summary>
        /// Verifies that all diff properties of T are localized, unless their name can be ignored
        /// or a custom localizer is provided
        /// </summary>
        private List<UnlocalizedProperty> GetUnlocalizedProperties(Type T, PropertyPath path)
        {
            var properties = Reflector.GetProperties(T);

            var parentCollectionInfo = CollectionInfo.ForType(T);
            var parentString = parentCollectionInfo != null ? parentCollectionInfo.ElementType.Name : T.Name;

            var unlocalizedProperties = new List<UnlocalizedProperty>();
            foreach (var property in properties)
            {
                var subPath = path.Property(property.PropertyInfo.Name);
                if (!property.IgnoreName)
                {
                    string[] names;
                    if (property.CustomLocalizer != null)
                    {
                        var localizer = CustomPropertyLocalizer.CreateInstance(property.CustomLocalizer);
                        names = localizer.PossibleResourceNames;
                    }
                    else
                    {
                        names = new[] { parentString + "_" + property.PropertyInfo.Name };
                    }

                    foreach (var name in names)
                    {
                        var localized = PropertyNames.ResourceManager.GetString(name);
                        if (localized == null)
                        {
                            var propPath = property.CustomLocalizer != null ? PropertyPath.Parse(name) : subPath;
                            unlocalizedProperties.Add(new UnlocalizedProperty(propPath, name));
                        }
                    }
                }

                var type = property.PropertyInfo.PropertyType;
                var collectionInfo = CollectionInfo.ForType(type);
                if (collectionInfo != null)
                    type = collectionInfo.ElementType;

                // The reflector will fail with a non class type because of the type restriction
                // on Reflector<T>
                if (type.IsClass)
                    unlocalizedProperties.AddRange(GetUnlocalizedProperties(type, subPath));
            }

            return unlocalizedProperties;
        }

        public class UnlocalizedProperty
        {
            public UnlocalizedProperty(PropertyPath propertyPath, string unlocalizedString)
            {
                PropertyPath = propertyPath;
                UnlocalizedString = unlocalizedString;
            }

            public PropertyPath PropertyPath { get; private set; }
            public string UnlocalizedString { get; private set; }

            public override string ToString()
            {
                return string.Format("{0} ({1})", PropertyPath, UnlocalizedString);
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
            LOG_ENTRIES.ForEach(e => { e.Verify(); });

            if(IsRecordMode)
                Assert.Fail("Successfully recorded data");

            // Test audit log clear
            Assert.AreEqual(LOG_ENTRY_MESSAGESES.Length, LogEntry.GetAuditLogEntryCount());
            RunUI(() => SkylineWindow.ClearAuditLog());
            Assert.AreEqual(1, LogEntry.GetAuditLogEntryCount());
            // Clearing the audit log can be undone
            RunUI(() => SkylineWindow.Undo());
            Assert.AreEqual(LOG_ENTRY_MESSAGESES.Length, LogEntry.GetAuditLogEntryCount());
                
            // Test UI
            RunUI(() => SkylineWindow.ShowAuditLog());
            var auditLogForm = FindOpenForm<AuditLogForm>();
            Assert.IsNotNull(auditLogForm);

            // Make sure built in views are set up correctly
            ViewSpec[] builtInViews = null;
            RunUI(() => builtInViews = ((AbstractViewContext)auditLogForm.BindingListSource.ViewContext).BuiltInViews.ToArray());
            var expectedViewNames = new[] { AuditLogStrings.AuditLogForm_MakeAuditLogForm_Undo_Redo, AuditLogStrings.AuditLogForm_MakeAuditLogForm_Summary, AuditLogStrings.AuditLogForm_MakeAuditLogForm_All_Info };
            var expectedColumns = new[]
            {
                new[] {"TimeStamp", "UndoRedoMessage"}, new[] {"TimeStamp", "SummaryMessage"},
                new[] {"TimeStamp", "Details!*.AllInfoMessage"}
            };
            Assert.AreEqual(expectedViewNames.Length, builtInViews.Length);

            for (var i = 0; i < expectedViewNames.Length; ++i)
            {
                Assert.AreEqual(expectedViewNames[i], builtInViews[i].Name);
                Assert.AreEqual(expectedColumns[i].Length, builtInViews[i].Columns.Count);

                for (var j = 0; j < builtInViews[i].Columns.Count; ++j)
                    Assert.AreEqual(expectedColumns[i][j], builtInViews[i].Columns[j].Name);
            }

            WaitForConditionUI(() => auditLogForm.BindingListSource.IsComplete);
            RunUI(() =>
            {
                // Verify that the audit log rows in the grid correspond to the audit log entries
                Assert.AreEqual(auditLogForm.BindingListSource.Count, LOG_ENTRY_MESSAGESES.Length);
                for (var i = 0; i < auditLogForm.BindingListSource.Count; i++)
                {
                    var rowItem = auditLogForm.BindingListSource[i] as RowItem;
                    Assert.IsNotNull(rowItem);
                    var row = rowItem.Value as AuditLogRow;
                    Assert.IsNotNull(row);

                    Assert.AreEqual(LOG_ENTRY_MESSAGESES[i].ExpectedSummary.ToString(), row.SummaryMessage);
                    Assert.AreEqual(LOG_ENTRY_MESSAGESES[i].ExpectedUndoRedo.ToString(), row.UndoRedoMessage);

                    if (LOG_ENTRY_MESSAGESES[i].ExpectedAllInfo.Length != row.Details.Count)
                    {
                        Assert.Fail("Expected: " +
                                    string.Join(",\n", LOG_ENTRY_MESSAGESES[i].ExpectedAllInfo.Select(l => l.ToString())) +
                                    "\nActual: " + string.Join(",\n", row.Details.Select(d => d.AllInfoMessage)));
                    }

                    for (var j = 0; j < row.Details.Count; ++j)
                        Assert.AreEqual(LOG_ENTRY_MESSAGESES[i].ExpectedAllInfo[j].ToString(), row.Details[j].AllInfoMessage);

                }
            });

            // Change to a view that shows the reason
            RunUI(() =>
            {
                var descriptor = ColumnDescriptor.RootColumn(auditLogForm.ViewInfo.DataSchema, typeof(AuditLogRow));
                auditLogForm.ViewInfo = new ViewInfo(descriptor,
                    auditLogForm.ViewInfo.ViewSpec.SetColumns(new[]
                    {
                        new ColumnSpec(PropertyPath.Parse("UndoRedoMessage")),
                        new ColumnSpec(PropertyPath.Parse("Reason")),
                        new ColumnSpec(PropertyPath.Parse("Details!*.Reason"))
                    }).SetName("Reason View"));
            });
            WaitForConditionUI(() => auditLogForm.BindingListSource.IsComplete);
            // Verify that changing the reason of a row correctly modifies the audit log entries in the document
            RunUI(() =>
            {
                // (Precursor mass changed to "Average" row) Changing the reason of this row should change the reason of its detail row and vice versa
                ChangeReason(auditLogForm, "Reason", 1, "Reason 1");
                Assert.AreEqual("Reason 1", SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[1].Reason);
                Assert.AreEqual("Reason 1", SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[1].AllInfo[0].Reason);
                ChangeReason(auditLogForm, "Details!*.Reason", 1, "Reason 2");
                Assert.AreEqual("Reason 2", SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[1].Reason);
                Assert.AreEqual("Reason 2", SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[1].AllInfo[0].Reason);

                // (Collision Energy changed from Thermo to Thermo TSQ Q.) Changing the reason of this row should not change the reason of its detail row and vice versa
                ChangeReason(auditLogForm, "Reason", 2, "Reason 3");
                Assert.AreEqual("Reason 3", SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[2].Reason);
                Assert.IsTrue(SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[2].AllInfo
                    .All(l => string.IsNullOrEmpty(l.Reason)));
                ChangeReason(auditLogForm, "Details!*.Reason", 2, "Reason 4");
                Assert.AreEqual("Reason 3", SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[2].Reason);
                Assert.AreEqual("Reason 4", SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[2].AllInfo[0].Reason);
            });
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

            private static string LogMessageToCode(LogMessage msg, int indentLvl = 0)
            {
                var indent = "";
                for (var i = 0; i < indentLvl; ++i)
                    indent += "    ";

                var result = string.Format(indent + "new LogMessage(LogLevel.{0}, MessageType.{1}, string.Empty, {2},\r\n", msg.Level, msg.Type, msg.Expanded ? "true" : "false");
                foreach (var name in msg.Names)
                {
                    var n = name.Replace("\"", "\\\"");
                    result += indent + string.Format("    \"{0}\",\r\n", n);
                }
                return result.Substring(0, result.Length - 3) + "),\r\n";
            }

            public string AuditLogEntryToCode(AuditLogEntry entry)
            {
                var text = "";

                text += "            new LogEntryMessages(\r\n";
                text += LogMessageToCode(entry.UndoRedo, 4);
                text += LogMessageToCode(entry.Summary, 4);

                text += "                new[]\r\n                {\r\n";
                text = entry.AllInfo.Aggregate(text, (current, info) => current + LogMessageToCode(info, 5));

                return text + "                }),";
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
                RunUI(() => result = SkylineWindow.DocumentUI.AuditLog.AuditLogEntries[count - 1]);
                return result;
            }

            public void Verify()
            {
                RunUI(SettingsChange);

                var newestEntry = GetNewestEntry();
                //PauseTest(newestEntry.UndoRedo.ToString());

                if (IsRecordMode)
                {
                    Console.WriteLine(AuditLogEntryToCode(newestEntry));
                    return;
                }

                ++_expectedAuditLogEntryCount;
                Assert.AreEqual(_expectedAuditLogEntryCount, GetAuditLogEntryCount());
                Assert.IsNotNull(newestEntry);

                Assert.AreEqual(ExpectedMessages.ExpectedUndoRedo, newestEntry.UndoRedo);
                Assert.AreEqual(ExpectedMessages.ExpectedSummary, newestEntry.Summary);

                if (ExpectedMessages.ExpectedAllInfo.Length != newestEntry.AllInfo.Count)
                {
                    Assert.Fail("Expected: " +
                                string.Join(",\n", ExpectedMessages.ExpectedAllInfo.Select(l => l.ToString())) +
                                "\nActual: " + string.Join(",\n", newestEntry.AllInfo.Select(l => l.ToString())));
                }

                for (var i = 0; i < ExpectedMessages.ExpectedAllInfo.Length; ++i)
                    Assert.AreEqual(ExpectedMessages.ExpectedAllInfo[i], newestEntry.AllInfo[i]);

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

        public class LogEntryMessages
        {
            public LogEntryMessages(LogMessage expectedUndoRedo, LogMessage expectedSummary, LogMessage[] expectedAllInfo)
            {
                ExpectedUndoRedo = expectedUndoRedo;
                ExpectedSummary = expectedSummary;
                ExpectedAllInfo = expectedAllInfo;
            }

            public LogMessage ExpectedUndoRedo { get; set; }
            public LogMessage ExpectedSummary { get; set; }
            public LogMessage[] ExpectedAllInfo { get; set; }
        }

        private static LogEntry[] CreateLogEnries()
        {
            return new [] {
                // Enable audit logging
                new LogEntry(() =>
                {
                    AuditLogForm.EnableAuditLogging(true, SkylineWindow);
                }, LOG_ENTRY_MESSAGESES[0]), 

                // Basic property change
                new LogEntry(() => SkylineWindow.ChangeSettings(
                        SkylineWindow.DocumentUI.Settings.ChangeTransitionPrediction(p =>
                            p.ChangePrecursorMassType(MassType.Average)), true), LOG_ENTRY_MESSAGESES[1]),

                // Collection change: named to named
                new LogEntry(() => SkylineWindow.ChangeSettings(
                    SkylineWindow.DocumentUI.Settings.ChangeTransitionPrediction(p =>
                        p.ChangeCollisionEnergy(Settings.Default.CollisionEnergyList.First(c => c.Name == "Thermo TSQ Quantiva"))), true), LOG_ENTRY_MESSAGESES[2]),

                // Collection change: null to named
                new LogEntry(() => SkylineWindow.ChangeSettings(
                    SkylineWindow.DocumentUI.Settings.ChangeTransitionPrediction(p =>
                        p.ChangeDeclusteringPotential(Settings.Default.DeclusterPotentialList.First(c => c.Name == "SCIEX"))), true), LOG_ENTRY_MESSAGESES[3]),

                // Collection change: multiple named elements with sub properties added
                new LogEntry(() => SkylineWindow.ChangeSettings(
                    SkylineWindow.DocumentUI.Settings.ChangeTransitionFilter(p =>
                        p.ChangeMeasuredIons(new[] { Settings.Default.MeasuredIonList[0], Settings.Default.MeasuredIonList[1] })), true), LOG_ENTRY_MESSAGESES[4]),

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
                        SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeAnnotationDefs(l =>
                        {
                            var newList = new List<AnnotationDef>(l);
                            newList.RemoveAt(0);
                            return newList;
                        }), true);
                    }, LOG_ENTRY_MESSAGESES[5]),

                // Undo redo shortened names added
                new LogEntry(() =>
                    {
                        SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeAnnotationDefs(l =>
                        {
                            var newList = new List<AnnotationDef>(l);
                            newList.Insert(0, Settings.Default.AnnotationDefList[0]);
                            return newList;
                        }), true);
                    }, LOG_ENTRY_MESSAGESES[6]),
            
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
                        SkylineWindow.ChangeSettings(SkylineWindow.DocumentUI.Settings.ChangeTransitionSettings(t =>
                            {
                                return t.ChangeFullScan(t.FullScan.ChangeAcquisitionMethod(FullScanAcquisitionMethod.DIA,
                                    Settings.Default.IsolationSchemeList.First(i => i.Name == "SWATH (VW 64)")));
                            }), true);
                    }, LOG_ENTRY_MESSAGESES[7]),

                // Disable audit logging
                new LogEntry(() =>
                {
                    AuditLogForm.EnableAuditLogging(false, SkylineWindow);
                }, LOG_ENTRY_MESSAGESES[8]),
            };
        }

        //Has to be defined prior to LOG_ENTRIES
        #region DATA
        private static readonly LogEntryMessages[] LOG_ENTRY_MESSAGESES =
        {	
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.log_enabled, string.Empty, false),
                new LogMessage(LogLevel.summary, MessageType.log_enabled, string.Empty, false),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.log_enabled, string.Empty, false),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed_to, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_PrecursorMassType}",
                    "\"Average\""),
                new LogMessage(LogLevel.summary, MessageType.changed_to, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_PrecursorMassType}",
                    "\"Average\""),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.changed_from_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_PrecursorMassType}",
                        "\"Monoisotopic\"",
                        "\"Average\""),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed_from_to, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_CollisionEnergy}",
                    "\"Thermo\"",
                    "\"Thermo TSQ Quantiva\""),
                new LogMessage(LogLevel.summary, MessageType.changed_from_to, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_CollisionEnergy}",
                    "\"Thermo\"",
                    "\"Thermo TSQ Quantiva\""),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.changed_from_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_CollisionEnergy}",
                        "\"Thermo\"",
                        "\"Thermo TSQ Quantiva\""),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_CollisionEnergy}{2:PropertySeparator}{0:CollisionEnergyRegression_Conversions}",
                        "{ {0:ChargeRegressionLine_Charge}={3:2}, {0:ChargeRegressionLine_Slope}={3:0.0339}, {0:ChargeRegressionLine_Intercept}={3:2.3597} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_CollisionEnergy}{2:PropertySeparator}{0:CollisionEnergyRegression_Conversions}",
                        "{ {0:ChargeRegressionLine_Charge}={3:3}, {0:ChargeRegressionLine_Slope}={3:0.0295}, {0:ChargeRegressionLine_Intercept}={3:1.5123} }"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_CollisionEnergy}{2:PropertySeparator}{0:CollisionEnergyRegression_StepSize}",
                        "{3:1}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_CollisionEnergy}{2:PropertySeparator}{0:CollisionEnergyRegression_StepCount}",
                        "{3:5}"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed_from_to, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_DeclusteringPotential}",
                    "{2:Missing}",
                    "\"SCIEX\""),
                new LogMessage(LogLevel.summary, MessageType.changed_from_to, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_DeclusteringPotential}",
                    "{2:Missing}",
                    "\"SCIEX\""),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.changed_from_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_DeclusteringPotential}",
                        "{2:Missing}",
                        "\"SCIEX\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_DeclusteringPotential}{2:PropertySeparator}{0:DeclusteringPotentialRegression_Slope}",
                        "{3:0}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_DeclusteringPotential}{2:PropertySeparator}{0:DeclusteringPotentialRegression_Intercept}",
                        "{3:80}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_DeclusteringPotential}{2:PropertySeparator}{0:DeclusteringPotentialRegression_StepSize}",
                        "{3:10}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Prediction}{2:PropertySeparator}{0:TransitionPrediction_DeclusteringPotential}{2:PropertySeparator}{0:DeclusteringPotentialRegression_StepCount}",
                        "{3:3}"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}"),
                new LogMessage(LogLevel.summary, MessageType.changed, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}"),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.added_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}",
                        "\"N-terminal to Proline\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_Fragment}",
                        "\"P\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_Restrict}",
                        "{2:Missing}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_Terminus}",
                        "\"N\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_MinFragmentLength}",
                        "{3:3}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_IsFragment}",
                        "{3:True}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"N-terminal to Proline\"{2:PropertySeparator}{0:MeasuredIon_SettingsCustomIon}",
                        "{2:Missing}"),
                    new LogMessage(LogLevel.all_info, MessageType.added_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}",
                        "\"C-terminal to Glu or Asp\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_Fragment}",
                        "\"ED\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_Restrict}",
                        "{2:Missing}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_Terminus}",
                        "\"C\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_MinFragmentLength}",
                        "{3:3}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_IsFragment}",
                        "{3:True}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_Filter}{2:PropertySeparator}{0:TransitionFilter_MeasuredIons}{2:PropertySeparator}\"C-terminal to Glu or Asp\"{2:PropertySeparator}{0:MeasuredIon_SettingsCustomIon}",
                        "{2:Missing}"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.removed, string.Empty, false,
                    "{1:DataSettings_AnnotationDefs}{2:ElementTypeSeparator}\"SubjectId\""),
                new LogMessage(LogLevel.summary, MessageType.removed_from, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                    "\"SubjectId\""),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.removed_from, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                        "\"SubjectId\""),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.added, string.Empty, false,
                    "{1:DataSettings_AnnotationDefs}{2:ElementTypeSeparator}\"SubjectId\""),
                new LogMessage(LogLevel.summary, MessageType.added_to, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                    "\"SubjectId\""),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.added_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}",
                        "\"SubjectId\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"SubjectId\"{2:PropertySeparator}{0:AnnotationDef_AnnotationTargets}",
                        "\"replicate\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"SubjectId\"{2:PropertySeparator}{0:AnnotationDef_Type}",
                        "\"text\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_DataSettings}{2:TabSeparator}{0:DataSettings_AnnotationDefs}{2:PropertySeparator}\"SubjectId\"{2:PropertySeparator}{0:AnnotationDef_Items}",
                        "[  ]"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.changed, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}"),
                new LogMessage(LogLevel.summary, MessageType.changed, string.Empty, false,
                    "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}"),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.changed_from_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_AcquisitionMethod}",
                        "\"None\"",
                        "\"DIA\""),
                    new LogMessage(LogLevel.all_info, MessageType.changed_from_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}",
                        "{2:Missing}",
                        "\"SWATH (VW 64)\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrecursorFilter}",
                        "{2:Missing}"),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_IsolationWidth}",
                        "\"results\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_SpecialHandling}",
                        "\"None\""),
                    new LogMessage(LogLevel.all_info, MessageType.is_, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_WindowsPerScan}",
                        "{2:Missing}"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:400}, {0:IsolationWindow_End}={3:409}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:409}, {0:IsolationWindow_End}={3:416}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:416}, {0:IsolationWindow_End}={3:423}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:423}, {0:IsolationWindow_End}={3:430}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:430}, {0:IsolationWindow_End}={3:437}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:437}, {0:IsolationWindow_End}={3:444}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:444}, {0:IsolationWindow_End}={3:451}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:451}, {0:IsolationWindow_End}={3:458}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:458}, {0:IsolationWindow_End}={3:465}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:465}, {0:IsolationWindow_End}={3:471}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:471}, {0:IsolationWindow_End}={3:477}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:477}, {0:IsolationWindow_End}={3:483}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:483}, {0:IsolationWindow_End}={3:489}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:489}, {0:IsolationWindow_End}={3:495}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:495}, {0:IsolationWindow_End}={3:501}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:501}, {0:IsolationWindow_End}={3:507}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:507}, {0:IsolationWindow_End}={3:514}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:514}, {0:IsolationWindow_End}={3:521}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:521}, {0:IsolationWindow_End}={3:528}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:528}, {0:IsolationWindow_End}={3:535}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:535}, {0:IsolationWindow_End}={3:542}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:542}, {0:IsolationWindow_End}={3:549}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:549}, {0:IsolationWindow_End}={3:556}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:556}, {0:IsolationWindow_End}={3:563}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:563}, {0:IsolationWindow_End}={3:570}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:570}, {0:IsolationWindow_End}={3:577}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:577}, {0:IsolationWindow_End}={3:584}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:584}, {0:IsolationWindow_End}={3:591}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:591}, {0:IsolationWindow_End}={3:598}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:598}, {0:IsolationWindow_End}={3:605}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:605}, {0:IsolationWindow_End}={3:612}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:612}, {0:IsolationWindow_End}={3:619}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:619}, {0:IsolationWindow_End}={3:626}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:626}, {0:IsolationWindow_End}={3:633}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:633}, {0:IsolationWindow_End}={3:640}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:640}, {0:IsolationWindow_End}={3:647}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:647}, {0:IsolationWindow_End}={3:654}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:654}, {0:IsolationWindow_End}={3:663}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:663}, {0:IsolationWindow_End}={3:672}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:672}, {0:IsolationWindow_End}={3:681}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:681}, {0:IsolationWindow_End}={3:690}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:690}, {0:IsolationWindow_End}={3:699}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:699}, {0:IsolationWindow_End}={3:708}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:708}, {0:IsolationWindow_End}={3:722}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:722}, {0:IsolationWindow_End}={3:736}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:736}, {0:IsolationWindow_End}={3:750}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:750}, {0:IsolationWindow_End}={3:764}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:764}, {0:IsolationWindow_End}={3:778}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:778}, {0:IsolationWindow_End}={3:792}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:792}, {0:IsolationWindow_End}={3:806}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:5} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:806}, {0:IsolationWindow_End}={3:825}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:825}, {0:IsolationWindow_End}={3:844}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:844}, {0:IsolationWindow_End}={3:863}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:863}, {0:IsolationWindow_End}={3:882}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:882}, {0:IsolationWindow_End}={3:901}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:901}, {0:IsolationWindow_End}={3:920}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:920}, {0:IsolationWindow_End}={3:939}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:939}, {0:IsolationWindow_End}={3:968}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:968}, {0:IsolationWindow_End}={3:997}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:997}, {0:IsolationWindow_End}={3:1026}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:1026}, {0:IsolationWindow_End}={3:1075}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:1075}, {0:IsolationWindow_End}={3:1124}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:1124}, {0:IsolationWindow_End}={3:1173}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.contains, string.Empty, true,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_IsolationScheme}{2:PropertySeparator}{0:IsolationScheme_PrespecifiedIsolationWindows}",
                        "{ {0:IsolationWindow_Start}={3:1173}, {0:IsolationWindow_End}={3:1249}, {0:IsolationWindow_StartMargin}={3:0.5}, {0:IsolationWindow_CERange}={3:10} }"),
                    new LogMessage(LogLevel.all_info, MessageType.changed_from_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:TransitionFullScan_ProductMassAnalyzer}",
                        "\"none\"",
                        "\"qit\""),
                    new LogMessage(LogLevel.all_info, MessageType.changed_from_to, string.Empty, false,
                        "{0:SrmDocument_Settings}{2:PropertySeparator}{0:SrmSettings_TransitionSettings}{2:TabSeparator}{0:TransitionSettings_FullScan}{2:PropertySeparator}{0:Resolution}",
                        "{2:Missing}",
                        "{3:0.7}"),
                }),
            new LogEntryMessages(
                new LogMessage(LogLevel.undo_redo, MessageType.log_disabled, string.Empty, false),
                new LogMessage(LogLevel.summary, MessageType.log_disabled, string.Empty, false),
                new[]
                {
                    new LogMessage(LogLevel.all_info, MessageType.log_disabled, string.Empty, false),
                }),
        };
        #endregion

        private static readonly LogEntry[] LOG_ENTRIES = CreateLogEnries();
    }
}