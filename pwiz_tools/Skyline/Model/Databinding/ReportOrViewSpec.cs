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
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Layout;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Databinding
{
    [XmlRoot("report")]
    public class ReportOrViewSpec : XmlNamedElement
    {
        private ReportOrViewSpec()
        {
        }

        public ReportOrViewSpec(ReportSpec reportSpec) : base(reportSpec.Name ?? NAME_INTERNAL)
        {
            ReportSpec = reportSpec;
        }

        public ReportOrViewSpec(ViewSpecLayout viewSpec) : base(viewSpec.Name ?? NAME_INTERNAL)
        {
            ViewSpecLayout = viewSpec;
        }

        public ReportSpec ReportSpec { get; private set; }
        public ViewSpecLayout ViewSpecLayout { get; private set; }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            if (null != reader.GetAttribute(@"rowsource") || null != reader.GetAttribute(@"sublist"))
            {
                ViewSpecLayout = new ViewSpecLayout(ViewSpec.ReadXml(reader), ViewLayoutList.EMPTY);
            }
            else
            {
                ReportSpec = ReportSpec.Deserialize(reader);
            }
        }
        public override void WriteXml(XmlWriter writer)
        {
            if (ViewSpecLayout != null)
            {
                ViewSpecLayout.ViewSpec.WriteXml(writer);
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
            if (null != ViewSpecLayout)
            {
                return new ReportOrViewSpec(ViewSpecLayout.ChangeName(newName));
            }

            if (null != ReportSpec)
            {
                return new ReportOrViewSpec((ReportSpec)ReportSpec.ChangeName(newName));
            }

            throw new InvalidOperationException();
        }
    }
}
