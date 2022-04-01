/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class PeptideDocNode : DocNodeParent
    {
        public static readonly StandardType STANDARD_TYPE_IRT = StandardType.IRT;
        public static readonly StandardType STANDARD_TYPE_QC = StandardType.QC;
        public static readonly StandardType STANDARD_TYPE_GLOBAL = StandardType.GLOBAL_STANDARD;

        public PeptideDocNode(Peptide id, ExplicitMods mods = null, ExplicitRetentionTimeInfo explicitRetentionTime = null)
            : this(id, null, mods, null, null, null, explicitRetentionTime, Annotations.EMPTY, null, new TransitionGroupDocNode[0], true)
        {
        }

        public PeptideDocNode(Peptide id, SrmSettings settings, ExplicitMods mods, ModifiedSequenceMods sourceKey, ExplicitRetentionTimeInfo explicitRetentionTime,
            TransitionGroupDocNode[] children, bool autoManageChildren)
            : this(id, settings, mods, sourceKey, null, null, explicitRetentionTime, Annotations.EMPTY, null, children, autoManageChildren)
        {
        }

        public PeptideDocNode(Peptide id,
                              SrmSettings settings,
                              ExplicitMods mods,
                              ModifiedSequenceMods sourceKey,
                              StandardType standardType,
                              int? rank,
                              ExplicitRetentionTimeInfo explicitRetentionTimeInfo,
                              Annotations annotations,
                              Results<PeptideChromInfo> results,
                              TransitionGroupDocNode[] children,
                              bool autoManageChildren)
            : base(id, annotations, children, autoManageChildren)
        {
            if (mods == null && !id.Target.IsProteomic)
            {
                mods = ExplicitMods.EMPTY; // Small molecules take label info from adducts, but a null value is problematic
            }
            ExplicitMods = mods;
            SourceKey = sourceKey;
            GlobalStandardType = standardType;
            Rank = rank;
            if (ExplicitRetentionTimeInfo.EMPTY.Equals(explicitRetentionTimeInfo))
            {
                explicitRetentionTimeInfo = null; // Users sometimes say RT=0 when they actually mean "unknown"
            }
            ExplicitRetentionTime = explicitRetentionTimeInfo;
            Results = results;
            BestResult = CalcBestResult();

            if (settings != null)
            {
                CalculateModifiedTarget(settings, out Target modifiedTarget, out string modifiedSequenceDisplay);
                ModifiedTarget = modifiedTarget;
                ModifiedSequenceDisplay = modifiedSequenceDisplay;
            }
            else
            {
                ModifiedTarget = Peptide.Target;
                ModifiedSequenceDisplay = Peptide.Target.DisplayName;
            }

            ExplicitMods?.VerifyNoLegacyData();
        }

        public override string AuditLogText
        {
            get
            {
                var label = PeptideTreeNode.GetLabel(this, string.Empty);
                return (CustomMolecule != null && !string.IsNullOrEmpty(CustomMolecule.Formula)) ? string.Format(@"{0} ({1})", label, CustomMolecule.Formula) : label;
            }
        }

        protected override IList<DocNode> OrderedChildren(IList<DocNode> children)
        {
            if (Peptide.IsCustomMolecule && children.Any())
            {
                // Enforce order for small molecules, except those that are fictions of the test system
                return children.OrderBy(t => (TransitionGroupDocNode)t, new TransitionGroupDocNode.CustomIonPrecursorComparer()).ToArray();
            }
            else
            {
                return children;
            }
        }

        public Peptide Peptide { get { return (Peptide)Id; } }

        public PeptideDocNode ChangeFastaSequence(FastaSequence newSequence)
        {
            int begin = newSequence.Sequence.IndexOf(Peptide.Target.Sequence, StringComparison.Ordinal);
            Assume.IsTrue(begin >= 0);
            int end = begin + Peptide.Target.Sequence.Length;
            var newPeptide = new Peptide(newSequence, Peptide.Target.Sequence,
                begin, end, Peptide.MissedCleavages);
            return ChangePeptide(newPeptide, TransitionGroups.Select(tg => tg.ChangePeptide(newPeptide)));
        }

        public PeptideDocNode ChangePeptide(Peptide peptide, IEnumerable<TransitionGroupDocNode> newTransitionGroups)
        {
            var node = (PeptideDocNode)ChangeId(peptide);
            node = (PeptideDocNode)node.ChangeChildren(newTransitionGroups.Cast<DocNode>().ToList());
            return node;
        }

        [TrackChildren(ignoreName:true, defaultValues: typeof(DefaultValuesNull))]
        public CustomMolecule CustomMolecule { get { return Peptide.CustomMolecule; } }

        public PeptideModKey Key { get { return new PeptideModKey(Peptide, ExplicitMods); } }

        public PeptideSequenceModKey SequenceKey { get { return new PeptideSequenceModKey(Peptide, ExplicitMods); } }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.peptide; } }

        private static IList<LoggableExplicitMod> EmptyIfDefault(ExplicitMods mods)
        {
            if (mods == null || mods.Equals(ExplicitMods.EMPTY))
                return EMPTY_LOGGABLE;

            return null;
        }

        private static LoggableExplicitMod[] EMPTY_LOGGABLE = new LoggableExplicitMod[0];
        private static LoggableExplicitModList[] EMPTY_LOGGABLE_LIST = new LoggableExplicitModList[0];

        [TrackChildren(defaultValues:typeof(DefaultValuesNullOrEmpty))]
        public IList<LoggableExplicitMod> ExplicitModsStatic
        {
            get
            {
                return EmptyIfDefault(ExplicitMods) ?? GetLoggableMods(ExplicitMods.StaticModifications);
            }
        }

        [TrackChildren(defaultValues: typeof(DefaultValuesNullOrEmpty))]
        public IList<LoggableExplicitMod> ExplicitModsHeavy
        {
            get
            {
                return EmptyIfDefault(ExplicitMods) ?? GetLoggableMods(ExplicitMods.HeavyModifications);
            }
        }

        [TrackChildren(defaultValues: typeof(DefaultValuesNullOrEmpty))]
        public IList<LoggableExplicitModList> ExplicitModsTyped
        {
            get
            {
                if (ExplicitMods == null)
                    return EMPTY_LOGGABLE_LIST;
                var labeledNonHeavyMods = ExplicitMods.GetHeavyModifications()
                    .Where(m => !ReferenceEquals(m.LabelType, IsotopeLabelType.heavy)).ToArray();
                if (labeledNonHeavyMods.Length == 0)
                    return EMPTY_LOGGABLE_LIST;

                return labeledNonHeavyMods.Select(mods =>
                        new LoggableExplicitModList(mods.LabelType, GetLoggableMods(mods.Modifications)))
                    .ToArray();
            }
        }

        private IList<LoggableExplicitMod> GetLoggableMods(IList<ExplicitMod> mods)
        {
            if (mods != null)
                return mods.Select(mod => new LoggableExplicitMod(mod, Peptide.Sequence)).ToArray();
            return EMPTY_LOGGABLE;
        }

        public ExplicitMods ExplicitMods { get; private set; }

        public CrosslinkStructure CrosslinkStructure
        {
            get { return ExplicitMods?.CrosslinkStructure ?? CrosslinkStructure.EMPTY; }
        }

        public string GetCrosslinkedSequence()
        {
            return string.Join(@"-", CrosslinkStructure.LinkedPeptides.Prepend(Peptide).Select(pep => pep.Sequence));
        }
        public ModifiedSequenceMods SourceKey { get; private set; }

        [TrackChildren(defaultValues:typeof(DefaultValuesNull))]
        public ExplicitRetentionTimeInfo ExplicitRetentionTime { get; private set; } // For transition lists with explicit values for RT

        [Track(defaultValues:typeof(DefaultValuesNull))]
        public StandardType GlobalStandardType { get; private set; }

        public Target ModifiedTarget { get; private set; }
        public string ModifiedSequence { get { return ModifiedTarget.Sequence; } }

        public string ModifiedSequenceDisplay { get; private set; }

        public Color Color { get; private set; }
        public static readonly Color UNKNOWN_COLOR = Color.FromArgb(170, 170, 170);

        public Target Target { get { return Peptide.Target; }}
        public string TextId { get { return CustomInvariantNameOrText(Peptide.Sequence); } }
        public string RawTextId { get { return CustomInvariantNameOrText(ModifiedTarget.Sequence); } }

        public string RawUnmodifiedTextId { get { return CustomInvariantNameOrText(Peptide.Sequence); }}

        public string RawTextIdDisplay { get { return CustomDisplayNameOrText(ModifiedSequenceDisplay); } }

        private string CustomDisplayNameOrText(string text)
        {
            return Peptide.IsCustomMolecule ? Peptide.CustomMolecule.DisplayName : text;
        }

        private string CustomInvariantNameOrText(string text)
        {
            return Peptide.IsCustomMolecule ? Peptide.CustomMolecule.InvariantName : text;
        }

        /// <summary>
        /// For decoy peptides, returns modified sequence of the source peptide
        /// For non-decoy peptides, returns modified sequence
        /// For non-peptides, returns the display name (Name or Formula or masses)
        /// </summary>
        public string SourceTextId { get { return SourceKey != null ? SourceKey.ModifiedSequence : RawTextId; } }
        public Target SourceModifiedTarget { get { return SourceKey != null ? new Target(SourceKey.ModifiedSequence) : ModifiedTarget; } }

        /// <summary>
        /// For decoy peptides, returns unmodified sequence of the source peptide
        /// For non-decoy peptides, returns unmodified sequence
        /// For non-peptides, returns the display name (Name or Formula or masses)
        /// </summary>
        public string SourceUnmodifiedTextId { get { return SourceKey != null ? SourceKey.Sequence : RawUnmodifiedTextId; } }

        public Target SourceUnmodifiedTarget { get { return SourceKey != null ? new Target(SourceKey.Sequence) : Target; } }

        /// <summary>
        /// Explicit modifications for this peptide or a source peptide for a decoy
        /// Combined with SourceUnmodifiedTextId to form a unique key
        /// </summary>
        public ExplicitMods SourceExplicitMods { get { return SourceKey != null ? SourceKey.ExplicitMods : ExplicitMods; } }

        public bool HasExplicitMods { get { return !ExplicitMods.IsNullOrEmpty(ExplicitMods); } } // Small molecules will have a dummy ExplicitMods but never use it

        public bool HasVariableMods { get { return HasExplicitMods && ExplicitMods.IsVariableStaticMods; } }

        public bool IsProteomic { get { return !Peptide.IsCustomMolecule; } }

        public bool AreVariableModsPossible(int maxVariableMods, IList<StaticMod> modsVarAvailable)
        {
            if (HasVariableMods)
            {
                var explicitModsVar = ExplicitMods.StaticModifications;
                if (explicitModsVar.Count > maxVariableMods)
                    return false;
                foreach (var explicitMod in explicitModsVar)
                {
                    if (!modsVarAvailable.Contains(explicitMod.Modification))
                        return false;
                }
            }
            return true;
        }

        public bool CanHaveImplicitStaticMods
        {
            get
            {
                return !HasExplicitMods ||
                       !ExplicitMods.IsModified(IsotopeLabelType.light) ||
                       ExplicitMods.IsVariableStaticMods;
            }
        }

        public bool CanHaveImplicitHeavyMods(IsotopeLabelType labelType)
        {
            return !HasExplicitMods || !ExplicitMods.IsModified(labelType);
        }

        public bool HasChildType(IsotopeLabelType labelType)
        {
            return Children.Contains(nodeGroup => ReferenceEquals(labelType,
                                                                  ((TransitionGroupDocNode)nodeGroup).TransitionGroup.LabelType));
        }

        public bool HasChildCharge(Adduct charge)
        {
            return Children.Contains(nodeGroup => Equals(charge,
                                                         ((TransitionGroupDocNode) nodeGroup).TransitionGroup.PrecursorAdduct));
        }

        public int? Rank { get; private set; }
        public bool IsDecoy { get { return Peptide.IsDecoy; } }

        public Results<PeptideChromInfo> Results { get; private set; }

        public bool HasResults { get { return Results != null; } }

        public ChromInfoList<PeptideChromInfo> GetSafeChromInfo(int i)
        {
            return HasResults && Results.Count > i ? Results[i] : default(ChromInfoList<PeptideChromInfo>);
        }

        public float GetRankValue(PeptideRankId rankId)
        {
            float value = Single.MinValue;
            foreach (TransitionGroupDocNode nodeGroup in Children)
                value = Math.Max(value, nodeGroup.GetRankValue(rankId));
            return value;
        }

        public float? GetPeakCountRatio(int i)
        {
            if (i == -1)
                return AveragePeakCountRatio;

            var result = GetSafeChromInfo(i);
            if (result.IsEmpty)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.PeakCountRatio);
        }

        public float? AveragePeakCountRatio
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.PeakCountRatio);
            }
        }

        public float? GetSchedulingTime(int i)
        {
            return GetMeasuredRetentionTime(i);
        }

        public float? SchedulingTime
        {
            get { return AverageMeasuredRetentionTime; }
        }

        public float? GetSchedulingTime(ChromFileInfoId fileId)
        {
            return GetMeasuredRetentionTime(fileId);
        }

        public float? GetMeasuredRetentionTime(int i)
        {
            if (i == -1)
                return AverageMeasuredRetentionTime;

            var result = GetSafeChromInfo(i);
            if (result.IsEmpty)
                return null;
            return result.GetAverageValue(chromInfo => chromInfo.RetentionTime.HasValue
                                             ? chromInfo.RetentionTime.Value
                                             : (float?)null);
        }

        public float? AverageMeasuredRetentionTime
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.RetentionTime.HasValue
                                             ? chromInfo.RetentionTime.Value
                                             : (float?)null);
            }
        }

        public float? PercentileMeasuredRetentionTime
        {
            get
            {
                if (Results == null)
                    return null;
                var statTimes = new Statistics(
                    from result in Results
                    from chromInfo in result.Where(chromInfo => !Equals(chromInfo, default(PeptideChromInfo)))
                    where chromInfo.RetentionTime.HasValue
                    select (double)chromInfo.RetentionTime.Value);
                return statTimes.Length > 0
                    ? (float?)statTimes.Percentile(IrtStandard.GetSpectrumTimePercentile(ModifiedTarget))
                    : null;
            }
        }

        public float? GetMeasuredRetentionTime(ChromFileInfoId fileId)
        {
            double totalTime = 0;
            int countTime = 0;
            foreach (var chromInfo in TransitionGroups.SelectMany(nodeGroup => nodeGroup.ChromInfos))
            {
                if (fileId != null && !ReferenceEquals(fileId, chromInfo.FileId))
                    continue;
                float? retentionTime = chromInfo.RetentionTime;
                if (!retentionTime.HasValue)
                    continue;

                totalTime += retentionTime.Value;
                countTime++;
            }
            if (countTime == 0)
                return null;
            return (float)(totalTime / countTime);
        }

        public float? GetPeakCenterTime(int i)
        {
            if (i == -1)
                return AveragePeakCenterTime;

            double totalTime = 0;
            int countTime = 0;
            foreach (var nodeGroup in TransitionGroups)
            {
                if (!nodeGroup.HasResults)
                    continue;
                var result = nodeGroup.Results[i];
                if (result.IsEmpty)
                    continue;

                foreach (var chromInfo in result)
                {
                    float? centerTime = GetPeakCenterTime(chromInfo);
                    if (!centerTime.HasValue)
                        continue;

                    totalTime += centerTime.Value;
                    countTime++;
                }
            }
            if (countTime == 0)
                return null;
            return (float)(totalTime / countTime);
        }

        public float? AveragePeakCenterTime
        {
            get { return GetPeakCenterTime((ChromFileInfoId) null); }
        }

        public float? GetPeakCenterTime(ChromFileInfoId fileId)
        {
            double totalTime = 0;
            int countTime = 0;
            foreach (var chromInfo in TransitionGroups.SelectMany(nodeGroup => nodeGroup.ChromInfos))
            {
                if (fileId != null && !ReferenceEquals(fileId, chromInfo.FileId))
                    continue;
                float? centerTime = GetPeakCenterTime(chromInfo);
                if (!centerTime.HasValue)
                    continue;

                totalTime += centerTime.Value;
                countTime++;
            }
            if (countTime == 0)
                return null;
            return (float)(totalTime / countTime);
        }

        private float? GetPeakCenterTime(TransitionGroupChromInfo chromInfo)
        {
            if (!chromInfo.StartRetentionTime.HasValue || !chromInfo.EndRetentionTime.HasValue)
                return null;

            return (chromInfo.StartRetentionTime.Value + chromInfo.EndRetentionTime.Value) / 2;
        }

        private float? GetAverageResultValue(Func<PeptideChromInfo, float?> getVal)
        {
            return HasResults ? Results.GetAverageValue(getVal) : null;
        }

        public int BestResult { get; private set; }

        /// <summary>
        /// Returns the index of the "best" result for a peptide.  This is currently
        /// base solely on total peak area, could be enhanced in the future to be
        /// more like picking the best peak in the import code, including factors
        /// such as peak-found-ratio and dot-product.
        /// </summary>
        private int CalcBestResult()
        {
            if (!HasResults)
                return -1;

            int iBest = -1;
            double? bestZScore = null;
            double bestArea = double.MinValue;
            for (int i = 0; i < Results.Count; i++)
            {
                var i1 = i;
                var zScores = Children.Cast<TransitionGroupDocNode>()
                    .Where(nodeTranGroup => nodeTranGroup.HasResults)
                    .SelectMany(nodeTranGroup => nodeTranGroup.Results[i1])
                    .Where(ci => ci.ZScore.HasValue)
                    .Select(ci => ci.ZScore.Value).ToArray();
                var zScore = zScores.Length > 0 ? (float?) zScores.Max() : null;

                if (zScore.HasValue)
                {
                    if (!bestZScore.HasValue || zScore.Value > bestZScore.Value)
                    {
                        iBest = i;
                        bestZScore = zScore.Value;
                    }
                    continue;
                }
                else if (bestZScore.HasValue)
                {
                    continue;
                }

                double maxScore = 0;
                foreach (TransitionGroupDocNode nodeGroup in Children)
                {

                    double groupArea = 0;
                    double groupTranMeasured = 0;
                    bool isGroupIdentified = false;
                    foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                    {
                        if (!nodeTran.HasResults)
                            continue;
                        var result = nodeTran.Results[i];
                        int resultCount = result.Count;
                        if (resultCount == 0)
                            continue;
                        // Use average area over all files in a replicate to avoid
                        // counting a replicate as best, simply because it has more
                        // measurements.  Most of the time there should only be one
                        // file per precursor per replicate.
                        double tranArea = 0;
                        double tranMeasured = 0;
                        for (int iChromInfo = 0; iChromInfo < resultCount; iChromInfo++)
                        {
                            var chromInfo = result[iChromInfo];
                            if (chromInfo.Area > 0)
                            {
                                tranArea += chromInfo.Area;
                                tranMeasured++;

                                isGroupIdentified = isGroupIdentified || chromInfo.IsIdentified;
                            }
                        }
                        groupArea += tranArea/resultCount;
                        groupTranMeasured += tranMeasured/resultCount;
                    }

                    maxScore = Math.Max(maxScore, 
                        ChromDataPeakList.ScorePeak(groupArea, LegacyCountScoreCalc.GetPeakCountScore(groupTranMeasured, nodeGroup.Children.Count), isGroupIdentified));
                }
                if (maxScore > bestArea)
                {
                    iBest = i;
                    bestArea = maxScore;
                }
            }
            return iBest;            
        }

        public double? InternalStandardConcentration { get; private set; }
        public double? ConcentrationMultiplier { get; private set; }

        public NormalizationMethod NormalizationMethod { get; private set; }

        public string AttributeGroupId { get; private set; }

        #region Property change methods

        private PeptideDocNode UpdateModifiedSequence(SrmSettings settingsNew)
        {
            if (!IsProteomic)
                return this; // Settings have no effect on custom ions

            CalculateModifiedTarget(settingsNew, out Target modifiedTarget, out string modifiedSequenceDisplay);
            if (Equals(modifiedTarget, ModifiedTarget) &&
                String.Equals(modifiedSequenceDisplay, ModifiedSequenceDisplay))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im =>
                {
                    im.ModifiedTarget = modifiedTarget;
                    im.ModifiedSequenceDisplay = modifiedSequenceDisplay;
                });
        }

        private void CalculateModifiedTarget(SrmSettings srmSettings, out Target modifiedTarget,
            out string modifiedSequenceDisplay)
        {
            if (ExplicitMods == null || !ExplicitMods.HasCrosslinks)
            {
                var calcPre = srmSettings.GetPrecursorCalc(IsotopeLabelType.light, ExplicitMods);

                modifiedTarget =
                    calcPre.GetModifiedSequence(Peptide.Target, SequenceModFormatType.full_precision, false);
                modifiedSequenceDisplay = calcPre.GetModifiedSequence(Peptide.Target, true).DisplayName;
            }
            else
            {
                modifiedTarget = srmSettings.GetCrosslinkModifiedSequence(Peptide.Target, IsotopeLabelType.light, ExplicitMods);
                modifiedSequenceDisplay = modifiedTarget.ToString();
            }
        }

        public PeptideDocNode ChangeExplicitMods(ExplicitMods prop)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitMods = prop);
        }

        public PeptideDocNode ChangeSourceKey(ModifiedSequenceMods prop)
        {
            return ChangeProp(ImClone(this), im => im.SourceKey = prop);
        }

        public PeptideDocNode ChangeStandardType(StandardType prop)
        {
            return ChangeProp(ImClone(this), im => im.GlobalStandardType = prop);
        }

        public PeptideDocNode ChangeRank(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.Rank = prop);
        }

        public PeptideDocNode ChangeColor(Color prop)
        {
            return ChangeProp(ImClone(this), im => im.Color = prop);
        }

        public PeptideDocNode ChangeResults(Results<PeptideChromInfo> prop)
        {
            return ChangeProp(ImClone(this), im =>
                                                 {
                                                     im.Results = prop;
                                                     im.BestResult = im.CalcBestResult();
                                                 });
        }

        public PeptideDocNode ChangeExplicitRetentionTime(ExplicitRetentionTimeInfo prop)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitRetentionTime = prop);
        }

        public PeptideDocNode ChangeExplicitRetentionTime(double? prop)
        {
            double? oldwindow = null;
            if (ExplicitRetentionTime != null)
                oldwindow = ExplicitRetentionTime.RetentionTimeWindow;
            return ChangeProp(ImClone(this), im => im.ExplicitRetentionTime = prop.HasValue ? new ExplicitRetentionTimeInfo(prop.Value, oldwindow) : null);
        }

        // Note: this potentially returns a node with a different ID, which has to be Inserted rather than Replaced
        public PeptideDocNode ChangeCustomIonValues(SrmSettings settings, CustomMolecule customMolecule, ExplicitRetentionTimeInfo explicitRetentionTime)
        {
            var newPeptide = new Peptide(customMolecule);
            Helpers.AssignIfEquals(ref newPeptide, Peptide);
            if (Equals(Peptide, newPeptide))
            {
                return Equals(ExplicitRetentionTime, explicitRetentionTime) ? this : ChangeExplicitRetentionTime(explicitRetentionTime);
            }
            else
            {
                // ID Changes impact all children, because IDs have back pointers to their parents
                var children = new List<TransitionGroupDocNode>();
                foreach (var nodeGroup in TransitionGroups)
                {
                    children.Add(nodeGroup.UpdateSmallMoleculeTransitionGroup(newPeptide, null, settings));
                }
                return new PeptideDocNode(newPeptide, settings, ExplicitMods, SourceKey, explicitRetentionTime, children.ToArray(), AutoManageChildren);
            }
        }

        public PeptideDocNode ChangeInternalStandardConcentration(double? internalStandardConcentration)
        {
            return ChangeProp(ImClone(this), im => im.InternalStandardConcentration = internalStandardConcentration);
        }

        public PeptideDocNode ChangeConcentrationMultiplier(double? concentrationMultiplier)
        {
            return ChangeProp(ImClone(this), im => im.ConcentrationMultiplier = concentrationMultiplier);
        }

        public PeptideDocNode ChangeNormalizationMethod(NormalizationMethod normalizationMethod)
        {
            return ChangeProp(ImClone(this), im => im.NormalizationMethod = normalizationMethod);
        }

        public PeptideDocNode ChangeAttributeGroupId(string attributeGroupId)
        {
            return ChangeProp(ImClone(this),
                im => im.AttributeGroupId = string.IsNullOrEmpty(attributeGroupId) ? null : attributeGroupId);
        }

        #endregion

        /// <summary>
        /// Node level depths below this node
        /// </summary>
