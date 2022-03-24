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
    /// Holds all of the Skyline views (aka "reports") that get saved in <see cref="Settings.PersistedViews"/>, and
    /// thus appear in the Document Grid "Reports" dropdown button.
    /// 
    /// This class replaces <see cref="Settings.ViewSpecList"/> and <see cref="Settings.ReportSpecList"/>.
    ///
    /// TO ADD OR CHANGE A DEFAULT REPORT
    ///
    /// 1) increase the number that is returned by "PersistedViews.RevisionIndexCurrent" (necessary even mid-release-cycle if there has been an official Daily release).
    /// 2) add a new string constant defining the new or revised report. If there's already a report by that name, the more recent one is what shows up in Skyline.
    /// 3) change the PersistedViews.GetDefaults(int revisionIndex) method so that it adds the new string to "reportString".
    /// 
    /// A convenient way to create the string constant defining a report is to build the report in Skyline, then export the report
    /// definition with "File > Export > Report > Edit List > Share" (or you can do it from "Manage Views" on the Document Grid).
    /// Then open the.skyr file in Notepad and replace all of the double quotes with single quotes and paste the contents of the file into
    /// a @"" string constant. Find the commit in which this comment was added for an example.
    ///
    /// Note that if you skip step 1, users who have installed the current Skyline-Daily will not see the new report(s) when they upgrade.
    /// On your own computer, if you want to see the new reports without increasing the version number, you can go to Tools > Options > Miscellaneous
    /// and push the "Clear all saved settings" button.
    /// 
    /// </summary>
    [XmlRoot("persisted_views")]
    public class PersistedViews : SerializableViewGroups
    {
        public static readonly ViewGroup MainGroup = new ViewGroup(@"main",
            () => Resources.PersistedViews_MainGroup_Main);
        public static readonly ViewGroup ExternalToolsGroup = new ViewGroup(@"external_tools",
            () => Resources.PersistedViews_ExternalToolsGroup_External_Tools);

        /// <summary>
        /// Construct a new PersistedViews, migrating over the values from the old ViewSpecList 
        /// and ReportSpecList properties.  Views that are in use by an external tool get put in
        /// the External Tools group, and views that are
        /// </summary>
        public PersistedViews(ReportSpecList reportSpecList, ViewSpecList viewSpecList, ToolList toolList)
        {
            var viewItems = new List<ViewSpecLayout>();
            if (null != viewSpecList)
            {
                viewItems.AddRange(viewSpecList.ViewSpecLayouts);
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
            var viewSpecLists = new Dictionary<ViewGroup, Dictionary<string, ViewSpecLayout>>();
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
                Dictionary<string, ViewSpecLayout> list;
                if (!viewSpecLists.TryGetValue(group, out list))
                {
                    list = new Dictionary<string, ViewSpecLayout>();
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
                            list.Add(name, viewItem.ChangeName(name));
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
        public int RevisionIndexCurrent { get { return 12; } } // v12 adds small mol peak boundaries report
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
            if (revisionIndex >= 7)
            {
                reportStrings.Add(REPORTS_V7);
            }
            if (revisionIndex >= 8)
            {
                reportStrings.Add(REPORTS_V8);
            }
            if (revisionIndex >= 10)
            {
                reportStrings.Add(REPORTS_V10);
            }

            if (revisionIndex >= 11)
            {
                reportStrings.Add(REPORTS_V11);
            }

            if (revisionIndex >= 12)
            {
                reportStrings.Add(REPORTS_V12); // Including molecule peak boundaries export
            }

            var list = new List<KeyValuePair<ViewGroupId, ViewSpec>>();
            var xmlSerializer = new XmlSerializer(typeof(ViewSpecList));
            foreach (var reportString in reportStrings)
            {
                list.AddRange(((ViewSpecList)xmlSerializer.Deserialize(new StringReader(reportString))).ViewSpecs.Select(spec=>new KeyValuePair<ViewGroupId, ViewSpec>(MainGroup.Id, spec)));
            }
            // ReSharper disable LocalizableElement
            var nameMap = new Dictionary<string, ViewName>{
                {"Peptide Ratio Results", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results)},
                {"Peptide Quantification", MainGroup.Id.ViewName(Resources.Resources_ReportSpecList_GetDefaults_Peptide_Quantification)},
                {"Peptide RT Results", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Peptide_RT_Results)},
                {"Transition Results", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Transition_Results)},
                {"Peak Boundaries", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Peak_Boundaries)},
                {"Molecule Peak Boundaries", MainGroup.Id.ViewName(Resources.ReportSpecList_GetDefaults_Molecule_Peak_Boundaries)},
                {"Peptide Transition List", MainGroup.Id.ViewName(Resources.SkylineViewContext_GetTransitionListReportSpec_Peptide_Transition_List)},
                {"Small Molecule Transition List", MainGroup.Id.ViewName(Resources.SkylineViewContext_GetTransitionListReportSpec_Small_Molecule_Transition_List)},
                {"Molecule Quantification", MainGroup.Id.ViewName(Resources.PersistedViews_GetDefaults_Molecule_Quantification)},
                {"Molecule Ratio Results", MainGroup.Id.ViewName(Resources.PersistedViews_GetDefaults_Molecule_Ratio_Results)},
                {"SRM Collider Input", ExternalToolsGroup.Id.ViewName("SRM Collider Input")},
            };
            // ReSharper restore LocalizableElement

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

        // ReSharper disable LocalizableElement
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
  <view name='Small Molecule Transition List' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>
    <column name='Precursor.Peptide.Protein.Name' />
    <column name='Precursor.Peptide.MoleculeName' />
    <column name='Precursor.Peptide.MoleculeFormula' />
    <column name='Precursor.IonFormula' />
    <column name='Precursor.NeutralFormula' />
    <column name='Precursor.Adduct' />
    <column name='Precursor.Mz' />
    <column name='Precursor.Charge' />
    <column name='Precursor.CollisionEnergy' />
    <column name='Precursor.ExplicitCollisionEnergy' />
    <column name='Precursor.Peptide.ExplicitRetentionTime' />
    <column name='Precursor.Peptide.ExplicitRetentionTimeWindow' />
    <column name='ProductMz' />
    <column name='ProductCharge' />
    <column name='ProductIonFormula' />
    <column name='ProductNeutralFormula' />
    <column name='ProductAdduct' />
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
        private const string REPORTS_V7 = @"<views>
  <view name='Peptide Quantification' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='' />
    <column name='Protein' />
    <column name='ModifiedSequence' />
    <column name='StandardType' />
    <column name='InternalStandardConcentration' />
    <column name='ConcentrationMultiplier' />
    <column name='NormalizationMethod' />
    <column name='CalibrationCurve' />
    <column name='Note' />
  </view>
