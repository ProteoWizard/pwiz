/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding
{
    public static class ReportSharing
    {
        /// <summary>
        /// Reads reports from a stream.
        /// The list of reports might be in one of 3 formats.
        /// <see cref="ReportSpecList"/> if it is from a version of Skyline before 2.5
        /// <see cref="ViewSpecList"/> if it was shared from "Manage Views" in the Document Grid in Skyline 2.5 
        /// (this was a mistake, it should have been a ReportOrViewSpecList).
        /// <see cref="ReportOrViewSpecList"/> if it was Shared from File>Export Report in Skyline 2.5 or greater.
        /// </summary>
        public static List<ReportOrViewSpec> DeserializeReportList(Stream stream)
        {
            XmlReader xmlReader = new XmlTextReader(stream);
            xmlReader.Read();
            if (xmlReader.IsStartElement("views")) // Not L10N
            {
                ViewSpecList viewSpecList = ViewSpecList.Deserialize(xmlReader);
                return viewSpecList.ViewSpecs.Select(view => new ReportOrViewSpec(view)).ToList();
            }
            var reportOrViewSpecList = new ReportOrViewSpecListNoDefaults();
            reportOrViewSpecList.ReadXml(xmlReader);
            return reportOrViewSpecList.ToList();
        }

        public static bool ImportSkyrFile(string fileName, Func<IList<string>, IList<string>> whichToNotOverWrite)
        {
            var reportOrViewSpecList = new ReportOrViewSpecList();
            reportOrViewSpecList.AddRange(GetExistingReports().Values);
            if (!reportOrViewSpecList.ImportFile(fileName, whichToNotOverWrite))
            {
                return false;
            }
            foreach (var item in reportOrViewSpecList)
            {
                SaveReport(PersistedViews.MainGroup, item);
            }
            return true;
        }

        public static IDictionary<ViewName, ReportOrViewSpec> GetExistingReports()
        {
            var documentGridViewContext = new DocumentGridViewContext(GetSkylineDataSchema(GetDefaultDocument(), DataSchemaLocalizer.INVARIANT));
            var items = documentGridViewContext.ViewGroups.SelectMany(group=>documentGridViewContext.GetViewSpecList(group.Id).ViewSpecs.Select(
                viewSpec=> new KeyValuePair<ViewName, ReportOrViewSpec>(
                    new ViewName(group.Id, viewSpec.Name), new ReportOrViewSpec(viewSpec))));
            return SafeToDictionary(items);
        }

        public static void SaveReport(ViewGroup viewGroup, ReportOrViewSpec reportOrViewSpec)
        {
            var srmDocument = GetDefaultDocument();
            var documentGridViewContext = new DocumentGridViewContext(GetSkylineDataSchema(srmDocument, DataSchemaLocalizer.INVARIANT));
            documentGridViewContext.AddOrReplaceViews(viewGroup.Id, ConvertAll(new[] {reportOrViewSpec}, srmDocument));
        }

        public static void SaveReportAs(ViewGroup viewPath, ReportOrViewSpec reportOrViewSpec, string newName)
        {
            if (null != reportOrViewSpec.ReportSpec)
            {
                SaveReport(viewPath, new ReportOrViewSpec((ReportSpec) reportOrViewSpec.ReportSpec.ChangeName(newName)));
            }
            else if (null != reportOrViewSpec.ViewSpec)
            {
                SaveReport(viewPath, new ReportOrViewSpec(reportOrViewSpec.ViewSpec.SetName(newName)));
            }
        }

        public static IEnumerable<ViewSpec> ConvertAll(IEnumerable<ReportOrViewSpec> reportOrViewSpecs,
            SrmDocument document)
        {
            ReportSpecConverter converter = null;
            foreach (var reportOrViewSpec in reportOrViewSpecs)
            {
                if (reportOrViewSpec.ViewSpec != null)
                {
                    yield return reportOrViewSpec.ViewSpec;
                }
                else
                {
                    converter = converter ?? new ReportSpecConverter(GetSkylineDataSchema(document, DataSchemaLocalizer.INVARIANT));
                    yield return converter.Convert(reportOrViewSpec.ReportSpec).GetViewSpec();
                }
            }
        }

        private static ViewSpec ConvertView(ReportOrViewSpec reportOrViewSpec)
        {
            return ConvertAll(new[] {reportOrViewSpec}, GetDefaultDocument()).First();
        }

        public static bool AreEquivalent(ReportOrViewSpec reportOrViewSpec1, ReportOrViewSpec reportOrViewSpec2)
        {
            if (reportOrViewSpec1.ReportSpec != null && reportOrViewSpec2.ReportSpec != null)
            {
                return reportOrViewSpec1.ReportSpec.Equals(reportOrViewSpec2.ReportSpec);
            }
            var viewSpec1 = ConvertView(reportOrViewSpec1);
            var viewSpec2 = ConvertView(reportOrViewSpec2);
            return viewSpec1.Equals(viewSpec2);
        }

        private static SrmDocument GetDefaultDocument()
        {
            return new SrmDocument(SrmSettingsList.GetDefault());
        }

        private static SkylineDataSchema GetSkylineDataSchema(SrmDocument srmDocument, DataSchemaLocalizer dataSchemaLocalizer)
        {
            var memoryDocumentContainer = new MemoryDocumentContainer();
            memoryDocumentContainer.SetDocument(srmDocument,
                memoryDocumentContainer.Document);
            return new SkylineDataSchema(memoryDocumentContainer, dataSchemaLocalizer);
        }

        private static IDictionary<TKey, TValue> SafeToDictionary<TKey, TValue>(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            var result = new Dictionary<TKey, TValue>();
            foreach (var item in items)
            {
                result[item.Key] = item.Value;
            }
            return result;
        }

        [XmlRoot("ReportSpecList")]
        public class ReportOrViewSpecListNoDefaults : ReportOrViewSpecList
        {
            public override IEnumerable<ReportOrViewSpec> GetDefaults(int revisionIndex)
            {
                return new ReportOrViewSpec[0];
            }
        }
    }
}
