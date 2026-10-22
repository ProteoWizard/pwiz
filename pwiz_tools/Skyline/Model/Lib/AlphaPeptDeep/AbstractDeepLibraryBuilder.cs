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
using pwiz.CarafeSharp.Core;
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
            var modificationTypes = new List<(ModifiedSequence.Modification Mod, StaticMod UniModMod, ModificationType Type)>();
            foreach (var explicitMod in modifiedSequence.ExplicitMods)
            {
                var uniModMod = MatchUniMod(explicitMod);
                if (uniModMod != null)
                {
                    var modificationType = LibraryBuilderModificationSupport.GetModificationType(uniModMod.UnimodId);
                    modificationTypes.Add((explicitMod, uniModMod, modificationType));
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
            foreach (var modAndType in modificationTypes.GroupBy(tuple => tuple.Mod.Name)
                         .Select(g => g.First()))
            {
                var modType = modAndType.Type;
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
                    Messages.WriteAsyncUserMessage(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        modifiedSequence, modAndType.Mod.Name, UnimodIdWithName(modAndType.UniModMod), ToolName);
                }
            }

            var modsList = new List<string>();
            var modSitesList = new List<int>();

            foreach (var group in modificationTypes.GroupBy(tuple => tuple.Mod.IndexAA))
            {
                var first = group.First();
                var modNames = UniModData.UNI_MOD_DATA
                    .Where(m => UnimodIdWithName(first.UniModMod).Contains(m.Name)).ToArray();
                if (modNames.Length == 0)
                {
                    supportedModels = PredictionSupport.none;

                    Messages.WriteAsyncUserMessage(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        modifiedSequence, first.Mod.Name, UnimodIdWithName(first.UniModMod), ToolName);
                }
                else
                {
                    modsList.AddRange(modificationTypes
                        .FindAll(tuple => tuple.Mod.IndexAA == first.Mod.IndexAA).Select(tuple =>
                            GetModName(new ModificationType(modNames.Single(), PredictionSupport.none),
                                modifiedSequence.GetUnmodifiedSequence(), tuple.Mod.IndexAA)).ToList());
                    modSitesList.AddRange(modificationTypes
                        .FindAll(tuple => tuple.Mod.IndexAA == first.Mod.IndexAA)
                        .Select(tuple => tuple.Mod.IndexAA + 1).ToList());
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

        /// <summary>
        /// The UniMod modification a document modification is: itself when it carries a UniMod id,
        /// else the UniMod modification with the same definition (formula or mass, amino acids and
        /// terminus), which an older document names in its own way, like "Carbamidomethyl Cysteine"
        /// for Carbamidomethyl (C). Null when no UniMod modification matches.
        /// </summary>
        public static StaticMod MatchUniMod(ModifiedSequence.Modification mod)
        {
            var staticMod = mod.StaticMod;
            if (staticMod.UnimodId.HasValue)
                return staticMod;
            // The models see the modification's chemistry: not its relative retention time, and not its
            // neutral losses, which the models do not predict, so an Oxidation (M) defined with its own
            // losses is UniMod's Oxidation (M) for prediction
            return UniMod.FindMatchingStaticMod(staticMod.ChangeRelativeRT(RelativeRT.Matching), true, true);
        }

        private static string UnimodIdWithName(StaticMod uniModMod)
        {
            return uniModMod.UnimodId + @":" + uniModMod.Name;
        }

        public string GetWarning()
        {
            return GetWarning(GetWarningMods(), true);
        }

        /// <summary>
        /// The warning about building with modifications the models do not know or support only in
        /// part, from <see cref="GetWarningMods(SrmDocument,LibraryBuilderModificationSupport)"/>, or
        /// null when there are none. With <paramref name="ask"/> it ends with the question a build
        /// asks before it starts; without, it is the statement alone, for a tooltip.
        /// </summary>
        public static string GetWarning(Dictionary<string, PredictionSupport> warningModSupports, bool ask)
        {
            if (warningModSupports.IsNullOrEmpty())
                return null;

            var warnings = new List<string>();
            AddWarning(warnings, ModelResources.Alphapeptdeep_Warn_unknown_modification,
                warningModSupports.Where(kvp => kvp.Value == PredictionSupport.none).Select(kvp => kvp.Key));
            // Only the modifications the models know in part; an unknown one is already listed above
            AddWarning(warnings, ModelResources.Alphapeptdeep_Warn_limited_modification,
                warningModSupports.Where(kvp => kvp.Value != PredictionSupport.none && kvp.Value != PredictionSupport.all).Select(kvp => kvp.Key));
            if (ask)
            {
                warnings.Add(Environment.NewLine);
                warnings.Add(ModelResources.Alphapeptdeep_Warn_continue_building);
            }
            return TextUtil.LineSeparate(warnings);
        }

        private static void AddWarning(List<string> warnings, string formatString, IEnumerable<string> modNames)
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
            return GetWarningMods(Document, LibraryBuilderModificationSupport);
        }

        /// <summary>
        /// The document's modifications the models do not know (<see cref="PredictionSupport.none"/>)
        /// or support only in part, by the document's name for each, as a form can ask before a
        /// builder exists.
        /// </summary>
        public static Dictionary<string, PredictionSupport> GetWarningMods(SrmDocument document,
            LibraryBuilderModificationSupport modificationSupport)
        {
            var warningModSupports = new Dictionary<string, PredictionSupport>();

            // Build precursor table row by row
            foreach (var peptide in document.Peptides)
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(document.Settings, peptide, IsotopeLabelType.light);

                foreach (var mod in modifiedSequence.ExplicitMods)
                {
                    if (warningModSupports.ContainsKey(mod.Name))
                        continue;

                    var modType = modificationSupport.GetModificationType(MatchUniMod(mod)?.UnimodId);
                    if (modType == null)
                        warningModSupports[mod.Name] = PredictionSupport.none;
                    // The library's spectra and retention times are what the warning is about; CCS, which only
                    // the pretrained model predicts, is extra, so a fine-tuned modification without it is not listed
                    else if (!modType.IsSupported(PredictionSupport.fragmentation | PredictionSupport.retention_time))
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

        /// <summary>
        /// This support extended by the modifications a fine-tuned model was trained on, named as
        /// CarafeSharp names them (<c>Carbamidomethyl@C</c>), each supported for fragmentation and
        /// retention time; the CCS model stays pretrained. A modification already supported keeps its
        /// support, and a name CarafeSharp's table or UniMod does not know adds nothing.
        /// </summary>
        public LibraryBuilderModificationSupport WithTrainingModifications(IEnumerable<string> modificationNames)
        {
            var types = _modificationSupport.Values.ToList();
            var ids = new HashSet<int>(_modificationSupport.Keys);
            foreach (string name in modificationNames)
            {
                if (!ModificationTable.TryGet(name, out var definition) || definition.UnimodId <= 0 ||
                    !ids.Add(definition.UnimodId) || UniModData.UNI_MOD_DATA.All(m => m.ID != definition.UnimodId))
                {
                    continue;
                }
                types.Add(AbstractDeepLibraryBuilder.GetUniModType(definition.UnimodId,
                    PredictionSupport.fragmentation | PredictionSupport.retention_time));
            }
            return new LibraryBuilderModificationSupport(types);
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
