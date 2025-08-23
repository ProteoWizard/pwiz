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
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.AlphaPeptDeep
{
    public class PredictionSupport : Immutable
    {
        public PredictionSupport(bool fragmentation, bool retentionTime, bool ccs)
        {
            Fragmentation = fragmentation;
            RetentionTime = retentionTime; 
            Ccs = ccs;
        }
        public PredictionSupport(PredictionSupport other)
        {
            Fragmentation = other.Fragmentation;
            RetentionTime = other.RetentionTime;
            Ccs = other.Ccs;
        }

        public static readonly PredictionSupport ALL = new PredictionSupport(true, true, true);

        public static readonly PredictionSupport FRAGMENTATION = new PredictionSupport(true, false, false);

        public static readonly PredictionSupport RETENTION_TIME = new PredictionSupport(false, true, false);

        public static readonly PredictionSupport CCS = new PredictionSupport(false, false, true);

        public static readonly PredictionSupport FRAG_RT_ONLY = new PredictionSupport(true, true, false);

        public static readonly PredictionSupport FRAG_CCS_ONLY = new PredictionSupport(true, false, true);

        public static readonly PredictionSupport RT_CCS_ONLY = new PredictionSupport(false, true, true);

        public static readonly PredictionSupport NONE = new PredictionSupport(false, false, false);


        public bool Fragmentation { get; private set; }
        public bool RetentionTime { get; private set; }
        public bool Ccs { get; private set; }

        public bool Equals(PredictionSupport other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Fragmentation == other.Fragmentation && RetentionTime == other.RetentionTime && Ccs == other.Ccs;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Fragmentation.GetHashCode();
                hashCode = (hashCode * 397) ^ RetentionTime.GetHashCode();
                hashCode = (hashCode * 397) ^ Ccs.GetHashCode();
                return hashCode;
            }
        }
    };
    public class ModificationType
    {
        public ModificationType(int id, string name, string comment, PredictionSupport supportedModels)
        {
            Id = id;
            Accession = id + @":" + name;
            Name = name;
            Comment = comment;
            SupportedModels = supportedModels;
        }

        public ModificationType(UniModModificationData unimodModificationData, PredictionSupport supportedModels)
        {
            Id = unimodModificationData.ID.Value;
            Accession = Id + @":" + unimodModificationData.Name;
            Name = unimodModificationData.Name;
            Comment = unimodModificationData.Formula;
            SupportedModels = supportedModels;
        }

        public int Id { get; private set; }
        public string Accession { get; private set; }
        public string Name { get; private set; }
        public string Comment { get; private set; }
        public PredictionSupport SupportedModels { get; private set; }

        public string AlphaNameWithAminoAcid(string unmodifiedSequence, int index)
        {
            string modification = Name.Replace(@"(", "").Replace(@")", "").Replace(@" ", @"@").Replace(@"Acetyl@N-term", @"Acetyl@Protein_N-term");
            char delimiter = '@';
            string[] name = modification.Split(delimiter);
            string alphaName = name[0] + @"@" + unmodifiedSequence[index];
            if (index == 0 && modification.EndsWith(@"term"))
            {
                alphaName = modification;
            }
            return alphaName;
        }
        public override string ToString() { return string.Format(ModelsResources.BuildPrecursorTable_ModificationType, Accession, Name, Comment); }

        public bool Equals(ModificationType other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Id == other.Id && Accession == other.Accession && Name == other.Name && Comment == other.Comment && SupportedModels.Equals(other.SupportedModels);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Id;
                hashCode = (hashCode * 397) ^ (Accession != null ? Accession.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Comment != null ? Comment.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (SupportedModels != null ? SupportedModels.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
    public class LibraryBuilderModificationSupport
    {
        internal Dictionary<int, ModificationType> _modificationSupport;

        public LibraryBuilderModificationSupport(List<ModificationType> supportedModifications)
        {
            if (supportedModifications != null)
            {
                _modificationSupport = new Dictionary<int, ModificationType>();
                foreach (var mod in supportedModifications)
                {
                    _modificationSupport[mod.Id] = mod;
                }
            }
        }

        public bool AreAllModelsSupported(int? id)
        {
            if (id == null)
                return false;
            return IsMs2SupportedMod(id) && IsRtSupportedMod(id) && IsCcsSupportedMod(id);
        }

        public bool IsMs2SupportedMod(int? id)
        {
            if (id == null)
                return false;

            var type = GetModificationType(id.Value);
            if (type == null)
            {
                return false;
            }

            return type.SupportedModels.Fragmentation;
        }
        public bool IsRtSupportedMod(int? id)
        {
            if (id == null)
                return false;

            var type = GetModificationType(id.Value);
            if (type == null)
            {
                return false;
            }

            return type.SupportedModels.RetentionTime;
        }
        public bool IsCcsSupportedMod(int? id)
        {
            if (id == null)
                return false;

            var type = GetModificationType(id.Value);
            if (type == null)
            {
                return false;
            }

            return type.SupportedModels.Ccs;
        }
        /// <summary>
        /// Helper function to extract Unimod Ids from a modified peptide sequence.
        /// </summary>
        /// <param name="modifiedPeptide">Peptide sequence that maybe encoded with Unimod Ids.</param>
        /// <returns></returns>
        private IEnumerable<int> GetModificationIds(string modifiedPeptide)
        {
            var modifications = new UniqueList<int>();
            string[] peptideParts = modifiedPeptide.Split(new[] { '[', ']', ':' });
            foreach (string part in peptideParts)
            {
                if (int.TryParse(part, out int intResult))
                {
                    modifications.Add(intResult);
                }
            }
            return modifications;
        }

        [CanBeNull]
        public ModificationType GetModificationType(int id)
        {
            if (_modificationSupport.ContainsKey(id))
                return _modificationSupport[id];
            return null;
        }

        public bool PeptideHasOnlyMs2SupportedMod(string modifiedPeptide)
        {
            return GetModificationIds(modifiedPeptide)
                .All(id => true == GetModificationType(id)?.SupportedModels.Fragmentation);
        }
        public bool PeptideHasOnlyRtSupportedMod(string modifiedPeptide)
        {
            return GetModificationIds(modifiedPeptide)
                .All(id => true == GetModificationType(id)?.SupportedModels.RetentionTime);
        }
        public bool PeptideHasOnlyCcsSupportedMod(string modifiedPeptide)
        {
            return GetModificationIds(modifiedPeptide)
                .All(id => true == GetModificationType(id)?.SupportedModels.Ccs);
        }
    }

    /// <summary>
    /// Abstract base class for AlphaPeptDeep and Carafe library builders so they can share code. 
    /// </summary>
    public abstract class AbstractDeepLibraryBuilder : ILibraryBuildWarning
    {
        private DateTime _nowTime = DateTime.Now;
        private Dictionary<string, PredictionSupport> _warningModSupports;
        protected AbstractDeepLibraryBuilder(SrmDocument document, IrtStandard irtStandard)
        {
            Document = document;
            IrtStandard = irtStandard;
        }
        protected AbstractDeepLibraryBuilder(SrmDocument document, SrmDocument trainingDocument, IrtStandard irtStandard)
        {
            Document = document;
            TrainingDocument = trainingDocument;
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


        /// <summary>
        /// Helper function that adds a peptide to the result list
        /// </summary>
        private IEnumerable<string> GetTableRows(PeptideDocNode peptide, bool training)
        {
            var modifiedSeq = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);
            var peptidePredictionSupport =
                ValidateSequenceModifications(modifiedSeq, out var mods, out var modSites);

            if (peptidePredictionSupport.Equals( PredictionSupport.NONE))
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


        protected abstract LibraryBuilderModificationSupport libraryBuilderModificationSupport { get; }

        /// <summary>
        /// Return the available PredictionSupport models for a modified peptide sequence.
        /// </summary>
        /// <param name="modifiedSequence">Modified peptide sequence.</param>
        /// <param name="mods">String representation of mods contained.</param>
        /// <param name="modSites">String representation of modification sites.</param>
        /// <returns></returns>
        protected internal PredictionSupport ValidateSequenceModifications(ModifiedSequence modifiedSequence, out string mods, out string modSites)
        {
            var modsList = new List<string>();
            var modSitesList = new List<int>();

            bool ms2SupportedMod = true;
            bool rtSupportedMod = true;
            bool ccsSupportedMod = true;

            // The list returned below is probably always short enough that determining
            // if it contains a modification would not be greatly improved by caching a set
            // for use here instead of the list.
            var modificationTypes = new List<Tuple<ModifiedSequence.Modification, ModificationType>>();
            foreach (var explicitMod in modifiedSequence.ExplicitMods)
            {
                ModificationType modificationType = null;
                if (explicitMod.UnimodId.HasValue)
                {
                    modificationType =
                        libraryBuilderModificationSupport.GetModificationType(explicitMod.UnimodId.Value);
                    modificationTypes.Add(Tuple.Create(explicitMod, modificationType));
                }
                else
                {
                    var msg = string.Format(ModelsResources.BuildPrecursorTable_UnsupportedModification,
                        modifiedSequence, explicitMod.Name, ToolName);
                    Messages.WriteAsyncUserMessage(msg);

                    ms2SupportedMod = false;
                    rtSupportedMod = false;
                    ccsSupportedMod = false;
                }
            }

            foreach (var group in modificationTypes.GroupBy(tuple => tuple.Item1.Name))
            {
                if (group.Any(tuple => true != tuple.Item2?.SupportedModels?.Fragmentation))
                {
                    var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        modifiedSequence, group.Key, group.First().Item1.UnimodIdWithName, ToolName);
                    Messages.WriteAsyncUserMessage(msg);
                    ms2SupportedMod = false;
                    rtSupportedMod = false;
                    ccsSupportedMod = false;
                    continue;
                }

                bool msgGenerated = false;
                if (group.Any(tuple => true != tuple.Item2?.SupportedModels?.RetentionTime))
                {
                    var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        modifiedSequence, group.Key, group.First().Item1.UnimodIdWithName, ToolName);
                    Messages.WriteAsyncUserMessage(msg);
                    rtSupportedMod = false;
                    msgGenerated = true;
                }

                if (group.Any(tuple => true != tuple.Item2?.SupportedModels?.Ccs))
                {
                    if (!msgGenerated)
                    {
                        var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                            modifiedSequence, group.Key, group.First().Item1.UnimodIdWithName, ToolName);
                        Messages.WriteAsyncUserMessage(msg);
                    }

                    ccsSupportedMod = false;
                }

            }

            foreach (var group in modificationTypes.GroupBy(tuple => tuple.Item1.IndexAA))
            {
                var modNames = UniModData.UNI_MOD_DATA
                    .Where(m => group.First().Item1.UnimodIdWithName.Contains(m.Name)).ToArray();
                if (modNames.Length == 0)
                {
                    ms2SupportedMod = false;
                    rtSupportedMod = false;
                    ccsSupportedMod = false;
                    var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification,
                        modifiedSequence, group.First().Item1.Name, group.First().Item1.UnimodIdWithName, ToolName);
                    Messages.WriteAsyncUserMessage(msg);
                }
                else
                {
                    modsList.AddRange(modificationTypes
                        .FindAll(tuple => tuple.Item1.IndexAA == group.First().Item1.IndexAA).Select(tuple =>
                            GetModName(new ModificationType(modNames.Single(), PredictionSupport.NONE),
                                modifiedSequence.GetUnmodifiedSequence(), tuple.Item1.IndexAA)).ToList());
                    modSitesList.AddRange(modificationTypes
                        .FindAll(tuple => tuple.Item1.IndexAA == group.First().Item1.IndexAA)
                        .Select(tuple => tuple.Item1.IndexAA + 1).ToList());
                }
            }

            mods = string.Join(TextUtil.SEMICOLON, modsList).ToString();
            modSites = string.Join(TextUtil.SEMICOLON, modSitesList).ToString();

            return new PredictionSupport(ms2SupportedMod, rtSupportedMod, ccsSupportedMod);
        }


        protected virtual string GetModName(ModificationType mod, string unmodifiedSequence, int modIndexAA)
        {
            return mod.AlphaNameWithAminoAcid(unmodifiedSequence, modIndexAA);
        }

        public string GetWarning()
        {
            var warningModSupports = GetWarningMods();

            var noMs2SupportWarningMods = warningModSupports.Where(kvp => kvp.Value.Fragmentation == false).Select(kvp => kvp.Key).ToList();
            var noRtSupportWarningMods = warningModSupports.Where(kvp => kvp.Value.RetentionTime == false).Select(kvp => kvp.Key).ToList();
            var noCcsSupportWarningMods = warningModSupports.Where(kvp => kvp.Value.Ccs == false).Select(kvp => kvp.Key).ToList();

            // var (noMs2SupportWarningMods, noRtSupportWarningMods, noCcsSupportWarningMods) = GetWarningMods();

            if (noMs2SupportWarningMods.Count == 0 && noRtSupportWarningMods.Count == 0 && noCcsSupportWarningMods.Count == 0)
                return null;

            string warningModString;
            if (noMs2SupportWarningMods.Count > 0)
            {
                warningModString = string.Join(Environment.NewLine, noMs2SupportWarningMods.Select(w => w.Indent(1)));
                return string.Format(ModelResources.Alphapeptdeep_Warn_unknown_modification,
                    warningModString);
            }

            if (noRtSupportWarningMods.Count > 0)
            {
                warningModString = string.Join(Environment.NewLine, noRtSupportWarningMods.Select(w => w.Indent(1)));
                return string.Format(ModelResources.Alphapeptdeep_Warn_limited_modification,
                    warningModString);
            }
            warningModString = string.Join(Environment.NewLine, noCcsSupportWarningMods.Select(w => w.Indent(1)));
            return string.Format(ModelResources.Alphapeptdeep_Warn_limited_modification,
                warningModString);


        }
        /// <summary>
        /// Returns a mapping between modification names in the document that will generate a warning and their available PredictionSupport. 
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, PredictionSupport> GetWarningMods()
        {
            if (_warningModSupports != null)
                return _warningModSupports;

            _warningModSupports = new Dictionary<string, PredictionSupport>();

            // Build precursor table row by row
            foreach (var peptide in Document.Peptides)
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);

                for (var i = 0; i < modifiedSequence.ExplicitMods.Count; i++)
                {
                    var mod = modifiedSequence.ExplicitMods[i];
                    if (!mod.UnimodId.HasValue)
                    {
                        _warningModSupports[mod.Name] = PredictionSupport.NONE;
                    }
                    else
                    {
                        var support = libraryBuilderModificationSupport.GetModificationType(mod.UnimodId.Value)?.SupportedModels ?? PredictionSupport.NONE;
                        if (!(Equals(support, PredictionSupport.ALL)))
                            _warningModSupports[mod.Name] = new PredictionSupport(support);
                    }
                }
            }

            return _warningModSupports;
        }
    }
}
