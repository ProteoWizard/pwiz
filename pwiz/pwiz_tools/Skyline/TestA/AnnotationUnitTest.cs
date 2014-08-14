/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for AnnotationUnitTest
    /// </summary>
    [TestClass]
    public class AnnotationUnitTest : AbstractUnitTest
    {
        [TestMethod]
        public void AnnotationMergeTest()
        {
            const string note1 = "This is a test";
            const string note2 = "Different note";
            // Self-merge
            var annotations1 = new Annotations(note1, null, -1);
            Assert.AreSame(annotations1, annotations1.Merge(annotations1));

            // Merge with equal annotation
            var annotations2 = new Annotations(note1, null, -1);
            Assert.AreSame(annotations1, annotations1.Merge(annotations2));

            // Merge with empty note annotation
            var emptyNote = new Annotations(null, null, -1);
            Assert.AreSame(annotations1, annotations1.Merge(emptyNote));
            Assert.AreSame(annotations1, emptyNote.Merge(annotations1));

            // Merge with different not annotation
            var annotations3 = new Annotations(note2, null, -1);
            var mergeNote = annotations1.Merge(annotations3);
            Assert.IsTrue(mergeNote.Note.Contains(note1));
            Assert.IsTrue(mergeNote.Note.Contains(note2));

            // Merge annotation with a note that is already present
            Assert.AreSame(mergeNote, mergeNote.Merge(annotations2));

            // Check color merging
            var annotationsColor = new Annotations("color", null, 0);
            Assert.AreEqual(0, annotations1.Merge(annotationsColor).ColorIndex);
            Assert.AreEqual(0, annotationsColor.Merge(annotations1).ColorIndex);

            // Merge with actual annotations
            const string annotationName1 = "Test";
            const string annotationName2 = "FullText";
            const string annotationValue2 = "Hello";
            var annotationsComplex1 = new Annotations(note1, new[] {new KeyValuePair<string, string>(annotationName1, "Test")}, -1);
            Assert.AreSame(annotationsComplex1, annotations1.Merge(annotationsComplex1));
            Assert.AreSame(annotationsComplex1, annotationsComplex1.Merge(annotations1));

            // Merge with disjoint annotations
            var annotationsComplex2 = new Annotations(note1, new[] {new KeyValuePair<string, string>(annotationName2, annotationValue2)}, -1);
            var complexMerge = annotationsComplex1.Merge(annotationsComplex2);
            Assert.AreEqual(2, complexMerge.ListAnnotations().Length);
            Assert.AreEqual(0, complexMerge.ListAnnotations().IndexOf(a => Equals(a.Key, annotationName1)));
            Assert.AreEqual(1, complexMerge.ListAnnotations().IndexOf(a => Equals(a.Key, annotationName2)));

            // Merge with conflicting annotations
            var annotationsComplex3 = new Annotations(null,
                                                      new[]
                                                          {
                                                              new KeyValuePair<string, string>("NewName", "Value"),
                                                              new KeyValuePair<string, string>(annotationName2, "Error")
                                                          },
                                                      -1);
            AssertEx.ThrowsException<InvalidDataException>(() => complexMerge.Merge(annotationsComplex3));

            // Merge with same annotation
            var annotationsComplex4 = new Annotations(null,
                                                      new[]
                                                          {
                                                              new KeyValuePair<string, string>("NewName", "Value"),
                                                              new KeyValuePair<string, string>(annotationName2, annotationValue2)
                                                          },
                                                      -1);
            Assert.AreEqual(3, complexMerge.Merge(annotationsComplex4).ListAnnotations().Length);
        }
    }
}