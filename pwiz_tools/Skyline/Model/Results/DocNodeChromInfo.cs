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
using JetBrains.Annotations;
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

        [Track(defaultValues:typeof(DefaultValuesFalse))]
        public bool ExcludeFromCalibration { get; private set; }

        public PeptideChromInfo ChangeExcludeFromCalibration(bool exclude)
        {
            if (exclude == ExcludeFromCalibration)
            {
                return this;
            }
            return ChangeProp(ImClone(this), im => im.ExcludeFromCalibration = exclude);
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public double? AnalyteConcentration { get; private set; }

        public PeptideChromInfo ChangeAnalyteConcentration(double? analyteConcentration)
        {
            if (Equals(analyteConcentration, AnalyteConcentration))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im => im.AnalyteConcentration = analyteConcentration);
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
                   other.AnalyteConcentration.Equals(AnalyteConcentration) &&
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
                result = (result * 397) ^ AnalyteConcentration.GetHashCode();
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
        [Flags]
        private enum Flags
        {
            HasRetentionTime = 1,
            HasStartRetentionTime = 2,
            HasEndRetentionTime = 4,
            HasFwhm = 8,
            HasArea = 16,
            HasAreaMs1 = 32,
            HasAreaFragment = 64,
            HasBackgroundArea = 128,
            HasBackgroundAreaMs1 = 256,
            HasBackgroundAreaFragment = 512,
            HasHeight = 1024,
            HasMassError = 2048,
            HasLibraryDotProduct = 4096,
            HasIsotopeDotProduct = 8192,
            HasQValue = 16384,
            HasZScore = 32768,
        }

        private Flags _flags;

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
            OptimizationStep = Convert.ToInt16(optimizationStep);
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
        public short OptimizationStep { get; private set; }

        public float PeakCountRatio { get; private set; }

        private float _retentionTime;
        public float? RetentionTime
        {
            get { return GetOptional(_retentionTime, Flags.HasRetentionTime); }
            set { _retentionTime = SetOptional(value, Flags.HasRetentionTime); }
        }

        private float _startRetentionTime;
        public float? StartRetentionTime
        {
            get { return GetOptional(_startRetentionTime, Flags.HasStartRetentionTime); }
            set { _startRetentionTime = SetOptional(value, Flags.HasStartRetentionTime); }
        }

        private float _endRetentionTime;
        public float? EndRetentionTime
        {
            get { return GetOptional(_endRetentionTime, Flags.HasEndRetentionTime); }
            set { _endRetentionTime = SetOptional(value, Flags.HasEndRetentionTime); }
        }

        public TransitionGroupIonMobilityInfo IonMobilityInfo { get; private set; }
        private float _fwhm;

        public float? Fwhm
        {
            get { return GetOptional(_fwhm, Flags.HasFwhm); }
            set { _fwhm = SetOptional(value, Flags.HasFwhm); }
        }

        private float _area;
        public float? Area
        {
            get { return GetOptional(_area, Flags.HasArea);}
            private set { _area = SetOptional(value, Flags.HasArea); }
        }

        private float _areaMs1;
        public float? AreaMs1
        {
            get { return GetOptional(_areaMs1, Flags.HasAreaMs1);}

            private set { _areaMs1 = SetOptional(value, Flags.HasAreaMs1); }
        }

        private float _areaFragment;
        public float? AreaFragment
        {
            get { return GetOptional(_areaFragment, Flags.HasAreaFragment); }
            private set { _areaFragment = SetOptional(value, Flags.HasAreaFragment); }
        }

        private float _backgroundArea;
        public float? BackgroundArea
        {
            get { return GetOptional(_backgroundArea, Flags.HasBackgroundArea); }
            private set { _backgroundArea = SetOptional(value, Flags.HasBackgroundArea); }
        }

        private float _backgroundAreaMs1;
        public float? BackgroundAreaMs1
        {
            get { return GetOptional(_backgroundAreaMs1, Flags.HasBackgroundAreaMs1); }
            private set { _backgroundAreaMs1 = SetOptional(value, Flags.HasBackgroundAreaMs1); }
        }

        private float _backgroundAreaFragment;
        public float? BackgroundAreaFragment 
        {
            get { return GetOptional(_backgroundAreaFragment, Flags.HasBackgroundAreaFragment); }
            private set { _backgroundAreaFragment = SetOptional(value, Flags.HasBackgroundAreaFragment); }
        }

        private float _height;
        public float? Height 
        { 
            get { return GetOptional(_height, Flags.HasHeight); }
            private set { _height = SetOptional(value, Flags.HasHeight); }
        }

        private float _massError;
        public float? MassError 
        { 
            get { return GetOptional(_massError, Flags.HasMassError); }
            private set { _massError = SetOptional(value, Flags.HasMassError); }
        }

        private int _truncated;
        public int? Truncated
        {
            get { return _truncated >= 0 ? _truncated : (int?) null; }
            private set { _truncated = value ?? -1; }
        }

        public PeakIdentification Identified { get; private set; }
        public bool IsIdentified { get { return Identified != PeakIdentification.FALSE; } }

        private float _libraryDotProduct;
        public float? LibraryDotProduct
        {
            get { return GetOptional(_libraryDotProduct, Flags.HasLibraryDotProduct);}
            private set { _libraryDotProduct = SetOptional(value, Flags.HasLibraryDotProduct); }
        }

        private float _isotopeDotProduct;
        public float? IsotopeDotProduct
        {
            get { return GetOptional(_isotopeDotProduct, Flags.HasIsotopeDotProduct); }
            private set { _isotopeDotProduct = SetOptional(value, Flags.HasIsotopeDotProduct); }
        }

        private float _qValue;
        public float? QValue
        {
            get { return GetOptional(_qValue, Flags.HasQValue); }
            private set { _qValue = SetOptional(value, Flags.HasQValue); }
        }

        private float _zScore;
        public float? ZScore
        {
            get { return GetOptional(_zScore, Flags.HasZScore); }
            private set { _zScore = SetOptional(value, Flags.HasZScore); }
        }

        public Annotations Annotations { get; private set; }

        /// <summary>
        /// Set if user action has explicitly set these values
        /// </summary>
        public UserSet UserSet { get; private set; }

        public bool IsUserSetManual { get { return UserSet == UserSet.TRUE; } }

        public bool IsUserSetAuto { get { return UserSet == UserSet.IMPORTED || UserSet == UserSet.REINTEGRATED; } }

        public bool IsUserSetMatched { get { return UserSet == UserSet.MATCHED; }}

        public bool IsUserModified { get { return IsUserSetManual || !Annotations.IsEmpty; } }

        private bool GetFlag(Flags flag)
        {
            return 0 != (_flags & flag);
        }

        private void SetFlag(Flags flag, bool b)
        {
            if (b)
            {
                _flags |= flag;
            }
            else
            {
                _flags &= ~flag;
            }
        }

        private T? GetOptional<T>(T field, Flags flag) where T:struct
        {
            return GetFlag(flag) ? field : (T?) null;
        }

        private T SetOptional<T>(T? value, Flags flag) where T : struct
        {
            SetFlag(flag, value.HasValue);
            return value ?? default(T);
        }

        #region Property change methods

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
                result = (result*397) ^ (Truncated.HasValue ? Truncated.Value.GetHashCode() : 0);
                result = (result*397) ^ Identified.GetHashCode();
                result = (result*397) ^ (LibraryDotProduct.HasValue ? LibraryDotProduct.Value.GetHashCode() : 0);
                result = (result*397) ^ (IsotopeDotProduct.HasValue ? IsotopeDotProduct.Value.GetHashCode() : 0);
                result = (result*397) ^ QValue.GetHashCode();
                result = (result*397) ^ ZScore.GetHashCode();
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
        [Flags]
        private enum Flags : byte
        {
            HasMassError = 1,
            IsFwhmDegenerate = 2,
            HasPointsAcrossPeak = 4,
            TruncatedKnown = 8,
            Truncated = 16,
            ForcedIntegration = 32,
            Identified = 64,
            IdentifiedByAlignment = 128,
        }

        private Flags _flags;
        public TransitionChromInfo(float startRetentionTime, float endRetentionTime)
            : base(null)
        {
            StartRetentionTime = startRetentionTime;
            EndRetentionTime = endRetentionTime;
        }

        public TransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, ChromPeak peak,
            IonMobilityFilter ionMobility, Annotations annotations, UserSet userSet)
            : this(fileId, optimizationStep, peak.MassError, peak.RetentionTime, peak.StartTime, peak.EndTime,
                   ionMobility,
                   peak.Area, peak.BackgroundArea, peak.Height, peak.Fwhm,
                   peak.IsFwhmDegenerate, peak.IsTruncated, 
                   peak.PointsAcross, 
                   peak.Identified, 0, 0,
                   annotations, userSet, peak.IsForcedIntegration)
        {
        }

        public TransitionChromInfo(ChromFileInfoId fileId, int optimizationStep, float? massError,
                                   float retentionTime, float startRetentionTime, float endRetentionTime,
                                   IonMobilityFilter ionMobility,
                                   float area, float backgroundArea, float height,
                                   float fwhm, bool fwhmDegenerate, bool? truncated, short? pointsAcrossPeak,
                                   PeakIdentification identified, short rank, short rankByLevel,
                                   Annotations annotations, UserSet userSet, bool isForcedIntegration)
            : base(fileId)
        {
            OptimizationStep = Convert.ToInt16(optimizationStep);
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
        public short OptimizationStep { get; private set; }

        private short _massError;

        public float? MassError
        {
            get
            {
                if (GetFlag(Flags.HasMassError))
                {
                    return _massError / 10f;
                }
                return null;
            }
            private set
            {
                SetFlag(Flags.HasMassError, value.HasValue);
                _massError = ChromPeak.To10x(value ?? 0);
            }
        }

        public float RetentionTime { get; private set; }
        public float StartRetentionTime { get; private set; }
        public float EndRetentionTime { get; private set; }
        public IonMobilityFilter IonMobility { get; private set; } // The actual ion mobility used for this transition
        public float Area { get; private set; }
        public float BackgroundArea { get; private set; }
        public float Height { get; private set; }
        public float Fwhm { get; private set; }

        public bool IsFwhmDegenerate
        {
            get
            {
                return GetFlag(Flags.IsFwhmDegenerate);
            }
            set
            {
                SetFlag(Flags.IsFwhmDegenerate, value);
            }
        }

        public bool? IsTruncated
        {
            get
            {
                if (!GetFlag(Flags.TruncatedKnown))
                {
                    return null;
                }
                return GetFlag(Flags.Truncated);
            }
            private set
            {
                SetFlag(Flags.TruncatedKnown, value.HasValue);
                SetFlag(Flags.Truncated, value ?? false);
            }
        }

        public bool IsIdentified { get { return Identified != PeakIdentification.FALSE; } }

        public PeakIdentification Identified
        {
            get
            {
                if (!GetFlag(Flags.Identified))
                {
                    return PeakIdentification.FALSE;
                }

                return GetFlag(Flags.IdentifiedByAlignment) ? PeakIdentification.ALIGNED : PeakIdentification.TRUE;
            }
            set
            {
                SetFlag(Flags.Identified, value != PeakIdentification.FALSE);
                SetFlag(Flags.IdentifiedByAlignment, value == PeakIdentification.ALIGNED);
            }
        }
        public short Rank { get; private set; }
        public short RankByLevel { get; private set; }

        private short _pointsAcrossPeak;

        public short? PointsAcrossPeak
        {
            get
            {
                return GetFlag(Flags.HasPointsAcrossPeak) ? _pointsAcrossPeak : (short?) null;
            }
            private set
            {
                SetFlag(Flags.HasPointsAcrossPeak, value.HasValue);
                _pointsAcrossPeak = value ?? 0;
            }
        }

        public bool IsForcedIntegration
        {
            get
            {
                return GetFlag(Flags.ForcedIntegration);
            }
            private set
            {
                SetFlag(Flags.ForcedIntegration, value);
            }
        }

        public bool IsGoodPeak(bool integrateAll)
        {
            if (integrateAll)
            {
                return Area > 0;
            }
            return Area > 0 && !IsForcedIntegration;
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
                    .Select(transitionPeak => FromProtoTransitionPeak(annotationScrubber, settings, transitionPeak)).ToList();
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
                ionMobility = IonMobilityFilter.GetIonMobilityFilter(ionMobilityValue.Value, ionMobilityUnits, ionMobilityWidth, null);
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
                annotationScrubber.ScrubAnnotations(Annotations.FromProtoAnnotations(transitionPeak.Annotations), AnnotationDef.AnnotationTarget.transition_result), 
                DataValues.FromUserSet(transitionPeak.UserSet),
                transitionPeak.ForcedIntegration
                );
        }

        private bool GetFlag(Flags flag)
        {
            return 0 != (_flags & flag);
        }

        private void SetFlag(Flags flag, bool b)
        {
            if (b)
            {
                _flags |= flag;
            }
            else
            {
                _flags &= ~flag;
            }
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
        public ChromInfoList(IList<TItem> elements)
        {
            _oneOrManyItems = MakeOneOrManyItems(elements);
        }

        public ChromInfoList(IEnumerable<TItem> elements) : this(ImmutableList.ValueOf(elements))
        {
        }
        
        /// <summary>
        /// Make the value which is to be stored in the <see cref="_oneOrManyItems"/> field.
        /// After removing all nulls from the passed in list, if the resulting collection is empty,
        /// then _oneOrManyItems is set to null. If the collection contains only one element, then
        /// _oneOrManyItems is set to that element. Otherwise, _oneOrManyItems is an ImmutableList containing
        /// the items.
        /// </summary>
        private static object MakeOneOrManyItems(IList<TItem> chromInfos)
        {
            if (chromInfos is ChromInfoList<TItem> chromInfoList)
            {
                return chromInfoList._oneOrManyItems;
            }

            switch (chromInfos?.Count)
            {
                case null:
                case 0:
                    return null;
                case 1:
                    return chromInfos[0];
            }

            var list = ImmutableList.ValueOf(chromInfos);
            if (list.Contains(default(TItem)))
            {
                return MakeOneOrManyItems(ImmutableList.ValueOf(list.Where(item => null != item)));
            }

            return list;
        }

        public float? GetAverageValue(Func<TItem, float?> getVal)
        {
            int valCount = 0;
            double valTotal = 0;

            foreach (var chromInfo in this)
            {
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
            return new ChromInfoList<TItem>(AsList().ReplaceAt(i, item));
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
                return ((ImmutableList<TItem>) _oneOrManyItems).Count;
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

        [NotNull]
        public TItem this[int index]
        {
            get
            {
                if (_oneOrManyItems == null)
                {
                    throw new IndexOutOfRangeException();
                }

                if (_oneOrManyItems is TItem item)
                {
                    if (index != 0)
                    {
                        throw new IndexOutOfRangeException();
                    }

                    return item;
                }
                return ((ImmutableList<TItem>)_oneOrManyItems)[index];
            }
        }

        public ImmutableList<TItem> AsList()
        {
            if (_oneOrManyItems == null)
            {
                return ImmutableList<TItem>.EMPTY;
            }
            if (_oneOrManyItems is TItem)
            {
                return ImmutableList.Singleton((TItem) _oneOrManyItems);
            }
            return (ImmutableList<TItem>) _oneOrManyItems;
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

    public enum UserSet : byte
    {
        TRUE,   // SET by manual integration
        FALSE,  // Best peak picked during results import
        IMPORTED,   // Import peak boundaries
        REINTEGRATED,   // Edit > Refine > Reintegrate
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
