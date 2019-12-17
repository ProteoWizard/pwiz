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
    /// <summary>
    /// Represents Prosit's Intensity model.
    /// </summary>
    public sealed class PrositIntensityModel : PrositModel<PrositIntensityModel.PrositIntensityInput.PrositPrecursorInput,
        PrositIntensityModel.PrositIntensityInput,
        PrositIntensityModel.PeptidePrecursorNCE,
        PrositIntensityModel.PrositIntensityOutput.PrositPrecursorOutput,
        PrositIntensityModel.PrositIntensityOutput,
        PrositMS2Spectra>
    {
        static PrositIntensityModel()
        {
            // Not sure if the models count will ever change. Maybe in the future
            // users can somehow add their own models. But this is just capacity anyways.
            _instances = new List<PrositIntensityModel>(Models.Count());
        }

        // Singleton pattern
        private PrositIntensityModel(string model)
        {
            if (!Models.Contains(model))
                throw new PrositNotConfiguredException(string.Format(
                    PrositResources.PrositIntensityModel_PrositIntensityModel_Intensity_model__0__does_not_exist,
                    model));

            Model = model;
        }
        
        private static List<PrositIntensityModel> _instances;

        public static PrositIntensityModel GetInstance(string model)
        {
            var intensityModel = _instances.FirstOrDefault(p => p.Model == model);
            if (intensityModel == null)
                _instances.Add(intensityModel = new PrositIntensityModel(model));
                
            return intensityModel;
        }

        public static PrositIntensityModel Instance
        {
            get
            {
                var selectedModel = Settings.Default.PrositIntensityModel;
                return GetInstance(selectedModel);
            }
        }

        public const string SIGNATURE = "v1";

        public override string Signature => SIGNATURE;
        public override string Model { get; protected set; }

        private bool Equals(PrositIntensityModel other)
        {
            return string.Equals(Model, other.Model);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is PrositIntensityModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Model != null ? Model.GetHashCode() : 0);
        }

        public static IEnumerable<string> Models
        {
            get
            {
                yield return @"intensity_prosit_publication";
            }
        }

        public override PrositIntensityInput.PrositPrecursorInput CreatePrositInputRow(SrmSettings settings, PeptidePrecursorNCE skylineInput, out PrositException exception)
        {
            var peptideSequence = PrositHelpers.EncodeSequence(settings, skylineInput.NodePep, IsotopeLabelType.light, out exception);
            if (peptideSequence == null) // equivalently, exception != null
                return null;

            var precursorCharge = PrositHelpers.OneHotEncode(skylineInput.NodeGroup.PrecursorCharge - 1, PrositConstants.PRECURSOR_CHARGES);

            return new PrositIntensityInput.PrositPrecursorInput(peptideSequence, precursorCharge, skylineInput.NCE.Value / 100.0f);
        }

        public override PrositIntensityInput CreatePrositInput(IList<PrositIntensityInput.PrositPrecursorInput> prositInputRows)
        {
            return new PrositIntensityInput(prositInputRows);
        }

        public override PrositIntensityOutput CreatePrositOutput(MapField<string, TensorProto> prositOutputData)
        {
            return new PrositIntensityOutput(prositOutputData);
        }

        public override PrositMS2Spectra CreateSkylineOutput(SrmSettings settings, IList<PeptidePrecursorNCE> skylineInputs, PrositIntensityOutput prositOutput)
        {
            return new PrositMS2Spectra(settings, skylineInputs, prositOutput);
        }

        public class PeptidePrecursorNCE : SkylineInputRow
        {
            public PeptidePrecursorNCE(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup) : this(nodePep, nodeGroup, nodeGroup.LabelType, null)
            {
            }
            public PeptidePrecursorNCE(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, IsotopeLabelType labelType, int? nce)
            {
                NodePep = nodePep;
                NodeGroup = nodeGroup;
                LabelType = labelType;
                NCE = nce;
            }

            public PeptideDocNode NodePep { get; private set; }
            public TransitionGroupDocNode NodeGroup { get; private set; }
            public IsotopeLabelType LabelType { get; private set; }
            public int? NCE { get; private set; }

            public PeptidePrecursorNCE WithNCE(int nce)
            {
                return new PeptidePrecursorNCE(NodePep, NodeGroup, LabelType, nce);
            }

            public override bool Equals(SkylineInputRow other)
            {
                if (other == null)
                    return false;
                if (!(other is PeptidePrecursorNCE peptidePrecursorPair))
                    return false;
                return ReferenceEquals(NodePep, peptidePrecursorPair.NodePep)
                       && ReferenceEquals(NodeGroup, peptidePrecursorPair.NodeGroup)
                       && NCE == peptidePrecursorPair.NCE
                       && Equals(LabelType, peptidePrecursorPair.LabelType);
            }
        }

        /// <summary>
        /// Input structure at the Prosit level, it contains the tensors
        /// which are sent to Prosit.
        /// </summary>
        public sealed class PrositIntensityInput : PrositInput<PrositIntensityInput.PrositPrecursorInput>
        {
            public static readonly string PEPTIDES_KEY = @"peptides_in:0";
            public static readonly string PRECURSOR_CHARGE_KEY = @"precursor_charge_in:0";
            public static readonly string COLLISION_ENERGY_KEY = @"collision_energy_in:0";

            public PrositIntensityInput(IList<PrositPrecursorInput> precursorInputs)
            {
                InputRows = precursorInputs;
            }

            public override IList<PrositPrecursorInput> InputRows { get; }

            public override MapField<string, TensorProto> PrositTensors
            {
                get
                {
                    return new MapField<string, TensorProto>
                    {
                        [PEPTIDES_KEY] = Create2dTensor(DataType.DtInt32, tp => tp.IntVal,
                            InputRows.SelectMany(p => p.PeptideSequence).ToArray(),
                            InputRows.Count, PrositConstants.PEPTIDE_SEQ_LEN),
                        [PRECURSOR_CHARGE_KEY] = Create2dTensor(DataType.DtFloat, tp => tp.FloatVal,
                            InputRows.SelectMany(p => p.PrecursorCharge).ToArray(),
                            InputRows.Count, PrositConstants.PRECURSOR_CHARGES),
                        [COLLISION_ENERGY_KEY] = Create2dTensor(DataType.DtFloat, tp => tp.FloatVal,
                            InputRows.Select(p => p.NormalizedCollisionEnergy).ToArray(),
                            InputRows.Count, 1)
                    };
                }

            }

            /// <summary>
            /// Represents a single Precursor that can be used to construct
            /// input tensors for Prosits intensity model.
            /// </summary>
            public class PrositPrecursorInput
            {
                public PrositPrecursorInput(int[] peptideSequence, float[] precursorCharge, float normalizedCollisionEnergy)
                {
                    PeptideSequence = peptideSequence;
                    PrecursorCharge = precursorCharge;
                    NormalizedCollisionEnergy = normalizedCollisionEnergy;
                }


                public int[] PeptideSequence { get; }
                public float[] PrecursorCharge { get; }

                public float NormalizedCollisionEnergy { get; }
            }
        }

        /// <summary>
        /// Represents the output returned from Prosits
        /// intensity model. Is constructed directly from the incoming tensors.
        /// </summary>
        public sealed class PrositIntensityOutput : PrositOutput<PrositIntensityOutput, PrositIntensityOutput.PrositPrecursorOutput>
        {
            public static readonly string OUTPUT_KEY = @"out/Reshape:0";

            public PrositIntensityOutput(MapField<string, TensorProto> prositOutput)
            {
                var outputTensor = prositOutput[OUTPUT_KEY];

                // Note that this is essentially a lightweight iterator. We pass
                // down an index by reference that keeps getting increased.
                var precursorCount = outputTensor.TensorShape.Dim[0].Size;
                OutputRows = new PrositPrecursorOutput[precursorCount];
                var index = 0;
                // Copy intensities for each precursor
                for (var i = 0; i < precursorCount; ++i)
                    OutputRows[i] = new PrositPrecursorOutput(outputTensor, ref index);
            }

            public PrositIntensityOutput()
            {
                OutputRows = new PrositPrecursorOutput[0];
            }

            public override IList<PrositPrecursorOutput> OutputRows { get; protected set; }


            /// <summary>
            /// Represents the intensity predictions for a single peptide.
            /// </summary>
            public class PrositPrecursorOutput
            {
                public PrositPrecursorOutput(TensorProto tensor, ref int index)
                {
                    Intensities = new FragmentIonIntensity[PrositConstants.PEPTIDE_SEQ_LEN - 1];

                    // Copy blocks of intensities for each ion
                    for (var i = 0; i < PrositConstants.PEPTIDE_SEQ_LEN - 1; ++i)
                        Intensities[i] = new FragmentIonIntensity(tensor, ref index);
                }

                public FragmentIonIntensity[] Intensities { get; private set; }
            }

            /// <summary>
            /// Represents the intensity predictions for a single
            /// fragment ion of a single precursor (for different charges).
            /// </summary>
            public class FragmentIonIntensity
            {
                public FragmentIonIntensity(TensorProto tensor, ref int index)
                {
                    if (index + PrositConstants.PRECURSOR_CHARGES > tensor.FloatVal.Count)
                        throw new ArgumentException();

                    Intensities = new float[PrositConstants.PRECURSOR_CHARGES];
                    // Copy intensities
                    for (var i = 0; i < PrositConstants.PRECURSOR_CHARGES; ++i)
                        Intensities[i] = PrositHelpers.ReLU(tensor.FloatVal[index++]);
                }

                public float[] Intensities { get; }

                public float Y1 => Intensities[0];
                public float Y2 => Intensities[1];
                public float Y3 => Intensities[2];

                public float B1 => Intensities[3];
                public float B2 => Intensities[4];
                public float B3 => Intensities[5];
            }
        }
    }
}
