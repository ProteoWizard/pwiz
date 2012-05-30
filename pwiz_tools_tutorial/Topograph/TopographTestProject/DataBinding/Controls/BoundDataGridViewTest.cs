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
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Topograph.Test.DataBinding.SampleData;

namespace pwiz.Topograph.Test.DataBinding.Controls
{
    /// <summary>
    /// Summary description for BoundDataGridViewTest
    /// </summary>
    [TestClass]
    public class BoundDataGridViewTest
    {
        [TestMethod]
        public void TestSubList()
        {
            var boundDataGridView = new BoundDataGridView()
                                        {
                                            BindingContext = new BindingContext(),
                                            DataSource = new BindingSource(),
                                        };
            using (boundDataGridView)
            {
                var columnIds = new[]
                                    {
                                        IdentifierPath.Root,
                                        IdentifierPath.Parse("Sequence"),
                                        IdentifierPath.Parse("AminoAcidsList.[].Code"),
                                        IdentifierPath.Parse("Molecule.[]"),
                                    };
                var viewSpec = new ViewSpec()
                    .SetColumns(columnIds.Select(id => new ColumnSpec().SetIdentifierPath(id)))
                    .SetSublistId(IdentifierPath.Parse("AminoAcidsList.[]"));
                var viewInfo = new ViewInfo(new DataSchema(), typeof (LinkValue<Peptide>), viewSpec);
                boundDataGridView.BindingListView.ViewInfo = viewInfo;
                var innerList = new BindingList<LinkValue<Peptide>>();
                innerList.Add(new LinkValue<Peptide>(new Peptide("AD"), null));
                boundDataGridView.BindingListView.RowSource = innerList;
                Assert.AreEqual(2, boundDataGridView.Rows.Count);
                innerList.Add(new LinkValue<Peptide>(new Peptide("TISE"), null));
                Assert.AreEqual(6, boundDataGridView.Rows.Count);
            }
        }
    }
}
