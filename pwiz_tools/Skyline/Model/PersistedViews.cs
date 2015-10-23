/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// Holds all of the Skyline views that get saved in <see cref="Settings.PersistedViews"/>.
    /// This class replaces <see cref="Settings.ViewSpecList"/> and <see cref="Settings.ReportSpecList"/>.
    /// </summary>
    [XmlRoot("persisted_views")]
    public class PersistedViews : SerializableViewGroups
    {
        public static readonly ViewGroup MainGroup = new ViewGroup("main", // Not L10N
            () => Resources.PersistedViews_MainGroup_Main); // Not L10N
        public static readonly ViewGroup ExternalToolsGroup = new ViewGroup("external_tools", // Not L10N
            () => Resources.PersistedViews_ExternalToolsGroup_External_Tools);

        /// <summary>
        /// Construct a new PersistedViews, migrating over the values from the old ViewSpecList 
        /// and ReportSpecList properties.  Views that are in use by an external tool get put in
        /// the External Tools group, and views that are
        /// </summary>
        public PersistedViews(ReportSpecList reportSpecList, ViewSpecList viewSpecList, ToolList toolList)
        {
            var viewItems = new List<ViewSpec>();
            if (null != viewSpecList)
            {
                viewItems.AddRange(viewSpecList.ViewSpecs);
            }
            if (null != reportSpecList)
            {
                RevisionIndex = reportSpecList.RevisionIndex + 1;
                foreach (var newView in ReportSharing.ConvertAll(reportSpecList.Select(reportSpec => new ReportOrViewSpec(reportSpec)),
                            new SrmDocument(SrmSettingsList.GetDefault())))
                {
                    if (viewItems.Any(viewSpec => viewSpec.Name == newView.Name))
                    {
                        continue;
                    }
                    viewItems.Add(newView);
                }
            }
            var viewSpecLists = new Dictionary<ViewGroup, Dictionary<string, ViewSpec>>();
            foreach (var viewItem in viewItems)
            {
                ViewGroup group;
                if (toolList.Any(tool => tool.ReportTitle == viewItem.Name))
                {
                    group = ExternalToolsGroup;
                }
                else
                {
                    group = MainGroup;
                }
                Dictionary<string, ViewSpec> list;
                if (!viewSpecLists.TryGetValue(group, out list))
                {
                    list = new Dictionary<string, ViewSpec>();
                    viewSpecLists.Add(group, list);
                }
                if (!list.ContainsKey(viewItem.Name))
                {
                    list.Add(viewItem.Name, viewItem);
                }
                else
                {
                    for (int i = 1;; i++)
                    {
                        string name = viewItem.Name + i;
                        if (!list.ContainsKey(name))
                        {
                            list.Add(name, viewItem.SetName(name));
                            break;
                        }
                    }
                }
            }
            foreach (var entry in viewSpecLists)
            {
                SetViewSpecList(entry.Key.Id, new ViewSpecList(entry.Value.Values));
            }
            AddDefaults();
        }

        private PersistedViews()
        {
        }

        public int RevisionIndex { get; private set; }
        public int RevisionIndexCurrent { get { return 6; } }
        public override void ReadXml(XmlReader reader)
        {
            RevisionIndex = reader.GetIntAttribute(Attr.revision);
            base.ReadXml(reader);
            AddDefaults();
        }

        public override void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(Attr.revision, RevisionIndex);
            base.WriteXml(writer);
        }

        public IEnumerable<KeyValuePair<ViewGroupId, ViewSpec>> GetDefaults(int revisionIndex)
        {
            List<string> reportStrings = new List<string>();
            if (revisionIndex >= 1)
            {
                reportStrings.Add(REPORTS_V1);
            }
            if (revisionIndex >= 2)
            {
                reportStrings.Add(REPORTS_V2);
            }
            if (revisionIndex >= 3)
            {
                reportStrings.Add(REPORTS_V3);
            }
            if (revisionIndex >= 4)
            {
                reportStrings.Add(REPORTS_V4);
            }
            if (revisionIndex >= 5)
            {
                reportStrings.Add(REPORTS_V5);
            }
            if (revisionIndex >= 6)
            {
                reportStrings.Add(REPORTS_V6);
            }
            var list = new List<KeyValuePair<ViewGroupId, ViewSpec>>();
            var xmlSerializer = new XmlSerializer(typeof(ViewSpecList));
            foreach (var reportString in reportStrings)
            {
                list.AddRange(((ViewSpecList)xmlSerializer.Deserialize(new StringReader(reportString))).ViewSpecs.Select(spec=>new KeyValuePair<ViewGroupId, ViewSpec>(MainGroup.Id, spec)));
            }
            // ReSharper disable NonLocalizedString
            var nameMap = new Dictionary<string, ViewName>{
                {"Peptide Ratio Results", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results)},
                {"Peptide Quantification", MainGroup.Id.ViewName(Resources.Resources_ReportSpecList_GetDefaults_Peptide_Quantification)},
                {"Peptide RT Results", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Peptide_RT_Results)},
                {"Transition Results", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Transition_Results)},
                {"Peak Boundaries", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Peak_Boundaries)},
                {"Peptide Transition List", MainGroup.Id.ViewName(Resources.SkylineViewContext_GetTransitionListReportSpec_Peptide_Transition_List)},
                {"Mixed Transition List", MainGroup.Id.ViewName(Resources.SkylineViewContext_GetTransitionListReportSpec_Mixed_Transition_List)},
                {"Small Molecule Transition List", MainGroup.Id.ViewName(Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List)},
                {"SRM Collider Input", ExternalToolsGroup.Id.ViewName("SRM Collider Input")},
            };
            // ReSharper restore NonLocalizedString

            for (int i = 0; i < list.Count; i++)
            {
                ViewName newName;
                if (nameMap.TryGetValue(list[i].Value.Name, out newName))
                {
                    list[i] = new KeyValuePair<ViewGroupId, ViewSpec>(newName.GroupId, list[i].Value.SetName(newName.Name));
                }
            }

            return RemoveDuplicates(list);
        }

        /// <summary>
        /// If there is more than one view in the list with a particular name, remove duplicates
        /// so only the last entry with that name is present.
        /// </summary>
        private List<KeyValuePair<ViewGroupId, ViewSpec>> RemoveDuplicates(
            IList<KeyValuePair<ViewGroupId, ViewSpec>> views)
        {
            HashSet<ViewName> names = new HashSet<ViewName>();
            List<KeyValuePair<ViewGroupId, ViewSpec>> listWithDuplicatesRemoved = new List<KeyValuePair<ViewGroupId, ViewSpec>>();
            // Go through the list items in reverse order, and copy over only those items that are unique.
            for (int i = views.Count - 1; i >= 0; i--)
            {
                var entry = views[i];
                if (names.Add(entry.Key.ViewName(entry.Value.Name)))
                {
                    listWithDuplicatesRemoved.Add(entry);
                }
            }
            // Reverse the elements in the new list, since we added them in reverse order.
            listWithDuplicatesRemoved.Reverse();
            return listWithDuplicatesRemoved;
        }

        // ReSharper disable NonLocalizedString
        private const string REPORTS_V1 = @"<views>
  <view name='Peptide Ratio Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='Sequence' />
    <column name='Protein.Name' />
    <column name='Results!*.Value.ResultFile.Replicate.Name' />
    <column name='Results!*.Value.PeptidePeakFoundRatio' />
    <column name='Results!*.Value.PeptideRetentionTime' />
    <column name='Results!*.Value.RatioToStandard' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
  <view name='Peptide RT Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='Sequence' />
    <column name='Protein.Name' />
    <column name='Results!*.Value.ResultFile.Replicate.Name' />
    <column name='PredictedRetentionTime' />
    <column name='Results!*.Value.PeptideRetentionTime' />
    <column name='Results!*.Value.PeptidePeakFoundRatio' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
  <view name='Transition Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>
    <column name='Precursor.Peptide.Sequence' />
    <column name='Precursor.Peptide.Protein.Name' />
    <column name='Results!*.Value.PrecursorResult.PeptideResult.ResultFile.Replicate.Name' />
    <column name='Precursor.Mz' />
    <column name='Precursor.Charge' />
    <column name='ProductMz' />
    <column name='ProductCharge' />
    <column name='FragmentIon' />
    <column name='Results!*.Value.RetentionTime' />
    <column name='Results!*.Value.Area' />
    <column name='Results!*.Value.Background' />
    <column name='Results!*.Value.PeakRank' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
