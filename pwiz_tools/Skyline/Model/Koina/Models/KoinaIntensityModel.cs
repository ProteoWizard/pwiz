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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Google.Protobuf;
using Inference;
using pwiz.Common.Chemistry;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using static Inference.ModelInferRequest.Types;

namespace pwiz.Skyline.Model.Koina.Models
{
    /// <summary>
    /// Represents Koina's Intensity model.
    /// </summary>
    public sealed class KoinaIntensityModel : KoinaModel<KoinaIntensityModel.KoinaIntensityInput.KoinaPrecursorInput,
        KoinaIntensityModel.KoinaIntensityInput,
        KoinaIntensityModel.PeptidePrecursorNCE,
        KoinaIntensityModel.KoinaIntensityOutput.KoinaPrecursorOutput,
        KoinaIntensityModel.KoinaIntensityOutput,
        KoinaMS2Spectra>
    {
        static KoinaIntensityModel()
        {
            // Not sure if the models count will ever change. Maybe in the future
            // users can somehow add their own models. But this is just capacity anyways.
            _instances = new List<KoinaIntensityModel>(Models.Count());
        }

        // Singleton pattern
        private KoinaIntensityModel(string model)
        {
            if (!Models.Contains(model))
                throw new KoinaNotConfiguredException(string.Format(
                    KoinaResources.KoinaIntensityModel_KoinaIntensityModel_Intensity_model__0__does_not_exist,
                    model));

            Model = model;
        }
        
        private static List<KoinaIntensityModel> _instances;

        public static KoinaIntensityModel GetInstance(string model)
        {
            var intensityModel = _instances.FirstOrDefault(p => p.Model == model);
            if (intensityModel == null)
                _instances.Add(intensityModel = new KoinaIntensityModel(model));
                
            return intensityModel;
        }

        public static KoinaIntensityModel Instance
        {
            get
            {
                var selectedModel = Settings.Default.KoinaIntensityModel;
                return GetInstance(selectedModel);
            }
        }

        public const string SIGNATURE = "v1";

        public override string Signature => SIGNATURE;
        public override string Model { get; protected set; }

        private bool Equals(KoinaIntensityModel other)
        {
            return string.Equals(Model, other.Model);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is KoinaIntensityModel other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Model != null ? Model.GetHashCode() : 0);
        }

        public static IEnumerable<string> Models => new[]
        {
            @"Prosit_2019_intensity",
            @"Prosit_2020_intensity_CID",
            @"Prosit_2020_intensity_HCD",
            //@"Prosit_2020_intensity_TMT",
            @"Prosit_2023_intensity_timsTOF",
            @"ms2pip_2021_HCD",

            // TODO: AlphaPept_ms2 requires instrument type tensor (https://github.com/MannLabs/alphapeptdeep?tab=readme-ov-file#export-settings)
            // @"AlphaPept_ms2_generic",
        };

        public override IDictionary<string, ModelInputs> InputsForModel => new Dictionary<string, ModelInputs>
        {
            {@"Prosit_2019_intensity", new ModelInputs(true, true, true)},
            {@"Prosit_2020_intensity_CID", new ModelInputs(true, true, false)},
            {@"Prosit_2020_intensity_HCD", new ModelInputs(true, true, true)},
            //@"Prosit_2020_intensity_TMT",
            {@"Prosit_2023_intensity_timsTOF", new ModelInputs(true, true, true)},
            {@"ms2pip_2021_HCD", new ModelInputs(true, true, false)},
            // {@"AlphaPept_ms2_generic", new ModelInputs(true, true, true)},
        };

