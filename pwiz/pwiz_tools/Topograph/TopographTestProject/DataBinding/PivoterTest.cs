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
using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Test.DataBinding.SampleData;

namespace pwiz.Topograph.Test.DataBinding
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
                                      "Molecule.[]",
                                  };
            var viewSpec = new ViewSpec()
                .SetColumns(columnNames.Select(name => new ColumnSpec().SetName(name)))
                .SetSublistId(IdentifierPath.Parse("Molecule.[]"));
            var viewInfo = new ViewInfo(new DataSchema(), typeof (AminoAcid), viewSpec);
            var pivoter = new Pivoter(viewInfo);
            var alaAcid = new AminoAcid("Ala");
            Assert.AreEqual("C3H5NO", alaAcid.Molecule.ToString());
            var nodes = pivoter.Expand(new RowItem(null, alaAcid)).ToArray();
            Assert.AreEqual(1, nodes.Length);
            var atomicNodes = nodes[0].GetChildren(IdentifierPath.Parse("Molecule.[]"));
            CollectionAssert.AreEquivalent(alaAcid.Molecule.Keys.ToArray(), atomicNodes.Select(atomicNode=>atomicNode.RowItem.Key).ToArray());
            var rowItems = pivoter.Pivot(nodes).ToArray();
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
                                      "Molecule.[]",
                                  };
            var viewSpec = new ViewSpec()
                .SetColumns(columnNames.Select(name => new ColumnSpec().SetName(name)));
            var viewInfo = new ViewInfo(new DataSchema(), typeof(AminoAcid), viewSpec);
            var pivoter = new Pivoter(viewInfo);
            var alaAcid = new AminoAcid("Ala");
            var rowItems = pivoter.ExpandAndPivot(new[] {new RowItem(null, alaAcid)});
            CollectionAssert.AreEqual(new object[]{alaAcid}, rowItems.Select(rowItem=>rowItem.Value).ToArray());
            var elementNames = pivoter.GetPivotKeys(IdentifierPath.Parse("Molecule.[]"), rowItems)
                .Select(groupKey => groupKey.ValuePairs[0].Value).ToArray();
            CollectionAssert.AreEquivalent(alaAcid.Molecule.Keys.ToArray(), elementNames);
        }
        [TestMethod]
        public void TestDoublePivot()
        {
            IdentifierPath molecules = IdentifierPath.Parse("Molecule.[]");
            IdentifierPath massEntries = IdentifierPath.Parse("MassDistribution.[]");
            var viewSpec = new ViewSpec()
                .SetColumns(new[]
                                {
                                    new ColumnSpec(molecules),
                                    new ColumnSpec(massEntries),
                                });
            var viewInfo = new ViewInfo(new DataSchema(), typeof(AminoAcid), viewSpec);
            var pivoter = new Pivoter(viewInfo);
            var alaAcid = new AminoAcid("Ala");
            var rowItems = pivoter.ExpandAndPivot(new[] {new RowItem(null, alaAcid)});
            CollectionAssert.AreEqual(new object[]{alaAcid}, rowItems.Select(rowItem=>rowItem.Value).ToArray());

            CollectionAssert.AreEquivalent(alaAcid.Molecule.Keys.ToArray(), pivoter.GetPivotKeys(molecules, rowItems).Select(rk=>rk.ValuePairs[0].Value).ToArray());

            CollectionAssert.AreEquivalent(Enumerable.Range(0, alaAcid.MassDistribution.Count).ToArray(), 
                pivoter.GetPivotKeys(massEntries, rowItems).Select(rk=>rk.ValuePairs[0].Value).ToArray());
        }
        [TestMethod]
        public void TestPivotWithSublist()
        {
            IdentifierPath molecules = IdentifierPath.Parse("Molecule.[]");
            IdentifierPath massEntries = IdentifierPath.Parse("MassDistribution.[]");
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
            var rowItems = pivoter.ExpandAndPivot(new[] {new RowItem(null, alaAcid)});
            CollectionAssert.AreEquivalent(Enumerable.Range(0, alaAcid.MassDistribution.Count).ToArray(), rowItems.Select(rowItem=>rowItem.Key).ToArray());
            CollectionAssert.AreEquivalent(alaAcid.Molecule.Keys.ToArray(), pivoter.GetPivotKeys(molecules, rowItems).Select(rk => rk.ValuePairs[0].Value).ToArray());
        }
        [TestMethod]
        public void TestFilter()
        {
            var viewSpec = new ViewSpec().SetColumns(new[] {new ColumnSpec(IdentifierPath.Parse("Code"))})
                .SetFilters(new[] {new FilterSpec(IdentifierPath.Parse("CharCode"), FilterOperations.OpEquals, "A"),});
            var viewInfo = new ViewInfo(new DataSchema(), typeof (AminoAcid), viewSpec);
            var pivoter = new Pivoter(viewInfo);
            var alaAcid = new AminoAcid("Ala");
            Assert.AreEqual('A', alaAcid.CharCode);
            var argAcid = new AminoAcid("Arg");
            Assert.AreEqual('R', argAcid.CharCode);
            viewSpec = viewSpec.SetFilters(
                new[] { new FilterSpec(IdentifierPath.Parse("Code"), FilterOperations.OpStartsWith, "Ar") });
            pivoter = new Pivoter(new ViewInfo(new DataSchema(), typeof(AminoAcid), viewSpec));
            var rowItems = pivoter.ExpandAndPivot(new[] { new RowItem(null, alaAcid), new RowItem(null, argAcid) }).ToArray();
            Assert.AreEqual(1, rowItems.Length);
            Assert.AreEqual("Arg", viewInfo.DisplayColumns[0].GetValue(rowItems[0], null));
        }
    }
}
