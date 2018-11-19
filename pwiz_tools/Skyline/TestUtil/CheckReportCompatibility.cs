﻿/*
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.Databinding.Entities;
using pwiz.Skyline.Model.DocSettings;
using Peptide = pwiz.Skyline.Model.Databinding.Entities.Peptide;
using Transition = pwiz.Skyline.Model.Databinding.Entities.Transition;

namespace pwiz.SkylineTestUtil
{
    public class CheckReportCompatibility : IDisposable
    {
        private readonly SkylineDataSchema _dataSchema;
        public CheckReportCompatibility(SrmDocument document)
        {
            IDocumentContainer documentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(documentContainer.SetDocument(document, null));
            _dataSchema = new SkylineDataSchema(documentContainer, DataSchemaLocalizer.INVARIANT);
        }

        public void Dispose()
        {
        }

        public void CheckAll()
        {
        }

        public void CheckReport(ReportSpec reportSpec)
        {
            var converter = new ReportSpecConverter(_dataSchema);
            var viewInfo = converter.Convert(reportSpec);
            Assert.IsNotNull(viewInfo);
        }

        public IRowSource GetRowSource(ViewInfo viewInfo)
        {
            var type = viewInfo.ParentColumn.PropertyType;
            if (type == typeof (Protein))
            {
                return new Proteins(_dataSchema);
            }
            if (type == typeof (Peptide))
            {
                return new Peptides(_dataSchema, new[]{IdentityPath.ROOT});
            }
            if (type == typeof (Precursor))
            {
                return new Precursors(_dataSchema, new[]{IdentityPath.ROOT});
            }
            if (type == typeof (Transition))
            {
                return new Transitions(_dataSchema, new[]{IdentityPath.ROOT});
            }
            throw new ArgumentException(string.Format("No row source for {0}", viewInfo.ParentColumn.PropertyType));
        }
        
        public static void CheckAll(SrmDocument document)
        {
            using (var checkReportCompatibility = new CheckReportCompatibility(document))
            {
                checkReportCompatibility.CheckAll();
            }
        }

        public static void ReportToCsv(ReportSpec reportSpec, SrmDocument doc, string fileName, CultureInfo cultureInfo)
        {
            var documentContainer = new MemoryDocumentContainer();
            Assert.IsTrue(documentContainer.SetDocument(doc, documentContainer.Document));
            var skylineDataSchema = new SkylineDataSchema(documentContainer, new DataSchemaLocalizer(cultureInfo, cultureInfo));
            var viewSpec = ReportSharing.ConvertAll(new[] {new ReportOrViewSpec(reportSpec)}, doc).First();
            var viewContext = new DocumentGridViewContext(skylineDataSchema);
            using (var writer = new StreamWriter(fileName))
            {
                IProgressStatus status = new ProgressStatus();
                viewContext.Export(CancellationToken.None, new SilentProgressMonitor(), ref status,
                    viewContext.GetViewInfo(ViewGroup.BUILT_IN, viewSpec), writer, viewContext.GetCsvWriter());
            }
        }
    }
}