</views>";
        private const string REPORTS_V2 = @"<views>
  <view name='SRM Collider Input' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist=''>
    <column name='Precursor.Peptide.Sequence' />
    <column name='Precursor.ModifiedSequence' />
    <column name='Precursor.Charge' />
    <column name='Precursor.Mz' />
    <column name='ProductMz' />
    <column name='LibraryIntensity' />
    <column name='ProductCharge' />
    <column name='FragmentIon' />
  </view>
</views>";
        private const string REPORTS_V3 = @"<views>
  <view name='Peak Boundaries' rowsource='pwiz.Skyline.Model.Databinding.Entities.Precursor' sublist='Results!*'>
    <column name='Results!*.Value.PeptideResult.ResultFile.FileName' />
    <column name='Peptide.ModifiedSequence' />
    <column name='Results!*.Value.MinStartTime' />
    <column name='Results!*.Value.MaxEndTime' />
    <column name='Charge' />
    <column name='IsDecoy' caption='PrecursorIsDecoy' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
</views>";
        private const string REPORTS_V4 = @"<views>
  <view name='Peptide Transition List' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>
    <column name='Precursor.Peptide.Protein.Name' />
    <column name='Precursor.Peptide.ModifiedSequence' />
    <column name='Precursor.Mz' />
    <column name='Precursor.Charge' />
    <column name='Precursor.CollisionEnergy' />
    <column name='ProductMz' />
    <column name='ProductCharge' />
    <column name='FragmentIon' />
    <column name='FragmentIonType' />
    <column name='FragmentIonOrdinal' />
    <column name='CleavageAa' />
    <column name='LossNeutralMass' />
    <column name='Losses' />
    <column name='LibraryRank' />
    <column name='LibraryIntensity' />
    <column name='IsotopeDistIndex' />
    <column name='IsotopeDistRank' />
    <column name='IsotopeDistProportion' />
    <column name='FullScanFilterWidth' />
    <column name='IsDecoy' />
    <column name='ProductDecoyMzShift' />
  </view>
  <view name='Mixed Transition List' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>
    <column name='Precursor.Peptide.Protein.Name' />
    <column name='Precursor.Peptide.ModifiedSequence' />
    <column name='Precursor.Peptide.IonName' />
    <column name='Precursor.Peptide.IonFormula' />
    <column name='Precursor.Mz' />
    <column name='Precursor.Charge' />
    <column name='Precursor.CollisionEnergy' />
    <column name='Precursor.ExplicitCollisionEnergy' />
    <column name='Precursor.Peptide.ExplicitRetentionTime' />
    <column name='Precursor.Peptide.ExplicitRetentionTimeWindow' />
    <column name='ProductMz' />
    <column name='ProductCharge' />
    <column name='FragmentIon' />
    <column name='ProductIonFormula' />
    <column name='FragmentIonType' />
    <column name='FragmentIonOrdinal' />
    <column name='CleavageAa' />
    <column name='LossNeutralMass' />
    <column name='Losses' />
    <column name='LibraryRank' />
    <column name='LibraryIntensity' />
    <column name='IsotopeDistIndex' />
    <column name='IsotopeDistRank' />
    <column name='IsotopeDistProportion' />
    <column name='FullScanFilterWidth' />
    <column name='IsDecoy' />
    <column name='ProductDecoyMzShift' />
  </view>
  <view name='Small Molecule Transition List' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>
    <column name='Precursor.Peptide.Protein.Name' />
    <column name='Precursor.Peptide.IonName' />
    <column name='Precursor.Peptide.IonFormula' />
    <column name='Precursor.Mz' />
    <column name='Precursor.Charge' />
    <column name='Precursor.CollisionEnergy' />
    <column name='Precursor.ExplicitCollisionEnergy' />
    <column name='Precursor.Peptide.ExplicitRetentionTime' />
    <column name='Precursor.Peptide.ExplicitRetentionTimeWindow' />
    <column name='ProductMz' />
    <column name='ProductCharge' />
    <column name='ProductIonFormula' />
  </view>
