/*
 * Original author: Matt Chambers <matt.chambers42+UW .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Google.Protobuf;
using Grpc.Core;
using Inference;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using pwiz.Common.Collections;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Koina;
using pwiz.Skyline.Model;
using pwiz.Skyline;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Koina.Communication;
using pwiz.Skyline.Model.Koina.Config;
using pwiz.Skyline.Model.Koina.Models;
using pwiz.Skyline.Util;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using Newtonsoft.Json.Converters;

namespace pwiz.SkylineTestUtil
{

    /// <summary>
    /// A fake prediction client for logging predictions and returning cached
    /// predictions. For logging, it needs to be constructed with a server address.
    /// For returning cached predictions, a queue of expected queries should be passed in.
    /// </summary>
    public class FakeKoinaPredictionClient : KoinaPredictionClient
    {
        public readonly HashSet<KoinaQuery> ExpectedQueries;
        private bool _recordData;

        public FakeKoinaPredictionClient(Channel channel, HashSet<KoinaQuery> expectedQueries) :
            base(channel, KoinaConfig.GetKoinaConfig().Server)
        {
            _recordData = expectedQueries == null;
            if (!_recordData)
            {
                ExpectedQueries = expectedQueries;
            }
            else
            {
                ExpectedQueries = new HashSet<KoinaQuery>(new InputsEqualityComparer());
            }
        }

        public int QueryIndex { get; private set; }

        /// <summary>
        /// When set, RT model queries return an empty response, simulating
        /// the case where the RT model fails to return a result for the peptide.
        /// Used to test regression fix for issue #3971.
        /// </summary>
        public bool SuppressRetentionTimePrediction { get; set; }

        public override ModelInferResponse ModelInfer(ModelInferRequest request, CallOptions options)
        {
            // If this is a ping ms2 request, silently return and don't log
            if (PING_QUERY_MS2.MatchesQuery(request))
                return PING_QUERY_MS2.Response;

            // If this is a ping irt request, silently return and don't log
            if (PING_QUERY_IRT.MatchesQuery(request))
                return PING_QUERY_IRT.Response;

            // If suppressing RT predictions, return an empty response for RT model queries
            if (SuppressRetentionTimePrediction &&
                KoinaRetentionTimeModel.Models.Any(m => request.ModelName.StartsWith(m)))
            {
                return new ModelInferResponse();
            }

            // Logging mode
            if (_recordData)
            {
                var response = base.ModelInfer(request, options);
                LogQuery(request, response);
                return response;
            }

            // Caching mode
            if (!KoinaQuery.TryGetQuery(ExpectedQueries, request, out var nextQuery))
                Assert.Fail("No matching reference query for request: " + request);
            nextQuery.AssertMatchesQuery(request);
            return nextQuery.Response;
        }

        private void LogQuery(ModelInferRequest request, ModelInferResponse response)
        {
            if (request.ModelName.StartsWith(KoinaIntensityModel.Models.First()))
                ExpectedQueries.Add(KoinaIntensityQuery.FromTensors(request, response));
            else if (request.ModelName.StartsWith(KoinaRetentionTimeModel.Models.First()))
                ExpectedQueries.Add(KoinaRetentionTimeQuery.FromTensors(request, response));
            else
                Assert.Fail("Unknown model \"{0}\"", request.ModelName);
        }

        static KoinaQuery PING_QUERY_MS2 = new KoinaIntensityQuery(
            new[]
            {
                new KoinaIntensityInput("PING", 0.3200f, 1)
            },
            new[]
            {
                new IonTable<float[]>(IonType.z, 4)
            }
        );

        static KoinaQuery PING_QUERY_IRT = new KoinaRetentionTimeQuery(
            new[]
            {
                "PING"
            },
            new[] { 0.0f });

    }

    public class FakeKoina : IDisposable
    {
        private readonly bool _recordData;
        private readonly string _expectedQueriesJsonFilepath;
        private Channel _channel;
        private FakeKoinaPredictionClient _fakeClient;
        private int _maxThreads;

        private JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            TypeNameHandling = TypeNameHandling.Auto,
            ContractResolver = new IonTableContractResolver()
        };

        public FakeKoina(bool recordData, string expectedQueriesJsonFilepath)
        {
            _recordData = recordData;
            _expectedQueriesJsonFilepath = expectedQueriesJsonFilepath;
            HashSet<KoinaQuery> expectedQueries = null;

            if (!recordData && _expectedQueriesJsonFilepath != null)
            {
                using var streamReader = new StreamReader(expectedQueriesJsonFilepath);
                using var jsonTextReader = new JsonTextReader(streamReader);
                expectedQueries = JsonSerializer.Create(JsonSettings).Deserialize<HashSet<KoinaQuery>>(jsonTextReader);
                expectedQueries = new HashSet<KoinaQuery>(expectedQueries, new InputsEqualityComparer());
            }

            _channel = KoinaConfig.GetKoinaConfig().CreateChannel();
            _fakeClient = new FakeKoinaPredictionClient(_channel, expectedQueries);
            Assert.IsNull(KoinaPredictionClient.FakeClient);
            KoinaPredictionClient.FakeClient = _fakeClient;

            // set Koina to single threaded when using FakeKoina
            _maxThreads = KoinaConstants.MAX_THREADS;
            KoinaConstants.MAX_THREADS = 1;
        }

        public void Dispose()
        {
            if (_recordData && _expectedQueriesJsonFilepath != null)
            {
                using var streamWriter = new StreamWriter(_expectedQueriesJsonFilepath);
                using var jsonTextWriter = new JsonTextWriter(streamWriter);
                JsonSerializer.Create(JsonSettings).Serialize(jsonTextWriter, _fakeClient.ExpectedQueries);
            }
            Assert.AreSame(_fakeClient, KoinaPredictionClient.FakeClient);
            KoinaPredictionClient.FakeClient = null;
            _channel.ShutdownAsync().Wait();
            KoinaConstants.MAX_THREADS = _maxThreads;
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public abstract class KoinaQuery
    {
        [JsonProperty]
        public abstract string Model { get; }
        public abstract ModelInferResponse Response { get; }

        public abstract void AssertMatchesQuery(ModelInferRequest pr);
        public abstract bool MatchesQuery(ModelInferRequest pr);

        public static bool TryGetQuery(HashSet<KoinaQuery> cachedQueries, ModelInferRequest request, out KoinaQuery cachedQuery)
        {
            if (KoinaIntensityQuery.IsQueryType(request))
            {
                return cachedQueries.TryGetValue(KoinaIntensityQuery.FromTensors(request), out cachedQuery);
            }
            else if (KoinaRetentionTimeQuery.IsQueryType(request))
            {
                return cachedQueries.TryGetValue(KoinaRetentionTimeQuery.FromTensors(request), out cachedQuery);
            }

            throw new ArgumentException("unhandled Koina query type");
        }

        public static bool TryGetQuery(HashSet<KoinaQuery> cachedQueries, SrmSettings settings, IList<KoinaIntensityModel.PeptidePrecursorNCE> request, int NCE, out KoinaIntensityQuery cachedQuery)
        {
            var model = KoinaIntensityModel.Instance;
            var queriesPassingFilters = request.Where(i => model.CreateKoinaInputRow(settings, i.WithNCE(NCE), out var ex) != null && ex == null);
            var intensityInputs = queriesPassingFilters.Select(i => new KoinaIntensityInput(i.NodePep.ModifiedSequence, NCE, i.PrecursorCharge));
            var intensityInputQuery = new KoinaIntensityQuery(intensityInputs.ToArray(), null);

            var tryGetValue = cachedQueries.TryGetValue(intensityInputQuery, out var intensityQuery);
            if (!tryGetValue)
            {
                cachedQuery = null;
                return false;
            }
            cachedQuery = (KoinaIntensityQuery) intensityQuery;
            return true;
        }

        public static bool TryGetQuery(HashSet<KoinaQuery> cachedQueries, SrmSettings settings, IList<KoinaRetentionTimeModel.PeptideDocNodeWrapper> sequencesForRtQuery, out KoinaRetentionTimeQuery cachedQuery)
        {
            var model = KoinaRetentionTimeModel.Instance;
            var queriesPassingFilters = sequencesForRtQuery.Where(i => model.CreateKoinaInputRow(settings, i, out var ex) != null && ex == null);
            var rtInputQuery = new KoinaRetentionTimeQuery(queriesPassingFilters.Select(i => i.Node.ModifiedSequence).Distinct().ToArray(), null);

            var tryGetValue = cachedQueries.TryGetValue(rtInputQuery, out var rtQuery);
            if (!tryGetValue)
            {
                cachedQuery = null;
                return false;
            }
            cachedQuery = (KoinaRetentionTimeQuery) rtQuery;
            return true;
        }
    }

    public class InputsEqualityComparer : EqualityComparer<KoinaQuery>
    {
        public override bool Equals(KoinaQuery x, KoinaQuery y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(null, x)) return false;
            if (ReferenceEquals(null, y)) return false;
            if (x.GetType() != y.GetType()) return false;

            if (x is KoinaIntensityQuery x2 && y is KoinaIntensityQuery y2)
            {
                return x2.InputsEquals(y2);
            }
            else if (x is KoinaRetentionTimeQuery x3 && y is KoinaRetentionTimeQuery y3)
            {
                return x3.InputsEquals(y3);
            }

            throw new ArgumentException("unhandled Koina query type");
        }

        public override int GetHashCode(KoinaQuery x)
        {
            if (x is KoinaIntensityQuery x2)
            {
                return x2.GetInputsHashCode();
            }
            else if (x is KoinaRetentionTimeQuery x3)
            {
                return x3.GetInputsHashCode();
            }
            throw new ArgumentException("unhandled Koina query type");
        }
    }

    [JsonObject]
    public class KoinaIntensityInput
    {
        public KoinaIntensityInput(string modifiedSequence, float normalizedCollisionEnergy, int precursorCharge)
        {
            ModifiedSequence = modifiedSequence;
            NormalizedCollisionEnergy = normalizedCollisionEnergy;
            PrecursorCharge = precursorCharge;
        }

        public string ModifiedSequence { get; private set; }
        public float NormalizedCollisionEnergy { get; private set; }
        public int PrecursorCharge { get; private set; }

        public bool Equals(KoinaIntensityInput other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return ModifiedSequence == other.ModifiedSequence &&
                   NormalizedCollisionEnergy.Equals(other.NormalizedCollisionEnergy) &&
                   PrecursorCharge == other.PrecursorCharge;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((KoinaIntensityInput)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (ModifiedSequence != null ? ModifiedSequence.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ NormalizedCollisionEnergy.GetHashCode();
                hashCode = (hashCode * 397) ^ PrecursorCharge;
                return hashCode;
            }
        }
    }

    [JsonObject(MemberSerialization.Fields)]
    public class KoinaIntensityQuery : KoinaQuery
    {
        private KoinaIntensityInput[] _inputs;
        private IonTable<float[]>[] _ionTables;

        public KoinaIntensityQuery(KoinaIntensityInput[] inputs, IonTable<float[]>[] actualIntensities)
        {
            _inputs = inputs;
            _ionTables = actualIntensities;
            if (actualIntensities != null)
                Assert.AreEqual(_inputs.Length, _ionTables.Length);
        }

        public bool InputsEquals(KoinaIntensityQuery y)
        {
            if (ReferenceEquals(this, y)) return true;
            if (ReferenceEquals(null, y)) return false;

            return _inputs.SequenceEqual(y._inputs);
        }

        public int GetInputsHashCode()
        {
            return ((IStructuralEquatable)_inputs).GetHashCode(EqualityComparer<KoinaIntensityInput>.Default);
        }

        public static KoinaIntensityQuery FromTensors(ModelInferRequest request, ModelInferResponse response = null)
        {
            // Sequences
            var seqs = request.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.PEPTIDES_KEY, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(seqs.Shape.Count, 2);
            //Assert.AreEqual(seqs.Shape[1], KoinaConstants.PEPTIDE_SEQ_LEN);
            var decodedSeqs = KoinaHelpers.DecodeSequences(seqs);

            // CEs
            var ces = request.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.COLLISION_ENERGY_KEY, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(ces.Shape.Count, 2);
            Assert.AreEqual(ces.Shape[1], 1);
            var decodedCes = ces.Contents.Fp32Contents.ToArray();

            // Charges
            var charges = request.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.PRECURSOR_CHARGE_KEY, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(charges.Shape.Count, 2);
            //Assert.AreEqual(charges.Shape[1], KoinaConstants.PRECURSOR_CHARGES);
            var decodedCharges = KoinaHelpers.DecodeCharges(charges);

            var inputs = Enumerable.Range(0, decodedSeqs.Length)
                .Select(i => new KoinaIntensityInput(decodedSeqs[i], decodedCes[i], decodedCharges[i])).ToArray();

            if (response == null)
                return new KoinaIntensityQuery(inputs, null);

            var koinaOutput = new KoinaIntensityModel.KoinaIntensityOutput(response);

            var outputs = new IonTable<float[]>[inputs.Length];
            for (int i = 0; i < inputs.Length; ++i)
            {
                outputs[i] = koinaOutput.OutputRows[i].Intensities;
            }

            return new KoinaIntensityQuery(inputs, outputs);
        }

        public override ModelInferResponse Response
        {
            get
            {
                var pr = new ModelInferResponse();
                pr.ModelName = Model;

                // Construct Tensor
                var tp = new ModelInferResponse.Types.InferOutputTensor { Datatype = "BYTES" };
                tp.Name = KoinaIntensityModel.KoinaIntensityOutput.OUTPUT_KEYS[0];
                tp.Shape.Add(_ionTables.Length);
                tp.Shape.Add(1);
                pr.Outputs.Add(tp);

                tp = new ModelInferResponse.Types.InferOutputTensor { Datatype = "FP32" };
                tp.Name = KoinaIntensityModel.KoinaIntensityOutput.OUTPUT_KEYS[1];
                tp.Shape.Add(_ionTables.Length);
                tp.Shape.Add(1);
                pr.Outputs.Add(tp);

                using var annotationStream = new MemoryStream();
                using var annotationWriter = new BinaryWriter(annotationStream);
                var intensitiesFlat = new List<float>();
                int maxIonNumber = _ionTables.Max(t => t.GetLength(1));
                int maxCharge = _ionTables.Max(t =>
                {
                    var entries = IonTableEntry.EnumerateIonTable(t).ToArray();
                    return entries.Any() ? entries.Max(e => e.chargeState) : 0;
                });
                var allIonTypes = _ionTables.SelectMany(t => Helpers.GetEnumValues<IonType>().Where(t.ContainsIonType)).Distinct().ToList();
                for (var i = 0; i < _inputs.Length; i++)
                {
                    var ionTable = _ionTables[i];
                    foreach(var ionType in allIonTypes)
                    {
                        for (int ionNumber = 1; ionNumber <= maxIonNumber; ++ionNumber)
                        {
                            float[] intensities = null;

                            if (ionNumber < ionTable.GetLength(1))
                                intensities = ionTable.GetIonValue(ionType, ionNumber);

                            if (intensities == null)
                            {
                                for (int charge = 0; charge < maxCharge; ++charge)
                                {
                                    annotationWriter.Write(0);
                                    intensitiesFlat.Add(-1);
                                }
                                continue;
                            }

                            for (int charge = 0; charge < maxCharge; ++charge)
                            {
                                string annotation = string.Format("{0}{1}+{2}", ionType, ionNumber, charge + 1);
                                annotationWriter.Write(annotation.Length);
                                annotationWriter.Write(annotation.ToCharArray());
                                intensitiesFlat.Add(intensities[charge]);
                            }
                        }
                    }
                }

                annotationStream.Seek(0, SeekOrigin.Begin);
                pr.RawOutputContents.Add(ByteString.FromStream(annotationStream));
                pr.RawOutputContents.Add(ByteString.CopyFrom(PrimitiveArrays.ToBytes(intensitiesFlat.ToArray())));

                return pr;
            }
        }

        public override string Model => KoinaIntensityModel.Models.First();

        public static bool IsQueryType(ModelInferRequest pr)
        {
            var keys = pr.Inputs.Select(t => t.Name).OrderBy(s => s).ToArray();
            return pr.Inputs.Count == 3 &&
                   keys[0] == KoinaIntensityModel.KoinaIntensityInput.COLLISION_ENERGY_KEY &&
                   keys[1] == KoinaIntensityModel.KoinaIntensityInput.PEPTIDES_KEY &&
                   keys[2] == KoinaIntensityModel.KoinaIntensityInput.PRECURSOR_CHARGE_KEY;
        }

        public override bool MatchesQuery(ModelInferRequest pr)
        {
            var keys = pr.Inputs.Select(t => t.Name).OrderBy(s => s).ToArray();
            if (Model != pr.ModelName ||
                pr.Inputs.Count != 3 ||
                keys[0] != KoinaIntensityModel.KoinaIntensityInput.COLLISION_ENERGY_KEY ||
                keys[1] != KoinaIntensityModel.KoinaIntensityInput.PEPTIDES_KEY ||
                keys[2] != KoinaIntensityModel.KoinaIntensityInput.PRECURSOR_CHARGE_KEY)
                return false;

            var seqs = pr.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.PEPTIDES_KEY, StringComparison.InvariantCultureIgnoreCase));
            var ces = pr.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.COLLISION_ENERGY_KEY, StringComparison.InvariantCultureIgnoreCase));
            var charges = pr.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.PRECURSOR_CHARGE_KEY, StringComparison.InvariantCultureIgnoreCase));
            return // Sequences
                   seqs.Shape.Count == 2 &&
                   seqs.Shape[0] == _inputs.Length &&
                   seqs.Shape[1] == 1 &&
                   ArrayUtil.EqualsDeep(_inputs.Select(i => new ModifiedSequence(i.ModifiedSequence, MassType.Monoisotopic)).ToArray(),
                       KoinaHelpers.DecodeSequences2(seqs)) &&

                   // CEs
                   ces.Shape.Count == 2 &&
                   ces.Shape[0] == _inputs.Length &&
                   ces.Shape[1] == 1 &&

                   // Charges
                   charges.Shape.Count == 2 &&
                   charges.Shape[0] == _inputs.Length &&
                   charges.Shape[1] == 1;
        }

        public override void AssertMatchesQuery(ModelInferRequest pr)
        {
            Assert.AreEqual(Model, pr.ModelName);

            Assert.AreEqual(pr.Inputs.Count, 3);
            var keys = pr.Inputs.Select(t => t.Name).OrderBy(s => s).ToArray();
            Assert.AreEqual(keys[0], KoinaIntensityModel.KoinaIntensityInput.COLLISION_ENERGY_KEY);
            Assert.AreEqual(keys[1], KoinaIntensityModel.KoinaIntensityInput.PEPTIDES_KEY);
            Assert.AreEqual(keys[2], KoinaIntensityModel.KoinaIntensityInput.PRECURSOR_CHARGE_KEY);

            // Sequences
            var seqs = pr.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.PEPTIDES_KEY, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(seqs.Shape.Count, 2);
            Assert.AreEqual(seqs.Shape[0], _inputs.Length);
            Assert.AreEqual(seqs.Shape[1], 1);
            AssertEx.AreEqualDeep(_inputs.Select(i => new ModifiedSequence(i.ModifiedSequence, MassType.Monoisotopic)).ToArray(),
                KoinaHelpers.DecodeSequences2(seqs).Select(i => new ModifiedSequence(i.MonoisotopicMasses, MassType.Monoisotopic)).ToArray());

            // CEs
            var ces = pr.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.COLLISION_ENERGY_KEY, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(ces.Shape.Count, 2);
            Assert.AreEqual(ces.Shape[0], _inputs.Length);
            Assert.AreEqual(ces.Shape[1], 1);

            // Charges
            var charges = pr.Inputs.Single(t => t.Name.Equals(KoinaIntensityModel.KoinaIntensityInput.PRECURSOR_CHARGE_KEY, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(charges.Shape.Count, 2);
            Assert.AreEqual(charges.Shape[0], _inputs.Length);
            Assert.AreEqual(charges.Shape[1], 1);
        }

        public void AssertMatchesSpectra(KoinaIntensityModel.PeptidePrecursorNCE[] peptidePrecursorNCEs, SpectrumDisplayInfo[] spectrumDisplayInfos)
        {
            for (int i = 0; i < _inputs.Length; ++i)
                if (spectrumDisplayInfos[i] != null)
                    AssertMatchesSpectrum(peptidePrecursorNCEs[i], _inputs[i], _ionTables[i], spectrumDisplayInfos[i]);
        }

        public void AssertMatchesSpectrum(KoinaIntensityModel.PeptidePrecursorNCE peptidePrecursorNCE, SpectrumDisplayInfo spectrumDisplayInfo)
        {
            AssertMatchesSpectrum(peptidePrecursorNCE, _inputs[0], _ionTables[0], spectrumDisplayInfo);
        }

        public static void AssertMatchesSpectrum(KoinaIntensityModel.PeptidePrecursorNCE peptidePrecursorNCE, KoinaIntensityInput input, IonTable<float[]> spectrum, SpectrumDisplayInfo spectrumDisplayInfo)
        {
            Assert.IsNotNull(spectrumDisplayInfo);
            Assert.AreEqual(spectrumDisplayInfo.Name, "Koina");

            // Calculate expected number of peaks. 1 peak per residue times the number of possible charges
            var residues = FastaSequence.StripModifications(input.ModifiedSequence).Length - 1;
            var charges = Math.Min(input.PrecursorCharge, 3);
            var ionCount = 2 * residues * charges;

            //Assert.AreEqual(spectrumDisplayInfo.SpectrumPeaksInfo.Peaks.Length, ionCount);

            // Construct a koina output object so that we can construct a spectrum for comparison.
            // There really is no easier way to do this without rewriting a lot of code for parsing the
            // flattened intensities and adding lots of extra test code inside of Skyline code.
            var response = new ModelInferResponse();
            var tensor = new ModelInferResponse.Types.InferOutputTensor();
            tensor.Shape.Add(1);
            tensor.Shape.Add(spectrum.GetLength(1));
            tensor.Name = KoinaIntensityModel.KoinaIntensityOutput.OUTPUT_KEYS[1];
            response.Outputs.Add(tensor);

            using var annotationStream = new MemoryStream();
            using var annotationWriter = new BinaryWriter(annotationStream);
            using var intensityStream = new MemoryStream();
            using var intensityWriter = new BinaryWriter(intensityStream);
            foreach (var ionType in new[] { IonType.y, IonType.b })
                for (int i = 1; i <= spectrum.GetLength(1); ++i)
                {
                    var intensities = spectrum.GetIonValue(ionType, i);
                    if (intensities == null)
                        continue;

                    for (int z = 1; z <= intensities.Length; ++z)
                    {
                        string annotation = string.Format("{0}{1}+{2}", ionType, i, z);
                        annotationWriter.Write(annotation.Length);
                        annotationWriter.Write(annotation.ToCharArray());
                        intensityWriter.Write(intensities[z - 1]);
                    }
                }
            annotationStream.Seek(0, SeekOrigin.Begin);
            intensityStream.Seek(0, SeekOrigin.Begin);
            response.RawOutputContents.Add(ByteString.FromStream(annotationStream));
            response.RawOutputContents.Add(ByteString.FromStream(intensityStream));

            var fakeKoinaOutput = new KoinaIntensityModel.KoinaIntensityOutput(response);
            var ms2Spectrum = new KoinaMS2Spectrum(Program.MainWindow.Document.Settings,
                peptidePrecursorNCE.WithNCE((int)(input.NormalizedCollisionEnergy * 100.0f)), 0, fakeKoinaOutput);

            // Compare the spectra
            AssertEx.AreEqualDeep(ms2Spectrum.SpectrumPeaks.Peaks, spectrumDisplayInfo.SpectrumPeaksInfo.Peaks);
        }
    }

    [JsonObject(MemberSerialization.Fields)]
    public class KoinaRetentionTimeQuery : KoinaQuery
    {
        private string[] _modifiedSequences;
        private float[] _iRTs;

        public KoinaRetentionTimeQuery(string[] modifiedSequences, float[] iRTs)
        {
            _modifiedSequences = modifiedSequences;
            _iRTs = iRTs;
        }

        public bool InputsEquals(KoinaRetentionTimeQuery y)
        {
            if (ReferenceEquals(this, y)) return true;
            if (ReferenceEquals(null, y)) return false;

            return _modifiedSequences.SequenceEqual(y._modifiedSequences);
        }

        public int GetInputsHashCode()
        {
            return ((IStructuralEquatable)_modifiedSequences).GetHashCode(EqualityComparer<string>.Default);
        }

        public static KoinaRetentionTimeQuery FromTensors(ModelInferRequest request, ModelInferResponse response = null)
        {
            // Sequences
            var seqs = request.Inputs.Single(t => t.Name.Equals(KoinaRetentionTimeModel.KoinaRTInput.PEPTIDES_KEY, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(seqs.Shape.Count, 2);
            //Assert.AreEqual(seqs.Shape[1], KoinaConstants.PEPTIDE_SEQ_LEN);
            var decodedSeqs = KoinaHelpers.DecodeSequences(seqs);

            if (response == null)
                return new KoinaRetentionTimeQuery(decodedSeqs, null);

            var outputs = new KoinaRetentionTimeModel.KoinaRTOutput(response);
            return new KoinaRetentionTimeQuery(decodedSeqs, outputs.OutputRows.Select(o => (float) o.iRT).ToArray());
        }

        public static bool IsQueryType(ModelInferRequest pr)
        {
            return pr.Inputs.Count == 1 &&
                   pr.Inputs.First().Name == KoinaRetentionTimeModel.KoinaRTInput.PEPTIDES_KEY;
        }

        private string[] DecodedSequences(ModelInferRequest pr)
        {
            return KoinaHelpers.DecodeSequences2(pr.Inputs.Single(t =>
                t.Name.Equals(KoinaRetentionTimeModel.KoinaRTInput.PEPTIDES_KEY))).Select(i => i.MonoisotopicMasses).ToArray();
        }

        public override bool MatchesQuery(ModelInferRequest pr)
        {
            if (Model != pr.ModelName ||
                pr.Inputs.Count != 1 &&
                pr.Inputs.First().Name != KoinaRetentionTimeModel.KoinaRTInput.PEPTIDES_KEY)
                return false;

            var tensor = pr.Inputs.Single(t => t.Name.Equals(KoinaRetentionTimeModel.KoinaRTInput.PEPTIDES_KEY, StringComparison.InvariantCultureIgnoreCase));
            return tensor.Shape.Count == 2 &&
                   tensor.Shape[0] == _modifiedSequences.Length &&
                   tensor.Shape[1] == 1 &&
                   ArrayUtil.EqualsDeep(_modifiedSequences.Select(i => new ModifiedSequence(i, MassType.Monoisotopic).MonoisotopicMasses).ToArray(),
                       DecodedSequences(pr));
        }

        public override void AssertMatchesQuery(ModelInferRequest pr)
        {
            Assert.AreEqual(Model, pr.ModelName);
            Assert.AreEqual(pr.Inputs.Count, 1);
            Assert.AreEqual(pr.Inputs.First().Name, KoinaRetentionTimeModel.KoinaRTInput.PEPTIDES_KEY);
            var tensor = pr.Inputs.Single(t => t.Name.Equals(KoinaRetentionTimeModel.KoinaRTInput.PEPTIDES_KEY, StringComparison.InvariantCultureIgnoreCase));
            Assert.AreEqual(tensor.Shape.Count, 2);
            Assert.AreEqual(tensor.Shape[0], _modifiedSequences.Length);
            Assert.AreEqual(tensor.Shape[1], 1);
            AssertEx.AreEqualDeep(_modifiedSequences.Select(i => new ModifiedSequence(i, MassType.Monoisotopic).MonoisotopicMasses).ToArray(),
                DecodedSequences(pr));
        }

        public void AssertMatchesSpectra(SpectrumDisplayInfo[] spectrumDisplayInfos)
        {
            var iRTIndex = 0;
            foreach (var info in spectrumDisplayInfos.Where(i => i != null))
            {
                if (Equals(info.Precursor.LabelType, IsotopeLabelType.heavy))
                    --iRTIndex; // Reuse previous iRT, since we only made a single iRT prediction for heavy and light

                AssertMatchesSpectrum(_iRTs[iRTIndex++], info);
            }
        }

        public void AssertMatchesSpectrum(SpectrumDisplayInfo spectrumDisplayInfo)
        {
            AssertMatchesSpectrum(_iRTs[0], spectrumDisplayInfo);
        }

        public static void AssertMatchesSpectrum(float iRT, SpectrumDisplayInfo spectrumDisplayInfo)
        {
            var expected = iRT;

            Assert.AreEqual(expected, spectrumDisplayInfo.RetentionTime);
        }

        public override string Model => KoinaRetentionTimeModel.Models.First();

        public override ModelInferResponse Response
        {
            get
            {
                var pr = new ModelInferResponse();
                pr.ModelName = Model;

                // Construct Tensor
                var tp = new ModelInferResponse.Types.InferOutputTensor { Datatype = "FP32" };
                tp.Name = KoinaRetentionTimeModel.KoinaRTOutput.OUTPUT_KEY;
                //tp.Contents = new InferTensorContents();

                // Populate with data
                //tp.Contents.Fp32Contents.AddRange(_iRTs);
                tp.Shape.Add(_iRTs.Length);
                pr.Outputs.Add(tp);

                pr.RawOutputContents.Add(ByteString.CopyFrom(PrimitiveArrays.ToBytes(_iRTs)));

                return pr;
            }
        }
    }

    public struct IonTableEntry
    {
        public IonType ionType;
        public int ionNumber;
        public int chargeState;
        public float intensity;

        public IonTableEntry(IonType ionType, int ionNumber, int chargeState, float intensity)
        {
            this.ionType = ionType;
            this.ionNumber = ionNumber;
            this.chargeState = chargeState;
            this.intensity = intensity;
        }

        public static IEnumerable<IonTableEntry> EnumerateIonTable(IonTable<float[]> ionTable)
        {
            for (int i = 1; i < ionTable.GetLength(0); ++i)
            {
                var ionType = (IonType)(i - 1);
                for (int j = 1; j <= ionTable.GetLength(1); ++j)
                {
                    var intensities = ionTable.GetIonValue(ionType, j);
                    if (intensities == null)
                        continue;

                    for (int charge = 0; charge < intensities.Length; ++charge)
                    {
                        if (intensities[charge] != 0)
                            yield return new IonTableEntry(ionType, j, charge + 1, intensities[charge]);
                    }
                }
            }
        }
    }

    public class IonTableContractResolver : DefaultContractResolver
    {
        private readonly JsonConverter _converter;

        public IonTableContractResolver()
        {
            _converter = new SparseIonTableConverter();
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (member.Name == "_store" && member.DeclaringType == typeof(IonTable<float[]>))
            {
                property.Converter = _converter;
                property.Readable = true;
                property.Writable = true;
            }

            return property;
        }

        protected override IList<JsonProperty> CreateProperties(Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);

            if (memberSerialization == MemberSerialization.Fields)
                return properties;

            // Add _store field manually
            var privateFields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance);

            foreach (var field in privateFields)
            {
                if (field.Name == "_store")
                    properties.Add(CreateProperty(field, memberSerialization));
            }

            return properties;
        }
    }

    public class SparseIonTableConverter : JsonConverter<float[,][]>
    {
        public override void WriteJson(JsonWriter writer, float[,][] value, JsonSerializer serializer)
        {
            var table = value;
            var ionTypes = new List<IonType>();
            var ionNumbers = new List<int>();
            var charges = new List<int>();
            var intensities = new List<float>();

            int maxCharge = 0;
            for (int i = 0; i < table.GetLength(0); ++i)
                for (int j = 0; j < table.GetLength(1); ++j)
                    maxCharge = Math.Max(maxCharge, table[i, j]?.Length ?? 0);

            for (int row = 0; row < table.GetLength(0); row++)
            {
                for (int col = 0; col < table.GetLength(1); col++)
                {
                    var intensityByCharge = table[row, col];
                    if (intensityByCharge == null && row + 1 < table.GetLength(0))
                        continue;

                    for (int charge = 0; charge < maxCharge; charge++)
                    {
                        //if (intensityByCharge[charge] <= 0)
                         //   continue; // Only serialize non-default values

                        IonType ionType = (IonType) row - 1;
                        int ionNumber = ionType.IsNTerminal() ? col + 1 : table.GetLength(1) - col;

                        ionTypes.Add(ionType);
                        ionNumbers.Add(ionNumber);
                        charges.Add(charge + 1);
                        intensities.Add(intensityByCharge?[charge] ?? -1);
                    }
                }
            }

            var sparseRepresentation = new SparseRepresentation
            {
                IonType = ionTypes,
                IonNumber = ionNumbers,
                Charge = charges,
                Intensity = intensities
            };

            var formatting = serializer.Formatting;
            writer.Formatting = Formatting.None;
            string newlineAndIndent = "\n" + new string(' ', 17);

            // Manually write each array in order to write them compactly (on one line) but put newlines between arrays
            writer.WriteStartObject();
            writer.WritePropertyName("IonType");
            writer.WriteStartArray();
            foreach (var ionType in sparseRepresentation.IonType)
                serializer.Serialize(writer, ionType.ToString());
            writer.WriteEndArray();
            writer.WriteWhitespace(newlineAndIndent);

            writer.WritePropertyName("IonNumber");
            writer.WriteStartArray();
            foreach (var ionNumber in sparseRepresentation.IonNumber)
                serializer.Serialize(writer, ionNumber);
            writer.WriteEndArray();
            writer.WriteWhitespace(newlineAndIndent);

            writer.WritePropertyName("Charge");
            writer.WriteStartArray();
            foreach (var charge in sparseRepresentation.Charge)
                serializer.Serialize(writer, charge);
            writer.WriteEndArray();
            writer.WriteWhitespace(newlineAndIndent);

            writer.WritePropertyName("Intensity");
            writer.WriteStartArray();
            foreach (var intensity in sparseRepresentation.Intensity)
                serializer.Serialize(writer, intensity);
            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.Formatting = formatting;
        }

        public override float[,][] ReadJson(JsonReader reader, Type objectType, float[,][] existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var sparseRepresentation = serializer.Deserialize<SparseRepresentation>(reader);
            if (sparseRepresentation == null)
                return null;

            // Determine dimensions
            int maxIonType = 0, maxIonNumber = 0, maxCharge = 0;
            for (int i = 0; i < sparseRepresentation.IonType.Count; i++)
            {
                if ((int) sparseRepresentation.IonType[i] + 1 > maxIonType) maxIonType = (int) sparseRepresentation.IonType[i] + 1;
                if (sparseRepresentation.IonNumber[i] > maxIonNumber) maxIonNumber = sparseRepresentation.IonNumber[i] + 1;
                if (sparseRepresentation.Charge[i] > maxCharge) maxCharge = sparseRepresentation.Charge[i];
            }

            var result = new float[maxIonType + 1, maxIonNumber + 1][];

            // Populate the result array
            for (int i = 0; i < sparseRepresentation.IonType.Count; i++)
            {
                IonType ionType = sparseRepresentation.IonType[i];
                int ionNumber = sparseRepresentation.IonNumber[i];

                int row = (int) ionType + 1;
                int col = ionType.IsNTerminal() ? ionNumber - 1 : maxIonNumber - ionNumber + 1;
                int charge = sparseRepresentation.Charge[i] - 1;
                float intensity = sparseRepresentation.Intensity[i];

                if (result[row, col] == null)
                    result[row, col] = new float[maxCharge];
                result[row, col][charge] = intensity;
            }

            return result;
        }

        private class SparseRepresentation
        {
            [JsonProperty(ItemConverterType = typeof(StringEnumConverter))]
            public List<IonType> IonType { get; set; }
            public List<int> IonNumber { get; set; }
            public List<int> Charge { get; set; }
            public List<float> Intensity { get; set; }
        }
    }
}