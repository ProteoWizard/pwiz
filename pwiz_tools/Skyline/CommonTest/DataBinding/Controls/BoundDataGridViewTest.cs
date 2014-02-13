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
using CommonTest.DataBinding.SampleData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.SkylineTestUtil;

namespace CommonTest.DataBinding.Controls
{
    /// <summary>
    /// Summary description for BoundDataGridViewTest
    /// </summary>
    [TestClass]
    public class BoundDataGridViewTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDataBindingSubList()
        {
            var boundDataGridView = new BoundDataGridView
                                        {
                                            BindingContext = new BindingContext(),
                                            DataSource = new BindingListSource(),
                                        };
            using (boundDataGridView)
            {
                var columnIds = new[]
                                    {
                                        PropertyPath.Root,
                                        PropertyPath.Parse("Sequence"),
                                        PropertyPath.Parse("AminoAcidsList!*.Code"),
                                        PropertyPath.Parse("Molecule!*"),
                                    };
                var viewSpec = new ViewSpec()
                    .SetColumns(columnIds.Select(id => new ColumnSpec().SetPropertyPath(id)))
                    .SetSublistId(PropertyPath.Parse("AminoAcidsList!*"));
                var viewInfo = new ViewInfo(new DataSchema(), typeof (LinkValue<Peptide>), viewSpec);
                // ReSharper disable once UseObjectOrCollectionInitializer
                var innerList = new BindingList<LinkValue<Peptide>>();
                innerList.Add(new LinkValue<Peptide>(new Peptide("AD"), null));
                ((BindingListSource)boundDataGridView.DataSource).SetViewContext(new TestViewContext(viewInfo.DataSchema, new[]{new RowSourceInfo(innerList, viewInfo)}));
                Assert.AreEqual(2, boundDataGridView.Rows.Count);
                innerList.Add(new LinkValue<Peptide>(new Peptide("TISE"), null));
                Assert.AreEqual(6, boundDataGridView.Rows.Count);
            }
        }
    }
}
