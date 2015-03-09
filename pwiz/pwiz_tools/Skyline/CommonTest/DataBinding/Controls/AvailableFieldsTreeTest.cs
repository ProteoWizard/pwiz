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

using CommonTest.DataBinding.SampleData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.SkylineTestUtil;

namespace CommonTest.DataBinding.Controls
{
    /// <summary>
    /// Summary description for AvailableFieldsTest
    /// </summary>
    [TestClass]
    public class AvailableFieldsTreeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDataBindingMapAttribute()
        {
            var tree = new AvailableFieldsTree
            {
                RootColumn = ColumnDescriptor.RootColumn(new DataSchema(), typeof (Peptide)),
                ShowAdvancedFields = true
            };
            var idAminoAcidMoleculeElement = PropertyPath.Parse("AminoAcidsList!*.Molecule!*.Key");
            Assert.IsNull(tree.FindTreeNode(idAminoAcidMoleculeElement, false));
            var aminoAcidMoleculeElementNode = tree.FindTreeNode(idAminoAcidMoleculeElement, true);
            Assert.AreEqual("Element", aminoAcidMoleculeElementNode.Text);
            Assert.AreEqual(PropertyPath.Parse("AminoAcidsList!*.Molecule!*"), tree.GetTreeColumn(aminoAcidMoleculeElementNode.Parent).PropertyPath);
            Assert.AreEqual(PropertyPath.Parse("AminoAcidsList!*.Molecule!*.Value"), tree.GetValueColumn(aminoAcidMoleculeElementNode.Parent).PropertyPath);
            Assert.AreEqual(PropertyPath.Parse("AminoAcidsList!*"), tree.GetValueColumn(aminoAcidMoleculeElementNode.Parent.Parent).PropertyPath);
            var aminoAcidDictKeyNode = tree.FindTreeNode(PropertyPath.Parse("AminoAcidsDict!*.Key"), true);
            var aminoAcidDictCodeNode = tree.FindTreeNode(PropertyPath.Parse("AminoAcidsDict!*.Value.Code"), true);
            Assert.AreEqual("Code", aminoAcidDictCodeNode.Text);
            Assert.AreSame(aminoAcidDictKeyNode.Parent, aminoAcidDictCodeNode.Parent);
        }
    }
}
