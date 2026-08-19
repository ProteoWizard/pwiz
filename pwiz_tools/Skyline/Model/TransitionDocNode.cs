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
using System.Globalization;
using System.IO;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Optimization;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class TransitionDocNode : DocNode
    {
        public static string TITLE => ModelResources.TransitionDocNode_Title;
        public static string TITLES => ModelResources.TransitionDocNode_Titles;

        public TransitionDocNode(Transition id,
                                 TransitionLosses losses,
                                 TypedMass massH,
                                 TransitionQuantInfo quantInfo,
                                 ExplicitTransitionValues explicitTransitionValues)
            : this(id, Annotations.EMPTY, losses, massH, quantInfo, explicitTransitionValues, null)
        {
        }

        public TransitionDocNode(Transition id,
                                 Annotations annotations,
                                 TransitionLosses losses,
                                 TypedMass mass,
                                 TransitionQuantInfo transitionQuantInfo,
                                 ExplicitTransitionValues explicitTransitionValues,
                                 Results<TransitionChromInfo> results)
            : this(ComplexFragmentIon.Simple(id, losses), annotations, losses == null ? mass : mass - losses.Mass, transitionQuantInfo, explicitTransitionValues, results)
        {
        }

        public TransitionDocNode(ComplexFragmentIon complexFragmentIon, Annotations annotations, TypedMass mass,
            TransitionQuantInfo transitionQuantInfo,
            ExplicitTransitionValues explicitTransitionValues,
            Results<TransitionChromInfo> results) : base(complexFragmentIon.PrimaryTransition, annotations)
        {
            ComplexFragmentIon = complexFragmentIon;
            Mz = Transition.IsCustom() ?
                new SignedMz(Transition.Adduct.MzFromNeutralMass(mass), Transition.IsNegative()) :
                new SignedMz(SequenceMassCalc.GetMZ(mass, Transition.Adduct) + SequenceMassCalc.GetPeptideInterval(Transition.DecoyMassShift), Transition.IsNegative());
            MzMassType = mass.MassType;
            IsotopeDistInfo = transitionQuantInfo.IsotopeDistInfo;
            LibInfo = transitionQuantInfo.LibInfo;
            _emptyResults = EmptyLike(results);
            ExplicitQuantitative = transitionQuantInfo.Quantititative;
            ExplicitValues = explicitTransitionValues ?? ExplicitTransitionValues.EMPTY;

        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.transition; } }

        public Transition Transition { get { return (Transition)Id; } }

        [TrackChildren(ignoreName:true, defaultValues:typeof(DefaultValuesNull))]
        public CustomIon CustomIon { get { return Transition.CustomIon; } }

        public ComplexFragmentIon ComplexFragmentIon { get; private set; }

        [TrackChildren]
        public ExplicitTransitionValues ExplicitValues { get; private set; }

        public TransitionLossKey Key(TransitionGroupDocNode parent)
        {
            return new TransitionLossKey(parent, this, Losses);
        }

        public MassType MzMassType { get; private set; }  // The massType used to calculate Mz
        public SignedMz Mz { get; private set; }

        // Returns molecule mass (or massH, for peptides)
        public TypedMass GetMoleculeMass()
        {
            Assume.IsTrue(Transition.IsCustom() || MzMassType.IsMassH());
            return Transition.IsCustom()
                ? Transition.CustomIon.GetMass(MzMassType)
                : new TypedMass(SequenceMassCalc.GetMH(Mz, Transition.Charge), MzMassType);
        }

        public TypedMass GetMoleculeMass(CustomMolecule molecule)
        {
            Assume.IsTrue(Transition.IsCustom() || MzMassType.IsMassH());
            return molecule.GetMass(MzMassType);
        }

        public bool IsDecoy { get { return Transition.DecoyMassShift.HasValue; } }

        public TransitionLosses Losses
        {
            get { return ComplexFragmentIon.Losses; }
        }

        public bool HasLoss { get { return Losses != null; } }

        public double LostMass { get { return HasLoss ? Losses.Mass : 0; } }

        [Track(defaultValues: typeof(DefaultValuesTrue))]
        public bool ExplicitQuantitative { get; private set; }

        public bool ParticipatesInScoring => Transition.ParticipatesInScoring; // Don't use things like reporter ions (e.g. TMT etc) in "best" peak selection

        public TransitionDocNode ChangeExplicitSLens(double? value)
        {
            return ChangeExplicitValues(ExplicitValues.ChangeSLens(value));
        }

        public TransitionDocNode ChangeExplicitCollisionEnergy(double? value)
        {
            return ChangeExplicitValues(ExplicitValues.ChangeCollisionEnergy(value));
        }

        public TransitionDocNode ChangeExplicitConeVoltage(double? value)
        {
            return ChangeExplicitValues(ExplicitValues.ChangeConeVoltage(value));
        }

        public TransitionDocNode ChangeExplicitDeclusteringPotential(double? value)
        {
            return ChangeExplicitValues(ExplicitValues.ChangeDeclusteringPotential(value));
        }

        public TransitionDocNode ChangeExplicitIonMobilityHighEnergyOffset(double? value)
        {
            return ChangeExplicitValues(ExplicitValues.ChangeIonMobilityHighEnergyOffset(value));
        }

        public bool IsQuantitative(SrmSettings settings)
        {
            if (!ExplicitQuantitative)
            {
                return false;
            }
            if (!IsMs1 && FullScanAcquisitionMethod.DDA.Equals(settings.TransitionSettings.FullScan.AcquisitionMethod))
            {
                return false;
            }
            return true;
        }

        public TransitionQuantInfo QuantInfo { get { return new TransitionQuantInfo(IsotopeDistInfo, LibInfo, ExplicitQuantitative);} }

        public bool IsLossPossible(int maxLossMods, IList<StaticMod> modsLossAvailable)
        {
            if (HasLoss)
            {
                var losses = Losses.Losses;
                if (losses.Count > maxLossMods)
                    return false;
                foreach (var loss in losses)
                {
                    // If the same precursor mod exists, then it will also have the
                    // loss in question, since modification equality depends on loss
                    // equality also.
                    if (!modsLossAvailable.Any(l => l.EquivalentAll(loss.PrecursorMod)))
                        return false;
                }
            }
            return true;
        }

        public string FragmentIonName
        {
            get { return GetFragmentIonName(LocalizationHelper.CurrentCulture); }
        }

        public string GetFragmentIonName(CultureInfo cultureInfo, MzTolerance tolerance = null)
        {
            if (ComplexFragmentIon.IsCrosslinked)
            {
                return ComplexFragmentIon.GetFragmentIonName();
            }
            string ionName = Transition.GetFragmentIonName(cultureInfo, tolerance);
            return (HasLoss ? string.Format(@"{0} -{1}", ionName, Math.Round(Losses.Mass, 1)) : ionName);
        }

        /// <summary>
        /// Returns true for a transition that would be filtered from MS1 in full-scan filtering.
        /// </summary>
        public bool IsMs1
        {
            get { return ComplexFragmentIon.IsMs1; }
        }

        public TransitionIsotopeDistInfo IsotopeDistInfo { get; private set; }

        public bool HasDistInfo { get { return IsotopeDistInfo != null; }}

        public static TransitionIsotopeDistInfo GetIsotopeDistInfo(Transition transition, TransitionLosses losses, IsotopeDistInfo isotopeDist)
        {
            if (isotopeDist == null || !transition.IsPrecursor() || losses != null)
                return null;
            return new TransitionIsotopeDistInfo(isotopeDist.GetRankI(transition.MassIndex),
                isotopeDist.GetProportionI(transition.MassIndex));
        }

        public static bool IsValidIsotopeTransition(Transition transition, IsotopeDistInfo isotopeDist)
        {
            if (!transition.IsPrecursor())
                return true;
            if (isotopeDist == null)
                return transition.MassIndex == 0;
            int i = isotopeDist.MassIndexToPeakIndex(transition.MassIndex);
            return 0 <= i && i < isotopeDist.CountPeaks;
        }

        /// <summary>
        /// Gets a formatted label for a transition, including ion name, m/z, charge indicator, rank info, and optional results text.
        /// </summary>
        public static string GetLabel(TransitionDocNode nodeTran, string resultsText)
        {
            Transition tran = nodeTran.Transition;
            string labelPrefix;
            const string labelPrefixSpacer = " - ";
            if (nodeTran.ComplexFragmentIon.IsCrosslinked)
            {
                labelPrefix = nodeTran.ComplexFragmentIon.GetTargetsTreeLabel() + labelPrefixSpacer;
            }
            else if (tran.IsPrecursor())
            {
                labelPrefix = nodeTran.FragmentIonName + Transition.GetMassIndexText(tran.MassIndex) + labelPrefixSpacer;
            }
            else if (tran.IsCustom())
            {
                if (!string.IsNullOrEmpty(tran.CustomIon.Name))
                    labelPrefix = tran.CustomIon.Name + labelPrefixSpacer;
                else if (tran.CustomIon.HasChemicalFormula)
                    labelPrefix = tran.CustomIon.Formula + labelPrefixSpacer; // Show formula (e.g. C12H5 or maybe C12H5[-1.2/1.21]
                else
                    labelPrefix = string.Empty; // Just show the mass
            }
            else
            {
                labelPrefix = string.Format(ModelResources.TransitionDocNode_GetLabel__0__1__, tran.AA, nodeTran.FragmentIonName) + labelPrefixSpacer;
            }

            if (!nodeTran.HasLibInfo && !nodeTran.HasDistInfo)
            {
                return string.Format(@"{0}{1}{2}{3}",
                                     labelPrefix,
                                     GetMzLabel(nodeTran),
                                     Transition.GetChargeIndicator(tran.Adduct),
                                     resultsText);
            }
            
            string rank = nodeTran.HasDistInfo
                              ? string.Format(ModelResources.TransitionDocNode_GetLabel_irank__0__, nodeTran.IsotopeDistInfo.Rank)
                              : string.Format(ModelResources.TransitionDocNode_GetLabel_rank__0__, nodeTran.LibInfo.Rank);

            return string.Format(@"{0}{1}{2} ({3}){4}",
                                 labelPrefix,
                                 GetMzLabel(nodeTran),
                                 Transition.GetChargeIndicator(tran.Adduct),
                                 rank,
                                 resultsText);
        }

        private static string GetMzLabel(TransitionDocNode nodeTran)
        {
            int? massShift = nodeTran.Transition.DecoyMassShift;
            double shift = SequenceMassCalc.GetPeptideInterval(massShift);
            return string.Format(@"{0:F04}{1}", nodeTran.Mz - shift,
                Transition.GetDecoyText(massShift));
        }

        public TransitionLibInfo LibInfo { get; private set; }

        public bool HasLibInfo { get { return LibInfo != null; } }

        /// <summary>
        /// An empty entry for each replicate the transition has results in, which is all a
        /// transition holds now. Its peaks are the columnar results of the precursor which owns it
        /// - see <see cref="TransitionGroupResults"/> - and the chrom infos they were read as are
        /// rebuilt from the .skyd by a <see cref="MoleculeResults"/>.
        /// <para>
        /// What this still says is whether the node was made from chrom infos at all, which is what
        /// <see cref="HasResults"/> answers.
        /// </para>
        /// </summary>
        private Results<TransitionChromInfo> _emptyResults;

        /// <summary>
        /// See <see cref="_emptyResults"/>. Named for what it holds - nothing - because reading
        /// peaks out of it is the mistake this replaced a property called Results to stop.
        /// </summary>
        public Results<TransitionChromInfo> EmptyResults { get { return _emptyResults; } }

        /// <summary>
        /// An empty entry for each replicate of <paramref name="results"/>, which is all that a
        /// transition ever reports now.
        /// </summary>
        private static Results<TransitionChromInfo> EmptyLike(Results<TransitionChromInfo> results)
        {
            return results == null
                ? null
                : new Results<TransitionChromInfo>(new ChromInfoList<TransitionChromInfo>[results.Count]);
        }

        /// <summary>
        /// The mean of the ranks this transition's peaks have, which does not vary by replicate and
        /// so is one of the few result values a transition still keeps. Null until a results pass
        /// has run over the document - see
        /// <see cref="TransitionGroupDocNode.GetTransitionAverageRank"/>, which works the same rank
        /// out from the columnar areas for a document no pass has run over.
        /// </summary>
        public int? ResultsRank { get; private set; }

        public bool HasResults { get { return _emptyResults != null; } }

        /// <summary>
        /// How many replicates the results cover, which is what Results.Count used to answer, and
        /// zero for a transition with no results at all.
        /// </summary>
        public int ResultsReplicateCount
        {
            get { return _emptyResults?.Count ?? 0; }
        }

        /// <summary>
        /// The rank of this transition, either among the library's or among the measured peaks.
        /// The latter comes from the precursor, which is what holds the areas a rank is the order
        /// of - see <see cref="TransitionGroupDocNode.GetTransitionRank"/>.
        /// </summary>
        public int? GetRank(TransitionGroupDocNode nodeGroup, int? i, bool useResults)
        {
            if (useResults && HasResults)
            {
                if (i.HasValue)
                    return nodeGroup?.GetTransitionRank(Transition, i.Value, false);
                return ResultsRank ?? nodeGroup?.GetTransitionAverageRank(Transition);
            }
            if (!useResults && HasLibInfo && LibInfo.Rank > 0)
                return LibInfo.Rank;
            return null;
        }

        /// <summary>
        /// Return product's neutral mass rounded for XML I/O
        /// </summary>
        public double GetMoleculePersistentNeutralMass()
        {
            var moleculeMass = GetMoleculeMass();
            return Transition.IsCustom() ? Math.Round(moleculeMass, SequenceMassCalc.MassPrecision) : SequenceMassCalc.PersistentNeutral(moleculeMass);
        }



        public DocNode EnsureChildren(TransitionGroupDocNode parent, SrmSettings settings)
        {
            // Make sure node points to correct parent.
            if  (ReferenceEquals(parent.TransitionGroup, Transition.Group))
                return this;

            var transition = Transition.IsCustom()
                ? new Transition(parent.TransitionGroup,
                    Transition.Adduct,
                    Transition.MassIndex,
                    Transition.CustomIon,
                    Transition.IonType)
                : new Transition(parent.TransitionGroup,
                                Transition.IonType,
                                Transition.CleavageOffset,
                                Transition.MassIndex,
                                Transition.Adduct);

            return new TransitionDocNode(transition,
                                         Annotations,
                                         Losses,
                                         TypedMass.ZERO_MONO_MASSH, 
                                         QuantInfo,
                                         ExplicitValues,
                                         null) {Mz = Mz, MzMassType = MzMassType};
        }

        public override string GetDisplayText(DisplaySettings settings)
        {
            // Mirror legacy UI semantics without depending on Controls.SeqNode
            return GetLabel(this, GetResultsText(settings, FindNodeGroup(settings, this), this));
        }

        /// <summary>
        /// The rank and the ratio the targets tree shows beside a transition. Both come from the
        /// precursor's columnar results, so painting the tree reads no chromatogram, which is what
        /// asking a <see cref="MoleculeResults"/> for every node would have meant.
        /// </summary>
        public static string GetResultsText(DisplaySettings displaySettings, TransitionGroupDocNode nodeGroup,
            TransitionDocNode nodeTran)
        {
            int resultsIndex = displaySettings.ResultsIndex;
            int? rank = nodeGroup?.GetTransitionRank(nodeTran.Transition, resultsIndex, true);
            string label = string.Empty;
            if (rank.HasValue && rank > 0)
            {
                // Mark MS1 transition ranks with "i" for isotope
                string rankText = (nodeTran.IsMs1 ? @"i " : string.Empty) + rank;
                label = string.Format(Resources.TransitionTreeNode_GetResultsText__0__, rankText);
            }

            float? ratio = null;
            if (nodeGroup != null && !Equals(displaySettings.NormalizationMethod, NormalizationMethod.NONE) &&
                nodeGroup.TryGetReplicateTransitionPeak(nodeTran.Transition, resultsIndex, out var fileId,
                    out var peak) && !peak.IsEmpty)
            {
                ratio = (float?) displaySettings.NormalizedValueCalculator.GetTransitionAreaValue(
                    displaySettings.NormalizationMethod, displaySettings.NodePep, nodeGroup, nodeTran, resultsIndex,
                    fileId, peak.Area);
            }
            if (!ratio.HasValue)
                return label;

            return string.Format(Resources.TransitionTreeNode_GetResultsText__0__ratio__1__, label, MathEx.RoundAboveZero(ratio.Value, 2, 4));
        }

        /// <summary>
        /// The precursor which owns a transition, which is where its results are now. Null when the
        /// molecule being displayed does not hold it.
        /// </summary>
        private static TransitionGroupDocNode FindNodeGroup(DisplaySettings displaySettings,
            TransitionDocNode nodeTran)
        {
            return (TransitionGroupDocNode) displaySettings.NodePep?.FindNode(nodeTran.Transition.Group);
        }

        public string PrimaryCustomIonEquivalenceKey
        {
            get { return Transition.CustomIon.PrimaryEquivalenceKey; }
        }

        public string SecondaryCustomIonEquivalenceKey
        {
            get { return Transition.CustomIon.SecondaryEquivalenceKey; }
        }

        public class CustomIonEquivalenceComparer : IComparer<TransitionDocNode>
        {
            public int Compare(TransitionDocNode left, TransitionDocNode right)
            {
                // ReSharper disable PossibleNullReferenceException
                if (left.Transition.IsPrecursor() != right.Transition.IsPrecursor())
                    return left.Transition.IsPrecursor() ? -1 : 1;  // Precursors come first
                if (!string.IsNullOrEmpty(left.PrimaryCustomIonEquivalenceKey) && !string.IsNullOrEmpty(right.PrimaryCustomIonEquivalenceKey))
                    return string.CompareOrdinal(left.PrimaryCustomIonEquivalenceKey, right.PrimaryCustomIonEquivalenceKey);
                if (!string.IsNullOrEmpty(left.SecondaryCustomIonEquivalenceKey) && !string.IsNullOrEmpty(right.SecondaryCustomIonEquivalenceKey))
                    return string.CompareOrdinal(left.SecondaryCustomIonEquivalenceKey, right.SecondaryCustomIonEquivalenceKey);
                return right.Mz.CompareTo(left.Mz); // Decreasing mz sort
                // ReSharper restore PossibleNullReferenceException
            }
        }

        /// <summary>
        /// <paramref name="chromInfos"/> is what this transition's peaks are written from, since a
        /// transition no longer keeps them: the caller rebuilds them through a
        /// <see cref="MoleculeResults"/> once for the whole molecule. Null writes no peaks, which
        /// is what a document with no results does.
        /// </summary>
        public SkylineDocumentProto.Types.Transition ToTransitionProto(SrmSettings settings, PeptideDocNode nodePep,
            TransitionGroupDocNode nodeGroup, Results<TransitionChromInfo> chromInfos)
        {
            var transitionProto = new SkylineDocumentProto.Types.Transition
            {
                FragmentType = DataValues.ToIonType(Transition.IonType),
                NotQuantitative = !ExplicitQuantitative,
                OrphanedCrosslinkIon = ComplexFragmentIon.IsOrphan
            };
            if (Transition.IsCustom() && !Transition.IsPrecursor())
            {
                SetCustomIonFragmentInfo(transitionProto);
            }
            transitionProto.DecoyMassShift = DataValues.ToOptional(Transition.DecoyMassShift);
            transitionProto.MassIndex = Transition.MassIndex;
            if (HasDistInfo)
            {
                transitionProto.IsotopeDistRank = DataValues.ToOptional(IsotopeDistInfo.Rank);
                transitionProto.IsotopeDistProportion = IsotopeDistInfo.Proportion;
            }
            if (!Transition.IsPrecursor() || !Equals(Transition.Adduct, Transition.Group.PrecursorAdduct))
            {
                if (!Transition.IsCustom())
                {
                    transitionProto.FragmentOrdinal = Transition.Ordinal;
                    transitionProto.CalcNeutralMass = GetMoleculePersistentNeutralMass();
                }
                transitionProto.Charge = Transition.Charge;
                if (!Transition.Adduct.IsProteomic)
                {
                    transitionProto.Adduct = Transition.Adduct.AsFormulaOrSignedInt();
                }
                if (!Transition.IsCustom())
                {
                    transitionProto.CleavageAa = Transition.AA;
                    transitionProto.LostMass = LostMass;
                }
            }
            if (Annotations != null)
            {
                transitionProto.Annotations = Annotations.ToProtoAnnotations();
            }
            transitionProto.ProductMz = Mz;
            if (Losses != null)
            {
                foreach (var loss in Losses.Losses)
                {
                    var neutralLoss = new SkylineDocumentProto.Types.TransitionLoss();
                    if (loss.PrecursorMod == null)
                    {
                        neutralLoss.Formula = loss.Loss.Formula;
                        neutralLoss.MonoisotopicMass = loss.Loss.MonoisotopicMass;
                        neutralLoss.AverageMass = loss.Loss.AverageMass;
                        neutralLoss.LossInclusion = DataValues.ToLossInclusion(loss.Loss.Inclusion);
                        neutralLoss.Charge = loss.Loss.Charge;
                    }
                    else
                    {
                        neutralLoss.ModificationName = loss.PrecursorMod.Name;
                        neutralLoss.LossIndex = loss.LossIndex;
                    }
                    transitionProto.Losses.Add(neutralLoss);
                }
            }
            if (HasLibInfo)
            {
                transitionProto.LibInfo = new SkylineDocumentProto.Types.TransitionLibInfo
                {
                    Intensity = LibInfo.Intensity,
                    Rank = LibInfo.Rank
                };
            }
            if (chromInfos != null)
            {
                transitionProto.Results = new SkylineDocumentProto.Types.TransitionResults();
                transitionProto.Results.Peaks.AddRange(GetTransitionPeakProtos(chromInfos, settings.MeasuredResults));
            }

            if (!Equals(ExplicitValues, ExplicitTransitionValues.EMPTY))
            {
                transitionProto.ExplicitCollisionEnergy = ExplicitValues.CollisionEnergy;
                transitionProto.ExplicitConeVoltage = ExplicitValues.ConeVoltage;
                transitionProto.ExplicitDeclusteringPotential = ExplicitValues.DeclusteringPotential;
                transitionProto.ExplicitIonMobilityHighEnergyOffset = ExplicitValues.IonMobilityHighEnergyOffset;
                transitionProto.ExplicitSLens = ExplicitValues.SLens;
            }

            foreach (IonOrdinal part in ComplexFragmentIon.NeutralFragmentIon.IonChain.Skip(1))
            {
                var linkedIon = new SkylineDocumentProto.Types.LinkedIon();
                if (part.IsEmpty)
                {
                    linkedIon.Orphan = true;
                }
                else
                {
                    linkedIon.IonType = DataValues.ToIonType(part.Type.Value);
                    linkedIon.Ordinal = part.Ordinal;
                }
                transitionProto.LinkedIons.Add(linkedIon);
            }

            double? ce = GetCollisionEnergy(settings, nodePep, nodeGroup);
            double? dp = GetDeclusteringPotential(settings, nodePep, nodeGroup);

            if (ce.HasValue)
            {
                transitionProto.CollisionEnergy = ce;
            }

            if (dp.HasValue)
            {
                transitionProto.DeclusteringPotential = dp;
            }

            return transitionProto;
        }

        public double? GetCollisionEnergy(SrmSettings settings, PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        {
            if (ExplicitValues.CollisionEnergy.HasValue)
                return ExplicitValues.CollisionEnergy; // Explicitly imported, overrides any calculation

            double? ce = null;
            TransitionPrediction predict = settings.TransitionSettings.Prediction;
            var optimizationMethod = predict.OptimizedMethodType;
            var lib = predict.OptimizedLibrary;
            if (lib != null && !lib.IsNone)
            {
                var optimization = lib.GetOptimization(OptimizationType.collision_energy,
                    settings.GetSourceTarget(nodePep), nodeGroup.PrecursorAdduct,
                    FragmentIonName, Transition.Adduct);
                if (optimization != null)
                {
                    ce = optimization.Value;
                }
            }

            double regressionMz = settings.GetRegressionMz(nodePep, nodeGroup);
            var ceRegression = predict.CollisionEnergy;
            if (optimizationMethod == OptimizedMethodType.None)
            {
                if (ceRegression != null && !ce.HasValue)
                {
                    ce = ceRegression.GetCollisionEnergy(nodeGroup.PrecursorAdduct, regressionMz);
                }
            }
            else
            {
                if (!ce.HasValue)
                {
                    ce = OptimizationStep<CollisionEnergyRegression>.FindOptimizedValue(settings,
                        nodePep, nodeGroup, this, optimizationMethod, ceRegression,
                        SrmDocument.GetCollisionEnergy);
                }
            }

            return ce;
        }

        public double? GetDeclusteringPotential(SrmSettings settings, PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup)
        {
            double? dp = null;

            TransitionPrediction predict = settings.TransitionSettings.Prediction;
            var optimizationMethod = predict.OptimizedMethodType;
            double regressionMz = settings.GetRegressionMz(nodePep, nodeGroup);
            var dpRegression = predict.DeclusteringPotential;
            if (optimizationMethod == OptimizedMethodType.None)
            {
                if (dpRegression != null)
                {
                    dp = dpRegression.GetDeclustringPotential(regressionMz);
                }
            }
            else
            {
                dp = OptimizationStep<DeclusteringPotentialRegression>.FindOptimizedValue(settings,
                    nodePep, nodeGroup, this, optimizationMethod, dpRegression,
                    SrmDocument.GetDeclusteringPotential);
            }

            return dp;
        }

        private void SetCustomIonFragmentInfo(SkylineDocumentProto.Types.Transition transitionProto)
        {
            if (Transition.IsNonReporterCustomIon())
            {
                transitionProto.Formula = Transition.CustomIon.ParsedMolecule.IsMassOnly ?
                    null : 
                    Transition.CustomIon.ParsedMolecule.ToString();
                if (Transition.CustomIon.AverageMass.IsMassH())
                    transitionProto.AverageMassH = Transition.CustomIon.AverageMass;
                else
                    transitionProto.AverageMass = Transition.CustomIon.AverageMass;
                if (Transition.CustomIon.MonoisotopicMass.IsMassH())
                    transitionProto.MonoMassH = Transition.CustomIon.MonoisotopicMass;
                else
                    transitionProto.MonoMass = Transition.CustomIon.MonoisotopicMass;
                transitionProto.CustomIonName = Transition.CustomIon.Name;
                transitionProto.MoleculeId = Transition.CustomIon.AccessionNumbers.ToString();
            }
            else
            {
                transitionProto.MeasuredIonName = Transition.CustomIon.Name;
            }
        }

        public static TransitionDocNode FromTransitionProto(AnnotationScrubber scrubber, SrmSettings settings,
            TransitionGroup group, ExplicitMods mods, IsotopeDistInfo isotopeDist, ExplicitTransitionValues pre422ExplicitTransitionValues,
            CrosslinkBuilder crosslinkBuilder,
            SkylineDocumentProto.Types.Transition transitionProto,
            out Results<TransitionChromInfo> chromInfos)
        {
            IonType ionType = DataValues.FromIonType(transitionProto.FragmentType);
            MeasuredIon measuredIon = null;
            if (transitionProto.MeasuredIonName != null)
            {
                measuredIon = settings.TransitionSettings.Filter.MeasuredIons.SingleOrDefault(
                    i => i.Name.Equals(transitionProto.MeasuredIonName));
                if (measuredIon == null)
                    throw new InvalidDataException(string.Format(Resources.TransitionInfo_ReadXmlAttributes_The_reporter_ion__0__was_not_found_in_the_transition_filter_settings_, transitionProto.MeasuredIonName));
                ionType = IonType.custom;
            }
            bool isCustom = Transition.IsCustom(ionType, group);
            bool isPrecursor = Transition.IsPrecursor(ionType);
            CustomMolecule customIon = null;
            if (isCustom)
            {
                if (measuredIon != null)
                {
                    customIon = measuredIon.SettingsCustomIon;
                }
                else if (isPrecursor)
                {
                    customIon = group.CustomMolecule;
                }
                else
                {
                    var formula = ParsedMolecule.Create(transitionProto.Formula);
                    var moleculeID = MoleculeAccessionNumbers.FromString(transitionProto.MoleculeId); // Tab separated list of InChiKey, CAS etc
                    var monoMassH = transitionProto.MonoMassH;
                    var averageMassH = transitionProto.AverageMassH;
                    var monoMass = transitionProto.MonoMass ?? monoMassH;
                    var averageMass = transitionProto.AverageMass ?? averageMassH;
                    var monoMassType = monoMassH.HasValue ? MassType.MonoisotopicMassH : MassType.Monoisotopic;
                    customIon = ParsedMolecule.IsNullOrEmpty(formula) ?
                        new CustomMolecule(new TypedMass(monoMass??0, monoMassType),
                            new TypedMass(averageMass??0, averageMassH.HasValue ? MassType.AverageMassH : MassType.Average),
                            transitionProto.CustomIonName, moleculeID) :
                        new CustomMolecule(formula.ChangeIsMassH(monoMassType.IsMassH()), transitionProto.CustomIonName, moleculeID);
                }
            }
            Transition transition;
            var adductString = transitionProto.Adduct;
            var adduct = string.IsNullOrEmpty(adductString)
                ? Adduct.FromChargeProtonated(transitionProto.Charge)
                : Adduct.FromStringAssumeChargeOnly(adductString);
            if (isCustom)
            {
                transition = new Transition(group, isPrecursor ? group.PrecursorAdduct : adduct, transitionProto.MassIndex, customIon, ionType);
            }
            else if (isPrecursor)
            {
                // TODO(nicksh): Make sure this adduct stuff is correct.
                transition = new Transition(group, ionType, group.Peptide.Length - 1, transitionProto.MassIndex,
                    adduct.IsEmpty ? group.PrecursorAdduct : adduct, DataValues.FromOptional(transitionProto.DecoyMassShift));
            }
            else
            {
                int offset = Transition.OrdinalToOffset(ionType, transitionProto.FragmentOrdinal,
                    group.Peptide.Length);
                transition = new Transition(group, ionType, offset, transitionProto.MassIndex, adduct, DataValues.FromOptional(transitionProto.DecoyMassShift));
            }
            var losses = TransitionLosses.FromLossProtos(settings, transitionProto.Losses);
            var isotopeDistInfo = GetIsotopeDistInfo(transition, losses, isotopeDist);
            if (group.DecoyMassShift.HasValue && transitionProto.DecoyMassShift == null)
            {
                throw new InvalidDataException(Resources.SrmDocument_ReadTransitionXml_All_transitions_of_decoy_precursors_must_have_a_decoy_mass_shift);
            }

            TransitionLibInfo libInfo = null;
            if (transitionProto.LibInfo != null)
            {
                libInfo = new TransitionLibInfo(transitionProto.LibInfo.Rank, transitionProto.LibInfo.Intensity);
            }
            var annotations = scrubber.ScrubAnnotations(Annotations.FromProtoAnnotations(transitionProto.Annotations), AnnotationDef.AnnotationTarget.transition);
            // Handed back for the precursor to keep in its columnar results rather than put on the
            // node, which no longer holds any.
            chromInfos = TransitionChromInfo.FromProtoTransitionResults(scrubber, settings, transitionProto.Results);
            var explicitTransitionValues = pre422ExplicitTransitionValues ?? ExplicitTransitionValues.Create(
                transitionProto.ExplicitCollisionEnergy,
                transitionProto.ExplicitIonMobilityHighEnergyOffset,
                transitionProto.ExplicitSLens,
                transitionProto.ExplicitConeVoltage,
                transitionProto.ExplicitDeclusteringPotential);

            TransitionDocNode transitionDocNode;
            var transitionQuantInfo =
                new TransitionQuantInfo(isotopeDistInfo, libInfo, !transitionProto.NotQuantitative);
            if (mods != null && mods.HasCrosslinks)
            {
                var parts = new List<IonOrdinal>();
                parts.Add(transitionProto.OrphanedCrosslinkIon
                    ? IonOrdinal.Empty
                    : IonOrdinal.FromTransition(transition));
                if (null != mods.LegacyCrosslinkMap)
                {
                    parts.AddRange(LegacyComplexFragmentIonName.ToIonChain(mods.LegacyCrosslinkMap, transitionProto.LinkedIons.Select(LegacyComplexFragmentIonName.FromLinkedIonProto)));
                }
                else
                {
                    foreach (var linkedIon in transitionProto.LinkedIons)
                    {
                        if (linkedIon.Orphan)
                        {
                            parts.Add(IonOrdinal.Empty);
                        }
                        else
                        {
                            parts.Add(new IonOrdinal(DataValues.FromIonType(linkedIon.IonType), linkedIon.Ordinal));
                        }
                    }
                }
                var complexFragmentIon = new NeutralFragmentIon(parts, losses);
                var chargedIon = new ComplexFragmentIon(transition, complexFragmentIon, mods);
                transitionDocNode = crosslinkBuilder.MakeTransitionDocNode(chargedIon, isotopeDist, annotations, transitionQuantInfo, explicitTransitionValues, null);
            }
            else
            {
                var mass = settings.GetFragmentMass(group, mods, transition, isotopeDist);
                transitionDocNode = new TransitionDocNode(transition, annotations, losses, mass, transitionQuantInfo, explicitTransitionValues, null);
            }

            return transitionDocNode;
        }

        public static IEnumerable<SkylineDocumentProto.Types.TransitionPeak> GetTransitionPeakProtos(
            Results<TransitionChromInfo> results, MeasuredResults measuredResults)
        {
            if (results == null)
            {
                yield break;
            }
            for (int replicateIndex = 0; replicateIndex < results.Count; replicateIndex++)
            {
                var replicateResults = results[replicateIndex];
                if (replicateResults.IsEmpty)
                {
                    continue;
                }
                foreach (var transitionChromInfo in replicateResults)
                {
                    if (transitionChromInfo == null)
                    {
                        continue;
                    }
                    var transitionPeak = new SkylineDocumentProto.Types.TransitionPeak();
                    transitionPeak.OptimizationStep = transitionChromInfo.OptimizationStep;
                    if (null != transitionChromInfo.Annotations)
                    {
                        transitionPeak.Annotations = transitionChromInfo.Annotations.ToProtoAnnotations();
                    }
                    transitionPeak.ReplicateIndex = replicateIndex;
                    transitionPeak.FileIndexInReplicate = measuredResults.Chromatograms[replicateIndex].IndexOfId(transitionChromInfo.FileId);
                    transitionPeak.MassError = transitionChromInfo.MassError;
                    transitionPeak.RetentionTime = transitionChromInfo.RetentionTime;
                    transitionPeak.StartRetentionTime = transitionChromInfo.StartRetentionTime;
                    transitionPeak.EndRetentionTime = transitionChromInfo.EndRetentionTime;
                    transitionPeak.IonMobility = transitionChromInfo.IonMobility.IonMobility.Mobility;
                    transitionPeak.IonMobilityWindow = transitionChromInfo.IonMobility.IonMobilityExtractionWindowWidth;
                    transitionPeak.IonMobilityCollisionCrossSection = transitionChromInfo.IonMobility.CollisionalCrossSectionSqA;
                    transitionPeak.Area = transitionChromInfo.Area;
                    transitionPeak.BackgroundArea = transitionChromInfo.BackgroundArea;
                    transitionPeak.Height = transitionChromInfo.Height;
                    transitionPeak.Fwhm = transitionChromInfo.Fwhm;
                    transitionPeak.IsFwhmDegenerate = transitionChromInfo.IsFwhmDegenerate;
                    transitionPeak.Truncated = DataValues.ToOptional(transitionChromInfo.IsTruncated);
                    transitionPeak.UserSet = DataValues.ToUserSet(transitionChromInfo.UserSet);
                    transitionPeak.ForcedIntegration = transitionChromInfo.IsForcedIntegration;
                    switch (transitionChromInfo.Identified)
                    {
                        case PeakIdentification.ALIGNED:
                            transitionPeak.Identified = SkylineDocumentProto.Types.PeakIdentification.Aligned;
                            break;
                        case PeakIdentification.FALSE:
                            transitionPeak.Identified = SkylineDocumentProto.Types.PeakIdentification.False;
                            break;
                        case PeakIdentification.TRUE:
                            transitionPeak.Identified = SkylineDocumentProto.Types.PeakIdentification.True;
                            break;
                    }
                    transitionPeak.Rank = transitionChromInfo.Rank;
                    transitionPeak.RankByLevel = transitionChromInfo.RankByLevel;
                    transitionPeak.PointsAcrossPeak = DataValues.ToOptional(transitionChromInfo.PointsAcrossPeak);
                    var peakShapeValues = transitionChromInfo.PeakShapeValues;
                    if (peakShapeValues.HasValue)
                    {
                        transitionPeak.PeakShapeValues =
                            new SkylineDocumentProto.Types.TransitionPeak.Types.PeakShapeValues
                            {
                                StdDev = peakShapeValues.Value.StdDev,
                                Skewness = peakShapeValues.Value.Skewness,
                                Kurtosis = peakShapeValues.Value.Kurtosis,
                                ShapeCorrelation = peakShapeValues.Value.ShapeCorrelation
                            };
                    }
                    yield return transitionPeak;
                }
            }
        }


        #region Property change methods

        public TransitionDocNode ChangeExplicitValues(ExplicitTransitionValues prop)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitValues = prop);
        }

        public TransitionDocNode ChangeQuantitative(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitQuantitative = prop);
        }

        public TransitionDocNode ChangeLibInfo(TransitionLibInfo prop)
        {
            return ChangeProp(ImClone(this), im => im.LibInfo = prop);
        }

        /// <summary>
        /// The peaks are not kept - see <see cref="_emptyResults"/> - so all this takes from
        /// <paramref name="prop"/> is how many replicates it covers.
        /// </summary>
        public TransitionDocNode ChangeResults(Results<TransitionChromInfo> prop)
        {
            return ChangeProp(ImClone(this), im => im._emptyResults = EmptyLike(prop));
        }

        public TransitionDocNode ChangeResultsRank(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.ResultsRank = prop);
        }

        /// <summary>
        /// Merges only what a transition node still holds. Its results belong to the precursor now,
        /// so <see cref="TransitionGroupDocNode.MergeUserInfo"/> merges those once the children of
        /// the merged precursor are settled.
        /// </summary>
        public TransitionDocNode MergeUserInfo(SrmSettings settings, TransitionDocNode nodeTranMerge)
        {
            var annotations = Annotations.Merge(nodeTranMerge.Annotations);
            if (ReferenceEquals(annotations, Annotations))
                return this;
            return (TransitionDocNode)ChangeAnnotations(annotations);
        }


        #endregion

        #region object overrides

        public bool Equals(TransitionDocNode obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            var equal =  base.Equals(obj) && obj.Mz == Mz &&
                   Equals(obj.IsotopeDistInfo, IsotopeDistInfo) &&
                   Equals(obj.LibInfo, LibInfo) &&
                   Equals(obj._emptyResults, _emptyResults) &&
                   Equals(obj.ExplicitQuantitative, ExplicitQuantitative);
            return equal;  // For debugging convenience
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as TransitionDocNode);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ Mz.GetHashCode();
                result = (result*397) ^ (IsotopeDistInfo != null ? IsotopeDistInfo.GetHashCode() : 0);
                result = (result*397) ^ (LibInfo != null ? LibInfo.GetHashCode() : 0);
                result = (result*397) ^ (_emptyResults != null ? _emptyResults.GetHashCode() : 0);
                result = (result*397) ^ ExplicitQuantitative.GetHashCode();
                return result;
            }
        }

        #endregion

        public struct TransitionQuantInfo
        {
            public static readonly TransitionQuantInfo DEFAULT = new TransitionQuantInfo(null, null, true);
            private bool _notQuantitative;
            public static TransitionQuantInfo GetTransitionQuantInfo(ComplexFragmentIon complexFragmentIon, IsotopeDistInfo isotopeDist, TypedMass massH, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> ranks)
            {
                var transitionIsotopeDistInfo = complexFragmentIon.IsMs1 ? GetIsotopeDistInfo(complexFragmentIon.PrimaryTransition, complexFragmentIon.Losses, isotopeDist) : null;
                return GetLibTransitionQuantInfo(complexFragmentIon.PrimaryTransition, complexFragmentIon.Losses, massH, ranks).ChangeIsotopeDistInfo(transitionIsotopeDistInfo);
            }

            public static TransitionQuantInfo GetLibTransitionQuantInfo(Transition transition, TransitionLosses losses, TypedMass massH,
                IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> ranks)
            {
                LibraryRankedSpectrumInfo.RankedMI rmi = null;
                if (ranks != null)
                {
                    ranks.TryGetValue(SequenceMassCalc.GetMZ(massH, transition.Adduct), out rmi);
                }
                TransitionLibInfo transitionLibInfo = null;
                if (rmi != null)
                {
                    transitionLibInfo = new TransitionLibInfo(rmi.Rank, rmi.Intensity);
                }
                return new TransitionQuantInfo(null, transitionLibInfo,
                    rmi == null || rmi.Quantitative);
            }

            public TransitionQuantInfo(TransitionIsotopeDistInfo isotopeDistInfo, TransitionLibInfo libInfo,
                bool quantitative) : this()
            {
                IsotopeDistInfo = isotopeDistInfo;
                LibInfo = libInfo;
                Quantititative = quantitative;
            }

            public TransitionIsotopeDistInfo IsotopeDistInfo { get; private set; }
            public TransitionLibInfo LibInfo { get; private set; }
            public bool Quantititative {
                get { return !_notQuantitative;}
                private set { _notQuantitative = !value; }
            }

            public TransitionQuantInfo UseValuesFrom(TransitionQuantInfo existing)
            {
                var isotopeDistInfo = IsotopeDistInfo;
                var libInfo = LibInfo;
                Helpers.AssignIfEquals(ref isotopeDistInfo, existing.IsotopeDistInfo);
                Helpers.AssignIfEquals(ref libInfo, existing.LibInfo);
                return new TransitionQuantInfo(isotopeDistInfo, libInfo, existing.Quantititative);
            }

            public TransitionQuantInfo ChangeLibInfo(TransitionLibInfo libInfo)
            {
                var quantInfo = this;
                quantInfo.LibInfo = libInfo;
                return quantInfo;
            }

            public TransitionQuantInfo ChangeIsotopeDistInfo(TransitionIsotopeDistInfo transitionIsotopeDistInfo)
            {
                var quantInfo = this;
                quantInfo.IsotopeDistInfo = transitionIsotopeDistInfo;
                if (transitionIsotopeDistInfo != null)
                {
                    quantInfo.LibInfo = null;
                }
                return quantInfo;
            }
        }

        public override string AuditLogText
        {
            get { return GetLabel(this, string.Empty); }
        }

        public TransitionDocNode ChangeTransitionGroup(TransitionGroup newTransitionGroup)
        {
            return ChangeTransitionId(new Transition(newTransitionGroup, Transition.IonType, Transition.CleavageOffset,
                Transition.MassIndex, Transition.Adduct, Transition.DecoyMassShift, Transition.CustomIon));
        }

        public TransitionDocNode ChangeTransitionId(Transition transition)
        {
            return (TransitionDocNode)ChangeId(transition);
        }
    }
}