        public override KoinaIntensityInput.KoinaPrecursorInput CreateKoinaInputRow(SrmSettings settings, PeptidePrecursorNCE skylineInput, out KoinaException exception)
        {
            var peptideSequence = KoinaHelpers.EncodeSequence(settings, skylineInput.NodePep, IsotopeLabelType.light, out exception);
            if (peptideSequence == null) // equivalently, exception != null
                return null;
            if (skylineInput.NodeGroup.PrecursorCharge > KoinaConstants.PRECURSOR_CHARGES)
            {
                exception = new KoinaException(string.Format(
                    ModelsResources.KoinaIntensityModel_CreateKoinaInputRow_UnsupportedCharge,
                    skylineInput.NodeGroup.PrecursorCharge, KoinaConstants.PRECURSOR_CHARGES));
                return null;
            }

            return new KoinaIntensityInput.KoinaPrecursorInput(peptideSequence, skylineInput.PrecursorCharge, skylineInput.NCE.Value);
        }

        public static KoinaIntensityInput.KoinaPrecursorInput CreateKoinaInputRow(string sequence, int charge, float nce, out KoinaException exception)
        {
            var peptideSequence = KoinaHelpers.EncodeSequence(sequence, out exception);
            if (peptideSequence == null) // equivalently, exception != null
                return null;

            return new KoinaIntensityInput.KoinaPrecursorInput(peptideSequence, charge, nce);
        }

        public override KoinaIntensityInput CreateKoinaInput(IList<KoinaIntensityInput.KoinaPrecursorInput> koinaInputRows)
        {
            return new KoinaIntensityInput(koinaInputRows);
        }

        public override KoinaIntensityOutput CreateKoinaOutput(ModelInferResponse koinaOutputData)
        {
            return new KoinaIntensityOutput(koinaOutputData);
        }

        public override KoinaMS2Spectra CreateSkylineOutput(SrmSettings settings, IList<PeptidePrecursorNCE> skylineInputs, KoinaIntensityOutput koinaOutput)
        {
            return new KoinaMS2Spectra(settings, skylineInputs, koinaOutput);
        }

        public class PeptidePrecursorNCE : SkylineInputRow
        {

            public PeptidePrecursorNCE(string sequence, int precursorCharge, SignedMz precursorMz, ExplicitMods explicitMods, IsotopeLabelType isotopeLabelType, int? nce = null)
            {
                Sequence = sequence;
                PrecursorCharge = precursorCharge;
                PrecursorMz = precursorMz;
                ExplicitMods = explicitMods;
                LabelType = isotopeLabelType;
                NCE = nce;
            }

            public PeptidePrecursorNCE(PeptideDocNode nodePep, TransitionGroupDocNode nodeGroup, IsotopeLabelType isotopeLabelType = null, int? nce = null)
                : this(nodePep.Peptide.Sequence, nodeGroup.PrecursorCharge, nodeGroup.PrecursorMz, nodePep.ExplicitMods, isotopeLabelType ?? nodeGroup.LabelType, nce)
            {
                NodePep = nodePep;
                NodeGroup = nodeGroup;
            }

            public PeptideDocNode NodePep { get; }
            public TransitionGroupDocNode NodeGroup { get; }
            public string Sequence { get; }
            public int PrecursorCharge { get; }
            public SignedMz PrecursorMz { get; }
            public ExplicitMods ExplicitMods { get; }
            public IsotopeLabelType LabelType { get; }
            public int? NCE { get; private set; }

            public PeptidePrecursorNCE WithNCE(int nce)
            {
                return new PeptidePrecursorNCE(Sequence, PrecursorCharge, PrecursorMz, ExplicitMods, LabelType, nce);
            }

            public override bool Equals(SkylineInputRow other)
            {
                if (other == null)
                    return false;
                if (!(other is PeptidePrecursorNCE peptidePrecursorPair))
                    return false;
                return ReferenceEquals(Sequence, peptidePrecursorPair.Sequence)
                       && PrecursorCharge == peptidePrecursorPair.PrecursorCharge
                       && ReferenceEquals(ExplicitMods, peptidePrecursorPair.ExplicitMods)
                       && NCE == peptidePrecursorPair.NCE
                       && Equals(LabelType, peptidePrecursorPair.LabelType);
            }
        }

