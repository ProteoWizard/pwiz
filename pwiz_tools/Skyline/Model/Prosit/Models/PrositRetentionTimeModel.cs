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

        public static IEnumerable<string> Models => new[]
        {
            @"Prosit_2019_irt",
            //@"Prosit_2020_irt_TMT",
            @"Deeplc_hela_hf",
            @"AlphaPept_rt_generic"
        };

        public override IDictionary<string, ModelInputs> InputsForModel => null;

        public override PrositRTInput.PrositPeptideInput CreatePrositInputRow(SrmSettings settings, PeptideDocNodeWrapper skylineInput,
            out PrositException exception)
        {
            var sequence = PrositHelpers.EncodeSequence(settings, skylineInput, IsotopeLabelType.light, out exception);
            if (sequence == null)
                return null;

            return new PrositRTInput.PrositPeptideInput(sequence);
        }

        public static PrositRTInput.PrositPeptideInput CreatePrositInputRow(SrmSettings settings, string peptide, out PrositException exception)
        {
            var sequence = PrositHelpers.EncodeSequence(peptide, out exception);
            if (sequence == null)
                return null;

            return new PrositRTInput.PrositPeptideInput(sequence);
        }

        public override PrositRTInput CreatePrositInput(IList<PrositRTInput.PrositPeptideInput> prositInputRows)
        {
            return new PrositRTInput(prositInputRows);
        }

        public override PrositRTOutput CreatePrositOutput(ModelInferResponse prositOutputData)
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
            public static readonly string PEPTIDES_KEY = @"peptide_sequences";

            public PrositRTInput(IList<PrositPeptideInput> peptideInputs)
            {
                InputRows = peptideInputs;
            }

            public override IList<PrositPeptideInput> InputRows { get; }

            public override IList<InferInputTensor> PrositTensors
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

            public override IList<string> OutputTensorNames => new List<string> { PrositRTOutput.OUTPUT_KEY };

            public class PrositPeptideInput
            {
                public PrositPeptideInput(string peptideSequence)
                {
                    PeptideSequence = peptideSequence;
                }

                public string PeptideSequence { get; private set; }
            }
        }

        public sealed class PrositRTOutput : PrositOutput<PrositRTOutput, PrositRTOutput.PrositPeptideOutput>
        {
            public static readonly string OUTPUT_KEY = @"irt";

            public PrositRTOutput(ModelInferResponse prositOutput)
            {
                //var outputTensor = prositOutput.Single(t => t.Name.Equals(OUTPUT_KEY, StringComparison.InvariantCultureIgnoreCase));
                var peptideCount = prositOutput.Outputs[0].Shape[0];
                OutputRows = new PrositPeptideOutput[peptideCount];
                var stream = prositOutput.RawOutputContents[0].CreateCodedInput();
                // Copy iRTs for each peptide
                for (var i = 0; i < peptideCount; ++i)
                    OutputRows[i] = new PrositPeptideOutput(stream.ReadFloat());
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
                    this.iRT = iRT;
                }
                
                public double iRT { get; }
            }
        }
    }
}
