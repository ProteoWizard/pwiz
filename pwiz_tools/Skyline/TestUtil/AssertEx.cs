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
using pwiz.Skyline.Model.Serialization;
using pwiz.Skyline.Util;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil.Schemas;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;

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
        public static void AreEqualDeep<TItem>(IList<TItem> l1, IList<TItem> l2, string message = null)
        {
            AreEqual(l1.Count, l2.Count);
            for (int i = 0; i < l1.Count; i++)
            {
                if (!Equals(l1[i], l2[i]))
                {
                    AreEqual(l1[i], l2[i], message);  // For setting breakpoint
                }
            }
        }

        public static void AreEqual<TKey,TValue>(IDictionary<TKey,TValue> expected, IDictionary<TKey, TValue> actual, string message = null)
        {
            AreEqual(expected.Count, actual.Count, message);

            foreach (var keyValuePairExpected in expected)
            {
                if (!actual.TryGetValue(keyValuePairExpected.Key, out var valueActual ))
                {
                    AreEqual(keyValuePairExpected.Key.ToString(), null, message);
                }
                else
                {
                    AreEqual(keyValuePairExpected.Value, valueActual, message);
                }
            }
        }

        public static void AreEqual<T>(T expected, T actual, string message = null)
        {
            if (!Equals(expected, actual))
            {
                if (string.IsNullOrEmpty(message))
                {
                    message = string.Format(@"AssertEx.AreEqual failed, expected: {0} actual: {1}", expected, actual);
                }
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
                if (string.IsNullOrEmpty(message))
                {
                    message = string.Format(@"AssertEx.AreNotEqual failed, expected: {0} actual: {1}", expected, actual);
                }
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
                var message = string.Format(@"AssertEx.AreNotSame failed, expected: {0} actual: {1}", expected, actual);
                if (Assume.InvokeDebuggerOnFail)
                {
                    Assume.Fail(message); // Handles the debugger launch
                }
                Assert.AreNotSame(expected, actual, message);
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
                Assume.Fail(string.Format(message, parameters)); // Handles the debugger launch
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

        public static void IsTrue(bool actual, string message = null)
        {
            AreEqual(true, actual, message);
        }

        public static void IsFalse(bool actual, string message = null)
        {
            AreEqual(false, actual, message);
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

        public static void ThrowsException<TEx>(Action throwEx, Action<TEx> checkException)
            where TEx : Exception
        {
            ThrowsException(() => { throwEx(); return null; }, checkException);
        }

        public static void ThrowsException<TEx>(Func<object> throwEx, string message = null)
            where TEx : Exception
        {
            ThrowsException<TEx>(throwEx, x =>
            {
                if (message != null)
                    AreComparableStrings(message, x.Message);
            });
        }

        private static void ThrowsException<TEx>(Func<object> throwEx, Action<TEx> checkException)
            where TEx : Exception
        {
            bool exceptionThrown = false;
            try
            {
                throwEx();
            }
            catch (TEx x)
            {
                checkException(x);
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
                TextUtil.LineSeparate(exceptions.Select(x => x.ToString()))));
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

        public static void DoesNotContain(string str, string substr, string message = null)
        {
            IsNotNull(str, "No message found");
            if (str.Contains(substr))
                Fail(TextUtil.LineSeparate(string.Format("The text '{0}' must not contain '{1}'", str, substr), message ?? string.Empty));
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
            if (!s.StartsWith(XmlUtil.XML_DIRECTIVE.Split(' ')[0])) // Just match "<?xml" in <?xml version="1.0" encoding="utf-16"?>"
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
            Serializable(Deserialize<TObj>(s), validate, DocumentFormat.CURRENT, checkAgainstSkylineSchema, expectedTypeInSkylineSchema);
        }

        public static void DeserializeNoError<TObj>(string s, bool roundTrip = true, bool checkAgainstSkylineSchema = true, string expectedSkylineSchemaType = null)
            where TObj : class
        {
            DeserializeError<TObj, Exception>(s, roundTrip ? DeserializeType.roundtrip : DeserializeType.no_error, DocumentFormat.CURRENT, null, checkAgainstSkylineSchema, expectedSkylineSchemaType);
        }

        public static void DeserializeNoError<TObj>(string s, DocumentFormat docFormat, bool roundTrip = true, bool checkAgainstSkylineSchema = true, string expectedSkylineSchemaType = null)
            where TObj : class
        {
            DeserializeError<TObj, Exception>(s, roundTrip ? DeserializeType.roundtrip : DeserializeType.no_error, docFormat, null, checkAgainstSkylineSchema, expectedSkylineSchemaType);
        }

        public static void DeserializeError<TObj>(string s, string expectedExceptionText = null)
            where TObj : class
        {
            DeserializeError<TObj, InvalidDataException>(s, expectedExceptionText);
        }

        public static void DeserializeError<TObj>(string s, DocumentFormat formatVersion, string expectedExceptionText = null)
            where TObj : class
        {
            DeserializeError<TObj, InvalidDataException>(s, DeserializeType.error, formatVersion, expectedExceptionText);
        }

        public static void DeserializeError<TObj, TEx>(string s, string expectedExceptionText = null)
            where TEx : Exception
            where TObj : class
        {
            DeserializeError<TObj, TEx>(s, DeserializeType.error, DocumentFormat.CURRENT, expectedExceptionText);
        }

        private enum DeserializeType
        {
            error, no_error, roundtrip
        }

        private static void DeserializeError<TObj, TEx>(string s, DeserializeType deserializeType, DocumentFormat formatVersion, string expectedExceptionText = null, bool checkAgainstSkylineSchema = true, string expectedSkylineSchemaType = null)
            where TEx : Exception
            where TObj : class
        {
            if (!s.StartsWith(XmlUtil.XML_DIRECTIVE.Split(' ')[0])) // Just match "<?xml" in <?xml version="1.0" encoding="utf-16"?>"
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
                        Serializable(obj, Cloned, formatVersion, checkAgainstSkylineSchema, expectedSkylineSchemaType);
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
            Serializable(doc, DocumentCloned, DocumentFormat.CURRENT);
            VerifyModifiedSequences(doc);
            // Skyline uses a format involving protocol buffers if the document is very large.
            // Make sure to serialize the document the other way, and make sure it's still the same.
            bool wasCompactFormat = CompactFormatOption.FromSettings().UseCompactFormat(doc);
            string oldSetting = Settings.Default.CompactFormatOption;
            try
            {
                Settings.Default.CompactFormatOption =
                    (wasCompactFormat ? CompactFormatOption.NEVER : CompactFormatOption.ALWAYS).Name;
                Assert.AreNotEqual(wasCompactFormat, CompactFormatOption.FromSettings().UseCompactFormat(doc));
                Serializable(doc, DocumentCloned);
            }
            finally
            {
                Settings.Default.CompactFormatOption = oldSetting;
            }
        }

        public static void Serializable<TObj>(TObj target, Action<TObj, TObj> validate, bool checkAgainstSkylineSchema = true, string expectedTypeInSkylineSchema = null)
            where TObj : class
        {
            Serializable(target, 1, validate, DocumentFormat.CURRENT, checkAgainstSkylineSchema, expectedTypeInSkylineSchema);
        }

        public static void Serializable<TObj>(TObj target, Action<TObj, TObj> validate, DocumentFormat formatVersion, bool checkAgainstSkylineSchema = true, string expectedTypeInSkylineSchema = null)
            where TObj : class
        {
            Serializable(target, 1, validate, formatVersion, checkAgainstSkylineSchema, expectedTypeInSkylineSchema);
        }

        public static void Serializable<TObj>(TObj target, int roundTrips, Action<TObj, TObj> validate, bool checkAgainstSkylineSchema = true, string expectedTypeInSkylineSchema = null)
            where TObj : class
        {
            Serializable(target, roundTrips, validate, DocumentFormat.CURRENT, checkAgainstSkylineSchema, expectedTypeInSkylineSchema);
        }

        public static void Serializable<TObj>(TObj target, int roundTrips, Action<TObj, TObj> validate, DocumentFormat formatVersion, bool checkAgainstSkylineSchema = true, string expectedTypeInSkylineSchema = null)
            where TObj : class
        {
            string expected = null;
            for (int i = 0; i < roundTrips; i++)
                validate(target, RoundTrip(target, ref expected));

            // Validate documents or document fragments against current schema
            if (checkAgainstSkylineSchema)
                ValidatesAgainstSchema(target, formatVersion, expectedTypeInSkylineSchema);
        }

        public static SrmDocument Serializable(SrmDocument target, string testPath, SkylineVersion skylineVersion, bool checkAgainstSkylineSchema = true, bool forceFullLoad = false)
        {
            string asXML = null;
            var actual = RoundTrip(target, skylineVersion, ref asXML);
            DocumentClonedLoadable(ref target, ref actual, testPath, forceFullLoad);
            VerifyModifiedSequences(target);
            // Validate document against indicated schema
            if (checkAgainstSkylineSchema)
                ValidatesAgainstSchema(actual, skylineVersion.SrmDocumentVersion, nameof(SrmDocument), asXML);
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
                if (peptide.ExplicitMods != null && peptide.ExplicitMods.HasCrosslinks)
                {
                    continue;
                }
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
            ValidatesAgainstSchema(obj, DocumentFormat.CURRENT, expectedTypeInSkylineSchema);
        }

        /// <summary>
        /// Checks validity of a document or document fragment against the indicated schema
        /// </summary>
        public static void ValidatesAgainstSchema(Object obj, DocumentFormat formatVersion, string expectedTypeInSkylineSchema = null)
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
                        ValidatesAgainstSchema(obj, formatVersion, expectedTypeInSkylineSchema, xmlText);
                    }
                    catch (Exception e)
                    {
                        Fail(e.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Checks validity of a document or document fragment against the indicated schema
        /// </summary>
        private static void ValidatesAgainstSchema(object obj, DocumentFormat formatVersion, string expectedTypeInSkylineSchema, string xmlText)
        {
            var assembly = Assembly.GetAssembly(typeof(AssertEx));
            var xsdName = SchemaDocuments.GetSkylineSchemaResourceName(formatVersion.ToString());
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

            ValidatesAgainstSchema(xmlText, SchemaDocuments.GetSkylineSchemaResourceName(schemaVer));
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

            ValidatesAgainstSchema(xmlText, SchemaDocuments.GetAuditLogSchemaResourceName(version));
        }


        public static void ValidatesAgainstSchema(string xmlText, string xsdResourceName)
        {
            var assembly = Assembly.GetAssembly(typeof(AssertEx));
            var schemaFile = assembly.GetManifestResourceStream(xsdResourceName);
            IsNotNull(schemaFile, "could not locate a schema file called " + xsdResourceName);
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
            string message = String.Format(CultureInfo.InvariantCulture, "XML Validation error: {0}", args.Message);
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
            using (var memStream = new MemoryStream())
            {
                XmlTextWriter writer = new XmlTextWriter(memStream, Encoding.UTF8);
                writer.Formatting = Formatting.Indented;

                try
                {
                    ser.Serialize(writer, target);
                    memStream.Seek(0, SeekOrigin.Begin);
                    string xmlString = string.Empty;
                    using (var reader = new StreamReader(memStream))
                        xmlString = reader.ReadToEnd();

                    if (String.IsNullOrEmpty(expected))
                        expected = xmlString;
                    else
                        NoDiff(expected, xmlString);
                    using (TextReader reader = new StringReader(xmlString))
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
        public static SrmDocument RoundTrip(SrmDocument target, SkylineVersion skylineVersion, ref string expected)
        {
            XmlSerializer ser = new XmlSerializer(typeof(SrmDocument));
            StringBuilder sb = new StringBuilder();
            using (XmlTextWriter writer = new XmlTextWriter(new StringWriter(sb)))
            {
                writer.Formatting = Formatting.Indented;

                try
                {
                    target.Serialize(writer, null, skylineVersion,  null);
                    if (String.IsNullOrEmpty(expected))
                        expected = sb.ToString();
                    else
                        NoDiff(expected, sb.ToString());
                    var s = sb.ToString();
                    using (TextReader reader = new StringReader(s))
                    {
                        var copy = (SrmDocument)ser.Deserialize(reader);
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
            var inputs = new MassListInputs(DuplicateAndReverseLines(transitionList, exporter.HasHeaders),
                CultureInfo.InvariantCulture, TextUtil.SEPARATOR_CSV);
            docImport = docImport.ImportMassList(inputs, null, IdentityPath.ROOT, out _);

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

        public static void NoDiff(string target, string actual, string helpMsg=null, 
            Dictionary<int, double> columnTolerances = null, // Per-column numerical tolerances if strings can be read as TSV, "-1" means any column
            bool ignorePathDifferences = false)
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
                    {
                        return; // We're done
                    }
                    if (lineTarget == null)
                    {
                        Fail(GetEarlyEndingMessage(helpMsg, "Expected", count-1, lineEqualLast, lineActual, readerActual));
                    }
                    if (lineActual == null)
                    {
                        Fail(GetEarlyEndingMessage(helpMsg, "Actual", count-1, lineEqualLast, lineTarget, readerTarget));
                    }

                    // Save original lines for report
                    var expectedLine = lineTarget;
                    var actualLine = lineActual;

                    if (ignorePathDifferences)
                    {
                        RemovePathDifferences(ref lineTarget, ref lineActual);
                    }
                    // If only difference appears to be generated GUIDs or timestamps, let it pass
                    if (!LinesEquivalentIgnoringTimeStampsAndGUIDs(lineTarget, lineActual, columnTolerances))
                    {
                        int pos;
                        for (pos = 0; pos < expectedLine?.Length && pos < actualLine?.Length && expectedLine[pos] == actualLine[pos];) {pos++;}
                        Fail(helpMsg + $@" Diff found at line {count} position {pos}: expected{Environment.NewLine}{expectedLine}{Environment.NewLine}actual{Environment.NewLine}{actualLine}");
                    }
                    lineEqualLast = expectedLine;
                    count++;
                }

            }
        }

        // Look for one or more filenames, see if they match when ignoring path, or when filenames are tempfiles
        private static void RemovePathDifferences(ref string lineExpected, ref string lineActual)
        {
            if (string.Equals(lineExpected, lineActual))
            {
                return; // Identical
            }

            var splitChars = new[] { '\t', ';', ',' };

            var colsActual = lineActual.Split(splitChars);
            var colsExpected = lineExpected.Split(splitChars);
            if (colsExpected.Length != colsActual.Length)
            {
                return; // No way we're cleaning this up to make a match
            }

            if (colsExpected.Length == 1 && lineActual.Contains(@"""")) // Is path embedded in a simple string?
            {
                // e.g. 'Import Molecule Search > Extract Chromatograms > Found results files : contains "C:\Users\bspratt\Downloads\Perftests\Label-free\Orbi3_SA_IP_pHis3_01.RAW"'
                colsActual = lineActual.Split('\"');
                colsExpected = lineExpected.Split('\"');
                if (colsExpected.Length != colsActual.Length)
                {
                    return; // No way we're cleaning this up to make a match
                }
            }

            for (var col = 0; col < colsActual.Length; col++)
            {
                var pathE = colsExpected[col];
                var pathA = colsActual[col];
                if (string.Equals(pathE, pathA))
                {
                    continue;
                }

                // Did column contain a filename?
                var partsE = pathE.Trim().Split('"'); // e.g. 'value="c:\foo\bar.baz",' => {'value=', '"c:\foo\bar.baz"', ','}
                var partsA = pathA.Trim().Split('"'); 
                if (partsE.Length != partsA.Length)
                {
                    return; // No way we're cleaning this up to make a match
                }

                for (var p = 0; p < partsE.Length; p++)
                {
                    var partE = partsE[p];
                    var partA = partsA[p];
                    if (string.Equals(partE, partA))
                    {
                        continue;
                    }

                    try
                    {
                        var fileE = Path.GetFileName(partE);
                        var fileA = Path.GetFileName(partA);
                        var tmpExt = @".tmp";
                        if (string.Equals(fileE, fileA) || // Same filename, different path
                            (Path.GetExtension(fileE) == tmpExt) && Path.GetExtension(fileA) == tmpExt) // Tmp file names will always vary
                        {
                            var ignoredPath = @"<ignored_path_difference>";
                            lineExpected = lineExpected.Replace(partE, ignoredPath);
                            lineActual = lineActual.Replace(partA, ignoredPath);
                        }
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
        }


        private static bool LinesEquivalentIgnoringTimeStampsAndGUIDs(string lineExpected, string lineActual,
            Dictionary<int, double> columnTolerances = null) // Per-column numerical tolerances if strings can be read as TSV, "-1" means any column
        {
            if (string.Equals(lineExpected, lineActual))
            {
                return true; // Identical
            }

            // If only difference appears to be a generated GUID, let it pass
            var regexGUID =
                new Regex(
                    @"(.*)\:[0123456789abcdef]*-[0123456789abcdef]*-[0123456789abcdef]*-[0123456789abcdef]*-[0123456789abcdef]*\:(.*)");
            var matchExpected = regexGUID.Match(lineExpected);
            var matchActual = regexGUID.Match(lineActual);
            if (matchExpected.Success && matchActual.Success
                                      && Equals(matchExpected.Groups[1].ToString(), matchActual.Groups[1].ToString())
                                      && Equals(matchExpected.Groups[2].ToString(), matchActual.Groups[2].ToString()))
            {
                return true;
            }

            // If only difference appears to be a generated ISO timestamp, let it pass
            // e.g. 2020-07-10T10:40:03Z or 2020-07-10T10:40:03-07:00 etc
            var regexTimestamp =
                new Regex(@"(.*"")\d\d\d\d\-\d\d\-\d\dT\d\d\:\d\d\:\d\d(?:Z|(?:[\-\+]\d\d\:\d\d))("".*)");
            matchExpected = regexTimestamp.Match(lineExpected);
            matchActual = regexTimestamp.Match(lineActual);
            if (matchExpected.Success && matchActual.Success
                                      && Equals(matchExpected.Groups[1].ToString(), matchActual.Groups[1].ToString())
                                      && Equals(matchExpected.Groups[2].ToString(), matchActual.Groups[2].ToString()))
            {
                return true;
            }

            if (columnTolerances != null)
            {
                // ReSharper disable PossibleNullReferenceException
                var colsActual = lineActual.Split('\t');
                var colsExpected = lineExpected.Split('\t');
                // ReSharper restore PossibleNullReferenceException
                if (colsExpected.Length == colsActual.Length)
                {
                    for (var c = 0; c < colsActual.Length; c++)
                    {
                        if (colsActual[c] != colsExpected[c])
                        {
                            // See if there's a tolerance for this column, or a default tolerance (column "-1" in the dictionary)
                            if ((!columnTolerances.TryGetValue(c, out var tolerance) && !columnTolerances.TryGetValue(-1, out tolerance)) || // No tolerance given for this column
                                !(TextUtil.TryParseDoubleUncertainCulture(colsActual[c], out var valActual) &&
                                  TextUtil.TryParseDoubleUncertainCulture(colsExpected[c], out var valExpected)) || // One or both don't parse as doubles
                                (Math.Abs(valActual - valExpected) > tolerance + tolerance / 1000)) // Allow for rounding cruft
                            {
                                return false; // Can't account for difference
                            }
                        }
                    }
                    return true; // Differences accounted for
                }
            }

            return false; // Could not account for difference
        }

        private static string GetEarlyEndingMessage(string helpMsg, string name, int count, string lineEqualLast, string lineNext, TextReader reader)
        {
            int linesRemaining = 0;
            while (reader.ReadLine() != null)
                linesRemaining++;

            return string.Format(helpMsg + "{0} stops at line {1}:\r\n{2}\r\n>\r\n+ {3}\r\n{4} more lines",
                name, count, lineEqualLast, lineNext, linesRemaining);
        }

        public static void FileEquals(string pathExpectedFile, string pathActualFile, Dictionary<int, double> columnTolerances = null, bool ignorePathDifferences = false )
        {
            string file1 = File.ReadAllText(pathExpectedFile);
            string file2 = File.ReadAllText(pathActualFile);
            NoDiff(file1, file2, null, columnTolerances, ignorePathDifferences);
        }

        public static void LibraryEquals(LibrarySpec libraryExpected, LibrarySpec libraryActual, double mzTolerance = 1e-8, double intensityTolerance = 1e-5)
        {
            Library expectedLoaded = null, actualLoaded = null;
            try
            {
                FileExists(libraryExpected.FilePath);
                FileExists(libraryActual.FilePath);

                var monitor = new DefaultFileLoadMonitor(new SilentProgressMonitor());
                expectedLoaded = libraryExpected.LoadLibrary(monitor);
                actualLoaded = libraryActual.LoadLibrary(monitor);
                
                Assert.AreEqual(expectedLoaded.SpectrumCount, actualLoaded.SpectrumCount, "spectrum counts not equal");

                var expectedList = expectedLoaded.Keys.ToList();
                var actualList = actualLoaded.Keys.ToList();

                for (int i=0; i < expectedList.Count; ++i)
                {
                    var expected = expectedList[i];
                    var actual = actualList[i];
                    Assert.AreEqual(expected, actual, "spectrum library keys not equal");

                    var expectedSpectra = expectedLoaded.GetSpectra(expected, IsotopeLabelType.light, LibraryRedundancy.best);
                    var expectedSpectrum = expectedSpectra.First().SpectrumPeaksInfo.Peaks;
                    var actualSpectra = actualLoaded.GetSpectra(actual, IsotopeLabelType.light, LibraryRedundancy.best);
                    var actualSpectrum = actualSpectra.First().SpectrumPeaksInfo.Peaks;
                    Assert.AreEqual(expectedSpectrum.Length, actualSpectrum.Length, "peak counts not equal");
                    for (int j = 0; j < expectedSpectrum.Length; ++j)
                    {
                        Assert.AreEqual(expectedSpectrum[j].Mz, actualSpectrum[j].Mz, mzTolerance, "peak m/z delta exceeded tolerance");
                        Assert.AreEqual(expectedSpectrum[j].Intensity, actualSpectrum[j].Intensity, intensityTolerance, "peak intensity delta exceeded tolerance");
                    }
                }
            }
            finally
            {
                expectedLoaded?.ReadStream.CloseStream();
                actualLoaded?.ReadStream.CloseStream();
            }
        }

        /// <summary>
        /// Compare two DSV files, accounting for possible L10N differences
        /// </summary>
        public static void AreEquivalentDsvFiles(string path1, string path2, bool hasHeaders, int[] ignoredColumns = null)
        {
            var lines1 = File.ReadAllLines(path1);
            var lines2 = File.ReadAllLines(path2);
            AreEqual(lines1.Length, lines2.Length, "Expected same line count");
            if (lines1.Length == 0)
            {
                return;
            }

            ignoredColumns ??= new int[] { };

            var sep1 = DetermineDsvDelimiter(lines1, out var colCount1);
            var sep2 = DetermineDsvDelimiter(lines2, out var colCount2);
            var errors = new List<string>();
            for (var lineNum = 0; lineNum < lines1.Length; lineNum++)
            {
                var cols1 = lines1[lineNum].ParseDsvFields(sep1);
                var cols2 = lines2[lineNum].ParseDsvFields(sep2);

                colCount1 = cols1.Length;
                colCount2 = cols2.Length;

                // If a rightmost column is missing don't worry if it's been declared as ignorable
                while (ignoredColumns.Contains(colCount1 - 1))
                {
                    colCount1--;
                }
                while (ignoredColumns.Contains(colCount2 - 1))
                {
                    colCount2--;
                }

                AreEqual(colCount1, colCount2, $"Expected same column count at line {lineNum}");

                if (hasHeaders && Equals(lineNum, 0) && !Equals(CultureInfo.CurrentCulture.TwoLetterISOLanguageName, @"en"))
                {
                    continue; // Don't expect localized headers to match 
                }
                for (var colNum = 0; colNum < colCount1; colNum++)
                {
                    if (ignoredColumns.Contains(colNum))
                    {
                        continue;
                    }
                    var same = Equals(cols1[colNum], cols2[colNum]);

                    if (!same)
                    {
                        // Possibly a decimal value, or even a field like "1.234[M+H]" vs "1,234[M+H]"
                        string Dotted(string val)
                        {
                            return val.Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, @"_dot_").
                                Replace(CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator, @"_dot_");
                        }
                        same = Equals(Dotted(cols1[colNum]), Dotted(cols2[colNum]));
                    }

                    if (!same)
                    {
                        errors.Add($"Difference at row {lineNum} column {colNum}: expected \"{cols1[colNum]}\" got \"{cols2[colNum]}\"");
                    }
                }
            }
            AreEqual(0, errors.Count, string.Join("\n", errors));
        }

        /// <summary>
        /// Examine the lines of a DSV file an attempt to determine what kind of delimiter it uses
        /// N.B. NOT ROBUST ENOUGH FOR GENERAL USE - would likely fail, for example, on data that has
        /// irregular column counts. But still useful in the test context where we aren't handed random
        /// data sets from users.
        /// </summary>
        /// <param name="lines">lines of the file</param>
        /// <param name="columnCount">return value: column count</param>
        /// <returns>the identified delimiter</returns>
        /// <exception cref="LineColNumberedIoException">thrown when we can't figure it out</exception>
        public static char DetermineDsvDelimiter(string[] lines, out int columnCount)
        {

            // If a candidate delimiter yields different column counts line to line, it's probably not the right one.
            // So parse some distance in to see which delimiters give a consistent column count.
            // NOTE we do see files like that in the wild, but not in our test suite
            var countsPerLinePerCandidateDelimiter = new Dictionary<char, List<int>>
            {
                { TextUtil.SEPARATOR_CSV, new List<int>()},
                { TextUtil.SEPARATOR_SPACE, new List<int>()},
                { TextUtil.SEPARATOR_TSV, new List<int>()},
                { TextUtil.SEPARATOR_CSV_INTL, new List<int>()}
            };

            for (var lineNum = 0; lineNum < Math.Min(100, lines.Length); lineNum++)
            {
                foreach (var sep in countsPerLinePerCandidateDelimiter.Keys)
                {
                    countsPerLinePerCandidateDelimiter[sep].Add((new DsvFileReader(new StringReader(lines[lineNum]), sep)).NumberOfFields);
                }
            }

            var likelyCandidates =
                countsPerLinePerCandidateDelimiter.Where(kvp => kvp.Value.Distinct().Count() == 1).ToArray();
            if (likelyCandidates.Length > 0)
            {
                // The candidate that yields the highest column count wins
                var maxColumnCount = likelyCandidates.Max(kvp => kvp.Value[0]);
                if (likelyCandidates.Count(kvp => Equals(maxColumnCount, kvp.Value[0])) == 1)
                {
                    var delimiter = likelyCandidates.First(kvp => Equals(maxColumnCount, kvp.Value[0])).Key;
                    columnCount = maxColumnCount;
                    return delimiter;
                }
            }

            throw new LineColNumberedIoException(Resources.TextUtil_DeterminDsvSeparator_Unable_to_determine_format_of_delimiter_separated_value_file, 1, 1);
        }

        public static void FieldsEqual(string target, string actual, int countFields, bool allowForNumericPrecisionDifferences = false)
        {
            FieldsEqual(target, actual, countFields, null, allowForNumericPrecisionDifferences);
        }

        public static void FieldsEqual(string target, string actual, double tolerance, int? expectedFieldCount=null)
        {
            using (StringReader readerTarget = new StringReader(target))
            using (StringReader readerActual = new StringReader(actual))
            {
                FieldsEqual(readerTarget, readerActual, expectedFieldCount, null, false, 0, tolerance);
            }
        }

        public static void FieldsEqual(string target, string actual, int? expectedFieldCount, int? exceptIndex, bool allowForTinyNumericDifferences = false, string message = null)
        {
            using (StringReader readerTarget = new StringReader(target))
            using (StringReader readerActual = new StringReader(actual))
            {
                FieldsEqual(readerTarget, readerActual, expectedFieldCount, exceptIndex, allowForTinyNumericDifferences, 0, null, 0, message);
            }
        }

        public static void FieldsEqual(TextReader readerTarget, TextReader readerActual, int? expectedFieldCount, int? exceptIndex, bool allowForTinyNumericDifferences = false, int allowedExtraLinesInActual = 0, double? tolerance=null, int skipLines = 0, string message = null)
        {
            message = message == null ? string.Empty : message + " ";
            var count = 0;
            while (true)
            {
                string lineTarget = readerTarget.ReadLine();
                string lineActual = readerActual.ReadLine();
                if (lineTarget == null && lineActual == null)
                    return;
                if (count++ < skipLines)
                {
                    continue; // OK to ignore this line
                }
                if (lineTarget == null)
                {
                    while ((lineActual != null) && (allowedExtraLinesInActual > 0))  // As in test mode where we add a special non-proteomic molecule node to every document
                    {
                        lineActual = readerActual.ReadLine();
                        allowedExtraLinesInActual--;
                    }
                    if (lineActual != null)
                        Fail($"{message}Target stops at line {count}.");
                }
                else if (lineActual == null)
                {
                    Fail($"{message}Actual stops at line {count}.");
                }
                else if (lineTarget != lineActual)
                {
                    var culture = CultureInfo.InvariantCulture;
                    char sep;
                    if (lineTarget.Contains("\t"))
                        sep = '\t';
                    else if (lineTarget.Contains(","))
                        sep = ',';
                    else
                        sep = ' ';
                    string[] fieldsTarget = lineTarget.Split(new[] {sep});
                    string[] fieldsActual = lineActual.Split(new[] {sep});
                    var countFields = expectedFieldCount ?? Math.Max(fieldsTarget.Length, fieldsActual.Length);
                    if (fieldsTarget.Length < countFields || fieldsActual.Length < countFields)
                    {
                        Fail($"{message}Diff found at line {count}:\r\n{lineTarget}\r\n>\r\n{lineActual}");
                    }
                    for (int i = 0; i < countFields; i++)
                    {
                        if (exceptIndex.HasValue && exceptIndex.Value == i)
                            continue; // Just ignore this column

                        var targetField = fieldsTarget[i].ToUpper(CultureInfo.InvariantCulture);
                        var actualField = fieldsActual[i].ToUpper(CultureInfo.InvariantCulture);
                        if (!Equals(targetField, actualField))
                        {
                            if (targetField.Contains(@"E") && actualField.Contains(@"E"))
                            {
                                // Same exponent? Then only compare the mantissa
                                var targetFieldParts = targetField.Split('E');
                                var actualFieldParts = actualField.Split('E');
                                if (Equals(targetFieldParts[1], actualFieldParts[1]))
                                {
                                    targetField = targetFieldParts[0];
                                    actualField = actualFieldParts[0];
                                }
                            }
                            // Test numerics with the precision presented in the output text
                            double dTarget, dActual;
                            if (Double.TryParse(targetField, NumberStyles.Float, culture, out dTarget) &&
                                Double.TryParse(actualField, NumberStyles.Float, culture, out dActual))
                            {
                                if (tolerance.HasValue)
                                {
                                    if (Math.Abs(dTarget - dActual) <= tolerance)
                                        continue;
                                }
                                if (allowForTinyNumericDifferences)
                                {
                                    // how much of that was decimal places?
                                    var precTarget = targetField.Length - String.Format("{0}.", (int)dTarget).Length;
                                    var precActual = actualField.Length - String.Format("{0}.", (int)dActual).Length;
                                    if (precTarget == -1 && precActual == -1)
                                    {
                                        // Integers - allow for rounding errors on larger values
                                        var diff = Math.Abs(dTarget - dActual);
                                        var max = Math.Max(Math.Abs(dTarget), Math.Abs(dActual));
                                        if (max != 0 && diff <= 1)
                                        {
                                            var ratio = diff / max;
                                            if (ratio <= .001)
                                            {
                                                continue; // e.g. 5432 vs 5433 but not 1 vs 2
                                            }
                                        }
                                    }
                                    else
                                    {
                                        var prec = Math.Max(Math.Min(precTarget, precActual), 0);
                                        var mult = (precActual == precTarget) ? 1.01 : 0.501; // Allow for double precision calculation cruft e.g 34995.22-34995.21 = 0.010000000002037268
                                        double toler = mult * ((prec == 0) ? 0 : Math.Pow(10, -prec));
                                        // so .001 is seen as close enough to .0009, or 12.3 same as 12.4 (could be serializations of very similar numbers that rounded differently)
                                        if (Math.Abs(dTarget - dActual) <= toler)
                                            continue;
                                    }
                                }
                            }
                            Fail($"{message}Diff found at line {count}:\r\n{lineTarget}\r\n>\r\n{lineActual}");
                        }
                    }
                }
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
                IsTrue(cmd.OpenSkyFile(tmpSky)); // Handles any path shifts in database files, like our .imsdb file
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

                    target = ResultsUtil.ClearFileImportTimes(target);
                    actual = ResultsUtil.ClearFileImportTimes(actual);

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
            Cloned(target.PeptideSettings.ProteinAssociationSettings, target.PeptideSettings.ProteinAssociationSettings);
            Cloned(target.PeptideSettings, copy.PeptideSettings);
            var defTran = defSet.TransitionSettings;
            Cloned(target.TransitionSettings.Prediction, copy.TransitionSettings.Prediction, defTran.Prediction);
            Cloned(target.TransitionSettings.Filter, copy.TransitionSettings.Filter, defTran.Filter);
            Cloned(target.TransitionSettings.Libraries, copy.TransitionSettings.Libraries, defTran.Libraries);
            Cloned(target.TransitionSettings.IonMobilityFiltering, copy.TransitionSettings.IonMobilityFiltering, defTran.IonMobilityFiltering);
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
            converted = Serializable(converted, testDir, SkylineVersion.CURRENT, true,  true); // Force a full load to verify library correctness
            document = Serializable(document, testDir, SkylineVersion.CURRENT, true,  true); // Force a full load to verify library correctness

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
                IsNull(group.IsotopeDist);
            else if (conversionMode != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)
                IsTrue(convertedGroup.IsotopeDist.IsSimilar(group.IsotopeDist));

            if (conversionMode == RefinementSettings.ConvertToSmallMoleculesMode.masses_only && group.Results != null)
            {
                // All we can really expect is that retention times agree - but nothing beyond that, not even the peak width
                AreEqual(group.Results.Count, convertedGroup.Results.Count);
                for (var i = 0; i < group.Results.Count; i++)
                {
                    AreEqual(group.Results[i].Count, convertedGroup.Results[i].Count);
                    for (var j = 0; j < group.Results[i].Count; j++)
                    {
                        AreEqual(group.Results[i][j].RetentionTime, convertedGroup.Results[i][j].RetentionTime, group + " vs " + convertedGroup);
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

        /// <summary>
        /// Verifies that a Comparer has the reflexive, symmetric and transitive properties when
        /// applied to all combinations of the elements provided.
        /// </summary>
        public static void ComparerWellBehaved<T>(IComparer<T> comparer, IEnumerable<T> items)
        {
            var itemList = items.ToList();
            for (int i = 0; i < itemList.Count; i++)
            {
                var itemI = itemList[i];
                for (int j = 0; j < itemList.Count; j++)
                {
                    var itemJ = itemList[j];
                    var compareIJ = Math.Sign(comparer.Compare(itemI, itemJ));
                    var compareJI = Math.Sign(comparer.Compare(itemJ, itemI));
                    Assert.AreEqual(compareIJ, -compareJI, "Compare of {0} with {1} should be opposite of {1} with {0}",
                        itemI, itemJ);
                    if (compareIJ <= 0)
                    {
                        for (int k = 0; k < itemList.Count; k++)
                        {
                            var itemK = itemList[k];
                            var compareJK = Math.Sign(comparer.Compare(itemJ, itemK));
                            if (compareJK <= 0)
                            {
                                Assert.AreNotEqual(1, Math.Sign(comparer.Compare(itemI, itemK)),
                                    "Compare of {0} with {2} should not be positive because {0} < {1} and {1} < {2}",
                                    itemI, itemJ, itemK);
                            }
                        }
                    }
                }
            }
        }
    }
}
