/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2017 University of Washington - Seattle, WA
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

using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class ExplicitLossTest : AbstractUnitTest
    {
        [TestMethod]
        public void InstrumentRangeExplicitLossTest()
        {
            var docLoss = ResultsUtil.DeserializeDocument("explicit-losses.sky", GetType());
            AssertEx.IsDocumentState(docLoss, null, 1, 1, 7);
            var docLoss1000 = docLoss.ChangeSettings(docLoss.Settings.ChangeTransitionInstrument(i => i.ChangeMaxMz(1000)));
            AssertEx.IsDocumentState(docLoss1000, null, 1, 1, 7);
            Assert.AreNotSame(docLoss, docLoss1000);
            Assert.AreSame(docLoss.Children, docLoss1000.Children);
            var docLoss800 = docLoss.ChangeSettings(docLoss.Settings.ChangeTransitionInstrument(i => i.ChangeMaxMz(800)));
            AssertEx.IsDocumentState(docLoss800, null, 1, 1, 4);
        }
    }
}
