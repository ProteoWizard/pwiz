/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2019 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Grpc.Core;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Prosit;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Model.Prosit.Models;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;
using Tensorflow;
using Tensorflow.Serving;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class PrositSkylineIntegrationTest : AbstractFunctionalTestEx
    {
        private bool RecordData { get { return false; } }

        private static Queue<PrositQuery> QUERIES = new Queue<PrositQuery>()
        {
        };

        [TestMethod]
        public void TestPrositSkylineIntegration()
        {
            TestFilesZip = "TestFunctional/AreaCVHistogramTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            OpenDocument(TestFilesDir.GetTestPath(@"Rat_plasma.sky"));
            PrositPredictionClient.Current = new FakePrositPredictionClient(QUERIES);
            PauseTest();
            TestPrositOptions();
            PrositPredictionClient.Current = null;
        }

        public void TestPrositOptions()
        {
            // For now just set all Prosit settings
            RunDlg<ToolOptionsUI>(() => SkylineWindow.ShowToolOptionsUI(ToolOptionsUI.TABS.Prosit),
                dlg =>
                {
                    dlg.PrositServerCombo = null;
                    dlg.PrositIntensityModelCombo = "intensity_2";
                    dlg.PrositRetentionTimeModelCombo = "irt";
                    dlg.DialogResult = DialogResult.OK;
                });
        }
    }

    public abstract class PrositQuery
    {
        public abstract string Model { get; }
        public abstract PredictResponse Response { get; }

        public abstract void AssertMatchesQuery(PredictRequest pr);
    }

    public class PrositRetentionTimeQuery : PrositQuery
    {
        private string[] _modifiedSequences;
        private float[] _iRTs;

        public PrositRetentionTimeQuery(string[] modifiedSequences, float[] iRTs)
        {
            _modifiedSequences = modifiedSequences;
            _iRTs = iRTs;
        }

        public override void AssertMatchesQuery(PredictRequest pr)
        {
            Assert.AreEqual(Model, pr.ModelSpec.Name);
            Assert.AreEqual(pr.Inputs.Count, 1);
            Assert.AreEqual(pr.Inputs.Keys.First(), PrositRetentionTimeModel.PrositRTInput.PEPTIDES_KEY);
            var tensor = pr.Inputs[PrositRetentionTimeModel.PrositRTInput.PEPTIDES_KEY];
            Assert.AreEqual(tensor.TensorShape.Dim.Count, 2);
            Assert.AreEqual(tensor.TensorShape.Dim[0].Size, _modifiedSequences.Length);
            Assert.AreEqual(tensor.TensorShape.Dim[1].Size, Constants.PEPTIDE_SEQ_LEN);
            AssertEx.AreEqualDeep(_modifiedSequences, DecodeInputTensors(pr.Inputs[PrositRetentionTimeModel.PrositRTInput.PEPTIDES_KEY]));
        }

        public string[] DecodeInputTensors(TensorProto prInput)
        {
            var n = prInput.TensorShape.Dim[0].Size;
            // var pepLen = prInput.TensorShape.Dim[1].Size; // 30

            var result = new string[n];
            var encodedSeqs = prInput.IntVal.ToArray();
            for (int i = 0; i < n; ++i)
            {
                var encodedSeq = new int[Constants.PEPTIDE_SEQ_LEN];
                Array.Copy(encodedSeqs, i * Constants.PEPTIDE_SEQ_LEN, encodedSeq, 0, Constants.PEPTIDE_SEQ_LEN);
                var seq = PrositHelpers.DecodeSequence(encodedSeq, out var ex);
                if (ex != null)
                    throw ex;
                result[i] = seq;
            }

            return result;
        }

        public override string Model => "iRT";

        public override PredictResponse Response
        {
            get
            {
                var pr = new PredictResponse();
                pr.ModelSpec = new ModelSpec { Name = Model };

                // Construct Tensor
                var tp = new TensorProto { Dtype = DataType.DtFloat };

                // Populate with data
                tp.FloatVal.AddRange(_iRTs);
                tp.TensorShape = new TensorShapeProto();
                tp.TensorShape.Dim.Add(new TensorShapeProto.Types.Dim { Size = _iRTs.Length });
                pr.Outputs[PrositRetentionTimeModel.PrositRTOutput.OUTPUT_KEY] = tp;

                return pr;
            }
        }
    }

    public class FakePrositPredictionClient : PredictionService.PredictionServiceClient
    {
        private Queue<PrositQuery> _expectedQueries;

        public FakePrositPredictionClient(Queue<PrositQuery> expectedQueries)
        {
            _expectedQueries = expectedQueries;
        }

        public override PredictResponse Predict(PredictRequest request, CallOptions options)
        {
            if (_expectedQueries.Count == 0)
                Assert.Fail("Unexpected call to Predict. Model: {0}", request.ModelSpec.Name);

            var nextQuery = _expectedQueries.Dequeue();
            nextQuery.AssertMatchesQuery(request);
            return nextQuery.Response;
        }
    }
}