// ReSharper disable InconsistentNaming
        public enum Level { TransitionGroups, Transitions }
// ReSharper restore InconsistentNaming

        public int TransitionGroupCount { get { return GetCount((int)Level.TransitionGroups); } }
        public int TransitionCount { get { return GetCount((int)Level.Transitions); } }

        [TrackChildren(ignoreName: true, defaultValues: typeof(DefaultValuesNullOrEmpty))]
        public IEnumerable<TransitionGroupDocNode> TransitionGroups
        {
            get
            {
                return Children.Cast<TransitionGroupDocNode>();
            }
        }

        public bool HasHeavyTransitionGroups
        {
            get
            {
                return TransitionGroups.Contains(nodeGroup => !nodeGroup.TransitionGroup.LabelType.IsLight);
            }
        }

        public bool HasLibInfo
        {
            get
            {
                return TransitionGroups.Contains(nodeGroup => nodeGroup.HasLibInfo);
            }
        }

        public bool IsUserModified
        {
            get
            {
                if (!Annotations.IsEmpty)
                    return true;
                return TransitionGroups.Contains(nodeGroup => nodeGroup.IsUserModified);
            }
        }

        /// <summary>
        /// Given a <see cref="TransitionGroupDocNode"/> returns a <see cref="TransitionGroupDocNode"/> for which
        /// transition rankings based on imported results should be used for determining primary transitions
        /// in triggered-MRM (iSRM).  This ensures that light and isotope labeled precursors with the same
        /// transitions use the same ranking, and that only one isotope label type need be measured to
        /// produce a method for a document with light-heavy pairs.
        /// </summary>
        public TransitionGroupDocNode GetPrimaryResultsGroup(TransitionGroupDocNode nodeGroup)
        {
            TransitionGroupDocNode nodeGroupPrimary = nodeGroup;
            if (TransitionGroupCount > 1)
            {
                double maxArea = nodeGroup.AveragePeakArea ?? 0;
                var precursorCharge = nodeGroup.TransitionGroup.PrecursorAdduct;
                foreach (var nodeGroupChild in TransitionGroups.Where(g =>
                        g.TransitionGroup.PrecursorAdduct.Equals(precursorCharge) &&
                        !ReferenceEquals(g, nodeGroup)))
                {
                    // Only when children match can one precursor provide primary values for another
                    if (!nodeGroup.EquivalentChildren(nodeGroupChild))
                        continue;

                    float peakArea = nodeGroupChild.AveragePeakArea ?? 0;
                    if (peakArea > maxArea)
                    {
                        maxArea = peakArea;
                        nodeGroupPrimary = nodeGroupChild;
                    }
                }
            }
            return nodeGroupPrimary;
        }

        public bool CanTrigger(int? replicateIndex)
        {
            foreach (var nodeGroup in TransitionGroups)
            {
                var nodeGroupPrimary = GetPrimaryResultsGroup(nodeGroup);
                // Return false, if any primary group lacks the ranking information necessary for tMRM/iSRM
                if (!nodeGroupPrimary.HasReplicateRanks(replicateIndex) && !nodeGroupPrimary.HasLibRanks)
                    return false;
            }
            return true;
        }

        public PeptideDocNode Merge(PeptideDocNode nodePepMerge)
        {
            return Merge(nodePepMerge, (n, nMerge) => n.Merge(nMerge));
        }

        public PeptideDocNode Merge(PeptideDocNode nodePepMerge,
            Func<TransitionGroupDocNode, TransitionGroupDocNode, TransitionGroupDocNode> mergeMatch)
        {
            var childrenNew = Children.Cast<TransitionGroupDocNode>().ToList();
            // Remember where all the existing children are
            var dictPepIndex = new Dictionary<TransitionGroup, int>();
            for (int i = 0; i < childrenNew.Count; i++)
            {
                var key = childrenNew[i].TransitionGroup;
                if (!dictPepIndex.ContainsKey(key))
                    dictPepIndex[key] = i;
            }
            // Add the new children to the end, or merge when the node is already present
            foreach (TransitionGroupDocNode nodeGroup in nodePepMerge.Children)
            {
                int i;
                if (!dictPepIndex.TryGetValue(nodeGroup.TransitionGroup, out i))
                    childrenNew.Add(nodeGroup);
                else if (mergeMatch != null)
                    childrenNew[i] = mergeMatch(childrenNew[i], nodeGroup);
            }
            childrenNew.Sort(Peptide.CompareGroups);
            return (PeptideDocNode)ChangeChildrenChecked(childrenNew.Cast<DocNode>().ToArray());
        }

        public PeptideDocNode MergeUserInfo(PeptideDocNode nodePepMerge, SrmSettings settings, SrmSettingsDiff diff)
        {
            var result = Merge(nodePepMerge, (n, nMerge) => n.MergeUserInfo(nodePepMerge, nMerge, settings, diff));
            var annotations = Annotations.Merge(nodePepMerge.Annotations);
            if (!ReferenceEquals(annotations, Annotations))
                result = (PeptideDocNode) result.ChangeAnnotations(annotations);
            return result.UpdateResults(settings);
        }

        public PeptideDocNode ChangeSettings(SrmSettings settingsNew, SrmSettingsDiff diff, bool recurse = true)
        {
            if (diff.Monitor != null)
                diff.Monitor.ProcessMolecule(this);

            // If the peptide has explicit modifications, and the modifications have
            // changed, see if any of the explicit modifications have changed
            var explicitMods = ExplicitMods;
            if (HasExplicitMods &&
                !diff.IsUnexplainedExplicitModificationAllowed &&
                diff.SettingsOld != null &&
                !ReferenceEquals(settingsNew.PeptideSettings.Modifications,
                                 diff.SettingsOld.PeptideSettings.Modifications))
            {
                explicitMods = ExplicitMods.ChangeGlobalMods(settingsNew);
                if (explicitMods == null || !ArrayUtil.ReferencesEqual(explicitMods.GetHeavyModifications().ToArray(),
                                                                       ExplicitMods.GetHeavyModifications().ToArray()))
                {
                    diff = new SrmSettingsDiff(diff, SrmSettingsDiff.ALL);                    
                }
                else if (!ReferenceEquals(explicitMods.StaticModifications, ExplicitMods.StaticModifications))
                {
                    diff = new SrmSettingsDiff(diff, SrmSettingsDiff.PROPS);
                }
            }

            TransitionSettings transitionSettings = settingsNew.TransitionSettings;
            PeptideDocNode nodeResult = this;
            if (!ReferenceEquals(explicitMods, ExplicitMods))
                nodeResult = nodeResult.ChangeExplicitMods(explicitMods);
            nodeResult = nodeResult.UpdateModifiedSequence(settingsNew);

            if (diff.DiffPeptideProps)
            {
                var rt = settingsNew.PeptideSettings.Prediction.RetentionTime;
                bool isStandard = Equals(nodeResult.GlobalStandardType, STANDARD_TYPE_IRT);
                if (rt != null)
                {
                    bool isStandardNew = rt.IsStandardPeptide(nodeResult);
                    if (isStandard ^ isStandardNew)
                        nodeResult = nodeResult.ChangeStandardType(isStandardNew ? STANDARD_TYPE_IRT : null);
                }
                else if (isStandard)
                {
                    nodeResult = nodeResult.ChangeStandardType(null);
                }
            }

            if (diff.DiffTransitionGroups && settingsNew.TransitionSettings.Filter.AutoSelect && AutoManageChildren)
            {
                IList<DocNode> childrenNew = new List<DocNode>();

                PeptideRankId rankId = settingsNew.PeptideSettings.Libraries.RankId;
                bool useHighestRank = (rankId != null && settingsNew.PeptideSettings.Libraries.PeptideCount.HasValue);
                bool isPickedIntensityRank = useHighestRank &&
                                             ReferenceEquals(rankId, LibrarySpec.PEP_RANK_PICKED_INTENSITY);

                IEqualityComparer<TransitionGroup> transitionGroupEqualityComparer = null;
                if (!IsProteomic)
                {
                    transitionGroupEqualityComparer = new IdentityEqualityComparer<TransitionGroup>();
                }

                ILookup<TransitionGroup, TransitionGroupDocNode> mapIdToChild =
                    TransitionGroups.ToLookup(nodeGroup => nodeGroup.TransitionGroup, transitionGroupEqualityComparer);
                foreach (TransitionGroup tranGroup in GetTransitionGroups(settingsNew, explicitMods, true)
                    .Distinct(transitionGroupEqualityComparer))
                {
                    IList<TransitionGroupDocNode> nodeGroups;
                    SrmSettingsDiff diffNode = diff;

                    // Add values that existed before the change, unless using picked intensity ranking,
                    // since this could bias the ranking, otherwise.
                    if (!isPickedIntensityRank && mapIdToChild.Contains(tranGroup))
                    {
                        nodeGroups = mapIdToChild[tranGroup].ToArray();
                    }
                    // Add new node
                    else
                    {
                        TransitionDocNode[] transitions = !isPickedIntensityRank
                            ? GetMatchingTransitions(tranGroup, settingsNew, explicitMods)
                            : null;

                        nodeGroups = ImmutableList.Singleton(new TransitionGroupDocNode(tranGroup, transitions));
                        // If not recursing, then ChangeSettings will not be called on nodeGroup.  So, make
                        // sure its precursor m/z is set correctly.
                        if (!recurse)
                        {
                            nodeGroups = ImmutableList.ValueOf(nodeGroups.Select(nodeGroup =>
                                nodeGroup.ChangePrecursorMz(settingsNew, explicitMods)));
                        }
                        diffNode = SrmSettingsDiff.ALL;
                    }

                    if (nodeGroups != null)
                    {
                        if (recurse)
                        {
                            nodeGroups = nodeGroups.Select(nodeGroup =>
                                nodeGroup.ChangeSettings(settingsNew, nodeResult, explicitMods, diffNode)).ToList();
                        }
                        foreach (var nodeGroup in nodeGroups)
                        {
                            if (settingsNew.TransitionSettings.Libraries.HasMinIonCount(nodeGroup) && transitionSettings.IsMeasurablePrecursor(nodeGroup.PrecursorMz))
                            {
                                childrenNew.Add(nodeGroup);
                            }
                        }
                    }
                }

                // If only using rank limited peptides, then choose only the single
                // highest ranked precursor charge.
                if (useHighestRank)
                {
                    childrenNew = FilterHighestRank(childrenNew, rankId);

                    // If using picked intensity, make sure original nodes are replaced
                    if (isPickedIntensityRank)
                    {
                        for (int i = 0; i < childrenNew.Count; i++)
                        {
                            var nodeNew = (TransitionGroupDocNode) childrenNew[i];
                            IList<TransitionGroupDocNode> existing = mapIdToChild[nodeNew.TransitionGroup].ToList();
                            if (existing.Count == 1)
                            {
                                childrenNew[i] = existing.First()
                                    .ChangeSettings(settingsNew, nodeResult, explicitMods, diff);
                            }
                        }
                    }
                }

                nodeResult = (PeptideDocNode) nodeResult.ChangeChildrenChecked(childrenNew);                
            }
            else
            {
                // Even with auto-select off, transition groups for which there is
                // no longer a precursor calculator must be removed.
                if (diff.DiffTransitionGroups && nodeResult.HasHeavyTransitionGroups)
                {
                    IList<DocNode> childrenNew = new List<DocNode>();
                    foreach (TransitionGroupDocNode nodeGroup in nodeResult.Children)
                    {
                        if (settingsNew.SupportsPrecursor(nodeGroup, explicitMods))
                        {
                            childrenNew.Add(nodeGroup);
                        }
                    }

                    nodeResult = (PeptideDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                }

                // Update properties and children, if necessary
                if (diff.DiffTransitionGroupProps ||
                    diff.DiffTransitions || diff.DiffTransitionProps ||
                    diff.DiffResults)
                {
                    IList<DocNode> childrenNew = new List<DocNode>(nodeResult.Children.Count);

                    // Enumerate the nodes making necessary changes.
                    foreach (TransitionGroupDocNode nodeGroup in nodeResult.Children)
                    {
                        TransitionGroupDocNode nodeChanged = nodeGroup.ChangeSettings(settingsNew, nodeResult, explicitMods, diff);
                        // Skip if the node can no longer be measured on the target instrument
                        if (!transitionSettings.IsMeasurablePrecursor(nodeChanged.PrecursorMz))
                            continue;
                        // Skip this node, if it is heavy and the update caused it to have the
                        // same m/z value as the light value.
                        if (!nodeChanged.TransitionGroup.LabelType.IsLight &&
                            !Peptide.IsCustomMolecule) // No mods on customs (TODO bspratt - not sure this holds true any longer)
                        {
                            var precursorMassLight = settingsNew.GetPrecursorMass(
                                IsotopeLabelType.light, Peptide.Target, explicitMods);
                            double precursorMzLight = SequenceMassCalc.GetMZ(precursorMassLight,
                                                                             nodeChanged.TransitionGroup.PrecursorCharge);
                            if (nodeChanged.PrecursorMz == precursorMzLight)
                                continue;
                        }

                        childrenNew.Add(nodeChanged);
                    }

                    nodeResult = (PeptideDocNode)nodeResult.ChangeChildrenChecked(childrenNew);
                }                
            }

            if (diff.DiffResults || ChangedResults(nodeResult))
                nodeResult = nodeResult.UpdateResults(settingsNew /*, diff*/);

            return nodeResult;
        }

        public IEnumerable<TransitionGroup> GetTransitionGroups(SrmSettings settings, ExplicitMods explicitMods, bool useFilter)
        {
            return Peptide.GetTransitionGroups(settings, this, explicitMods, useFilter);
        }

        /// <summary>
        /// Make sure children are preserved as much as possible.  It may not be possible
        /// to always preserve children, because the settings in the target document may
        /// not allow certain states (e.g. label types that to not exist in the target).
        /// </summary>
        public PeptideDocNode EnsureChildren(SrmSettings settings, bool peptideList)
        {
            var result = this;

            // Make a first attempt at changing to the new settings to figure out
            // whether this will change the children.
            var changed = result.ChangeSettings(settings, SrmSettingsDiff.ALL);
            // If the children are auto-managed, and they changed, figure out whether turning off
            // auto-manage will allow the children to be preserved better.
            if (result.AutoManageChildren && !AreEquivalentChildren(result.Children, changed.Children))
            {
                // Turn off auto-manage and change again.
                var resultAutoManageFalse = (PeptideDocNode)result.ChangeAutoManageChildren(false);
                var changedAutoManageFalse = resultAutoManageFalse.ChangeSettings(settings, SrmSettingsDiff.ALL);
                // If the children are not the same as they were with auto-manage on, then use
                // a the version of this node with auto-manage turned off.
                if (!AreEquivalentChildren(changed.Children, changedAutoManageFalse.Children))
                {
                    result = resultAutoManageFalse;
                    changed = changedAutoManageFalse;
                }
            }
            // If this is being added to a peptide list, but was a FASTA sequence,
            // make sure the Peptide ID no longer points to the old FASTA sequence.
            if (peptideList && Peptide.FastaSequence != null)
            {
                result = new PeptideDocNode(new Peptide(null, Peptide.Target.Sequence, null, null, Peptide.MissedCleavages), settings,
                                            result.ExplicitMods, result.SourceKey, result.ExplicitRetentionTime, new TransitionGroupDocNode[0], result.AutoManageChildren); 
            }
            // Create a new child list, using existing children where GlobalIndexes match.
            var dictIndexToChild = Children.ToDictionary(child => child.Id.GlobalIndex);
            var listChildren = new List<DocNode>();
            foreach (TransitionGroupDocNode nodePep in changed.Children)
            {
                DocNode child;
                if (dictIndexToChild.TryGetValue(nodePep.Id.GlobalIndex, out child))
                {
                    listChildren.Add(((TransitionGroupDocNode)child).EnsureChildren(result, ExplicitMods, settings));
                }
            }
            return (PeptideDocNode)result.ChangeChildrenChecked(listChildren);
        }

        public static bool AreEquivalentChildren(IList<DocNode> children1, IList<DocNode> children2)
        {
            if(children1.Count != children2.Count)
                return false;
            for (int i = 0; i < children1.Count; i++)
            {
                if(!Equals(children1[i].Id, children2[i].Id))
                    return false;
            }
            return true;
        }

        public PeptideDocNode EnsureMods(PeptideModifications source, PeptideModifications target,
                                         MappedList<string, StaticMod> defSetStat, MappedList<string, StaticMod> defSetHeavy)
        {
            // Create explicit mods matching the implicit mods on this peptide for each document.
            var sourceImplicitMods = new ExplicitMods(this, source.StaticModifications, defSetStat, source.GetHeavyModifications(), defSetHeavy);
            var targetImplicitMods = new ExplicitMods(this, target.StaticModifications, defSetStat, target.GetHeavyModifications(), defSetHeavy);
            
            // If modifications match, no need to create explicit modifications for the peptide.
            if (sourceImplicitMods.Equals(targetImplicitMods))
                return this;

            // Add explicit mods if static mods not implicit in the target document.
            IList<ExplicitMod> newExplicitStaticMods = null;
            bool preserveVariable = HasVariableMods;
            // Preserve non-variable explicit modifications
            if (!preserveVariable && HasExplicitMods && ExplicitMods.StaticModifications != null)
            {
                // If they are not the same as the implicit modifications in the new document
                if (!ArrayUtil.EqualsDeep(ExplicitMods.StaticModifications, targetImplicitMods.StaticModifications))
                    newExplicitStaticMods = ExplicitMods.StaticModifications;
            }
            else if (!ArrayUtil.EqualsDeep(sourceImplicitMods.StaticModifications, targetImplicitMods.StaticModifications))
            {
                preserveVariable = false;
                newExplicitStaticMods = sourceImplicitMods.StaticModifications;
            }
            else if (preserveVariable)
            {
                newExplicitStaticMods = ExplicitMods.StaticModifications;
            }
                
            // Drop explicit mods if matching implicit mods are found in the target document.
            IList<TypedExplicitModifications> newExplicitHeavyMods = new List<TypedExplicitModifications>();
            // For each heavy label type, add explicit mods if static mods not found in the target document.
            var newTypedStaticMods = newExplicitStaticMods != null
                ? new TypedExplicitModifications(Peptide, IsotopeLabelType.light, newExplicitStaticMods)
                : null;
            foreach (TypedExplicitModifications targetDocMod in targetImplicitMods.GetHeavyModifications())
            {
                // Use explicit modifications when available.  Otherwise, compare against new implicit modifications
                IList<ExplicitMod> heavyMods = (HasExplicitMods ? ExplicitMods.GetModifications(targetDocMod.LabelType) : null) ??
                    sourceImplicitMods.GetModifications(targetDocMod.LabelType);
                if (heavyMods != null && !ArrayUtil.EqualsDeep(heavyMods, targetDocMod.Modifications) && heavyMods.Count > 0)
                {
                    var newTypedHeavyMods = new TypedExplicitModifications(Peptide, targetDocMod.LabelType, heavyMods);
                    newTypedHeavyMods = newTypedHeavyMods.AddModMasses(newTypedStaticMods);
                    newExplicitHeavyMods.Add(newTypedHeavyMods);
                }
            }

            if (newExplicitStaticMods != null || newExplicitHeavyMods.Count > 0 || !CrosslinkStructure.IsEmpty)
                return ChangeExplicitMods(new ExplicitMods(Peptide, newExplicitStaticMods, newExplicitHeavyMods, preserveVariable).ChangeCrosslinkStructure(CrosslinkStructure));
            return ChangeExplicitMods(null);
        }

        private static IList<DocNode> FilterHighestRank(IList<DocNode> childrenNew, PeptideRankId rankId)
        {
            if (childrenNew.Count < 2)
                return childrenNew;
            var maxCharge = Adduct.EMPTY;
            float maxValue = Single.MinValue;
            foreach (TransitionGroupDocNode nodeGroup in childrenNew)
            {
                float rankValue = nodeGroup.GetRankValue(rankId);
                if (rankValue > maxValue)
                {
                    maxCharge = nodeGroup.TransitionGroup.PrecursorAdduct;
                    maxValue = rankValue;
                }
            }
            var listHighestRankChildren = new List<DocNode>();
            foreach (TransitionGroupDocNode nodeGroup in childrenNew)
            {
                if (nodeGroup.TransitionGroup.PrecursorAdduct.Equals(maxCharge))
                    listHighestRankChildren.Add(nodeGroup);
            }
            return listHighestRankChildren;
        }

        public TransitionDocNode[] GetMatchingTransitions(TransitionGroup tranGroup, SrmSettings settings, ExplicitMods explicitMods)
        {
            int iMatch = Children.IndexOf(nodeGroup =>
                                          ((TransitionGroupDocNode)nodeGroup).TransitionGroup.PrecursorAdduct == tranGroup.PrecursorAdduct);
            if (iMatch == -1)
                return null;
            TransitionGroupDocNode nodeGroupMatching = (TransitionGroupDocNode) Children[iMatch];
            // If the matching node is auto-managed, and auto-select is on in the settings,
            // then returning no transitions should allow transitions to be chosen correctly
            // automatically.
            if (nodeGroupMatching.AutoManageChildren && settings.TransitionSettings.Filter.AutoSelect &&
                // Having disconnected libraries can mess up automatic picking
                    settings.PeptideSettings.Libraries.DisconnectedLibraries == null)
                return null;

            return tranGroup.GetMatchingTransitions(settings, nodeGroupMatching, explicitMods);
        }

        private PeptideDocNode UpdateResults(SrmSettings settingsNew /*, SrmSettingsDiff diff*/)
        {
            // First check whether any child results are present
            if (!settingsNew.HasResults || Children.Count == 0)
            {
                if (!HasResults)
                    return this;
                return ChangeResults(null);
            }
            else if (!settingsNew.MeasuredResults.Chromatograms.Any(c => c.IsLoaded) &&
                     (!HasResults || Results.All(r => r.IsEmpty)))
            {
                if (HasResults && Results.Count == settingsNew.MeasuredResults.Chromatograms.Count)
                    return this;
                return ChangeResults(settingsNew.MeasuredResults.EmptyPeptideResults);
            }

            var transitionGroupKeys = new HashSet<Tuple<IsotopeLabelType, Adduct>>();
            // Update the results summary
            var resultsCalc = new PeptideResultsCalculator(settingsNew, NormalizationMethod);
            foreach (TransitionGroupDocNode nodeGroup in Children)
            {
                var transitionGroupKey =
                    Tuple.Create(nodeGroup.LabelType, nodeGroup.TransitionGroup.PrecursorAdduct.Unlabeled);
                if (!transitionGroupKeys.Add(transitionGroupKey))
                {
                    continue;
                }
                resultsCalc.AddGroupChromInfo(nodeGroup);
            }

            return resultsCalc.UpdateResults(this);
        }

        private bool ChangedResults(DocNodeParent nodePeptide)
        {
            if (nodePeptide.Children.Count != Children.Count)
                return true;

            int iChild = 0;
            foreach (TransitionGroupDocNode nodeGroup in Children)
            {
                // Results will differ if the identies of the children differ
                // at all.
                var nodeGroup2 = (TransitionGroupDocNode)nodePeptide.Children[iChild];
                if (!ReferenceEquals(nodeGroup.Id, nodeGroup2.Id))
                    return true;

                // or if the results for any child have changed
                if (!ReferenceEquals(nodeGroup.Results, nodeGroup2.Results))
                    return true;

                iChild++;
            }
            return false;
        }

        public override string GetDisplayText(DisplaySettings settings)
        {
            return PeptideTreeNode.DisplayText(this, settings);
        }

        public bool IsExcludeFromCalibration(int replicateIndex)
        {
            if (Results == null || replicateIndex < 0 || replicateIndex >= Results.Count)
            {
                return false;
            }
            var chromInfos = Results[replicateIndex];
            if (chromInfos.IsEmpty)
            {
                return false;
            }
            return chromInfos.Any(peptideChromInfo => peptideChromInfo != null && peptideChromInfo.ExcludeFromCalibration);
        }

        public PeptideDocNode ChangeExcludeFromCalibration(int replicateIndex, bool excluded)
        {
            var newChromInfos = new ChromInfoList<PeptideChromInfo>(Results[replicateIndex]
                .Select(peptideChromInfo => peptideChromInfo.ChangeExcludeFromCalibration(excluded)));
            return ChangeResults(Results.ChangeAt(replicateIndex, newChromInfos));
        }

        public bool HasPrecursorConcentrations
        {
            get { return TransitionGroups.Any(tg => tg.PrecursorConcentration.HasValue); }
        }

        private sealed class PeptideResultsCalculator
        {
            private readonly List<PeptideChromInfoListCalculator> _listResultCalcs;

            public PeptideResultsCalculator(SrmSettings settings, NormalizationMethod normalizationMethod)
            {
                Settings = settings;
                _listResultCalcs = new List<PeptideChromInfoListCalculator>(settings.MeasuredResults.Chromatograms.Count);
            }

            private SrmSettings Settings { get; set; }
            private int TransitionGroupCount { get; set; }

            public void AddGroupChromInfo(TransitionGroupDocNode nodeGroup)
            {
                TransitionGroupCount++;

                if (nodeGroup.HasResults)
                {
                    int countResults = nodeGroup.Results.Count;
                    while (_listResultCalcs.Count < countResults)
                    {
                        var calc = new PeptideChromInfoListCalculator(Settings, _listResultCalcs.Count);
                        _listResultCalcs.Add(calc);
                    }
                    for (int i = 0; i < countResults; i++)
                    {
                        var calc = _listResultCalcs[i];
                        calc.AddChromInfoList(nodeGroup);
                        foreach (TransitionDocNode nodeTran in nodeGroup.GetQuantitativeTransitions(Settings))
                            calc.AddChromInfoList(nodeGroup, nodeTran);
                    }
                }
            }

            public PeptideDocNode UpdateResults(PeptideDocNode nodePeptide)
            {
                var listChromInfoList = _listResultCalcs.ConvertAll(calc => calc.CalcChromInfoList(TransitionGroupCount));
                listChromInfoList = CopyChromInfoAttributes(nodePeptide, listChromInfoList);
                var results = Results<PeptideChromInfo>.Merge(nodePeptide.Results, listChromInfoList);
                if (!ReferenceEquals(results, nodePeptide.Results))
                    nodePeptide = nodePeptide.ChangeResults(results);

                var listGroupsNew = new List<DocNode>();
                foreach (TransitionGroupDocNode nodeGroup in nodePeptide.Children)
                {
                    // Update transition group ratios
                    var nodeGroupConvert = nodeGroup;
                    bool isMatching = nodeGroup.RelativeRT == RelativeRT.Matching;
                    var listGroupInfoList = _listResultCalcs.ConvertAll(
                        calc => calc.UpdateTransitonGroupRatios(nodeGroupConvert,
                                                                nodeGroupConvert.HasResults
                                                                    ? nodeGroupConvert.Results[calc.ResultsIndex]
                                                                    : default(ChromInfoList<TransitionGroupChromInfo>),
                                                                isMatching));
                    var resultsGroup = Results<TransitionGroupChromInfo>.Merge(nodeGroup.Results, listGroupInfoList);
                    var nodeGroupNew = nodeGroup;
                    if (!ReferenceEquals(resultsGroup, nodeGroup.Results))
                        nodeGroupNew = nodeGroup.ChangeResults(resultsGroup);

                    var listTransNew = new List<DocNode>();
                    foreach (TransitionDocNode nodeTran in nodeGroup.Children)
                    {
                        // Update transition ratios
                        var nodeTranConvert = nodeTran;
                        var listTranInfoList = _listResultCalcs.ConvertAll(
                            calc => calc.UpdateTransitionRatios(nodeGroup,
                                                               nodeTranConvert,
                                                               nodeTranConvert.Results[calc.ResultsIndex], isMatching));
                        var resultsTran = Results<TransitionChromInfo>.Merge(nodeTran.Results, listTranInfoList);
                        listTransNew.Add(ReferenceEquals(resultsTran, nodeTran.Results)
                                             ? nodeTran
                                             : nodeTran.ChangeResults(resultsTran));
                    }
                    listGroupsNew.Add(nodeGroupNew.ChangeChildrenChecked(listTransNew));
                }
                return (PeptideDocNode) nodePeptide.ChangeChildrenChecked(listGroupsNew);
            }

            
            private List<IList<PeptideChromInfo>> CopyChromInfoAttributes(PeptideDocNode peptideDocNode,
                List<IList<PeptideChromInfo>> results)
            {
                if (peptideDocNode == null || peptideDocNode.Results == null)
                {
                    return results;
                }
                Dictionary<int, Tuple<bool, double?>> peptideChromInfoAttributes = null;   // Delay allocation
                foreach (var chromInfos in peptideDocNode.Results)
                {
                    if (chromInfos.IsEmpty)
                    {
                        continue;
                    }
                    foreach (var chromInfo in chromInfos)
                    {
                        if (chromInfo != null)
                        {
                            if (chromInfo.ExcludeFromCalibration || chromInfo.AnalyteConcentration.HasValue)
                            {
                                if (peptideChromInfoAttributes == null)
                                {
                                    peptideChromInfoAttributes = new Dictionary<int, Tuple<bool, double?>>();
                                }
                                peptideChromInfoAttributes.Add(chromInfo.FileId.GlobalIndex, Tuple.Create(chromInfo.ExcludeFromCalibration, chromInfo.AnalyteConcentration));
                            }
                        }
                    }
                }
                if (peptideChromInfoAttributes == null)
                {
                    return results;
                }
                List<IList<PeptideChromInfo>> newResults = new List<IList<PeptideChromInfo>>(results.Count);
                for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
                {
                    var chromInfoList = results[replicateIndex];
                    if (chromInfoList == null)
                    {
                        newResults.Add(null);
                    }
                    else
                    {
                        IList<PeptideChromInfo> newChromInfoList = null;
                        foreach (var chromInfo in chromInfoList)
                        {
                            var chromInfoAdd = chromInfo;
                            if (chromInfo != null)
                            {
                                Tuple<bool, double?> attributes;
                                if (peptideChromInfoAttributes.TryGetValue(chromInfoAdd.FileId.GlobalIndex,
                                    out attributes))
                                {
                                    chromInfoAdd = chromInfoAdd.ChangeExcludeFromCalibration(attributes.Item1)
                                        .ChangeAnalyteConcentration(attributes.Item2);
                                }
                            } 
                            if (newChromInfoList != null)
                                newChromInfoList.Add(chromInfoAdd);
                            else
                            {
                                if (chromInfoList.Count < 2)
                                    newChromInfoList = new SingletonList<PeptideChromInfo>(chromInfoAdd);
                                else
                                    newChromInfoList = new List<PeptideChromInfo>(chromInfoList.Count){chromInfoAdd};
                            }
                        }
                        newResults.Add(newChromInfoList);
                    }
                }
                return newResults;
            }
        }

        private sealed class PeptideChromInfoListCalculator
        {
            public PeptideChromInfoListCalculator(SrmSettings settings, int resultsIndex)
            {
                ResultsIndex = resultsIndex;
                Settings = settings;
            }
            public int ResultsIndex { get; private set; }

            private SrmSettings Settings { get; set; }

            private int FileIndexFirst { get; set; }

            private PeptideChromInfoCalculator CalculatorFirst;
            private Dictionary<int, PeptideChromInfoCalculator> Calculators { get; set; }

            public void AddChromInfoList(TransitionGroupDocNode nodeGroup)
            {
                var listInfo = nodeGroup.Results[ResultsIndex];
                if (listInfo.IsEmpty)
                    return;

                foreach (var info in listInfo)
                {
                    if (info.OptimizationStep != 0)
                        continue;

                    PeptideChromInfoCalculator calc;
                    if (!TryGetCalculator(info.FileIndex, out calc))
                    {
                        calc = new PeptideChromInfoCalculator(Settings, ResultsIndex);
                        AddCalculator(info.FileIndex, calc);
                    }
                    calc.AddChromInfo(nodeGroup, info);
                }
            }

            private void AddCalculator(int fileIndex, PeptideChromInfoCalculator calc)
            {
                if (CalculatorFirst == null)
                {
                    FileIndexFirst = fileIndex;
                    CalculatorFirst = calc;
                }
                else
                {
                    if (Calculators == null)
                        Calculators = new Dictionary<int, PeptideChromInfoCalculator>{{FileIndexFirst, CalculatorFirst}};
                    Calculators.Add(fileIndex, calc);
                }
            }

            private bool TryGetCalculator(int fileIndex, out PeptideChromInfoCalculator calc)
            {
                if (CalculatorFirst != null)
                {
                    if (FileIndexFirst == fileIndex)
                    {
                        calc = CalculatorFirst;
                        return true;
                    }
                    else if (Calculators != null)
                    {
                        return Calculators.TryGetValue(fileIndex, out calc);
                    }
                }
                calc = null;
                return false;
            }

            public void AddChromInfoList(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
            {
                var listInfo = nodeTran.Results[ResultsIndex];
                if (listInfo.IsEmpty)
                    return;

                foreach (var info in listInfo)
                {
                    if (info.OptimizationStep != 0)
                        continue;

                    PeptideChromInfoCalculator calc;
                    if (!TryGetCalculator(info.FileIndex, out calc))
                    {
                        calc = new PeptideChromInfoCalculator(Settings, ResultsIndex);
                        AddCalculator(info.FileIndex, calc);
                    }
                    calc.AddChromInfo(nodeGroup, nodeTran, info);
                }
            }

            public IList<PeptideChromInfo> CalcChromInfoList(int transitionGroupCount)
            {
                if (CalculatorFirst == null)
                    return null;

                if (Calculators == null)
                {
                    var chromInfo = CalculatorFirst.CalcChromInfo(transitionGroupCount);
                    if (chromInfo == null)
                        return null;
                    return new SingletonList<PeptideChromInfo>(chromInfo);
                }

                return Calculators.Values
                    .OrderBy(c => c.FileOrder)
                    .Select(c => c.CalcChromInfo(transitionGroupCount))
                    .ToArray();
            }

            public IList<TransitionChromInfo> UpdateTransitionRatios(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, IList<TransitionChromInfo> listInfo, bool isMatching)
            {
                if (CalculatorFirst == null || listInfo == null)
                    return null;

                int countInfo = listInfo.Count;
                // Delay allocation in the hope that nothing has changed for faster loading
                TransitionChromInfo[] listInfoNew = null;
                int changeStartIndex = -1;
                for (int iInfo = 0; iInfo < countInfo; iInfo++)
                {
                    var info = listInfo[iInfo];

                    PeptideChromInfoCalculator calc;
                    if (!TryGetCalculator(info.FileIndex, out calc))
                        Assume.Fail();    // Should never happen
                    else
                    {
                        // Label free data will produce lots of reference equal empty ratios, so check that
                        // first as a shortcut
                        var infoNew = info;
                        if (isMatching && calc.IsSetMatching && !info.IsUserSetMatched)
                            infoNew = infoNew.ChangeUserSet(UserSet.MATCHED);
                        if (!ReferenceEquals(info, infoNew) && listInfoNew == null)
                        {
                            listInfoNew = new TransitionChromInfo[countInfo];
                            changeStartIndex = iInfo;
                        }
                        if (listInfoNew != null)
                            listInfoNew[iInfo] = infoNew;
                    }
                }
                
                if (listInfoNew == null)
                    return listInfo;

                for (int i = 0; i < changeStartIndex; i++)
                    listInfoNew[i] = listInfo[i];
                return listInfoNew;
            }

            public IList<TransitionGroupChromInfo> UpdateTransitonGroupRatios(TransitionGroupDocNode nodeGroup,
                                                                              IList<TransitionGroupChromInfo> listInfo,
                                                                              bool isMatching)
            {
                if (CalculatorFirst == null || listInfo == null)
                    return null;

                // Delay allocation in the hope that nothing has changed for faster loading
                TransitionGroupChromInfo[] listInfoNew = null;
                int changeStartIndex = -1;
                var standardTypes = Settings.PeptideSettings.Modifications.RatioInternalStandardTypes;
                for (int iInfo = 0; iInfo < listInfo.Count; iInfo++)
                {
                    var info = listInfo[iInfo];

                    PeptideChromInfoCalculator calc;
                    if (!TryGetCalculator(info.FileIndex, out calc))
                        Assume.Fail();    // Should never happen
                    else
                    {
                        var infoNew = info;
                        // Optimize for label free, no normalization cases
                        if (isMatching && calc.IsSetMatching && !infoNew.IsUserSetMatched)
                            infoNew = infoNew.ChangeUserSet(UserSet.MATCHED);

                        if (!ReferenceEquals(info, infoNew) && listInfoNew == null)
                        {
                            listInfoNew = new TransitionGroupChromInfo[listInfo.Count];
                            changeStartIndex = iInfo;
                        }
                        if (listInfoNew != null)
                            listInfoNew[iInfo] = infoNew;
                    }
                }
                if (listInfoNew == null)
                    return listInfo;

                for (int i = 0; i < changeStartIndex; i++)
                    listInfoNew[i] = listInfo[i];
                return listInfoNew;
            }
        }

        private sealed class PeptideChromInfoCalculator
        {
            public PeptideChromInfoCalculator(SrmSettings settings, int resultsIndex)
            {
                Settings = settings;
                ResultsIndex = resultsIndex;
                TranTypes = new HashSet<IsotopeLabelType>();
                TranAreas = new Dictionary<TransitionKey, float>();
            }

            private SrmSettings Settings { get; set; }
            private int ResultsIndex { get; set; }
            private ChromFileInfoId FileId { get; set; }
            public int FileOrder { get; private set; }
            private double PeakCountRatioTotal { get; set; }
            private int ResultsCount { get; set; }
            private int RetentionTimesMeasured { get; set; }
            private double RetentionTimeTotal { get; set; }
            private double GlobalStandardArea { get; set; }

            private HashSet<IsotopeLabelType> TranTypes { get; set; }
            private Dictionary<TransitionKey, float> TranAreas { get; set; }

            public bool HasGlobalArea { get { return Settings.HasGlobalStandardArea; }}
            public bool IsSetMatching { get; private set; }

// ReSharper disable UnusedParameter.Local
            public void AddChromInfo(TransitionGroupDocNode nodeGroup,
                                     TransitionGroupChromInfo info)
// ReSharper restore UnusedParameter.Local
            {
                if (info == null)
                    return;

                Assume.IsTrue(FileId == null || ReferenceEquals(info.FileId, FileId));
                var fileIdPrevious = FileId;
                FileId = info.FileId;
                FileOrder = Settings.MeasuredResults.Chromatograms[ResultsIndex].IndexOfId(FileId);

                // First time through calculate the global standard area for this file
                if (fileIdPrevious == null)
                    GlobalStandardArea = Settings.CalcGlobalStandardArea(ResultsIndex, Settings.MeasuredResults.Chromatograms[ResultsIndex].MSDataFileInfos[FileOrder]);

                ResultsCount++;
                PeakCountRatioTotal += info.PeakCountRatio;
                if (info.RetentionTime.HasValue)
                {
                    RetentionTimesMeasured++;
                    RetentionTimeTotal += info.RetentionTime.Value;
                }

                if (info.UserSet == UserSet.MATCHED)
                    IsSetMatching = true;
            }

            public void AddChromInfo(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, TransitionChromInfo info)
            {
                if (info.IsEmpty)
                    return;

                var key = new TransitionKey(nodeGroup, nodeTran.Key(nodeGroup), nodeGroup.TransitionGroup.LabelType);
                if (TranAreas.ContainsKey(key))
                {
                    return;
                }

                TranAreas.Add(key, info.Area);
                TranTypes.Add(nodeGroup.TransitionGroup.LabelType);
            }

            public PeptideChromInfo CalcChromInfo(int transitionGroupCount)
            {
                if (ResultsCount == 0)
                    return null;

                float peakCountRatio = (float) (PeakCountRatioTotal/transitionGroupCount);

                float? retentionTime = null;
                if (RetentionTimesMeasured > 0)
                    retentionTime = (float) (RetentionTimeTotal/RetentionTimesMeasured);
                var mods = Settings.PeptideSettings.Modifications;
                var listRatios = mods.CalcPeptideRatios((l, h) => CalcTransitionGroupRatio(Adduct.EMPTY, l, h),
                    l => CalcTransitionGroupGlobalRatio(Adduct.EMPTY, l));
                return new PeptideChromInfo(FileId, peakCountRatio, retentionTime, listRatios);
            }

            public float? CalcTransitionGlobalRatio(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, IsotopeLabelType labelType)
            {
                if (GlobalStandardArea == 0)
                    return null;

                float areaNum;
                var keyNum = new TransitionKey(nodeGroup, nodeTran.Key(nodeGroup), labelType);
                if (!TranAreas.TryGetValue(keyNum, out areaNum))
                    return null;
                return (float) (areaNum / GlobalStandardArea);
            }

            public float? CalcTransitionRatio(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran, IsotopeLabelType labelTypeNum, IsotopeLabelType labelTypeDenom)
            {
                if (Settings.PeptideSettings.Quantification.SimpleRatios)
                {
                    return null;
                }
                // Avoid 1.0 ratios for self-to-self
                if (ReferenceEquals(labelTypeNum, labelTypeDenom) || !TranTypes.Contains(labelTypeDenom) || !TranTypes.Contains(labelTypeNum))
                    return null;

                float areaNum, areaDenom;
                var key = nodeTran.Key(nodeGroup);
                var keyNum = new TransitionKey(nodeGroup, key, labelTypeNum);
                var keyDenom = new TransitionKey(nodeGroup, key, labelTypeDenom);
                if (!TranAreas.TryGetValue(keyNum, out areaNum) ||
                    !TranAreas.TryGetValue(keyDenom, out areaDenom))
                    return null;
                return areaNum/areaDenom;
            }

            public RatioValue CalcTransitionGroupGlobalRatio(TransitionGroupDocNode nodeGroup,
                                                             IsotopeLabelType labelTypeNum)
            {
                return CalcTransitionGroupGlobalRatio(nodeGroup.TransitionGroup.PrecursorAdduct,
                                                      labelTypeNum);
            }

            private RatioValue CalcTransitionGroupGlobalRatio(Adduct precursorAdduct, IsotopeLabelType labelType)
            {
                if (GlobalStandardArea == 0)
                    return null;

                int count = 0;
                double num = 0;
                foreach (var pair in GetAreaPairs(labelType))
                {
                    var key = pair.Key;
                    if (!key.IsMatchForRatioPurposes(precursorAdduct))
                        continue;
                    num += pair.Value;
                    count++;
                }
                if (count == 0)
                {
                    return null;
                }
                return new RatioValue(num / GlobalStandardArea);
            }

            public RatioValue CalcTransitionGroupRatio(TransitionGroupDocNode nodeGroup,
                                                       IsotopeLabelType labelTypeNum,
                                                       IsotopeLabelType labelTypeDenom)
            {
                return CalcTransitionGroupRatio(nodeGroup.TransitionGroup.PrecursorAdduct,
                                                labelTypeNum, labelTypeDenom);
            }

            private RatioValue CalcTransitionGroupRatio(Adduct precursorAdduct,
                                                        IsotopeLabelType labelTypeNum,
                                                        IsotopeLabelType labelTypeDenom)
            {
                // Avoid 1.0 ratios for self-to-self and extra work for cases where the denom label type is not represented
                if (ReferenceEquals(labelTypeNum, labelTypeDenom) || !TranTypes.Contains(labelTypeDenom))
                {
                    return null;
                }

                // Delay allocation, which can be costly in DIA data with no ratios
                List<double> numerators = null;
                List<double> denominators = null;

                if (Settings.PeptideSettings.Quantification.SimpleRatios)
                {
                    numerators = GetAreaPairs(labelTypeNum).Select(pair => (double) pair.Value).ToList();
                    denominators = GetAreaPairs(labelTypeDenom).Select(pair => (double)pair.Value).ToList();
                    if (numerators.Count == 0 || denominators.Count == 0)
                    {
                        return null;
                    }

                    return RatioValue.ValueOf(numerators.Sum() / denominators.Sum());
                }

                foreach (var pair in GetAreaPairs(labelTypeNum))
                {
                    var key = pair.Key;
                    if (!key.IsMatchForRatioPurposes(precursorAdduct))
                        continue; // Match charge states if any specified (adduct may also contain isotope info, so look at charge specifically)

                    float areaNum = pair.Value;
                    float areaDenom;
                    if (!TranAreas.TryGetValue(new TransitionKey(key, labelTypeDenom), out areaDenom))
                        continue;

                    if (numerators == null)
                    {
                        numerators = new List<double>();
                        denominators = new List<double>();
                    }
                    numerators.Add(areaNum);
                    denominators.Add(areaDenom);
                }

                if (numerators == null)
                    return null;

                return RatioValue.Calculate(numerators, denominators);
            }

            private IEnumerable<KeyValuePair<TransitionKey, float>> GetAreaPairs(IsotopeLabelType labelType)
            {
                return from pair in TranAreas
                       where ReferenceEquals(labelType, pair.Key.LabelType)
                       select pair;
            }
        }

        public struct TransitionKey
        {
            private readonly IonType _ionType;
            private readonly string _customIonEquivalenceTestValue; // Derived from formula, or name, or an mz sort
            private readonly int _ionOrdinal;
            private readonly int _massIndex;
            private readonly int? _decoyMassShift;
            private readonly Adduct _adduct; // We only care about charge and formula, other adduct details such as labels are intentionally not part of the comparison
            private readonly Adduct _precursorAdduct; // We only care about charge and formula, other adduct details such as labels are intentionally not part of the comparison
            private readonly TransitionLosses _losses;
            private readonly IsotopeLabelType _labelType;

            public TransitionKey(TransitionGroupDocNode nodeGroup, TransitionLossKey tranLossKey, IsotopeLabelType labelType)
            {
                var transition = tranLossKey.Transition;
                _ionType = transition.IonType;
                _customIonEquivalenceTestValue = tranLossKey.CustomIonEquivalenceTestValue;
                _ionOrdinal = transition.Ordinal;
                _massIndex = transition.MassIndex;
                _decoyMassShift = transition.DecoyMassShift;
                _adduct = transition.Adduct.Unlabeled; // Only interested in charge and formula, ignore any labels
                _precursorAdduct = nodeGroup.TransitionGroup.PrecursorAdduct.Unlabeled; // Only interested in charge and formula, ignore any labels
                _losses = tranLossKey.Losses;
                _labelType = labelType;
            }

            public TransitionKey(TransitionKey key, IsotopeLabelType labelType)
            {
                _ionType = key._ionType;
                _customIonEquivalenceTestValue = key._customIonEquivalenceTestValue;
                _ionOrdinal = key._ionOrdinal;
                _massIndex = key._massIndex;
                _decoyMassShift = key._decoyMassShift;
                _adduct = key._adduct;
                _precursorAdduct = key._precursorAdduct;
                _losses = key._losses;
                _labelType = labelType;
            }

            public Adduct PrecursorAdduct { get { return _precursorAdduct; } }
            public IsotopeLabelType LabelType { get { return _labelType; } }

            // Match charge states if any specified (adduct may also contain isotope info, so look at charge specifically)
            internal bool IsMatchForRatioPurposes(Adduct other)
            {
                return other.IsEmpty || Equals(PrecursorAdduct, other.Unlabeled);
            }

            #region object overrides

            private bool Equals(TransitionKey other)
            {
                return Equals(other._ionType, _ionType) &&
                       Equals(_customIonEquivalenceTestValue, other._customIonEquivalenceTestValue) &&
                       other._ionOrdinal == _ionOrdinal &&
                       other._massIndex == _massIndex &&
                       Equals(other._decoyMassShift, _decoyMassShift) &&
                       Equals(other._adduct, _adduct) &&
                       Equals(other._precursorAdduct, _precursorAdduct) &&
                       Equals(other._losses, _losses) &&
                       Equals(other._labelType, _labelType);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (obj.GetType() != typeof (TransitionKey)) return false;
                return Equals((TransitionKey) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int result = _ionType.GetHashCode();
                    result = (result*397) ^ (_customIonEquivalenceTestValue == null ? 0 : _customIonEquivalenceTestValue.GetHashCode());
                    result = (result*397) ^ _ionOrdinal;
                    result = (result*397) ^ _massIndex;
                    result = (result*397) ^ (_decoyMassShift.HasValue ? _decoyMassShift.Value : 0);
                    result = (result*397) ^ _adduct.GetHashCode();
                    result = (result*397) ^ _precursorAdduct.GetHashCode();
                    result = (result*397) ^ (_losses != null ? _losses.GetHashCode() : 0);
                    result = (result*397) ^ _labelType.GetHashCode();
                    return result;
                }
            }

            #endregion
        }

        #region object overrides

        public bool Equals(PeptideDocNode other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            var equal = base.Equals(other) &&
                Equals(other.ExplicitMods, ExplicitMods) &&
                Equals(other.SourceKey, SourceKey) &&
                other.Rank.Equals(Rank) &&
                Equals(other.Results, Results) &&
                Equals(other.ExplicitRetentionTime, ExplicitRetentionTime) &&
                other.BestResult == BestResult &&
                Equals(other.InternalStandardConcentration, InternalStandardConcentration) &&
                Equals(other.ConcentrationMultiplier, ConcentrationMultiplier) &&
                Equals(other.NormalizationMethod, NormalizationMethod) &&
                Equals(other.AttributeGroupId, AttributeGroupId);
            return equal; // For debugging convenience
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as PeptideDocNode);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (ExplicitMods != null ? ExplicitMods.GetHashCode() : 0);
                result = (result*397) ^ (SourceKey != null ? SourceKey.GetHashCode() : 0);
                result = (result*397) ^ (Rank.HasValue ? Rank.Value : 0);
                result = (result*397) ^ (ExplicitRetentionTime != null ? ExplicitRetentionTime.GetHashCode() : 0);
                result = (result*397) ^ (Results != null ? Results.GetHashCode() : 0);
                result = (result*397) ^ BestResult;
                result = (result*397) ^ InternalStandardConcentration.GetHashCode();
                result = (result*397) ^ ConcentrationMultiplier.GetHashCode();
                result = (result*397) ^ (NormalizationMethod == null ? 0 : NormalizationMethod.GetHashCode());
                result = (result*397) ^ (AttributeGroupId == null ? 0 : AttributeGroupId.GetHashCode());
                return result;
            }
        }

        public override string ToString()
        {
            return Rank.HasValue
                       ? String.Format(Resources.PeptideDocNodeToString__0__rank__1__, Peptide, Rank)
                       : Peptide.ToString();
        }

        #endregion
    }

    public class PeptidePrecursorPair
    {
        public PeptidePrecursorPair(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        {
            NodePep = nodePep;
            NodeGroup = nodeGroup;
        }

        public PeptideDocNode NodePep { get; private set; }
        public TransitionGroupDocNode NodeGroup { get; private set; }
    }

    public class ExplicitRetentionTimeInfo : IAuditLogComparable
    {
        public ExplicitRetentionTimeInfo(double retentionTime, double? retentionTimeWindow)
        {
            RetentionTime = retentionTime;
            RetentionTimeWindow = retentionTimeWindow;
        }

        [Track]
        public double RetentionTime { get; private set; }
        [Track]
        public double? RetentionTimeWindow { get; private set; }

        public static readonly ExplicitRetentionTimeInfo EMPTY = new ExplicitRetentionTimeInfo(0, null);

        public bool Equals(ExplicitRetentionTimeInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Equals(other.RetentionTime, RetentionTime) &&
                Equals(other.RetentionTimeWindow, RetentionTimeWindow);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as ExplicitRetentionTimeInfo);
        }


        public override int GetHashCode()
        {
            int result = RetentionTime.GetHashCode() ;
            if (RetentionTimeWindow.HasValue)
                result = (result*397) ^ RetentionTimeWindow.Value.GetHashCode() ;
            return result;
        }

        public object GetDefaultObject(ObjectInfo<object> info)
        {
            return EMPTY;
        }
    }
}