</views>";
        private const string REPORTS_V5 = @"<views>
<view name='Peptide Ratio Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='Sequence' />
    <column name='Protein.Name' />
    <column name='Results!*.Value.ResultFile.Replicate.Name' />
    <column name='Results!*.Value.PeptidePeakFoundRatio' />
    <column name='Results!*.Value.PeptideRetentionTime' />
    <column name='Results!*.Value.RatioToStandard' />
    <column name='Results!*.Value.Quantification' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
  <view name='Peptide Quantification' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='' />
    <column name='Protein' />
    <column name='ModifiedSequence' />C:\Users\nicksh\svn\sky_calibrationfeedback\pwiz_tools\Skyline\Model\Databinding\Entities\Replicate.cs
    <column name='StandardType' />
    <column name='StockConcentration' />
    <column name='InternalStandardConcentration' />
    <column name='ConcentrationUnits' />
    <column name='CalibrationCurve' />
    <column name='Note' />
  </view>
</views>
";
        private const string REPORTS_V6 = @"<views>
  <view name='Peptide Quantification' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='' />
    <column name='Protein' />
    <column name='ModifiedSequence' />
    <column name='StandardType' />
    <column name='InternalStandardConcentration' />
    <column name='ConcentrationMultiplier' />
    <column name='CalibrationCurve' />
    <column name='Note' />
  </view>