</views>
";

        private const string REPORTS_V8 = @"<views>
  <view name='Peptide Ratio Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='' />
    <column name='Protein' />
    <column name='Results!*.Value.ResultFile.Replicate' />
    <column name='Results!*.Value.PeptidePeakFoundRatio' />
    <column name='Results!*.Value.PeptideRetentionTime' />
    <column name='Results!*.Value.RatioToStandard' />
    <column name='Results!*.Value.Quantification' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
  <view name='Peptide RT Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='' />
    <column name='Protein' />
    <column name='Results!*.Value.ResultFile.Replicate' />
    <column name='PredictedRetentionTime' />
    <column name='Results!*.Value.PeptideRetentionTime' />
    <column name='Results!*.Value.PeptidePeakFoundRatio' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
  <view name='Transition Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>
    <column name='Precursor.Peptide' />
    <column name='Precursor.Peptide.Protein' />
    <column name='Results!*.Value.PrecursorResult.PeptideResult.ResultFile.Replicate' />
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
</views>
";

        // There is no REPORTS_V9 in order to work around a problem where V4 was changed.

        private const string REPORTS_V10 = @"<views>
  <view name='Small Molecule Transition List' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*' uimode='small_molecules'>
    <column name='Precursor.Peptide.Protein.Name' />
    <column name='Precursor.Peptide.MoleculeName' />
    <column name='Precursor.Peptide.MoleculeFormula' />
    <column name='Precursor.IonFormula' />
    <column name='Precursor.NeutralFormula' />
    <column name='Precursor.Adduct' />
    <column name='Precursor.Mz' />
    <column name='Precursor.Charge' />
    <column name='Precursor.CollisionEnergy' />
    <column name='ExplicitCollisionEnergy' />
    <column name='Precursor.Peptide.ExplicitRetentionTime' />
    <column name='Precursor.Peptide.ExplicitRetentionTimeWindow' />
    <column name='ProductMz' />
    <column name='ProductCharge' />
    <column name='ProductIonFormula' />
    <column name='ProductNeutralFormula' />
    <column name='ProductAdduct' />
  </view>
</views>
";

        private const string REPORTS_V11 = @"<views>
  <view name='Molecule Quantification' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*' uimode='small_molecules'>
    <column name='' />
    <column name='Protein' />
    <column name='StandardType' />
    <column name='InternalStandardConcentration' />
    <column name='ConcentrationMultiplier' />
    <column name='NormalizationMethod' />
    <column name='CalibrationCurve' />
    <column name='Note' />
  </view>
  <view name='Molecule Ratio Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*' uimode='small_molecules'>
    <column name='' />
    <column name='Protein' />
    <column name='Results!*.Value.ResultFile.Replicate' />
    <column name='Results!*.Value.PeptidePeakFoundRatio' />
    <column name='Results!*.Value.PeptideRetentionTime' />
    <column name='Results!*.Value.RatioToStandard' />
    <column name='Results!*.Value.Quantification' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
</views>";

        private const string REPORTS_V12 = @"<views>
  <view name='Molecule Peak Boundaries' rowsource='pwiz.Skyline.Model.Databinding.Entities.Precursor' sublist='Results!*' uimode='small_molecules'>
    <column name='Results!*.Value.PeptideResult.ResultFile.FileName' />
    <column name='Peptide' />
    <column name='Results!*.Value.MinStartTime' />
    <column name='Results!*.Value.MaxEndTime' />
    <column name='Adduct' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </view>
</views>";

        // ReSharper restore LocalizableElement

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
                    viewSpecList = viewSpecList.ReplaceView(viewSpec.Name, new ViewSpecLayout(viewSpec, viewSpecList.GetViewLayouts(viewSpec.Name)));
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
