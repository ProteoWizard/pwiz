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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    /// <summary>
    /// Abstract base class for AlphaPeptDeep and Carafe library builders so they can share code. 
    /// </summary>
    public abstract class AbstractDeepLibraryBuilder : ILibraryBuildWarning
    {
        private DateTime _nowTime = DateTime.Now;
        
        protected AbstractDeepLibraryBuilder(SrmDocument document, IrtStandard irtStandard)
        {
            Document = document;
            IrtStandard = irtStandard;
        }

        public SrmDocument Document { get; private set; }

        public SrmDocument TrainingDocument { get; private set; }
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
            ? TotalGeneratedLinesOfOutput / (float)TotalExpectedLinesOfOutput
            : 1.0F;

        public int TotalExpectedLinesOfOutput { get; private protected set; }
        public int TotalGeneratedLinesOfOutput { get; private protected set; }

        public void PreparePrecursorInputFile(IProgressMonitor progress, ref IProgressStatus progressStatus)
        {
            progress.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(ModelResources.LibraryHelper_PreparePrecursorInputFile_Preparing_prediction_input_file));

            var precursorTable = GetPrecursorTable(false);
            File.WriteAllLines(InputFilePath, precursorTable);
        }

        public void PrepareTrainingInputFile(IProgressMonitor progress, ref IProgressStatus progressStatus)
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
            if (!training && IrtStandard != null && !IrtStandard.IsEmpty && !IrtStandard.IsAuto)
            {
                foreach (var peptide in IrtStandard.GetDocument().Peptides)
                {
                    result.AddRange(GetTableRows(peptide, false));
                }
            }

            // Build precursor table row by row
            if (training)
            {
                if (TrainingDocument != null)
                    foreach (var peptide in TrainingDocument.Peptides)
                    {
                        result.AddRange(GetTableRows(peptide, true));
                    }
            }
            else
            {
                foreach (var peptide in Document.Peptides)
                {
                    result.AddRange(GetTableRows(peptide, false));
                }
            }

            return result.Distinct();
        }

        public static ModificationType GetUniModType(int id, PredictionSupport pr)
        {
            return new ModificationType(UniModData.UNI_MOD_DATA.FirstOrDefault(m => m.ID == id), pr);
        }

        /// <summary>
        /// Helper function that adds a peptide to the result list.
        /// This function may output async warnings.
        /// </summary>
        private IEnumerable<string> GetTableRows(PeptideDocNode peptide, bool training)
        {
            var modifiedSeq = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);
            var peptidePredictionSupport =
                ValidateSequenceModifications(modifiedSeq, out var mods, out var modSites);

            if (peptidePredictionSupport == PredictionSupport.none)
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

        protected abstract LibraryBuilderModificationSupport LibraryBuilderModificationSupport { get; }

        /// <summary>
        /// Validates a modified peptide sequence with potential warning output for lack
        /// of model support, returning information required to write the input table row(s)
        /// for the given peptide.
        /// </summary>
        /// <param name="modifiedSequence">Modified peptide sequence</param>
        /// <param name="mods">String representation of mods contained</param>
        /// <param name="modSites">String representation of modification sites</param>
        /// <returns>Available <see cref="PredictionSupport"/> models for the modified peptide sequence</returns>
        protected internal PredictionSupport ValidateSequenceModifications(ModifiedSequence modifiedSequence, out string mods, out string modSites)
        {
            var supportedModels = PredictionSupport.all;    // Assume all supported to start

            // The list created below is probably always short enough that determining
            // if it contains a modification would not be greatly improved by caching a set
            // for use here instead of the list.
            var modificationTypes = new List<Tuple<ModifiedSequence.Modification, ModificationType>>();
            foreach (var explicitMod in modifiedSequence.ExplicitMods)
            {
                if (explicitMod.UnimodId.HasValue)
                {
                    var modificationType = LibraryBuilderModificationSupport.GetModificationType(explicitMod.UnimodId.Value);
                    modificationTypes.Add(Tuple.Create(explicitMod, modificationType));
                }
                else
                {
                    supportedModels = PredictionSupport.none;

                    Messages.WriteAsyncUserMessage(ModelsResources.BuildPrecursorTable_UnsupportedModification,
                        modifiedSequence, explicitMod.Name, ToolName);
                }
            }

            // Review all modifications for support in the order they appeared, mentioning any
            // limitations for the first occurence.
            foreach (var modAndType in modificationTypes.GroupBy(tuple => tuple.Item1.Name)
                         .Select(g => g.First()))
            {
                var modType = modAndType.Item2;
                if (modType == null || !modType.IsSupported(PredictionSupport.fragmentation))
                {
                    supportedModels = PredictionSupport.none;
                }
                else
                {
                    if (!modType.IsSupported(PredictionSupport.retention_time))
                        supportedModels &= ~PredictionSupport.retention_time;
                    if (!modType.IsSupported(PredictionSupport.ccs))
                        supportedModels &= ~PredictionSupport.ccs;
                }

                if (modType == null || !modType.IsSupported(PredictionSupport.all))
                {
                    var mod = modAndType.Item1;
                    Messages.WriteAsyncUserMessage(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        modifiedSequence, mod.Name, mod.UnimodIdWithName, ToolName);
                }
            }

            var modsList = new List<string>();
            var modSitesList = new List<int>();

            foreach (var group in modificationTypes.GroupBy(tuple => tuple.Item1.IndexAA))
            {
                var modNames = UniModData.UNI_MOD_DATA
                    .Where(m => group.First().Item1.UnimodIdWithName.Contains(m.Name)).ToArray();
                if (modNames.Length == 0)
                {
                    supportedModels = PredictionSupport.none;
                    
                    Messages.WriteAsyncUserMessage(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        modifiedSequence, group.First().Item1.Name, group.First().Item1.UnimodIdWithName, ToolName);
                }
                else
                {
                    modsList.AddRange(modificationTypes
                        .FindAll(tuple => tuple.Item1.IndexAA == group.First().Item1.IndexAA).Select(tuple =>
                            GetModName(new ModificationType(modNames.Single(), PredictionSupport.none),
                                modifiedSequence.GetUnmodifiedSequence(), tuple.Item1.IndexAA)).ToList());
                    modSitesList.AddRange(modificationTypes
                        .FindAll(tuple => tuple.Item1.IndexAA == group.First().Item1.IndexAA)
                        .Select(tuple => tuple.Item1.IndexAA + 1).ToList());
                }
            }

            mods = string.Join(TextUtil.SEMICOLON, modsList);
            modSites = string.Join(TextUtil.SEMICOLON, modSitesList);

            return supportedModels;
        }


        protected virtual string GetModName(ModificationType mod, string unmodifiedSequence, int modIndexAA)
        {
            return mod.AlphaNameWithAminoAcid(unmodifiedSequence, modIndexAA);
        }

        public string GetWarning()
        {
            var warningModSupports = GetWarningMods();
            if (warningModSupports.IsNullOrEmpty())
                return null;

            var warnings = new List<string>();
            AddWarning(warnings, ModelResources.Alphapeptdeep_Warn_unknown_modification,
                warningModSupports.Where(kvp => kvp.Value == PredictionSupport.none).Select(kvp => kvp.Key));
            AddWarning(warnings, ModelResources.Alphapeptdeep_Warn_limited_modification,
                warningModSupports.Where(kvp => kvp.Value != PredictionSupport.all).Select(kvp => kvp.Key));
            return TextUtil.LineSeparate(warnings);
        }

        private void AddWarning(List<string> warnings, string formatString, IEnumerable<string> modNames)
        {
            var mods = modNames.OrderBy(n => n).ToList();
            if (mods.Count > 0)
            {
                if (warnings.Count > 0)
                    warnings.Add(Environment.NewLine);  // Add extra blank line between the warnings.
                warnings.Add(string.Format(formatString, TextUtil.LineSeparate(mods.Select(m => m.Indent(1)))));
            }
        }

        /// <summary>
        /// Returns a mapping between modification names in the document that will generate a warning and their available PredictionSupport. 
        /// </summary>
        public Dictionary<string, PredictionSupport> GetWarningMods()
        {
            var warningModSupports = new Dictionary<string, PredictionSupport>();

            // Build precursor table row by row
            foreach (var peptide in Document.Peptides)
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);

                foreach (var mod in modifiedSequence.ExplicitMods)
                {
                    if (warningModSupports.ContainsKey(mod.Name))
                        continue;
                    
                    var modType = LibraryBuilderModificationSupport.GetModificationType(mod.UnimodId);
                    if (modType == null)
                        warningModSupports[mod.Name] = PredictionSupport.none;
                    else if (!modType.IsSupported(PredictionSupport.all))
                        warningModSupports[mod.Name] = modType.SupportedModels;
                }
            }

            return warningModSupports;
        }
    }
    
    [Flags]
    public enum PredictionSupport { none = 0, fragmentation = 1, retention_time = 2, ccs = 4, all = 7 }

    public class LibraryBuilderModificationSupport
    {
        private Dictionary<int, ModificationType> _modificationSupport;

        public LibraryBuilderModificationSupport(List<ModificationType> supportedModifications)
        {
            _modificationSupport = new Dictionary<int, ModificationType>();
            if (supportedModifications != null)
                _modificationSupport = supportedModifications.ToDictionary(mt => mt.Id);
        }

        /// <summary>
        /// Helper function to extract Unimod Ids from a modified peptide sequence
        /// </summary>
        /// <param name="modifiedPeptide">Peptide sequence that maybe encoded with Unimod Ids</param>
        /// <returns>A set of unique integers representing the Unimod IDs found in the modified sequence</returns>
        private IEnumerable<int> GetModificationIds(string modifiedPeptide)
        {
            var modifications = new UniqueList<int>();
            foreach (string part in modifiedPeptide.Split('[', ']', ':'))
            {
                if (int.TryParse(part, out int intResult))
                {
                    modifications.Add(intResult);
                }
            }
            return modifications;
        }

        public ModificationType GetModificationType(int? id)
        {
            if (id.HasValue && _modificationSupport.TryGetValue(id.Value, out var type))
                return type;
            return null;
        }

        public bool PeptideHasOnlyMs2SupportedMod(string modifiedPeptide)
        {
            return GetModificationIds(modifiedPeptide)
                .All(id => IsSupportedMod(id, PredictionSupport.fragmentation));
        }

        public bool PeptideHasOnlyRtSupportedMod(string modifiedPeptide)
        {
            return GetModificationIds(modifiedPeptide)
                .All(id => IsSupportedMod(id, PredictionSupport.retention_time));
        }

        public bool PeptideHasOnlyCcsSupportedMod(string modifiedPeptide)
        {
            return GetModificationIds(modifiedPeptide)
                .All(id => IsSupportedMod(id, PredictionSupport.ccs));
        }

        public bool IsSupportedMod(int? id, PredictionSupport ps)
        {
            var type = GetModificationType(id);
            return type != null && type.IsSupported(ps);
        }
    }

    public class ModificationType
    {
        public ModificationType(UniModModificationData unimodModificationData, PredictionSupport supportedModels)
        {
            Id = unimodModificationData.ID.Value;
            Name = unimodModificationData.Name;
            Accession = Name + ':' + Id;
            Comment = unimodModificationData.Formula;
            SupportedModels = supportedModels;
        }

        public int Id { get; }
        public string Name { get; }
        public string Accession { get; }
        public string Comment { get; }

        public PredictionSupport SupportedModels { get; }

        public bool IsSupported(PredictionSupport ps)
        {
            return (SupportedModels & ps) == ps;
        }

        public string AlphaNameWithAminoAcid(string unmodifiedSequence, int index)
        {
            string modification = Name.Replace(TextUtil.LEFT_PARENTHESIS, string.Empty)
                .Replace(TextUtil.RIGHT_PARENTHESIS, string.Empty)
                .Replace(TextUtil.SPACE, TextUtil.AT)
                .Replace(@"Acetyl@N-term", @"Acetyl@Protein_N-term");
            string[] name = modification.Split(TextUtil.AT[0]);
            string alphaName = name[0] + TextUtil.AT + unmodifiedSequence[index];
            if (index == 0 && modification.EndsWith(@"term"))
            {
                alphaName = modification;
            }
            return alphaName;
        }

        #region object overrides

        public override string ToString()
        {
            return string.Format(ModelsResources.BuildPrecursorTable_ModificationType, Accession, Name, Comment);
        }

        protected bool Equals(ModificationType other)
        {
            return Id == other.Id &&
                   Name == other.Name &&
                   Accession == other.Accession &&
                   Comment == other.Comment &&
                   SupportedModels == other.SupportedModels;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ModificationType)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id;
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Accession != null ? Accession.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Comment != null ? Comment.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)SupportedModels;
                return hashCode;
            }
        }

        #endregion
    }
}
