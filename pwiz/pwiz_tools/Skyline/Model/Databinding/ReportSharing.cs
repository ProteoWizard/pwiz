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
using System.Xml.Serialization;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Databinding
{
    public static class ReportSharing
    {
        public static bool IsEnableLiveReports
        {
            get { return Settings.Default.EnableLiveReports; }
        }

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
            try
            {
                // First try to read the file as a pre 2.5 list of ReportSpec's
                var reportSerializer = new XmlSerializer(typeof(ReportSpecList));
                var reportSpecList = (ReportSpecList) reportSerializer.Deserialize(stream);
                return reportSpecList.Select(report => new ReportOrViewSpec(report)).ToList();
            }
            catch (Exception)
            {
                if (!IsEnableLiveReports)
                {
                    throw;
                }
                try
                {
                    // Next try to read it as a ViewSpecList, which was mistakenly exported from Skyline 2.5
                    stream.Seek(0, SeekOrigin.Begin);
                    var viewSerializer = new XmlSerializer(typeof(ViewSpecList));
                    ViewSpecList viewSpecList = (ViewSpecList)viewSerializer.Deserialize(stream);
                    return viewSpecList.ViewSpecs.Select(view=>new ReportOrViewSpec(view)).ToList();
                }
                catch (Exception)
                {
                    // Next try to read it as a ReportOrViewSpecList, which is the current format.
                    stream.Seek(0, SeekOrigin.Begin);
                    var reportOrViewSpecListSerializer = new XmlSerializer(typeof (ReportOrViewSpecList));
                    return ((ReportOrViewSpecList) reportOrViewSpecListSerializer.Deserialize(stream)).ToList();
                }
            }
        }

        public static bool ImportSkyrFile(string fileName, Func<IList<string>, IList<string>> whichToNotOverWrite)
        {
            if (!Settings.Default.EnableLiveReports)
            {
                return Settings.Default.ReportSpecList.ImportFile(fileName, whichToNotOverWrite);
            }
            var reportOrViewSpecList = new ReportOrViewSpecList();
            reportOrViewSpecList.AddRange(GetExistingReports().Values);
            if (!reportOrViewSpecList.ImportFile(fileName, whichToNotOverWrite))
            {
                return false;
            }
            foreach (var item in reportOrViewSpecList)
            {
                SaveReport(item);
            }
            return true;
        }

        public static IDictionary<string, ReportOrViewSpec> GetExistingReports()
        {
            if (!IsEnableLiveReports)
            {
                return Settings.Default.ReportSpecList.ToDictionary(reportSpec => reportSpec.Name,
                    reportSpec => new ReportOrViewSpec(reportSpec));
            }
            var documentGridViewContext = new DocumentGridViewContext(GetSkylineDataSchema(GetDefaultDocument(), DataSchemaLocalizer.INVARIANT));
            return SafeToDictionary(documentGridViewContext.CustomViews, 
                view => view.Name,
                view => new ReportOrViewSpec(view));
        }

        public static void SaveReport(ReportOrViewSpec reportOrViewSpec)
        {
            if (!IsEnableLiveReports)
            {
                Settings.Default.ReportSpecList.RemoveKey(reportOrViewSpec.GetKey());
                Settings.Default.ReportSpecList.Add(reportOrViewSpec.ReportSpec);
                return;
            }
            var srmDocument = GetDefaultDocument();
            var documentGridViewContext = new DocumentGridViewContext(GetSkylineDataSchema(srmDocument, DataSchemaLocalizer.INVARIANT));
            var newCustomViews =
                documentGridViewContext.CustomViews.Where(view => view.Name != reportOrViewSpec.GetKey())
                    .Concat(ConvertAll(new[]{reportOrViewSpec}, srmDocument));
            documentGridViewContext.SaveViews(newCustomViews);
        }

        public static void SaveReportAs(ReportOrViewSpec reportOrViewSpec, string newName)
        {
            if (null != reportOrViewSpec.ReportSpec)
            {
                SaveReport(new ReportOrViewSpec((ReportSpec) reportOrViewSpec.ReportSpec.ChangeName(newName)));
            }
            else if (null != reportOrViewSpec.ViewSpec)
            {
                SaveReport(new ReportOrViewSpec(reportOrViewSpec.ViewSpec.SetName(newName)));
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

        private static IDictionary<TKey, TValue> SafeToDictionary<TItem, TKey, TValue>(IEnumerable<TItem> items,
            Func<TItem, TKey> getKeyFunc, Func<TItem, TValue> getValueFunc)
        {
            var result = new Dictionary<TKey, TValue>();
            foreach (var item in items)
            {
                result[getKeyFunc(item)] = getValueFunc(item);
            }
            return result;
        }
    }
}
