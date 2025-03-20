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
using pwiz.Common.DataBinding.Controls;
using pwiz.SkylineTestUtil;

namespace CommonTest.DataBinding
{
    [TestClass]
    public class FilterTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestDataBindingIsNotNullFilter()
        {
            var dataSchema = new DataSchema();
            var viewSpec = new ViewSpec().SetColumns(new[] {new ColumnSpec(PropertyPath.Parse("AminoAcidsDict!*.Value")),})
                .SetSublistId(PropertyPath.Parse("AminoAcidsDict!*"));
            var viewSpecWithFilter = viewSpec.SetFilters(new[]
            {
                new FilterSpec(PropertyPath.Parse("AminoAcidsDict!*.Value"), FilterPredicate.IS_NOT_BLANK),
            });
            var bindingListSource = new BindingListSource();
            var bindingListSourceWithFilter = new BindingListSource();
            bindingListSource.SetView(new ViewInfo(dataSchema, typeof(Peptide), viewSpec), StaticRowSource.EMPTY);
            bindingListSourceWithFilter.SetView(new ViewInfo(dataSchema, typeof(Peptide), viewSpecWithFilter), 
                new StaticRowSource(new[]{new Peptide("")}));
            Assert.AreEqual(0, bindingListSourceWithFilter.Count);
            bindingListSource.RowSource = bindingListSourceWithFilter.RowSource;
            Assert.AreEqual(1, bindingListSource.Count);
        }

        [TestMethod]
        public void TestDataBindingIsSulfur()
        {
            var dataSchema = new DataSchema();
            var viewSpec =
                new ViewSpec().SetColumns(new[]
                {new ColumnSpec(PropertyPath.Parse("Code")), new ColumnSpec(PropertyPath.Parse("Molecule!*.Key")),})
                    .SetFilters(new[]
                    {new FilterSpec(PropertyPath.Parse("Molecule!*.Key"), FilterPredicate.CreateFilterPredicate(dataSchema, typeof(string), FilterOperations.OP_EQUALS, "S"))});
            var bindingListSource = new BindingListSource();
            bindingListSource.SetView(new ViewInfo(dataSchema, typeof(AminoAcid), viewSpec), new StaticRowSource(AminoAcid.AMINO_ACIDS));
            Assert.AreEqual(2, bindingListSource.Count);


        }
    }
}
