/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class AnnotatedDoubleTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestAnnotatedDoubleAggregateOperation()
        {
            var converter = TypeDescriptor.GetConverter(typeof(AnnotatedDouble));
            Assert.IsTrue(converter.CanConvertTo(typeof(double)));
            var dataSchema = new DataSchema();
            Assert.IsTrue(AggregateOperation.Cv.IsValidForType(dataSchema, typeof(AnnotatedDouble)));
            Assert.IsTrue(AggregateOperation.Sum.IsValidForType(dataSchema, typeof(AnnotatedDouble)));
        }

        [TestMethod]
        public void TestAnnotatedDoubleConvertTo()
        {
            var annotatedDouble = new AnnotatedDouble(2.2, "This is a message");
            Assert.AreEqual(2.2, Convert.ToDouble(annotatedDouble));
            Assert.AreEqual(2, Convert.ToInt32(annotatedDouble));
        }
    }
}
