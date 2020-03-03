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
using Google.Protobuf.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using Tensorflow;

namespace pwiz.Skyline.Model.Prosit.Models
{
    public sealed class PrositRetentionTimeModel : PrositModel<PrositRetentionTimeModel.PrositRTInput.PrositPeptideInput,
        PrositRetentionTimeModel.PrositRTInput,
        PrositRetentionTimeModel.PeptideDocNodeWrapper,
        PrositRetentionTimeModel.PrositRTOutput.PrositPeptideOutput,
        PrositRetentionTimeModel.PrositRTOutput,
        Dictionary<PeptideDocNode, double>>
    {
        static PrositRetentionTimeModel()
        {
            // Not sure if the models count will ever change. Maybe in the future
            // users can somehow add their own models. But this is just capacity anyways.
            _instances = new List<PrositRetentionTimeModel>(Models.Count());
        }

        // Singleton pattern
        private PrositRetentionTimeModel(string model)
        {
            if (!Models.Contains(model))
                throw new PrositNotConfiguredException(string.Format(
                    PrositResources.PrositRetentionTimeModel_PrositRetentionTimeModel_Retention_time_model___0___does_not_exist,
                    model));

            Model = model;
        }

        private static List<PrositRetentionTimeModel> _instances;

        public static PrositRetentionTimeModel GetInstance(string model)
        {
            var intensityModel = _instances.FirstOrDefault(p => p.Model == model);
            if (intensityModel == null)
                _instances.Add(intensityModel = new PrositRetentionTimeModel(model));
            return intensityModel;
        }

        public static PrositRetentionTimeModel Instance
        {
            get
            {
                var selectedModel = Settings.Default.PrositRetentionTimeModel;
                return GetInstance(selectedModel);
            }
        }

        // Model constants
        private const string SIGNATURE = "v1";

        public override string Signature => SIGNATURE;
        public override string Model { get; protected set; }

        private bool Equals(PrositRetentionTimeModel other)
        {
            return string.Equals(Model, other.Model);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is PrositRetentionTimeModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Model != null ? Model.GetHashCode() : 0);
        }

        public static IEnumerable<string> Models
        {
            get { yield return @"iRT"; }
        }

        public override PrositRTInput.PrositPeptideInput CreatePrositInputRow(SrmSettings settings, PeptideDocNodeWrapper skylineInput,
            out PrositException exception)
        {
            var sequence = PrositHelpers.EncodeSequence(settings, (PeptideDocNode) skylineInput, IsotopeLabelType.light, out exception);
            if (sequence == null)
                return null;

            return new PrositRTInput.PrositPeptideInput(sequence);
        }

        public override PrositRTInput CreatePrositInput(IList<PrositRTInput.PrositPeptideInput> prositInputRows)
        {
            return new PrositRTInput(prositInputRows);
        }

        public override PrositRTOutput CreatePrositOutput(MapField<string, TensorProto> prositOutputData)
        {
            return new PrositRTOutput(prositOutputData);
        }

        public override Dictionary<PeptideDocNode, double> CreateSkylineOutput(SrmSettings settings, IList<PeptideDocNodeWrapper> skylineInputs, PrositRTOutput prositOutput)
        {
            return Enumerable.Range(0, skylineInputs.Count)
                .ToDictionary(i => (PeptideDocNode) skylineInputs[i], i => prositOutput.OutputRows[i].iRT);
        }

        /// <summary>
        /// A simple wrapper for PeptideDocNode, since we need to inherit
        /// SkylineInputRow
        /// </summary>
        public class PeptideDocNodeWrapper : SkylineInputRow
        {
            public PeptideDocNodeWrapper(PeptideDocNode node)
            {
                Node = node;
            }

            public PeptideDocNode Node { get; private set; }

            public static implicit operator PeptideDocNode(PeptideDocNodeWrapper pep)
            {
                return pep.Node;
            }

            public static implicit operator PeptideDocNodeWrapper(PeptideDocNode pep)
            {
                return new PeptideDocNodeWrapper(pep);
            }

            public override bool Equals(SkylineInputRow other)
            {
                if (other == null)
                    return false;
                if (!(other is PeptideDocNodeWrapper pepDocNode))
                    return false;
                return ReferenceEquals(Node, pepDocNode.Node);
            }
        }

        /// <summary>
        /// Input structure at the Prosit level, it contains the tensor
        /// which is sent to Prosit.
        /// </summary>
        public sealed class PrositRTInput : PrositInput<PrositRTInput.PrositPeptideInput>
        {
            public static readonly string PEPTIDES_KEY = @"sequence_integer";

            public PrositRTInput(IList<PrositPeptideInput> peptideInputs)
            {
                InputRows = peptideInputs;
            }

            public override IList<PrositPeptideInput> InputRows { get; }

            public override MapField<string, TensorProto> PrositTensors
            {
                get
                {
                    return new MapField<string, TensorProto>
                    {
                        [PEPTIDES_KEY] = Create2dTensor(DataType.DtInt32, tp => tp.IntVal,
                            InputRows.SelectMany(p => p.PeptideSequence).ToArray(),
                            InputRows.Count, PrositConstants.PEPTIDE_SEQ_LEN),
                    };
                }
            }

            public class PrositPeptideInput
            {
                public PrositPeptideInput(int[] peptideSequence)
                {
                    PeptideSequence = peptideSequence;
                }

                public int[] PeptideSequence { get; private set; }
            }
        }

        public sealed class PrositRTOutput : PrositOutput<PrositRTOutput, PrositRTOutput.PrositPeptideOutput>
        {
            public static readonly string OUTPUT_KEY = @"prediction/BiasAdd:0";

            public const double iRT_MEAN = 56.35363441;
            public const double iRT_VARIANCE = 1883.0160689;

            public PrositRTOutput(MapField<string, TensorProto> prositOutput)
            {
                var outputTensor = prositOutput[OUTPUT_KEY];
                var peptideCount = outputTensor.TensorShape.Dim[0].Size;
                OutputRows = new PrositPeptideOutput[peptideCount];

                // Copy iRTs for each peptide
                for (var i = 0; i < peptideCount; ++i)
                    OutputRows[i] = new PrositPeptideOutput(outputTensor.FloatVal[i]);
            }
            
            public PrositRTOutput()
            {
                OutputRows = new PrositPeptideOutput[0];
            }

            public override IList<PrositPeptideOutput> OutputRows { get; protected set; }

            public class PrositPeptideOutput
            {
                public PrositPeptideOutput(double iRT)
                {
                    this.iRT = iRT * Math.Sqrt(iRT_VARIANCE) + iRT_MEAN;
                }
                
                public double iRT { get; }
            }
        }
    }
}
