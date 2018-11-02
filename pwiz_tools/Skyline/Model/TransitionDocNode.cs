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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    public class TransitionDocNode : DocNode
    {
        public TransitionDocNode(Transition id,
                                 TransitionLosses losses,
                                 TypedMass massH,
                                 TransitionQuantInfo quantInfo)
            : this(id, Annotations.EMPTY, losses, massH, quantInfo, null)
        {
        }

        public TransitionDocNode(Transition id,
                                 Annotations annotations,
                                 TransitionLosses losses,
                                 TypedMass mass,
                                 TransitionQuantInfo transitionQuantInfo,
                                 Results<TransitionChromInfo> results)
            : base(id, annotations)
        {
            Losses = losses;
            if (losses != null)
                mass -= losses.Mass;
            Mz = id.IsCustom() ?
                  new SignedMz(id.Adduct.MzFromNeutralMass(mass), id.IsNegative()) : 
                  new SignedMz(SequenceMassCalc.GetMZ(mass, id.Adduct) + SequenceMassCalc.GetPeptideInterval(id.DecoyMassShift), id.IsNegative());
            MzMassType = mass.MassType;
            IsotopeDistInfo = transitionQuantInfo.IsotopeDistInfo;
            LibInfo = transitionQuantInfo.LibInfo;
            Results = results;
            ExplicitQuantitative = transitionQuantInfo.Quantititative;
        }

        public override AnnotationDef.AnnotationTarget AnnotationTarget { get { return AnnotationDef.AnnotationTarget.transition; } }

        public Transition Transition { get { return (Transition)Id; } }

        [TrackChildren(ignoreName:true, defaultValues:typeof(DefaultValuesNull))]
        public CustomIon CustomIon { get { return Transition.CustomIon; } }

        public TransitionLossKey Key(TransitionGroupDocNode parent)
        {
            return new TransitionLossKey(parent, this, Losses);
        }

        public TransitionLossEquivalentKey EquivalentKey(TransitionGroupDocNode parent)
        {
            return new TransitionLossEquivalentKey(parent, this, Losses); 
        }

        public MassType MzMassType { get; private set; }  // The massType used to calculate Mz
        public SignedMz Mz { get; private set; }

        // Returns molecule mass (or massH, for peptides)
        public TypedMass GetMoleculeMass()
        {
            Assume.IsTrue(Transition.IsCustom() || MzMassType.IsMassH());
            return Transition.IsCustom()
                ? Transition.Adduct.MassFromMz(Mz, MzMassType)
                : new TypedMass(SequenceMassCalc.GetMH(Mz, Transition.Charge), MzMassType);            
        }

        public bool IsDecoy { get { return Transition.DecoyMassShift.HasValue; } }

        public TransitionLosses Losses { get; private set; }

        public bool HasLoss { get { return Losses != null; } }

        public double LostMass { get { return HasLoss ? Losses.Mass : 0; } }

        public bool ExplicitQuantitative { get; private set; }

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

        public string GetFragmentIonName(CultureInfo cultureInfo, double? tolerance = null)
        {
            string ionName = Transition.GetFragmentIonName(cultureInfo, tolerance);
            return (HasLoss ? string.Format("{0} -{1}", ionName, Math.Round(Losses.Mass, 1)) : ionName); // Not L10N
        }

        /// <summary>
        /// Returns true for a transition that would be filtered from MS1 in full-scan filtering.
        /// </summary>
        public bool IsMs1
        {
            get { return Transition.IsPrecursor() && Losses == null; }
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
            if (isotopeDist == null || !transition.IsPrecursor())
                return true;
            int i = isotopeDist.MassIndexToPeakIndex(transition.MassIndex);
            return 0 <= i && i < isotopeDist.CountPeaks;
        }

        public TransitionLibInfo LibInfo { get; private set; }

        public bool HasLibInfo { get { return LibInfo != null; } }

        public IEnumerable<TransitionChromInfo> ChromInfos
        {
            get
            {
                if (HasResults)
                {
                    foreach (var result in Results)
                    {
                        if (result.IsEmpty)
                            continue;
                        foreach (var chromInfo in result)
                            yield return chromInfo;
                    }
                }
            }
        }

        public Results<TransitionChromInfo> Results { get; private set; }

        public int? ResultsRank { get; private set; }

        public bool HasResults { get { return Results != null; } }

        public IEnumerable<TransitionChromInfo> GetChromInfos(int? i)
        {
            if (!i.HasValue)
                return ChromInfos;
            var chromInfos = GetSafeChromInfo(i.Value);
            if (!chromInfos.IsEmpty)
                return chromInfos;
            return new TransitionChromInfo[0];
        }

        public ChromInfoList<TransitionChromInfo> GetSafeChromInfo(int i)
        {
            return HasResults && Results.Count > i ? Results[i] : default(ChromInfoList<TransitionChromInfo>);
        }

        public TransitionChromInfo GetChromInfoEntry(int i)
        {
            var result = GetSafeChromInfo(i);
            // CONSIDER: Also specify the file index and/or optimization step?
            if (!result.IsEmpty)
            {
                foreach (var chromInfo in result)
                {
                    if (chromInfo != null && chromInfo.OptimizationStep == 0)
                        return chromInfo;
                }
            }
            return null;
        }

        public TransitionChromInfo GetChromInfo(int resultsIndex, ChromFileInfoId chromFileInfoId)
        {
            return GetSafeChromInfo(resultsIndex).FirstOrDefault(chromInfo =>
                chromFileInfoId == null || ReferenceEquals(chromFileInfoId, chromInfo.FileId));
        }

        public float? GetPeakCountRatio(int i, bool integrateAll)
        {
            if (i == -1)
                return GetAveragePeakCountRatio(integrateAll);

            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return GetPeakCountRatio(chromInfo, integrateAll);
        }

        public float? GetAveragePeakCountRatio(bool integrateAll)
        {
            return GetAverageResultValue(chromInfo =>
                chromInfo.OptimizationStep != 0 ?
                    (float?)null : GetPeakCountRatio(chromInfo, integrateAll));
        }

        private static float GetPeakCountRatio(TransitionChromInfo chromInfo, bool integrateAll)
        {
            return chromInfo.IsGoodPeak(integrateAll) ? 1 : 0;
        }

        public float? GetPeakArea(int i)
        {
            if (i == -1)
                return AveragePeakArea;

            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return chromInfo.Area;
        }

        public float? AveragePeakArea
        {
            get
            {
                return GetAverageResultValue(chromInfo => chromInfo.OptimizationStep != 0
                                                              ? (float?) null
                                                              : chromInfo.Area);
            }
        }

        public bool IsUserModified
        {
            get
            {
                if (!Annotations.IsEmpty)
                    return true;
                return HasResults && Results.SelectMany(l => l)
                                         .Contains(chromInfo => chromInfo.IsUserModified);
            }
        }

        public int? GetPeakRank(int i)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo != null && chromInfo.Rank > 0)
                return chromInfo.Rank;
            return null;
        }

        public int? GetPeakRankByLevel(int i)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo != null && chromInfo.Rank > 0)
                return chromInfo.RankByLevel;
            return null;
        }

        public int? GetRank(int? i, bool useResults)
        {
            if (useResults && HasResults)
            {
                if (i.HasValue)
                    return GetPeakRank(i.Value);
                else
                    return ResultsRank;
            }
            else if (!useResults && HasLibInfo && LibInfo.Rank > 0)
                return LibInfo.Rank;
            return null;
        }

        public float? GetPeakAreaRatio(int i)
        {
            return GetPeakAreaRatio(i, 0);
        }

        public float? GetPeakAreaRatio(int i, int indexIS)
        {
            // CONSIDER: Also specify the file index?
            var chromInfo = GetChromInfoEntry(i);
            if (chromInfo == null)
                return null;
            return chromInfo.GetRatio(indexIS);
        }

        private float? GetAverageResultValue(Func<TransitionChromInfo, float?> getVal)
        {
            return HasResults ? Results.GetAverageValue(getVal) : null;
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
                                         null) {Mz = Mz, MzMassType = MzMassType};
        }

        public override string GetDisplayText(DisplaySettings settings)
        {
            return TransitionTreeNode.DisplayText(this, settings);    
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

        public SkylineDocumentProto.Types.Transition ToTransitionProto(SrmSettings settings)
        {
            var transitionProto = new SkylineDocumentProto.Types.Transition
            {
                FragmentType = DataValues.ToIonType(Transition.IonType),
                NotQuantitative = !ExplicitQuantitative
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
                transitionProto.IsotopeDistProportion = DataValues.ToOptional(IsotopeDistInfo.Proportion);
            }
            if (!Transition.IsPrecursor())
            {
                if (!Transition.IsCustom())
                {
                    transitionProto.FragmentOrdinal = Transition.Ordinal;
                    transitionProto.CalcNeutralMass = GetMoleculePersistentNeutralMass();
                }
                transitionProto.Charge = Transition.Charge;
                if (!Transition.Adduct.IsProteomic)
                {
                    transitionProto.Adduct = DataValues.ToOptional(Transition.Adduct.AsFormulaOrSignedInt());
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
            if (Results != null)
            {
                transitionProto.Results = new SkylineDocumentProto.Types.TransitionResults();
                transitionProto.Results.Peaks.AddRange(GetTransitionPeakProtos(settings.MeasuredResults));
            }
            return transitionProto;
        }

        private void SetCustomIonFragmentInfo(SkylineDocumentProto.Types.Transition transitionProto)
        {
            if (Transition.IsNonReporterCustomIon())
            {
                transitionProto.Formula = DataValues.ToOptional(Transition.CustomIon.Formula);
                if (Transition.CustomIon.AverageMass.IsMassH())
                    transitionProto.AverageMassH = DataValues.ToOptional(Transition.CustomIon.AverageMass);
                else
                    transitionProto.AverageMass = DataValues.ToOptional(Transition.CustomIon.AverageMass);
                if (Transition.CustomIon.MonoisotopicMass.IsMassH())
                    transitionProto.MonoMassH = DataValues.ToOptional(Transition.CustomIon.MonoisotopicMass);
                else
                    transitionProto.MonoMass = DataValues.ToOptional(Transition.CustomIon.MonoisotopicMass);
                transitionProto.CustomIonName = DataValues.ToOptional(Transition.CustomIon.Name);
                transitionProto.MoleculeId = DataValues.ToOptional(Transition.CustomIon.AccessionNumbers.ToString());
            }
            else
            {
                transitionProto.MeasuredIonName = DataValues.ToOptional(Transition.CustomIon.Name);
            }
        }

        public static TransitionDocNode FromTransitionProto(StringPool stringPool, SrmSettings settings,
            TransitionGroup group, ExplicitMods mods, IsotopeDistInfo isotopeDist,
            SkylineDocumentProto.Types.Transition transitionProto)
        {
            IonType ionType = DataValues.FromIonType(transitionProto.FragmentType);
            MeasuredIon measuredIon = null;
            if (transitionProto.MeasuredIonName != null)
            {
                measuredIon = settings.TransitionSettings.Filter.MeasuredIons.SingleOrDefault(
                    i => i.Name.Equals(transitionProto.MeasuredIonName.Value));
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
                    var formula = DataValues.FromOptional(transitionProto.Formula);
                    var moleculeID = MoleculeAccessionNumbers.FromString(DataValues.FromOptional(transitionProto.MoleculeId)); // Tab separated list of InChiKey, CAS etc
                    var monoMassH = DataValues.FromOptional(transitionProto.MonoMassH);
                    var averageMassH = DataValues.FromOptional(transitionProto.AverageMassH);
                    var monoMass = DataValues.FromOptional(transitionProto.MonoMass) ?? monoMassH;
                    var averageMass = DataValues.FromOptional(transitionProto.AverageMass) ?? averageMassH;
                    customIon = new CustomMolecule(formula,
                        new TypedMass(monoMass.Value, monoMassH.HasValue ? MassType.MonoisotopicMassH : MassType.Monoisotopic),
                        new TypedMass(averageMass.Value, averageMassH.HasValue ? MassType.AverageMassH : MassType.Average),
                        DataValues.FromOptional(transitionProto.CustomIonName), moleculeID);
                }
            }
            Transition transition;
            var adductString = DataValues.FromOptional(transitionProto.Adduct);
            var adduct = string.IsNullOrEmpty(adductString)
                ? Adduct.FromChargeProtonated(transitionProto.Charge)
                : Adduct.FromStringAssumeChargeOnly(adductString);
            if (isCustom)
            {
                transition = new Transition(group, isPrecursor ? group.PrecursorAdduct :adduct, transitionProto.MassIndex, customIon, ionType);
            }
            else if (isPrecursor)
            {
                transition = new Transition(group, ionType, group.Peptide.Length - 1, transitionProto.MassIndex,
                    group.PrecursorAdduct, DataValues.FromOptional(transitionProto.DecoyMassShift));
            }
            else
            {
                int offset = Transition.OrdinalToOffset(ionType, transitionProto.FragmentOrdinal,
                    group.Peptide.Length);
                transition = new Transition(group, ionType, offset, transitionProto.MassIndex, adduct, DataValues.FromOptional(transitionProto.DecoyMassShift));
            }
            var losses = TransitionLosses.FromLossProtos(settings, transitionProto.Losses);
            var mass = settings.GetFragmentMass(group, mods, transition, isotopeDist);
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
            var annotations = Annotations.FromProtoAnnotations(stringPool, transitionProto.Annotations);
            var results = TransitionChromInfo.FromProtoTransitionResults(stringPool, settings, transitionProto.Results);
            return new TransitionDocNode(transition, annotations, losses, mass, new TransitionQuantInfo(isotopeDistInfo, libInfo, !transitionProto.NotQuantitative), results);
        }


        public IEnumerable<SkylineDocumentProto.Types.TransitionPeak> GetTransitionPeakProtos(MeasuredResults measuredResults)
        {
            if (Results == null)
            {
                yield break;
            }
            for (int replicateIndex = 0; replicateIndex < Results.Count; replicateIndex++)
            {
                var replicateResults = Results[replicateIndex];
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
                    transitionPeak.MassError = DataValues.ToOptional(transitionChromInfo.MassError);
                    transitionPeak.RetentionTime = transitionChromInfo.RetentionTime;
                    transitionPeak.StartRetentionTime = transitionChromInfo.StartRetentionTime;
                    transitionPeak.EndRetentionTime = transitionChromInfo.EndRetentionTime;
                    transitionPeak.IonMobility = DataValues.ToOptional(transitionChromInfo.IonMobility.IonMobility.Mobility);
                    transitionPeak.IonMobilityWindow = DataValues.ToOptional(transitionChromInfo.IonMobility.IonMobilityExtractionWindowWidth);
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
                    yield return transitionPeak;
                }
            }
        }


        #region Property change methods

        public TransitionDocNode ChangeQuantitative(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.ExplicitQuantitative = prop);
        }

        public TransitionDocNode ChangeLibInfo(TransitionLibInfo prop)
        {
            return ChangeProp(ImClone(this), im => im.LibInfo = prop);
        }

        public TransitionDocNode ChangeResults(Results<TransitionChromInfo> prop)
        {
            return ChangeProp(ImClone(this), im => im.Results = prop);
        }

        public TransitionDocNode ChangeResultsRank(int? prop)
        {
            return ChangeProp(ImClone(this), im => im.ResultsRank = prop);
        }

        public DocNode ChangePeak(int indexSet, ChromFileInfoId fileId, int step, ChromPeak peak, IonMobilityFilter ionMobility, int ratioCount, UserSet userSet)
        {
            if (Results == null)
                return this;

            var listChromInfo = Results[indexSet];
            var listChromInfoNew = new List<TransitionChromInfo>();
            if (listChromInfo.IsEmpty)
                listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ionMobility, ratioCount, userSet));
            else
            {
                bool peakAdded = false;
                foreach (var chromInfo in listChromInfo)
                {
                    // Replace an existing entry with same index values
                    if (ReferenceEquals(chromInfo.FileId, fileId) && chromInfo.OptimizationStep == step)
                    {
                        // Something is wrong, if the value has already been added (duplicate peak? out of order?)
                        if (peakAdded)
                        {
                            throw new InvalidDataException(string.Format(Resources.TransitionDocNode_ChangePeak_Duplicate_or_out_of_order_peak_in_transition__0_,
                                                              FragmentIonName));
                        }
                        
                        // If the target peak is exactly the same as the proposed change and userSet is not overriding,
                        // simply return the original node unchanged
                        if (chromInfo.EquivalentTolerant(fileId, step, peak) && !chromInfo.UserSet.IsOverride(userSet))
                            return this;

                        listChromInfoNew.Add(chromInfo.ChangePeak(peak, userSet));
                        peakAdded = true;
                    }
                    else
                    {
                        // Entries should be ordered, so if the new entry has not been added
                        // when an entry past it is seen, then add the new entry.
                        if (!peakAdded &&
                            chromInfo.FileIndex >= fileId.GlobalIndex &&
                            chromInfo.OptimizationStep > step)
                        {
                            listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ionMobility, ratioCount, userSet));
                            peakAdded = true;
                        }
                        listChromInfoNew.Add(chromInfo);
                    }
                }
                // Finally, make sure the peak is added
                if (!peakAdded)
                    listChromInfoNew.Add(CreateChromInfo(fileId, step, peak, ionMobility, ratioCount, userSet));
            }

            return ChangeResults(Results.ChangeAt(indexSet, new ChromInfoList<TransitionChromInfo>(listChromInfoNew)));
        }

        private static TransitionChromInfo CreateChromInfo(ChromFileInfoId fileId, int step, ChromPeak peak, IonMobilityFilter ionMobility, int ratioCount, UserSet userSet)
        {
            return new TransitionChromInfo(fileId, step, peak, ionMobility, new float?[ratioCount], Annotations.EMPTY, userSet);
        }

        public DocNode RemovePeak(int indexSet, ChromFileInfoId fileId, UserSet userSet)
        {
            bool peakChanged = false;
            var listChromInfo = Results[indexSet];
            if (listChromInfo.IsEmpty)
                return this;
            var listChromInfoNew = new List<TransitionChromInfo>();
            foreach (var chromInfo in listChromInfo)
            {
                if (!ReferenceEquals(chromInfo.FileId, fileId))
                    listChromInfoNew.Add(chromInfo);
                else if (chromInfo.OptimizationStep == 0)
                {
                    if (!chromInfo.Equivalent(fileId, 0, ChromPeak.EMPTY, IonMobilityFilter.EMPTY))
                    {
                        listChromInfoNew.Add(chromInfo.ChangePeak(ChromPeak.EMPTY, userSet));
                        peakChanged = true;
                    }
                    else
                        listChromInfoNew.Add(chromInfo);
                }
            }
            if (listChromInfo.Count == listChromInfoNew.Count && !peakChanged)
                return this;
            return ChangeResults(Results.ChangeAt(indexSet, new ChromInfoList<TransitionChromInfo>(listChromInfoNew)));
        }

        public TransitionDocNode MergeUserInfo(SrmSettings settings, TransitionDocNode nodeTranMerge)
        {
            var result = this;
            var annotations = Annotations.Merge(nodeTranMerge.Annotations);
            if (!ReferenceEquals(annotations, Annotations))
                result = (TransitionDocNode)result.ChangeAnnotations(annotations);
            var resultsInfo = MergeResultsUserInfo(settings, nodeTranMerge.Results);
            if (!ReferenceEquals(resultsInfo, Results))
                result = result.ChangeResults(resultsInfo);
            return result;
        }


        private Results<TransitionChromInfo> MergeResultsUserInfo(
            SrmSettings settings, Results<TransitionChromInfo> results)
        {
            if (!HasResults)
                return Results;

            var dictFileIdToChromInfo = results.SelectMany(l => l)
                                               // Merge everything that does not already exist (handled below),
                                               // as merging only user modified causes loss of information in
                                               // updates
                                               //.Where(i => i.IsUserModified)
                                               .ToDictionary(i => i.FileIndex);

            var listResults = new List<ChromInfoList<TransitionChromInfo>>();
            for (int i = 0; i < results.Count; i++)
            {
                List<TransitionChromInfo> listChromInfo = null;
                var chromSet = settings.MeasuredResults.Chromatograms[i];
                var chromInfoList = Results[i];
                foreach (var fileInfo in chromSet.MSDataFileInfos)
                {
                    TransitionChromInfo chromInfo;
                    if (!dictFileIdToChromInfo.TryGetValue(fileInfo.FileIndex, out chromInfo))
                        continue;
                    if (listChromInfo == null)
                    {
                        listChromInfo = new List<TransitionChromInfo>(chromInfoList);
                    }
                    int iExist = listChromInfo.IndexOf(chromInfoExist =>
                                                       ReferenceEquals(chromInfoExist.FileId, chromInfo.FileId) &&
                                                       chromInfoExist.OptimizationStep == chromInfo.OptimizationStep);
                    if (iExist == -1)
                        listChromInfo.Add(chromInfo);
                    else if (chromInfo.IsUserModified)
                        listChromInfo[iExist] = chromInfo;
                }
                if (listChromInfo != null)
                    chromInfoList = new ChromInfoList<TransitionChromInfo>(listChromInfo);
                listResults.Add(chromInfoList);
            }
            if (ArrayUtil.InnerReferencesEqual<TransitionChromInfo, ChromInfoList<TransitionChromInfo>>(listResults, Results))
                return Results;
            return new Results<TransitionChromInfo>(listResults);
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
                   Equals(obj.Results, Results) &&
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
                result = (result*397) ^ (Results != null ? Results.GetHashCode() : 0);
                result = (result*397) ^ ExplicitQuantitative.GetHashCode();
                return result;
            }
        }

        #endregion

        public struct TransitionQuantInfo
        {
            public static readonly TransitionQuantInfo DEFAULT = new TransitionQuantInfo(null, null, true);
            private bool _notQuantitative;
            public static TransitionQuantInfo GetTransitionQuantInfo(Transition transition, TransitionLosses losses, IsotopeDistInfo isotopeDist, TypedMass massH, IDictionary<double, LibraryRankedSpectrumInfo.RankedMI> ranks)
            {
                var transitionIsotopeDistInfo = GetIsotopeDistInfo(transition, losses, isotopeDist);
                return GetLibTransitionQuantInfo(transition, losses, massH, ranks).ChangeIsotopeDistInfo(transitionIsotopeDistInfo);
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
            get { return TransitionTreeNode.GetLabel(this, string.Empty); }
        }
    }
}