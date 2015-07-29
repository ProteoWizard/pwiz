/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding
{
    [XmlRoot("report")]    
    public class ReportOrViewSpec : XmlNamedElement
    {
        private ReportOrViewSpec()
        {
        }
        public ReportOrViewSpec(ReportSpec reportSpec) : base(reportSpec.Name)
        {
            ReportSpec = reportSpec;
        }

        public ReportOrViewSpec(ViewSpec viewSpec) : base(viewSpec.Name ?? NAME_INTERNAL)
        {
            ViewSpec = viewSpec;
        }

        public ReportSpec ReportSpec { get; private set; }
        public ViewSpec ViewSpec { get; private set; }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            if (null != reader.GetAttribute("rowsource") || null != reader.GetAttribute("sublist")) // Not L10N
            {
                ViewSpec = ViewSpec.ReadXml(reader);
            }
            else
            {
                ReportSpec = ReportSpec.Deserialize(reader);
            }
        }

        public override void WriteXml(XmlWriter writer)
        {
            if (ViewSpec != null)
            {
                ViewSpec.WriteXml(writer);
            }
            else
            {
                ReportSpec.WriteXml(writer);
            }
        }
        public static ReportOrViewSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new ReportOrViewSpec());
        }

        public new ReportOrViewSpec ChangeName(string newName)
        {
            if (null != ViewSpec)
            {
                return new ReportOrViewSpec(ViewSpec.SetName(newName));
            }
            if (null != ReportSpec)
            {
                return new ReportOrViewSpec((ReportSpec) ReportSpec.ChangeName(newName));
            }
            return (ReportOrViewSpec) base.ChangeName(newName);
        }
    }

    [XmlRoot("ReportSpecList")]
    public class ReportOrViewSpecList : SerializableSettingsList<ReportOrViewSpec>, IItemEditor<ReportOrViewSpec>
    {
        public ReportOrViewSpecList()
        {
        }

        public ReportOrViewSpecList(ReportSpecList oldList)
        {
            RevisionIndex = oldList.RevisionIndex;
            var oldReports = oldList.Select(item => new ReportOrViewSpec(item));
            var convertedReports = ReportSharing.ConvertAll(oldReports, new SrmDocument(SrmSettingsList.GetDefault()));
            AddRange(convertedReports.Select(view=>new ReportOrViewSpec(view)));
            ValidateLoad();
        }

        public override int RevisionIndexCurrent { get { return 2; } }

        public override IEnumerable<ReportOrViewSpec> GetDefaults(int revisionIndex)
        {
            List<ReportOrViewSpec> list = new List<ReportOrViewSpec>();
            list.AddRange(ReportSharing.DeserializeReportList(new MemoryStream(Encoding.UTF8.GetBytes(REPORTS_V1))));
            if (revisionIndex >= 2)
            {
                list.AddRange(ReportSharing.DeserializeReportList(new MemoryStream(Encoding.UTF8.GetBytes(REPORTS_V2))));
            }
            if (revisionIndex >= 3)
            {
                list.AddRange(ReportSharing.DeserializeReportList(new MemoryStream(Encoding.UTF8.GetBytes(REPORTS_V3))));
            }
            var nameMap = new Dictionary<string, string>{
                {"Peptide Ratio Results", Resources.ReportSpecList_GetDefaults_Peptide_Ratio_Results}, // Not L10N
                {"Peptide RT Results", Resources.ReportSpecList_GetDefaults_Peptide_RT_Results}, // Not L10N
                {"Transition Results", Resources.ReportSpecList_GetDefaults_Transition_Results}, // Not L10N
                {"Peak Boundaries", Resources.ReportSpecList_GetDefaults_Peak_Boundaries} // Not L10N
            };
            for (int i = 0; i < list.Count; i++)
            {
                string newName;
                if (nameMap.TryGetValue(list[i].Name, out newName))
                {
                    list[i] = list[i].ChangeName(newName);
                }
            }

            return list;
        }

        public override string Title
        {
            get { return Resources.ReportSpecList_Title_Edit_Reports; }
        }

        public override string Label
        {
            get { return Resources.ReportSpecList_Label_Report; }
        }

        public override Type SerialType
        {
            get { return typeof(ReportOrViewSpecList); }
        }

        public override Type DeserialType
        {
            get
            {
                // .skyr files may contain one of several different formats
                // The base class implementation of DeserializeItems should never be used.
                throw new InvalidOperationException();
            }
        }

        public override ICollection<ReportOrViewSpec> CreateEmptyList()
        {
            return new ReportOrViewSpecList();
        }

        public ReportOrViewSpec NewItem(Control owner, IEnumerable<ReportOrViewSpec> existing, object tag)
        {
            return EditItem(owner, null, existing, tag);
        }

        public ReportOrViewSpec EditItem(Control owner, ReportOrViewSpec item, IEnumerable<ReportOrViewSpec> existing, object tag)
        {
            return null;
        }

        public ReportOrViewSpec CopyItem(ReportOrViewSpec item)
        {
            return new ReportOrViewSpec(item.ViewSpec.SetName(null));
        }

        protected override IList<ReportOrViewSpec> DeserializeItems(Stream stream)
        {
            return ReportSharing.DeserializeReportList(stream);
        }

        // ReSharper disable NonLocalizedString
        private const string REPORTS_V1 = @"<ReportSpecList>
  <report name='Peptide Ratio Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='Sequence' />
    <column name='Protein.Name' />
    <column name='Results!*.Value.ResultFile.Replicate.Name' />
    <column name='Results!*.Value.PeptidePeakFoundRatio' />
    <column name='Results!*.Value.PeptideRetentionTime' />
    <column name='Results!*.Value.RatioToStandard' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </report>
  <report name='Peptide RT Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Peptide' sublist='Results!*'>
    <column name='Sequence' />
    <column name='Protein.Name' />
    <column name='Results!*.Value.ResultFile.Replicate.Name' />
    <column name='PredictedRetentionTime' />
    <column name='Results!*.Value.PeptideRetentionTime' />
    <column name='Results!*.Value.PeptidePeakFoundRatio' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </report>
  <report name='Transition Results' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist='Results!*'>
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
  </report>
</ReportSpecList>";
        private const string REPORTS_V2 = @"<ReportSpecList>
  <report name='SRM Collider Input' rowsource='pwiz.Skyline.Model.Databinding.Entities.Transition' sublist=''>
    <column name='Precursor.Peptide.Sequence' />
    <column name='Precursor.ModifiedSequence' />
    <column name='Precursor.Charge' />
    <column name='Precursor.Mz' />
    <column name='ProductMz' />
    <column name='LibraryIntensity' />
    <column name='ProductCharge' />
    <column name='FragmentIon' />
  </report>
</ReportSpecList>";
        private const string REPORTS_V3 = @"<ReportSpecList>
  <report name='Peak Boundaries' rowsource='pwiz.Skyline.Model.Databinding.Entities.Precursor' sublist='Results!*'>
    <column name='Results!*.Value.PeptideResult.ResultFile.FileName' />
    <column name='Peptide.ModifiedSequence' />
    <column name='Results!*.Value.MinStartTime' />
    <column name='Results!*.Value.MaxEndTime' />
    <column name='Charge' />
    <column name='IsDecoy' caption='PrecursorIsDecoy' />
    <filter column='Results!*.Value' opname='isnotnullorblank' />
  </report>
</ReportSpecList>";
        // ReSharper restore NonLocalizedString
    }
}
