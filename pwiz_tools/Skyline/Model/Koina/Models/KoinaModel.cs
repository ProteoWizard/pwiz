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
using System.Threading;
using Google.Protobuf.Collections;
using Grpc.Core;
using Inference;
using pwiz.Common.Chemistry;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Koina.Communication;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using static Inference.ModelInferRequest.Types;
using Settings = pwiz.Skyline.Properties.Settings;

namespace pwiz.Skyline.Model.Koina.Models
{
    /// <summary>
    /// An abstract class representing a Koina tensor flow model
    /// such as the intensity or RT model. It supports simple prediction
    /// and batch construction/prediction. The various type parameters
    /// are necessary due to the several layers of data representation.
    /// 
    /// For the input layer, we convert:
    /// IList{TSkylineInputRow} -> TKoinaIn{TKoinaInputRow} -> MapField{string}{TensorProto}
    /// 
    /// For the output layer, we convert:
    /// MapField{string}{TensorProto} -> TKoinaOut{TKoinaOut, TKoinaOutputRow} -> TSkylineOutput
    /// </summary>
    /// 
    /// <typeparam name="TKoinaInputRow">A single input for Koina, for instance a precursor</typeparam>
    /// <typeparam name="TKoinaIn">The entire input for Koina, such as a list of precursors</typeparam>
    /// <typeparam name="TSkylineInputRow">A single input for Koina in Skyline data structures, such as a TransitionGroupDocNode</typeparam>
    /// <typeparam name="TKoinaOutputRow">A single output of Koina, such as list of fragment intensities</typeparam>
    /// <typeparam name="TKoinaOut">The entire output of Koina, such as a list of fragment intensities for all requested peptides</typeparam>
    /// <typeparam name="TSkylineOutput">The entire output of Koina in Skyline  friendly data structures</typeparam>

