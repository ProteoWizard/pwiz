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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections.Transpositions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ColumnDataTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestColumnDataForValues()
        {
            VerifyColumn(ColumnData.ForValues(Array.Empty<int>()));
            VerifyColumn(ColumnData.ForValues(new[]{1,2}));
            VerifyColumn(ColumnData.ForValues(new[]{1,1}));
            VerifyColumn(ColumnData.ForValues(new []{new object(), null}));
        }

        public static void VerifyColumn<T>(ColumnData<T> columnData)
        {
            if (columnData is ColumnData<T>.List list)
            {
                Assert.AreNotEqual(0, list.RowCount);
                Assert.AreNotEqual(1, list.RowCount);
                var uniqueValues = list.ToImmutableList().Distinct().ToList();
                Assert.AreNotEqual(0, uniqueValues.Count);
                Assert.AreNotEqual(1, uniqueValues.Count);
            }
        }
    }
}
