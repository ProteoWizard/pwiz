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
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;

namespace pwiz.SkylineTestUtil
{
    public class AssertEx
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

        public static void ThrowsException<TEx>(Action throwEx)
            where TEx : Exception
        {
            ThrowsException<TEx>(() => { throwEx(); return null; });
        }

        public static void ThrowsException<TEx>(Func<object> throwEx)
            where TEx : Exception
        {
            try
            {
                throwEx();
                Assert.Fail("Exception expected");
            }
            catch (TEx)
            {
            }            
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
            foreach (string part in parts)
            {
                Assert.IsTrue(value.Contains(part),
                    string.Format("The text '{0}' does not contain '{1}'", value, part));
            }
        }

        private const string XML_DIRECTIVE = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\r\n";

        public static TObj Deserialize<TObj>(string s)
        {
            s = XML_DIRECTIVE + s;

            XmlSerializer ser = new XmlSerializer(typeof(TObj));
            using (TextReader reader = new StringReader(s))
            {
                return (TObj)ser.Deserialize(reader);
            }            
        }

        public static void Serialization<TObj>(string s, Action<TObj, TObj> validate)
            where TObj : class
        {
            Serializable(Deserialize<TObj>(s), validate);
        }

        public static void DeserializeNoError<TObj>(string s)
        {
            DeserializeError<TObj, Exception>(s, false);
        }

        public static void DeserializeError<TObj>(string s)
        {
            DeserializeError<TObj, InvalidDataException>(s);
        }

        public static void DeserializeError<TObj, TEx>(string s)
            where TEx : Exception
        {
            DeserializeError<TObj, TEx>(s, true);
        }

        public static void DeserializeError<TObj, TEx>(string s, bool expectError)
            where TEx : Exception
        {
            s = XML_DIRECTIVE + s;

            XmlSerializer ser = new XmlSerializer(typeof(TObj));
            using (TextReader reader = new StringReader(s))
            {
                try
                {
                    ser.Deserialize(reader);

                    if (expectError)
                    {
                        // Fail if deserialization succeeds.
                        Assert.Fail(String.Format("Expected error deserializing {0}:\r\n{1}", typeof(TObj).Name, s));
                    }
                }
                catch (InvalidOperationException x)
                {
                    if (expectError)
                    {
                        // Make sure the XML parsing exception was thrown
                        // with the expected innerException type.
                        HasInnerExceptionType(x, typeof(TEx));
                    }
                    else
                    {
                        String message = GetMessageStack(x, null);
                        Assert.Fail(String.Format("Unexpected exception {0} - {1}:\r\n{2}", typeof(TEx), message, x.StackTrace));
                    }
                }
                catch (TEx x)
                {
                    if (!expectError)
                    {
                        String message = GetMessageStack(x, null);
                        Assert.Fail(String.Format("Unexpected exception {0} - {1}:\r\n{2}", typeof(TEx), message, x.StackTrace));
                    }
                }
            }
        }

        public static void Serializable(SrmDocument doc)
        {
            Serializable(doc, DocumentCloned);
        }

        public static void Serializable<TObj>(TObj target, Action<TObj, TObj> validate)
            where TObj : class
        {
            Serializable(target, 1, validate);
        }

        public static void Serializable<TObj>(TObj target, int roundTrips, Action<TObj, TObj> validate)
            where TObj : class
        {
            string expected = null;
            for (int i = 0; i < roundTrips; i++)
                validate(target, RoundTrip(target, ref expected));
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
            using (var readerImport = new StringReader(DuplicateAndReverseLines(transitionList, exporter.HasHeaders)))
            {
                IdentityPath pathAdded;
                IFormatProvider provider = CultureInfo.InvariantCulture;
                docImport = docImport.ImportMassList(readerImport, provider, ',', IdentityPath.ROOT, out pathAdded);
            }

            IsDocumentState(docImport, 1,
                                     docExport.PeptideGroupCount,
                                     docExport.PeptideCount,
                                     docExport.TransitionGroupCount,
                                     docExport.TransitionCount);
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

        public static void NoDiff(string target, string actual)
        {
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
                        Assert.Fail(String.Format("Target stops at line {0}.", count));
                    if (lineActual == null)
                        Assert.Fail(String.Format("Actual stops at line {0}.", count));
                    if (lineTarget != lineActual)
                        Assert.Fail(String.Format("Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual));
                    count++;
                }

            }
        }

        public static void FieldsEqual(string target, string actual, int countFields)
        {
            FieldsEqual(target, actual, countFields, null);
        }

        public static void FieldsEqual(string target, string actual, int countFields, int? exceptIndex)
        {
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
                        Assert.Fail(String.Format("Target stops at line {0}.", count));
                    else if (lineActual == null)
                        Assert.Fail(String.Format("Actual stops at line {0}.", count));
                    else if (lineTarget != lineActual)
                    {
                        string[] fieldsTarget = lineTarget.Split(new[] { ',' });
                        string[] fieldsActual = lineActual.Split(new[] { ',' });
                        if (fieldsTarget.Length < countFields || fieldsActual.Length < countFields)
                            Assert.Fail(String.Format("Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual));                        
                        for (int i = 0; i < countFields; i++)
                        {
                            if (exceptIndex.HasValue && exceptIndex.Value == i)
                                continue;

                            if (!Equals(fieldsTarget[i], fieldsActual[i]))
                                Assert.Fail(String.Format("Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineTarget, lineActual));
                        }
                    }
                    count++;
                }
            }
        }

        public static void Cloned(object expected, object actual)
        {
            Cloned(expected, actual, null);
        }

        public static void Cloned(object expected, object actual, object defaultObj)
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
                Assert.Fail(String.Format("Expected exception type {0} not found:\r\n{1}", t.Name, message));
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

        public static void IsDocumentState(SrmDocument document, int? revision, int groups, int peptides, int transitions)
        {
            IsDocumentState(document, revision, groups, peptides, peptides, transitions);
        }

        public static void IsDocumentState(SrmDocument document, int? revision, int groups, int peptides,
                                           int tranGroups, int transitions)
        {
            if (revision != null)
                Assert.AreEqual(revision, document.RevisionIndex);
            Assert.AreEqual(groups, document.PeptideGroupCount);
            Assert.AreEqual(peptides, document.PeptideCount);
            Assert.AreEqual(tranGroups, document.TransitionGroupCount);
            Assert.AreEqual(transitions, document.TransitionCount);

            // Verify that no two nodes in the document tree have the same global index
            var setIndexes = new HashSet<int>();
            var nodeDuplicate = FindFirstDuplicateGlobalIndex(document, setIndexes);
            if (nodeDuplicate != null)
            {
                Assert.Fail(string.Format("Duplicate global index {0} found in node {1}",
                    nodeDuplicate.Id.GlobalIndex, nodeDuplicate));
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
            Cloned(target.PeptideSettings, copy.PeptideSettings);
            Cloned(target.TransitionSettings.Prediction, copy.TransitionSettings.Prediction);
            Cloned(target.TransitionSettings.Filter, copy.TransitionSettings.Filter);
            Cloned(target.TransitionSettings.Libraries, copy.TransitionSettings.Libraries);
            Cloned(target.TransitionSettings.Integration, copy.TransitionSettings.Integration);
            Cloned(target.TransitionSettings.Instrument, copy.TransitionSettings.Instrument);
            Cloned(target.TransitionSettings.FullScan, copy.TransitionSettings.FullScan, SrmSettingsList.GetDefault().TransitionSettings.FullScan);
            Cloned(target.TransitionSettings, copy.TransitionSettings);
            Cloned(target, copy);
        }
    }
}
