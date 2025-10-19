/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Web;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.Properties;

namespace pwiz.Common.DataBinding.Documentation
{
    // ReSharper disable LocalizableElement
    public class DocumentationGenerator
    {
        public DocumentationGenerator(ColumnDescriptor rootColumn)
        {
            RootColumn = rootColumn;
            StyleSheetHtml = GetStyleSheetHtml();
            IncludeHidden = true;
        }

        public bool IncludeHidden { get; set; }

        public ColumnDescriptor RootColumn { get; private set; }

        public DataSchema DataSchema { get { return RootColumn.DataSchema; } }

        public String StyleSheetHtml { get; set; }

        public string GetDocumentationHtmlPage()
        {
            StringWriter writer = new StringWriter();
            writer.WriteLine("<html><head>");
            if (null != StyleSheetHtml)
            {
                writer.WriteLine(StyleSheetHtml);
            }
            writer.WriteLine("</head><body>");
            writer.WriteLine(GenerateDocumentation(RootColumn));
            writer.WriteLine("</body></html>");
            return writer.ToString();
        }

        public string GenerateDocumentation(ColumnDescriptor rootColumn)
        {
            StringWriter stringWriter = new StringWriter();
            var processedTypes = new HashSet<Type>();
            Queue<ColumnDescriptor> columnQueue = new Queue<ColumnDescriptor>();
            columnQueue.Enqueue(rootColumn);
            while (columnQueue.Any())
            {
                ColumnDescriptor columnDescriptor = columnQueue.Dequeue();
                var rowType = DataSchema.GetWrappedValueType(columnDescriptor.PropertyType);
                if (processedTypes.Contains(rowType))
                {
                    continue;
                }
                if (!IsNestedColumn(columnDescriptor))
                {
                    processedTypes.Add(rowType);
                    var collectionInfo = DataSchema.GetCollectionInfo(rowType);
                    if (collectionInfo != null)
                    {
                        columnQueue.Enqueue(ColumnDescriptor.RootColumn(rootColumn.DataSchema, collectionInfo.ElementValueType, rootColumn.UiMode));
                    }
                    else if (!IsScalar(rowType))
                    {
                        stringWriter.WriteLine("<div id=\"" + HtmlEncode(rowType.FullName) + "\"><span class=\"RowType\">" +
                                               HtmlEncode(GetTypeName(rowType)) + "</span>");
                        string description = GetTypeDescription(rowType);
                        if (!string.IsNullOrEmpty(description))
                        {
                            stringWriter.WriteLine("<span class=\"Description\">" + HtmlEncode(description) + "</span>");
                        }
                        stringWriter.WriteLine("</div>");
                        stringWriter.WriteLine(GetDocumentation(columnDescriptor));
                    }
                }
                foreach (var child in GetChildColumns(columnDescriptor))
                {
                    if (!IncludeHidden && DataSchema.IsHidden(child))
                    {
                        continue;
                    }
                    columnQueue.Enqueue(child);
                }
            }
            return stringWriter.ToString();
        }

        public String GetDocumentation(ColumnDescriptor columnDescriptor)
        {
            StringWriter stringWriter = new StringWriter();
            stringWriter.WriteLine("<table><tr><th>Name</th><th>Description</th><th>Type</th>");
            foreach (var child in GetChildColumns(columnDescriptor))
            {
                List<string> captionClasses = new List<string> { @"ColumnCaption" };
                if (DataSchema.IsHidden(child))
                {
                    if (!IncludeHidden)
                    {
                        continue;
                    }

                    if (DataSchema.IsObsolete(child))
                    {
                        captionClasses.Add(@"Obsolete");
                    }
                    else
                    {
                        captionClasses.Add(@"Hidden");
                    }
                }

                string captionClass = string.Join(" ", captionClasses);
                var columnCaption = DataSchema.GetColumnCaption(child);

                stringWriter.Write("<tr><td class=\"" + captionClass + "\">" +
                                   HtmlEncode(columnCaption.GetCaption(DataSchema.DataSchemaLocalizer)) + "</td>");
                stringWriter.Write("<td class=\"ColumnDescription\">");
                String tooltip = DataSchema.GetColumnDescription(child);
                stringWriter.Write(HtmlEncode(tooltip));
                stringWriter.Write("</td>");
                stringWriter.Write("<td class=\"ColumnType\">");
                stringWriter.Write(GetHtmlForType(child.PropertyType));
                stringWriter.Write("</td>");
                stringWriter.WriteLine("</tr>");
                if (IsNestedColumn(child))
                {
                    stringWriter.WriteLine("<tr><td>&nbsp;</td><td colspan=\"2\">");
                    stringWriter.WriteLine(GetDocumentation(child));
                    stringWriter.WriteLine("</td></tr>");
                }
            }
            stringWriter.WriteLine("</table>");
            return stringWriter.ToString();
        }

