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
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.Databinding.Entities;
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

    }

    [XmlRoot("ReportSpecList")]
    public class ReportOrViewSpecList : SerializableSettingsList<ReportOrViewSpec>, IItemEditor<ReportOrViewSpec>
    {
        public override IEnumerable<ReportOrViewSpec> GetDefaults(int revisionIndex)
        {
            return new ReportSpecList().GetDefaults(revisionIndex)
                .Select(reportSpec => new ReportOrViewSpec(reportSpec));
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
            var skylineViewContext = (SkylineViewContext) tag;
            var view = item == null ? new ViewSpec().SetRowType(typeof (Protein)).SetSublistId(PropertyPath.Parse("Results!*")) : item.ViewSpec; // Not L10N
            var result = skylineViewContext.CustomizeView(owner, view);
            if (null == result)
            {
                return null;
            }
            return new ReportOrViewSpec(result);
        }

        public ReportOrViewSpec CopyItem(ReportOrViewSpec item)
        {
            return new ReportOrViewSpec(item.ViewSpec.SetName(null));
        }

        protected override IList<ReportOrViewSpec> DeserializeItems(Stream stream)
        {
            return ReportSharing.DeserializeReportList(stream);
        }
    }
}