    public abstract class KoinaModel<TKoinaInputRow, TKoinaIn, TSkylineInputRow, TKoinaOutputRow, TKoinaOut, TSkylineOutput>
        where TSkylineInputRow : SkylineInputRow
        where TKoinaIn : KoinaInput<TKoinaInputRow>
        where TKoinaOut : KoinaOutput<TKoinaOut, TKoinaOutputRow>, new()
    {
        /// <summary>
        /// A signature that is required by TensorFlow, currently v1
        /// </summary>
        public abstract string Signature { get; }

        /// <summary>
        /// Name of the model, for example intensity or iRT
        /// </summary>
        public abstract string Model { get; protected set; }


        public class ModelInputs
        {
            public ModelInputs(bool peptideSequences, bool precursorCharges, bool collisionEnergies)
            {
                PeptideSequences = peptideSequences;
                PrecursorCharges = precursorCharges;
                CollisionEnergies = collisionEnergies;
            }

            public bool PeptideSequences { get; }
            public bool PrecursorCharges { get; }
            public bool CollisionEnergies { get; }

            public IEnumerable<string> TensorKeys
            {
                get
                {
                    var result = new List<string>();
                    if (PeptideSequences)
                        result.Add(KoinaIntensityModel.KoinaIntensityInput.PEPTIDES_KEY);
                    if (PrecursorCharges)
                        result.Add(KoinaIntensityModel.KoinaIntensityInput.PRECURSOR_CHARGE_KEY);
                    if (CollisionEnergies)
                        result.Add(KoinaIntensityModel.KoinaIntensityInput.COLLISION_ENERGY_KEY);
                    return result;
                }
            }
        }

        public abstract IDictionary<string, ModelInputs> InputsForModel { get; }

        /// <summary>
        /// Construct Koina input given Skyline input. Note that
        /// this function does not throw any exceptions but uses the exception out
        /// parameter to speed up constructing large amounts of inputs.
        /// </summary>
        /// <param name="settings">Settings to use for construction</param>
        /// <param name="skylineInput">The input at the Skyline level (for example docnodes)</param>
        /// <param name="exception">Exception that occured during the creating Process</param>
        /// <returns>The input at the Koina level</returns>
        public abstract TKoinaInputRow CreateKoinaInputRow(SrmSettings settings, TSkylineInputRow skylineInput, out KoinaException exception);

        /// <summary>
        /// Constructs the final input (tensors) that is sent to Koina, given
        /// Koina inputs.
        /// </summary>
        /// <param name="koinaInputRows">The koina inputs to use</param>
        /// <returns>A Koina input object that can be directly sent to Koina</returns>
        public abstract TKoinaIn CreateKoinaInput(IList<TKoinaInputRow> koinaInputRows);

        /// <summary>
        /// Converts a map of tensors to an easier to work with data structure,
        /// still at the Koina level.
        /// </summary>
        /// <param name="koinaOutputData">The data from the prediction</param>
        /// <returns>A Koina output object containing the parsed information from the tensors</returns>
        public abstract TKoinaOut CreateKoinaOutput(ModelInferResponse koinaOutputData);

        /// <summary>
        /// Constructs Skyline level outputs given Koina outputs
        /// </summary>
        /// <param name="settings">Settings to use for construction</param>
        /// <param name="skylineInputs">The original skyline inputs used for the prediction. Should
        /// exclude items that could not be predicted</param>
        /// <param name="koinaOutput">The koina output from the prediction</param>
        /// <returns>A skyline level object that can be used in Skyline, for instance for display in the UI</returns>
        public abstract TSkylineOutput CreateSkylineOutput(SrmSettings settings, IList<TSkylineInputRow> skylineInputs, TKoinaOut koinaOutput);

        /// <summary>
        /// Helper function for creating input tensors.
        /// type should match T.
        /// </summary>
        public static InferInputTensor Create2dTensor<T>(string name, string type, Func<InferTensorContents, RepeatedField<T>> getVal,
            ICollection<T> inputs, params long[] dimensions)
        {
            // Construct Tensor
            var tp = new InferInputTensor { Datatype = type };
            tp.Name = name;
            tp.Contents = new InferTensorContents();

            // Populate with data
            getVal(tp.Contents).AddRange(inputs);
            tp.Shape.AddRange(dimensions);
            Assume.AreEqual(dimensions.Aggregate(1L, (a, b) => a * b), (long) inputs.Count);

            return tp;
        }

        /// <summary>
        /// Single threaded Koina prediction for several inputs
        /// </summary>
        /// <param name="predictionClient">Client to use for prediction</param>
        /// <param name="settings">Settings to use for constructing </param>
        /// <param name="inputs">The precursors (and other info) to make predictions for</param>
        /// <param name="token">Token for cancelling prediction</param>
        /// <returns>Predictions from Koina</returns>
        public TSkylineOutput Predict(GRPCInferenceService.GRPCInferenceServiceClient predictionClient,
            SrmSettings settings, IList<TSkylineInputRow> inputs, CancellationToken token)
        {
            inputs = inputs.Distinct().ToArray();

            var validSkylineInputs = new List<TSkylineInputRow>(inputs.Count);
            var koinaInputs = new List<TKoinaInputRow>(inputs.Count);

            foreach (var singleInput in inputs)
            {
                var input = CreateKoinaInputRow(settings, singleInput, out _);
                if (input != null)
                {
                    koinaInputs.Add(input);
                    validSkylineInputs.Add(singleInput);
                }
            }

            var koinaIn = CreateKoinaInput(koinaInputs);
            var prediction = Predict(predictionClient, koinaIn, token);
            return CreateSkylineOutput(settings, validSkylineInputs, prediction);
        }

        // Variables for remembering the previous prediction request and its outcome.
        // Uses lock since the KoinaModel classes are intended to be used as singletons
        private readonly object _cacheLock = new object();
        private GRPCInferenceService.GRPCInferenceServiceClient _cachedClient;
        private SrmSettings _cachedSettings;
        private TSkylineInputRow _cachedInput;
        private TSkylineOutput _cachedOutput;

        /// <summary>
        /// Makes prediction for a single precursor. The result is cached and
        /// if the same prediction is requested, the cached result is returned. This
        /// is useful since predictions are made from a ui updating function, which might be
        /// called repeatedly per update.
        /// </summary>
        /// <param name="predictionClient">Client to use for prediction</param>
        /// <param name="settings">Settings to use for creating inputs and outputs</param>
        /// <param name="input">Precursor and other information</param>
        /// <param name="token">Token for cancelling prediction</param>
        /// <returns>Prediction from Koina</returns>
        public TSkylineOutput PredictSingle(GRPCInferenceService.GRPCInferenceServiceClient predictionClient,
            SrmSettings settings, TSkylineInputRow input, CancellationToken token)
        {
            lock (_cacheLock)
            {
                if (KoinaConstants.CACHE_PREV_PREDICTION && _cachedInput != null && _cachedOutput != null &&
                    _cachedInput.Equals(input) && ReferenceEquals(_cachedClient, predictionClient) &&
                    ReferenceEquals(settings, _cachedSettings))
                    return _cachedOutput;
            }

            var koinaInputRow = CreateKoinaInputRow(settings, input, out var exception);
            if (koinaInputRow == null)
                throw exception;

            var koinaIn = CreateKoinaInput(new[] { koinaInputRow });
            var prediction = Predict(predictionClient, koinaIn, token);
            var output = CreateSkylineOutput(settings, new[] { input }, prediction);

            lock (_cacheLock)
            {
                _cachedClient = predictionClient;
                _cachedSettings = settings;
                _cachedInput = input;
                _cachedOutput = output;
            }

            return output;
        }

        /// <summary>
        /// Private version of Predict that works with data structures at
        /// the Koina level
        /// </summary>
        /// <param name="predictionClient">Client to use for prediction</param>
        /// <param name="inputData">Input data, consisting tensors to send for prediction</param>
        /// <param name="token">Token for cancelling prediction</param>
        /// <returns>Predicted tensors from Koina</returns>
        private TKoinaOut Predict(GRPCInferenceService.GRPCInferenceServiceClient predictionClient, TKoinaIn inputData, CancellationToken token)
        {
            var predictRequest = new ModelInferRequest();
            predictRequest.ModelName = Model;

            try {
                // Copy input
                if (InputsForModel != null && InputsForModel.ContainsKey(Model))
                {
                    var tensorKeys = InputsForModel[Model].TensorKeys;
                    predictRequest.Inputs.AddRange(inputData.KoinaTensors.Where(t => tensorKeys.Contains(t.Name)));
                }
                else
                {
                    predictRequest.Inputs.AddRange(inputData.KoinaTensors);
                }
                predictRequest.Outputs.AddRange(inputData.OutputTensorNames.Select(n => new InferRequestedOutputTensor { Name = n }));

                // Make prediction
                var predictResponse = predictionClient.ModelInfer(predictRequest, cancellationToken: token);
                return CreateKoinaOutput(predictResponse);
            }
            catch (RpcException ex) {
                throw new KoinaException(ex.Message, ex);
            }
        }

        /// <summary>
        /// Constructs batches and makes predictions in parallel
        /// </summary>
        /// <param name="predictionClient">Client to use for prediction</param>
        /// <param name="progressMonitor">Monitor to show progress in UI</param>
        /// <param name="progressStatus"/>
        /// <param name="settings">Settings to use for constructing inputs and outputs</param>
        /// <param name="inputs">List of inputs to predict</param>
        /// <param name="token">Token for cancelling prediction</param>
        /// <returns>Predictions from Koina</returns>
        public TSkylineOutput PredictBatches(GRPCInferenceService.GRPCInferenceServiceClient predictionClient,
            IProgressMonitor progressMonitor, ref IProgressStatus progressStatus, SrmSettings settings, IList<TSkylineInputRow> inputs, CancellationToken token)
        {
            const int CONSTRUCTING_INPUTS_FRACTION = 50;
            progressMonitor.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(KoinaResources.KoinaModel_BatchPredict_Constructing_Koina_inputs)
                .ChangePercentComplete(0));
              
            inputs = inputs.Distinct().ToArray();

            var processed = 0;
            var totalCount = inputs.Count;

            var inputLock = new object();
            var inputsList =
                new List<TKoinaInputRow>();
            var validInputsList =
                new List<TSkylineInputRow>();

            // Construct batch inputs in parallel
            var localProgressStatus = progressStatus;
            ParallelEx.ForEach(KoinaHelpers.EnumerateBatches(inputs, KoinaConstants.BATCH_SIZE),
                batchEnumerable =>
                {
                    var batch = batchEnumerable.ToArray();

                    var batchInputs = new List<TKoinaInputRow>(batch.Length);
                    var validSkylineInputs = new List<TSkylineInputRow>(batch.Length);

                    foreach (var singleInput in batch)
                    {
                        var input = CreateKoinaInputRow(settings, singleInput, out _);
                        if (input != null)
                        {
                            batchInputs.Add(input);
                            validSkylineInputs.Add(singleInput);
                        }
                    }

                    lock (inputLock)
                    {
                        inputsList.AddRange(batchInputs);
                        validInputsList.AddRange(validSkylineInputs);

                        // ReSharper disable AccessToModifiedClosure
                        processed += batch.Length;
                        progressMonitor.UpdateProgress(localProgressStatus.ChangePercentComplete(CONSTRUCTING_INPUTS_FRACTION * processed / totalCount));
                        // ReSharper restore AccessToModifiedClosure
                    }
                });

            processed = 0;
            totalCount = inputsList.Count;

            // Make predictions batch by batch in sequence and merge the outputs
            var koinaOutputAll = PredictBatches(predictionClient, progressMonitor, ref progressStatus, settings, inputsList, token);

            return CreateSkylineOutput(settings, validInputsList.Select(i => i).ToArray(), koinaOutputAll);
        }

