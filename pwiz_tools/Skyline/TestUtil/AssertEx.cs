/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    public static class AssertEx
    {
        public static void AreEqualDeep<TItem>(IList<TItem> l1, IList<TItem> l2)
        {
            Assert.AreEqual(l1.Count, l2.Count);
            for (int i = 0; i < l1.Count; i++)
            {
                if (!Equals(l1[i], l2[i]))
                    Assert.AreEqual(l1[i], l2[i]);  // For setting breakpoint
            }
        }

        public static void ThrowsException<TEx>(Action throwEx, string message = null)
            where TEx : Exception
        {
            ThrowsException<TEx>(() => { throwEx(); return null; }, message);
        }

        public static void ThrowsException<TEx>(Func<object> throwEx, string message = null)
            where TEx : Exception
        {
            try
            {
                throwEx();
                Assert.Fail("Exception expected");
            }
            catch (TEx x)
            {
                if (message != null)
                    AreComparableStrings(message, x.Message);
            }
        }


        public static void NoExceptionThrown<TEx>(Action throwEx)
            where TEx : Exception
        {
            NoExceptionThrown<TEx>(() => { throwEx(); return null; });
        }

        public static void NoExceptionThrown<TEx>(Func<object> throwEx)
            where TEx : Exception
        {
            try
            {
                throwEx();
            }
            catch (TEx)
            {
                Assert.Fail("Unexception expected");
            }
        }

        public static void Contains(string value, params string[] parts)
        {
            Assert.IsNotNull(value, "No message found");
            Assert.AreNotEqual(0, parts.Length, "Must have at least one thing contained");
            foreach (string part in parts)
            {
                if (!value.Contains(part))
                    Assert.Fail("The text '{0}' does not contain '{1}'", value, part);
            }
        }

        public static TObj Deserialize<TObj>(string s)
        {
            s = XmlUtil.XML_DIRECTIVE + s;

            XmlSerializer ser = new XmlSerializer(typeof(TObj));
            using (TextReader reader = new StringReader(s))
            {
                return (TObj)ser.Deserialize(reader);
            }            
        }

        public static void Serialization<TObj>(string s, Action<TObj, TObj> validate, bool checkAgainstSkylineSchema = true, string expectedTypeInSkylineSchema = null)
            where TObj : class
        {
            Serializable(Deserialize<TObj>(s), validate, checkAgainstSkylineSchema, expectedTypeInSkylineSchema);
        }

        public static void DeserializeNoError<TObj>(string s, bool roundTrip = true, bool checkAgainstSkylineSchema = true, string expectedSkylineSchemaType = null)
            where TObj : class
        {
            DeserializeError<TObj, Exception>(s, roundTrip ? DeserializeType.roundtrip : DeserializeType.no_error, null, checkAgainstSkylineSchema, expectedSkylineSchemaType);
        }

        public static void DeserializeError<TObj>(string s, string expectedExceptionText = null)
            where TObj : class
        {
            DeserializeError<TObj, InvalidDataException>(s, expectedExceptionText);
        }

        public static void DeserializeError<TObj, TEx>(string s, string expectedExceptionText = null)
            where TEx : Exception
            where TObj : class
        {
            DeserializeError<TObj, TEx>(s, DeserializeType.error, expectedExceptionText);
        }

        private enum DeserializeType
        {
            error, no_error, roundtrip
        }

        private static void DeserializeError<TObj, TEx>(string s, DeserializeType deserializeType, string expectedExceptionText = null, bool checkAgainstSkylineSchema = true, string expectedSkylineSchemaType = null)
            where TEx : Exception
            where TObj : class
        {
            s = XmlUtil.XML_DIRECTIVE + s;

            XmlSerializer ser = new XmlSerializer(typeof(TObj));
            using (TextReader reader = new StringReader(s))
            {
                String message = null;
                try
                {
                    TObj obj = (TObj) ser.Deserialize(reader);

                    if (deserializeType == DeserializeType.error)
                    {
                        // Fail if deserialization succeeds.
                        Assert.Fail("Expected error deserializing {0}:\r\n{1}", typeof(TObj).Name, s);
                    }

                    if (deserializeType == DeserializeType.roundtrip)
                        Serializable(obj, Cloned, checkAgainstSkylineSchema, expectedSkylineSchemaType);
                }
                catch (InvalidOperationException x)
                {
                    message = GetMessageStack(x, null);
                    if (deserializeType == DeserializeType.error)
                    {
                        // Make sure the XML parsing exception was thrown
                        // with the expected innerException type.
                        HasInnerExceptionType(x, typeof(TEx));
                    }
                    else
                    {
                        Assert.Fail("Unexpected exception {0} - {1}:\r\n{2}", typeof(TEx), message, x.StackTrace);
                    }
                }
                catch (TEx x)
                {
                    message = GetMessageStack(x, null);
                    if (deserializeType != DeserializeType.error)
                    {
                        Assert.Fail("Unexpected exception {0} - {1}:\r\n{2}", typeof(TEx), message, x.StackTrace);
                    }
                }
                if (expectedExceptionText != null)
                {
                    if ((message == null) || !message.Contains(expectedExceptionText))
                    {
                        Assert.Fail("Unexpected exception message for {0}: expected to contain\r\n{1}\r\nactual\r\n{2}", typeof(TEx), expectedExceptionText, message ?? "<none>");
                    }
                }
            }
        }

        public static void Serializable(SrmDocument doc)
        {
            Serializable(doc, DocumentCloned);
        }

        public static void Serializable<TObj>(TObj target, Action<TObj, TObj> validate, bool checkAgainstSkylineSchema = true, string expectedTypeInSkylineSchema = null)
            where TObj : class
        {
            Serializable(target, 1, validate, checkAgainstSkylineSchema, expectedTypeInSkylineSchema);
        }

        public static void Serializable<TObj>(TObj target, int roundTrips, Action<TObj, TObj> validate, bool checkAgainstSkylineSchema = true, string expectedTypeInSkylineSchema = null)
            where TObj : class
        {
            string expected = null;
            for (int i = 0; i < roundTrips; i++)
                validate(target, RoundTrip(target, ref expected));

            // Validate documents or document fragments against current schema
            if (checkAgainstSkylineSchema)
                ValidatesAgainstSchema(target, expectedTypeInSkylineSchema);
        }

        /// <summary>
        /// Checks validity of a document or document fragment against the current schema
        /// </summary>
        public static void ValidatesAgainstSchema(Object obj, string expectedTypeInSkylineSchema = null)
        {
            var sb = new StringBuilder();
            using (var sw = new StringWriter(sb))
            {
                using (var writer = new XmlTextWriter(sw))
                {
                    writer.Formatting = Formatting.Indented;
                    try
                    {
                        var ser = new XmlSerializer(obj.GetType());
                        ser.Serialize(writer, obj);
                        var xmlText = sb.ToString();
                        var assembly = Assembly.GetAssembly(typeof(AssertEx));
                        var xsdName = typeof(AssertEx).Namespace + String.Format(CultureInfo.InvariantCulture, ".Schemas.Skyline_{0}.xsd", SrmDocument.FORMAT_VERSION);
                        var schemaStream = assembly.GetManifestResourceStream(xsdName);
                        Assert.IsNotNull(schemaStream);
                        var schemaText = (new StreamReader(schemaStream)).ReadToEnd();
                        string targetXML = null;
                        if (!(obj is SrmDocument))
                        {
                            // XSD validation takes place from the root, so make the object's type a root element for test purposes.
                            // Inspired by http://stackoverflow.com/questions/715626/validating-xml-nodes-not-the-entire-document
                            var elementName = xmlText.Split('<')[2].Split(' ')[0];
                            var xd = new XmlDocument();
                            xd.Load(new MemoryStream(Encoding.UTF8.GetBytes(schemaText)));
                            var nodes = xd.GetElementsByTagName("xs:element");
                            for (var i = 0; i < nodes.Count; i++)
                            {
                                var xmlAttributeCollection = nodes[i].Attributes;
                                if (xmlAttributeCollection != null &&
                                    elementName.Equals(xmlAttributeCollection.GetNamedItem("name").Value) &&
                                    (expectedTypeInSkylineSchema == null || expectedTypeInSkylineSchema.Equals(xmlAttributeCollection.GetNamedItem("type").Value)))
                                {
                                    // Insert this XML as a root element at the end of the schema
                                    xmlAttributeCollection.RemoveNamedItem("minOccurs"); // Useful only in the full sequence context
                                    xmlAttributeCollection.RemoveNamedItem("maxOccurs");
                                    var xml = nodes[i].OuterXml;
                                    if (!xml.Equals(targetXML))
                                    {
                                        // Don't enter a redundant definition
                                        targetXML = xml;
                                        var lines = schemaText.Split('\n');
                                        schemaText = String.Join("\n", lines.Take(lines.Count() - 2));
                                        schemaText += xml;
                                        schemaText += lines[lines.Count() - 2];
                                        schemaText += lines[lines.Count() - 1];
                                    }
                                }
                            }
                        }

                        using (var schemaReader = new XmlTextReader(new MemoryStream(Encoding.UTF8.GetBytes(schemaText))))
                        {
                            var schema = XmlSchema.Read(schemaReader, ValidationCallBack);
                            var readerSettings = new XmlReaderSettings
                            {
                                ValidationType = ValidationType.Schema
                            };
                            readerSettings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
                            readerSettings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                            readerSettings.ValidationEventHandler += ValidationCallBack;
                            readerSettings.Schemas.Add(schema);

                            using (var reader = XmlReader.Create(new StringReader(xmlText), readerSettings))
                            {
                                try
                                {
                                    while (reader.Read())
                                    {
                                    }
                                    reader.Close();
                                }
                                catch (Exception e)
                                {
                                    Assert.Fail(e.Message + "  XML text:\r\n" + xmlText);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Assert.Fail(e.ToString());
                    }
                }
            }
        }

        private static void OldSchemaValidationCallBack(object sender, ValidationEventArgs args)
        {
            throw (new Exception(String.Format("XML Validation error:" + args.Message)));
        }

        /// <summary>
        /// Checks validity of a document in a string against its declared schema
        /// </summary>
        public static void ValidatesAgainstSchema(string xmlText)
        {
            var verStart = xmlText.IndexOf("format_version=\"", StringComparison.Ordinal) + 16; // Not L10N
            string schemaVer = xmlText.Substring(verStart, xmlText.Substring(verStart).IndexOf("\"", StringComparison.Ordinal)); // Not L10N
            var assembly = Assembly.GetAssembly(typeof(AssertEx));
            var schemaFileName = typeof(AssertEx).Namespace + String.Format(CultureInfo.InvariantCulture, ".Schemas.Skyline_{0}.xsd", schemaVer); // Not L10N
            var schemaFile = assembly.GetManifestResourceStream(schemaFileName);   
            Assert.IsNotNull(schemaFile, "could not locate a schema file called "+schemaFileName);
            using (var schemaReader = new XmlTextReader(schemaFile))
            {
                var schema = XmlSchema.Read(schemaReader, OldSchemaValidationCallBack);
                var readerSettings = new XmlReaderSettings
                {
                    ValidationType = ValidationType.Schema
                };
                readerSettings.ValidationFlags |= XmlSchemaValidationFlags.ProcessInlineSchema;
                readerSettings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                readerSettings.ValidationEventHandler += OldSchemaValidationCallBack;
                readerSettings.Schemas.Add(schema);

                using (var reader = XmlReader.Create(new StringReader(xmlText), readerSettings))
                {
                    try
                    {
                        while (reader.Read())
                        {
                        }
                        reader.Close();
                    }
                    catch (Exception e)
                    {
                        Assert.Fail(e.Message + "  XML text:\r\n" + xmlText);
                    }
                }
            }
        }

        private static void ValidationCallBack(object sender, ValidationEventArgs args)
        {
            string message = String.Format(CultureInfo.InvariantCulture, "XML Validation error using Skyline_{0}.xsd:",
                SrmDocument.FORMAT_VERSION) + args.Message;
            if (null != args.Exception)
            {
                message = TextUtil.SpaceSeparate(message, string.Format("Line {0} Position {1}", args.Exception.LineNumber, args.Exception.LinePosition));
            }
            throw (new Exception(message));
        }

        public static TObj RoundTrip<TObj>(TObj target)
            where TObj : class
        {
            string dummy = null;
            return RoundTrip(target, ref dummy);
        }

        public static TObj RoundTrip<TObj>(TObj target, ref string expected)
            where TObj : class
        {
            XmlSerializer ser = new XmlSerializer(typeof(TObj));
            StringBuilder sb = new StringBuilder();
            using (XmlTextWriter writer = new XmlTextWriter(new StringWriter(sb)))
            {
                writer.Formatting = Formatting.Indented;

                try
                {
                    ser.Serialize(writer, target);
                    if (String.IsNullOrEmpty(expected))
                        expected = sb.ToString();
                    else
                        NoDiff(expected, sb.ToString());
                    var s = sb.ToString();
                    using (TextReader reader = new StringReader(s))
                    {
                        TObj copy = (TObj)ser.Deserialize(reader);
                        return copy;
                    }
                }
                catch (Exception e)
                {
                    Assert.Fail(e.ToString());
                }
            }

// ReSharper disable HeuristicUnreachableCode
            return null;
// ReSharper restore HeuristicUnreachableCode
        }

        public static SrmDocument RoundTripTransitionList(AbstractMassListExporter exporter)
        {
            exporter.Export(null);

            // Reverse the output lines and import into a new document
            var docExport = exporter.Document;
            var docImport = new SrmDocument(docExport.Settings);
            string transitionList = exporter.MemoryOutput.Values.ToArray()[0].ToString();
            IdentityPath pathAdded;
            var inputs = new MassListInputs(DuplicateAndReverseLines(transitionList, exporter.HasHeaders),
                CultureInfo.InvariantCulture, TextUtil.SEPARATOR_CSV);
            docImport = docImport.ImportMassList(inputs, IdentityPath.ROOT, out pathAdded);

            IsDocumentState(docImport, 1,
                                     docExport.MoleculeGroupCount,
                                     docExport.MoleculeCount,
                                     docExport.MoleculeTransitionGroupCount,
                                     docExport.MoleculeTransitionCount);
            return docImport;
        }

        /// <summary>
        /// Duplicates and reverses lines of a transition list.  The transition list import code
        /// should be able to deal with this, producing a correctly sorted, distinct set of transitions.
        /// </summary>
        private static string DuplicateAndReverseLines(string transitionList, bool hasHeader)
        {
            var listLines = new List<string>();
            using (var readerList = new StringReader(transitionList))
            {
                string line;
                while ((line = readerList.ReadLine()) != null)
                {
                    // Add each line twice
                    listLines.Add(line);
                    listLines.Add(line);
                }
            }

            string lineHeader = (hasHeader ? listLines[0] : null);
            if (hasHeader)
                listLines.RemoveRange(0, 2);                

            listLines.Reverse();

            if (hasHeader)
                listLines.Insert(0, lineHeader);

            var sb = new StringBuilder();
            listLines.ForEach(line => sb.AppendLine(line));
            return sb.ToString();
        }

        public static void NoDiff(string target, string actual, string helpMsg=null)
        {
            if (helpMsg == null)
                helpMsg = String.Empty;
            else
                helpMsg += " ";
            using (StringReader readerTarget = new StringReader(target))
            using (StringReader readerActual = new StringReader(actual))
            {
                int count = 1;
                while (true)
                {
                    string lineTarget = readerTarget.ReadLine();
                    string lineActual = readerActual.ReadLine();
                    if (lineTarget == null && lineActual == null)
                        return;
                    if (lineTarget == null)
                        Assert.Fail(helpMsg + "Target stops at line {0}.", count);
                    if (lineActual == null)
                        Assert.Fail(helpMsg + "Actual stops at line {0}.", count);
                    if (lineTarget != lineActual)
                        Assert.Fail(helpMsg + "Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual);
                    count++;
                }

            }
        }

        public static void FileEquals(string path1, string path2)
        {
            string file1 = File.ReadAllText(path1);
            string file2 = File.ReadAllText(path2);
            NoDiff(file1, file2);
        }

        public static void FieldsEqual(string target, string actual, int countFields, bool allowForNumericPrecisionDifferences = false)
        {
            FieldsEqual(target, actual, countFields, null, allowForNumericPrecisionDifferences);
        }

        public static void FieldsEqual(string target, string actual, int countFields, int? exceptIndex, bool allowForTinyNumericDifferences = false)
        {
            using (StringReader readerTarget = new StringReader(target))
            using (StringReader readerActual = new StringReader(actual))
            {
                FieldsEqual(readerTarget, readerActual, countFields, exceptIndex, allowForTinyNumericDifferences);
            }
        }

        public static void FieldsEqual(TextReader readerTarget, TextReader readerActual, int countFields, int? exceptIndex, bool allowForTinyNumericDifferences = false, int allowedExtraLinesInActual = 0)
        {

            int count = 1;
            while (true)
            {
                string lineTarget = readerTarget.ReadLine();
                string lineActual = readerActual.ReadLine();
                if (lineTarget == null && lineActual == null)
                    return;
                if (lineTarget == null)
                {
                    while ((lineActual != null) && (allowedExtraLinesInActual > 0))  // As in test mode where we add a special non-proteomic molecule node to every document
                    {
                        lineActual = readerActual.ReadLine();
                        allowedExtraLinesInActual--;
                    }
                    if (lineActual != null)
                        Assert.Fail("Target stops at line {0}.", count);
                }
                else if (lineActual == null)
                {
                    Assert.Fail("Actual stops at line {0}.", count);
                }
                else if (lineTarget != lineActual)
                {
                    var culture = CultureInfo.InvariantCulture;
                        // for the moment at least, we are hardcoded for commas in CSV
                    string[] fieldsTarget = lineTarget.Split(new[] {','});
                    string[] fieldsActual = lineActual.Split(new[] {','});
                    if (fieldsTarget.Length < countFields || fieldsActual.Length < countFields)
                        Assert.Fail("Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual);
                    for (int i = 0; i < countFields; i++)
                    {
                        if (exceptIndex.HasValue && exceptIndex.Value == i)
                            continue;

                        if (!Equals(fieldsTarget[i], fieldsActual[i]))
                        {
                            // test numerics with the precision presented in the output text
                            double dTarget, dActual;
                            if (allowForTinyNumericDifferences &&
                                Double.TryParse(fieldsTarget[i], NumberStyles.Float, culture, out dTarget) &&
                                Double.TryParse(fieldsActual[i], NumberStyles.Float, culture, out dActual))
                            {
                                // how much of that was decimal places?
                                var precTarget = fieldsTarget[i].Length - String.Format("{0}.", (int) dTarget).Length;
                                var precActual = fieldsActual[i].Length - String.Format("{0}.", (int) dActual).Length;
                                var prec = Math.Max(Math.Min(precTarget, precActual), 0);
                                double toler = .5*((prec == 0) ? 0 : Math.Pow(10, -prec));
                                    // so .001 is seen as close enough to .0009
                                if (Math.Abs(dTarget - dActual) <= toler)
                                    continue;
                            }
                            Assert.Fail("Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual);
                        }
                    }
                }
                count++;
            }
        }

        public static void DocsEqual(SrmDocument expected, SrmDocument actual)
        {
            if (!Equals(expected, actual))
            {
                // If the documents don't agree, try to show where in XML
                string expectedXML = null;
                RoundTrip(expected, ref expectedXML); // Just for the XML output
                string actualXML = null;
                RoundTrip(actual, ref actualXML); // Just for the XML output
                NoDiff(expectedXML, actualXML, "AssertEx.DocsEqual failed.  Expressing as XML to aid in debugging:");  // This should throw
                Assert.AreEqual(expected, actual);  // In case NoDiff doesn't throw (as when problem is actually in XML read or write)
            }
        }

        public static void Cloned(SrmDocument expected, SrmDocument actual)
        {
            DocsEqual(expected, actual);
            if (!ReferenceEquals(expected, actual))
            {
                Assert.AreNotSame(expected, actual);
            }
        }

        public static void Cloned(object expected, object actual)
        {
            if (!Equals(expected, actual))
            {
                Assert.AreEqual(expected, actual);
            }
            if (!ReferenceEquals(expected, actual))
            {
                Assert.AreNotSame(expected, actual);
            }
        }

        public static void HasInnerExceptionType(Exception x, Type t)
        {
            string message = GetMessageStack(x, t);
            if (message != null)
                Assert.Fail("Expected exception type {0} not found:\r\n{1}", t.Name, message);
        }

        private static string GetMessageStack(Exception x, Type t)
        {
            const string indentString = "--------------------------------------";
            int indent = 0;
            StringBuilder sb = new StringBuilder();
            while (x != null)
            {
                if (t != null && t.IsInstanceOfType(x))
                    return null;
                sb.Append(indentString.Substring(0, indent++)).Append(x.GetType().Name)
                    .Append(" : ").AppendLine(x.Message);
                x = x.InnerException;
            }
            return sb.ToString();
        }

        public static void IsDocumentTransitionGroupCount(SrmDocument document, int? transitionGroupCount, int? transitionCount = null)
        {
            IsDocumentState(document, null, null, null, transitionGroupCount, transitionCount);
        }

        public static void IsDocumentTransitionCount(SrmDocument document, int? transitionCount)
        {
            IsDocumentState(document, null, null, null, null, transitionCount);
        }

        public static void IsDocumentState(SrmDocument document, int? revision, int groups, int peptides, int transitions)
        {
            IsDocumentState(document, revision, groups, peptides, peptides, transitions);
        }

        static string DocumentStateTestAreEqual(string itemName, object expected, object actual)
        {
            if (!Equals(expected, actual))
            {
                return string.Format(itemName + " mismatch: expected {0}, actual {1}.  ", expected, actual);
            }
            return string.Empty;
        }

        public static string DocumentStateTestResultString(SrmDocument document, int? revision, int? groups, int? molecules,
            int? tranGroups, int? transitions)
        {
            string errmsg = string.Empty;
            if (revision != null)
            {
                if (Settings.Default.TestSmallMolecules && (revision == (document.RevisionIndex - 1)))
                    revision++;
                        // Presumably this got bumped up during document deserialization in our special test mode
                errmsg += DocumentStateTestAreEqual("RevisionIndex", revision, document.RevisionIndex);
            }
            if (Settings.Default.TestSmallMolecules)
            {
                // We'll have added a node at the end that the test writer didn't anticipate - bump the counts accordingly
                if (groups.HasValue)
                    groups = document.MoleculeGroups.Where(SrmDocument.IsSpecialNonProteomicTestDocNode)
                        .Aggregate(groups, (current, @group) => current + 1);
                if (molecules.HasValue)
                    molecules = document.Molecules.Where(SrmDocument.IsSpecialNonProteomicTestDocNode)
                        .Aggregate(molecules, (current, @molecule) => current + 1);
                if (tranGroups.HasValue)
                    tranGroups =
                        document.MoleculeTransitionGroups.Where(SrmDocument.IsSpecialNonProteomicTestDocNode)
                            .Aggregate(tranGroups, (current, @transgroup) => current + 1);
                if (transitions.HasValue)
                {
                    foreach (var tg in document.MoleculeTransitionGroups.Where(SrmDocument.IsSpecialNonProteomicTestDocNode))
                    {
                        transitions += tg.TransitionCount;
                    }
                }
            }
            if (groups.HasValue)
                errmsg += DocumentStateTestAreEqual("MoleculeGroupCount", groups, document.MoleculeGroupCount);
            if (molecules.HasValue)
                errmsg += DocumentStateTestAreEqual("MoleculeCount", molecules, document.MoleculeCount);
            if (tranGroups.HasValue)
                errmsg += DocumentStateTestAreEqual("MoleculeTransitionGroupCount", tranGroups, document.MoleculeTransitionGroupCount);
            if (transitions.HasValue)
                errmsg += DocumentStateTestAreEqual("MoleculeTransitionCount", transitions, document.MoleculeTransitionCount);
            return errmsg;
        }

        public static void IsDocumentState(SrmDocument document, int? revision, int? groups, int? peptides,
                                           int? tranGroups, int? transitions)
        {
            var errmsg = DocumentStateTestResultString(document, revision, groups, peptides, tranGroups, transitions);
            if (errmsg.Length > 0)
                Assert.Fail(errmsg);

            // Verify that no two nodes in the document tree have the same global index
            var setIndexes = new HashSet<int>();
            var nodeDuplicate = FindFirstDuplicateGlobalIndex(document, setIndexes);
            if (nodeDuplicate != null)
            {
                Assert.Fail("Duplicate global index {0} found in node {1}",
                    nodeDuplicate.Id.GlobalIndex, nodeDuplicate);
            }
        }

        private static DocNode FindFirstDuplicateGlobalIndex(DocNode node, HashSet<int> setIndexes)
        {
            var nodeParent = node as DocNodeParent;
            if (nodeParent != null)
            {
                foreach (var child in nodeParent.Children)
                {
                    var nodeDuplicate = FindFirstDuplicateGlobalIndex(child, setIndexes);
                    if (nodeDuplicate != null)
                        return nodeDuplicate;
                }
            }
            if (setIndexes.Contains(node.Id.GlobalIndex))
                return node;
            setIndexes.Add(node.Id.GlobalIndex);
            return null;
        }

        public static void DocumentCloned(SrmDocument target, SrmDocument actual)
        {
            SettingsCloned(target.Settings, actual.Settings);
            Cloned(target, actual);
        }

        public static void SettingsCloned(SrmSettings target, SrmSettings copy)
        {
            Cloned(target.PeptideSettings.Enzyme, copy.PeptideSettings.Enzyme);
            Cloned(target.PeptideSettings.DigestSettings, copy.PeptideSettings.DigestSettings);
            Cloned(target.PeptideSettings.Filter, copy.PeptideSettings.Filter);
            Cloned(target.PeptideSettings.Libraries, copy.PeptideSettings.Libraries);
            Cloned(target.PeptideSettings.Modifications, copy.PeptideSettings.Modifications);
            Cloned(target.PeptideSettings.Prediction, copy.PeptideSettings.Prediction);
            Cloned(target.PeptideSettings, copy.PeptideSettings);
            Cloned(target.TransitionSettings.Prediction, copy.TransitionSettings.Prediction);
            Cloned(target.TransitionSettings.Filter, copy.TransitionSettings.Filter);
            Cloned(target.TransitionSettings.Libraries, copy.TransitionSettings.Libraries);
            Cloned(target.TransitionSettings.Integration, copy.TransitionSettings.Integration);
            Cloned(target.TransitionSettings.Instrument, copy.TransitionSettings.Instrument);
            Cloned(target.TransitionSettings.FullScan, copy.TransitionSettings.FullScan);
            Cloned(target.TransitionSettings, copy.TransitionSettings);
            Cloned(target, copy);
        }

        public static void AreComparableStrings(string expected, string actual, int? replacements = null)
        {
            // Split strings on placeholders
            string[] expectedParts = Regex.Split(expected,@"{\d}");
            if (replacements.HasValue)
            {
                Assert.AreEqual(replacements, expectedParts.Length - 1,
                    "Expected {0} replacements in string resource '{1}'", replacements, expected);
            }

            int startIndex = 0;
            foreach (var expectedPart in expectedParts)
            {
                int partIndex = actual.IndexOf(expectedPart, startIndex, StringComparison.Ordinal);
                Assert.AreNotEqual(-1, partIndex,
                    "Expected part '{0}' not found in the string '{1}'", expectedPart, actual);
                startIndex = partIndex + expectedPart.Length;
            }
        }

        public static void AreEqualNullable(double? num1, double? num2, double diff)
        {
            if (num1 == null || num2 == null)
            {
                Assert.IsTrue(num1 == null && num2 == null);
            }
            else
            {
                Assert.AreEqual(num1.Value, num2.Value, diff);
            }
        }

        public static void AreEqualLines(string expected, string actual)
        {
            Assert.AreEqual(LineBracket(expected), LineBracket(actual));
        }

        /// <summary>
        /// Puts newlines before and after a string to make error reporting clearer
        /// </summary>
        private static string LineBracket(string text)
        {
            var sb = new StringBuilder();
            return sb.AppendLine().AppendLine(text).ToString();
        }

        /// <summary>
        /// Compares a peptide document with a small molecule document which is presumed to be
        /// derived from the peptide document via RefinementSettings.ConvertToSmallMolecules()
        /// </summary>
        public static void ConvertedSmallMoleculeDocumentIsSimilar(SrmDocument document, SrmDocument converted)
        {
            // Are both versions valid?
            Serializable(converted);
            Serializable(document);

            var convertedMoleculeGroupsIterator = converted.MoleculeGroups.GetEnumerator();
            foreach (var peptideGroupDocNode in document.MoleculeGroups)
            {
                convertedMoleculeGroupsIterator.MoveNext();
                var convertedMoleculeGroupDocNode = convertedMoleculeGroupsIterator.Current;
                var convertedMoleculesIterator = convertedMoleculeGroupDocNode.Molecules.GetEnumerator();
                foreach (var mol in peptideGroupDocNode.Molecules)
                {
                    convertedMoleculesIterator.MoveNext();
                    var convertedMol = convertedMoleculesIterator.Current;
                    if (convertedMol.Note != null)
                        Assert.AreEqual(mol.Note ?? string.Empty, convertedMol.Note.Replace(RefinementSettings.TestingConvertedFromProteomic, string.Empty));
                    else
                        Assert.AreEqual(mol.CustomIon.InvariantName, SrmDocument.TestingNonProteomicMoleculeName); // This was the magic test molecule
                    Assert.AreEqual(mol.SourceKey, convertedMol.SourceKey);
                    Assert.AreEqual(mol.Rank, convertedMol.Rank);
                    Assert.AreEqual(mol.Results, convertedMol.Results);
                    Assert.AreEqual(mol.ExplicitRetentionTime, convertedMol.ExplicitRetentionTime);
                    Assert.AreEqual(mol.BestResult, convertedMol.BestResult);
                    var convertedTransitionGroupIterator = convertedMol.TransitionGroups.GetEnumerator();
                    foreach (var transitionGroupDocNode in mol.TransitionGroups)
                    {
                        convertedTransitionGroupIterator.MoveNext();
                        var convertedTransitionGroupDocNode = convertedTransitionGroupIterator.Current;
                        Assert.AreEqual(transitionGroupDocNode.TransitionGroup.PrecursorCharge, convertedTransitionGroupDocNode.TransitionGroup.PrecursorCharge);
                        Assert.AreEqual(transitionGroupDocNode.TransitionGroup.LabelType, convertedTransitionGroupDocNode.TransitionGroup.LabelType);
                        Assert.AreEqual(transitionGroupDocNode.PrecursorMz, convertedTransitionGroupDocNode.PrecursorMz, SequenceMassCalc.MassTolerance, "transitiongroup as small molecule");
                        Assert.AreEqual(transitionGroupDocNode.IsotopeDist, convertedTransitionGroupDocNode.IsotopeDist);
                        // Remove any library info, since for the moment at least small molecules don't support this and it won't roundtrip
                        var resultsNew = RefinementSettings.RemoveTransitionGroupChromInfoLibraryInfo(transitionGroupDocNode);
                        Assert.AreEqual(resultsNew, convertedTransitionGroupDocNode.Results, transitionGroupDocNode + " vs " + convertedTransitionGroupDocNode);

                        Assert.AreEqual(transitionGroupDocNode.TransitionGroup.PrecursorCharge, convertedTransitionGroupDocNode.TransitionGroup.PrecursorCharge);
                        Assert.AreEqual(transitionGroupDocNode.TransitionGroup.LabelType, convertedTransitionGroupDocNode.TransitionGroup.LabelType);

                        var convertedTransitionIterator = convertedTransitionGroupDocNode.Transitions.GetEnumerator();
                        foreach (var transition in transitionGroupDocNode.Transitions)
                        {
                            convertedTransitionIterator.MoveNext();
                            var convertedTransition = convertedTransitionIterator.Current;
                            Assert.AreEqual(transition.Mz, convertedTransition.Mz, SequenceMassCalc.MassTolerance, "transition as small molecule");
                            Assert.AreEqual(transition.IsotopeDistInfo, convertedTransition.IsotopeDistInfo);
                            Assert.AreEqual(transition.Results, convertedTransition.Results);
                        }
                        Assert.IsFalse(convertedTransitionIterator.MoveNext());
                    }
                    Assert.IsFalse(convertedTransitionGroupIterator == null || convertedTransitionGroupIterator.MoveNext());
                }
                Assert.IsFalse(convertedMoleculesIterator.MoveNext());
            }
            Assert.IsFalse(convertedMoleculeGroupsIterator.MoveNext());
        }
    }
}