</views>
";
        // ReSharper restore NonLocalizedString

        #region XML Serialization

        private enum Attr
        {
            revision
        }

        #endregion

        #region Defaults

        private void AddDefaults()
        {
            if (RevisionIndex >= RevisionIndexCurrent)
            {
                return;
            }
            IList<KeyValuePair<ViewGroupId, ViewSpec>> previousDefaults = GetDefaults(RevisionIndex).ToArray();
            IList<KeyValuePair<ViewGroupId, ViewSpec>> currentDefaults = GetDefaults(RevisionIndexCurrent).ToArray();
            foreach (var keyValuePair in currentDefaults)
            {
                if (previousDefaults.Contains(keyValuePair))
                {
                    continue;
                }
                var viewGroup = keyValuePair.Key;
                var viewSpec = keyValuePair.Value;
                ViewSpecList viewSpecList = GetViewSpecList(viewGroup);
                var currentView = viewSpecList.ViewSpecs.FirstOrDefault(view => view.Name == viewSpec.Name);
                if (currentView == null || previousDefaults.Contains(new KeyValuePair<ViewGroupId, ViewSpec>(viewGroup, currentView)))
                {
                    viewSpecList = viewSpecList.ReplaceView(viewSpec.Name, viewSpec);
                    _viewSpecLists[viewGroup.Name] = viewSpecList;
                }
            }
            RevisionIndex = RevisionIndexCurrent;
        }
        #endregion

        #region For Testing

        public void ResetDefaults()
        {
            Clear();
            RevisionIndex = 0;
            AddDefaults();
        }
        #endregion
    }
}
