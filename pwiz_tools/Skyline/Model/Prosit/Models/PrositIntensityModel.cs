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
        PeptidePrecursorPair,
        PrositIntensityModel.PrositIntensityOutput.PrositPrecursorOutput,
        PrositIntensityModel.PrositIntensityOutput,
        PrositMSMSSpectra>
    {
        static PrositIntensityModel()
        {
            // Not sure if the models count will ever change. Maybe in the future
            // users can somehow add their own models. But this is just capacity anyways.
            _instances = new List<PrositIntensityModel>(Models.Count());
        }

        // Singleton
        private PrositIntensityModel(string model)
        {
            if (!Models.Contains(model))
                throw new PrositException(string.Format(
                    PrositResources.PrositIntensityModel_PrositIntensityModel_Intensity_model__0__does_not_exist,
                    model));

            Model = model;
        }

        private static List<PrositIntensityModel> _instances;

        public static PrositIntensityModel GetInstance(string model)
        {
            var intensityModel = _instances.FirstOrDefault(p => p.Model == model);
            if (intensityModel == null)
            {
                // Don't let the constructor throw that exception for now
                if (!Models.Contains(model))
                    return null;
                _instances.Add(intensityModel = new PrositIntensityModel(model));
            }
                
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

        // Model constants
        public const string SIGNATURE = "v1";

        public override string Signature => SIGNATURE;
        public override string Model { get; protected set; }

        public static IEnumerable<string> Models
        {
            get
            {
                yield return @"intensity";
                yield return @"intensity_2";
            }
        }

        public override PrositIntensityInput.PrositPrecursorInput CreatePrositInputRow(SrmSettings setting, PeptidePrecursorPair skylineInput, out PrositException exception)
        {
            var peptideSequence = PrositHelpers.ParseSequence(setting, skylineInput.NodePep, skylineInput.NodeGroup.LabelType, out exception);
            if (peptideSequence == null) // equivalently, exception != null
                return null;

            var precursorCharge = PrositHelpers.OneHotEncode(skylineInput.NodeGroup.PrecursorCharge - 1, Constants.PRECURSOR_CHARGES);

            float collisionEnergy;
            if (skylineInput.CE.HasValue)
            {
                collisionEnergy = (float) (skylineInput.CE.Value / 100.0);
            }
            else
            {
                // Use current collision energy regression to calculate CE
                collisionEnergy = (float)(setting.TransitionSettings.Prediction.CollisionEnergy.GetCollisionEnergy(
                                              skylineInput.NodeGroup.PrecursorAdduct,
                                              setting.GetRegressionMz(skylineInput.NodePep,
                                                  skylineInput.NodeGroup)) / 100.0);
            }

            return new PrositIntensityInput.PrositPrecursorInput(peptideSequence, precursorCharge, collisionEnergy);
        }

        public override PrositIntensityInput CreatePrositInput(IList<PrositIntensityInput.PrositPrecursorInput> prositInputRows)
        {
            return new PrositIntensityInput(prositInputRows);
        }

        public override PrositIntensityOutput CreatePrositOutput(MapField<string, TensorProto> prositOutputData)
        {
            return new PrositIntensityOutput(prositOutputData);
        }

        public override PrositMSMSSpectra CreateSkylineOutput(SrmSettings settings, IList<PeptidePrecursorPair> skylineInputs, PrositIntensityOutput prositOutput)
        {
            return new PrositMSMSSpectra(settings, skylineInputs, prositOutput);
        }

        public sealed class PrositIntensityInput : PrositInput<PrositIntensityInput.PrositPrecursorInput>
        {
            private const string PEPTIDES_KEY = "peptides_in:0";
            private const string PRECURSOR_CHARGE_KEY = "precursor_charge_in:0";
            private const string COLLISION_ENERGY_KEY = "collision_energy_in:0";

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
                            InputRows.Count, Constants.PEPTIDE_SEQ_LEN),
                        [PRECURSOR_CHARGE_KEY] = Create2dTensor(DataType.DtFloat, tp => tp.FloatVal,
                            InputRows.SelectMany(p => p.PrecursorCharge).ToArray(),
                            InputRows.Count, Constants.PRECURSOR_CHARGES),
                        [COLLISION_ENERGY_KEY] = Create2dTensor(DataType.DtFloat, tp => tp.FloatVal,
                            InputRows.Select(p => p.CollisionEnergy).ToArray(),
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
                public PrositPrecursorInput(int[] peptideSequence, float[] precursorCharge, float collisionEnergy)
                {
                    PeptideSequence = peptideSequence;
                    PrecursorCharge = precursorCharge;
                    CollisionEnergy = collisionEnergy;
                }


                public int[] PeptideSequence { get; }
                public float[] PrecursorCharge { get; }

                public float CollisionEnergy { get; }
            }
        }

        /// <summary>
        /// Represents the output returned from Prosits
        /// intensity model.
        /// </summary>
        public sealed class PrositIntensityOutput : PrositOutput<PrositIntensityOutput, PrositIntensityOutput.PrositPrecursorOutput>
        {
            private const string OUTPUT_KEY = "out/Reshape:0";

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
                    Intensities = new FragmentIonIntensity[Constants.PEPTIDE_SEQ_LEN - 1];

                    // Copy blocks of intensities for each ion
                    for (var i = 0; i < Constants.PEPTIDE_SEQ_LEN - 1; ++i)
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
                    if (index + Constants.PRECURSOR_CHARGES > tensor.FloatVal.Count)
                        throw new ArgumentException();

                    Intensities = new float[Constants.PRECURSOR_CHARGES];
                    // Copy intensities
                    for (var i = 0; i < Constants.PRECURSOR_CHARGES; ++i)
                        Intensities[i] = PrositHelpers.ReLu(tensor.FloatVal[index++]);
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
