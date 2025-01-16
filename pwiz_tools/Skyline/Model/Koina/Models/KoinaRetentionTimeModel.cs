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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Inference;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using static Inference.ModelInferRequest.Types;

namespace pwiz.Skyline.Model.Koina.Models
{
    public sealed class KoinaRetentionTimeModel : KoinaModel<KoinaRetentionTimeModel.KoinaRTInput.KoinaPeptideInput,
        KoinaRetentionTimeModel.KoinaRTInput,
        KoinaRetentionTimeModel.PeptideDocNodeWrapper,
        KoinaRetentionTimeModel.KoinaRTOutput.KoinaPeptideOutput,
        KoinaRetentionTimeModel.KoinaRTOutput,
        Dictionary<PeptideDocNode, double>>
    {
        static KoinaRetentionTimeModel()
        {
            // Not sure if the models count will ever change. Maybe in the future
            // users can somehow add their own models. But this is just capacity anyways.
            _instances = new List<KoinaRetentionTimeModel>(Models.Count());
        }

        // Singleton pattern
        private KoinaRetentionTimeModel(string model)
        {
            if (!Models.Contains(model))
                throw new KoinaNotConfiguredException(string.Format(
                    KoinaResources.KoinaRetentionTimeModel_KoinaRetentionTimeModel_Retention_time_model___0___does_not_exist,
                    model));

            Model = model;
        }

        private static List<KoinaRetentionTimeModel> _instances;

        public static KoinaRetentionTimeModel GetInstance(string model)
        {
            var intensityModel = _instances.FirstOrDefault(p => p.Model == model);
            if (intensityModel == null)
                _instances.Add(intensityModel = new KoinaRetentionTimeModel(model));
            return intensityModel;
        }

        public static KoinaRetentionTimeModel Instance
        {
            get
            {
                var selectedModel = Settings.Default.KoinaRetentionTimeModel;
                return GetInstance(selectedModel);
            }
        }

        // Model constants
        private const string SIGNATURE = "v1";

        public override string Signature => SIGNATURE;
        public override string Model { get; protected set; }

        private bool Equals(KoinaRetentionTimeModel other)
        {
            return string.Equals(Model, other.Model);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is KoinaRetentionTimeModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Model != null ? Model.GetHashCode() : 0);
        }

        public static IEnumerable<string> Models => new[]
        {
            @"Prosit_2019_irt",
            //@"Prosit_2020_irt_TMT",
            //@"Deeplc_hela_hf",
            @"AlphaPept_rt_generic"
        };

        public override IDictionary<string, ModelInputs> InputsForModel => null;

        public override KoinaRTInput.KoinaPeptideInput CreateKoinaInputRow(SrmSettings settings, PeptideDocNodeWrapper skylineInput,
            out KoinaException exception)
        {
            var sequence = KoinaHelpers.EncodeSequence(settings, skylineInput, IsotopeLabelType.light, out exception);
            if (sequence == null)
                return null;

            return new KoinaRTInput.KoinaPeptideInput(sequence);
        }

        public static KoinaRTInput.KoinaPeptideInput CreateKoinaInputRow(SrmSettings settings, string peptide, out KoinaException exception)
        {
            var sequence = KoinaHelpers.EncodeSequence(peptide, out exception);
            if (sequence == null)
                return null;

            return new KoinaRTInput.KoinaPeptideInput(sequence);
        }

        public override KoinaRTInput CreateKoinaInput(IList<KoinaRTInput.KoinaPeptideInput> koinaInputRows)
        {
            return new KoinaRTInput(koinaInputRows);
        }

        public override KoinaRTOutput CreateKoinaOutput(ModelInferResponse koinaOutputData)
        {
            return new KoinaRTOutput(koinaOutputData);
        }

        public override Dictionary<PeptideDocNode, double> CreateSkylineOutput(SrmSettings settings, IList<PeptideDocNodeWrapper> skylineInputs, KoinaRTOutput koinaOutput)
        {
            return Enumerable.Range(0, skylineInputs.Count)
                .ToDictionary(i => (PeptideDocNode) skylineInputs[i], i => koinaOutput.OutputRows[i].iRT);
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
        /// Input structure at the Koina level, it contains the tensor
        /// which is sent to Koina.
        /// </summary>
        public sealed class KoinaRTInput : KoinaInput<KoinaRTInput.KoinaPeptideInput>
        {
            public static readonly string PEPTIDES_KEY = @"peptide_sequences";

            public KoinaRTInput(IList<KoinaPeptideInput> peptideInputs)
            {
                InputRows = peptideInputs;
            }

            public override IList<KoinaPeptideInput> InputRows { get; }

            public override IList<InferInputTensor> KoinaTensors
            {
                get
                {
                    return new List<InferInputTensor>()
                    {
                        Create2dTensor(PEPTIDES_KEY, DataTypes.BYTES, tp => tp.BytesContents,
                            InputRows.Select(p => ByteString.CopyFrom(p.PeptideSequence, Encoding.ASCII)).ToArray(),
                            InputRows.Count, 1)
                    };
                }
            }

            public override IList<string> OutputTensorNames => new List<string> { KoinaRTOutput.OUTPUT_KEY };

            public class KoinaPeptideInput
            {
                public KoinaPeptideInput(string peptideSequence)
                {
                    PeptideSequence = peptideSequence;
                }

                public string PeptideSequence { get; private set; }
                public override string ToString()
                {
                    return PeptideSequence;
                }
            }
        }

        public sealed class KoinaRTOutput : KoinaOutput<KoinaRTOutput, KoinaRTOutput.KoinaPeptideOutput>
        {
            public static readonly string OUTPUT_KEY = @"irt";

            public KoinaRTOutput(ModelInferResponse koinaOutput)
            {
                //var outputTensor = koinaOutput.Single(t => t.Name.Equals(OUTPUT_KEY, StringComparison.InvariantCultureIgnoreCase));
                var peptideCount = koinaOutput.Outputs[0].Shape[0];
                OutputRows = new KoinaPeptideOutput[peptideCount];
                var stream = koinaOutput.RawOutputContents[0].CreateCodedInput();
                // Copy iRTs for each peptide
                for (var i = 0; i < peptideCount; ++i)
                    OutputRows[i] = new KoinaPeptideOutput(stream.ReadFloat());
            }
            
            public KoinaRTOutput()
            {
                OutputRows = new KoinaPeptideOutput[0];
            }

            public override IList<KoinaPeptideOutput> OutputRows { get; protected set; }

            public class KoinaPeptideOutput
            {
                public KoinaPeptideOutput(double iRT)
                {
                    this.iRT = iRT;
                }
                
                public double iRT { get; }
            }
        }
    }
}
