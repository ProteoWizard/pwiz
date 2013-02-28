/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.Linq;
using CommonTest.DataBinding.SampleData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Internal;

namespace CommonTest
{
    /// <summary>
    /// Summary description for PivoterTest
    /// </summary>
    [TestClass]
    public class PivoterTest
    {
        [TestMethod]
        public void TestSubList()
        {
            var columnNames = new[]
                                  {
                                      "Code",
                                      "Molecule!*",
                                  };
            var viewSpec = new ViewSpec()
                .SetColumns(columnNames.Select(name => new ColumnSpec().SetName(name)))
                .SetSublistId(PropertyPath.Parse("Molecule!*"));
            var viewInfo = new ViewInfo(new DataSchema(), typeof (AminoAcid), viewSpec);
            var pivoter = new Pivoter(viewInfo);
            var alaAcid = new AminoAcid("Ala");
            Assert.AreEqual("C3H5NO", alaAcid.Molecule.ToString());
            var nodes = pivoter.Expand(new RowItem(null, alaAcid)).ToArray();
            Assert.AreEqual(1, nodes.Length);
            var atomicNodes = nodes[0].GetChildren(PropertyPath.Parse("Molecule!*"));
            CollectionAssert.AreEquivalent(alaAcid.Molecule.Keys.ToArray(), atomicNodes.Select(atomicNode=>atomicNode.RowItem.Key).ToArray());
            var rowItems = pivoter.Pivot(new Pivoter.TickCounter(), nodes).ToArray();
            CollectionAssert.AreEqual(Enumerable.Repeat(viewSpec.SublistId,rowItems.Length).ToArray(), rowItems.Select(rowItem=>rowItem.SublistId).ToArray());
            CollectionAssert.AreEquivalent(alaAcid.Molecule.Keys.ToArray(), rowItems.Select(rowItem=>rowItem.Key).ToArray());
            CollectionAssert.AreEqual(Enumerable.Repeat(alaAcid, rowItems.Length).ToArray(), rowItems.Select(rowItem=>rowItem.Parent.Value).ToArray());
        }
        [TestMethod]
        public void TestPivot()
        {
            var columnNames = new[]
                                  {
                                      "Code",
                                      "Molecule!*",
                                  };
            var viewSpec = new ViewSpec()
                .SetColumns(columnNames.Select(name => new ColumnSpec().SetName(name)));
            var viewInfo = new ViewInfo(new DataSchema(), typeof(AminoAcid), viewSpec);
            var pivoter = new Pivoter(viewInfo);
            var alaAcid = new AminoAcid("Ala");
            var rowItems = pivoter.ExpandAndPivot(new Pivoter.TickCounter(), new[] {new RowItem(null, alaAcid)}).ToArray();
            CollectionAssert.AreEqual(new object[]{alaAcid}, Enumerable.ToArray<object>(rowItems.Select(rowItem=>rowItem.Value)));
            var elementNames = pivoter.GetPivotKeys(PropertyPath.Parse("Molecule!*"), rowItems)
                .Select(groupKey => groupKey.ValuePairs[0].Value)
                .ToArray();
            CollectionAssert.AreEquivalent(alaAcid.Molecule.Keys.ToArray(), elementNames);
        }
        [TestMethod]
        public void TestDoublePivot()
        {
            PropertyPath molecules = PropertyPath.Parse("Molecule!*");
            PropertyPath massEntries = PropertyPath.Parse("MassDistribution!*");
            var viewSpec = new ViewSpec()
                .SetColumns(new[]
                                {
                                    new ColumnSpec(molecules),
                                    new ColumnSpec(massEntries),
                                });
            var viewInfo = new ViewInfo(new DataSchema(), typeof(AminoAcid), viewSpec);
            var pivoter = new Pivoter(viewInfo);
            var alaAcid = new AminoAcid("Ala");
            var rowItems = pivoter.ExpandAndPivot(new Pivoter.TickCounter(), new[] {new RowItem(null, alaAcid)}).ToArray();
            CollectionAssert.AreEqual(new object[]{alaAcid}, Enumerable.ToArray<object>(rowItems.Select(rowItem=>rowItem.Value)));

            CollectionAssert.AreEquivalent(Enumerable.ToArray<string>(alaAcid.Molecule.Keys), Enumerable.ToArray<object>(pivoter.GetPivotKeys(molecules, rowItems).Select(rk=>rk.ValuePairs[0].Value)));

            CollectionAssert.AreEquivalent(Enumerable.Range(0, alaAcid.MassDistribution.Count).ToArray(), 
                Enumerable.ToArray<object>(pivoter.GetPivotKeys(massEntries, rowItems).Select(rk=>rk.ValuePairs[0].Value)));
        }
        [TestMethod]
        public void TestPivotWithSublist()
        {
            PropertyPath molecules = PropertyPath.Parse("Molecule!*");
            PropertyPath massEntries = PropertyPath.Parse("MassDistribution!*");
            var viewSpec = new ViewSpec()
                .SetColumns(new[]
                                {
                                    new ColumnSpec(molecules),
                                    new ColumnSpec(massEntries),
                                })
                .SetSublistId(massEntries);
            var viewInfo = new ViewInfo(new DataSchema(), typeof(AminoAcid), viewSpec);
            var pivoter = new Pivoter(viewInfo);
            var alaAcid = new AminoAcid("Ala");
            var rowItems = pivoter.ExpandAndPivot(new Pivoter.TickCounter(), new[] {new RowItem(null, alaAcid)}).ToArray();
            CollectionAssert.AreEquivalent(Enumerable.Range(0, alaAcid.MassDistribution.Count).ToArray(), Enumerable.ToArray<object>(rowItems.Select(rowItem=>rowItem.Key)));
            CollectionAssert.AreEquivalent(Enumerable.ToArray<string>(alaAcid.Molecule.Keys), Enumerable.ToArray<object>(pivoter.GetPivotKeys(molecules, rowItems).Select(rk => rk.ValuePairs[0].Value)));
        }
        [TestMethod]
        public void TestFilter()
        {
            var viewSpec = new ViewSpec().SetColumns(new[] {new ColumnSpec(PropertyPath.Parse("Code"))})
                .SetFilters(new[] {new FilterSpec(PropertyPath.Parse("CharCode"), FilterOperations.OP_EQUALS, "A"),});
            var viewInfo = new ViewInfo(new DataSchema(), typeof (AminoAcid), viewSpec);
            var alaAcid = new AminoAcid("Ala");
            Assert.AreEqual<char>('A', alaAcid.CharCode);
            var argAcid = new AminoAcid("Arg");
            Assert.AreEqual<char>('R', argAcid.CharCode);
            viewSpec = viewSpec.SetFilters(
                new[] { new FilterSpec(PropertyPath.Parse("Code"), FilterOperations.OP_STARTS_WITH, "Ar") });
            var pivoter = new Pivoter(new ViewInfo(new DataSchema(), typeof(AminoAcid), viewSpec));
            var rowItems = pivoter.ExpandAndPivot(
                new Pivoter.TickCounter(), 
                new[] { new RowItem(null, alaAcid), new RowItem(null, argAcid) }).ToArray();
            Assert.AreEqual<int>(1, rowItems.Length);
            Assert.AreEqual((object) "Arg", viewInfo.DisplayColumns[0].GetValue(rowItems[0], null, false));
        }
    }
}
