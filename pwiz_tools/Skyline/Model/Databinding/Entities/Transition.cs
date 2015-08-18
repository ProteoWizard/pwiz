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
using System.Globalization;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.transition)]
    public class Transition : SkylineDocNode<TransitionDocNode>
    {
        private readonly Lazy<Precursor> _precursor;
        private readonly CachedValue<IDictionary<ResultKey, TransitionResult>> _results;
        public Transition(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
            _precursor = new Lazy<Precursor>(() => new Precursor(DataSchema, IdentityPath.Parent));
            _results = CachedValue.Create(DataSchema, MakeResults);
        }

        [HideWhen(AncestorOfType = typeof(Precursor))]
        public Precursor Precursor
        {
            get
            {
                return _precursor.Value;
            }
        }

        [InvariantDisplayName("TransitionResults")]
        [OneToMany(ForeignKey = "Transition", ItemDisplayName = "TransitionResult")]
        public IDictionary<ResultKey, TransitionResult> Results
        {
            get
            {
                return _results.Value;
            }
        }

        private IDictionary<ResultKey, TransitionResult> MakeResults()
        {
            return MakeChromInfoResultsMap(DocNode.Results, file => new TransitionResult(this, file));
        }

        private bool IsCustomTransition()
        {
            return DocNode.Transition.IsNonReporterCustomIon();  // As opposed to just IsCustom(), which might be a reporter ion on a peptide node
        }

        protected override TransitionDocNode CreateEmptyNode()
        {
            return new TransitionDocNode(new Model.Transition(new TransitionGroup(new Model.Peptide(null, "X", null, null, 0), null, 1, IsotopeLabelType.light), 0), Annotations.EMPTY, null, 0, null, null, null); // Not L10N
        }

        [InvariantDisplayName("TransitionResultsSummary")]
        public TransitionResultSummary ResultSummary
        {
            get
            {
                return new TransitionResultSummary(this, Results.Values);
            }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int ProductCharge { get { return DocNode.Transition.Charge; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double ProductNeutralMass
        {
            get { return DocNode.GetIonPersistentNeutralMass(); }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double ProductMz
        {
            get { return SequenceMassCalc.PersistentMZ(DocNode.Mz); }
        }
        public string FragmentIon
        {
            get
            {
                string fragmentIon =  DocNode.GetFragmentIonName(CultureInfo.InvariantCulture);
                if (DocNode.Transition.IonType == IonType.precursor)
                {
                    fragmentIon += Model.Transition.GetMassIndexText(DocNode.Transition.MassIndex);
                }
                return fragmentIon;
            }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public string ProductIonFormula
        {
            get
            {
                return IsCustomTransition()
                ? (DocNode.Transition.CustomIon.Formula ?? string.Empty)
                : null;
            }
        }
        public IonType FragmentIonType
        {
            get { return DocNode.Transition.IonType; }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? FragmentIonOrdinal
        {
            get
            {
                if (DocNode.Transition.IsCustom())
                    return null;
                return DocNode.Transition.Ordinal;
            }
        }
        public char? CleavageAa
        {
            get
            {
                return IsCustomTransition()
                    ? default(char?) 
                    : DocNode.Transition.AA;
            }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LossNeutralMass
        {
            get
            {
                if (IsCustomTransition())
                    return  null;
                return DocNode.LostMass;
            }
        }
        public string Losses
        {
            get
            {
                return IsCustomTransition()
                    ? null
                    : (DocNode.HasLoss ? string.Join(", ", DocNode.Losses.ToStrings()) : string.Empty);  // Not L10N
            }
        }
        [InvariantDisplayName("TransitionNote")]
        public string Note
        {
            get { return DocNode.Note; }
            set { ChangeDocNode(EditDescription.SetColumn("TransitionNote", value), // Not L10N
                DocNode.ChangeAnnotations(DocNode.Annotations.ChangeNote(value)));}
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? LibraryRank
        {
            get
            {
                return DocNode.HasLibInfo ? (int?) DocNode.LibInfo.Rank : null;
            }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryIntensity
        {
            get { return DocNode.HasLibInfo ? (double?) DocNode.LibInfo.Intensity : null; }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int IsotopeDistIndex { get { return DocNode.Transition.MassIndex; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? IsotopeDistRank { get { return DocNode.HasDistInfo ? (int?)DocNode.IsotopeDistInfo.Rank : null; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? IsotopeDistProportion {get { return DocNode.HasDistInfo ? (double?) DocNode.IsotopeDistInfo.Proportion : null; }}
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? FullScanFilterWidth
        {
            get
            {
                var fullScan = SrmDocument.Settings.TransitionSettings.FullScan;
                if (fullScan.IsEnabledMs && DocNode.Transition.IonType == IonType.precursor)
                {
                    return SequenceMassCalc.PersistentMZ(fullScan.GetPrecursorFilterWindow(ProductMz));
                }
                if (fullScan.IsEnabledMsMs &&
                    (DocNode.Transition.IonType != IonType.precursor || DocNode.Transition.MassIndex == 0))
                {
                    return SequenceMassCalc.PersistentMZ(fullScan.GetProductFilterWindow(DocNode.Mz));
                }
                return null;
            }
        }
        [InvariantDisplayName("TransitionIsDecoy")]
        public bool IsDecoy
        {
            get { return DocNode.IsDecoy; }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? ProductDecoyMzShift
        {
            get { return DocNode.Transition.DecoyMassShift; }
        }

        public override string ToString()
        {
            return DocNode.Transition.ToString();
        }
    }

    public class TransitionResultSummary : SkylineObject
    {
        public TransitionResultSummary(Transition transition, IEnumerable<TransitionResult> results)
            : base(transition.DataSchema)
        {
#pragma warning disable 612
            Transition = transition;
#pragma warning restore 612
            var retentionTimes = new List<double>();
            var fwhms = new List<double>();
            var areas = new List<double>();
            var areasRatio = new List<double>();
            var areasNormalized = new List<double>();

            foreach (var result in results)
            {
                if (result.RetentionTime.HasValue)
                {
                    retentionTimes.Add(result.RetentionTime.Value);
                }
                if (result.Fwhm.HasValue)
                {
                    fwhms.Add(result.Fwhm.Value);
                }
                if (result.Area.HasValue)
                {
                    areas.Add(result.Area.Value);
                }
                if (result.AreaNormalized.HasValue)
                {
                    areasNormalized.Add(result.AreaNormalized.Value);
                }
                if (result.AreaRatio.HasValue)
                {
                    areasRatio.Add(result.AreaRatio.Value);
                }
            }
            if (retentionTimes.Count > 0)
            {
                RetentionTime = new RetentionTimeSummary(new Statistics(retentionTimes));
            }
            if (fwhms.Count > 0)
            {
                Fwhm = new FwhmSummary(new Statistics(fwhms));
            }
            if (areas.Count > 0)
            {
                Area = new AreaSummary(new Statistics(areas));
            }
            if (areasNormalized.Count > 0)
            {
                AreaNormalized = new AreaNormalizedSummary(new Statistics(areasNormalized));
            }
            if (areasRatio.Count > 0)
            {
                AreaRatio = new AreaRatioSummary(new Statistics(areasRatio));
            }
        }

        [Obsolete]
        public string ReplicatePath { get { return "/"; } } // Not L10N
        [Obsolete]
        public Transition Transition { get; private set; }
        [ChildDisplayName("{0}RetentionTime")]
        public RetentionTimeSummary RetentionTime { get; private set; }
        [ChildDisplayName("{0}Fwhm")]
        public FwhmSummary Fwhm { get; private set; }
        [ChildDisplayName("{0}Area")]
        public AreaSummary Area { get; private set; }
        [ChildDisplayName("{0}AreaNormalized")]
        public AreaNormalizedSummary AreaNormalized { get; private set; }
        [ChildDisplayName("{0}AreaRatio")]
        public AreaRatioSummary AreaRatio { get; private set; }

        public override string ToString()
        {
            return string.Format("RT: {0} Area: {1}", RetentionTime, Area); // Not L10N?
        }
    }
}
