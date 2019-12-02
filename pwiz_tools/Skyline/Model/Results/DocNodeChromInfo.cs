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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results
{
    /// <summary>
    /// Chromatogram results summary of a single <see cref="PeptideDocNode"/> calculated
    /// from its <see cref="TransitionGroupDocNode"/> children.
    /// </summary>
    public sealed class PeptideChromInfo : ChromInfo
    {
        private ImmutableList<PeptideLabelRatio> _labelRatios;

        public PeptideChromInfo(ChromFileInfoId fileId, float peakCountRatio, float? retentionTime,
                IList<PeptideLabelRatio> labelRatios)
            : base(fileId)
        {
            PeakCountRatio = peakCountRatio;
            RetentionTime = retentionTime;
            LabelRatios = labelRatios;
        }

        public float PeakCountRatio { get; private set; }
        public float? RetentionTime { get; private set; }
        public IList<PeptideLabelRatio> LabelRatios
        {
            get { return _labelRatios; }
            private set { _labelRatios = value as ImmutableList<PeptideLabelRatio> ?? MakeReadOnly(value); }
        }

        public bool ExcludeFromCalibration { get; private set; }

        public PeptideChromInfo ChangeExcludeFromCalibration(bool exclude)
        {
            return ChangeProp(ImClone(this), im => im.ExcludeFromCalibration = exclude);
        }

        #region object overrides

        public bool Equals(PeptideChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   other.PeakCountRatio == PeakCountRatio &&
                   other.RetentionTime.Equals(RetentionTime) &&
                   other.ExcludeFromCalibration.Equals(ExcludeFromCalibration) &&
                   ArrayUtil.EqualsDeep(other.LabelRatios, LabelRatios);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as PeptideChromInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ PeakCountRatio.GetHashCode();
                result = (result*397) ^ (RetentionTime.HasValue ? RetentionTime.Value.GetHashCode() : 0);
                result = (result * 397) ^ ExcludeFromCalibration.GetHashCode();
                result = (result * 397) ^ LabelRatios.GetHashCodeDeep();
                return result;
            }
        }

        #endregion
    }

    public struct PeptideLabelRatio
    {
        public PeptideLabelRatio(IsotopeLabelType labelType, IsotopeLabelType standardType,
            RatioValue ratio) : this()
        {
            LabelType = labelType;
            StandardType = standardType;
            Ratio = ratio;
        }

        public IsotopeLabelType LabelType { get; private set; }
        public IsotopeLabelType StandardType { get; private set; }
        public RatioValue Ratio { get; private set; }

        #region object overrides

        public bool Equals(PeptideLabelRatio other)
        {
            return Equals(other.LabelType, LabelType) &&
                Equals(other.StandardType, StandardType) &&
                Equals(other.Ratio, Ratio);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (obj.GetType() != typeof (PeptideLabelRatio)) return false;
            return Equals((PeptideLabelRatio) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = LabelType.GetHashCode();
                result = (result*397) ^ (StandardType == null ? 0 : StandardType.GetHashCode());
                result = (result*397) ^ (Ratio == null ? 0 : Ratio.GetHashCode());
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Chromatogram results summary of a single <see cref="TransitionGroupDocNode"/> from
    /// a single raw file of a single replicate.
    /// </summary>
    public sealed class TransitionGroupChromInfo : ChromInfo
    {
        private static readonly IList<RatioValue>[] EMPTY_RATIOS  = new ImmutableList<RatioValue>[4];

        static TransitionGroupChromInfo()
        {
            for (int i = 0; i < EMPTY_RATIOS.Length; i++)
            {
                EMPTY_RATIOS[i] = ImmutableList<RatioValue>.ValueOf(new RatioValue[i + 1]);
            }
        }

        public static IList<RatioValue> GetEmptyRatios(int countRatios)
        {
            int i = countRatios - 1;
            return i < EMPTY_RATIOS.Length ? EMPTY_RATIOS[i] : new RatioValue[countRatios];
        }

        private ImmutableList<RatioValue> _ratios;

        public TransitionGroupChromInfo(ChromFileInfoId fileId,
                                        int optimizationStep,
                                        float peakCountRatio,
                                        float? retentionTime,
                                        float? startTime,
                                        float? endTime,
                                        TransitionGroupIonMobilityInfo ionMobilityInfo,
                                        float? fwhm,
                                        float? area,
                                        float? areaMs1,
                                        float? areaFragment,
                                        float? backgroundArea,
                                        float? backgroundAreaMs1,
                                        float? backgroundAreaFragment,
                                        float? height,
                                        IList<RatioValue> ratios,
                                        float? massError,
                                        int? truncated,
                                        PeakIdentification identified,
                                        float? libraryDotProduct,
                                        float? isotopeDotProduct,
                                        float? qvalue,
                                        float? zscore,
                                        Annotations annotations,
                                        UserSet userSet)
            : base(fileId)
        {
            OptimizationStep = optimizationStep;
            PeakCountRatio = peakCountRatio;
            RetentionTime = retentionTime;
            StartRetentionTime = startTime;
            EndRetentionTime = endTime;
            IonMobilityInfo = ionMobilityInfo ?? TransitionGroupIonMobilityInfo.EMPTY;
            Fwhm = fwhm;
            Area = area;
            AreaMs1 = areaMs1;
            AreaFragment = areaFragment;
            BackgroundArea = backgroundArea;
            BackgroundAreaMs1 = backgroundAreaMs1;
            BackgroundAreaFragment = backgroundAreaFragment;
            Height = height;
            Ratios = ratios;
            MassError = massError;
            Truncated = truncated;
            Identified = identified;
            LibraryDotProduct = libraryDotProduct;
            IsotopeDotProduct = isotopeDotProduct;
            QValue = qvalue;
            ZScore = zscore;
            Annotations = annotations;
            UserSet = userSet;
        }

        public int OptimizationStep { get; private set; }

        public float PeakCountRatio { get; private set; }
        public float? RetentionTime { get; private set; }
        public float? StartRetentionTime { get; private set; }
        public float? EndRetentionTime { get; private set; }
        public TransitionGroupIonMobilityInfo IonMobilityInfo { get; private set; }
        public float? Fwhm { get; private set; }
        public float? Area { get; private set; }
        public float? AreaMs1 { get; private set; }
        public float? AreaFragment { get; private set; }
        public float? BackgroundArea { get; private set; }
        public float? BackgroundAreaMs1 { get; private set; }
        public float? BackgroundAreaFragment { get; private set; }
        public float? Height { get; private set; }
        public float? Ratio { get { return _ratios[0] == null ? (float?) null : _ratios[0].Ratio; } }
        public IList<RatioValue> Ratios
        {
            get { return _ratios; }
            private set { _ratios = value as ImmutableList<RatioValue> ?? MakeReadOnly(value); }
        }
        public float? MassError { get; private set; }
        public int? Truncated { get; private set; }
        public PeakIdentification Identified { get; private set; }
        public bool IsIdentified { get { return Identified != PeakIdentification.FALSE; } }
        public float? LibraryDotProduct { get; private set; }
        public float? IsotopeDotProduct { get; private set; }
        public float? QValue { get; private set; }
        public float? ZScore { get; private set; }
        public Annotations Annotations { get; private set; }

        public RatioValue GetRatio(int index)
        {
            return index != RATIO_INDEX_GLOBAL_STANDARDS
                       ? _ratios[index]
                       : _ratios[_ratios.Count - 1];
        }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public UserSet UserSet { get; private set; }

        public bool IsUserSetManual { get { return UserSet == UserSet.TRUE; } }

        public bool IsUserSetAuto { get { return UserSet == UserSet.IMPORTED || UserSet == UserSet.REINTEGRATED; } }

        public bool IsUserSetMatched { get { return UserSet == UserSet.MATCHED; }}

        public bool IsUserModified { get { return IsUserSetManual || !Annotations.IsEmpty; } }

        #region Property change methods

        public TransitionGroupChromInfo ChangeRatios(IList<RatioValue> prop)
        {
            return ChangeProp(ImClone(this), im => im.Ratios = prop);
        }

        public TransitionGroupChromInfo ChangeAnnotations(Annotations annotations)
        {
            if (Equals(annotations, Annotations))
                return this;
            return ChangeProp(ImClone(this), im => im.Annotations = annotations);
        }

        public TransitionGroupChromInfo ChangeUserSet(UserSet prop)
        {
            return ChangeProp(ImClone(this), im => im.UserSet = prop);
        }

        public TransitionGroupChromInfo ChangeLibraryDotProduct(float? prop)
        {
            return ChangeProp(ImClone(this), im => im.LibraryDotProduct = prop);
        }

        public TransitionGroupChromInfo ChangeScore(float qvalue, float score)
        {
            return ChangeProp(ImClone(this), im =>
            {
                im.QValue = qvalue;
                im.ZScore = score;
            });
        }
        #endregion

        #region object overrides

        public bool Equals(TransitionGroupChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            bool result = base.Equals(other) &&
                   other.PeakCountRatio == PeakCountRatio &&
                   other.RetentionTime.Equals(RetentionTime) &&
                   other.StartRetentionTime.Equals(StartRetentionTime) &&
                   other.EndRetentionTime.Equals(EndRetentionTime) &&
                   Equals(other.IonMobilityInfo, IonMobilityInfo) &&
                   other.Fwhm.Equals(Fwhm) &&
                   other.Area.Equals(Area) &&
                   other.AreaMs1.Equals(AreaMs1) &&
                   other.AreaFragment.Equals(AreaFragment) &&
                   other.BackgroundArea.Equals(BackgroundArea) &&
                   other.BackgroundAreaMs1.Equals(BackgroundAreaMs1) &&
                   other.BackgroundAreaFragment.Equals(BackgroundAreaFragment) &&
                   other.Height.Equals(Height) &&
                   ArrayUtil.EqualsDeep(other.Ratios, Ratios) &&
                   other.Truncated.Equals(Truncated) &&
                   other.Identified.Equals(Identified) &&
                   other.LibraryDotProduct.Equals(LibraryDotProduct) &&
                   other.IsotopeDotProduct.Equals(IsotopeDotProduct) &&
                   other.QValue.Equals(QValue) &&
                   other.ZScore.Equals(ZScore) &&
                   other.Annotations.Equals(Annotations) &&
                   other.OptimizationStep.Equals(OptimizationStep) &&
                   other.Annotations.Equals(Annotations) &&
                   other.UserSet.Equals(UserSet);
            return result;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as TransitionGroupChromInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ PeakCountRatio.GetHashCode();
                result = (result*397) ^ (RetentionTime.HasValue ? RetentionTime.Value.GetHashCode() : 0);
                result = (result*397) ^ (StartRetentionTime.HasValue ? StartRetentionTime.Value.GetHashCode() : 0);
                result = (result*397) ^ (EndRetentionTime.HasValue ? EndRetentionTime.Value.GetHashCode() : 0);
                result = (result*397) ^ IonMobilityInfo.GetHashCode();
                result = (result*397) ^ (Fwhm.HasValue ? Fwhm.Value.GetHashCode() : 0);
                result = (result*397) ^ (Area.HasValue ? Area.Value.GetHashCode() : 0);
                result = (result*397) ^ (AreaMs1.HasValue ? AreaMs1.Value.GetHashCode() : 0);
                result = (result*397) ^ (AreaFragment.HasValue ? AreaFragment.Value.GetHashCode() : 0);
                result = (result*397) ^ (BackgroundArea.HasValue ? BackgroundArea.Value.GetHashCode() : 0);
                result = (result*397) ^ (BackgroundAreaMs1.HasValue ? BackgroundAreaMs1.Value.GetHashCode() : 0);
                result = (result*397) ^ (BackgroundAreaFragment.HasValue ? BackgroundAreaFragment.Value.GetHashCode() : 0);
                result = (result*397) ^ (Height.HasValue ? Height.Value.GetHashCode() : 0);
                result = (result*397) ^ Ratios.GetHashCodeDeep();
                result = (result*397) ^ (Truncated.HasValue ? Truncated.Value.GetHashCode() : 0);
                result = (result*397) ^ Identified.GetHashCode();
                result = (result*397) ^ (LibraryDotProduct.HasValue ? LibraryDotProduct.Value.GetHashCode() : 0);
                result = (result*397) ^ (IsotopeDotProduct.HasValue ? IsotopeDotProduct.Value.GetHashCode() : 0);
                result = (result*397) ^ (QValue.HasValue ? QValue.Value.GetHashCode() : 0);
                result = (result*397) ^ (ZScore.HasValue ? ZScore.Value.GetHashCode() : 0);
                result = (result*397) ^ OptimizationStep;
                result = (result*397) ^ Annotations.GetHashCode();
                result = (result*397) ^ UserSet.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Chromatogram results summary of a single <see cref="TransitionDocNode"/> from
    /// a single raw file of a single replicate.
    /// </summary>
    public sealed class TransitionChromInfo : ChromInfo
    {
        private static readonly IList<float?>[] EMPTY_RATIOS  = new ImmutableList<float?>[4];

        static TransitionChromInfo()
        {
            for (int i = 0; i < EMPTY_RATIOS.Length; i++)
            {
                EMPTY_RATIOS[i] = ImmutableList<float?>.ValueOf(new float?[i + 1]);
            }
        }

        public static IList<float?> GetEmptyRatios(int countRatios)
        {
            int i = countRatios - 1;
            return i < EMPTY_RATIOS.Length ? EMPTY_RATIOS[i] : new float?[countRatios];
        }

        private ImmutableList<float?> _ratios;

        public TransitionChromInfo(float startRetentionTime, float endRetentionTime)
            : base(null)
        {
            StartRetentionTime = startRetentionTime;
            EndRetentionTime = endRetentionTime;
        }

        public TransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, ChromPeak peak,
            IonMobilityFilter ionMobility,
            IList<float?> ratios, Annotations annotations, UserSet userSet)
            : this(fileId, optimizationStep, peak.MassError, peak.RetentionTime, peak.StartTime, peak.EndTime,
                   ionMobility,
                   peak.Area, peak.BackgroundArea, peak.Height, peak.Fwhm,
                   peak.IsFwhmDegenerate, peak.IsTruncated, 
                   peak.PointsAcross, 
                   peak.Identified, 0, 0,
                   ratios, annotations, userSet, peak.IsForcedIntegration)
        {
        }

        public TransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, float? massError,
                                   float retentionTime, float startRetentionTime, float endRetentionTime,
                                   IonMobilityFilter ionMobility,
                                   float area, float backgroundArea, float height,
                                   float fwhm, bool fwhmDegenerate, bool? truncated, short? pointsAcrossPeak,
                                   PeakIdentification identified, short rank, short rankByLevel,
                                   IList<float?> ratios, Annotations annotations, UserSet userSet, bool isForcedIntegration)
            : base(fileId)
        {
            OptimizationStep = optimizationStep;
            MassError = massError;
            RetentionTime = retentionTime;
            StartRetentionTime = startRetentionTime;
            EndRetentionTime = endRetentionTime;
            IonMobility = ionMobility;
            Area = area;
            BackgroundArea = backgroundArea;
            Height = height;
            Fwhm = fwhm;
            // Crawdad can set FWHM to NaN. Need to protect against that here.
            if (float.IsNaN(fwhm))
                Fwhm = 0;
            IsFwhmDegenerate = fwhmDegenerate;
            IsTruncated = truncated;
            Identified = identified;
            Rank = rank;
            RankByLevel = rankByLevel;
            Ratios = ratios;
            Annotations = annotations;
            UserSet = userSet;
            PointsAcrossPeak = pointsAcrossPeak;
            IsForcedIntegration = isForcedIntegration;
        }

        /// <summary>
        /// Set to the number of steps from the regression value for a
        /// transition attribute which can be optimized or calculated using
        /// a linear regression (e.g. CE, DP, CV)
        /// </summary>
        public int OptimizationStep { get; private set; }

        public float? MassError { get; private set; }
        public float RetentionTime { get; private set; }
        public float StartRetentionTime { get; private set; }
        public float EndRetentionTime { get; private set; }
        public IonMobilityFilter IonMobility { get; private set; } // The actual ion mobility used for this transition
        public float Area { get; private set; }
        public float BackgroundArea { get; private set; }
        public float Height { get; private set; }
        public float Fwhm { get; private set; }
        public bool IsFwhmDegenerate { get; private set; }
        public bool? IsTruncated { get; private set; }
        public bool IsIdentified { get { return Identified != PeakIdentification.FALSE; } }
        public PeakIdentification Identified { get; private set; }
        public short Rank { get; private set; }
        public short RankByLevel { get; private set; }
        public short? PointsAcrossPeak { get; private set; }
        public bool IsForcedIntegration { get; private set; }

        public bool IsGoodPeak(bool integrateAll)
        {
            if (integrateAll)
            {
                return Area > 0;
            }
            return Area > 0 && !IsForcedIntegration;
        }

        /// <summary>
        /// Set after creation at the peptide results calculation level
        /// </summary>
        public IList<float?> Ratios
        {
            get { return _ratios; }
            private set { _ratios = value as ImmutableList<float?> ?? MakeReadOnly(value); }
        }
        public float? Ratio { get { return _ratios[0]; } }

        public float? GetRatio(int index)
        {
            return index != RATIO_INDEX_GLOBAL_STANDARDS
                ? _ratios[index]
                : _ratios[_ratios.Count - 1];
        }

        public Annotations Annotations { get; private set; }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public UserSet UserSet { get; private set; }

        public bool IsUserSetManual { get { return UserSet == UserSet.TRUE; } }

        public bool IsUserSetAuto { get { return UserSet == UserSet.IMPORTED || UserSet == UserSet.REINTEGRATED; } }

        public bool IsUserSetMatched { get { return UserSet == UserSet.MATCHED; } }

        public bool IsUserModified { get { return !Equals(UserSet, UserSet.FALSE) || !Annotations.IsEmpty; } }

        public bool IsEmpty { get { return EndRetentionTime == 0; } }

        public double GetMatchingQValue(ChromInfoList<TransitionGroupChromInfo> chromGroupInfos)
        {
            // For now, brute-force, because these lists should be very short and not commonly more than a single entry
            foreach (var chromGroupInfo in chromGroupInfos)
            {
                if (ReferenceEquals(FileId, chromGroupInfo.FileId) &&
                    OptimizationStep == chromGroupInfo.OptimizationStep)
                {
                    return chromGroupInfo.QValue ?? 1;
                }
            }
            return 1;
        }

        /// <summary>
        /// Used in <see cref="TransitionGroupDocNode.ChangeResults"/> so compare both peak and ion mobility
        /// </summary>
        public bool Equivalent(ChromFileInfoId fileId, int step, ChromPeak peak, IonMobilityFilter ionMobilityFilter)
        {
            return ReferenceEquals(fileId, FileId) &&
                   step == OptimizationStep &&
                   Equals(IonMobility, ionMobilityFilter) &&    // Unlikely to change, but still confirm
                   Equals(peak.MassError, MassError) &&
                   peak.RetentionTime == RetentionTime &&
                   peak.StartTime == StartRetentionTime &&
                   peak.EndTime == EndRetentionTime &&
                   peak.Area == Area &&
                   peak.BackgroundArea == BackgroundArea &&
                   peak.Height == Height &&
                   peak.Fwhm == Fwhm &&
                   peak.IsFwhmDegenerate == IsFwhmDegenerate &&
                   Equals(peak.IsTruncated, IsTruncated) &&
                   Equals(peak.Identified, Identified);
        }

        /// <summary>
        /// Used to validate need for a <see cref="ChangePeak"/>, so only compare peak informatio
        /// </summary>
        public bool EquivalentTolerant(ChromFileInfoId fileId, int step, ChromPeak peak)
        {
            const double tol = 1e-4;
            return ReferenceEquals(fileId, FileId) &&
                   step == OptimizationStep &&
                   Equals(peak.MassError, MassError) &&
                   IsEqualTolerant(peak.RetentionTime, RetentionTime, tol) &&
                   IsEqualTolerant(peak.StartTime, StartRetentionTime, tol) &&
                   IsEqualTolerant(peak.EndTime, EndRetentionTime, tol) &&
                   IsEqualTolerant(peak.Area, Area, tol * Area)  &&
                   IsEqualTolerant(peak.BackgroundArea, BackgroundArea, tol * BackgroundArea) &&
                   IsEqualTolerant(peak.Height, Height, tol * Height)  &&
                   (IsEqualTolerant(peak.Fwhm, Fwhm, tol * Fwhm)  || Equals(peak.Fwhm,Fwhm)) &&
                   Equals(peak.IsFwhmDegenerate, IsFwhmDegenerate) &&
                   Equals(peak.IsTruncated, IsTruncated) &&
                   Equals(peak.Identified, Identified);
        }

        public bool IsEqualTolerant(double a, double b, double tol)
        {
            return Math.Abs(a - b) <= tol;
        }

        #region Property change methods

        public TransitionChromInfo ChangePeak(ChromPeak peak, UserSet userSet)
        {
            var chromInfo = ImClone(this);
            chromInfo.MassError = peak.MassError;
            chromInfo.RetentionTime = peak.RetentionTime;
            chromInfo.StartRetentionTime = peak.StartTime;
            chromInfo.EndRetentionTime = peak.EndTime;
            chromInfo.Area = peak.Area;
            chromInfo.BackgroundArea = peak.BackgroundArea;
            chromInfo.Height = peak.Height;
            chromInfo.Fwhm = peak.Fwhm;
            // Crawdad can set FWHM to NaN. Need to protect against that here.
            if (float.IsNaN(peak.Fwhm))
                chromInfo.Fwhm = 0;
            chromInfo.IsFwhmDegenerate = peak.IsFwhmDegenerate;
            chromInfo.IsTruncated = peak.IsTruncated;
            chromInfo.Identified = peak.Identified;
            chromInfo.UserSet = userSet;
            chromInfo.PointsAcrossPeak = peak.PointsAcross;
            chromInfo.IsForcedIntegration = peak.IsForcedIntegration;
            return chromInfo;
        }

        public TransitionChromInfo ChangeRatios(IList<float?> prop)
        {
            return ChangeProp(ImClone(this), im => im.Ratios = prop);
        }

        /// <summary>
        /// Because creating a copy shows up in a profiler, and this is currently only used
        /// during calculation of this object, a copy flag was added to allow modified
        /// immutability with direct setting allowed during extended creation time.
        /// </summary>
        public TransitionChromInfo ChangeRank(bool copy, short prop, short propByLevel)
        {
            if (!copy)
            {
                Rank = prop;
                RankByLevel = propByLevel;
                return this;
            }
            return ChangeProp(ImClone(this), im =>
            {
                im.Rank = prop;
                im.RankByLevel = propByLevel;
            });
        }

        public TransitionChromInfo ChangeAnnotations(Annotations annotations)
        {
            if (Equals(annotations, Annotations))
                return this;
            return ChangeProp(ImClone(this), im => im.Annotations = annotations);
        }

        public TransitionChromInfo ChangeUserSet(UserSet prop)
        {
            return ChangeProp(ImClone(this), im => im.UserSet = prop);
        }

        public TransitionChromInfo ChangeIsForcedIntegration(bool isForcedCoelution)
        {
            return ChangeProp(ImClone(this), im => im.IsForcedIntegration = isForcedCoelution);
        }

        #endregion

        #region object overrides

        public bool Equals(TransitionChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            var result =  base.Equals(other) &&
                   Equals(other.MassError, MassError) &&
                   other.RetentionTime == RetentionTime &&
                   other.StartRetentionTime == StartRetentionTime &&
                   other.EndRetentionTime == EndRetentionTime &&
                   other.Area == Area &&
                   other.BackgroundArea == BackgroundArea &&
                   other.Height == Height &&
                   other.Fwhm == Fwhm &&
                   other.IsFwhmDegenerate.Equals(IsFwhmDegenerate) &&
                   Equals(other.IsTruncated, IsTruncated) &&
                   Equals(other.Identified, Identified) &&
                   other.Rank == Rank &&
                   other.RankByLevel == RankByLevel &&
                   ArrayUtil.EqualsDeep(other.Ratios, Ratios) &&
                   other.OptimizationStep.Equals(OptimizationStep) &&
                   other.Annotations.Equals(Annotations) &&
                   other.UserSet.Equals(UserSet) &&
                   Equals(other.IonMobility, IonMobility) &&
                   other.PointsAcrossPeak.Equals(PointsAcrossPeak) &&
                   Equals(IsForcedIntegration, other.IsForcedIntegration);
            return result; // For ease of breakpoint setting
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as TransitionChromInfo);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (MassError.HasValue ? MassError.Value.GetHashCode() : 0);
                result = (result*397) ^ RetentionTime.GetHashCode();
                result = (result*397) ^ StartRetentionTime.GetHashCode();
                result = (result*397) ^ EndRetentionTime.GetHashCode();
                result = (result*397) ^ Area.GetHashCode();
                result = (result*397) ^ BackgroundArea.GetHashCode();
                result = (result*397) ^ Height.GetHashCode();
                result = (result*397) ^ Fwhm.GetHashCode();
                result = (result*397) ^ IsFwhmDegenerate.GetHashCode();
                result = (result*397) ^ Identified.GetHashCode();
                result = (result*397) ^ (IsTruncated.HasValue ? IsTruncated.Value.GetHashCode() : 0);
                result = (result*397) ^ Rank;
                result = (result*397) ^ RankByLevel;
                result = (result*397) ^ Ratios.GetHashCodeDeep();
                result = (result*397) ^ OptimizationStep.GetHashCode();
                result = (result*397) ^ Annotations.GetHashCode();
                result = (result*397) ^ UserSet.GetHashCode();
                result = (result*397) ^ IonMobility.GetHashCode();
                result = (result*397) ^ PointsAcrossPeak.GetHashCode();
                result = (result*397) ^ IsForcedIntegration.GetHashCode();
                return result;
            }
        }

        #endregion

        public static Results<TransitionChromInfo> FromProtoTransitionResults(AnnotationScrubber annotationScrubber, SrmSettings settings,
            SkylineDocumentProto.Types.TransitionResults transitionResults)
        {
            if (transitionResults == null)
            {
                return null;
            }

            var measuredResults = settings.MeasuredResults;
            var peaksByReplicate = transitionResults.Peaks.ToLookup(peak => peak.ReplicateIndex);
            var lists = new List<ChromInfoList<TransitionChromInfo>>();
            for (int replicateIndex = 0; replicateIndex < measuredResults.Chromatograms.Count; replicateIndex++)
            {
                var transitionChromInfos = peaksByReplicate[replicateIndex]
                    .Select(transitionPeak => FromProtoTransitionPeak(annotationScrubber, settings, transitionPeak)).ToArray();
                lists.Add(new ChromInfoList<TransitionChromInfo>(transitionChromInfos));
            }
            return new Results<TransitionChromInfo>(lists);
        }

        private static TransitionChromInfo FromProtoTransitionPeak(AnnotationScrubber annotationScrubber, SrmSettings settings,
            SkylineDocumentProto.Types.TransitionPeak transitionPeak)
        {
            var measuredResults = settings.MeasuredResults;
            var msDataFileInfo = measuredResults.Chromatograms[transitionPeak.ReplicateIndex]
                .MSDataFileInfos[transitionPeak.FileIndexInReplicate];
            var fileId = msDataFileInfo.FileId;
            var ionMobilityValue = DataValues.FromOptional(transitionPeak.IonMobility);
            IonMobilityFilter ionMobility;
            if (ionMobilityValue.HasValue)
            {
                var ionMobilityWidth = DataValues.FromOptional(transitionPeak.IonMobilityWindow);
                var ionMobilityUnits = msDataFileInfo.IonMobilityUnits;
                ionMobility = IonMobilityFilter.GetIonMobilityFilter(IonMobilityValue.GetIonMobilityValue(ionMobilityValue, ionMobilityUnits), ionMobilityWidth, null);
            }
            else
            {
                ionMobility = IonMobilityFilter.EMPTY;
            }
            short? pointsAcrossPeak = (short?) DataValues.FromOptional(transitionPeak.PointsAcrossPeak);
            PeakIdentification peakIdentification = PeakIdentification.FALSE;
            switch (transitionPeak.Identified)
            {
                case SkylineDocumentProto.Types.PeakIdentification.Aligned:
                    peakIdentification = PeakIdentification.ALIGNED;
                    break;
                case SkylineDocumentProto.Types.PeakIdentification.True:
                    peakIdentification = PeakIdentification.TRUE;
                    break;
            }
            return new TransitionChromInfo(
                fileId, 
                transitionPeak.OptimizationStep,
                DataValues.FromOptional(transitionPeak.MassError),
                transitionPeak.RetentionTime,
                transitionPeak.StartRetentionTime,
                transitionPeak.EndRetentionTime,
                ionMobility,
                transitionPeak.Area,
                transitionPeak.BackgroundArea,
                transitionPeak.Height,
                transitionPeak.Fwhm,
                transitionPeak.IsFwhmDegenerate,
                DataValues.FromOptional(transitionPeak.Truncated),
                pointsAcrossPeak,
                peakIdentification,
                (short) transitionPeak.Rank,
                (short) transitionPeak.RankByLevel,
                GetEmptyRatios(settings.PeptideSettings.Modifications.RatioInternalStandardTypes.Count),
                annotationScrubber.ScrubAnnotations(Annotations.FromProtoAnnotations(transitionPeak.Annotations), AnnotationDef.AnnotationTarget.transition_result), 
                DataValues.FromUserSet(transitionPeak.UserSet),
                transitionPeak.ForcedIntegration
                );
        }
    }

    /// <summary>
    /// Chromatogram results summary data for a single <see cref="DocNode"/>.
    /// This list will contain one element per replicate (i.e. full run of the nodes
    /// in this document), which may take one or more injections into the instrument to
    /// accomplish.
    /// 
    /// The set of injections is encapsulated in the <see cref="ChromatogramSet"/> class,
    /// and is not relevant to this class.  The elements in this class are ordered
    /// to correspond to the elements in the document's <see cref="ChromatogramSet"/> list
    /// in <see cref="SrmSettings.MeasuredResults"/>.  This collection will have the same
    /// number of items as the chromatograms list.
    /// </summary>
    public sealed class Results<TItem> : AbstractReadOnlyList<ChromInfoList<TItem>>
        where TItem : ChromInfo
    {
        private readonly ImmutableList<ChromInfoList<TItem>> _list;
        public Results(params ChromInfoList<TItem>[] elements)
        {
            _list = ImmutableList.ValueOf(elements);
        }

        public Results(IList<ChromInfoList<TItem>> elements)
        {
            _list = ImmutableList.ValueOf(elements);
        }

        public override int Count
        {
            get { return _list.Count; }
        }

        public override ChromInfoList<TItem> this[int index]
        {
            get { return _list[index]; }
        }

        public Results<TItem> ChangeAt(int index, ChromInfoList<TItem> list)
        {
            return new Results<TItem>(_list.ReplaceAt(index, list));
        }

        public static Results<TItem> Merge(Results<TItem> resultsOld, List<IList<TItem>> chromInfoSet)
        {
            // Check for equal results in the same positions, and swap in the old
            // values if found to maintain reference equality.
            if (resultsOld != null)
            {
                for (int i = 0, len = Math.Min(resultsOld.Count, chromInfoSet.Count); i < len; i++)
                {
                    if (ArrayUtil.ReferencesEqual(resultsOld[i], chromInfoSet[i]) || EqualsDeep(resultsOld[i], chromInfoSet[i]))
                        chromInfoSet[i] = resultsOld[i];
                }
            }
            var listInfo = chromInfoSet.ConvertAll(l => new ChromInfoList<TItem>(l));
            if (ArrayUtil.InnerReferencesEqual<TItem, ChromInfoList<TItem>>(listInfo, resultsOld))
                return resultsOld;

            return new Results<TItem>(listInfo);
        }

        public static Results<TItem> ChangeChromInfo(Results<TItem> results, ChromFileInfoId id, TItem newChromInfo)
        {
            var elements = new List<ChromInfoList<TItem>>();
            bool found = false;
            foreach (var replicate in results)
            {
                var chromInfoList = new List<TItem>();
                if (replicate.IsEmpty)
                {
                    elements.Add(default(ChromInfoList<TItem>));
                    continue;
                }
                foreach (var chromInfo in replicate)
                {
                    if (!ReferenceEquals(chromInfo.FileId, id))
                        chromInfoList.Add(chromInfo);
                    else
                    {
                        found = true;
                        chromInfoList.Add(newChromInfo);
                    }
                }
                elements.Add(new ChromInfoList<TItem>(chromInfoList));
            }
            if (!found)
                throw new InvalidOperationException(Resources.ResultsGrid_ChangeChromInfo_Element_not_found);
            return new Results<TItem>(elements);
        }

        public static bool EqualsDeep(Results<TItem> resultsOld, Results<TItem> results)
        {
            if (resultsOld == null && results == null)
                return true;
            if (resultsOld == null || results == null)
                return false;
            if (resultsOld.Count != results.Count)
                return false;
            for (int i = 0, len = results.Count; i < len; i++)
            {
                if (!EqualsDeep(resultsOld[i], results[i]))
                    return false;
            }
            return true;
        }

        private static bool EqualsDeep(IList<TItem> chromInfosOld, IList<TItem> chromInfos)
        {
            if (!ArrayUtil.EqualsDeep(chromInfosOld, chromInfos))
                return false;
            if (chromInfos == null)
                return true;

            // If the arrays are otherwise equal, check the FileIds
            for (int i = 0; i < chromInfos.Count; i++)
            {
                if (!ReferenceEquals(chromInfosOld[i].FileId, chromInfos[i].FileId))
                    return false;
            }
            return true;
        }

        public float? GetAverageValue(Func<TItem, float?> getVal)
        {
            int valCount = 0;
            double valTotal = 0;

            foreach (var result in this)
            {
                if (result.IsEmpty)
                    continue;
                foreach (var chromInfo in result)
                {
                    if (Equals(chromInfo, default(TItem)))
                        continue;
                    float? val = getVal(chromInfo);
                    if (!val.HasValue)
                        continue;

                    valTotal += val.Value;
                    valCount++;
                }
            }

            if (valCount == 0)
                return null;

            return (float)(valTotal / valCount);            
        }

        public float? GetBestPeakValue(Func<TItem, RatedPeakValue> getVal)
        {
            double ratingBest = double.MinValue;
            float? valBest = null;

            foreach (var result in this)
            {
                if (result.IsEmpty)
                    continue;
                foreach (var chromInfo in result)
                {
                    if (Equals(chromInfo, default(TItem)))
                        continue;
                    RatedPeakValue rateVal = getVal(chromInfo);
                    if (rateVal.Rating > ratingBest)
                    {
                        ratingBest = rateVal.Rating;
                        valBest = rateVal.Value;
                    }
                }
            }

            return valBest;
        }

        public void Validate(SrmSettings settings)
        {
            var chromatogramSets = settings.MeasuredResults.Chromatograms;
            if (chromatogramSets.Count != Count)
            {
                throw new InvalidDataException(
                    string.Format(Resources.Results_Validate_DocNode_results_count__0__does_not_match_document_results_count__1__,
                                  Count, chromatogramSets.Count));
            }

            for (int i = 0; i < chromatogramSets.Count; i++)
            {
                var chromList = this[i];
                if (chromList.IsEmpty)
                    continue;

                var chromatogramSet = chromatogramSets[i];
                if (chromList.Any(chromInfo => chromatogramSet.IndexOfId(chromInfo.FileId) == -1))
                {
                    throw new InvalidDataException(
                        string.Format(Resources.Results_Validate_DocNode_peak_info_found_for_file_with_no_match_in_document_results));
                }
            }
        }

        private bool Equals(Results<TItem> other)
        {
            return _list.Equals(other._list);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is Results<TItem> && Equals((Results<TItem>) obj);
        }

        public override int GetHashCode()
        {
            return _list.GetHashCode();
        }
    }

    public struct RatedPeakValue
    {
        public RatedPeakValue(double rating, float? value) : this()
        {
            Rating = rating;
            Value = value;
        }

        public double Rating { get; private set; }
        public float? Value { get; private set; }
    }

    /// <summary>
    /// The set of all measured results for a single <see cref="DocNode"/> in a single replicate.
    /// There will usually be just one measurement, but in case of operator error,
    /// on a multi-injection replicate there may be more.  Also, a multi-injection replicate
    /// with an unlabeled internal standard will have measurements for that standard
    /// in every file.
    /// </summary>
    public struct ChromInfoList<TItem> : IList<TItem>
    {
        private readonly object _oneOrManyItems;
        public ChromInfoList(params TItem[] elements) : this((IEnumerable<TItem>) elements)
        {
        }

        public ChromInfoList(IEnumerable<TItem> elements)
        {
            var list = ImmutableList.ValueOf(elements);
            if (list == null || list.Count == 0)
            {
                _oneOrManyItems = null;
            }
            else if (list.Count == 1)
            {
                _oneOrManyItems = list[0];
            }
            else
            {
                _oneOrManyItems = list;
            }

        }

        public float? GetAverageValue(Func<TItem, float?> getVal)
        {
            int valCount = 0;
            double valTotal = 0;

            foreach (var chromInfo in this)
            {
                if (Equals(chromInfo, default(TItem)))
                    continue;
                float? val = getVal(chromInfo);
                if (!val.HasValue)
                    continue;

                valTotal += val.Value;
                valCount++;
            }

            if (valCount == 0)
                return null;

            return (float)(valTotal / valCount);
        }

        public ChromInfoList<TItem> ChangeAt(int i, TItem item)
        {
            var list = AsList().ToArray();
            list[i] = item;
            return new ChromInfoList<TItem>(list);
        }

        public int Count 
        {
            get
            {
                if (_oneOrManyItems == null)
                {
                    return 0;
                }
                if (_oneOrManyItems is TItem)
                {
                    return 1;
                }
                return ((IList<TItem>) _oneOrManyItems).Count;
            } 
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<TItem> GetEnumerator()
        {
            return AsList().GetEnumerator();
        }

        public TItem this[int index]
        {
            get { return AsList()[index]; }
        }

        public IList<TItem> AsList()
        {
            if (_oneOrManyItems == null)
            {
                return ImmutableList<TItem>.EMPTY;
            }
            if (_oneOrManyItems is TItem)
            {
                return ImmutableList.Singleton((TItem) _oneOrManyItems);
            }
            return (IList<TItem>) _oneOrManyItems;
        }

        public bool IsEmpty
        {
            get { return null == _oneOrManyItems; }
        }

        public bool Contains(TItem item)
        {
            return AsList().Contains(item);
        }

        public void CopyTo(TItem[] array, int arrayIndex)
        {
            AsList().CopyTo(array, arrayIndex);
        }

        public int IndexOf(TItem item)
        {
            return AsList().IndexOf(item);
        }

        bool ICollection<TItem>.IsReadOnly { get { return true; } }


        void ICollection<TItem>.Add(TItem item)
        {
            throw new InvalidOperationException();
        }

        void ICollection<TItem>.Clear()
        {
            throw new InvalidOperationException();
        }

        void IList<TItem>.Insert(int index, TItem item)
        {
            throw new InvalidOperationException();
        }

        bool ICollection<TItem>.Remove(TItem item)
        {
            throw new InvalidOperationException();
        }

        void IList<TItem>.RemoveAt(int index)
        {
            throw new InvalidOperationException();
        }

        TItem IList<TItem>.this[int index]
        {
            get
            {
                return this[index];
            }
            set
            {
                throw new InvalidOperationException();
            }
        }
    }

    public enum UserSet
    {
        TRUE,   // SET by manual integration
        FALSE,  // Best peak picked during results import
        IMPORTED,   // Import peak boundaries
        REINTEGRATED,   // Edit > Refine > Reintagrate
        MATCHED // Forced by peak matching when adding missing label type precursors
    }

    public static class UserSetFastLookup
    {
        public static readonly Dictionary<string, UserSet> Dict = XmlUtil.GetEnumLookupDictionary(
            UserSet.TRUE, UserSet.FALSE, UserSet.IMPORTED, UserSet.REINTEGRATED, UserSet.MATCHED);
    }

    public static class UserSetExtension
    {
        private static readonly UserSet[] USER_SET_PRIORITY_LIST =
            {
                UserSet.FALSE,
                UserSet.TRUE,
                UserSet.IMPORTED,
                UserSet.REINTEGRATED,
                UserSet.MATCHED
            };

        public static UserSet GetBest(UserSet us1, UserSet us2, bool includeFalse = false)
        {
            for (int i = includeFalse ? 0 : 1; i < USER_SET_PRIORITY_LIST.Length; i++)
            {
                UserSet usCurrent = USER_SET_PRIORITY_LIST[i];
                if (us1 == usCurrent || us2 == usCurrent)
                    return usCurrent;
            }
            return UserSet.FALSE;            
        }

        public static bool IsOverride(this UserSet userSetPrimary, UserSet userSetSecondary)
        {
            var userSetBest = GetBest(userSetPrimary, userSetSecondary, true);
            // If primary is already best, then this is not an override
            if (userSetBest == userSetPrimary)
                return false;
            // Otherwise, only override MATCHED with anything and anything with FALSE
            // This keeps importing and reintegrating from resetting each other's peaks
            // when the specifide boundaries are not different.
            return (userSetPrimary == UserSet.MATCHED || userSetSecondary == UserSet.FALSE);
        }        
    }

    /// <summary>
    /// Base class for a single measured result for a single <see cref="DocNode"/>
    /// in a single file of a single replicate.
    /// </summary>
    public abstract class ChromInfo : Immutable
    {
        public const int RATIO_INDEX_GLOBAL_STANDARDS = -2;

        protected ChromInfo(ChromFileInfoId fileId)
        {
            FileId = fileId;
        }

        public ChromFileInfoId FileId { get; private set; }
        public int FileIndex { get { return FileId.GlobalIndex; } }

        #region object overrides

        public bool Equals(ChromInfo other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            // TODO: This is not very strong equality, since all FileIds are equal
            //       It would be better to check reference equality, but this would
            //       break document equality tests across serialization/deserialization
            //       At the momement, we rely on it being very unlikely that two
            //       peaks from different files are exactly equal.
            return Equals(other.FileId, FileId);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (ChromInfo)) return false;
            return Equals((ChromInfo) obj);
        }

        public override int GetHashCode()
        {
            return FileId.GetHashCode();
        }

        public override string ToString()
        {
            return String.Format(@"FileId = {0}", FileId.GlobalIndex); // For debugging
        }

        #endregion
    }
}