        private string GetHtmlForType(Type type)
        {
            var collectionInfo = DataSchema.GetCollectionInfo(type);
            Type elementType;
            if (null == collectionInfo)
            {
                elementType = DataSchema.GetWrappedValueType(type);
            }
            else
            {
                elementType = collectionInfo.ElementType;
                if (collectionInfo.IsDictionary && elementType.IsGenericType &&
                    elementType.GetGenericTypeDefinition() == typeof (KeyValuePair<,>))
                {
                    elementType = elementType.GetGenericArguments()[1];
                }
            }
            bool elementTypeIsScalar = IsScalar(elementType);
            string strElement;
            if (elementTypeIsScalar)
            {
                strElement = HtmlEncode(GetTypeName(elementType));
            }
            else
            {
                strElement = "<a href=\"#" + elementType.FullName + "\">" + HtmlEncode(GetTypeName(elementType)) + "</a>";
            }
            if (null == collectionInfo)
            {
                return strElement;
            }
            if (collectionInfo.IsDictionary)
            {
                if (collectionInfo.ElementType.IsGenericType &&
                    collectionInfo.ElementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    return string.Format(Resources.DocumentationGenerator_GetHtmlForType_Map_of__0__to__1_, 
                        HtmlEncode(GetTypeName(collectionInfo.ElementType.GetGenericArguments()[0])),
                        strElement);
                }
                return string.Format(Resources.DocumentationGenerator_GetHtmlForType_Map_of__0_, strElement);
            }
            else
            {
                return string.Format(Resources.DocumentationGenerator_GetHtmlForType_List_of__0_, strElement);
            }
        }

        public string GetTypeName(Type type)
        {
            return DataSchema.GetInvariantDisplayName(RootColumn.UiMode, type).GetCaption(DataSchema.DataSchemaLocalizer);
        }

        public string GetTypeDescription(Type type)
        {
            return DataSchema.GetTypeDescription(RootColumn.UiMode, type);
        }

        private bool IsScalar(Type type)
        {
            return !DataSchema.GetPropertyDescriptors(type).Any();
        }

        public static bool IsNestedColumn(ColumnDescriptor columnDescriptor)
        {
            return columnDescriptor.GetAttributes().OfType<ChildDisplayNameAttribute>().Any();
        }

        public static IEnumerable<ColumnDescriptor> GetChildColumns(ColumnDescriptor columnDescriptor)
        {
            var collectionColumn = columnDescriptor.GetCollectionColumn();
            if (null != collectionColumn)
            {
                if (collectionColumn.PropertyType.IsGenericType &&
                    collectionColumn.PropertyType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
                {
                    return new[] { collectionColumn.ResolveChild("Key"), collectionColumn.ResolveChild("Value") };
                }
                return collectionColumn.GetChildColumns();
            }
            return columnDescriptor.GetChildColumns();
        }

        private static string HtmlEncode(string str)
        {
            return HttpUtility.HtmlEncode(str);
        }

        public static string GetStyleSheetHtml()
        {
            using (var stream = typeof(DocumentationGenerator).Assembly.GetManifestResourceStream(typeof(DocumentationGenerator),
                        "DocumentationGenerator.css")) 
            {
                if (stream == null)
                {
                    return string.Empty;
                }
                return "<style>" + new StreamReader(stream).ReadToEnd() + "</style>"; 
            }
        }
    }
    // ReSharper restore LocalizableElement
}