        /// <summary>
        /// Input structure at the Koina level, it contains the tensors
        /// which are sent to Koina.
        /// </summary>
        public sealed class KoinaIntensityInput : KoinaInput<KoinaIntensityInput.KoinaPrecursorInput>
        {
            public static readonly string PEPTIDES_KEY = @"peptide_sequences";
            public static readonly string PRECURSOR_CHARGE_KEY = @"precursor_charges";
            public static readonly string COLLISION_ENERGY_KEY = @"collision_energies";

            public KoinaIntensityInput(IList<KoinaPrecursorInput> precursorInputs)
            {
                InputRows = precursorInputs;
            }

            public override IList<KoinaPrecursorInput> InputRows { get; }

            public override IList<InferInputTensor> KoinaTensors
            {
                get
                {
                    return new List<InferInputTensor>
                    {
                        Create2dTensor(PEPTIDES_KEY, DataTypes.BYTES, tp => tp.BytesContents,
                            InputRows.Select(p => ByteString.CopyFrom(p.PeptideSequence, Encoding.ASCII)).ToArray(),
                            InputRows.Count, 1),
                        Create2dTensor(PRECURSOR_CHARGE_KEY, DataTypes.INT32, tp => tp.IntContents,
                            InputRows.Select(p => p.PrecursorCharge).ToArray(),
                            InputRows.Count, 1),
                        Create2dTensor(COLLISION_ENERGY_KEY, DataTypes.FP32, tp => tp.Fp32Contents,
                            InputRows.Select(p => p.NormalizedCollisionEnergy).ToArray(),
                            InputRows.Count, 1)
                    };
                }

            }

            public override IList<string> OutputTensorNames => KoinaIntensityOutput.OUTPUT_KEYS;

            /// <summary>
            /// Represents a single Precursor that can be used to construct
            /// input tensors for Koinas intensity model.
            /// </summary>
            public class KoinaPrecursorInput
            {
                public KoinaPrecursorInput(string peptideSequence, int precursorCharge, float normalizedCollisionEnergy)
                {
                    PeptideSequence = peptideSequence;
                    PrecursorCharge = precursorCharge;
                    NormalizedCollisionEnergy = normalizedCollisionEnergy;
                }


                public string PeptideSequence { get; }
                public int PrecursorCharge { get; }

                public float NormalizedCollisionEnergy { get; }

                public override string ToString()
                {
                    return TextUtil.SpaceSeparate(PeptideSequence + Transition.GetChargeIndicator(PrecursorCharge),
                        TextUtil.ColonSeparate(@"NCE", NormalizedCollisionEnergy.ToString(CultureInfo.CurrentCulture)));
                }
            }
        }

        /// <summary>
        /// Represents the output returned from Koinas
        /// intensity model. Is constructed directly from the incoming tensors.
        /// </summary>
        public sealed class KoinaIntensityOutput : KoinaOutput<KoinaIntensityOutput, KoinaIntensityOutput.KoinaPrecursorOutput>
        {
            public static readonly string[] OUTPUT_KEYS = { @"annotation", @"intensities" };

