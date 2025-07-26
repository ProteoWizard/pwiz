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
    public class PredictionSupport : Immutable, IEquatable<PredictionSupport>
    {
        public static readonly PredictionSupport ALL = new PredictionSupport()
            { Fragmentation = true, RetentionTime = true, Ccs = true };

        public static readonly PredictionSupport FRAGMENTATION = new PredictionSupport() { Fragmentation = true };

        public static readonly PredictionSupport RETENTION_TIME = new PredictionSupport() { RetentionTime = true };

        public static readonly PredictionSupport CCS = new PredictionSupport() { Ccs = true };

        public static readonly PredictionSupport FRAG_RT_ONLY = new PredictionSupport()
            { Fragmentation = true, RetentionTime = true, Ccs = false };

        public static readonly PredictionSupport FRAG_CCS_ONLY = new PredictionSupport()
            { Fragmentation = true, RetentionTime = false, Ccs = true };

        public static readonly PredictionSupport RT_CCS_ONLY = new PredictionSupport()
            { Fragmentation = false, RetentionTime = true, Ccs = true };

        public static readonly PredictionSupport NONE = new PredictionSupport()
            { Fragmentation = false, RetentionTime = false, Ccs = false };


        public bool Fragmentation { get; private set; }
        public bool RetentionTime { get; private set; }
        public bool Ccs { get; private set; }

        public bool Equals(PredictionSupport other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Fragmentation == other.Fragmentation && RetentionTime == other.RetentionTime && Ccs == other.Ccs;
        }

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((PredictionSupport)obj);
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
    public class ModificationType : IEquatable<ModificationType>
    {
        public ModificationType(int id, string name, string comment, PredictionSupport supportedModels = null)
        {
            Id = id;
            Accession = id + @":" + name;
            Name = name;
            Comment = comment;
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
        internal List<ModificationType> _predictionSupport; //key is ModificationType.Index

        public LibraryBuilderModificationSupport(List<ModificationType> supportedModifications)
        {
            _predictionSupport = supportedModifications;
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
            var type = _predictionSupport.FirstOrDefault(mod => mod.Id == id);
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
            var type = _predictionSupport.FirstOrDefault(mod => mod.Id == id);
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
            var type = _predictionSupport.FirstOrDefault(mod => mod.Id == id);
            if (type == null)
            {
                return false;
            }

            return type.SupportedModels.Ccs;
        }


        public bool PeptideHasOnlyMs2SupportedMod(string modifiedPeptide)
        {
            string[] peptideParts = modifiedPeptide.Split(new[]{'[',']',':'});
            bool supported = true;
            foreach (string part in peptideParts)
            {
                if (int.TryParse(part, out int intResult))
                {
                    supported = supported && IsMs2SupportedMod(intResult);
                }
            }

            return supported;
        }
        public bool PeptideHasOnlyRtSupportedMod(string modifiedPeptide)
        {
            string[] peptideParts = modifiedPeptide.Split(new [] { '[', ']', ':' });
            bool supported = true;
            foreach (string part in peptideParts)
            {
                if (int.TryParse(part, out int intResult))
                {
                    supported = supported && IsRtSupportedMod(intResult);
                }
            }

            return supported;
        }
        public bool PeptideHasOnlyCcsSupportedMod(string modifiedPeptide)
        {
            string[] peptideParts = modifiedPeptide.Split(new [] { '[', ']', ':' });
            bool supported = true;
            foreach (string part in peptideParts)
            {
                if (int.TryParse(part, out int intResult))
                {
                    supported = supported && IsCcsSupportedMod(intResult);
                }
            }

            return supported;
        }
    }

    /// <summary>
    /// Abstract base class for AlphaPeptDeep and Carafe library builders so they can share code. 
    /// </summary>
    public abstract class AbstractDeepLibraryBuilder : ILibraryBuildWarning
    {
        private DateTime _nowTime = DateTime.Now;
        private IList<string> _noMs2SupportWarningMods;
        private IList<string> _noRtSupportWarningMods;
        private IList<string> _noCcsSupportWarningMods;
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
            var (ms2SupportedMod, rtSupportedMod, ccsSupportedMod) =
                ValidateModifications(modifiedSeq, out var mods, out var modSites);

            if (!ms2SupportedMod && !rtSupportedMod && !ccsSupportedMod)
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
        /// Returns three booleans, first one is true if ms2 model supports all the mods, second one is true if rt model supports all the mods,
        /// third is true if ccs model supports all the mods.
        /// </summary>
        /// <param name="modifiedSequence">Modified peptide sequence.</param>
        /// <param name="mods">String representation of mods contained.</param>
        /// <param name="modSites">String representation of modification sites.</param>
        /// <returns></returns>
        protected internal (bool, bool, bool) ValidateModifications(ModifiedSequence modifiedSequence, out string mods, out string modSites)
        {
            var modsBuilder = new StringBuilder();
            var modSitesBuilder = new StringBuilder();

            // The list returned below is probably always short enough that determining
            // if it contains a modification would not be greatly improved by caching a set
            // for use here instead of the list.
            var (noMs2SupportWarningMods, noRtSupportWarningMods, noCcsSupportWarningMods) = GetWarningMods();

            bool ms2SupportedMod = true;
            bool rtSupportedMod = true;
            bool ccsSupportedMod = true;

            //var setUnderSupported = new HashSet<string>();
            var setMs2Unsupported = new HashSet<string>();
            var setRtUnsupported = new HashSet<string>();
            var setCcsUnsupported = new HashSet<string>();

            for (var i = 0; i < modifiedSequence.ExplicitMods.Count; i++)
            {
                var mod = modifiedSequence.ExplicitMods[i];
                if (mod.UnimodId == null)
                {
                    if (!setMs2Unsupported.Contains(mod.Name))
                    {
                        var msg = string.Format(ModelsResources.BuildPrecursorTable_UnsupportedModification, modifiedSequence, mod.Name, ToolName);
                        Messages.WriteAsyncUserMessage(msg);
                        ms2SupportedMod = false;
                        rtSupportedMod = false;
                        ccsSupportedMod = false;
                        setMs2Unsupported.Add(mod.Name);
                    }
                    continue;
                }

                var unimodIdWithName = mod.UnimodIdWithName;
                if (noMs2SupportWarningMods.Contains(mod.Name))
                {
                    if (!setMs2Unsupported.Contains(mod.Name))
                    {
                        var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification, modifiedSequence, mod.Name, unimodIdWithName, ToolName);
                        Messages.WriteAsyncUserMessage(msg);
                        ms2SupportedMod = false;
                        rtSupportedMod = false;
                        ccsSupportedMod = false;
                        setMs2Unsupported.Add(mod.Name);
                    }

                    continue;
                }

                bool msgGenerated = false;
                unimodIdWithName = mod.UnimodIdWithName;
                if (noRtSupportWarningMods.Contains(mod.Name))
                {
                    if (!setRtUnsupported.Contains(mod.Name))
                    {
                        var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification, modifiedSequence, mod.Name, unimodIdWithName, ToolName);
                        Messages.WriteAsyncUserMessage(msg);
                        rtSupportedMod = false;
                        setRtUnsupported.Add(mod.Name);
                        msgGenerated = true;
                    }
                    
                }
                
                if (noCcsSupportWarningMods.Contains(mod.Name))
                {
                    if (!setCcsUnsupported.Contains(mod.Name))
                    {
                        if (!msgGenerated)
                        {
                            var msg = string.Format(ModelsResources.BuildPrecursorTable_Unimod_limited_Modification, modifiedSequence, mod.Name, unimodIdWithName, ToolName);
                            Messages.WriteAsyncUserMessage(msg);
                        }
                        ccsSupportedMod = false;
                        setCcsUnsupported.Add(mod.Name);
                    }
                }

                var modNames = UniModData.UNI_MOD_DATA.Where(m => unimodIdWithName.Contains(m.Name)).ToArray();
                Assume.IsTrue(modNames.Length != 0);    // The warningMods test above should guarantee the mod is supported
                string modName = GetModName(new ModificationType( modNames.Single().ID.Value, modNames.Single().Name, modNames.Single().Formula), modifiedSequence.GetUnmodifiedSequence(), mod.IndexAA);
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
            
            return (ms2SupportedMod, rtSupportedMod, ccsSupportedMod);
        }

        protected virtual string GetModName(ModificationType mod, string unmodifiedSequence, int modIndexAA)
        {
            return mod.AlphaNameWithAminoAcid(unmodifiedSequence, modIndexAA);
        }

        public abstract string GetWarning();
        
        public (IList<string>,IList<string>,IList<string>) GetWarningMods()
        {
            if (_noMs2SupportWarningMods != null && _noRtSupportWarningMods != null && _noCcsSupportWarningMods != null)
                return (_noMs2SupportWarningMods, _noRtSupportWarningMods, _noCcsSupportWarningMods);
    
            _noMs2SupportWarningMods = new List<string>();
            _noRtSupportWarningMods = new List<string>();
            _noCcsSupportWarningMods = new List<string>();

            // Build precursor table row by row
            foreach (var peptide in Document.Peptides)
            {
                var modifiedSequence = ModifiedSequence.GetModifiedSequence(Document.Settings, peptide, IsotopeLabelType.light);

                for (var i = 0; i < modifiedSequence.ExplicitMods.Count; i++)
                {
                    var mod = modifiedSequence.ExplicitMods[i];
                    if (!mod.UnimodId.HasValue)
                    {
                        var haveMod = _noMs2SupportWarningMods.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            _noMs2SupportWarningMods.Add(mod.Name);
                        }

                        haveMod = _noRtSupportWarningMods.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            _noRtSupportWarningMods.Add(mod.Name);
                        }

                        haveMod = _noCcsSupportWarningMods.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            _noCcsSupportWarningMods.Add(mod.Name);
                        }
                    }

                    var unimodId = mod.UnimodId;
                    
                    if (!libraryBuilderModificationSupport.IsMs2SupportedMod(unimodId))
                    {
                        var haveMod = _noMs2SupportWarningMods.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            _noMs2SupportWarningMods.Add(mod.Name);
                        }
                    }
                    if (!libraryBuilderModificationSupport.IsRtSupportedMod(unimodId))
                    {
                        var haveMod = _noRtSupportWarningMods.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            _noRtSupportWarningMods.Add(mod.Name);
                        }
                    }
                    if (!libraryBuilderModificationSupport.IsCcsSupportedMod(unimodId))
                    {
                        var haveMod = _noCcsSupportWarningMods.FirstOrDefault(m => m == mod.Name);
                        if (haveMod == null)
                        {
                            _noCcsSupportWarningMods.Add(mod.Name);
                        }
                    }
 
                }
            }
            
            return (_noMs2SupportWarningMods, _noRtSupportWarningMods, _noCcsSupportWarningMods);
        }
    }
}
