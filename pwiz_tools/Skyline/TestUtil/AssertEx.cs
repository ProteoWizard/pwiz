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
using System.ComponentModel;
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
    /// <summary>
    /// AssertEx provides an extended set of Assert functions, such as collection equality, as well as
    /// new implementations of common Assert functions such as Assert.IsTrue.
    /// 
    /// These implementations give two advantages over standard Assert.* functions:
    ///    Easier to set breakpoints while debugging
    ///    Tests can optionally invoke a debugger instead of quitting, using the <see cref="Assume.DebugOnFail"/> mechanism.
    /// </summary>
    public static class AssertEx
    {
        public static void AreEqualDeep<TItem>(IList<TItem> l1, IList<TItem> l2)
        {
            AreEqual(l1.Count, l2.Count);
            for (int i = 0; i < l1.Count; i++)
            {
                if (!Equals(l1[i], l2[i]))
                {
                    AreEqual(l1[i], l2[i]);  // For setting breakpoint
                }
            }
        }

        public static void AreEqual<T>(T expected, T actual, string message = null)
        {
            if (!Equals(expected, actual))
            {
                if (Assume.InvokeDebuggerOnFail)
                {
                    Assume.Fail(message); // Handles the debugger launch
                }
                Assert.AreEqual(expected, actual, message);
            }
        }

        public static void AreNotEqual<T>(T expected, T actual, string message)
        {
            if (Equals(expected, actual))
            {
                if (Assume.InvokeDebuggerOnFail)
                {
                    Assume.Fail(message); // Handles the debugger launch
                }
                Assert.AreNotEqual(expected, actual, message);
            }
        }

        public static void AreNotSame(object expected, object actual)
        {
            if (!ReferenceEquals(expected, actual))
            {
                if (Assume.InvokeDebuggerOnFail)
                {
                    Assume.Fail(); // Handles the debugger launch
                }
                Assert.AreNotSame(expected, actual);
            }
        }

        public static void Fail(string message = null)
        {
            if (Assume.InvokeDebuggerOnFail)
            {
                Assume.Fail(message); // Handles the debugger launch
            }
            Assert.Fail(message);
        }

        public static void Fail(string message, params object[] parameters)
        {
            if (Assume.InvokeDebuggerOnFail)
            {
                Assume.Fail(message); // Handles the debugger launch
            }
            Assert.Fail(message, parameters);
        }

        public static void AreEqual(double expected, double actual, double tolerance, string message = null)
        {
            if (!Equals(expected, actual))
            {
                if (Math.Abs(expected - actual) > tolerance)
                {
                    AreEqual(expected, actual, message);
                }
            }
        }

        public static void AreEqual(double? expected, double? actual, double tolerance, string message = null)
        {
            if (!Equals(expected, actual))
            {
                if (expected.HasValue && actual.HasValue)
                {
                    AreEqual(expected.Value, actual.Value, tolerance, message);
                }
                else
                {
                    AreEqual(expected, actual, message);
                }
            }
        }

        public static void IsTrue(bool expected, string message = null)
        {
            AreEqual(expected, true, message);
        }

        public static void IsFalse(bool expected, string message = null)
        {
            AreEqual(expected, false, message);
        }

        public static void IsNull(object obj, string message = null)
        {
            AreEqual(null, obj, message);
        }

        public static void IsNotNull(object obj, string message = null)
        {
            AreNotEqual(null, obj, message);
        }


        public static void ThrowsException<TEx>(Action throwEx, string message = null)
            where TEx : Exception
        {
            ThrowsException<TEx>(() => { throwEx(); return null; }, message);
        }

        public static void ThrowsException<TEx>(Func<object> throwEx, string message = null)
            where TEx : Exception
        {
            bool exceptionThrown = false;
            try
            {
                throwEx();
            }
            catch (TEx x)
            {
                if (message != null)
                    AreComparableStrings(message, x.Message);
                exceptionThrown = true;
            }
            // Assert that an exception was thrown. We do this outside of the catch block
            // so that the AssertFailedException will not get caught if TEx is Exception.
            IsTrue(exceptionThrown, "Exception expected");
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
            catch (TEx x)
            {
                Fail(TextUtil.LineSeparate(string.Format("Unexpected exception: {0}", x.Message), x.StackTrace));
            }
        }

        public static void AreNoExceptions(IList<Exception> exceptions)
        {
            if (exceptions.Count == 0)
                return;

            Fail(TextUtil.LineSeparate(exceptions.Count == 1 ? "Unexpected exception:" : "Unexpected exceptions:",
                TextUtil.LineSeparate(exceptions.Select(x => TextUtil.LineSeparate(x.Message, ExceptionUtil.GetStackTraceText(x, null, false), string.Empty)))));
        }

        public static void Contains(string value, params string[] parts)
        {
            IsNotNull(value, "No message found");
            AreNotEqual(0, parts.Length, "Must have at least one thing contained");
            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part) && !value.Contains(part))
                    Fail("The text '{0}' does not contain '{1}'", value, part);
            }
        }

        public static void FileExists(string filePath, string message = null)
        {
            if (!File.Exists(filePath))
                Fail(TextUtil.LineSeparate(string.Format("Missing file {0}", filePath), message ?? string.Empty));
        }

        public static void FileNotExists(string filePath, string message = null)
        {
            if (File.Exists(filePath))
                Fail(TextUtil.LineSeparate(string.Format("Unexpected file exists {0}", filePath), message ?? string.Empty));
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
                        Fail("Expected error deserializing {0}:\r\n{1}", typeof(TObj).Name, s);
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
                        Fail("Unexpected exception {0} - {1}:\r\n{2}", typeof(TEx), message, x.StackTrace);
                    }
                }
                catch (TEx x)
                {
                    message = GetMessageStack(x, null);
                    if (deserializeType != DeserializeType.error)
                    {
                        Fail("Unexpected exception {0} - {1}:\r\n{2}", typeof(TEx), message, x.StackTrace);
                    }
                }
                if (expectedExceptionText != null)
                {
                    if ((message == null) || !message.Contains(expectedExceptionText))
                    {
                        Fail("Unexpected exception message for {0}: expected to contain\r\n{1}\r\nactual\r\n{2}", typeof(TEx), expectedExceptionText, message ?? "<none>");
                    }
                }
            }
        }

        public static void Serializable(SrmDocument doc)
        {
            Serializable(doc, DocumentCloned);
            VerifyModifiedSequences(doc);
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

        public static SrmDocument Serializable(SrmDocument target, string testPath, bool checkAgainstSkylineSchema = true, string expectedTypeInSkylineSchema = null, bool forceFullLoad = false)
        {
            string expected = null;
            var actual = RoundTrip(target, ref expected);
            DocumentClonedLoadable(ref target, ref actual, testPath, forceFullLoad);
            VerifyModifiedSequences(target);
            // Validate documents or document fragments against current schema
            if (checkAgainstSkylineSchema)
                ValidatesAgainstSchema(target, expectedTypeInSkylineSchema);
            return target;
        }

        /// <summary>
        /// Verifies that for every peptide and precursor in the document, the
        /// sequence that the class <see cref="ModifiedSequence" /> behaves
        /// the same as <see cref="IPrecursorMassCalc"/>.
        /// </summary>
        public static void VerifyModifiedSequences(SrmDocument doc)
        {
            foreach (var peptide in doc.Peptides)
            {
                var peptideModifiedSequence =
                    ModifiedSequence.GetModifiedSequence(doc.Settings, peptide, IsotopeLabelType.light);
                IsNotNull(peptideModifiedSequence);
                if (peptide.ModifiedSequenceDisplay != peptideModifiedSequence.ToString())
                {
                    AreEqual(peptide.ModifiedSequenceDisplay, peptideModifiedSequence.ToString());
                }
                foreach (var precursor in peptide.TransitionGroups)
                {
                    var modifiedSequence = ModifiedSequence.GetModifiedSequence(doc.Settings, peptide,
                        precursor.TransitionGroup.LabelType);
                    IsNotNull(modifiedSequence);
                    var expectedModifiedSequence = doc.Settings.GetPrecursorCalc(
                            precursor.TransitionGroup.LabelType, peptide.ExplicitMods)
                        .GetModifiedSequence(peptide.Peptide.Target, true).ToString();
                    if (expectedModifiedSequence != modifiedSequence.ToString())
                    {
                        AreEqual(expectedModifiedSequence, modifiedSequence.ToString());
                    }
                }
            }
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
                        string xmlText;
                        try
                        {
                            var ser = new XmlSerializer(obj.GetType());
                            ser.Serialize(writer, obj);
                            xmlText = sb.ToString();
                        }
                        catch (OutOfMemoryException x)
                        {
                            if (obj.GetType() == typeof (SrmDocument))
                                return;  // Just a really big document, let it slide
                            throw new OutOfMemoryException("Strangely large non-document object", x.InnerException);
                        }
                        var assembly = Assembly.GetAssembly(typeof(AssertEx));
                        var xsdName = typeof(AssertEx).Namespace + String.Format(CultureInfo.InvariantCulture, ".Schemas.Skyline_{0}.xsd", SrmDocument.FORMAT_VERSION);
                        var schemaStream = assembly.GetManifestResourceStream(xsdName);
                        IsNotNull(schemaStream, string.Format("Schema {0} not found in TestUtil assembly", xsdName));
                        // ReSharper disable once AssignNullToNotNullAttribute
                        var schemaText = (new StreamReader(schemaStream)).ReadToEnd();
                        var xd = new XmlDocument();
                        xd.Load(new MemoryStream(Encoding.UTF8.GetBytes(schemaText)));
                        string targetXML = null;
                        if (!(obj is SrmDocument))
                        {
                            // XSD validation takes place from the root, so make the object's type a root element for test purposes.
                            // Inspired by http://stackoverflow.com/questions/715626/validating-xml-nodes-not-the-entire-document
                            var elementName = xmlText.Split('<')[2].Split(' ')[0];
                            var nodes = xd.GetElementsByTagName("xs:element");
                            int currentCount = nodes.Count;
                            for (var i = 0; i < currentCount; i++)
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
                                        IsNotNull(xd.DocumentElement);
                                        // ReSharper disable once PossibleNullReferenceException
                                        xd.DocumentElement.AppendChild(nodes[i]);
                                    }
                                }
                            }
                        }
                        
                        using (var schemaReader = new XmlNodeReader(xd))
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
                                    Fail(e.Message + "  XML text:\r\n" + xmlText);
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Fail(e.ToString());
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
            // ReSharper disable LocalizableElement
            var verStart = xmlText.IndexOf("format_version=\"", StringComparison.Ordinal) + 16;
            string schemaVer = xmlText.Substring(verStart, xmlText.Substring(verStart).IndexOf("\"", StringComparison.Ordinal));
            // ReSharper restore LocalizableElement

            ValidatesAgainstSchema(xmlText, "Skyline_" + schemaVer);
        }

        [Localizable(false)]
        public static void ValidateAuditLogAgainstSchema(string xmlText)
        {
            int documentHashIndex = xmlText.IndexOf("document_hash", StringComparison.Ordinal);
            int formatVersionIndex = xmlText.IndexOf("format_version=\"", StringComparison.Ordinal);

            string version = "0";
            if (documentHashIndex < 0)
                Fail("Invalid Audit Log. No audit_log tag found");
            if (formatVersionIndex > 0 && formatVersionIndex < documentHashIndex)
            {
                version = xmlText.Substring(formatVersionIndex + 16,
                    xmlText.Substring(formatVersionIndex + 16).IndexOf("\"", StringComparison.Ordinal));
            }

            // While a change in Skyline schema is often associated with change in audit log
            // schema, it's not always the case
            if (double.Parse(version, CultureInfo.InvariantCulture) > 4.21)
            {
                version = "4.21";
            }

            ValidatesAgainstSchema(xmlText, "AuditLog.Skyl_" + version);
        }


        public static void ValidatesAgainstSchema(string xmlText, string xsdName)
        {
            var assembly = Assembly.GetAssembly(typeof(AssertEx));
            var schemaFileName = typeof(AssertEx).Namespace + String.Format(CultureInfo.InvariantCulture, @".Schemas.{0}.xsd", xsdName);
            var schemaFile = assembly.GetManifestResourceStream(schemaFileName);
            IsNotNull(schemaFile, "could not locate a schema file called " + schemaFileName);
            // ReSharper disable once AssignNullToNotNullAttribute
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
                        Fail(e.Message + "  XML text:\r\n" + xmlText);
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
                    Fail(e.ToString());
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

        public static void NoDiff(string target, string actual, string helpMsg=null, Dictionary<int, double> columnTolerances = null)
        {
            if (helpMsg == null)
                helpMsg = String.Empty;
            else
                helpMsg += " ";
            using (StringReader readerTarget = new StringReader(target))
            using (StringReader readerActual = new StringReader(actual))
            {
                int count = 1;
                string lineEqualLast = string.Empty;
                while (true)
                {
                    string lineTarget = readerTarget.ReadLine();
                    string lineActual = readerActual.ReadLine();
                    if (lineTarget == null && lineActual == null)
                        return;
                    if (lineTarget == null)
                        Fail(GetEarlyEndingMessage(helpMsg, "Expected", count-1, lineEqualLast, lineActual, readerActual));
                    if (lineActual == null)
                        Fail(GetEarlyEndingMessage(helpMsg, "Actual", count-1, lineEqualLast, lineTarget, readerTarget));
                    if (lineTarget != lineActual)
                    {
                        bool failed = true;
                        if (columnTolerances != null)
                        {
                            // ReSharper disable PossibleNullReferenceException
                            var colsActual = lineActual.Split('\t');
                            var colsTarget = lineTarget.Split('\t');
                            // ReSharper restore PossibleNullReferenceException
                            if (colsTarget.Length == colsActual.Length)
                            {
                                failed = false; // May yet be saved by tolerance check
                                for (var c = 0; c < colsActual.Length; c++)
                                {
                                    if (colsActual[c] != colsTarget[c])
                                    {
                                        double valActual, valTarget;
                                        if (!columnTolerances.ContainsKey(c) ||
                                            !(double.TryParse(colsActual[c], out valActual) && 
                                              double.TryParse(colsTarget[c], out valTarget)) ||
                                            (Math.Abs(valActual - valTarget) > columnTolerances[c] + columnTolerances[c]/1000)) // Allow for rounding cruft
                                        {
                                            failed = true;
                                            break;
                                        }
                                    }
                                }
                            }
                        }

                        if (failed)
                            Fail(helpMsg + "Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual);
                    }

                    lineEqualLast = lineTarget;
                    count++;
                }

            }
        }

        private static string GetEarlyEndingMessage(string helpMsg, string name, int count, string lineEqualLast, string lineNext, TextReader reader)
        {
            int linesRemaining = 0;
            while (reader.ReadLine() != null)
                linesRemaining++;

            return string.Format(helpMsg + "{0} stops at line {1}:\r\n{2}\r\n>\r\n+ {3}\r\n{4} more lines",
                name, count, lineEqualLast, lineNext, linesRemaining);
        }

        public static void FileEquals(string path1, string path2, Dictionary<int, double> columnTolerances = null )
        {
            string file1 = File.ReadAllText(path1);
            string file2 = File.ReadAllText(path2);
            NoDiff(file1, file2, null, columnTolerances);
        }

        public static void FieldsEqual(string target, string actual, int countFields, bool allowForNumericPrecisionDifferences = false)
        {
            FieldsEqual(target, actual, countFields, null, allowForNumericPrecisionDifferences);
        }

        public static void FieldsEqual(string target, string actual, double tolerance, int? countFields=null)
        {
            using (StringReader readerTarget = new StringReader(target))
            using (StringReader readerActual = new StringReader(actual))
            {
                FieldsEqual(readerTarget, readerActual, countFields, null, false, 0, tolerance);
            }
        }

        public static void FieldsEqual(string target, string actual, int countFields, int? exceptIndex, bool allowForTinyNumericDifferences = false)
        {
            using (StringReader readerTarget = new StringReader(target))
            using (StringReader readerActual = new StringReader(actual))
            {
                FieldsEqual(readerTarget, readerActual, countFields, exceptIndex, allowForTinyNumericDifferences);
            }
        }

        public static void FieldsEqual(TextReader readerTarget, TextReader readerActual, int? countFields, int? exceptIndex, bool allowForTinyNumericDifferences = false, int allowedExtraLinesInActual = 0, double? tolerance=null)
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
                        Fail("Target stops at line {0}.", count);
                }
                else if (lineActual == null)
                {
                    Fail("Actual stops at line {0}.", count);
                }
                else if (lineTarget != lineActual)
                {
                    var culture = CultureInfo.InvariantCulture;
                        // for the moment at least, we are hardcoded for commas in CSV
                    string[] fieldsTarget = lineTarget.Split(new[] {','});
                    string[] fieldsActual = lineActual.Split(new[] {','});
                    if (!countFields.HasValue)
                    {
                        countFields = Math.Max(fieldsTarget.Length, fieldsActual.Length);
                    }
                    if (fieldsTarget.Length < countFields || fieldsActual.Length < countFields)
                        Fail("Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual);
                    for (int i = 0; i < countFields; i++)
                    {
                        if (exceptIndex.HasValue && exceptIndex.Value == i)
                            continue;

                        if (!Equals(fieldsTarget[i], fieldsActual[i]))
                        {
                            // test numerics with the precision presented in the output text
                            double dTarget, dActual;
                            if ((allowForTinyNumericDifferences || tolerance.HasValue) &&
                                Double.TryParse(fieldsTarget[i], NumberStyles.Float, culture, out dTarget) &&
                                Double.TryParse(fieldsActual[i], NumberStyles.Float, culture, out dActual))
                            {
                                // how much of that was decimal places?
                                var precTarget = fieldsTarget[i].Length - String.Format("{0}.", (int) dTarget).Length;
                                var precActual = fieldsActual[i].Length - String.Format("{0}.", (int) dActual).Length;
                                var prec = Math.Max(Math.Min(precTarget, precActual), 0);
                                double toler = tolerance ?? .5*((prec == 0) ? 0 : Math.Pow(10, -prec));
                                    // so .001 is seen as close enough to .0009
                                if (Math.Abs(dTarget - dActual) <= toler)
                                    continue;
                            }
                            Fail("Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual);
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
                AreEqual(expected, actual);  // In case NoDiff doesn't throw (as when problem is actually in XML read or write)
            }
        }

        public static void Cloned(SrmDocument expected, SrmDocument actual)
        {
            DocsEqual(expected, actual);
            if (ReferenceEquals(expected, actual))
            {
                AreNotSame(expected, actual);
            }
        }

        public static void Cloned(object expected, object actual)
        {
            Cloned(expected, actual, null);
        }
        public static void Cloned(object expected, object actual, object def)
        {
            if (!Equals(expected, actual))
            {
                AreEqual(expected, actual);
            }
            if (ReferenceEquals(expected, actual) && !ReferenceEquals(actual, def))
            {
                AreNotSame(expected, actual);
            }
        }

        public static void HasInnerExceptionType(Exception x, Type t)
        {
            string message = GetMessageStack(x, t);
            if (message != null)
                Fail("Expected exception type {0} not found:\r\n{1}", t.Name, message);
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
                errmsg += DocumentStateTestAreEqual("RevisionIndex", revision, document.RevisionIndex);
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
                                           int? tranGroups, int? transitions, string hint = null)
        {
            var errmsg = DocumentStateTestResultString(document, revision, groups, peptides, tranGroups, transitions);
            if (errmsg.Length > 0)
                Fail((hint??string.Empty) + errmsg);

            // Verify that no two nodes in the document tree have the same global index
            var setIndexes = new HashSet<int>();
            var nodeDuplicate = FindFirstDuplicateGlobalIndex(document, setIndexes);
            if (nodeDuplicate != null)
            {
                Fail((hint??string.Empty) + "Duplicate global index {0} found in node {1}",
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

        private static SrmDocument ForceDocumentLoad(SrmDocument target, string testDir)
        {
            string xmlSaved = null;
            RoundTrip(target, ref xmlSaved);
            // Not threadsafe! This is for tests only.
            string tmpSky;
            int attempt = 0;
            do
            {
                tmpSky = Path.Combine(testDir, string.Format(@"tmp{0}.sky", attempt++));
            } 
            while (File.Exists(tmpSky));
            File.WriteAllText(tmpSky, xmlSaved);
            using (var cmd = new Skyline.CommandLine())
            {
                IsTrue(cmd.OpenSkyFile(tmpSky)); // Handles any path shifts in database files, like our .imdb file
                var docLoad = cmd.Document;
                using (var docContainer = new ResultsTestDocumentContainer(null, tmpSky))
                {
                    docContainer.SetDocument(docLoad, null, true);
                    docContainer.AssertComplete();
                    return docContainer.Document;
                }
            }
        }

        public static void DocumentCloned(SrmDocument target, SrmDocument actual)
        {
            DocumentClonedLoadable(ref target, ref actual, null, false);
        }
        public static void DocumentClonedLoadable(ref SrmDocument target, ref SrmDocument actual, string testDir, bool forceFullLoad)
            {
            for (var retry = 0; retry < 2;)
            {
                try
                {
                    // When libraries are involved, comparison only succeeds if both sets of libs (or neither) are fully loaded -
                    // that is, if the libraryspecs agree. When we're just testing serialization, though, we may not have
                    // that degree of loadedness. So, try saving to disk then doing a full reload
                    if ((forceFullLoad || retry > 0) && !string.IsNullOrEmpty(testDir))
                    {
                        target = ForceDocumentLoad(target, testDir);
                        actual = ForceDocumentLoad(actual, testDir);
                    }
                    SettingsCloned(target.Settings, actual.Settings);
                    Cloned(target, actual);
                    return;
                }
                catch
                {
                    retry++;
                }
            }
            // Do it once more and allow throw
            SettingsCloned(target.Settings, actual.Settings);
            Cloned(target, actual);
        }

        public static void SettingsCloned(SrmSettings target, SrmSettings copy)
        {
            var defSet = SrmSettingsList.GetDefault();
            var defPep = defSet.PeptideSettings;
            Cloned(target.PeptideSettings.Enzyme, copy.PeptideSettings.Enzyme, defPep.Enzyme);
            Cloned(target.PeptideSettings.DigestSettings, copy.PeptideSettings.DigestSettings, defPep.DigestSettings);
            Cloned(target.PeptideSettings.Filter, copy.PeptideSettings.Filter, defPep.Filter);
            Cloned(target.PeptideSettings.Libraries, copy.PeptideSettings.Libraries, defPep.Libraries);
            Cloned(target.PeptideSettings.Modifications, copy.PeptideSettings.Modifications, defPep.Modifications);
            Cloned(target.PeptideSettings.Prediction, copy.PeptideSettings.Prediction, defPep.Prediction);
            Cloned(target.PeptideSettings, copy.PeptideSettings);
            var defTran = defSet.TransitionSettings;
            Cloned(target.TransitionSettings.Prediction, copy.TransitionSettings.Prediction, defTran.Prediction);
            Cloned(target.TransitionSettings.Filter, copy.TransitionSettings.Filter, defTran.Filter);
            Cloned(target.TransitionSettings.Libraries, copy.TransitionSettings.Libraries, defTran.Libraries);
            Cloned(target.TransitionSettings.Integration, copy.TransitionSettings.Integration, defTran.Integration);
            Cloned(target.TransitionSettings.Instrument, copy.TransitionSettings.Instrument, defTran.Instrument);
            Cloned(target.TransitionSettings.FullScan, copy.TransitionSettings.FullScan, defTran.FullScan);
            Cloned(target.TransitionSettings, copy.TransitionSettings);
            var defData = defSet.DataSettings;
            Cloned(target.DataSettings.AnnotationDefs, copy.DataSettings.AnnotationDefs, defData.AnnotationDefs);
            Cloned(target.DataSettings.GroupComparisonDefs, copy.DataSettings.GroupComparisonDefs, defData.GroupComparisonDefs);
            Cloned(target.DataSettings.Lists, copy.DataSettings.Lists, defData.Lists);
            Cloned(target.DataSettings.ViewSpecList, copy.DataSettings.ViewSpecList, defData.ViewSpecList);
            AreEqual(target.DataSettings, copy.DataSettings);  // Might both by DataSettings.DEFAULT
            if (!DataSettings.DEFAULT.Equals(target.DataSettings))
                AreNotSame(target.DataSettings, copy.DataSettings);
            AreEqual(target.MeasuredResults, copy.MeasuredResults);
            if (target.MeasuredResults != null)
                AreNotSame(target.MeasuredResults, copy.MeasuredResults);
            Cloned(target, copy);
        }

        public static void AreComparableStrings(string expected, string actual, int? replacements = null)
        {
            // Split strings on placeholders
            string[] expectedParts = Regex.Split(expected,@"{\d}");
            if (replacements.HasValue)
            {
                AreEqual(replacements, expectedParts.Length - 1,
                    string.Format("Expected {0} replacements in string resource '{1}'", replacements, expected));
            }

            int startIndex = 0;
            foreach (var expectedPart in expectedParts)
            {
                int partIndex = actual.IndexOf(expectedPart, startIndex, StringComparison.Ordinal);
                AreNotEqual(-1, partIndex,
                    string.Format("Expected part '{0}' not found in the string '{1}'", expectedPart, actual));
                startIndex = partIndex + expectedPart.Length;
            }
        }

        public static void AreEqualNullable(double? num1, double? num2, double diff)
        {
            if (num1 == null || num2 == null)
            {
                IsTrue(num1 == null && num2 == null);
            }
            else
            {
                AreEqual(num1.Value, num2.Value, diff);
            }
        }

        public static void AreEqualLines(string expected, string actual)
        {
            AreEqual(LineBracket(expected), LineBracket(actual));
        }

        public static void IsLessThan<T>(T actual, T expectedBound) where T : IComparable<T>
        {
            if (actual.CompareTo(expectedBound) >= 0)
                Fail("\"{0}\" is not less than \"{1}\"", actual, expectedBound);
        }

        public static void IsLessThanOrEqual<T>(T actual, T expectedBound) where T : IComparable<T>
        {
            if (actual.CompareTo(expectedBound) > 0)
                Fail("\"{0}\" is not less than or equal to \"{1}\"", actual, expectedBound);
        }

        public static void IsGreaterThan<T>(T actual, T expectedBound) where T : IComparable<T>
        {
            if (actual.CompareTo(expectedBound) <= 0)
                Fail("\"{0}\" is not greater than \"{1}\"", actual, expectedBound);
        }

        public static void IsGreaterThanOrEqual<T>(T actual, T expectedBound) where T : IComparable<T>
        {
            if (actual.CompareTo(expectedBound) < 0)
                Fail("\"{0}\" is not greater than or equal to \"{1}\"", actual, expectedBound);
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
        public static void ConvertedSmallMoleculeDocumentIsSimilar(SrmDocument document, SrmDocument converted, string testDir, RefinementSettings.ConvertToSmallMoleculesMode conversionMode)
        {
            // Are both versions valid?
            converted = Serializable(converted, testDir, true, null, true); // Force a full load to verify library correctness
            document = Serializable(document, testDir, true, null, true); // Force a full load to verify library correctness

            using (var convertedMoleculeGroupsIterator = converted.MoleculeGroups.GetEnumerator())
            {
                foreach (var peptideGroupDocNode in document.MoleculeGroups)
                {
                    convertedMoleculeGroupsIterator.MoveNext();
                    ConvertedSmallMoleculePeptideGroupIsSimilar(convertedMoleculeGroupsIterator.Current, peptideGroupDocNode, conversionMode);
                }
                IsFalse(convertedMoleculeGroupsIterator.MoveNext());
            }
        }

        private static void ConvertedSmallMoleculePeptideGroupIsSimilar(PeptideGroupDocNode convertedMoleculeGroupDocNode,
            PeptideGroupDocNode peptideGroupDocNode, RefinementSettings.ConvertToSmallMoleculesMode conversionMode)
        {
            using (var convertedMoleculesIterator = convertedMoleculeGroupDocNode.Molecules.GetEnumerator())
            {
                foreach (var mol in peptideGroupDocNode.Molecules)
                {
                    convertedMoleculesIterator.MoveNext();
                    var convertedMol = convertedMoleculesIterator.Current;
                    Assert.IsNotNull(convertedMol);
                    // ReSharper disable once PossibleNullReferenceException
                    if (convertedMol.Note != null)
                        AreEqual(mol.Note ?? string.Empty,
                            convertedMol.Note.Replace(RefinementSettings.TestingConvertedFromProteomic, string.Empty));
                    else
                        Fail(@"unexpected empty note"); 
                    AreEqual(mol.SourceKey, convertedMol.SourceKey);
                    AreEqual(mol.Rank, convertedMol.Rank);
                    AreEqual(mol.Results, convertedMol.Results);
                    AreEqual(mol.ExplicitRetentionTime, convertedMol.ExplicitRetentionTime);
                    AreEqual(mol.BestResult, convertedMol.BestResult);
                    ConvertedSmallMoleculeIsSimilar(convertedMol, mol, conversionMode);
                }
                IsFalse(convertedMoleculesIterator.MoveNext());
            }
        }

        private static void ConvertedSmallMoleculeTransitionGroupResultIsSimilar(TransitionGroupDocNode convertedGroup,
            TransitionGroupDocNode group, RefinementSettings.ConvertToSmallMoleculesMode conversionMode)
        {

            if (convertedGroup.IsotopeDist == null)
                Assume.IsNull(group.IsotopeDist);
            else if (conversionMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)
                Assume.IsTrue(convertedGroup.IsotopeDist.IsSimilar(group.IsotopeDist));

            if (conversionMode == RefinementSettings.ConvertToSmallMoleculesMode.masses_only && group.Results != null)
            {
                // All we can really expect is that retention times agree - but nothing beyond that, not even the peak width
                Assume.AreEqual(group.Results.Count, convertedGroup.Results.Count);
                for (var i = 0; i < group.Results.Count; i++)
                {
                    Assume.AreEqual(group.Results[i].Count, convertedGroup.Results[i].Count);
                    for (var j = 0; j < group.Results[i].Count; j++)
                    {
                        Assume.AreEqual(group.Results[i][j].RetentionTime, convertedGroup.Results[i][j].RetentionTime, group + " vs " + convertedGroup);
                    }
                }
                return;
            }
            if (!Equals(group.Results, convertedGroup.Results))
                AreEqual(group.Results, convertedGroup.Results, group + " vs " + convertedGroup);
        }

        private static void ConvertedSmallMoleculeIsSimilar(PeptideDocNode convertedMol, PeptideDocNode mol, RefinementSettings.ConvertToSmallMoleculesMode conversionMode)
        {
            using (var convertedTransitionGroupIterator = convertedMol.TransitionGroups.GetEnumerator())
            {
                foreach (var transitionGroupDocNode in mol.TransitionGroups)
                {
                    convertedTransitionGroupIterator.MoveNext();
                    var convertedTransitionGroupDocNode = convertedTransitionGroupIterator.Current;
                    ConvertedSmallMoleculePrecursorIsSimilar(convertedTransitionGroupDocNode, transitionGroupDocNode, conversionMode);
                    IsNotNull(convertedTransitionGroupDocNode);
                    // ReSharper disable once PossibleNullReferenceException
                    AreEqual(transitionGroupDocNode.TransitionGroup.PrecursorCharge,
                        convertedTransitionGroupDocNode.TransitionGroup.PrecursorCharge);
                    AreEqual(transitionGroupDocNode.TransitionGroup.LabelType,
                        convertedTransitionGroupDocNode.TransitionGroup.LabelType);
                    AreEqual(transitionGroupDocNode.PrecursorMz, convertedTransitionGroupDocNode.PrecursorMz,
                        SequenceMassCalc.MassTolerance, "transitiongroup as small molecule");
                    ConvertedSmallMoleculeTransitionGroupResultIsSimilar(convertedTransitionGroupDocNode, transitionGroupDocNode, conversionMode);
                    AreEqual(transitionGroupDocNode.TransitionGroup.PrecursorCharge,
                        convertedTransitionGroupDocNode.TransitionGroup.PrecursorCharge);
                    AreEqual(transitionGroupDocNode.TransitionGroup.LabelType,
                        convertedTransitionGroupDocNode.TransitionGroup.LabelType);

                }
                IsFalse(convertedTransitionGroupIterator.MoveNext());
            }
        }

        private static void ConvertedSmallMoleculePrecursorIsSimilar(TransitionGroupDocNode convertedTransitionGroupDocNode, TransitionGroupDocNode transitionGroupDocNode, RefinementSettings.ConvertToSmallMoleculesMode conversionMode)
        {
            using (var convertedTransitionIterator = convertedTransitionGroupDocNode.Transitions.GetEnumerator())
            {
                foreach (var transition in transitionGroupDocNode.Transitions)
                {
                    convertedTransitionIterator.MoveNext();
                    var convertedTransition = convertedTransitionIterator.Current;
                    IsNotNull(convertedTransition);
                    // ReSharper disable once PossibleNullReferenceException
                    if (Math.Abs(transition.Mz - convertedTransition.Mz) > SequenceMassCalc.MassTolerance)
                        AreEqual(transition.Mz, convertedTransition.Mz, "mz mismatch transition as small molecule");
                    AreEqual(transition.IsotopeDistInfo, convertedTransition.IsotopeDistInfo);
                    if (conversionMode == RefinementSettings.ConvertToSmallMoleculesMode.formulas && 
                        !Equals(transition.Results, convertedTransition.Results))
                        AreEqual(transition.Results, convertedTransition.Results, "results mismatch transition as small molecule");
                }
                IsFalse(convertedTransitionIterator.MoveNext());
            }
        }
    }
}
