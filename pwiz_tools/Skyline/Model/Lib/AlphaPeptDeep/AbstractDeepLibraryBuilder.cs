/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    /// <summary>
    /// Abstract base class for AlphaPeptDeep and Carafe library builders so they can share code
    /// </summary>
    public abstract class AbstractDeepLibraryBuilder : ILibraryBuildWarning
    {
        public static IList<ModificationType> PopulateUniModList(IList<ModificationIndex> supportedList)
        {
            IList<ModificationType> modList = new List<ModificationType>();
            for (int m = 0; m < UniModData.UNI_MOD_DATA.Length; m++)
            {
                if (!UniModData.UNI_MOD_DATA[m].ID.HasValue ||
                    (supportedList != null &&
                     supportedList.FirstOrDefault(x => x.Index == UniModData.UNI_MOD_DATA[m].ID.Value) == null))
                    continue;

                var accession = UniModData.UNI_MOD_DATA[m].ID.Value + @":" + UniModData.UNI_MOD_DATA[m].Name;
                var name = UniModData.UNI_MOD_DATA[m].Name;
                var formula = UniModData.UNI_MOD_DATA[m].Formula;
                modList.Add(new ModificationType(accession, name, formula));
            }
            return modList;
        }

        private DateTime _nowTime = DateTime.Now;

        protected AbstractDeepLibraryBuilder(SrmDocument document, IrtStandard irtStandard)
        {
            Document = document;
            IrtStandard = irtStandard;
        }

        public SrmDocument Document { get; private set; }
        
        public IrtStandard IrtStandard { get; private set; }
        
        public string AmbiguousMatchesMessage => null;

        public string BuildCommandArgs => null;

        public string BuildOutput => null;

        public string TimeStamp => _nowTime.ToString(@"yyyy-MM-dd_HH-mm-ss");

        public string WorkDir { get; private set; }

        public void EnsureWorkDir(string path, string tool)
        {
            if (WorkDir == null)
            {
                WorkDir = Path.Combine(path, tool, TimeStamp);
                Directory.CreateDirectory(WorkDir);
            }
        }
        
        public abstract string InputFilePath { get; }
        
        public abstract string TrainingFilePath { get; }

        public float FractionOfExpectedOutputLinesGenerated => TotalExpectedLinesOfOutput != 0
            ? TotalGeneratedLinesOfOutput / (float) TotalExpectedLinesOfOutput
            : 1.0F;
        
        public int TotalExpectedLinesOfOutput { get; private protected set; }
        public int TotalGeneratedLinesOfOutput { get; private protected set; }

        public void PreparePrecursorInputFile(IList<ModificationType> modificationNames, IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.LibraryHelper_PreparePrecursorInputFile_Preparing_prediction_input_file));

            var precursorTable = GetPrecursorTable(false);
            File.WriteAllLines(InputFilePath, precursorTable);
        }

        public void PrepareTrainingInputFile(IList<ModificationType> modificationNames, IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.LibraryHelper_PrepareTrainingInputFile_Preparing_training_input_file));

            var trainingTable = GetPrecursorTable(true);
            File.WriteAllLines(TrainingFilePath, trainingTable);
        }

        protected abstract IEnumerable<string> GetHeaderColumnNames(bool training);

        public IEnumerable<string> GetPrecursorTable(bool training)
        {
            var result = new List<string> { string.Join(TextUtil.SEPARATOR_TSV_STR, GetHeaderColumnNames(training)) };

            // First add the iRT standard peptides
            if (IrtStandard != null && !IrtStandard.IsEmpty && !IrtStandard.IsAuto)
            {
                foreach (var peptide in IrtStandard.GetDocument().Peptides)
                {
                    result.AddRange(GetTableRows(peptide, training));
                }
            }

            // Build precursor table row by row
            foreach (var peptide in Document.Peptides)
            {
                result.AddRange(GetTableRows(peptide, training));
            }

            return result.Distinct();
        }


        /// <summary>
        /// Helper function that adds a peptide to the result list
        /// </summary>
        private IEnumerable<string> GetTableRows(PeptideDocNode peptide, bool training)
        {
            var modifiedSeq = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);

            if (!ValidateModifications(modifiedSeq, out var mods, out var modSites))
                yield break;

            foreach (var charge in peptide.TransitionGroups
                         .Select(transitionGroup => transitionGroup.PrecursorCharge).Distinct())
            {
                yield return GetTableRow(peptide, modifiedSeq, charge, training, mods, modSites);
            }
        }

        protected abstract string GetTableRow(PeptideDocNode peptide,
            ModifiedSequence modifiedSequence, int charge, bool training,
            string modsBuilder, string modSitesBuilder);

        protected abstract string ToolName { get; }
        
        protected abstract IList<ModificationType> ModificationTypes { get; }
        private IList<string> _warningMods;
        protected internal bool ValidateModifications(ModifiedSequence modifiedSequence, out string mods, out string modSites)
        {
            var modsBuilder = new StringBuilder();
            var modSitesBuilder = new StringBuilder();

            _warningMods ??= GetWarningMods();

            bool unsupportedModification = false;
            for (var i = 0; i < modifiedSequence.ExplicitMods.Count; i++)
            {
                var mod = modifiedSequence.ExplicitMods[i];
                var modWarns = _warningMods.Where(m => m.Contains(mod.Name)).ToArray();
                if (!mod.UnimodId.HasValue && modWarns.Length == 0)
                {
                    var msg = string.Format(ModelsResources.BuildPrecursorTable_UnsupportedModification, modifiedSequence, mod.Name, ToolName);
                    Messages.WriteAsyncUserMessage(msg);
                    unsupportedModification = true;
                    continue;
                }

                var unimodIdWithName = mod.UnimodIdWithName;
                var modNames = ModificationTypes.Where(m => m.Accession == unimodIdWithName).ToArray();

                if (modNames.Length == 0 && modWarns.Length == 0)
                {
                    var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_UnsupportedModification, modifiedSequence, mod.Name, unimodIdWithName, ToolName);
                    Messages.WriteAsyncUserMessage(msg);
                    unsupportedModification = true;
                    continue;
                }
                if (modNames.Length == 0)
                    continue;

                string modName = GetModName(modNames.Single(), modifiedSequence.GetUnmodifiedSequence(), mod.IndexAA);
                modsBuilder.Append(modName);
                modSitesBuilder.Append((mod.IndexAA + 1).ToString()); // + 1 because alphapeptdeep mod_site number starts from 1 as the first amino acid
                if (i != modifiedSequence.ExplicitMods.Count - 1)
                {
                    modsBuilder.Append(TextUtil.SEMICOLON);
                    modSitesBuilder.Append(TextUtil.SEMICOLON);
                }
            }

            mods = modsBuilder.ToString();
            modSites = modSitesBuilder.ToString();
            
            return !unsupportedModification;
        }

        protected virtual string GetModName(ModificationType mod, string unmodifiedSequence, int modIndexAA)
        {
            return mod.AlphaNameWithAminoAcid(unmodifiedSequence, modIndexAA);
        }

        public string GetWarning()
        {
            var warningMods = GetWarningMods();
            if (warningMods.Count == 0)
                return null;

            var warningModString = string.Join(Environment.NewLine, warningMods);
            return string.Format(ModelResources.Alphapeptdeep_Warn_unknown_modification,
                warningModString);
        }
        
        public IList<string> GetWarningMods()
        {
            var resultList = new List<string>();

            // Build precursor table row by row
            foreach (var peptide in Document.Peptides)
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);

                for (var i = 0; i < modifiedSequence.ExplicitMods.Count; i++)
                {
                    var mod = modifiedSequence.ExplicitMods[i];
                    if (!mod.UnimodId.HasValue)
                    {
                        var haveMod = resultList.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            resultList.Add(mod.Name);
                        }
                    }

                    var unimodIdWithName = mod.UnimodIdWithName;
                    var modNames = ModificationTypes.Where(m => m.Accession == unimodIdWithName).ToArray();
                    if (modNames.Length == 0)
                    {
                        var haveMod = resultList.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            resultList.Add(mod.Name);
                        }
                    }
                }
            }

            // For better readability
            for (int i = 0; i < resultList.Count; i++)
            {
                resultList[i] = resultList[i].Indent(1);
            }
            return resultList;
        }
    }
}
