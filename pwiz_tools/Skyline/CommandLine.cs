/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011-2015 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.ElementLocators.ExportAnnotations;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Proteome;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline
{
    public class CommandLine : IDisposable
    {
        private CommandStatusWriter _out;

        private SrmDocument _doc;
        private string _skylineFile;

        private ExportCommandProperties _exportProperties;

        /// <summary>
        /// True if any results files were imported successfully.
        /// </summary>
        private bool _importedResults;

        public CommandLine(CommandStatusWriter output)
        {
            _out = output;
        }

        public SrmDocument Document { get { return _doc; } }

        public CommandLine()
            : this(new CommandStatusWriter(new StringWriter()))
        {
        }

        public ResultsMemoryDocumentContainer DocContainer { get; private set; }

        public int Run(string[] args, bool withoutUsage = false, bool test = false)
        {
            var exitStatus = RunInner(args, withoutUsage);

            // Handle cases where the error reporting and exit code are out of synch
            // TODO: Add testing that fails when these happen and fix the causes
            if (!test)
            {
                if (exitStatus == Program.EXIT_CODE_SUCCESS)
                {
                    if (_out.IsErrorReported)
                    {
                        // Return the error status only if we are not running tests. We want the test to fail if errors were reported 
                        // but the exit code is 0.
                        exitStatus = Program.EXIT_CODE_RAN_WITH_ERRORS;
                    }
                }
                else if (!_out.IsErrorReported)
                {
                    // Output the catch-all error only if we are not running tests. We want the test to fail if no error is reported 
                    // and the exit code is not 0.
                    _out.WriteLine(Resources.CommandLine_Run_Error__Failure_occurred__Exiting___);
                }
            }

            return exitStatus;
        }

        private int RunInner(string[] args, bool withoutUsage = false)
        {
            _importedResults = false;

            var commandArgs = new CommandArgs(_out, _doc != null);

            if(!commandArgs.ParseArgs(args))
            {
                if (commandArgs.UsageShown)
                {
                    // Assume that the UsageShown flag is set while parsing arguments only if the --help argument is seen.
                    // ParseArgs will return false because ProcessArgument for --help returns false so that argument  
                    // processing does not go any further.
                    // We want to return an exit code of 0 here.
                    return Program.EXIT_CODE_SUCCESS;
                }
                _out.WriteLine(Resources.CommandLine_Run_Exiting___);
                return Program.EXIT_CODE_FAILURE_TO_START;
            }

            if (!string.IsNullOrEmpty(commandArgs.LogFile))
            {
                var oldOut = _out;
                try
                {
                    _out = new CommandStatusWriter(new StreamWriter(commandArgs.LogFile))
                    {
                        IsTimeStamped = oldOut.IsTimeStamped,
                        IsMemStamped = oldOut.IsMemStamped
                    };
                }
                catch (Exception)
                {
                    oldOut.WriteLine(Resources.CommandLine_Run_Error__Failed_to_open_log_file__0_, commandArgs.LogFile);
                    return Program.EXIT_CODE_FAILURE_TO_START;
                }
                using (oldOut)
                {
                    oldOut.WriteLine(Resources.CommandLine_Run_Writing_to_log_file__0_, commandArgs.LogFile);
                }
            }

            // First come the commands that do not depend on an --in command to run.
            // These commands modify Settings.Default instead of working with an open skyline document.
            bool anyAction = false;
            if (commandArgs.InstallingToolsFromZip)
            {
                if (!ImportToolsFromZip(commandArgs.ZippedToolsPath, commandArgs.ResolveZipToolConflictsBySkipping,
                    commandArgs.ResolveZipToolAnotationConflictsBySkipping,
                    commandArgs.ZippedToolsProgramPathContainer, commandArgs.ZippedToolsProgramPathValue,
                    commandArgs.ZippedToolsPackagesHandled))
                {
                    return Program.EXIT_CODE_RAN_WITH_ERRORS;
                }
                anyAction = true;
            }
            if (commandArgs.ImportingTool)
            {
                if (!ImportTool(commandArgs.ToolName, commandArgs.ToolCommand, commandArgs.ToolArguments,
                    commandArgs.ToolInitialDirectory, commandArgs.ToolReportTitle,
                    commandArgs.ToolOutputToImmediateWindow, commandArgs.ResolveToolConflictsBySkipping))
                {
                    return Program.EXIT_CODE_RAN_WITH_ERRORS;
                }
                anyAction = true;
            }
            if (commandArgs.RunningBatchCommands)
            {
                var exitCode = RunBatchCommands(commandArgs.BatchCommandsPath);
                if (exitCode != Program.EXIT_CODE_SUCCESS)
                    return exitCode;
                anyAction = true;
            }
            if (commandArgs.ImportingSkyr)
            {
                if (!ImportSkyr(commandArgs.SkyrPath, commandArgs.ResolveSkyrConflictsBySkipping))
                    return Program.EXIT_CODE_RAN_WITH_ERRORS;
                anyAction = true;
            }
            if (!commandArgs.RequiresSkylineDocument)
            {
                if (!anyAction && !withoutUsage)
                    commandArgs.Usage();

                // Exit quietly because Run(args[]) ran successfully. No work with a skyline document was called for.
                return Program.EXIT_CODE_SUCCESS;
            }

            var skylineFile = commandArgs.SkylineFile;
            if ((skylineFile != null && !OpenSkyFile(skylineFile)) ||
                (skylineFile == null && _doc == null))
            {
               _out.WriteLine(Resources.CommandLine_Run_Exiting___);
                return Program.EXIT_CODE_RAN_WITH_ERRORS;
            }

            if (skylineFile != null)
                _skylineFile = skylineFile;

            try
            {
                using (DocContainer = new ResultsMemoryDocumentContainer(null, _skylineFile))
                {
                    DocContainer.ProgressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(),
                        commandArgs.ImportWarnOnFailure);
                    // Make sure no joining happens on open, if joining is disabled
                    if (commandArgs.ImportDisableJoining && _doc != null && _doc.Settings.HasResults)
                    {
                        ModifyDocument(d => d.ChangeSettingsNoDiff(_doc.Settings.ChangeMeasuredResults(
                            d.MeasuredResults.ChangeIsJoiningDisabled(true))));
                    }
                    DocContainer.SetDocument(_doc, null);

                    bool successProcessing = ProcessDocument(commandArgs);
                    bool successExporting = true;
                    if (successProcessing)
                        successExporting = PerformExportOperations(commandArgs);

                    // Save any settings list changes made by opening the document
                    if (commandArgs.SaveSettings)
                        SaveSettings();

                    if (!successProcessing || !successExporting)
                        return Program.EXIT_CODE_RAN_WITH_ERRORS;
                }
            }
            finally
            {
                DocContainer = null;
            }
            return Program.EXIT_CODE_SUCCESS;
        }

        private bool ProcessDocument(CommandArgs commandArgs)
        {
            if (commandArgs.PredictTranSettings)
            {
                if (!SetPredictTranSettings(commandArgs))
                    return false;
            }
            if (commandArgs.FilterSettings)
            {
                if (!SetFilterSettings(commandArgs))
                    return false;
            }
            if (commandArgs.FullScanSettings)
            {
                if (!SetFullScanSettings(commandArgs))
                    return false;
            }

            if (commandArgs.ImsSettings)
            {
                if (!SetImsSettings(commandArgs))
                    return false;
            }

            if (commandArgs.SettingLibraryPath)
            {
                if (!SetLibrary(commandArgs.LibraryName, commandArgs.LibraryPath))
                {
                    _out.WriteLine(Resources.CommandLine_Run_Not_setting_library_);
                    return false;
                }
            }

            WaitForDocumentLoaded();

            if (_out.IsErrorReported)
            {
                return false;
            }

            if (commandArgs.ImportingFasta && !commandArgs.ImportingSearch)
            {
                try
                {
                    ImportFasta(commandArgs.FastaPath, commandArgs.KeepEmptyProteins);
                }
                catch (Exception x)
                {
                    _out.WriteLine(Resources.CommandLine_Run_Error__Failed_importing_the_file__0____1_, commandArgs.FastaPath,
                        x.Message);
                    return false;
                }
            }

            if (commandArgs.ImportingTransitionList)
            {
                try
                {
                    if (!ImportTransitionList(commandArgs))
                        return false;
                }
                catch (Exception x)
                {
                    _out.WriteLine(Resources.CommandLine_Run_Error__Failed_importing_the_file__0____1_,
                        commandArgs.TransitionListPath, x.Message);
                    return false;
                }
            }

            if (commandArgs.ImportingSearch)
            {
                if (!ImportSearch(commandArgs))
                    return false;
            }

            if (commandArgs.ImportingDocuments)
            {
                if (!ImportDocuments(commandArgs))
                    return false;
            }

            if (commandArgs.AddDecoys || commandArgs.DiscardDecoys)
            {
                if (!AddDecoys(commandArgs))
                    return false;
            }

            if (commandArgs.RemovingResults && !commandArgs.RemoveBeforeDate.HasValue)
            {
                // Remove all existing results in the document.
                RemoveResults(null);
            }

            if (commandArgs.ImportingResults)
            {
                if (!ImportResults(commandArgs))
                    return false;
            }

            WaitForDocumentLoaded();

            if (commandArgs.Minimizing)
            {
                if (!MinimizeResults(commandArgs))
                    return false;
            }

            if (commandArgs.RemovingResults && commandArgs.RemoveBeforeDate.HasValue)
            {
                // We are given a remove-before date. Remove results AFTER all results have been imported. 
                // Some of the results that were just imported may have been acquired before the remove-before 
                // date and we want to remove them.
                RemoveResults(commandArgs.RemoveBeforeDate);
            }

            if (commandArgs.Reintegrating && !ReintegratePeaks(commandArgs))
            {
                return false;
            }

            if (!ImportAnnotations(commandArgs))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(commandArgs.ImportPeakBoundariesPath) && !ImportPeakBoundaries(commandArgs))
            {
                return false;
            }

            if (commandArgs.Refinement != null && !RefineDocument(commandArgs))
            {
                return false;
            }

            if (commandArgs.Saving)
            {
                var saveFile = commandArgs.SaveFile ?? _skylineFile;
                if (!SaveFile(saveFile))
                    return false;

                _skylineFile = saveFile;
            }

            WaitForDocumentLoaded();
            return true;
        }

        private void WaitForDocumentLoaded()
        {
            if (_doc != null && !_doc.IsLoaded)
            {
                DocContainer.SetDocument(_doc, DocContainer.Document, true);
                SetDocument(DocContainer.Document);
            }
        }

        private bool ImportResults(CommandArgs commandArgs)
        {
            DocContainer.ChromatogramManager.LoadingThreads = commandArgs.ImportThreads;

            OptimizableRegression optimize = null;
            try
            {
                if (_doc != null)
                    optimize = _doc.Settings.TransitionSettings.Prediction.GetOptimizeFunction(commandArgs.ImportOptimizeType);
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_Run_Error__Failed_to_get_optimization_function__0____1_,
                    commandArgs.ImportOptimizeType, x.Message);
                return false;
            }

            if (commandArgs.ImportingReplicateFile)
            {
                IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();

                MsDataFileUri[] files;
                try
                {
                    files = commandArgs.ReplicateFile.SelectMany(DataSourceUtil.ListSubPaths).ToArray();
                }
                catch (Exception e)
                {
                    _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, e.Message);
                    return false;
                }
                
                if (!string.IsNullOrEmpty(commandArgs.ReplicateName))
                { 
                    listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(commandArgs.ReplicateName, files));
                }
                else
                {
                    foreach (var dataFile in files)
                    {
                        listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(
                            dataFile.GetSampleName() ?? dataFile.GetFileNameWithoutExtension(),
                            new[] {dataFile}));
                    }

                }

                if (!ApplyFileAndSampleNameRegex(commandArgs.ImportFileNamePattern, commandArgs.ImportSampleNamePattern, ref listNamedPaths))
                {
                    return false;
                }

                if (string.IsNullOrEmpty(commandArgs.ReplicateName) && !commandArgs.ImportAppend)
                {
                    // A named path will be removed if the document contains a replicate with this file path.
                    RemoveImportedFiles(listNamedPaths, out var listNewPaths,
                        true /*Remove files paths that have already been imported into any replicate.*/);

                    if (listNewPaths.Count == 0)
                    {
                        _out.WriteLine(Resources.CommandLine_ImportResults_Error__No_files_left_to_import_);
                        return false;
                    }

                    listNamedPaths = listNewPaths;
                    MakeReplicateNamesUnique(listNamedPaths);
                }

                // If expected results are not imported successfully, terminate
                if (!ImportDataFilesWithAppend(listNamedPaths,
                        commandArgs.LockMassParameters,
                        commandArgs.ImportBeforeDate,
                        commandArgs.ImportOnOrAfterDate,
                        optimize,
                        commandArgs.ImportDisableJoining,
                        commandArgs.ImportAppend,
                        commandArgs.ImportWarnOnFailure))
                    return false;
            }
            else if (commandArgs.ImportingSourceDirectory)
            {
                // If expected results are not imported successfully, terminate
                if (!ImportResultsInDir(commandArgs.ImportSourceDirectory,
                        commandArgs.ImportRecursive,
                        commandArgs.ImportNamingPattern,
                        commandArgs.ReplicateName,
                        commandArgs.LockMassParameters,
                        commandArgs.ImportBeforeDate,
                        commandArgs.ImportOnOrAfterDate,
                        commandArgs.ImportFileNamePattern,
                        commandArgs.ImportSampleNamePattern,
                        optimize,
                        commandArgs.ImportDisableJoining,
                        commandArgs.ImportWarnOnFailure))
                    return false;
            }

            return true;
        }

        private bool ImportAnnotations(CommandArgs commandArgs)
        {
            if (string.IsNullOrEmpty(commandArgs.ImportAnnotations))
            {
                return true;
            }
            try
            {
                var documentAnnotations = new DocumentAnnotations(_doc);
                ModifyDocument(d => documentAnnotations.ReadAnnotationsFromFile(CancellationToken.None, commandArgs.ImportAnnotations));
                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_ImportAnnotations_Error__Failed_while_reading_annotations_);
                _out.WriteLine(x.Message);
                return false;
            }
        }

        private bool ImportPeakBoundaries (CommandArgs commandArgs)
        {
            try
            {
                _out.WriteLine(Resources.CommandLine_ImportPeakBoundaries_Importing_peak_boundaries_from__0_, Path.GetFileName(commandArgs.ImportPeakBoundariesPath));
                long lineCount = Helpers.CountLinesInFile(commandArgs.ImportPeakBoundariesPath);
                PeakBoundaryImporter importer = new PeakBoundaryImporter(_doc);
                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
                ModifyDocument(d => importer.Import(commandArgs.ImportPeakBoundariesPath, progressMonitor, lineCount));
                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_ImportPeakBoundaries_Error__Failed_importing_peak_boundaries_);
                _out.WriteLine(x.Message);
                return false;
            }
        }

        private bool RefineDocument(CommandArgs commandArgs)
        {
            if (commandArgs.RefinementLabelTypeName != null)
            {
                var labelType = GetLabelTypeHelper(commandArgs.RefinementLabelTypeName);
                if (labelType != null)
                    commandArgs.Refinement.RefineLabelType = labelType;
                else
                    return false;
            }

            if (commandArgs.RefinementCvLabelTypeName != null)
            {
                var labelType = GetLabelTypeHelper(commandArgs.RefinementCvLabelTypeName);
                if (labelType != null)
                    commandArgs.Refinement.NormalizationMethod = NormalizeOption.FromIsotopeLabelType(labelType);
                else
                    return false;
            }

            if (!commandArgs.Refinement.GroupComparisonNames.IsNullOrEmpty())
            {
                foreach (var name in commandArgs.Refinement.GroupComparisonNames)
                {
                    var gc = Document.Settings.DataSettings.GroupComparisonDefs.FirstOrDefault(g => g.Name.Equals(name));
                    if (gc != null)
                        commandArgs.Refinement.GroupComparisonDefs.Add(gc);
                }
            }

            _out.WriteLine(Resources.CommandLine_RefineDocument_Refining_document___);
            try
            {
                ModifyDocumentWithLogging(doc => commandArgs.Refinement.Refine(doc),
                    commandArgs.Refinement.EntryCreator.Create);
                return true;
            }
            catch (Exception x)
            {
                if (!_out.IsErrorReported)
                {
                    _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, x.Message);
                }
                else
                {
                    _out.WriteLine(x.Message);
                }
                return false;
            }
        }

        private IsotopeLabelType GetLabelTypeHelper(string label)
        {
            var mods = _doc.Settings.PeptideSettings.Modifications;
            var typeMods = mods.GetModificationsByName(label);
            if (typeMods == null)
            {
                _out.WriteLine(Resources.CommandLine_RefineDocument_Error__The_label_type___0___was_not_found_in_the_document_);
                _out.WriteLine(Resources.CommandLine_RefineDocument_Choose_one_of__0_, string.Join(@", ", mods.GetModificationTypes().Select(t => t.Name)));
                return null;
            }

            return typeMods.LabelType;
        }

        private HashSet<AuditLogEntry> GetSeenAuditLogEntries()
        {
            var setSeenEntries = new HashSet<AuditLogEntry>();
            var log = Document.AuditLog;
            for (var entry = log.AuditLogEntries; !entry.IsRoot; entry = entry.Parent)
                setSeenEntries.Add(entry);
            return setSeenEntries;
        }

        private void LogNewEntries(AuditLogEntry entry, HashSet<AuditLogEntry> setSeenEntries)
        {
            if (entry.IsRoot || setSeenEntries.Contains(entry))
            {
                _out.WriteLine(Resources.CommandLine_LogNewEntries_Document_unchanged);
                return;
            }
            LogNewEntriesInner(entry, setSeenEntries);
        }

        private void LogNewEntriesInner(AuditLogEntry entry, HashSet<AuditLogEntry> setSeenEntries)
        {
            if (entry.IsRoot || setSeenEntries.Contains(entry))
                return;

            LogNewEntriesInner(entry.Parent, setSeenEntries);

            _out.Write(AuditLogEntryToString(entry));
        }

        private static string AuditLogEntryToString(AuditLogEntry entry)
        {
            var sb = new StringBuilder();
            foreach (var allInfoItem in entry.AllInfo)
                sb.AppendLine(allInfoItem.ToString());
            return sb.ToString();
        }

        private void LogDocumentDelta(SrmDocument docBefore, SrmDocument docAfter)
        {
            LogDocumentDelta(Resources.CommandLine_LogDocumentDelta_Removed___0_, docBefore, docAfter);
            LogDocumentDelta(Resources.CommandLine_LogDocumentDelta_Added___0_, docAfter, docBefore);
        }

        private void LogDocumentDelta(string verbText, SrmDocument docBefore, SrmDocument docAfter)
        {
            string deltaText = DiffDocuments(docBefore, docAfter);
            if (deltaText != null)
                _out.WriteLine(verbText, deltaText);
        }

        private static string DiffDocuments(SrmDocument docTry, SrmDocument docTest)
        {
            var testProteins = new HashSet<int>(docTest.PeptideGroups.Where(g => !g.IsPeptideList).Select(g => g.Id.GlobalIndex));
            var testGroups = new HashSet<int>(docTest.MoleculeGroups.Select(g => g.Id.GlobalIndex));
            var testPeptides = new HashSet<int>(docTest.Peptides.Select(p => p.Id.GlobalIndex));
            var testMolecules = new HashSet<int>(docTest.Molecules.Select(m => m.Id.GlobalIndex));
            var testPrecursors = new HashSet<int>(docTest.MoleculeTransitionGroups.Select(p => p.Id.GlobalIndex));
            var testTransitions = new HashSet<int>(docTest.MoleculeTransitions.Select(p => p.Id.GlobalIndex));

            return GetDeltaText(
                docTry.PeptideGroups.Where(g => !g.IsPeptideList).Count(g => !testProteins.Contains(g.Id.GlobalIndex)),
                docTry.MoleculeGroups.Count(g => !testGroups.Contains(g.Id.GlobalIndex)),
                docTry.Peptides.Count(p => !testPeptides.Contains(p.Id.GlobalIndex)),
                docTry.Molecules.Count(m => !testMolecules.Contains(m.Id.GlobalIndex)),
                docTry.MoleculeTransitionGroups.Count(p => !testPrecursors.Contains(p.Id.GlobalIndex)),
                docTry.MoleculeTransitions.Count(t => !testTransitions.Contains(t.Id.GlobalIndex)));
        }

        public static string RemovedText(int prot, int list, int pep, int mol, int prec, int tran)
        {
            string deltaText = GetDeltaText(prot, prot + list, pep, pep + mol, prec, tran);
            if (deltaText == null)
                return null;
            return string.Format(Resources.CommandLine_LogDocumentDelta_Removed___0_, deltaText);
        }

        public static string AddedText(int prot, int list, int pep, int mol, int prec, int tran)
        {
            string deltaText = GetDeltaText(prot, prot + list, pep, pep + mol, prec, tran);
            if (deltaText == null)
                return null;
            return string.Format(Resources.CommandLine_LogDocumentDelta_Added___0_, deltaText);
        }

        private static string GetDeltaText(int prot, int allGroup, int pep, int allMol, int prec, int tran)
        {
            var notInTestSet = new List<string>();
            AddDeltaText(notInTestSet, @"prot", prot);
            AddDeltaText(notInTestSet, @"list", allGroup - prot);
            AddDeltaText(notInTestSet, @"pep", pep);
            AddDeltaText(notInTestSet, @"mol", allMol - pep);
            AddDeltaText(notInTestSet, @"prec", prec);
            AddDeltaText(notInTestSet, @"tran", tran);
            return notInTestSet.Count > 0 ? string.Join(@", ", notInTestSet) : null;
        }

        private static void AddDeltaText(List<string> deltas, string typeName, int value)
        {
            if (value > 0)
                deltas.Add(TextUtil.SpaceSeparate(value.ToString(), typeName));
        }

        private bool PerformExportOperations(CommandArgs commandArgs)
        {
            if (commandArgs.ExportingReport)
            {
                if (!ExportReport(commandArgs.ReportName, commandArgs.ReportFile,
                    commandArgs.ReportColumnSeparator, commandArgs.IsReportInvariant))
                {
                    return false;
                }
            }

            if (commandArgs.ExportingChromatograms)
            {
                if (!ExportChromatograms(commandArgs.ChromatogramsFile, commandArgs.ChromatogramsPrecursors,
                    commandArgs.ChromatogramsProducts,
                    commandArgs.ChromatogramsBasePeaks, commandArgs.ChromatogramsTics))
                {
                    return false;
                }
            }

            var exportTypes =
                (string.IsNullOrEmpty(commandArgs.IsolationListInstrumentType) ? 0 : 1) +
                (string.IsNullOrEmpty(commandArgs.TransListInstrumentType) ? 0 : 1) +
                (string.IsNullOrEmpty(commandArgs.MethodInstrumentType) ? 0 : 1);
            if (exportTypes > 1)
            {
                _out.WriteLine(Resources.CommandLine_Run_Error__You_cannot_simultaneously_export_a_transition_list_and_a_method___Neither_will_be_exported__);
                return false;
            }
            else
            {
                if (commandArgs.ExportingIsolationList)
                {
                    if (!ExportInstrumentFile(ExportFileType.IsolationList, commandArgs))
                    {
                        return false;
                    }
                }

                if (commandArgs.ExportingTransitionList)
                {
                    if (!ExportInstrumentFile(ExportFileType.List, commandArgs))
                    {
                        return false;
                    }
                }

                if (commandArgs.ExportingMethod)
                {
                    if (!ExportInstrumentFile(ExportFileType.Method, commandArgs))
                    {
                        return false;
                    }
                }
            }

            if (Document != null && Document.Settings.HasResults)
            {
                CollectionUtil.ForEach(Document.Settings.MeasuredResults.ReadStreams, s => s.CloseStream());
            }
            if (commandArgs.SharingZipFile)
            {
                string sharedFileName;
                var sharedFileDir = Path.GetDirectoryName(_skylineFile) ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(commandArgs.SharedFile))
                {
                    sharedFileName = Path.GetFileName(commandArgs.SharedFile.Trim());
                    if (!PathEx.HasExtension(sharedFileName, SrmDocumentSharing.EXT_SKY_ZIP))
                    {
                        sharedFileName = Path.GetFileNameWithoutExtension(sharedFileName) + SrmDocumentSharing.EXT_SKY_ZIP;
                    }
                    var dir = Path.GetDirectoryName(commandArgs.SharedFile);
                    sharedFileDir = string.IsNullOrEmpty(dir) ? sharedFileDir : dir;
                }
                else
                {
                    sharedFileName = FileEx.GetTimeStampedFileName(_skylineFile);
                }
                var sharedFilePath = Path.Combine(sharedFileDir, sharedFileName);
                if (!ShareDocument(_doc, _skylineFile, sharedFilePath, commandArgs.SharedFileType, _out))
                {
                    return false;
                }
            }
            if (commandArgs.PublishingToPanorama)
            {
                // Publish the document to the given Panorama server if new results were added to the document
                // OR no results files were given on the command-line for importing to the document. 
                if (_importedResults || !commandArgs.ImportingResults)
                {
                    // Publish document to the given folder on the Panorama Server
                    var panoramaHelper = new PanoramaPublishHelper(_out);
                    return panoramaHelper.PublishToPanorama(commandArgs.PanoramaServer, _doc, _skylineFile,
                        commandArgs.PanoramaFolder, commandArgs.SharedFileType);
                }
                else
                {
                    // If we are here it means that ImportingResults was true AND nothing was imported.
                    // This should have already triggered an error message earlier in the process but 
                    // in case it didn't we will report an error and return false
                    _out.WriteLine(Resources.CommandLine_PerformExportOperations_Error__No_new_results_added__Skipping_Panorama_import_);
                    return false;
                }
            }

            return true;
        }

        public void SetDocument(SrmDocument doc)
        {
            ModifyDocument(d => doc);
        }

        public void ModifyDocument(Func<SrmDocument, SrmDocument> act)
        {
            ModifyDocument(act, null);
        }

        public void ModifyDocument(Func<SrmDocument, SrmDocument> act, Func<SrmDocumentPair, AuditLogEntry> logFunc)
        {
            var docOriginal = _doc;
            _doc = act(_doc);
            // If nothing changed, don't create a new audit log entry, just like SkylineWindow.ModifyDocument
            if (ReferenceEquals(_doc, docOriginal))
                return;
            var docPair = SrmDocumentPair.Create(docOriginal, _doc, SrmDocument.DOCUMENT_TYPE.none);
            var logEntry = logFunc?.Invoke(docPair);
            if (logEntry != null)
                _doc = AuditLogEntry.UpdateDocument(logEntry, docPair);
        }

        public void ModifyDocumentWithLogging(Func<SrmDocument, SrmDocument> act, Func<SrmDocumentPair, AuditLogEntry> logFunc)
        {
            var setSeenEntries = GetSeenAuditLogEntries();
            var docBefore = Document;
            if (!docBefore.Settings.DataSettings.AuditLogging)
                _doc = AuditLogList.ToggleAuditLogging(_doc, true);
            ModifyDocument(act, logFunc);
            LogNewEntries(Document.AuditLog.AuditLogEntries, setSeenEntries);
            LogDocumentDelta(docBefore, Document);
            if (!docBefore.Settings.DataSettings.AuditLogging)
                _doc = AuditLogList.ToggleAuditLogging(_doc, false);
        }

        private bool SetPredictTranSettings(CommandArgs commandArgs)
        {
            try
            {
                ModifyDocumentWithLogging(doc => doc.ChangeSettings(doc.Settings.ChangeTransitionPrediction(p =>
                {
                    if (commandArgs.PredictCEName != null)
                        p = p.ChangeCollisionEnergy(Settings.Default.GetCollisionEnergyByName(commandArgs.PredictCEName));
                    if (commandArgs.PredictDPName != null)
                        p = p.ChangeDeclusteringPotential(Settings.Default.GetDeclusterPotentialByName(commandArgs.PredictDPName));
                    if (commandArgs.PredictCoVName != null)
                        p = p.ChangeCompensationVoltage(Settings.Default.GetCompensationVoltageByName(commandArgs.PredictCoVName));
                    if (commandArgs.PredictOpimizationLibraryName != null)
                        p = p.ChangeOptimizationLibrary(Settings.Default.GetOptimizationLibraryByName(commandArgs.PredictOpimizationLibraryName));
                    return p;
                })), AuditLogEntry.SettingsLogFunction);
                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_SetPredictTranSettings_Error__Failed_attempting_to_change_the_transition_prediction_settings_);
                _out.WriteLine(x.Message);
                return false;
            }
        }

        private bool SetFilterSettings(CommandArgs commandArgs)
        {
            try
            {
                ModifyDocumentWithLogging(doc => doc.ChangeSettings(doc.Settings.ChangeTransitionFilter(f =>
                {
                    if (commandArgs.FilterPrecursorCharges != null)
                        f = f.ChangePeptidePrecursorCharges(commandArgs.FilterPrecursorCharges);
                    if (commandArgs.FilterProductCharges != null)
                        f = f.ChangePeptideProductCharges(commandArgs.FilterProductCharges);
                    if (commandArgs.FilterProductTypes != null)
                        f = f.ChangePeptideIonTypes(commandArgs.FilterProductTypes);
                    return f;
                })), AuditLogEntry.SettingsLogFunction);
                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_SetFilterSettings_Error__Failed_attempting_to_change_the_transition_filter_settings_);
                _out.WriteLine(x.Message);
                return false;
            }
        }

        private bool SetFullScanSettings(CommandArgs commandArgs)
        {
            try
            {
                if (commandArgs.FullScanPrecursorRes.HasValue)
                {
                    double res = commandArgs.FullScanPrecursorRes.Value;
                    double? resMz = commandArgs.FullScanPrecursorResMz;
                    if (!_doc.Settings.TransitionSettings.FullScan.IsHighResPrecursor)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_precursor_resolution_to__0__, res);
                    else if (_doc.Settings.TransitionSettings.FullScan.IsCentroidedMs)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_precursor_mass_accuracy_to__0__ppm_, res);
                    else if (resMz.HasValue)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_precursor_resolving_power_to__0__at__1__, res, resMz);
                    else
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_precursor_resolving_power_to__0__, res);

                    ModifyDocument(d => d.ChangeSettings(_doc.Settings.ChangeTransitionFullScan(f =>
                        f.ChangePrecursorResolution(f.PrecursorMassAnalyzer, res, resMz ?? f.PrecursorResMz))), AuditLogEntry.SettingsLogFunction);
                }
                if (commandArgs.FullScanProductRes.HasValue)
                {
                    double res = commandArgs.FullScanProductRes.Value;
                    double? resMz = commandArgs.FullScanProductResMz;
                    if (!_doc.Settings.TransitionSettings.FullScan.IsHighResProduct)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_product_resolution_to__0__, res);
                    else if (_doc.Settings.TransitionSettings.FullScan.IsCentroidedMsMs)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_product_mass_accuracy_to__0__ppm_, res);
                    else if (resMz.HasValue)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_product_resolving_power_to__0__at__1__, res, resMz);
                    else
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_product_resolving_power_to__0__, res);

                    ModifyDocument(d => d.ChangeSettings(_doc.Settings.ChangeTransitionFullScan(f =>
                        f.ChangeProductResolution(f.ProductMassAnalyzer, res, resMz ?? f.ProductResMz))), AuditLogEntry.SettingsLogFunction);
                }
                if (commandArgs.FullScanRetentionTimeFilterLength.HasValue)
                {
                    double rtLen = commandArgs.FullScanRetentionTimeFilterLength.Value;
                    if (_doc.Settings.TransitionSettings.FullScan.RetentionTimeFilterType == RetentionTimeFilterType.scheduling_windows)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_extraction_to______0__minutes_from_predicted_value_, rtLen);
                    else if (_doc.Settings.TransitionSettings.FullScan.RetentionTimeFilterType == RetentionTimeFilterType.ms2_ids)
                        _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Changing_full_scan_extraction_to______0__minutes_from_MS_MS_IDs_, rtLen);

                    ModifyDocument(d => d.ChangeSettings(_doc.Settings.ChangeTransitionFullScan(f =>
                        f.ChangeRetentionTimeFilter(f.RetentionTimeFilterType, rtLen))), AuditLogEntry.SettingsLogFunction);
                }
                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_SetFullScanSettings_Error__Failed_attempting_to_change_the_transiton_full_scan_settings_);
                _out.WriteLine(x.Message);
                return false;
            }
        }

        private bool SetImsSettings(CommandArgs commandArgs)
        {
            try
            {
                if (commandArgs.IonMobilityLibraryRes.HasValue)
                {
                    if (!_doc.Settings.TransitionSettings.IonMobilityFiltering.UseSpectralLibraryIonMobilityValues)
                        _out.WriteLine(Resources.CommandLine_SetImsSettings_Enabling_extraction_based_on_spectral_library_ion_mobility_values_);
                    double rp = commandArgs.IonMobilityLibraryRes.Value;
                    var imsWindowCalcNew = new IonMobilityWindowWidthCalculator(rp);
                    var imsWindowCalc = _doc.Settings.TransitionSettings.IonMobilityFiltering.FilterWindowWidthCalculator;
                    if (!Equals(imsWindowCalc, imsWindowCalcNew))
                        _out.WriteLine(Resources.CommandLine_SetImsSettings_Changing_ion_mobility_spectral_library_resolving_power_to__0__, rp);
                    ModifyDocument(d => d.ChangeSettings(d.Settings.ChangeTransitionIonMobilityFiltering(p =>
                        {
                            var result = p;
                            if (!result.UseSpectralLibraryIonMobilityValues)
                                result = result.ChangeUseSpectralLibraryIonMobilityValues(true);
                            if (!Equals(result.FilterWindowWidthCalculator, imsWindowCalcNew))
                                result = result.ChangeFilterWindowWidthCalculator(imsWindowCalcNew);
                            return result;
                        })),
                        AuditLogEntry.SettingsLogFunction);
                }
                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_SetImsSettings_Error__Failed_attempting_to_change_the_ion_mobility_settings_);
                _out.WriteLine(x.Message);
                return false;
            }
        }

        public bool OpenSkyFile(string skylineFile)
        {
            try
            {
                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
                string hash;
                using (var hashingStreamReader = new HashingStreamReaderWithProgress(skylineFile, progressMonitor))
                {
                    // Wrap stream in XmlReader so that BaseUri is known
                    var reader = XmlReader.Create(hashingStreamReader, 
                        new XmlReaderSettings() { IgnoreWhitespace = true }, 
                        skylineFile);  
                    XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
                    _out.WriteLine(Resources.CommandLine_OpenSkyFile_Opening_file___);

                    SetDocument(ConnectDocument((SrmDocument)xmlSerializer.Deserialize(reader), skylineFile));
                    if (_doc == null)
                        return false;

                    _out.WriteLine(Resources.CommandLine_OpenSkyFile_File__0__opened_, Path.GetFileName(skylineFile));
                    hash = hashingStreamReader.Stream.Done();
                }

                SetDocument(_doc.ReadAuditLog(skylineFile, hash, () => null));

                // Update settings for this file
                _doc.Settings.UpdateLists(skylineFile);
            }
            catch (FileNotFoundException)
            {
                _out.WriteLine(Resources.CommandLine_OpenSkyFile_Error__The_Skyline_file__0__does_not_exist_, skylineFile);
                return false;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_OpenSkyFile_Error__There_was_an_error_opening_the_file__0_, skylineFile);
                _out.WriteLine(XmlUtil.GetInvalidDataMessage(skylineFile, x));
                return false;
            }

            return true;
        }

        private SrmDocument ConnectDocument(SrmDocument document, string path)
        {
            document = ConnectLibrarySpecs(document, path);
            if (document != null)
                document = ConnectBackgroundProteome(document, path);
            if (document != null)
                document = ConnectIrtDatabase(document, path);
            if (document != null)
                document = ConnectOptimizationDatabase(document, path);
            if (document != null)
                document = ConnectIonMobilityDatabase(document, path);
            return document;
        }

        private SrmDocument ConnectLibrarySpecs(SrmDocument document, string documentPath)
        {
            string docLibFile = null;
            if (!string.IsNullOrEmpty(documentPath) && document.Settings.PeptideSettings.Libraries.HasDocumentLibrary)
            {
                docLibFile = BiblioSpecLiteSpec.GetLibraryFileName(documentPath);
                if (!File.Exists(docLibFile))
                {
                    _out.WriteLine(Resources.CommandLine_ConnectLibrarySpecs_Error__Could_not_find_the_spectral_library__0__for_this_document_, docLibFile);
                    return null;
                }
            }

            var settings = document.Settings.ConnectLibrarySpecs((library, librarySpec) =>
            {
                string name = library != null ? library.Name : librarySpec.Name;
                LibrarySpec spec;
                if (Settings.Default.SpectralLibraryList.TryGetValue(name, out spec))
                {
                    if (File.Exists(spec.FilePath))
                        return spec;
                }

                string fileName = library != null ? library.FileNameHint : Path.GetFileName(librarySpec.FilePath);
                if (fileName != null)
                {
                    // First look for the file name in the document directory
                    string pathLibrary = PathEx.FindExistingRelativeFile(documentPath, fileName);
                    if (pathLibrary != null)
                        return CreateLibrarySpec(library, librarySpec, pathLibrary, true);
                    // In the user's default library directory
                    pathLibrary = Path.Combine(Settings.Default.LibraryDirectory, fileName);
                    if (File.Exists(pathLibrary))
                        return CreateLibrarySpec(library, librarySpec, pathLibrary, false);
                }
                _out.WriteLine(Resources.CommandLine_ConnectLibrarySpecs_Warning__Could_not_find_the_spectral_library__0_, name);
                return CreateLibrarySpec(library, librarySpec, null, false);
            }, docLibFile);

            if (ReferenceEquals(settings, document.Settings))
                return document;

            // If the libraries were moved to disconnected state, then avoid updating
            // the document tree for this change, or it will strip all the library
            // information off the document nodes.
            if (settings.PeptideSettings.Libraries.DisconnectedLibraries != null)
                return document.ChangeSettingsNoDiff(settings);

            return document.ChangeSettings(settings);
        }

        private static LibrarySpec CreateLibrarySpec(Library library, LibrarySpec librarySpec, string pathLibrary, bool local)
        {
            var newLibrarySpec = library != null
                ? library.CreateSpec(pathLibrary)
                : librarySpec.ChangeFilePath(pathLibrary);
            if (local)
                newLibrarySpec = newLibrarySpec.ChangeDocumentLocal(true);
            return newLibrarySpec;
        }

        private SrmDocument ConnectIrtDatabase(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectIrtDatabase(calc => FindIrtDatabase(documentPath, calc));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }


        private RCalcIrt FindIrtDatabase(string documentPath, RCalcIrt irtCalc)
        {
            RetentionScoreCalculatorSpec result;
            if (Settings.Default.RTScoreCalculatorList.TryGetValue(irtCalc.Name, out result))
            {
                var irtCalcSettings = result as RCalcIrt;
                if (irtCalcSettings != null && (irtCalcSettings.IsUsable || File.Exists(irtCalcSettings.DatabasePath)))
                {
                    return irtCalcSettings;
                }
            }

            // First look for the file name in the document directory
            string filePath = PathEx.FindExistingRelativeFile(documentPath, irtCalc.DatabasePath);
            if (filePath != null)
            {
                try
                {
                    return irtCalc.ChangeDatabasePath(filePath);
                }
                catch (CalculatorException)
                {
                    //Todo: should this fail silenty or report an error
                }
            }

            _out.WriteLine(Resources.CommandLine_FindIrtDatabase_Error__Could_not_find_the_iRT_database__0__, Path.GetFileName(irtCalc.DatabasePath));
            return null;
        }

        private SrmDocument ConnectOptimizationDatabase(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectOptimizationDatabase(lib => FindOptimizationDatabase(documentPath, lib));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private OptimizationLibrary FindOptimizationDatabase(string documentPath, OptimizationLibrary optLib)
        {
            OptimizationLibrary result;
            if (Settings.Default.OptimizationLibraryList.TryGetValue(optLib.Name, out result))
            {
                if (result.IsNone || File.Exists(result.DatabasePath))
                    return result;
            }

            // First look for the file name in the document directory
            string filePath = PathEx.FindExistingRelativeFile(documentPath, optLib.DatabasePath);
            if (filePath != null)
            {
                try
                {
                    return optLib.ChangeDatabasePath(filePath);
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                    //Todo: should this fail silenty or report an error
                }
            }

            _out.WriteLine(Resources.CommandLine_FindOptimizationDatabase_Could_not_find_the_optimization_library__0__, Path.GetFileName(optLib.DatabasePath));
            return null;
        }

        private SrmDocument ConnectIonMobilityDatabase(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectIonMobilityLibrary(imsdb => FindIonMobilityDatabase(documentPath, imsdb));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private IonMobilityLibrary FindIonMobilityDatabase(string documentPath, IonMobilityLibrary ionMobilityLibSpec)
        {

            if (Settings.Default.IonMobilityLibraryList.TryGetValue(ionMobilityLibSpec.Name, out var result))
            {
                if (result.IsNone || File.Exists(result.FilePath))
                    return result;                
            }

            // First look for the file name in the document directory
            string filePath = PathEx.FindExistingRelativeFile(documentPath, ionMobilityLibSpec.FilePath);
            if (filePath != null)
            {
                try
                {
                    return ionMobilityLibSpec.ChangeDatabasePath(filePath);
                }
// ReSharper disable once EmptyGeneralCatchClause
                catch
                {
                    //Todo: should this fail silenty or report an error
                }
            }

            _out.WriteLine(Resources.CommandLine_FindIonMobilityDatabase_Error__Could_not_find_the_ion_mobility_library__0__, Path.GetFileName(ionMobilityLibSpec.FilePath));
            return null;
        }

        private SrmDocument ConnectBackgroundProteome(SrmDocument document, string documentPath)
        {
            var settings = document.Settings.ConnectBackgroundProteome(backgroundProteomeSpec =>
                FindBackgroundProteome(documentPath, backgroundProteomeSpec));
            if (settings == null)
                return null;
            if (ReferenceEquals(settings, document.Settings))
                return document;
            return document.ChangeSettings(settings);
        }

        private BackgroundProteomeSpec FindBackgroundProteome(string documentPath, BackgroundProteomeSpec backgroundProteomeSpec)
        {
            var result = Settings.Default.BackgroundProteomeList.GetBackgroundProteomeSpec(backgroundProteomeSpec.Name);
            if (result != null)
            {
                if (result.IsNone || File.Exists(result.DatabasePath))
                    return result;                
            }

            // First look for the file name in the document directory
            string pathBackgroundProteome = PathEx.FindExistingRelativeFile(documentPath, backgroundProteomeSpec.DatabasePath);
            if (pathBackgroundProteome != null)
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            // In the user's default library directory
            string fileName = Path.GetFileName(backgroundProteomeSpec.DatabasePath);
            pathBackgroundProteome = Path.Combine(Settings.Default.ProteomeDbDirectory, fileName ?? string.Empty);
            if (File.Exists(pathBackgroundProteome))
                return new BackgroundProteomeSpec(backgroundProteomeSpec.Name, pathBackgroundProteome);
            _out.WriteLine(Resources.CommandLine_FindBackgroundProteome_Warning__Could_not_find_the_background_proteome_file__0__, Path.GetFileName(fileName));
            return BackgroundProteomeList.GetDefault();
        }

        public bool ImportResultsInDir(string sourceDir, bool recursive, Regex namingPattern, string replicateName,
            LockMassParameters lockMassParameters,
            DateTime? importBefore, DateTime? importOnOrAfter,
            Regex fileNameRegex, Regex sampleNameRegex,
            OptimizableRegression optimize, bool disableJoining, bool warnOnFailure)
        {
            var listNamedPaths = GetDataSources(sourceDir, recursive, namingPattern, lockMassParameters);
            if (listNamedPaths == null || listNamedPaths.Count == 0)
            {
                return false;
            }

            if (!ApplyFileAndSampleNameRegex(fileNameRegex, sampleNameRegex, ref listNamedPaths))
            {
                return false;
            }

            // If there is a single name for everything then it should override any naming from GetDataSources
            if (!string.IsNullOrEmpty(replicateName))
            {
                // If the name exists, files should get imported into it
                listNamedPaths = new[] { new KeyValuePair<string, MsDataFileUri[]>(replicateName, listNamedPaths.SelectMany(s => s.Value).ToArray()) };
            }

            // Remove replicates and/or files that have already been imported into the document
            RemoveImportedFiles(listNamedPaths, out var listNewPaths,
                string.IsNullOrEmpty(replicateName) /*If a replicate name is not given, remove file paths imported into any replicate.*/);
            if (listNewPaths.Count == 0)
            {
                _out.WriteLine(Resources.CommandLine_ImportResults_Error__No_files_left_to_import_);
                return false;
            }

            listNamedPaths = listNewPaths;
            if(string.IsNullOrEmpty(replicateName))
            {  
                // If a replicate name is not given new replicates will be created and should have new unique names.  
                MakeReplicateNamesUnique(listNamedPaths);
            }

            return ImportDataFiles(listNamedPaths, lockMassParameters, importBefore, importOnOrAfter,
                optimize, disableJoining, warnOnFailure);
        }

        private void MakeReplicateNamesUnique(IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths)
        {
            var replicatesInDoc =
                _doc.Settings.HasResults ? _doc.MeasuredResults.Chromatograms.Select(chrom => chrom.Name).ToHashSet() : new HashSet<string>();
            var replicatesInNamedPaths = listNamedPaths.Select(path => path.Key).ToList();
            var newNames = Helpers.EnsureUniqueNames(replicatesInNamedPaths, replicatesInDoc);
            for (var i = 0; i < listNamedPaths.Count; i++)
            {
                var namedPath = listNamedPaths[i];
                if (!Equals(newNames[i], namedPath.Key))
                {
                    _out.WriteLine(
                        Resources.CommandLine_MakeReplicateNamesUnique_Replicate___0___already_exists_in_the_document__using___1___instead_,
                        namedPath.Key, newNames[i]);
                    listNamedPaths[i] = new KeyValuePair<string, MsDataFileUri[]>(newNames[i], namedPath.Value);
                }
            }
        }

        public bool ApplyFileAndSampleNameRegex(Regex fileNameRegex, Regex sampleNameRegex, ref IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths)
        {
            listNamedPaths = ApplyNameRegex(listNamedPaths, ApplyFileNameRegex, fileNameRegex);
            if (listNamedPaths.Count == 0)
            {
                _out.WriteLine(Resources.CommandLine_ApplyFileAndSampleNameRegex_Error__No_files_match_the_file_name_pattern___0___, fileNameRegex);
                return false;
            }

            listNamedPaths = ApplyNameRegex(listNamedPaths, ApplySampleNameRegex, sampleNameRegex);
            if (listNamedPaths.Count == 0)
            {
                _out.WriteLine(Resources.CommandLine_ApplyFileAndSampleNameRegex_Error__No_files_match_the_sample_name_pattern___0___, sampleNameRegex);
                return false;
            }

            return true;
        }

        private IList<KeyValuePair<string, MsDataFileUri[]>> ApplyNameRegex(IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths,
                        Func<MsDataFileUri, Regex, bool> applyRegex,  Regex regex)
        {
            var newNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();
            if (regex == null)
            {
                newNamedPaths.AddRange(listNamedPaths);
                return newNamedPaths;
            }

            for (var i = 0; i < listNamedPaths.Count; i++)
            {
                var namedPath = listNamedPaths[i];
                var files = namedPath.Value;

                var newFiles = files.Where(file => applyRegex(file, regex)).ToArray();
                if (newFiles.Length > 0)
                {
                    newNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(namedPath.Key, newFiles));
                }
            }
            
            return newNamedPaths;
        }

        private bool ApplyFileNameRegex(MsDataFileUri file, Regex regex)
        {
            if(!ApplyRegex(file.GetFileName(), regex))
            {
                _out.WriteLine(Resources.CommandLine_ApplyFileNameRegex_File_name___0___does_not_match_the_pattern___1____Ignoring__2_, file.GetFileName(), regex, file);
                return false;
            }
            return true;
        }
        
        private bool ApplySampleNameRegex(MsDataFileUri file, Regex regex)
        {
            if (string.IsNullOrEmpty(file.GetSampleName()))
            {
                _out.WriteLine(Resources.CommandLine_ApplySampleNameRegex_File___0___does_not_have_a_sample__Cannot_apply_sample_name_pattern__Ignoring_, file);
                return false;
            }

            if (!ApplyRegex(file.GetSampleName(), regex))
            {
                _out.WriteLine(Resources.CommandLine_ApplySampleNameRegex_Sample_name___0___does_not_match_the_pattern___1____Ignoring__2_, file.GetSampleName(), regex, file);
                return false;
            }

            return true;
        }

        private bool ApplyRegex(string name, Regex regex)
        {
            if (regex != null)
            {
                var match = regex.Match(name);
                return match.Success;
            }
            return true;
        }

        private bool ImportDataFiles(IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths, LockMassParameters lockMassParameters,
            DateTime? importBefore, DateTime? importOnOrAfter, OptimizableRegression optimize, bool disableJoining, bool warnOnFailure)
        {
            var namesAndFilePaths = GetNamesAndFilePaths(listNamedPaths).ToArray();
            bool hasMultiple = namesAndFilePaths.Length > 1;
            if (hasMultiple || disableJoining)
            {
                // Join at the end
                ModifyDocument(d => d.ChangeSettingsNoDiff(d.Settings.ChangeIsResultsJoiningDisabled(true)));
            }

            DocContainer.SetDocument(_doc, DocContainer.Document, true);

            // If this has not already been designated a multi-process import, and no specific
            // number of threads has been chosen, then choose dynamically based on the computer
            // specs as many as make sense.
            if (!Program.MultiProcImport && DocContainer.ChromatogramManager.LoadingThreads == 0)
            {
                DocContainer.ChromatogramManager.LoadingThreads = MultiFileLoader.GetOptimalThreadCount(null,
                    namesAndFilePaths.Length, MultiFileLoader.ImportResultsSimultaneousFileOptions.many);
            }

            // Add files to import list
            _out.WriteLine();
            for (int i = 0; i < namesAndFilePaths.Length; i++)
            {
                var namePath = namesAndFilePaths[i];
                if (!ImportResultsFile(namePath.FilePath.ChangeLockMassParameters(lockMassParameters), namePath.ReplicateName, importBefore, importOnOrAfter, optimize))
                    return false;
                _out.WriteLine(@"{0}. {1}", i + 1, namePath.FilePath);
            }
            _out.WriteLine();

            DocContainer.WaitForComplete();
            // Remember if an error occurred, in case LastProgress is not an instance of MultiProgressStatus
            var lastProgress = DocContainer.LastProgress;
            var isError = lastProgress != null && lastProgress.IsError;
            var multiStatus = lastProgress as MultiProgressStatus;
            // UGH. Because of the way imports remove failing files,
            // we can actually return from WaitForComplete() above before
            // the final status has been set. So, wait for a full second
            // for it to become final.
            if (warnOnFailure && Program.UnitTest) // Hack to get us past a race condition in TeamCity code coverage config
            {
               //  TODO: figure out a way to confirm that document is actually done processing errors
                Thread.Sleep(1000);
            }
            for (int i = 0; i < 10; i++)
            {
                if (multiStatus == null || multiStatus.IsFinal)
                    break;

                Thread.Sleep(100);
                lastProgress = DocContainer.LastProgress;
                isError = lastProgress != null && lastProgress.IsError;
                multiStatus = lastProgress as MultiProgressStatus;
            }

            SetDocument(DocContainer.Document);
            DocContainer.ResetProgress();

            if (_doc.Settings.HasResults)
            {
                if (multiStatus == null)
                {
                    // Can happen with import when joining is required
                    if (isError)
                        return false;

                    _importedResults = true;
                }
                else
                {
                    // Store whether anything imported successfully.
                    // CONSIDER: Should this be changed to store whether their were no errors? May be strange
                    //           to continue upload to Panorama after reporting import errors.
                    _importedResults = multiStatus.ProgressList.Any(s => s.IsComplete);

                    if (multiStatus.IsError)
                    {
                        if (!warnOnFailure)
                            return false;

                        var chromatograms = new List<ChromatogramSet>();
                        for (int i = 0; i < _doc.Settings.MeasuredResults.Chromatograms.Count; i++)
                        {
                            var modifiedSet = RemoveErrors(_doc.Settings.MeasuredResults.Chromatograms[i], multiStatus);
                            if (modifiedSet != null)
                                chromatograms.Add(modifiedSet);
                        }

                        ModifyDocument(d =>
                            d.ChangeMeasuredResults(chromatograms.Any()
                                ? d.Settings.MeasuredResults.ChangeChromatograms(chromatograms)
                                : null));
                    }
                }
            }

            if (hasMultiple && !disableJoining)
            {
                // Allow joining to happen
                ModifyDocument(d => d.ChangeSettingsNoDiff(d.Settings.ChangeIsResultsJoiningDisabled(false)));
                if (!_doc.IsLoaded)
                {
                    DocContainer.SetDocument(_doc, DocContainer.Document, true);
                    SetDocument(DocContainer.Document);
                    DocContainer.ResetProgress();
                    // If not fully loaded now, there must have been an error.
                    if (!_doc.IsLoaded)
                        return false;
                }
            }

            return true;
        }

        private ChromatogramSet RemoveErrors(ChromatogramSet set, MultiProgressStatus multiStatus)
        {
            var dataFiles = set.MSDataFilePaths.ToList();
            int originalCount = dataFiles.Count;
            for (int i = originalCount - 1; i >= 0; i--)
            {
                var status = multiStatus.GetStatus(dataFiles[i]);
                if (status != null && status.IsError)
                    dataFiles.RemoveAt(i);
            }

            if (dataFiles.Count == 0)
                return null;
            return dataFiles.Count == originalCount ? set : set.ChangeMSDataFilePaths(dataFiles);
        }

        private IEnumerable<NameAndPath> GetNamesAndFilePaths(IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths)
        {
            foreach (var namedPaths in listNamedPaths)
            {
                foreach (var file in namedPaths.Value)
                {
                    yield return new NameAndPath(namedPaths.Key, file);
                }
            }
        }

        private struct NameAndPath
        {
            public NameAndPath(string replicateName, MsDataFileUri filePath) : this()
            {
                ReplicateName = replicateName;
                FilePath = filePath;
            }

            public string ReplicateName { get; private set; }
            public MsDataFileUri FilePath { get; private set; }
        }

        private IList<KeyValuePair<string, MsDataFileUri[]>> GetDataSources(string sourceDir, bool recursive, Regex namingPattern, LockMassParameters lockMassParameters)
        {
            // get all the valid data sources (files and sub directories) in this directory.
            IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths;
            try
            {
                listNamedPaths = DataSourceUtil.GetDataSources(sourceDir, recursive).ToArray();
            }
            catch(IOException e)
            {
                _out.WriteLine(Resources.CommandLine_GetDataSources_Error__Failure_reading_file_information_from_directory__0__, sourceDir);
                _out.WriteLine(e.Message);
                return null;
            }
            if (!listNamedPaths.Any())
            {
                _out.WriteLine(Resources.CommandLine_GetDataSources_Error__No_data_sources_found_in_directory__0__, sourceDir);
                return null;
            }

            // If we were given a regular expression apply it to the replicate names
            if(namingPattern != null)
            {
                List<KeyValuePair<string, MsDataFileUri[]>> listRenamedPaths;
                if(!ApplyNamingPattern(listNamedPaths, namingPattern, out listRenamedPaths))
                {
                    return null;
                }
                listNamedPaths = listRenamedPaths;
            }
            
            return listNamedPaths;
        }

        private bool ApplyNamingPattern(IEnumerable<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths, Regex namingPattern, 
                                        out List<KeyValuePair<string, MsDataFileUri[]>> listRenamedPaths)
        {
            listRenamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();

            var uniqNames = new HashSet<string>();

            foreach (var namedPaths in listNamedPaths)
            {
                var replName = namedPaths.Key;
                Match match = namingPattern.Match(replName);
                if (match.Success)
                {
                    // Get the value of the first group
                    var replNameNew = match.Groups[1].Value;
                    if (string.IsNullOrEmpty(replNameNew))
                    {
                        _out.WriteLine(Resources.CommandLine_ApplyNamingPattern_Error__Match_to_regular_expression_is_empty_for__0__, replName);
                        return false;
                    }                    
                    if (uniqNames.Contains(replNameNew))
                    {
                        _out.WriteLine(Resources.CommandLine_ApplyNamingPattern_Error__Duplicate_replicate_name___0___after_applying_regular_expression_, replNameNew);
                        return false;
                    }
                    uniqNames.Add(replNameNew);
                    listRenamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(replNameNew, namedPaths.Value));
                }
                else
                {
                    _out.WriteLine(Resources.CommandLine_ApplyNamingPattern_Error___0__does_not_match_the_regular_expression_, replName);
                    return false;
                }
            }

            return true;
        }

        private void RemoveImportedFiles(IEnumerable<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths,
            out List<KeyValuePair<string, MsDataFileUri[]>> listNewNamedPaths, bool checkAllReplicates)
        {
            listNewNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();

            if(!_doc.Settings.HasResults)
            {
                listNewNamedPaths.AddRange(listNamedPaths);
                return;
            }

            var chromatFilePaths = new HashSet<MsDataFileUri>();
            if (checkAllReplicates)
            {
                // Get all the file paths imported in the document
                chromatFilePaths =
                    new HashSet<MsDataFileUri>(_doc.Settings.MeasuredResults.MSDataFilePaths.Select(path => path.ToLower()));
            }

            foreach (var namedPaths in listNamedPaths)
            {
                var replicateName = namedPaths.Key;

                if (!checkAllReplicates)
                {
                    // check if the document already has a replicate with this name
                    int indexChrom;
                    ChromatogramSet chromatogram;
                    if (!_doc.Settings.MeasuredResults.TryGetChromatogramSet(replicateName,
                        out chromatogram, out indexChrom))
                    {
                        listNewNamedPaths.Add(namedPaths);
                        continue;
                    }
                    else
                    {
                        // We are appending to an existing replicate in the document.
                        // We will remove files that are already associated with the replicate
                        // Get the files imported in this replicate
                        chromatFilePaths =
                            new HashSet<MsDataFileUri>(chromatogram.MSDataFilePaths.Select(path => path.ToLower()));
                    }
                }

                var filePaths = namedPaths.Value;
                var filePathsNotInRepl = new List<MsDataFileUri>(filePaths.Length);
                foreach (var fpath in filePaths)
                {
                    if (chromatFilePaths.Contains(fpath.ToLower()))
                    {
                        if (checkAllReplicates)
                        {
                            // We are looking at all file paths so get the name of the replicate to which this file belongs.
                            var chromatogram = _doc.Settings.MeasuredResults.Chromatograms.First(c =>
                                c.MSDataFilePaths.Any(path => path.ToLower().Equals(fpath.ToLower())));

                            replicateName = chromatogram.Name;
                        }

                        _out.WriteLine(Resources.CommandLine_RemoveImportedFiles__0______1___Note__The_file_has_already_been_imported__Ignoring___, replicateName, fpath);
                    }
                    else
                    {
                        filePathsNotInRepl.Add(fpath);
                    }
                }

                if (filePathsNotInRepl.Count > 0)
                {
                    listNewNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(replicateName,
                        filePathsNotInRepl.ToArray()));
                }
            }
        }

        private bool ImportDataFilesWithAppend(IList<KeyValuePair<string, MsDataFileUri[]>> listNamedPaths, LockMassParameters lockMassParameters,
            DateTime? importBefore, DateTime? importOnOrAfter, OptimizableRegression optimize, bool disableJoining, bool append, bool warnOnFailure)
        {
            for (int i = 0; i < listNamedPaths.Count; i++)
            {
                var namedPath = listNamedPaths[i];
                string replicateName = namedPath.Key;
                if (_doc.Settings.HasResults && _doc.Settings.MeasuredResults.ContainsChromatogram(replicateName))
                {
                    if (!append)
                    {
                        _out.WriteLine(
                            Resources.CommandLine_ImportDataFilesWithAppend_Error__The_replicate__0__already_exists_in_the_given_document_and_the___import_append_option_is_not_specified___The_replicate_will_not_be_added_to_the_document_,
                            replicateName);
                        return false;
                    }

                    var replicateFiles = namedPath.Value;
                    var newFiles = new List<MsDataFileUri>();
                    foreach (var replicateFile in replicateFiles)
                    {
                        // If we are appending to an existing replicate in the document
                        // make sure this file is not already in the replicate.
                        ChromatogramSet chromatogram;
                        int index;
                        _doc.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out chromatogram, out index);

                        string replicateFileString = replicateFile.ToString();
                        if (chromatogram.MSDataFilePaths.Any(filePath => StringComparer.OrdinalIgnoreCase.Equals(filePath.ToString(), replicateFileString)))
                        {
                            _out.WriteLine(Resources.CommandLine_ImportResultsFile__0______1___Note__The_file_has_already_been_imported__Ignoring___, replicateName, replicateFile);
                        }
                        else
                        {
                            newFiles.Add(replicateFile);
                        }
                    }
                    if (newFiles.Count == 0)
                    {
                        _out.WriteLine(Resources.CommandLine_ImportResults_Error__No_files_left_to_import_);
                        return false;
                    }

                    if (newFiles.Count != replicateFiles.Length)
                        listNamedPaths[i] = new KeyValuePair<string, MsDataFileUri[]>(replicateName, newFiles.ToArray());
                }
            }

            return ImportDataFiles(listNamedPaths, lockMassParameters, importBefore, importOnOrAfter, optimize, disableJoining, warnOnFailure);
        }

        public bool ImportResultsFile(MsDataFileUri replicateFile, string replicateName, DateTime? importBefore, DateTime? importOnOrAfter,
            OptimizableRegression optimize, bool disableJoining = false)
        {
            // Skip if file write time is after importBefore or before importAfter
            try
            {
                var fileLastWriteTime = replicateFile.GetFileLastWriteTime();
                if (importBefore != null && importBefore < fileLastWriteTime)
                {
                    _out.WriteLine(Resources.CommandLine_ImportResultsFile_File_write_date__0__is_after___import_before_date__1___Ignoring___,
                        fileLastWriteTime, importBefore);
                    return true;
                }
                else if (importOnOrAfter != null && importOnOrAfter >= fileLastWriteTime)
                {
                    _out.WriteLine(Resources.CommandLine_ImportResultsFile_File_write_date__0__is_before___import_on_or_after_date__1___Ignoring___, fileLastWriteTime, importOnOrAfter);
                    return true;
                }
            }
            catch (Exception e)
            {
                _out.WriteLine(Resources.CommandLine_ImportResultsInDir_Error__Could_not_get_last_write_time_for_file__0__, replicateFile);
                _out.WriteLine(e);
                return false;
            }

            _out.WriteLine(Resources.CommandLine_ImportResultsFile_Adding_results___);

            if (disableJoining)
                ModifyDocument(d => d.ChangeSettingsNoDiff(d.Settings.ChangeIsResultsJoiningDisabled(true)));

            //This function will also detect whether the replicate exists in the document
            ImportResults(DocContainer, replicateName, replicateFile, optimize);

            return true;
        }

        public void RemoveResults(DateTime? removeBefore)
        {
            if (removeBefore.HasValue)
                _out.WriteLine(Resources.CommandLine_RemoveResults_Removing_results_before_ + removeBefore.Value.ToShortDateString() + @"...");
            else
                _out.WriteLine(Resources.CommandLine_RemoveResults_Removing_all_results);
            var filteredChroms = new List<ChromatogramSet>();
            if (_doc.Settings.MeasuredResults == null)
            {
                // No imported results in the document.
                return;
            }
            foreach (var chromSet in _doc.Settings.MeasuredResults.Chromatograms)
            {
                var listFileInfosRemaining = new ChromFileInfo[0];
                if (removeBefore.HasValue)
                {
                    listFileInfosRemaining = chromSet.MSDataFileInfos.Where(fileInfo =>
                        fileInfo.RunStartTime == null || fileInfo.RunStartTime >= removeBefore).ToArray();
                }
                if (ArrayUtil.ReferencesEqual(listFileInfosRemaining, chromSet.MSDataFileInfos))
                    filteredChroms.Add(chromSet);
                else
                {
                    foreach (var fileInfo in chromSet.MSDataFileInfos.Except(listFileInfosRemaining))
                        _out.WriteLine(Resources.CommandLine_RemoveResults_Removed__0__, fileInfo.FilePath);
                    if (listFileInfosRemaining.Any())
                        filteredChroms.Add(chromSet.ChangeMSDataFileInfos(listFileInfosRemaining));
                }
            }
            if (!ArrayUtil.ReferencesEqual(filteredChroms, _doc.Settings.MeasuredResults.Chromatograms))
            {
                MeasuredResults newMeasuredResults = filteredChroms.Any() ?
                    _doc.Settings.MeasuredResults.ChangeChromatograms(filteredChroms) : null;

                ModifyDocument(d => d.ChangeMeasuredResults(newMeasuredResults));
            }
        }

        public bool MinimizeResults(CommandArgs commandArgs)
        {
            if (!_doc.Settings.HasResults)
            {
                _out.WriteLine(Resources.CommandLine_ReintegratePeaks_Error__You_must_first_import_results_into_the_document_before_reintegrating_);
                return false;
            }

            var saveFile = commandArgs.SaveFile ?? _skylineFile;
            _out.WriteLine(Resources.CommandLine_MinimizeResults_Minimizing_results_to__0_, saveFile);
            if (commandArgs.ChromatogramsDiscard)
                _out.WriteLine(Resources.CommandLine_MinimizeResults_Removing_unused_chromatograms___);
            if (commandArgs.LimitNoise.HasValue)
                _out.WriteLine(Resources.CommandLine_MinimizeResults_Limiting_chromatogram_noise_to______0__minutes_around_peak___, commandArgs.LimitNoise);

            var minimizeResults = Model.MinimizeResults.MinimizeResultsFromDocument(Document, ((statistics, sizeCalculator) =>
            {
                _out.WriteLine(statistics.PercentComplete + @"%");
            }));
            minimizeResults.Settings = minimizeResults.Settings
                .ChangeDiscardUnmatchedChromatograms(commandArgs.ChromatogramsDiscard)
                .ChangeNoiseTimeRange(commandArgs.LimitNoise);
            minimizeResults.MinimizeToFile(saveFile);
            _doc = minimizeResults.Document;
            _skylineFile = saveFile;
            return true;
        }

        private bool ImportSearch(CommandArgs commandArgs)
        {
            var doc = Document;
            try
            {
                return ImportSearchInternal(commandArgs, ref doc);
            }
            finally
            {
                if (doc != null)
                    ImportPeptideSearch.ClosePeptideSearchLibraryStreams(doc);
            }
        }

        private bool ImportSearchInternal(CommandArgs commandArgs, ref SrmDocument doc)
        {
            var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(String.Empty));
            IrtStandard irtStandard = null;
            if (!string.IsNullOrEmpty(commandArgs.IrtStandardName))
            {
                irtStandard = Settings.Default.IrtStandardList.FirstOrDefault(standard => Equals(standard.Name, commandArgs.IrtStandardName));
                if (irtStandard == null)
                {
                    _out.WriteLine(Resources.CommandLine_ImportSearchInternal_The_iRT_standard_name___0___is_invalid_, commandArgs.IrtStandardName);
                    return false;
                }
            }
            var import = new ImportPeptideSearch
            {
                SearchFilenames = commandArgs.SearchResultsFiles.ToArray(),
                CutoffScore = commandArgs.CutoffScore.GetValueOrDefault(),
                IrtStandard = irtStandard
            };

            // Build library
            var builder = import.GetLibBuilder(doc, commandArgs.Saving ? commandArgs.SaveFile : commandArgs.SkylineFile, commandArgs.IncludeAmbiguousMatches);
            builder.PreferEmbeddedSpectra = commandArgs.PreferEmbeddedSpectra;
            ImportPeptideSearch.ClosePeptideSearchLibraryStreams(doc);
            _out.WriteLine(Resources.CommandLine_ImportSearch_Creating_spectral_library_from_files_);
            foreach (var file in commandArgs.SearchResultsFiles)
                _out.WriteLine(Path.GetFileName(file));
            if (!builder.BuildLibrary(progressMonitor))
                return false;

            if (!string.IsNullOrEmpty(builder.AmbiguousMatchesMessage))
                _out.WriteLine(builder.AmbiguousMatchesMessage);

            var docLibSpec = builder.LibrarySpec.ChangeDocumentLibrary(true);

            _out.WriteLine(Resources.CommandLine_ImportSearch_Loading_library);
            var libraryManager = new LibraryManager();
            if (!import.LoadPeptideSearchLibrary(libraryManager, docLibSpec, progressMonitor))
                return false;

            doc = import.AddDocumentSpectralLibrary(doc, docLibSpec);
            if (doc == null)
                return false;

            // Add iRTs
            if (import.IrtStandard != null && !import.IrtStandard.IsEmpty)
            {
                ImportPeptideSearch.GetLibIrtProviders(import.DocLib, import.IrtStandard, progressMonitor,
                    out var irtProviders, out var autoStandards, out var cirtPeptides);
                int? numCirt = null;
                if (cirtPeptides.Length >= RCalcIrt.MIN_PEPTIDES_COUNT)
                {
                    if (!commandArgs.NumCirts.HasValue)
                    {
                        _out.WriteLine(Resources.CommandLine_ImportSearchInternal_Error___0__must_be_set_when_using_CiRT_peptides_, CommandArgs.ARG_IMPORT_PEPTIDE_SEARCH_NUM_CIRTS.Name);
                        return false;
                    }
                    numCirt = commandArgs.NumCirts.Value;
                }
                else if (import.IrtStandard.IsAuto)
                {
                    switch (autoStandards.Count)
                    {
                        case 0:
                            import.IrtStandard = new IrtStandard(XmlNamedElement.NAME_INTERNAL, null, null, IrtPeptidePicker.Pick(irtProviders, 10));
                            break;
                        case 1:
                            import.IrtStandard = autoStandards[0];
                            break;
                        default:
                            _out.WriteLine(Resources.CommandLine_ImportSearchInternal_iRT_standard_set_to__0___but_multiple_iRT_standards_were_found__iRT_standard_must_be_set_explicitly_,
                                IrtStandard.AUTO.Name);
                            return false;
                    }
                }

                ProcessedIrtAverages processed;
                try
                {
                    processed = ImportPeptideSearch.ProcessRetentionTimes(numCirt, irtProviders, import.IrtStandard.Peptides.ToArray(),
                        cirtPeptides, IrtRegressionType.DEFAULT, progressMonitor, out var newStandardPeptides);
                    if (newStandardPeptides != null)
                    {
                        import.IrtStandard = new IrtStandard(XmlNamedElement.NAME_INTERNAL, null, null, newStandardPeptides);
                    }
                }
                catch (Exception x)
                {
                    _out.WriteLine(TextUtil.LineSeparate(
                        Resources.BuildPeptideSearchLibraryControl_AddIrtLibraryTable_An_error_occurred_while_processing_retention_times_,
                        x.Message));
                    return false;
                }

                var processedDbIrtPeptides = processed.DbIrtPeptides.ToArray();
                if (processedDbIrtPeptides.Any())
                {
                    ImportPeptideSearch.CreateIrtDb(docLibSpec.FilePath, processed, import.IrtStandard.Peptides.ToArray(),
                        processed.CanRecalibrateStandards(import.IrtStandard.Peptides) && commandArgs.RecalibrateIrts, IrtRegressionType.DEFAULT, progressMonitor);
                }
                doc = ImportPeptideSearch.AddRetentionTimePredictor(doc, docLibSpec);
            }

            if (!import.VerifyRetentionTimes(import.GetFoundResultsFiles().Select(f => f.Path)))
            {
                _out.WriteLine(TextUtil.LineSeparate(
                    Resources.ImportPeptideSearchDlg_NextPage_The_document_specific_spectral_library_does_not_have_valid_retention_times_,
                    Resources.ImportPeptideSearchDlg_NextPage_Please_check_your_peptide_search_pipeline_or_contact_Skyline_support_to_ensure_retention_times_appear_in_your_spectral_libraries_));
                return false;
            }

            // Look for results files to import
            import.InitializeSpectrumSourceFiles(doc);
            import.UpdateSpectrumSourceFilesFromDirs(import.GetDirsToSearch(Path.GetDirectoryName(commandArgs.SkylineFile)), false, null);
            var missingResultsFiles = import.GetMissingResultsFiles().ToArray();
            if (missingResultsFiles.Any())
            {
                foreach (var file in missingResultsFiles)
                {
                    if (doc.Settings.HasResults && doc.Settings.MeasuredResults.FindMatchingMSDataFile(new MsDataFilePath(file)) != null)
                        continue;

                    _out.WriteLine(Resources.CommandLine_ImportSearch_Warning__Unable_to_locate_results_file___0__, Path.GetFileName(file));
                }
            }

            // Add all modifications, if requested
            if (commandArgs.AcceptAllModifications)
            {
                import.InitializeModifications(doc);
                var foundMods = import.GetMatchedMods().Count();
                var newModifications = new PeptideModifications(import.MatcherPepMods.StaticModifications,
                    new[] {new TypedModifications(IsotopeLabelType.heavy, import.MatcherHeavyMods)});
                var newSettings = import.AddModifications(doc, newModifications);
                if (!ReferenceEquals(doc.Settings, newSettings))
                {
                    if (foundMods != 1)
                        _out.WriteLine(Resources.CommandLine_ImportSearch_Adding__0__modifications_, foundMods);
                    else
                        _out.WriteLine(Resources.CommandLine_ImportSearch_Adding_1_modification_);
                    doc = doc.ChangeSettings(newSettings);
                    doc.Settings.UpdateDefaultModifications(false);
                }
            }

            // Import FASTA
            if (commandArgs.ImportingFasta)
            {

                _out.WriteLine(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____, Path.GetFileName(commandArgs.FastaPath));
                doc = ImportPeptideSearch.PrepareImportFasta(doc);
                List<PeptideGroupDocNode> peptideGroupsNew;
                try
                {
                    IdentityPath firstAdded, nextAdd;
                    doc = ImportPeptideSearch.ImportFasta(doc, commandArgs.FastaPath, import.IrtStandard, progressMonitor, null,
                        out firstAdded, out nextAdd, out peptideGroupsNew);
                }
                catch (Exception x)
                {
                    _out.WriteLine(Resources.CommandLine_Run_Error__Failed_importing_the_file__0____1_, commandArgs.FastaPath, x.Message);
                    SetDocument(doc);
                    return true;  // So that document will be saved with the new library
                }

                if (peptideGroupsNew.Count(pepGroup => pepGroup.PeptideCount == 0) > 0 && !commandArgs.KeepEmptyProteins)
                {
                    doc = ImportPeptideSearch.RemoveProteinsByPeptideCount(doc, 1);
                }

                doc = ImportPeptideSearch.AddStandardsToDocument(doc, import.IrtStandard);
            }

            // Import results
            SetDocument(doc);
            ImportFoundResultsFiles(commandArgs, import);
            return true;
        }
        

        private bool ImportDocuments(CommandArgs commandArgs)
        {
            // Add files to the end in the order they were given.
            foreach (var filePath in commandArgs.DocImportPaths)
            {
                _out.WriteLine(Resources.SkylineWindow_ImportFiles_Importing__0__, Path.GetFileName(PathEx.SafePath(filePath)));

                using (var reader = new StreamReader(filePath))
                {
                    IdentityPath firstAddedForFile, nextAdd;
                    _doc = _doc.ImportDocumentXml(reader,
                                                filePath,
                                                commandArgs.DocImportResultsMerge.Value,
                                                commandArgs.DocImportMergePeptides,
                                                FindSpectralLibrary,
                                                Settings.Default.StaticModList,
                                                Settings.Default.HeavyModList,
                                                null,   // Always add to the end
                                                out firstAddedForFile,
                                                out nextAdd,
                                                false);
                }
            }
            return true;
        }

        private string FindSpectralLibrary(string libraryName, string fileName)
        {
            // No ability to ask the user for the location of the library, so just warn
            _out.WriteLine(Resources.CommandLine_ConnectLibrarySpecs_Warning__Could_not_find_the_spectral_library__0_, libraryName);
            return null;
        }

        private bool AddDecoys(CommandArgs commandArgs)
        {
            if (!commandArgs.AddDecoys)
            {
                if (!commandArgs.DiscardDecoys || !_doc.MoleculeGroups.Contains(g => g.IsDecoy))
                    return true;

                ModifyDocument(RefinementSettings.RemoveDecoys);
                _out.WriteLine(Resources.CommandLine_AddDecoys_Decoys_discarded);
                return true;
            }
            if (!commandArgs.DiscardDecoys && _doc.PeptideGroups.Contains(g => g.IsDecoy))
            {
                _out.WriteLine(Resources.CommandLine_AddDecoys_Error__Attempting_to_add_decoys_to_document_with_decoys_);
                return false;
            }
            int numComparableGroups = RefinementSettings.SuggestDecoyCount(_doc);
            if (numComparableGroups == 0)
            {
                _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, Resources.GenerateDecoysError_No_peptide_precursor_models_for_decoys_were_found_);
                return false;
            }
            int numDecoys = commandArgs.AddDecoysCount ?? numComparableGroups;
            if (!Equals(commandArgs.AddDecoysType, DecoyGeneration.SHUFFLE_SEQUENCE) && numComparableGroups < numDecoys)
            {
                _out.WriteLine(Resources.CommandLine_AddDecoys_Error_The_number_of_peptides,
                    numDecoys, numComparableGroups, CommandArgs.ARG_DECOYS_ADD.ArgumentText, CommandArgs.ARG_VALUE_DECOYS_ADD_SHUFFLE);
                return false;
            }
            var refineAddDecoys = new RefinementSettings
            {
                DecoysMethod = commandArgs.AddDecoysType,
                NumberOfDecoys = numDecoys
            };
            int peptidesBefore = _doc.PeptideCount;
            int decoyPeptideCount = 0;
            if (commandArgs.DiscardDecoys)
                decoyPeptideCount = _doc.MoleculeGroups.Where(g => g.IsDecoy).Sum(g => g.MoleculeCount);

            ModifyDocument(d => refineAddDecoys.GenerateDecoys(d));

            if (decoyPeptideCount > 0)
                _out.WriteLine(Resources.CommandLine_AddDecoys_Decoys_discarded);
            _out.WriteLine(Resources.CommandLine_AddDecoys_Added__0__decoy_peptides_using___1___method,
                _doc.PeptideCount - (peptidesBefore - decoyPeptideCount), commandArgs.AddDecoysType);
            return true;
        }

        private void ImportFoundResultsFiles(CommandArgs commandArgs, ImportPeptideSearch import)
        {
            var listNamedPaths = new List<KeyValuePair<string, MsDataFileUri[]>>();
            foreach (var resultFile in import.GetFoundResultsFiles())
            {
                var filePath = new MsDataFilePath(resultFile.Path);
                if (!_doc.Settings.HasResults || _doc.Settings.MeasuredResults.FindMatchingMSDataFile(filePath) == null)
                {
                    listNamedPaths.Add(new KeyValuePair<string, MsDataFileUri[]>(resultFile.Name, new [] {filePath}));
                }
            }

            ImportDataFiles(listNamedPaths, null, null, null, null, false, commandArgs.ImportWarnOnFailure);
        }

        private bool ReintegratePeaks(CommandArgs commandArgs)
        {
            if (!_doc.Settings.HasResults)
            {
                _out.WriteLine(Resources.CommandLine_ReintegratePeaks_Error__You_must_first_import_results_into_the_document_before_reintegrating_);
                return false;
            }
            else
            {
                ModelAndFeatures modelAndFeatures;
                if (commandArgs.IsCreateScoringModel)
                {
                    modelAndFeatures = CreateScoringModel(commandArgs.ReintegrateModelName,
                        commandArgs.ReintegrateModelType,
                        commandArgs.ExcludeFeatures,
                        commandArgs.IsDecoyModel,
                        commandArgs.IsSecondBestModel,
                        commandArgs.IsLogTraining,
                        commandArgs.ReintegrateModelCutoffs,
                        commandArgs.ReintegrateModelIterationCount);

                    if (modelAndFeatures == null)
                        return false;
                }
                else
                {
                    PeakScoringModelSpec scoringModel;
                    if (!Settings.Default.PeakScoringModelList.TryGetValue(commandArgs.ReintegrateModelName, out scoringModel))
                    {
                        _out.WriteLine(Resources.CommandLine_ReintegratePeaks_Error__Unknown_peak_scoring_model___0__);
                        return false;
                    }
                    modelAndFeatures = new ModelAndFeatures(scoringModel, null);
                }

                if (!Reintegrate(modelAndFeatures, commandArgs.IsOverwritePeaks, commandArgs.IsLogTraining))
                    return false;
            }
            return true;
        }

        private class ModelAndFeatures
        {
            public ModelAndFeatures(PeakScoringModelSpec scoringModel, PeakTransitionGroupFeatureSet features)
            {
                ScoringModel = scoringModel;
                Features = features;
            }

            public PeakScoringModelSpec ScoringModel { get; private set; }
            public PeakTransitionGroupFeatureSet Features { get; private set; }

            public void ReleaseMemory()
            {
                ScoringModel = null;
                Features = null;
            }
        }

        private ModelAndFeatures CreateScoringModel(string modelName, CommandArgs.ScoringModelType modelType,
            IList<IPeakFeatureCalculator> excludeFeatures, bool decoys, bool secondBest, bool log,
            IList<double> modelCutoffs, int? modelIterationCount)
        {
            _out.WriteLine(Resources.CommandLine_CreateScoringModel_Creating_scoring_model__0_, modelName);

            try
            {
                // Create new scoring model using the default calculators.
                var scoringModel = CreateUntrainedScoringModel(modelName, modelType, excludeFeatures, decoys, secondBest);
                if (scoringModel == null)
                    return null;
                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(String.Empty));
                var targetDecoyGenerator = new TargetDecoyGenerator(scoringModel,
                    _doc.GetPeakFeatures(scoringModel.PeakFeatureCalculators, progressMonitor));

                // Get scores for target and decoy groups.
                List<IList<float[]>> targetTransitionGroups;
                List<IList<float[]>> decoyTransitionGroups;
                targetDecoyGenerator.GetTransitionGroups(out targetTransitionGroups, out decoyTransitionGroups);
                // If decoy box is checked and no decoys, throw an error
                if (decoys && decoyTransitionGroups.Count == 0)
                {
                    _out.WriteLine(Resources.CommandLine_CreateScoringModel_Error__There_are_no_decoy_peptides_in_the_document__Failed_to_create_scoring_model_);
                    return null;
                }
                // Use decoys for training only if decoy box is checked
                if (!decoys)
                    decoyTransitionGroups = new List<IList<float[]>>();

                // Set intial weights based on previous model (with NaN's reset to 0)
                var initialWeights = new double[scoringModel.PeakFeatureCalculators.Count];
                // But then set to NaN the weights that have unknown values for this dataset
                for (int i = 0; i < initialWeights.Length; ++i)
                {
                    if (!targetDecoyGenerator.EligibleScores[i])
                        initialWeights[i] = double.NaN;
                }
                var initialParams = new LinearModelParams(initialWeights);

                // Train the model.
                string documentPath = log ? DocContainer.DocumentFilePath : null;
                scoringModel = (PeakScoringModelSpec) scoringModel.Train(targetTransitionGroups,
                    decoyTransitionGroups, targetDecoyGenerator, initialParams, modelCutoffs, modelIterationCount, secondBest, true, progressMonitor, documentPath);

                Settings.Default.PeakScoringModelList.SetValue(scoringModel);

                if (scoringModel.IsTrained)
                {
                    var weights = targetDecoyGenerator.GetPeakCalculatorWeights(scoringModel, progressMonitor);
                    for (int i = 0; i < weights.Length; i++)
                    {
                        var w = weights[i];
                        if (w.IsEnabled)
                            _out.WriteLine(@"{0}: {1:F04} ({2:P1})", w.Name, w.Weight, w.PercentContribution);
                    }
                }

                return new ModelAndFeatures(scoringModel, targetDecoyGenerator.PeakGroupFeatures);
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_CreateScoringModel_Error__Failed_to_create_scoring_model_);
                _out.WriteLine(x);
                return null;
            }
        }

        private PeakScoringModelSpec CreateUntrainedScoringModel(string modelName, CommandArgs.ScoringModelType modelType,
            IList<IPeakFeatureCalculator> excludeFeatures, bool decoys, bool secondBest)
        {
            if (modelType == CommandArgs.ScoringModelType.Skyline)
            {
                return new LegacyScoringModel(modelName, LegacyScoringModel.DEFAULT_PARAMS, decoys, secondBest);
            }

            var calcs = modelType == CommandArgs.ScoringModelType.SkylineML
                ? LegacyScoringModel.AnalyteFeatureCalculators  // Assume unlabeled for now
                : MProphetPeakScoringModel.GetDefaultCalculators(_doc);
            if (excludeFeatures.Count > 0)
            {
                if (excludeFeatures.Count == 1)
                    _out.WriteLine(Resources.CommandLine_CreateScoringModel_Excluding_feature_score___0__,
                        excludeFeatures.First().Name);
                else
                {
                    _out.WriteLine(Resources.CommandLine_CreateScoringModel_Excluding_feature_scores_);
                    foreach (var featureCalculator in excludeFeatures)
                        _out.WriteLine(@"    " + featureCalculator.Name);
                }

                // Excluding any requested by the caller
                calcs = calcs.Where(c => excludeFeatures.All(c2 => c.GetType() != c2.GetType())).ToArray();
            }

            return new MProphetPeakScoringModel(modelName, (LinearModelParams) null, calcs, decoys, secondBest);
        }

        private bool Reintegrate(ModelAndFeatures modelAndFeatures, bool isOverwritePeaks, bool logTraining)
        {
            try
            {
                var resultsHandler = new MProphetResultsHandler(_doc, modelAndFeatures.ScoringModel, modelAndFeatures.Features)
                {
                    OverrideManual = isOverwritePeaks,
                    FreeImmutableMemory = true
                };
                
                // If logging training, give the modeling code a place to write
                if (logTraining)
                    resultsHandler.DocumentPath = DocContainer.DocumentFilePath;

                modelAndFeatures.ReleaseMemory();   // Avoid holding memory through peak adjustment

                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));

                resultsHandler.ScoreFeatures(progressMonitor, true, _out);
                if (resultsHandler.IsMissingScores())
                {
                    _out.WriteLine(Resources.CommandLine_Reintegrate_Error__The_current_peak_scoring_model_is_incompatible_with_one_or_more_peptides_in_the_document__Please_train_a_new_model_);
                    return false;
                }
                ModifyDocument(d => resultsHandler.ChangePeaks(progressMonitor));

                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_Reintegrate_Error__Failed_to_reintegrate_peaks_successfully_);
                _out.WriteLine(x);
                return false;
            }
        }

        public void ImportFasta(string path, bool keepEmptyProteins)
        {
            _out.WriteLine(Resources.CommandLine_ImportFasta_Importing_FASTA_file__0____, Path.GetFileName(path));
            using (var readerFasta = new StreamReader(PathEx.SafePath(path)))
            {
                var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
                IdentityPath selectPath;
                long lines = Helpers.CountLinesInFile(path);
                int emptiesIgnored;
                ModifyDocument(d => d.ImportFasta(readerFasta, progressMonitor, lines, false, null, out selectPath, out emptiesIgnored));
            }
            
            // Remove all empty proteins unless otherwise specified
            if (!keepEmptyProteins)
                ModifyDocument(d => new RefinementSettings { MinPeptidesPerProtein = 1 }.Refine(d));
 
        }

        private bool ImportTransitionList(CommandArgs commandArgs)
        {
            _out.WriteLine(Resources.CommandLine_ImportTransitionList_Importing_transiton_list__0____, Path.GetFileName(commandArgs.TransitionListPath));

            IdentityPath selectPath;
            List<MeasuredRetentionTime> irtPeptides;
            List<SpectrumMzInfo> librarySpectra;
            List<TransitionImportErrorInfo> errorList;
            List<PeptideGroupDocNode> peptideGroups;
            var retentionTimeRegression = _doc.Settings.PeptideSettings.Prediction.RetentionTime;
            RCalcIrt calcIrt = retentionTimeRegression != null ? (retentionTimeRegression.Calculator as RCalcIrt) : null;

            var progressMonitor = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
            var inputs = new MassListInputs(commandArgs.TransitionListPath);
            var importer = _doc.PreImportMassList(inputs, progressMonitor, false, SrmDocument.DOCUMENT_TYPE.none, false, Document.DocumentType);
            var docNew = _doc.ImportMassList(inputs, importer, progressMonitor, null,
                out selectPath, out irtPeptides, out librarySpectra, out errorList, out peptideGroups);

            // If nothing was imported (e.g. operation was canceled or zero error-free transitions) and also no errors, just return
            if (ReferenceEquals(docNew, _doc) && !errorList.Any())
                return true;
            // Show the errors or as warnings, if error transitions are ignore
            if (errorList.Any())
            {
                string messageFormat = !commandArgs.IsIgnoreTransitionErrors
                    ? Resources.CommandLine_ImportTransitionList_Error___line__0___column__1____2_
                    : Resources.CommandLine_ImportTransitionList_Warning___line__0___column__1____2_;
                foreach (var errorMessage in errorList)
                {
                    _out.WriteLine(messageFormat, errorMessage.LineNum, errorMessage.Column, errorMessage.ErrorMessage);
                }
                if (!commandArgs.IsIgnoreTransitionErrors)
                    return false;
            }
            if (!commandArgs.IsTransitionListAssayLibrary)
            {
                ModifyDocument(d => docNew);
                return true;
            }
            if (irtPeptides.Count == 0 || librarySpectra.Count == 0)
            {
                if (irtPeptides.Any())
                    _out.WriteLine(Resources.CommandLine_ImportTransitionList_Error__Imported_assay_library__0__lacks_ion_abundance_values_);
                else if (librarySpectra.Any())
                    _out.WriteLine(Resources.CommandLine_ImportTransitionList_Error__Imported_assay_library__0__lacks_iRT_values_);
                else
                    _out.WriteLine(Resources.CommandLine_ImportTransitionList_Error__Imported_assay_library__0__lacks_iRT_and_ion_abundance_values_);
                return false;
            }

            string destinationPath = commandArgs.SaveFile ?? commandArgs.SkylineFile;
            string documentLibrary = BiblioSpecLiteSpec.GetLibraryFileName(destinationPath);
            // ReSharper disable once AssignNullToNotNullAttribute
            string outputLibraryPath = Path.Combine(Path.GetDirectoryName(documentLibrary),
                Path.GetFileNameWithoutExtension(documentLibrary) + BiblioSpecLiteSpec.ASSAY_NAME +
                BiblioSpecLiteSpec.EXT);
            bool libraryExists = File.Exists(outputLibraryPath);
            string libraryName = Path.GetFileNameWithoutExtension(destinationPath) + BiblioSpecLiteSpec.ASSAY_NAME;
            int indexOldLibrary = docNew.Settings.PeptideSettings.Libraries.LibrarySpecs.IndexOf(
                    spec => spec != null && spec.FilePath == outputLibraryPath);
            bool libraryLinkedToDoc = indexOldLibrary != -1;
            if (libraryExists && !libraryLinkedToDoc)
            {
                _out.WriteLine(Resources.CommandLine_ImportTransitionList_Error__There_is_an_existing_library_with_the_same_name__0__as_the_document_library_to_be_created_,
                        libraryName);
                return false;
            }

            var dbIrtPeptides = irtPeptides.Select(rt => new DbIrtPeptide(rt.PeptideSequence, rt.RetentionTime, false, TimeSource.scan)).ToList();
            var dbIrtPeptidesFilter = ImportAssayLibraryHelper.GetUnscoredIrtPeptides(dbIrtPeptides, calcIrt);
            // If there are no iRT peptides with different values than the database, don't import any iRT's
            bool checkPeptides = false;
            if (dbIrtPeptidesFilter.Any())
            {
                if (calcIrt == null)
                {
                    string irtDatabasePath = commandArgs.IrtDatabasePath;
                    if (string.IsNullOrEmpty(irtDatabasePath))
                        irtDatabasePath = Path.ChangeExtension(destinationPath, IrtDb.EXT);
                    if (!string.IsNullOrEmpty(commandArgs.IrtStandardsPath))
                    {
                        _out.WriteLine(Resources.CommandLine_ImportTransitionList_Importing_iRT_transition_list__0_, commandArgs.IrtStandardsPath);
                        var irtInputs = new MassListInputs(commandArgs.IrtStandardsPath);
                        try
                        {
                            List<SpectrumMzInfo> irtLibrarySpectra;
                            docNew = docNew.ImportMassList(irtInputs, null, out selectPath, out irtPeptides, out irtLibrarySpectra, out errorList);
                            if (errorList.Any())
                            {
                                throw new InvalidDataException(errorList[0].ErrorMessage);
                            }
                            librarySpectra.AddRange(irtLibrarySpectra);
                            dbIrtPeptidesFilter.AddRange(irtPeptides.Select(rt => new DbIrtPeptide(rt.PeptideSequence, rt.RetentionTime, true, TimeSource.scan)));
                        }
                        catch (Exception x)
                        {
                            _out.WriteLine(Resources.CommandLine_Run_Error__Failed_importing_the_file__0____1_, commandArgs.IrtStandardsPath, x.Message);
                            return false;
                        }
                        if (!CreateIrtDatabase(irtDatabasePath, commandArgs))
                            return false;
                    }
                    else if (!string.IsNullOrEmpty(commandArgs.IrtGroupName))
                    {
                        var nodeGroupIrt = docNew.PeptideGroups.FirstOrDefault(nodeGroup => nodeGroup.Name == commandArgs.IrtGroupName);
                        if (nodeGroupIrt == null)
                        {
                            _out.WriteLine(Resources.CommandLine_ImportTransitionList_Error__The_name__0__specified_with__1__was_not_found_in_the_imported_assay_library_,
                                commandArgs.IrtGroupName, CommandArgs.ARG_IRT_STANDARDS_GROUP_NAME.ArgumentText);
                            return false;
                        }
                        var irtPeptideSequences = new HashSet<Target>(nodeGroupIrt.Peptides.Select(pep => pep.ModifiedTarget));
                        dbIrtPeptidesFilter.ForEach(pep => pep.Standard = irtPeptideSequences.Contains(pep.ModifiedTarget));
                        if (!CreateIrtDatabase(irtDatabasePath, commandArgs))
                            return false;
                    }
                    else if (!File.Exists(irtDatabasePath))
                    {
                        _out.WriteLine(Resources.CommandLine_ImportTransitionList_Error__To_create_the_iRT_database___0___for_this_assay_library__you_must_specify_the_iRT_standards_using_either_of_the_arguments__1__or__2_,
                            irtDatabasePath, CommandArgs.ARG_IRT_STANDARDS_GROUP_NAME.ArgumentText, CommandArgs.ARG_IRT_STANDARDS_FILE.ArgumentText);
                        return false;
                    }
                    else
                    {
                        checkPeptides = true;
                    }
                    string irtCalcName = commandArgs.IrtCalcName ?? Path.GetFileNameWithoutExtension(destinationPath);
                    calcIrt = new RCalcIrt(irtCalcName, irtDatabasePath);

                    retentionTimeRegression = new RetentionTimeRegression(calcIrt.Name, calcIrt, null, null, 10, new List<MeasuredRetentionTime>());
                    docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptidePrediction(prediction =>
                        prediction.ChangeRetentionTime(retentionTimeRegression)));
                }
                string dbPath = calcIrt.DatabasePath;
                IrtDb db = IrtDb.GetIrtDb(dbPath, null);
                if (checkPeptides)
                {
                    var standards = docNew.Molecules.Where(m => db.IsStandard(m.ModifiedTarget)).ToArray();
                    if (standards.Length != db.StandardPeptideCount)
                    {
                        _out.WriteLine(Resources.CommandLine_ImportTransitionList_Warning__The_document_is_missing_iRT_standards);
                        foreach (var rawTextId in db.StandardPeptides.Where(s => !standards.Contains(nodePep => s == nodePep.ModifiedTarget)))
                        {
                            _out.WriteLine(@"    " + rawTextId);
                        }
                    }
                }
                var oldPeptides = db.GetPeptides().ToList();
                IList<DbIrtPeptide.Conflict> conflicts;
                dbIrtPeptidesFilter = DbIrtPeptide.MakeUnique(dbIrtPeptidesFilter);
                DbIrtPeptide.FindNonConflicts(oldPeptides, dbIrtPeptidesFilter, null, out conflicts);
                // Warn about peptides that are present in the import and already in the database
                foreach (var conflict in conflicts)
                {
                    _out.WriteLine(Resources.CommandLine_ImportTransitionList_Warning__The_iRT_calculator_already_contains__0__with_the_value__1___Ignoring__2_,
                        conflict.ExistingPeptide.ModifiedTarget, conflict.ExistingPeptide.Irt, conflict.NewPeptide.Irt);
                }

                _out.WriteLine(Resources.CommandLine_ImportTransitionList_Importing__0__iRT_values_into_the_iRT_calculator__1_, dbIrtPeptidesFilter.Count, calcIrt.Name);
                docNew = docNew.AddIrtPeptides(dbIrtPeptidesFilter, false, progressMonitor);
                if (docNew == null)
                    return false;
            }

            librarySpectra = SpectrumMzInfo.RemoveDuplicateSpectra(librarySpectra);

            if (libraryLinkedToDoc)
            {
                string oldName = docNew.Settings.PeptideSettings.Libraries.LibrarySpecs[indexOldLibrary].Name;
                using (var libraryOld = new LibraryReference(docNew.Settings.PeptideSettings.Libraries.GetLibrary(oldName)))
                {
                    var additionalSpectra = SpectrumMzInfo.GetInfoFromLibrary(libraryOld.Reference);
                    additionalSpectra = SpectrumMzInfo.RemoveDuplicateSpectra(additionalSpectra);

                    librarySpectra = SpectrumMzInfo.MergeWithOverwrite(librarySpectra, additionalSpectra);
                }
            }

            if (librarySpectra.Any())
            {
                // Delete the existing library; either it's not tied to the document or we've already extracted the spectra
                _out.WriteLine(Resources.CommandLine_ImportTransitionList_Adding__0__spectra_to_the_library__1_, librarySpectra.Count, libraryName);
                if (libraryExists)
                {
                    FileEx.SafeDelete(outputLibraryPath);
                    FileEx.SafeDelete(Path.ChangeExtension(outputLibraryPath, BiblioSpecLiteSpec.EXT_REDUNDANT));
                }
                using (var blibDb = BlibDb.CreateBlibDb(outputLibraryPath))
                {
                    var docLibrarySpec = new BiblioSpecLiteSpec(libraryName, outputLibraryPath);
                    using (var docLibrary = new LibraryReference(blibDb.CreateLibraryFromSpectra(
                        docLibrarySpec, librarySpectra, libraryName, progressMonitor)))
                    {
                        var newSettings = docNew.Settings.ChangePeptideLibraries(
                            libs => libs.ChangeLibrary(docLibrary.Reference, docLibrarySpec, indexOldLibrary));
                        docNew = docNew.ChangeSettings(newSettings, new SrmSettingsChangeMonitor(progressMonitor,
                            Resources.SkylineWindow_ImportMassList_Finishing_up_import));
                    }
                }
            }

            ModifyDocument(d => docNew);
            return true;
        }

        private class LibraryReference : IDisposable
        {
            public Library Reference { get; private set; }

            public LibraryReference(Library reference)
            {
                Reference = reference;
            }

            public void Dispose()
            {
                if (Reference != null)
                {
                    foreach (var stream in Reference.ReadStreams)
                        stream.CloseStream();
                }
            }
        }

        public bool CreateIrtDatabase(string irtDatabasePath, CommandArgs commandArgs)
        {
            if (File.Exists(irtDatabasePath))
            {
                _out.WriteLine(Resources.CommandLine_CreateIrtDatabase_Error__Importing_an_assay_library_to_a_document_without_an_iRT_calculator_cannot_create__0___because_it_exists_,
                              irtDatabasePath);
                if (string.IsNullOrEmpty(commandArgs.IrtDatabasePath))
                {
                    _out.WriteLine(Resources.CommandLine_CreateIrtDatabase_Use_the__0__argument_to_specify_a_file_to_create_, CommandArgs.ARG_IRT_DATABASE_PATH.ArgumentText);
                }
                return false;
            }
            try
            {
                ImportAssayLibraryHelper.CreateIrtDatabase(irtDatabasePath);
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, x);
                return false;
            }
            return true;
        }

        public bool SetLibrary(string name, string path, bool append = true)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _out.WriteLine(Resources.CommandLine_SetLibrary_Error__Cannot_set_library_name_without_path_);
                return false;
            }
            else if (!File.Exists(path))
            {
                _out.WriteLine(Resources.CommandLine_SetLibrary_Error__The_file__0__does_not_exist_, path);
                return false;
            }
            else if (path.EndsWith(BiblioSpecLiteSpec.EXT_REDUNDANT))
            {
                _out.WriteLine(Resources.CommandLine_SetLibrary_Error__The_file__0__appears_to_be_a_redundant_library_);
                return false;
            }

            if (string.IsNullOrWhiteSpace(name))
                name = Path.GetFileNameWithoutExtension(path);

            var librarySpec = LibrarySpec.CreateFromPath(name, path);

            if (librarySpec == null)
            {
                _out.WriteLine(Resources.CommandLine_SetLibrary_Error__The_file__0__is_not_a_supported_spectral_library_file_format_, path);
                return false;
            }

            // Check for conflicting names
            foreach (var docLibrarySpec in _doc.Settings.PeptideSettings.Libraries.LibrarySpecs)
            {
                if (docLibrarySpec.Name == librarySpec.Name || docLibrarySpec.FilePath == librarySpec.FilePath)
                {
                    _out.WriteLine(Resources.CommandLine_SetLibrary_Error__The_library_you_are_trying_to_add_conflicts_with_a_library_already_in_the_file_);
                    return false;
                }
            }

            var librarySpecs = append ?
                new List<LibrarySpec>(_doc.Settings.PeptideSettings.Libraries.LibrarySpecs) { librarySpec } :
                new List<LibrarySpec>{ librarySpec };

            SrmSettings newSettings = _doc.Settings.ChangePeptideLibraries(l => l.ChangeLibrarySpecs(librarySpecs));
            ModifyDocument(d => d.ChangeSettings(newSettings));

            return true;
        }

        public bool SaveFile(string saveFile)
        {
            _out.WriteLine(Resources.CommandLine_SaveFile_Saving_file___);
            try
            {
                SaveDocument(_doc, saveFile, _out);
            }
            catch (Exception e)
            {
                _out.WriteLine(Resources.CommandLine_SaveFile_Error__The_file_could_not_be_saved_to__0____Check_that_the_directory_exists_and_is_not_read_only_, saveFile);
                _out.WriteLine(e);
                return false;
            }
            _out.WriteLine(Resources.CommandLine_SaveFile_File__0__saved_, Path.GetFileName(saveFile));
            return true;
        }

        public bool ExportReport(string reportName, string reportFile, char reportColSeparator, bool reportInvariant)
        {

            if (string.IsNullOrEmpty(reportFile))
            {
                _out.WriteLine(Resources.CommandLine_ExportReport_);
                return false;
            }

            return ExportLiveReport(reportName, reportFile, reportColSeparator, reportInvariant);
        }

        private bool ExportLiveReport(string reportName, string reportFile, char reportColSeparator, bool reportInvariant)
        {
            var viewContext = DocumentGridViewContext.CreateDocumentGridViewContext(_doc, reportInvariant
                ? DataSchemaLocalizer.INVARIANT
                : SkylineDataSchema.GetLocalizedSchemaLocalizer());
            // Make sure invariant report format uses a true comma if a tab separator was not specified.
            if (reportInvariant && reportColSeparator != TextUtil.SEPARATOR_TSV)
                reportColSeparator = TextUtil.SEPARATOR_CSV;
            var viewInfo = viewContext.GetViewInfo(PersistedViews.MainGroup.Id.ViewName(reportName));
            if (null == viewInfo)
            {
                _out.WriteLine(Resources.CommandLine_ExportLiveReport_Error__The_report__0__does_not_exist__If_it_has_spaces_in_its_name__use__double_quotes__around_the_entire_list_of_command_parameters_, reportName);
                return false;
            }
            _out.WriteLine(Resources.CommandLine_ExportLiveReport_Exporting_report__0____, reportName);

            try
            {
                using (var saver = new FileSaver(reportFile))
                {
                    if (!saver.CanSave())
                    {
                        _out.WriteLine(Resources.CommandLine_ExportLiveReport_Error__The_report__0__could_not_be_saved_to__1__, reportName, reportFile);
                        _out.WriteLine(Resources.CommandLine_ExportLiveReport_Check_to_make_sure_it_is_not_read_only_);
                        return false;
                    }

                    IProgressStatus status = new ProgressStatus(string.Empty);
                    IProgressMonitor broker = new CommandProgressMonitor(_out, status);

                    using (var writer = new StreamWriter(saver.SafeName))
                    {
                        viewContext.Export(CancellationToken.None, broker, ref status, viewInfo, writer,
                            viewContext.GetDsvWriter(reportColSeparator));
                    }

                    broker.UpdateProgress(status.Complete());
                    saver.Commit();
                    _out.WriteLine(Resources.CommandLine_ExportLiveReport_Report__0__exported_successfully_to__1__, reportName, reportFile);
                }
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_ExportLiveReport_Error__Failure_attempting_to_save__0__report_to__1__, reportName, reportFile);
                _out.WriteLine(x.Message);
                return false;
            }
            return true;
        }

        public bool ExportChromatograms(string chromatogramsFile, bool precursors, bool products, bool basePeaks, bool tics)
        {
            _out.WriteLine(Resources.CommandLine_ExportChromatograms_Exporting_chromatograms_file__0____, chromatogramsFile);

            var chromExtractors = new List<ChromExtractor>();
            if (tics)
                chromExtractors.Add(ChromExtractor.summed);
            if (basePeaks)
                chromExtractors.Add(ChromExtractor.base_peak);

            var chromSources = new List<ChromSource>();
            if (precursors)
                chromSources.Add(ChromSource.ms1);
            if (products)
                chromSources.Add(ChromSource.fragment);

            if (chromExtractors.Count == 0 && chromSources.Count == 0)
            {
                _out.WriteLine(Resources.CommandLine_ExportChromatograms_Error__At_least_one_chromatogram_type_must_be_selected);
                return false;
            }

            var filesToExport = Document.Settings.HasResults
                ? Document.Settings.MeasuredResults.MSDataFilePaths.Select(f => f.GetFileName()).ToList()
                : new List<string>();
            if (filesToExport.Count == 0)
            {
                _out.WriteLine(Resources.CommandLine_ExportChromatograms_Error__The_document_must_have_imported_results);
                return false;
            }

            try
            {
                var chromExporter = new ChromatogramExporter(Document);
                using (var saver = new FileSaver(chromatogramsFile))
                using (var writer = new StreamWriter(saver.SafeName))
                {
                    var status = new ProgressStatus(string.Empty);
                    IProgressMonitor broker = new CommandProgressMonitor(_out, status);
                    chromExporter.Export(writer, broker, filesToExport, LocalizationHelper.CurrentCulture, chromExtractors, chromSources);
                    writer.Close();
                    broker.UpdateProgress(status.Complete());
                    saver.Commit();
                    _out.WriteLine(Resources.CommandLine_ExportChromatograms_Chromatograms_file__0__exported_successfully_, chromatogramsFile);
                }
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_ExportChromatograms_Error__Failure_attempting_to_save_chromatograms_file__0_, chromatogramsFile);
                _out.WriteLine(x.Message);
                return false;
            }

            return true;
        }

        public enum ResolveZipToolConflicts
        {
            terminate,
            overwrite,
            in_parallel
        }

        public bool ImportToolsFromZip(string path, ResolveZipToolConflicts? resolveConflicts, bool? overwriteAnnotations, ProgramPathContainer ppc, string programPath, bool arePackagesHandled)
        {
            if (string.IsNullOrEmpty(path))
            {
                _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Error__to_import_tools_from_a_zip_you_must_specify_a_path___tool_add_zip_must_be_followed_by_an_existing_path_);
                return false;
            }
            if (!File.Exists(path))
            {
                _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Error__the_file_specified_with_the___tool_add_zip_command_does_not_exist__Please_verify_the_file_location_and_try_again_);
                return false;
            }
            if (Path.GetExtension(path) != ToolDescription.EXT_INSTALL)
            {
                _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Error__the_file_specified_with_the___tool_add_zip_command_is_not_a__zip_file__Please_specify_a_valid__zip_file_);
                return false;
            }
            string filename = Path.GetFileName(path);
            _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Installing_tools_from__0_, filename);
            ToolInstaller.UnzipToolReturnAccumulator result = null;
            try
            {
                result = ToolInstaller.UnpackZipTool(path, new AddZipToolHelper(resolveConflicts, overwriteAnnotations, _out, filename, ppc,
                                                                               programPath, arePackagesHandled));
            }
            catch (ToolExecutionException x)
            {
                    _out.WriteLine(x.Message);
                }
            if (result != null)
            {
                foreach (var message in result.MessagesThrown)
                {
                    _out.WriteLine(message);
                }
                foreach (var tool in result.ValidToolsFound)
                {
                    _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Installed_tool__0_, tool.Title);
                }

                SaveSettings();
                return true;
            }
            else
            {
                _out.WriteLine(Resources.CommandLine_ImportToolsFromZip_Error__Canceled_installing_tools_from__0__, filename);
                return false;
            }
        }

        private bool SaveSettings()
        {
            try
            {
                Settings.Default.Save();
                return true;
            }
            catch (Exception x)
            {
                _out.WriteLine(Resources.CommandLine_SaveSettings_Error__Failed_saving_to_the_user_configuration_file_);
                _out.WriteLine(x.Message);
            }
            return false;
        }

        // A function for adding tools to the Tools Menu.
        public bool ImportTool (string title, string command, string arguments, string initialDirectory, string reportTitle, bool outputToImmediateWindow, bool? resolveToolConflictsBySkipping)
        {
            if (title == null | command == null)
            {
                _out.WriteLine(Resources.CommandLine_ImportTool_Error__to_import_a_tool_it_must_have_a_name_and_a_command___Use___tool_add_to_specify_a_name_and_use___tool_command_to_specify_a_command___The_tool_was_not_imported___);
                return false;
            }
            // Check if the command is of a supported type and not a URL
            else if (!ConfigureToolsDlg.CheckExtension(command) && !ToolDescription.IsWebPageCommand(command))
            {
                string supportedTypes = String.Join(@"; ", ConfigureToolsDlg.EXTENSIONS);
                supportedTypes = supportedTypes.Replace(@".", @"*.");
                _out.WriteLine(Resources.CommandLine_ImportTool_Error__the_provided_command_for_the_tool__0__is_not_of_a_supported_type___Supported_Types_are___1_, title, supportedTypes);
                _out.WriteLine(Resources.CommandLine_ImportTool_The_tool_was_not_imported___);
                return false;
            }
            if (arguments != null && arguments.Contains(ToolMacros.INPUT_REPORT_TEMP_PATH))
            {
                if (string.IsNullOrEmpty(reportTitle))
                {
                    _out.WriteLine(Resources.CommandLine_ImportTool_Error__If__0__is_and_argument_the_tool_must_have_a_Report_Title__Use_the___tool_report_parameter_to_specify_a_report_, ToolMacros.INPUT_REPORT_TEMP_PATH);
                    _out.WriteLine(Resources.CommandLine_ImportTool_The_tool_was_not_imported___);
                    return false;
                }

                if (!ReportSharing.GetExistingReports().ContainsKey(PersistedViews.ExternalToolsGroup.Id.ViewName(reportTitle))) 
                {
                    _out.WriteLine(Resources.CommandLine_ImportTool_Error__Please_import_the_report_format_for__0____Use_the___report_add_parameter_to_add_the_missing_custom_report_, reportTitle);
                    _out.WriteLine(Resources.CommandLine_ImportTool_The_tool_was_not_imported___);
                    return false;                    
                }
            }            

            // Check for a name conflict. 
            ToolDescription toolToRemove = null;
            foreach (var tool  in Settings.Default.ToolList)
            {                
                if (tool.Title == title)
                {
                    // Conflict. 
                    if (resolveToolConflictsBySkipping == null)
                    {
                        // Complain. No resolution specified.
                        _out.WriteLine(Resources.CommandLine_ImportTool_, tool.Title);
                        return false; // Dont add.
                    }
                    // Skip conflicts
                    else if (resolveToolConflictsBySkipping == true)
                    {
                        _out.WriteLine(Resources.CommandLine_ImportTool_Warning__skipping_tool__0__due_to_a_name_conflict_, tool.Title);
//                        _out.WriteLine("         tool {0} was not modified.", tool.Title);
                        return true;
                    }
                    // Overwrite conflicts
                    else
                    {
                        _out.WriteLine(Resources.CommandLine_ImportTool_Warning__the_tool__0__was_overwritten, tool.Title);
//                      _out.WriteLine("         tool {0} was modified.", tool.Title);
                        if (toolToRemove == null) // If there are multiple tools with the same name this makes sure the first one with a naming conflict is overwritten.
                            toolToRemove = tool;
                    }
                }
            }
            // Remove the tool to be overwritten.
            if (toolToRemove !=null)
                Settings.Default.ToolList.Remove(toolToRemove);          
            // If no tool was overwritten then its a new tool. Show this message. 
            if (toolToRemove == null)
            {
                _out.WriteLine(Resources.CommandLine_ImportTool__0__was_added_to_the_Tools_Menu_, title);
            }
            // Conflicts have been dealt with now add the tool.                       
            // Adding the tool. ToolArguments and ToolInitialDirectory are optional. 
            // If arguments or initialDirectory is null set it to be an empty string.
            arguments = arguments ?? string.Empty; 
            initialDirectory = initialDirectory ?? string.Empty; 
            Settings.Default.ToolList.Add(new ToolDescription(title, command, arguments, initialDirectory, outputToImmediateWindow, reportTitle));
            SaveSettings();

            return true;
        }

        // A function for running each line of a text file like a SkylineRunner command
        public int RunBatchCommands(string path)
        {
            if (!File.Exists(path))
            {
                _out.WriteLine(Resources.CommandLine_RunBatchCommands_Error___0__does_not_exist____batch_commands_failed_, path);
                return Program.EXIT_CODE_RAN_WITH_ERRORS;
            }
            else
            {
                try
                {
                    using (StreamReader sr = File.OpenText(path))
                    {
                        string input;
                        // Run each line like its own command.
                        while ((input = sr.ReadLine()) != null)
                        {
                            // Parse the line and run it.
                            string[] args = ParseArgs(input);
                            int exitCode = Run(args);
                            if (exitCode != Program.EXIT_CODE_SUCCESS)
                                return exitCode;
                        }
                    }
                }
                catch (Exception)
                {
                    _out.WriteLine(Resources.CommandLine_RunBatchCommands_Error__failed_to_open_file__0____batch_commands_command_failed_, path);
                }
            }            
            return Program.EXIT_CODE_SUCCESS;
        }
        
        /// <summary>
        ///  A method for parsing command line inputs to accept quotes arround strings and double quotes within those strings.
        ///  See CommandLineTest.cs ConsoleParserTest() for specific examples of its behavior. 
        /// </summary>
        /// <param name="inputs"> string on inputs </param>
        /// <returns> string[] of parsed commands </returns>
        public static string[] ParseArgs(string inputs)
        {            
            List<string> output = new List<string>();
            bool foundSingle = false;
            string current = null; 
            // Loop char by char through inputs building an ouput 
            for (int i = 0; i < inputs.Length; i++)
            {
                char c = inputs[i];
                if (c == '"')
                {
                    // If you have not yet encountered a quote, its an open quote
                    if (!foundSingle)
                        foundSingle = true;
                    // If you have already encountered a quote, it could be a close quote or escaped quote
                    else
                    {
                        // In this case its an escaped quote
                        if ((i < inputs.Length - 1) && inputs[i + 1] == '"')
                        {
                            current += c;
                            i++;
                        }
                        // Its a close quote
                        else
                            foundSingle = false;
                    }                   
                }
                // If not within a quote and the current string being built isn't blank, a space is a place to break.
                else if (c == ' ' && !foundSingle && (!String.IsNullOrEmpty(current)))
                {
                    output.Add(current);
                    current = null;
                }   
                else if (c != ' ' || (c == ' ' && foundSingle))
                    current += c;

                // Catch the corner case at the end of the string, make sure the last chunk is added to output array.
                if (i == inputs.Length - 1 && (!String.IsNullOrEmpty(current)))
                {
                    output.Add(current);
                }
            }                
            return output.ToArray();
        }

        /// <summary>
        /// A method for joining an array of individual arguments to be passed to the command line to generate
        /// the string that will ultimately be passed. If a argument has white space, it is surrounded by quotes.
        /// If an empty (size 0) array is given, it returns string.Empty 
        /// </summary>
        /// <param name="arguments">The arguments to join</param>
        /// <returns>The appropriately formatted command line argument string</returns>
        public static string JoinArgs(string[] arguments)
        {
            if (!arguments.Any())
            {
                return string.Empty;
            }
            
            StringBuilder commandLineArguments = new StringBuilder();
            foreach (string argument in arguments)
            {
                // ReSharper disable LocalizableElement
                if (argument == null)
                {
                     commandLineArguments.Append(" \"\"");
                }
                else if (argument.Contains(" ") || argument.Contains("\t") || argument.Equals(string.Empty))
                {
                    commandLineArguments.Append(" \"" + argument + "\"");

                }
                else
                {
                    commandLineArguments.Append(TextUtil.SEPARATOR_SPACE + argument);
                }
                // ReSharper restore LocalizableElement
            }
            commandLineArguments.Remove(0, 1);
            return commandLineArguments.ToString();
        }

        public bool ImportSkyr(string path, bool? resolveSkyrConflictsBySkipping)
        {          
            if (!File.Exists(path))
            {
                _out.WriteLine(Resources.CommandLine_ImportSkyr_Error___0__does_not_exist____report_add_command_failed_, path);
                return false;
            }
            else
            {           
                ImportSkyrHelper helper = new ImportSkyrHelper(_out, resolveSkyrConflictsBySkipping);
                bool imported;
                try
                {
                    imported = ReportSharing.ImportSkyrFile(path, helper.ResolveImportConflicts);
                }
                catch (Exception e)
                {
                    _out.WriteLine(Resources.CommandLine_ImportSkyr_, path);
                    _out.WriteLine(e);
                    return false;
                }
                if (imported)
                {
                    if (!SaveSettings())
                        return false;
                    _out.WriteLine(Resources.CommandLine_ImportSkyr_Success__Imported_Reports_from__0_, Path.GetFileNameWithoutExtension(path));
                }
                // else // TODO: Return an error if the report was not imported?
                // {
                //     if (!_out.IsErrorReported)
                //     {
                //         _out.WriteLine("Reports could not be imported from {0}", path);
                //     }
                //     return false;
                // }
            }
            return true;
        }

        private class ImportSkyrHelper
        {
            private bool? resolveSkyrConflictsBySkipping { get; set; }
            private readonly TextWriter _outWriter;

            public ImportSkyrHelper(TextWriter outWriter, bool? resolveSkyrConflictsBySkipping)
            {
                _outWriter = outWriter;
                this.resolveSkyrConflictsBySkipping = resolveSkyrConflictsBySkipping;
            }

            internal IList<string> ResolveImportConflicts(IList<string> existing)
            {
                string messageFormat = existing.Count == 1
                                           ? Resources.ImportSkyrHelper_ResolveImportConflicts_The_name___0___already_exists_
                                           : Resources.ImportSkyrHelper_ResolveImportConflicts_;
                // ReSharper disable LocalizableElement
                _outWriter.WriteLine(messageFormat, string.Join("\n", existing.ToArray()));
                // ReSharper restore LocalizableElement
                if (resolveSkyrConflictsBySkipping == null)
                {
                    _outWriter.WriteLine(Resources.ImportSkyrHelper_ResolveImportConflicts_Use_command);
                    return null;
                }
                if (resolveSkyrConflictsBySkipping == true)
                {
                    _outWriter.WriteLine(Resources.ImportSkyrHelper_ResolveImportConflicts_Resolving_conflicts_by_skipping_);
                    // The objects are skipped below for being in the list called existing
                }
                if (resolveSkyrConflictsBySkipping == false)
                {
                    _outWriter.WriteLine(Resources.ImportSkyrHelper_ResolveImportConflicts_Resolving_conflicts_by_overwriting_);
                    existing.Clear();
                    // All conflicts are overwritten because existing is empty. 
                }
                return existing;
            }
        }

        // This function needs so many variables, we might as well just pass the whole CommandArgs object
        private bool ExportInstrumentFile(ExportFileType type, CommandArgs args)
        {
            if (string.IsNullOrEmpty(args.ExportPath))
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_);
                return false;
            }

            if (Equals(type, ExportFileType.Method))
            {
                if (string.IsNullOrEmpty(args.TemplateFile))
                {
                    _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__A_template_file_is_required_to_export_a_method_);
                    return false;
                }
                if (Equals(args.MethodInstrumentType, ExportInstrumentType.AGILENT6400)
                        ? !Directory.Exists(args.TemplateFile)
                        : !File.Exists(args.TemplateFile))
                {
                    _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_template_file__0__does_not_exist_, args.TemplateFile);
                    return false;
                }
                if (Equals(args.MethodInstrumentType, ExportInstrumentType.AGILENT6400) &&
                    !AgilentMethodExporter.IsAgilentMethodPath(args.TemplateFile))
                {
                    _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_folder__0__does_not_appear_to_contain_an_Agilent_QQQ_method_template___The_folder_is_expected_to_have_a__m_extension__and_contain_the_file_qqqacqmethod_xsd_, args.TemplateFile);
                    return false;
                }
            }

            if (!args.ExportStrategySet)
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Warning__No_export_strategy_specified__from__single____protein__or__buckets____Defaulting_to__single__);
                args.ExportStrategy = ExportStrategy.Single;
            }

            if (args.AddEnergyRamp && !Equals(args.TransListInstrumentType, ExportInstrumentType.THERMO))
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Warning__The_add_energy_ramp_parameter_is_only_applicable_for_Thermo_transition_lists__This_parameter_will_be_ignored_);
            }

            string instrument;
            switch (type)
            {
                case ExportFileType.IsolationList:
                    instrument = args.IsolationListInstrumentType;
                    break;
                case ExportFileType.List:
                    instrument = args.TransListInstrumentType;
                    break;
                case ExportFileType.Method:
                    instrument = args.MethodInstrumentType;
                    break;
                default:
                    instrument = string.Empty;
                    break;
            }
            if (!CheckInstrument(instrument, _doc))
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Warning__The_vendor__0__does_not_match_the_vendor_in_either_the_CE_or_DP_prediction_setting___Continuing_exporting_a_transition_list_anyway___, instrument);
            }


            int maxInstrumentTrans = _doc.Settings.TransitionSettings.Instrument.MaxTransitions ??
                                     TransitionInstrument.MAX_TRANSITION_MAX;

            if ((args.MaxTransitionsPerInjection < AbstractMassListExporter.MAX_TRANS_PER_INJ_MIN ||
                 args.MaxTransitionsPerInjection > maxInstrumentTrans) &&
                (Equals(args.ExportStrategy, ExportStrategy.Buckets) ||
                 Equals(args.ExportStrategy, ExportStrategy.Protein)))
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Warning__Max_transitions_per_injection_must_be_set_to_some_value_between__0__and__1__for_export_strategies__protein__and__buckets__and_for_scheduled_methods__You_specified__3___Defaulting_to__2__, AbstractMassListExporter.MAX_TRANS_PER_INJ_MIN, maxInstrumentTrans,AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT, args.MaxTransitionsPerInjection);

                args.MaxTransitionsPerInjection = AbstractMassListExporter.MAX_TRANS_PER_INJ_DEFAULT;
            }

            /*
             * Consider: for transition lists, AB Sciex and Agilent require the 
             * dwell time parameter, and Waters requires the run length parameter.
             * These are guaranteed to be set and within-bounds at this point, but
             * not necessarily by the user because there is a default.
             * 
             * Should we warn the user that they didn't set these parameters?
             * Should we warn the user if they set parameters that will not be used
             * with the given instrument?
             * 
             * This would require a pretty big matrix of conditionals, and there is
             * documentation after all...
             */

            if (Equals(type, ExportFileType.Method))
            {
                string extension = Path.GetExtension(args.TemplateFile);
                if (!Equals(ExportInstrumentType.MethodExtension(args.MethodInstrumentType), extension))
                {
                    _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_template_extension__0__does_not_match_the_expected_extension_for_the_instrument__1___No_method_will_be_exported_, extension,args.MethodInstrumentType);
                    return false;
                }
            }

            var prediction = _doc.Settings.TransitionSettings.Prediction;
            double optimizeStepSize = 0;
            int optimizeStepCount = 0;

            if (Equals(args.ExportOptimizeType, ExportOptimize.CE))
            {
                var regression = prediction.CollisionEnergy;
                optimizeStepSize = regression.StepSize;
                optimizeStepCount = regression.StepCount;
            }
            else if (Equals(args.ExportOptimizeType, ExportOptimize.DP))
            {
                var regression = prediction.DeclusteringPotential;
                optimizeStepSize = regression.StepSize;
                optimizeStepCount = regression.StepCount;
            }

            //Now is a good time to make this conversion
            _exportProperties = args.ExportCommandProperties;
            _exportProperties.OptimizeStepSize = optimizeStepSize;
            _exportProperties.OptimizeStepCount = optimizeStepCount;

            _exportProperties.FullScans = _doc.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            _exportProperties.Ms1Scan = _doc.Settings.TransitionSettings.FullScan.IsEnabledMs &&
                            _doc.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            _exportProperties.InclusionList = _doc.Settings.TransitionSettings.FullScan.IsEnabledMs &&
                                              !_doc.Settings.TransitionSettings.FullScan.IsEnabledMsMs;

            _exportProperties.MsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _doc.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            _exportProperties.MsMsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _doc.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);


            _exportProperties.MsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _doc.Settings.TransitionSettings.FullScan.PrecursorMassAnalyzer);
            _exportProperties.MsMsAnalyzer =
                TransitionFullScan.MassAnalyzerToString(
                    _doc.Settings.TransitionSettings.FullScan.ProductMassAnalyzer);

            if (!Equals(args.ExportMethodType, ExportMethodType.Standard))
            {
                if (Equals(args.ExportMethodType, ExportMethodType.Triggered))
                {
                    bool canTrigger = true;
                    if (!ExportInstrumentType.CanTriggerInstrumentType(instrument))
                    {
                        canTrigger = false;
                        if (Equals(args.MethodInstrumentType, ExportInstrumentType.THERMO_TSQ))
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the__0__instrument_lacks_support_for_direct_method_export_for_triggered_acquisition_, instrument);
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_You_must_export_a__0__transition_list_and_manually_import_it_into_a_method_file_using_vendor_software_, ExportInstrumentType.THERMO);
                        }
                        else
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_instrument_type__0__does_not_support_triggered_acquisition_, instrument);
                        }
                    }
                    else if (!_doc.Settings.HasResults && !_doc.Settings.HasLibraries)
                    {
                        canTrigger = false;
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__triggered_acquistion_requires_a_spectral_library_or_imported_results_in_order_to_rank_transitions_);
                    }
                    else if (!ExportInstrumentType.CanTrigger(instrument, _doc, _exportProperties.SchedulingReplicateNum))
                    {
                        canTrigger = false;
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_current_document_contains_peptides_without_enough_information_to_rank_transitions_for_triggered_acquisition_);
                    }
                    if (!canTrigger)
                    {
                        _out.WriteLine(!Equals(type, ExportFileType.Method)
                                               ? Resources.CommandLine_ExportInstrumentFile_No_list_will_be_exported_
                                               : Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_);
                        return false;
                    }
                    _exportProperties.PrimaryTransitionCount = args.PrimaryTransitionCount;
                }

                if (!ExportInstrumentType.CanSchedule(instrument, _doc))
                {
                    var predictionPep = _doc.Settings.PeptideSettings.Prediction;
                    if (!ExportInstrumentType.CanScheduleInstrumentType(instrument, _doc))
                    {
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_specified_instrument__0__is_not_compatible_with_scheduled_methods_,
                                       instrument);
                    }
                    else if (predictionPep.RetentionTime == null)
                    {
                        if (predictionPep.UseMeasuredRTs)
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__to_export_a_scheduled_method__you_must_first_choose_a_retention_time_predictor_in_Peptide_Settings___Prediction__or_import_results_for_all_peptides_in_the_document_);
                        }
                        else
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__to_export_a_scheduled_method__you_must_first_choose_a_retention_time_predictor_in_Peptide_Settings___Prediction_);
                        }
                    }
                    else if (!predictionPep.RetentionTime.Calculator.IsUsable)
                    {
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_retention_time_prediction_calculator_is_unable_to_score___Check_the_calculator_settings_);
                    }
                    else if (!predictionPep.RetentionTime.IsUsable)
                    {
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_retention_time_predictor_is_unable_to_auto_calculate_a_regression___Check_to_make_sure_the_document_contains_times_for_all_of_the_required_standard_peptides_);
                    }
                    else
                    {
                        _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__To_export_a_scheduled_method__you_must_first_import_results_for_all_peptides_in_the_document_);
                    }
                    _out.WriteLine(!Equals(type, ExportFileType.Method)
                                           ? Resources.CommandLine_ExportInstrumentFile_No_list_will_be_exported_
                                           : Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_);
                    return false;
                }

                if (Equals(args.ExportSchedulingAlgorithm, ExportSchedulingAlgorithm.Average))
                {
                    _exportProperties.SchedulingReplicateNum = null;
                }
                else
                {
                    if (args.SchedulingReplicate.Equals(@"LAST"))
                    {
                        _exportProperties.SchedulingReplicateNum = _doc.Settings.MeasuredResults.Chromatograms.Count - 1;
                    }
                    else
                    {
                        //check whether the given replicate exists
                        if (!_doc.Settings.MeasuredResults.ContainsChromatogram(args.SchedulingReplicate))
                        {
                            _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__the_specified_replicate__0__does_not_exist_in_the_document_,
                                           args.SchedulingReplicate);
                            _out.WriteLine(!Equals(type, ExportFileType.Method)
                                                   ? Resources.CommandLine_ExportInstrumentFile_No_list_will_be_exported_
                                                   : Resources.CommandLine_ExportInstrumentFile_No_method_will_be_exported_);
                            return false;
                        }

                        _exportProperties.SchedulingReplicateNum =
                            _doc.Settings.MeasuredResults.Chromatograms.IndexOf(
                                rep => rep.Name.Equals(args.SchedulingReplicate));
                    }
                }
            }
            _exportProperties.PolarityFilter = args.ExportPolarityFilter;
            try
            {
                _exportProperties.ExportFile(instrument, type, args.ExportPath, _doc, args.TemplateFile);
            }
            catch (IOException x)
            {
                _out.WriteLine(Resources.CommandLine_ExportInstrumentFile_Error__The_file__0__could_not_be_saved___Check_that_the_specified_file_directory_exists_and_is_writeable_, args.ExportPath);
                _out.WriteLine(x.Message);
                return false;
            }

            var exportPath = Path.GetFileName(args.ExportPath);
            if (_exportProperties.PolarityFilter == ExportPolarity.separate && type != ExportFileType.Method && _doc.IsMixedPolarity())
            {
                // Will create a pair of (or pair of sets of) files, let the final confirmation message reflect that
                var ext = Path.GetExtension(exportPath);
                if (ext == null)
                {
                    exportPath += @"*";
                }
                else
                {
                    exportPath = exportPath.Replace(ext, @"*" + ext);
                }
            }

            _out.WriteLine(!Equals(type, ExportFileType.Method)
                               ? Resources.CommandLine_ExportInstrumentFile_List__0__exported_successfully_
                               : Resources.CommandLine_ExportInstrumentFile_Method__0__exported_successfully_,
                           exportPath);
            return true;
        }

        public void SaveDocument(SrmDocument doc, string outFile, TextWriter outText)
        {
            // Make sure the containing directory is created
            string dirPath = Path.GetDirectoryName(outFile);
            if (dirPath != null)
                Directory.CreateDirectory(dirPath);

            var progressMonitor = new CommandProgressMonitor(outText, new ProgressStatus(string.Empty));
            using (var saver = new FileSaver(outFile))
            {
                saver.CheckException();
                doc.SerializeToFile(saver.SafeName, outFile, SkylineVersion.CURRENT, progressMonitor);
                // If the user has chosen "Save As", and the document has a
                // document specific spectral library, copy this library to 
                // the new name.
                if (_skylineFile != null && !Equals(_skylineFile, outFile))
                    SaveDocumentLibraryAs(outFile);

                saver.Commit();

                var settings = doc.Settings;
                if (settings.HasResults)
                {
                    if (settings.MeasuredResults.IsLoaded)
                    {
                        FileStreamManager fsm = FileStreamManager.Default;
                        settings.MeasuredResults.OptimizeCache(outFile, fsm);

                        //don't worry about updating the document with the results of optimization
                        //as is done in SkylineFiles
                    }
                }
                else
                {
                    string cachePath = ChromatogramCache.FinalPathForName(outFile, null);
                    FileEx.SafeDelete(cachePath, true);
                }
            }
        }

        private void SaveDocumentLibraryAs(string outFile)
        {
            string oldDocLibFile = BiblioSpecLiteSpec.GetLibraryFileName(_skylineFile);
            string oldRedundantDocLibFile = BiblioSpecLiteSpec.GetRedundantName(oldDocLibFile);
            // If the document has a document-specific library, and the files for it
            // exist on disk
            var document = Document;
            if (document.Settings.PeptideSettings.Libraries.HasDocumentLibrary
                && File.Exists(oldDocLibFile))
            {
                string newDocLibFile = BiblioSpecLiteSpec.GetLibraryFileName(outFile);
                using (var saverLib = new FileSaver(newDocLibFile))
                {
                    FileSaver saverRedundant = null;
                    if (File.Exists(oldRedundantDocLibFile))
                    {
                        string newRedundantDocLibFile = BiblioSpecLiteSpec.GetRedundantName(outFile);
                        saverRedundant = new FileSaver(newRedundantDocLibFile);
                    }
                    using (saverRedundant)
                    {
                        saverLib.CopyFile(oldDocLibFile);
                        if (saverRedundant != null)
                        {
                            saverRedundant.CopyFile(oldRedundantDocLibFile);
                        }
                        saverLib.Commit();
                        if (saverRedundant != null)
                        {
                            saverRedundant.Commit();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// This function will add the given replicate, from dataFile, to the given document. If the replicate
        /// does not exist, it will be added. If it does exist, it will be appended to.
        /// </summary>
        public void ImportResults(MemoryDocumentContainer docContainer,
            string replicate, MsDataFileUri dataFile, OptimizableRegression optimize)
        {
            SrmDocument docOriginal, docAdded;
            do
            {
                docOriginal= docContainer.Document;
                var listChromatograms = new List<ChromatogramSet>();

                if (docOriginal.Settings.HasResults)
                    listChromatograms.AddRange(docOriginal.Settings.MeasuredResults.Chromatograms);

                int indexChrom = listChromatograms.IndexOf(chrom => chrom.Name.Equals(replicate));
                if (indexChrom != -1)
                {
                    var chromatogram = listChromatograms[indexChrom];
                    var paths = chromatogram.MSDataFilePaths;
                    var listFilePaths = paths.ToList();
                    listFilePaths.Add(dataFile);
                    listChromatograms[indexChrom] = chromatogram.ChangeMSDataFilePaths(listFilePaths);
                }
                else
                {
                    listChromatograms.Add(new ChromatogramSet(replicate, new[] { dataFile.Normalize() }, Annotations.EMPTY, optimize));
                }

                var results = docOriginal.Settings.HasResults
                                  ? docOriginal.Settings.MeasuredResults.ChangeChromatograms(listChromatograms)
                                  : new MeasuredResults(listChromatograms, docOriginal.Settings.IsResultsJoiningDisabled);

                docAdded = docOriginal.ChangeMeasuredResults(results);
            }
            while (!docContainer.SetDocument(docAdded, docOriginal));
        }

        /// <summary>
        /// This method returns true/false whether or not there is any discrepancy
        /// between the specified instrument and the instrument in the document settings.
        /// </summary>
        /// <param name="instrument">specified instrument</param>
        /// <param name="doc">document to check against</param>
        /// <returns></returns>
        public static bool CheckInstrument(string instrument, SrmDocument doc)
        {
            // Thermo LTQ method building ignores CE and DP regression values
            if (!Equals(instrument, ExportInstrumentType.THERMO_LTQ))
            {
                // Check to make sure CE and DP match chosen instrument, and offer to use
                // the correct version for the instrument, if not.
                var predict = doc.Settings.TransitionSettings.Prediction;
                var ce = predict.CollisionEnergy;
                string ceName = (ce != null ? ce.Name : null);
                string ceNameDefault = instrument;
                if (ceNameDefault.IndexOf(' ') != -1)
                    ceNameDefault = ceNameDefault.Substring(0, ceNameDefault.IndexOf(' '));
                bool ceInSynch = ceName != null && ceName.StartsWith(ceNameDefault);

                var dp = predict.DeclusteringPotential;
                string dpName = (dp != null ? dp.Name : null);
                string dpNameDefault = instrument;
                if (dpNameDefault.IndexOf(' ') != -1)
                    dpNameDefault = dpNameDefault.Substring(0, dpNameDefault.IndexOf(' '));
                bool dpInSynch = true;
                if (instrument == ExportInstrumentType.ABI)
                    dpInSynch = dpName != null && dpName.StartsWith(dpNameDefault);
                //else
                    //dpNameDefault = null; // Ignored for all other types

                return (ceInSynch && dpInSynch);
            }

            return true;
        }

        private static bool ShareDocument(SrmDocument document, string documentPath, string fileDest, ShareType shareType, CommandStatusWriter statusWriter)
        {
            var waitBroker = new CommandProgressMonitor(statusWriter,
                new ProgressStatus(Resources.SkylineWindow_ShareDocument_Compressing_Files));
            var sharing = new SrmDocumentSharing(document, documentPath, fileDest, shareType);
            try
            {
                sharing.Share(waitBroker);
                return true;
            }
            catch (Exception x)
            {
                statusWriter.WriteLine(Resources.CommandLine_GeneralException_Error___0_,
                    Resources.SkylineWindow_ShareDocument_Failed_attempting_to_create_sharing_file__0__, fileDest);
                statusWriter.WriteLine(x.Message);
            }
            return false;
        }

        public void Dispose()
        {
            _out.Close();
        }

        private class PanoramaPublishHelper
        {
            private readonly CommandStatusWriter _statusWriter;

            public PanoramaPublishHelper(CommandStatusWriter statusWriter)
            {
                _statusWriter = statusWriter;
            }

            public bool PublishToPanorama(Server panoramaServer, SrmDocument document, string documentPath, string panoramaFolder, ShareType selectedShareType)
            {
                try
                {
                    WebPanoramaPublishClient publishClient = new WebPanoramaPublishClient();
                    // If the Panorama server does not support the skyd version of the document, change the Skyline version to the 
                    // max version supported by the server.
                    selectedShareType = publishClient.DecideShareTypeVersion(new FolderInformation(panoramaServer, true),
                        document, selectedShareType);
                }
                catch (PanoramaServerException panoramaServerException)
                {
                    _statusWriter.WriteLine(Resources.CommandLine_GeneralException_Error___0_, panoramaServerException.Message);
                    return false;
                }
                var zipFilePath = FileEx.GetTimeStampedFileName(documentPath);
                var published = false;
                if (ShareDocument(document, documentPath, zipFilePath, selectedShareType, _statusWriter))
                {
                    published = PublishDocToPanorama(panoramaServer, zipFilePath, panoramaFolder);
                }
                // Delete the zip file after it has been published to Panorama.
                FileEx.SafeDelete(zipFilePath, true);

                return published;
            }

            private bool PublishDocToPanorama(Server panoramaServer, string zipFilePath, string panoramaFolder)
            {
                var waitBroker = new CommandProgressMonitor(_statusWriter,
                    new ProgressStatus(Resources.PanoramaPublishHelper_PublishDocToPanorama_Uploading_document_to_Panorama));
                IPanoramaPublishClient publishClient = new WebPanoramaPublishClient();
                try
                {
                    publishClient.SendZipFile(panoramaServer, panoramaFolder, zipFilePath, waitBroker);
                    return true;
                }
                catch (Exception x)
                {
                    var panoramaEx = x.InnerException as PanoramaImportErrorException ?? x as PanoramaImportErrorException;
                    if (panoramaEx == null)
                    {
                        _statusWriter.WriteLine(Resources.PanoramaPublishHelper_PublishDocToPanorama_, x.Message);
                    }
                    else
                    {
                        if (panoramaEx.JobCancelled)
                        {
                            _statusWriter.WriteLine(Resources.PanoramaPublishHelper_PublishDocToPanorama_Error__Document_import_was_cancelled_on_the_Panorama_server__0__, panoramaEx.ServerUrl);
                            _statusWriter.WriteLine(Resources.PanoramaPublishHelper_PublishDocToPanorama_Job_details_can_be_found_at__0__, panoramaEx.JobUrl);
                        }
                        else
                        {
                            _statusWriter.WriteLine(
                                Resources.PanoramaPublishHelper_PublishDocToPanorama_Error__An_import_error_occurred_on_the_Panorama_server__0__,
                                panoramaEx.ServerUrl);
                            _statusWriter.WriteLine(
                                Resources.PanoramaPublishHelper_PublishDocToPanorama_Error_details_can_be_found_at__0_,
                                panoramaEx.JobUrl);
                        }
                    }
                }
                return false;
            }
        }
    }

    public class CommandStatusWriter : TextWriter
    {
        private TextWriter _writer;

        public CommandStatusWriter(TextWriter writer)
            : base(writer.FormatProvider)
        {
            _writer = Synchronized(writer); // Make this thread safe for more predictable console output
        }

        public bool IsTimeStamped { get; set; }

        public bool IsMemStamped { get; set; }

        public bool IsErrorReported { get; private set; }

        public override Encoding Encoding
        {
            get { return _writer.Encoding; }
        }

        protected override void Dispose(bool disposing)
        {
            if (_writer != null)
            {
                _writer.Dispose();
                _writer = null;
            }
        }

        public override void Flush()
        {
            _writer.Flush();
        }

        public override void Write(char value)
        {
            _writer.Write(value);
        }

        public override void WriteLine()
        {
            WriteLine(string.Empty);
        }

        public override void WriteLine(string value)
        {
            var message = new StringBuilder();
            if (IsTimeStamped)
                // ReSharper disable LocalizableElement
                message.Append(DateTime.Now.ToString("[yyyy/MM/dd HH:mm:ss]\t"));
                // ReSharper restore LocalizableElement
            if (IsMemStamped)
            {
                lock (_writer)
                {
                    // This can take long enough that we need to introduce a lock to keep
                    // output ordered as much as possible
                    message.Append(MemStamp(GC.GetTotalMemory(false)));
                    message.Append(MemStamp(Process.GetCurrentProcess().PrivateMemorySize64));
                }
            }
            message.Append(value);
            _writer.WriteLine(message);
            Flush();

            if (IsErrorMessage(value))
            {
                IsErrorReported = true;
            }
        }

        public const string ERROR_MESSAGE_HINT = @"Error:";

        private bool IsErrorMessage(string message)
        {
            if (message != null && !IsErrorReported)
            {
                return message.StartsWith(ERROR_MESSAGE_HINT, StringComparison.InvariantCulture) ||  // In Skyline-daily any message might not be localized
                       message.StartsWith(Resources.CommandStatusWriter_WriteLine_Error_,
                           StringComparison.CurrentCulture);
            }

            return false;
        }

        private string MemStamp(long memUsed)
        {
            const double mb = 1024 * 1024;
            // ReSharper disable LocalizableElement
            return string.Format("{0}\t", Math.Round(memUsed/mb));
            // ReSharper restore LocalizableElement
        }
    }

    public class ExportCommandProperties : ExportProperties
    {
        private readonly TextWriter _out;

        public ExportCommandProperties(TextWriter output)
        {
            _out = output;
        }

        public override void PerformLongExport(Action<IProgressMonitor> performExport)
        {
            var waitBroker = new CommandProgressMonitor(_out, new ProgressStatus(string.Empty));
            performExport(waitBroker);
        }
    }

    internal class AddZipToolHelper : IUnpackZipToolSupport
    {
        private string zipFileName { get; set; }
        private CommandStatusWriter _out { get; set; }
        private CommandLine.ResolveZipToolConflicts? resolveToolsAndReports { get; set; }
        private bool? overwriteAnnotations { get; set; }
        private ProgramPathContainer programPathContainer { get; set; }
        private string programPath { get; set; }
        private bool packagesHandled { get; set; }

        public AddZipToolHelper(CommandLine.ResolveZipToolConflicts? howToResolve, bool? howToResolveAnnotations,
                                CommandStatusWriter output,
                                string fileName, ProgramPathContainer ppc, string inputProgramPath,
                                bool arePackagesHandled)
        {
            resolveToolsAndReports = howToResolve;
            overwriteAnnotations = howToResolveAnnotations;
            _out = output;
            zipFileName = fileName;
            programPathContainer = ppc;
            programPath = inputProgramPath;
            packagesHandled = arePackagesHandled;
        }

        public bool? ShouldOverwrite(string toolCollectionName, string toolCollectionVersion, List<ReportOrViewSpec> reports,
                                     string foundVersion,
                                     string newCollectionName)
        {
            if (resolveToolsAndReports == CommandLine.ResolveZipToolConflicts.in_parallel)
                return false;
            if (resolveToolsAndReports == CommandLine.ResolveZipToolConflicts.overwrite)
            {
                string singularToolMessage = Resources.AddZipToolHelper_ShouldOverwrite_Overwriting_tool___0_;
                string singularReportMessage = Resources.AddZipToolHelper_ShouldOverwrite_Overwriting_report___0_;
                string plualReportMessage = Resources.AddZipToolHelper_ShouldOverwrite_Overwriting_reports___0_;
                if (reports.Count == 1)
                {
                    _out.WriteLine(singularReportMessage, reports[0].GetKey());
                }
                else if (reports.Count > 1)
                {
                    List<string> reportTitles = reports.Select(sp => sp.GetKey()).ToList();
                    string reportTitlesJoined = string.Join(@", ", reportTitles);
                    _out.WriteLine(plualReportMessage, reportTitlesJoined);
                }
                if (toolCollectionName != null)
                {
                    _out.WriteLine(singularToolMessage, toolCollectionName);
                }           
                return true;
            }
                
            else //Conflicts and no way to handle them. Display Message.
            {
                string firstpart = string.Empty;
                string secondpart = string.Empty;
                if (reports.Count == 0)
                {
                    if (toolCollectionName != null)
                    {
                        secondpart = Resources.AddZipToolHelper_ShouldOverwrite_Error__There_is_a_conflicting_tool;   
                    }
                }
                else
                {
                    if (reports.Count == 1)
                    {
                        firstpart = Resources.AddZipToolHelper_ShouldOverwrite_Error__There_is_a_conflicting_report;
                    }
                    if (reports.Count > 1)
                    {
                        firstpart = string.Format(Resources.AddZipToolHelper_ShouldOverwrite_Error__There_are__0__conflicting_reports, reports.Count);
                    }
                    if (toolCollectionName != null)
                    {                     
                        secondpart = Resources.AddZipToolHelper_ShouldOverwrite__and_a_conflicting_tool;
                    }
                }
                string message = string.Format(string.Concat(firstpart, secondpart, Resources.AddZipToolHelper_ShouldOverwrite__in_the_file__0_), zipFileName); 

                _out.WriteLine(message);
                _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwrite_Please_specify__overwrite__or__parallel__with_the___tool_zip_conflict_resolution_command_);
                
                string singularToolMessage = Resources.AddZipToolHelper_ShouldOverwrite_Conflicting_tool___0_;
                string singularReportMessage = Resources.AddZipToolHelper_ShouldOverwrite_Conflicting_report___0_;
                string plualReportMessage = Resources.AddZipToolHelper_ShouldOverwrite_Conflicting_reports___0_;
                if (reports.Count == 1)
                {
                    _out.WriteLine(singularReportMessage, reports[0].GetKey());
                }
                else if (reports.Count > 1)
                {
                    List<string> reportTitles = reports.Select(sp => sp.GetKey()).ToList();
                    string reportTitlesJoined = string.Join(@", ", reportTitles);
                    _out.WriteLine(plualReportMessage, reportTitlesJoined);
                }
                if (toolCollectionName != null)
                { 
                    _out.WriteLine(singularToolMessage, toolCollectionName);
                }
                return null;
            }
        }

        public string InstallProgram(ProgramPathContainer missingProgramPathContainer, ICollection<ToolPackage> packages, string pathToInstallScript)
        {
            if (packages.Count > 0 && !packagesHandled)
            {
                _out.WriteLine(Resources.AddZipToolHelper_InstallProgram_Error__Package_installation_not_handled_in_SkylineRunner___If_you_have_already_handled_package_installation_use_the___tool_ignore_required_packages_flag);
                return null;
            }
            string path;
            return Settings.Default.ToolFilePaths.TryGetValue(missingProgramPathContainer, out path) ? path : FindProgramPath(missingProgramPathContainer);
        }

        public bool? ShouldOverwriteAnnotations(List<AnnotationDef> annotations)
        {
            if (overwriteAnnotations == null)
            {
                _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_annotations_with_conflicting_names__Please_use_the___tool_zip_overwrite_annotations_command_);
            }
            if (overwriteAnnotations == true)
            {
                _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_conflicting_annotations__Overwriting_);
                foreach (var annotationDef in annotations)
                {
                    _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_Warning__the_annotation__0__is_being_overwritten, annotationDef.GetKey());
                }
            }
            if (overwriteAnnotations == false)
            {
                _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_There_are_conflicting_annotations__Keeping_existing_);
                foreach (var annotationDef in annotations)
                {
                    _out.WriteLine(Resources.AddZipToolHelper_ShouldOverwriteAnnotations_Warning__the_annotation__0__may_not_be_what_your_tool_requires_, annotationDef.GetKey());
                }
            }
            return overwriteAnnotations;
        }

        public string FindProgramPath(ProgramPathContainer missingProgramPathContainer)
        {
            if ((Equals(programPathContainer, missingProgramPathContainer)) && programPath != null)
            {
                //add to settings list
                Settings.Default.ToolFilePaths.Add(programPathContainer,programPath);
                return programPath;
            }
            _out.WriteLine(Resources.AddZipToolHelper_FindProgramPath_A_tool_requires_Program__0__Version__1__and_it_is_not_specified_with_the___tool_program_macro_and___tool_program_path_commands__Tool_Installation_Canceled_, missingProgramPathContainer.ProgramName, missingProgramPathContainer.ProgramVersion);
            return null;
        }
    }

    internal class CommandProgressMonitor : IProgressMonitor
    {
        private IProgressStatus _currentProgress;
        private readonly bool _warnOnImportFailure;
        private readonly DateTime _waitStart;
        private DateTime _lastOutput;
        private string _lastMessage;
        private string _lastWarning;

        private readonly TextWriter _out;
        private Thread _waitingThread;
        private volatile bool _waiting;

        public CommandProgressMonitor(TextWriter outWriter, IProgressStatus status, bool warnOnImportFailure = false)
        {
            _out = outWriter;
            _waitStart = _lastOutput = DateTime.UtcNow; // Said to be 117x faster than Now and this is for a delta
            _warnOnImportFailure = warnOnImportFailure;

            UpdateProgress(status);
        }

        bool IProgressMonitor.IsCanceled
        {
            get { return false; }
        }

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            return UpdateProgressInternal(status);
        }

        public bool HasUI { get { return false; } }

        public Exception ErrorException { get { return _currentProgress != null ? _currentProgress.ErrorException : null; } }

        private UpdateProgressResponse UpdateProgressInternal(IProgressStatus status)
        {
            if (status.PercentComplete != -1)
            {
                // Stop the waiting thread if it is running.
                _waiting = false;
                if(_waitingThread != null && _waitingThread.IsAlive)
                {
                    _waitingThread.Join();
                }  
            }

            if (IsLogStatusDeferred(status))
                return UpdateProgressResponse.normal;

            bool writeMessage = !string.IsNullOrEmpty(status.Message) && status.Message != _lastMessage;

            if (status.IsError)
            {
                var multiStatus = status as MultiProgressStatus;
                if (multiStatus != null)
                {
                    WriteMultiStatusErrors(multiStatus);
                }
                else if (!IsLibraryMissingSpectra(status))
                {
                    _out.WriteLine(Resources.CommandLine_GeneralException_Error___0_, status.ErrorException.Message);
                }
                writeMessage = false;
            }
            else if (status.PercentComplete == -1)
            {
                _out.Write(Resources.CommandWaitBroker_UpdateProgress_Waiting___);
                _waiting = true;
                // Start a thread that will indicate "waiting" progress to the console every few seconds.
                _waitingThread = new Thread(Wait){IsBackground = true};
                _waitingThread.Start();
            }
            else if (_currentProgress != null && !status.ProgressEqual(_currentProgress))
            {
                var multiStatus = status as MultiProgressStatus;
                if (multiStatus != null)
                {
                    // Display completion percentages for multiple tasks
                    var sb = new StringBuilder();
                    for (int i = 0; i < multiStatus.ProgressList.Count; i++)
                    {
                        var progressStatus = multiStatus.ProgressList[i];
                        if (Program.ImportProgressPipe != null)
                            sb.Append(string.Format(@"%%{0}", progressStatus.PercentComplete)); // double %% used for easy parsing by parent process
                        else if (progressStatus.State == ProgressState.running)
                            sb.Append(string.Format(@"[{0:##0}] {1:#0}%  ", i+1, progressStatus.PercentComplete));
                    }
                    _out.WriteLine(sb.ToString());
                }
                else
                {
                    // If writing a message at the same time as updating percent complete,
                    // put them both on the same line
                    if (writeMessage)
                        _out.WriteLine(@"{0}% - {1}", status.PercentComplete, status.Message);
                    else
                        _out.WriteLine(@"{0}%", status.PercentComplete);
                }
            }
            else if (writeMessage)
            {
                _out.WriteLine(status.Message);
            }

            if (!string.IsNullOrEmpty(status.WarningMessage) && !Equals(_lastWarning, status.WarningMessage))
            {
                _out.WriteLine(status.WarningMessage);
                _lastWarning = status.WarningMessage;
            }

            if (writeMessage)
                _lastMessage = status.Message;
            _currentProgress = status;

            return UpdateProgressResponse.normal;
        }

        private bool IsLogStatusDeferred(IProgressStatus status)
        {
            var currentTime = DateTime.UtcNow;
            if (IsLogStatusDeferredAtTime(currentTime, status))
            {
                return true;
            }
            // Check again inside a lock before falling through, which will cause output
            lock (_out)
            {
                if (IsLogStatusDeferredAtTime(currentTime, status))
                    return true;
                _lastOutput = currentTime;
            }
            return false;
        }

        private bool IsLibraryMissingSpectra(IProgressStatus status)
        {
            if (!BiblioSpecLiteBuilder.IsLibraryMissingExternalSpectraError(status.ErrorException,
                out IList<string> spectrumFilenames, out IList<string> directoriesSearched, out string resultsFilepath))
                return false;

            string extraHelp = Environment.NewLine + Resources.CommandLine_ShowLibraryMissingExternalSpectraError_Description;

            string messageFormat = spectrumFilenames.Count > 1
                ? Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFilesError
                : Resources.VendorIssueHelper_ShowLibraryMissingExternalSpectrumFileError;

            _out.WriteLine(string.Format(messageFormat,
                               resultsFilepath, string.Join(Environment.NewLine, spectrumFilenames),
                               string.Join(Environment.NewLine, directoriesSearched),
                               BiblioSpecLiteBuilder.BiblioSpecSupportedFileExtensions) + extraHelp);

            return true;
        }

        private bool IsLogStatusDeferredAtTime(DateTime currentTime, IProgressStatus status)
        {
            // Show progress at least every 2 seconds and at 100%, if any other percentage
            // output has been shown.
            return (currentTime - _lastOutput).TotalSeconds < 2 && !status.IsError &&
                   (status.PercentComplete != 100 || _lastOutput == _waitStart);
        }

        private void WriteMultiStatusErrors(MultiProgressStatus multiStatus)
        {
            for (int i = 0; i < multiStatus.ProgressList.Count; i++)
            {
                var progressStatus = multiStatus.ProgressList[i];
                if (progressStatus.IsError)
                {
                    var rawPath = progressStatus.FilePath.GetFilePath();
                    var missingDataException = progressStatus.ErrorException as MissingDataException;
                    if (missingDataException != null)
                        rawPath = missingDataException.ImportPath.GetFilePath();
                    _out.WriteLine(
                        _warnOnImportFailure
                        ? Resources.CommandLine_ImportResultsFile_Warning__Failed_importing_the_results_file__0____Ignoring___
                        : Resources.CommandLine_ImportResultsFile_Error__Failed_importing_the_results_file__0__,
                        rawPath);
                    _out.WriteLine(Resources.CommandProgressMonitor_UpdateProgressInternal_Message__ +
                                   progressStatus.ErrorException);
                    _out.WriteLine();
                }
            }
        }

        private void Wait()
        {
            while (_waiting)
            {
                _out.Write(@".");
                Thread.Sleep(1000);
            }
            _out.WriteLine(Resources.CommandWaitBroker_Wait_Done);
        }
    }
}