            public KoinaIntensityOutput(ModelInferResponse koinaOutput)
            {
                // Note that this is essentially a lightweight iterator. We pass
                // down an index by reference that keeps getting increased.
                var precursorCount = (int) koinaOutput.Outputs[0].Shape[0];
                OutputRows = new KoinaPrecursorOutput[precursorCount];

                using var annotationStream = new MemoryStream(koinaOutput.RawOutputContents[0].ToByteArray());
                using var annotationReader = new BinaryReader(annotationStream, Encoding.ASCII);
                var intensityStream = koinaOutput.RawOutputContents[1].CreateCodedInput();
                int totalAnnotations = koinaOutput.RawOutputContents[1].Length / sizeof(float);
                int annotationsPerPrecursor = koinaOutput.RawOutputContents[1].Length / precursorCount / sizeof(float);

                // assume all precursors have the same number of annotations (true for all models AFAIK)
                Assume.AreEqual(0, totalAnnotations % precursorCount);

                var annotations = new string[totalAnnotations];
                for (int i=0; i < totalAnnotations; ++i)
                {
                    int length = annotationReader.ReadInt32();
                    var bytes = annotationReader.ReadBytes(length);
                    annotations[i] = Encoding.ASCII.GetString(bytes);
                }

                //int annotationsPerPrecursor = annotations.Count / precursorCount;

                var index = 0;
                // Copy intensities for each precursor
                for (var i = 0; i < precursorCount; ++i)
                {
                    var annotationsForPrecursor = new ReadOnlySpan<string>(annotations, i * annotationsPerPrecursor, annotationsPerPrecursor);
                    OutputRows[i] = new KoinaPrecursorOutput(annotationsForPrecursor, intensityStream, ref index);
                }
            }

            public KoinaIntensityOutput()
            {
                OutputRows = new KoinaPrecursorOutput[0];
            }

            public override IList<KoinaPrecursorOutput> OutputRows { get; protected set; }


            /// <summary>
            /// Represents the intensity predictions for a single peptide.
            /// </summary>
            public class KoinaPrecursorOutput
            {
                static readonly char[] NUMBERS = { '1', '2', '3', '4', '5', '6', '7', '8', '9' };

                public KoinaPrecursorOutput(ReadOnlySpan<string> annotations, CodedInputStream intensityStream, ref int index)
                {
                    var ionTypeNumberCharges = new (IonType ionType, int ionNumber, int ionCharge, float intensity)[annotations.Length];

                    int maxCharge = 1, seqLength = 0;
                    for (int i=0; i < annotations.Length; ++i)
                    {
                        ionTypeNumberCharges[i].intensity = Math.Max(0, intensityStream.ReadFloat());
                        if (ionTypeNumberCharges[i].intensity <= 0)
                            continue;

                        var annotation = annotations[i]; // e.g. y20+2
                        int ionNumberStart = annotation.IndexOfAny(NUMBERS);
                        int ionChargeStart = annotation.IndexOf('+');
                        ionTypeNumberCharges[i].ionType = TransitionFilter.ParseIonType(annotation.Substring(0, ionNumberStart));
                        ionTypeNumberCharges[i].ionNumber = int.Parse(annotation.Substring(ionNumberStart, ionChargeStart - ionNumberStart));
                        ionTypeNumberCharges[i].ionCharge = int.Parse(annotation.Substring(ionChargeStart + 1)) - 1; // 0-based charge indexing

                        maxCharge = Math.Max(maxCharge, ionTypeNumberCharges[i].ionCharge + 1);
                        seqLength = Math.Max(seqLength, ionTypeNumberCharges[i].ionNumber);
                    }

                    Intensities = new IonTable<float[]>(IonType.y, seqLength);

                    const float intensityCutoff = 1.0e-6f; // Koina produces many peaks with intensity < 1e-6 that the older Koina does not

                    for (int i = 0; i < ionTypeNumberCharges.Length; ++i)
                    {
                        var ionTypeNumberCharge = ionTypeNumberCharges[i];
                        if (ionTypeNumberCharge.intensity <= intensityCutoff)
                            continue;

                        if (Intensities.GetIonValue(ionTypeNumberCharge.ionType, ionTypeNumberCharge.ionNumber) == null)
                            Intensities.SetIonValue(ionTypeNumberCharge.ionType, ionTypeNumberCharge.ionNumber, new float[maxCharge]);
                        Intensities.GetIonValue(ionTypeNumberCharge.ionType, ionTypeNumberCharge.ionNumber)[ionTypeNumberCharge.ionCharge] = ionTypeNumberCharge.intensity;
                    }
                }

                /// <summary>
                /// Ion table of intensities (as an array indexed by charge state minus 1)
                /// </summary>
                public IonTable<float[]> Intensities { get; }
            }
        }
    }
}