        /// <summary>
        /// Constructs batches and makes predictions in parallel
        /// </summary>
        /// <param name="predictionClient">Client to use for prediction</param>
        /// <param name="progressMonitor">Monitor to show progress in UI</param>
        /// <param name="progressStatus"/>
        /// <param name="settings">Settings to use for constructing inputs and outputs</param>
        /// <param name="inputs">List of inputs to predict</param>
        /// <param name="token">Token for cancelling prediction</param>
        /// <returns>Predictions from Koina</returns>
        public TKoinaOut PredictBatches(GRPCInferenceService.GRPCInferenceServiceClient predictionClient,
            IProgressMonitor progressMonitor, ref IProgressStatus progressStatus, SrmSettings settings, List<TKoinaInputRow> inputs, CancellationToken token)
        {
            var processed = 0;
            var totalCount = inputs.Count;

            if (totalCount == 0)
                return new TKoinaOut();

            var inputLock = new object();
            var batches = KoinaHelpers.EnumerateBatches(inputs, KoinaConstants.BATCH_SIZE).ToList();
            var inputsList = new List<TKoinaIn>();
            for(int i=0; i < batches.Count; ++i)
                inputsList.Add(null);

            // Construct batch inputs in parallel
            var localProgressStatus = progressStatus;
            ParallelEx.For(0, inputsList.Count,
                batchIndex =>
                {
                    var batch = batches[batchIndex].ToArray();
                    
                    lock (inputLock)
                    {
                        inputsList[batchIndex] = CreateKoinaInput(batch);

                        // ReSharper disable AccessToModifiedClosure
                        processed += batch.Length;
                        progressMonitor.UpdateProgress(localProgressStatus);
                        // ReSharper restore AccessToModifiedClosure
                    }
                }, maxThreads: KoinaConstants.MAX_THREADS);

            if (progressMonitor.UpdateProgress(localProgressStatus) == UpdateProgressResponse.cancel)
                return null;

            processed = 0;
            totalCount = inputsList.Sum(pi => pi.InputRows.Count);

            const int REQUESTING_INPUTS_FRACTION = 100;
            progressStatus = progressStatus
                .ChangeMessage(KoinaResources.KoinaModel_BatchPredict_Requesting_predictions_from_Koina)
                .ChangePercentComplete(0);
            if (progressMonitor.UpdateProgress(progressStatus) == UpdateProgressResponse.cancel)
                return null;

            // Make predictions batch by batch in sequence and merge the outputs
            var koinaOutput = new List<TKoinaOut>();
            for (int i = 0; i < inputsList.Count; ++i)
                koinaOutput.Add(null);
            int retryCount = Settings.Default.KoinaRetryCount;
            ParallelEx.For(0, inputsList.Count, batchIndex =>
            {
                var koinaIn = inputsList[batchIndex];

                if (progressMonitor.UpdateProgress(localProgressStatus) == UpdateProgressResponse.cancel)
                    return;
                var koinaExceptions = new List<KoinaException>();
                for (int attempt = 0;; attempt++)
                {
                    try
                    {
                        koinaOutput[batchIndex] = Predict(predictionClient, koinaIn, token);
                        break;
                    }
                    catch (KoinaException koinaException)
                    {
                        koinaExceptions.Add(koinaException);
                        if (attempt >= retryCount)
                        {
                            throw new AggregateException(koinaExceptions);
                        }
                        lock (progressMonitor)
                        {
                            string message = string.Format(
                                KoinaResources.KoinaModel_PredictBatches_Error___0__retrying__1_____2__,
                                koinaException.Message,
                                attempt + 1, retryCount);
                            localProgressStatus = localProgressStatus.ChangeMessage(message);
                            progressMonitor.UpdateProgress(localProgressStatus);
                        }
                    }
                }

                lock(progressMonitor)
                {
                    processed += koinaIn.InputRows.Count;
                    localProgressStatus = localProgressStatus.ChangeMessage(TextUtil.SpaceSeparate(
                            KoinaResources.KoinaModel_BatchPredict_Requesting_predictions_from_Koina,
                            processed.ToString(), @"/", totalCount.ToString()))
                        .ChangePercentComplete(REQUESTING_INPUTS_FRACTION * processed / totalCount);
                    progressMonitor.UpdateProgress(localProgressStatus);
                }
            }, maxThreads: KoinaConstants.MAX_THREADS);

            var koinaOutputAll = koinaOutput[0];
            for (int i = 1; i < koinaOutput.Count; ++i)
                koinaOutputAll = koinaOutputAll.MergeOutputs(koinaOutput[i]);

            return koinaOutputAll;
        }
    }

    public static class KoinaHelpers
    {
        public static KoinaMS2Spectra PredictBatchesFromKoinaCsv(string koinaCsvFilePath, IProgressMonitor progressMonitor,
            ref IProgressStatus progressStatus, CancellationToken token)
        {
            progressStatus = progressStatus.ChangeMessage(ModelsResources.KoinaHelpers_PredictBatchesFromKoinaCsv_Reading_Koina_CSV_input);

            var inputRows = new List<KoinaIntensityModel.KoinaIntensityInput.KoinaPrecursorInput>();
            var peptides = new List<KoinaIntensityModel.PeptidePrecursorNCE>();
            var calc = new SequenceMassCalc(MassType.Monoisotopic);
            var defaultSettings = SrmSettingsList.GetDefault();

            using var csvReader = new StreamReader(koinaCsvFilePath);
            csvReader.ReadLine(); // skip header
            while (csvReader.ReadLine() is { } row)
            {
                var values = row.Split(',');
                string sequence = values[0];
                var ce = Convert.ToSingle(values[1], CultureInfo.InvariantCulture);
                var charge = Convert.ToInt32(values[2], CultureInfo.InvariantCulture);
                inputRows.Add(KoinaIntensityModel.CreateKoinaInputRow(sequence, charge, ce, out _));
                peptides.Add(new KoinaIntensityModel.PeptidePrecursorNCE(sequence, charge, new SignedMz(calc.GetPrecursorMass(sequence) / charge),
                    ExplicitMods.EMPTY, IsotopeLabelType.light, (int)ce));
            }
            progressStatus = progressStatus.NextSegment();

            var koinaOutput = KoinaIntensityModel.Instance.PredictBatches(KoinaPredictionClient.Current,
                progressMonitor, ref progressStatus, defaultSettings, inputRows, token);
            progressStatus = progressStatus.NextSegment();

            var ms2 = new KoinaMS2Spectra(SrmSettingsList.GetDefault(), peptides, koinaOutput);

            // Predict iRTs for peptides
            var modifiedSequenceToDistinctPepIndex = new Dictionary<string, int>();
            var distinctPeps = new List<KoinaRetentionTimeModel.KoinaRTInput.KoinaPeptideInput>();
            var distinctPepIndexToIntensityPepIndex = new MultiMap<int, int>();
            for (var i = 0; i < peptides.Count; i++)
            {
                var p = peptides[i];
                if (modifiedSequenceToDistinctPepIndex.TryGetValue(p.Sequence, out int distinctPepIndex))
                {
                    distinctPepIndexToIntensityPepIndex.Add(distinctPepIndex, i);
                }
                else
                {
                    modifiedSequenceToDistinctPepIndex.Add(p.Sequence, distinctPeps.Count);
                    distinctPepIndexToIntensityPepIndex.Add(distinctPeps.Count, i);
                    distinctPeps.Add(KoinaRetentionTimeModel.CreateKoinaInputRow(defaultSettings, p.Sequence, out _));
                }
            }

            var irtOutput = KoinaRetentionTimeModel.Instance.PredictBatches(KoinaPredictionClient.Current,
                progressMonitor, ref progressStatus, defaultSettings, distinctPeps, token);
            //progressStatus = progressStatus.NextSegment();
            var iRTMap = new Dictionary<KoinaIntensityModel.PeptidePrecursorNCE, double>();
            for(int i = 0; i < distinctPeps.Count; ++i)
                foreach (var pepIndex in distinctPepIndexToIntensityPepIndex[i])
                    iRTMap[peptides[pepIndex]] = irtOutput.OutputRows[i].iRT;

            for (var i = 0; i < peptides.Count; ++i)
            {
                if (iRTMap.TryGetValue(ms2.Spectra[i].PeptidePrecursorNCE, out var iRT))
                    ms2.Spectra[i].SpecMzInfo.RetentionTime = iRT;
                ms2.Spectra[i].SpecMzInfo.SourceFile = koinaCsvFilePath;
            }

            return ms2;
        }

        public static void ExportKoinaSpectraToBlib(KoinaMS2Spectra spectra, string encyclopediaBlibFilePath, IProgressMonitor progressMonitor,
            ref IProgressStatus progressStatus)
        {
            progressStatus = progressStatus.ChangeMessage(ModelsResources.KoinaHelpers_ExportKoinaSpectraToBlib_Exporting_Koina_spectra_to_BiblioSpec_library);
            string libraryName = Path.GetFileName(encyclopediaBlibFilePath);

            progressMonitor.UpdateProgress(progressStatus);

            // Delete if already exists, no merging with Koina
            var libraryExists = File.Exists(encyclopediaBlibFilePath);
            if (libraryExists)
                FileEx.SafeDelete(encyclopediaBlibFilePath);

            // Build the library
            using (var blibDb = BlibDb.CreateBlibDb(encyclopediaBlibFilePath))
            {
                var docLibrarySpec = new BiblioSpecLiteSpec(libraryName, encyclopediaBlibFilePath);
                var docLibrarySpec2 = docLibrarySpec;

                var mzSpecInfo = spectra.Spectra.Select(s => s.SpecMzInfo);
                var docLibraryNew = blibDb.CreateLibraryFromSpectra(docLibrarySpec2, mzSpecInfo.ToList(), libraryName, progressMonitor, ref progressStatus);
                if (docLibraryNew == null)
                    throw new InvalidOperationException(ModelsResources.KoinaHelpers_ExportKoinaSpectraToBlib_failed_to_write_Koina_output_to_blib);
            }
        }

        public class KoinaRequest
        {
            protected CancellationTokenSource _tokenSource = new CancellationTokenSource();
            protected Action _updateCallback;

            public KoinaRequest(KoinaPredictionClient client, KoinaIntensityModel intensityModel,
                KoinaRetentionTimeModel rtModel, SrmSettings settings,
                PeptideDocNode peptide, TransitionGroupDocNode precursor, IsotopeLabelType labelType,
                int nce, Action updateCallback)
            {
                Client = client;
                IntensityModel = intensityModel;
                RTModel = rtModel;
                Settings = settings;
                Precursor = precursor;
                LabelType = labelType;
                Peptide = peptide;
                NCE = nce;
                _updateCallback = updateCallback;
            }

            public KoinaRequest(SrmSettings settings, PeptideDocNode peptide, TransitionGroupDocNode precursor,
                IsotopeLabelType labelType, int nce, Action updateCallback) :
                this(KoinaPredictionClient.Current, KoinaIntensityModel.Instance, KoinaRetentionTimeModel.Instance,
                    settings, peptide, precursor, labelType, nce, updateCallback)
            {
            }

            public virtual KoinaRequest Predict()
            {
                ActionUtil.RunAsync(() =>
                {
                    try
                    {
                        var labelType = LabelType ?? Precursor.LabelType;
                        var skyIn = new KoinaIntensityModel.PeptidePrecursorNCE(Peptide,
                            Precursor, labelType, NCE);
                        var massSpectrum = IntensityModel.PredictSingle(Client,
                            Settings, skyIn,
                            _tokenSource.Token);
                        var iRT = RTModel.PredictSingle(Client,
                            Settings,
                            Peptide, _tokenSource.Token);
                        Spectrum = new SpectrumDisplayInfo(
                            new SpectrumInfoKoina(massSpectrum, Precursor, labelType, NCE),
                            // ReSharper disable once AssignNullToNotNullAttribute
                            Precursor,
                            iRT[Peptide]);
                    }
                    catch (KoinaException ex)
                    {
                        Exception = ex;

                        // Ignore, UpdateUI is already working on a new request,
                        // so don't even update UI
                        if (ex.InnerException is RpcException rpcEx && rpcEx.StatusCode == StatusCode.Cancelled)
                            return;
                    }

                    _updateCallback.Invoke();
                });
                return this;
            }

            public void Cancel()
            {
                _tokenSource.Cancel();
            }

            // Output variables
            public SpectrumDisplayInfo Spectrum { get; protected set; }
    
            public Exception Exception { get; protected set; }

            public KoinaPredictionClient Client { get; protected set; }
            public KoinaIntensityModel IntensityModel { get; protected set; }
            public KoinaRetentionTimeModel RTModel { get; protected set; }
            public SrmSettings Settings { get; protected set; }
            public PeptideDocNode Peptide { get; protected set; }
            public TransitionGroupDocNode Precursor { get; protected set; }
            public IsotopeLabelType LabelType { get; protected set; }
            public int NCE { get; protected set; }

            protected bool Equals(KoinaRequest other)
            {
                return Client.Server == other.Client.Server && ReferenceEquals(IntensityModel, other.IntensityModel) &&
                       ReferenceEquals(RTModel, other.RTModel) && ReferenceEquals(Settings, other.Settings) &&
                       Equals(Peptide, other.Peptide) && Equals(Precursor, other.Precursor) &&
                       ReferenceEquals(LabelType, other.LabelType) && NCE == other.NCE;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((KoinaRequest)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = (Client != null ? Client.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (IntensityModel != null ? IntensityModel.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (RTModel != null ? RTModel.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Settings != null ? Settings.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Peptide != null ? Peptide.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (Precursor != null ? Precursor.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (LabelType != null ? LabelType.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ NCE;
                    return hashCode;
                }
            }
        }

        public static bool KoinaSettingsValid
        {
            get
            {
                return !string.IsNullOrEmpty(Settings.Default.KoinaIntensityModel) &&
                       !string.IsNullOrEmpty(Settings.Default.KoinaRetentionTimeModel);
            }
        }

        /// <summary>
        /// Calculates the normalized contrast angle of the square rooted
        /// intensities of the two given spectra.
        /// </summary>
        /// <param name="spectrum1">First spectrum</param>
        /// <param name="spectrum2">Second spectrum</param>
        /// <param name="mzTolerance">Tolerance for considering two mzs the same</param>
        public static double CalculateSpectrumDotpMzMatch(LibraryRankedSpectrumInfo spectrum1, LibraryRankedSpectrumInfo spectrum2, MzTolerance mzTolerance)
        {
            var matched1 = spectrum1.PeaksMatched.ToArray();
            var matched2 = spectrum2.PeaksMatched.ToArray();
            var intensities1All = new List<double>(matched1.Length + matched2.Length);
            var intensities2All = new List<double>(matched1.Length + matched2.Length);
            var matchIndex1 = 0;
            var matchIndex2 = 0;
            while (matchIndex1 < matched1.Length && matchIndex2 < matched2.Length)
            {
                var mz1 = matched1[matchIndex1].ObservedMz;
                var mz2 = matched2[matchIndex2].ObservedMz;
                if (mzTolerance.IsWithinTolerance(mz1, mz2))
                {
                    intensities1All.Add(matched1[matchIndex1].Intensity);
                    intensities2All.Add(matched2[matchIndex2].Intensity);

                    ++matchIndex1;
                    ++matchIndex2;
                }
                else if (mz1 < mz2)
                {
                    intensities1All.Add(matched1[matchIndex1].Intensity);
                    intensities2All.Add(0.0);
                    ++matchIndex1;
                }
                else
                {
                    intensities1All.Add(0.0);
                    intensities2All.Add(matched2[matchIndex2].Intensity);
                    ++matchIndex2;
                }
            }

            return new Statistics(intensities1All).NormalizedContrastAngleSqrt(
                new Statistics(intensities2All));
        }

        /*public static double CalculateSpectrumDotpIonMatch(LibraryRankedSpectrumInfo spectrum1,
            LibraryRankedSpectrumInfo spectrum2)
        {
            var matched1 = spectrum1.PeaksMatched.ToArray();
            var matched2 = spectrum2.PeaksMatched.ToArray();
            var intensities1All = new List<double>(matched1.Length + matched2.Length);
            var intensities2All = new List<double>(matched1.Length + matched2.Length);

            foreach (var match1 in matched1)
            {
                var other = matched2.Where(m => m.MatchedIons.Intersect())
            }
        }*/

        /// <summary>
        /// ReLU activation for spectra
        /// </summary>
        /// <param name="f">float to apply ReLU to</param>
        /// <returns>If f is positive, returns f, otherwise 0</returns>
        public static float ReLU(float f)
        {
            return Math.Max(f, 0.0f);
        }

        /// <summary>
        /// Applys ReLU to array
        /// </summary>
        /// <param name="f">Array to apply ReLU to</param>
        /// <returns>Array with each element transformed according to ReLU activation</returns>
        public static float[] ReLU(float[] f)
        {
            return f.Select(ReLU).ToArray();
        }

        /// <summary>
        /// Split data into batches for parallel processing
        /// </summary>
        /// <typeparam name="T">Underlying data type</typeparam>
        /// <param name="data">The data to split</param>
        /// <param name="batchSize">Size of a single batch, the last batch might be smaller</param>
        /// <returns>An enumerable that enumerates the batches</returns>
        public static IEnumerable<IEnumerable<T>> EnumerateBatches<T>(IList<T> data, int batchSize)
        {
            var dataIndex = 0;
            while (dataIndex < data.Count)
            {
                var size = Math.Min(data.Count - dataIndex, batchSize);
                yield return Enumerable.Range(dataIndex, size).Select(i => data[i]);
                dataIndex += size;
            }
        }

        private const string UNIMOD_FORMAT = "[UNIMOD:{0}]";

        /// <summary>
        /// Sequences are passed to Koina as AA strings (with modifications). Actually throwing exceptions in this method
        /// slows down constructing inputs (for larger data sets with unknown mods (and aa's)significantly,
        /// which is why KoinaExceptions (only) are set as an output parameter and null is returned.
        /// </summary>
        public static string EncodeSequence(SrmSettings settings, PeptideDocNode peptide, IsotopeLabelType label, out KoinaException exception)
        {
            if (!peptide.Target.IsProteomic)
                throw new KoinaSmallMoleculeException(peptide.ModifiedTarget);

            var sequence = peptide.Target.Sequence;
            if (sequence.Length > KoinaConstants.PEPTIDE_SEQ_LEN) {
                exception = new KoinaPeptideTooLongException(peptide.ModifiedTarget);
                return null;
            }

            if (true == peptide.ExplicitMods?.HasCrosslinks)
            {
                var crosslink = peptide.ExplicitMods.CrosslinkStructure.Crosslinks.First(
                    c => c.Sites.Any(site => site.AaIndex == 0));
                var siteOnFirstPeptide = crosslink.Sites.First(site => site.AaIndex == 0);
                exception = new KoinaUnsupportedModificationException(peptide.Target, crosslink.Crosslinker, siteOnFirstPeptide.AaIndex);
                return null;
            }

            var modifiedSequence = ModifiedSequence.GetModifiedSequence(settings, peptide.Target.Sequence, peptide.ExplicitMods, label);
            var result = new StringBuilder(KoinaConstants.PEPTIDE_SEQ_LEN);

            for (var i = 0; i < sequence.Length; ++i) {
                if (!KoinaConstants.AMINO_ACIDS.TryGetValue(sequence[i], out _)) {
                    exception = new KoinaUnsupportedAminoAcidException(peptide.ModifiedTarget, i);
                    return null;
                }

                result.Append(sequence[i]);

                var mods = modifiedSequence.ExplicitMods.Where(m => m.IndexAA == i).ToArray();
                foreach (var mod in mods)
                {
                    if (mod.MonoisotopicMass == 0.0)
                        continue;

                    var staticMod = UniMod.FindMatchingStaticMod(mod.StaticMod, true) ?? mod.StaticMod;
                    if (!KoinaConstants.MODIFICATIONS.TryGetValue(staticMod.Name, out var _))
                    {
                        exception = new KoinaUnsupportedModificationException(peptide.ModifiedTarget,
                            mod.StaticMod,
                            mod.IndexAA);
                        return null;
                    }

                    result.AppendFormat(UNIMOD_FORMAT, staticMod.UnimodId.Value);
                    break;
                }
            }

            exception = null;
            return result.ToString();
        }

        /// <summary>
        /// Sequences are passed to Koina as AA strings (with modifications). Actually throwing exceptions in this method
        /// slows down constructing inputs (for larger data sets with unknown mods (and aa's)significantly,
        /// which is why KoinaExceptions (only) are set as an output parameter and null is returned.
        /// </summary>
        public static string EncodeSequence(string sequence, out KoinaException exception)
        {
            if (sequence.Length > KoinaConstants.PEPTIDE_SEQ_LEN) {
                exception = new KoinaPeptideTooLongException(new Target(sequence));
                return null;
            }
            
            exception = null;
            return sequence;
        }

        /// <summary>
        /// Decodes "Koina-encoded" peptide sequences from a tensor. Only used in testing
        /// </summary>
        /// <param name="tensor">Int tensor of shape n x Constants.PEPTIDE_SEQ_LEN</param>
        /// <returns>A list of string representations of the sequence</returns>
        public static string[] DecodeSequences(InferInputTensor tensor)
        {
            return DecodeSequences2(tensor).Select(s => s.FullNames).ToArray();

            /*var n = tensor.TensorShape.Dim[0].Size;
            var result = new string[n];
            var encodedSeqs = tensor.IntVal.ToArray();

            var seq = new StringBuilder(Constants.PEPTIDE_SEQ_LEN);
            for (var i = 0; i < n; ++i)
            {
                var idx = i * Constants.PEPTIDE_SEQ_LEN;
                for (var j = 0; j < Constants.PEPTIDE_SEQ_LEN; ++j)
                {
                    if (encodedSeqs[idx + j] == 0) // Essentially a null terminator
                        break;
                    else if (Constants.AMINO_ACIDS_REVERSE.TryGetValue(encodedSeqs[idx + j], out var koinaAA))
                        seq.Append(koinaAA.AA);
                    else if (Constants.MODIFICATIONS_REVERSE.TryGetValue(encodedSeqs[idx + j], out var koinaAAMod))
                        seq.Append(string.Format(@"{0}[{1}]", koinaAAMod.AA, koinaAAMod.Mod.ShortName));
                    else
                        throw new KoinaException(string.Format(@"Unknown Koina AA index {0}", encodedSeqs[idx + j]));
                }

                result[i] = seq.ToString();
                seq.Clear();
            }

            return result;*/
        }

        /// <summary>
        /// Decodes "Koina-encoded" peptide sequences from a tensor. Only used in testing
        /// </summary>
        /// <param name="tensor">Int tensor of shape n x Constants.PEPTIDE_SEQ_LEN</param>
        /// <returns>A list of modified sequence objects representing the decoded sequences</returns>
        public static ModifiedSequence[] DecodeSequences2(InferInputTensor tensor)
        {
            var n = tensor.Shape[0];
            var result = new ModifiedSequence[n];
            var encodedSeqs = tensor.Contents.BytesContents.ToArray();

            for (var i = 0; i < n; ++i)
            {
                var modifiedSeq = encodedSeqs[i].ToStringUtf8();
                result[i] = new ModifiedSequence(modifiedSeq, MassType.Monoisotopic);
            }

            return result;
        }

        /// <summary>
        /// Decodes one hot encoded charges from a tensor. Only used in testing
        /// </summary>
        /// <param name="tensor">Float Tensor of shape n x Constants.PRECURSOR_CHARGES</param>
        /// <returns></returns>
        public static int[] DecodeCharges(InferInputTensor tensor)
        {
            var result = new int[tensor.Shape[0]];
            for (int i = 0; i < tensor.Shape[0]; ++i)
            {
                result[i] = -1;
                for (int j = 0; j < tensor.Shape[1]; ++j)
                {
                    if (tensor.Contents.Fp32Contents[i * KoinaConstants.PRECURSOR_CHARGES + j] == 1.0f)
                    {
                        result[i] = j + 1;
                        break;
                    }
                }

                if (result[i] == -1)
                {
                    var charges = tensor.Contents.Fp32Contents.Skip(i * KoinaConstants.PRECURSOR_CHARGES).Take(KoinaConstants.PRECURSOR_CHARGES);
                    throw new KoinaException(string.Format(@"[{0}] is not a valid one-hot encoded charge", string.Join(
                        @", ", charges)));
                }
            }

            return result;
        }


        /// <summary>
        /// Charges need to be one hot encoded
        /// </summary>
        public static float[] OneHotEncode(int i, int n)
        {
            var result = new float[n];
            result[i] = 1.0f;
            return result;
        }
    }

    public abstract class SkylineInputRow : IEquatable<SkylineInputRow>
    {
        public abstract bool Equals(SkylineInputRow other);
    }

    public abstract class DataTypes
    {
        public static string INT32 = @"INT32";
        public static string FP32 = @"FP32";
        public static string BYTES = @"BYTES";
    }

    /// <summary>
    /// An interface for mapping Koina inputs (only integers and floats) to
    /// tensors directly fed to the model. The interface
    /// itself represents an entire set of smaller inputs to Koina and allows
    /// for the conversion Koina Input -> Koina Tensor input
    /// </summary>
    /// <typeparam name="TKoinaInputRow">The type of a single input for Koina. For instance
    /// a single precursor</typeparam>
    public abstract class KoinaInput<TKoinaInputRow>
    {
        public abstract IList<TKoinaInputRow> InputRows { get; }
        public abstract IList<InferInputTensor> KoinaTensors { get; }
        public abstract IList<string> OutputTensorNames { get; }
    }

    /// <summary>
    /// An interface for mapping tensors returned from
    /// Koina to Skyline data structures.
    /// </summary>
    /// <typeparam name="T">The derived type itself</typeparam>
    /// <typeparam name="TKoinaOutputRow">Type of single koina output, such as an iRT value</typeparam>
    public abstract class KoinaOutput<T, TKoinaOutputRow> where T : KoinaOutput<T, TKoinaOutputRow>, new()
    {
        public abstract IList<TKoinaOutputRow> OutputRows { get; protected set; }

        public virtual T MergeOutputs(T other)
        {
            return new T
            {
                OutputRows = OutputRows.Concat(other.OutputRows).ToArray()
            };
        }
    }
}
