/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Linq;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    // ReSharper disable LocalizableElement
    [AnnotationTarget(AnnotationDef.AnnotationTarget.precursor)]
    public class Precursor : SkylineDocNode<TransitionGroupDocNode>
    {
        public Precursor(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
        }

        private Peptide _peptide;
        [HideWhen(AncestorOfType = typeof(SkylineDocument))]
        public Peptide Peptide
        {
            get { return _peptide = _peptide ?? new Peptide(DataSchema, IdentityPath.Parent); }
        }

        private Transitions _transitions;

        [OneToMany(ForeignKey = "Precursor")]
        public Transitions Transitions
        {
            get { return _transitions = _transitions ?? new Transitions(this); }
        }

        private IDictionary<ResultKey, PrecursorResult> _results;
        [DisplayName("PrecursorResults")]
        [OneToMany(ForeignKey = "Precursor", ItemDisplayName = "PrecursorResult")]
        public IDictionary<ResultKey, PrecursorResult> Results
        {
            get { return _results = _results ?? MakeChromInfoResultsMap(DocNode.Results, file => new PrecursorResult(this, file)); }
        }

        protected override void OnDocumentChanged()
        {
            _results = null;
            base.OnDocumentChanged();
        }

        protected override TransitionGroupDocNode CreateEmptyNode()
        {
            return new TransitionGroupDocNode(new TransitionGroup(new Model.Peptide(null, "X", null, null, 0), 1, IsotopeLabelType.light), null);
        }

        [DisplayName("PrecursorResultsSummary")]
        public PrecursorResultSummary ResultSummary
        {
            get
            {
                return new PrecursorResultSummary(this, Results.Values);
            }
        }

        [DisplayName("PrecursorCharge")]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int Charge
        {
            get { return DocNode.TransitionGroup.PrecursorCharge; }
        }

        public IsotopeLabelType IsotopeLabelType
        {
            get { return DocNode.TransitionGroup.LabelType; }
        }

        [DisplayName("PrecursorNeutralMass")]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double NeutralMass
        {
            get
            {
                return SequenceMassCalc.PersistentNeutral(
                    SequenceMassCalc.GetMH(DocNode.PrecursorMz, DocNode.TransitionGroup.PrecursorCharge));
            }
        }

        [DisplayName("PrecursorMz")]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double Mz
        {
            get { return SequenceMassCalc.PersistentMZ(DocNode.PrecursorMz); }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double CollisionEnergy
        {
            get
            {
                return SrmDocument.Settings.TransitionSettings.Prediction.CollisionEnergy
                                  .GetCollisionEnergy(Charge, GetRegressionMz());
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? DeclusteringPotential
        {
            get
            {
                var declusteringPotentialRegression =
                    SrmDocument.Settings.TransitionSettings.Prediction.DeclusteringPotential;
                if (null == declusteringPotentialRegression)
                {
                    return null;
                }
                return declusteringPotentialRegression.GetDeclustringPotential(GetRegressionMz());
            }
        }

        public string ModifiedSequence
        {
            get
            {
                var peptideDocNode = Peptide.DocNode;
                return SrmDocument.Settings.GetPrecursorCalc(
                    DocNode.TransitionGroup.LabelType, peptideDocNode.ExplicitMods)
                                  .GetModifiedSequence(peptideDocNode.Peptide.Sequence, true);
            }
        }

        [DisplayName("PrecursorNote")]
        public string Note
        {
            get { return DocNode.Note; }
            set { ChangeDocNode(DocNode.ChangeAnnotations(DocNode.Annotations.ChangeNote(value))); }
        }

        public string LibraryName
        {
            get { return DocNode.HasLibInfo ? DocNode.LibInfo.LibraryName : null; }
        }

        public string LibraryType
        {
            get
            {
                if (DocNode.LibInfo is NistSpectrumHeaderInfo)
                {
                    return "NIST";
                }
                if (DocNode.LibInfo is XHunterSpectrumHeaderInfo)
                {
                    return "GPM";
                }
                if (DocNode.LibInfo is BiblioSpecSpectrumHeaderInfo)
                {
                    return "BiblioSpec";
                }
                return null;
            }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryScore1
        {
            get { return GetLibraryScore(0); }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryScore2
        {
            get { return GetLibraryScore(1); }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryScore3
        {
            get { return GetLibraryScore(2); }
        }

        public bool IsDecoy { get { return DocNode.IsDecoy; } }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? DecoyMzShift { get { return DocNode.TransitionGroup.DecoyMassShift; } }

        private double? GetLibraryScore(int index)
        {
            var libInfo = DocNode.LibInfo;
            if (null == libInfo)
            {
                return null;
            }
            var peptideRankId = libInfo.RankValues.Skip(index).FirstOrDefault().Key;
            if (null == peptideRankId)
            {
                return null;
            }
            return libInfo.GetRankValue(peptideRankId);
        }

        internal double GetRegressionMz()
        {
            return SrmDocument.Settings.GetRegressionMz(Peptide.DocNode, DocNode);
        }

        public override string ToString()
        {
            // Consider: maybe change TransitionGroupDocNode.ToString() to be this as well:
            return TransitionGroupTreeNode.GetLabel(DocNode.TransitionGroup, DocNode.PrecursorMz, string.Empty);
        }

        [Obsolete]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? LibraryRank { get { return null; } }
    }

    public class PrecursorResultSummary : SkylineObject
    {
        public PrecursorResultSummary(Precursor precursor, IEnumerable<PrecursorResult> results)
            : base(precursor.DataSchema)
        {
            // ReSharper disable once CSharpWarnings::CS0612
            Precursor = precursor;
            var bestRetentionTimes = new List<double>();
            var maxFhwms = new List<double>();
            var totalAreas = new List<double>();
            var totalAreasNormalized = new List<double>();
            var totalAreasRatio = new List<double>();
            var maxHeights = new List<double>();
            foreach (var result in results)
            {
                if (result.BestRetentionTime.HasValue)
                {
                    bestRetentionTimes.Add(result.BestRetentionTime.Value);
                }
                if (result.MaxFwhm.HasValue)
                {
                    maxFhwms.Add(result.MaxFwhm.Value);
                }
                if (result.TotalArea.HasValue)
                {
                    totalAreas.Add(result.TotalArea.Value);
                }
                if (result.TotalAreaNormalized.HasValue)
                {
                    totalAreasNormalized.Add(result.TotalAreaNormalized.Value);
                }
                if (result.TotalAreaRatio.HasValue)
                {
                    totalAreasRatio.Add(result.TotalAreaRatio.Value);
                }
                if (result.MaxHeight.HasValue)
                {
                    maxHeights.Add(result.MaxHeight.Value);
                }
            }
            if (bestRetentionTimes.Count > 0)
            {
                BestRetentionTime = new RetentionTimeSummary(new Statistics(bestRetentionTimes));
            }
            if (maxFhwms.Count > 0)
            {
                MaxFwhm = new FwhmSummary(new Statistics(maxFhwms));
            }
            if (totalAreas.Count > 0)
            {
                TotalArea = new AreaSummary(new Statistics(totalAreas));
            }
            if (totalAreasNormalized.Count > 0)
            {
                TotalAreaNormalized = new AreaNormalizedSummary(new Statistics(totalAreasNormalized));
            }
            if (totalAreasRatio.Count > 0)
            {
                TotalAreaRatio = new AreaRatioSummary(new Statistics(totalAreasRatio));
            }
            if (maxHeights.Count > 0)
            {
                MaxHeight = new AreaSummary(new Statistics(maxHeights));
            }
        }

        [Obsolete]
        public Precursor Precursor { get; private set; }
        [Obsolete]
        public string ReplicatePath { get { return "/"; } }
        [ChildDisplayName(Format = "{0}BestRetentionTime")]
        public RetentionTimeSummary BestRetentionTime { get; private set; }
        [ChildDisplayName(Format="{0}MaxFwhm")]
        public FwhmSummary MaxFwhm { get; private set; }
        [ChildDisplayName(Format="{0}TotalArea")]
        public AreaSummary TotalArea { get; private set; }
        [ChildDisplayName(Format="{0}TotalAreaRatio")]
        public AreaRatioSummary TotalAreaRatio { get; private set; }
        [ChildDisplayName(Format="{0}TotalAreaNormalized")]
        public AreaNormalizedSummary TotalAreaNormalized { get; private set; }
        [ChildDisplayName(Format="{0}MaxHeigth")]
        public AreaSummary MaxHeight { get; private set; }

        public override string ToString()
        {
            return string.Format("RT: {0} Area: {1}", BestRetentionTime, TotalArea);
        }
    }
}
