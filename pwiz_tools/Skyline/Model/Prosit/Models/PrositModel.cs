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
using System.Text;
using System.Threading;
using Google.Protobuf.Collections;
using Grpc.Core;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Prosit.Communication;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using Tensorflow;
using Tensorflow.Serving;
using Settings = pwiz.Skyline.Properties.Settings;

namespace pwiz.Skyline.Model.Prosit.Models
{
    /// <summary>
    /// An abstract class representing a Prosit tensor flow model
    /// such as the intensity or RT model. It supports simple prediction
    /// and batch construction/prediction. The various type parameters
    /// are necessary due to the several layers of data representation.
    /// 
    /// For the input layer, we convert:
    /// IList{TSkylineInputRow} -> TPrositIn{TPrositInputRow} -> MapField{string}{TensorProto}
    /// 
    /// For the output layer, we convert:
    /// MapField{string}{TensorProto} -> TPrositOut{TPrositOut, TPrositOutputRow} -> TSkylineOutput
    /// </summary>
    /// 
    /// <typeparam name="TPrositInputRow">A single input for Prosit, for instance a precursor</typeparam>
    /// <typeparam name="TPrositIn">The entire input for Prosit, such as a list of precursors</typeparam>
    /// <typeparam name="TSkylineInputRow">A single input for Prosit in Skyline data structures, such as a TransitionGroupDocNode</typeparam>
    /// <typeparam name="TPrositOutputRow">A single output of Prosit, such as list of fragment intensities</typeparam>
    /// <typeparam name="TPrositOut">The entire output of Prosit, such as a list of fragment intensities for all requested peptides</typeparam>
    /// <typeparam name="TSkylineOutput">The entire output of Prosit in Skyline  friendly data structures</typeparam>

    public abstract class PrositModel<TPrositInputRow, TPrositIn, TSkylineInputRow, TPrositOutputRow, TPrositOut, TSkylineOutput>
        where TSkylineInputRow : SkylineInputRow
        where TPrositIn : PrositInput<TPrositInputRow>
        where TPrositOut : PrositOutput<TPrositOut, TPrositOutputRow>, new()
    {
        /// <summary>
        /// A signature that is required by TensorFlow, currently v1
        /// </summary>
        public abstract string Signature { get; }

        /// <summary>
        /// Name of the model, for example intensity or iRT
        /// </summary>
        public abstract string Model { get; protected set; }

        /// <summary>
        /// Construct Prosit input given Skyline input. Note that
        /// this function does not throw any exceptions but uses the exception out
        /// parameter to speed up constructing large amounts of inputs.
        /// </summary>
        /// <param name="settings">Settings to use for construction</param>
        /// <param name="skylineInput">The input at the Skyline level (for example docnodes)</param>
        /// <param name="exception">Exception that occured during the creating Process</param>
        /// <returns>The input at the Prosit level</returns>
        public abstract TPrositInputRow CreatePrositInputRow(SrmSettings settings, TSkylineInputRow skylineInput, out PrositException exception);

        /// <summary>
        /// Constructs the final input (tensors) that is sent to Prosit, given
        /// Prosit inputs.
        /// </summary>
        /// <param name="prositInputRows">The prosit inputs to use</param>
        /// <returns>A Prosit input object that can be directly sent to Prosit</returns>
        public abstract TPrositIn CreatePrositInput(IList<TPrositInputRow> prositInputRows);

        /// <summary>
        /// Converts a map of tensors to an easier to work with data structure,
        /// still at the Prosit level.
        /// </summary>
        /// <param name="prositOutputData">The data from the prediction</param>
        /// <returns>A Prosit output object containing the parsed information from the tensors</returns>
        public abstract TPrositOut CreatePrositOutput(MapField<string, TensorProto> prositOutputData);

        /// <summary>
        /// Constructs Skyline level outputs given Prosit outputs
        /// </summary>
        /// <param name="settings">Settings to use for construction</param>
        /// <param name="skylineInputs">The original skyline inputs used for the prediction. Should
        /// exclude items that could not be predicted</param>
        /// <param name="prositOutput">The prosit output from the prediction</param>
        /// <returns>A skyline level object that can be used in Skyline, for instance for display in the UI</returns>
        public abstract TSkylineOutput CreateSkylineOutput(SrmSettings settings, IList<TSkylineInputRow> skylineInputs, TPrositOut prositOutput);

        /// <summary>
        /// Helper function for creating input tensors.
        /// type should match T.
        /// </summary>
        public static TensorProto Create2dTensor<T>(DataType type, Func<TensorProto, RepeatedField<T>> getVal,
            ICollection<T> inputs, params long[] dimensions)
        {
            // Construct Tensor
            var tp = new TensorProto {Dtype = type};
            // Populate with data
            getVal(tp).AddRange(inputs);
            tp.TensorShape = new TensorShapeProto();
            Assume.AreEqual(dimensions.Aggregate(1L, (a, b) => a * b), (long) inputs.Count);
            tp.TensorShape.Dim.AddRange(dimensions.Select(d => new TensorShapeProto.Types.Dim {Size = d}));

            return tp;
        }

        /// <summary>
        /// Single threaded Prosit prediction for several inputs
        /// </summary>
        /// <param name="predictionClient">Client to use for prediction</param>
        /// <param name="settings">Settings to use for constructing </param>
        /// <param name="inputs">The precursors (and other info) to make predictions for</param>
        /// <param name="token">Token for cancelling prediction</param>
        /// <returns>Predictions from Prosit</returns>
        public TSkylineOutput Predict(PredictionService.PredictionServiceClient predictionClient,
            SrmSettings settings, IList<TSkylineInputRow> inputs, CancellationToken token)
        {
            inputs = inputs.Distinct().ToArray();

            var validSkylineInputs = new List<TSkylineInputRow>(inputs.Count);
            var prositInputs = new List<TPrositInputRow>(inputs.Count);

            foreach (var singleInput in inputs)
            {
                var input = CreatePrositInputRow(settings, singleInput, out _);
                if (input != null)
                {
                    prositInputs.Add(input);
                    validSkylineInputs.Add(singleInput);
                }
            }

            var prositIn = CreatePrositInput(prositInputs);
            var prediction = Predict(predictionClient, prositIn, token);
            return CreateSkylineOutput(settings, validSkylineInputs, prediction);
        }

        // Variables for remembering the previous prediction request and its outcome.
        // Uses lock since the PrositModel classes are intended to be used as singletons
        private readonly object _cacheLock = new object();
        private PredictionService.PredictionServiceClient _cachedClient;
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
        /// <returns>Prediction from Prosit</returns>
        public TSkylineOutput PredictSingle(PredictionService.PredictionServiceClient predictionClient,
            SrmSettings settings, TSkylineInputRow input, CancellationToken token)
        {
            lock (_cacheLock)
            {
                if (PrositConstants.CACHE_PREV_PREDICTION && _cachedInput != null && _cachedOutput != null &&
                    _cachedInput.Equals(input) && ReferenceEquals(_cachedClient, predictionClient) &&
                    ReferenceEquals(settings, _cachedSettings))
                    return _cachedOutput;
            }

            var prositInputRow = CreatePrositInputRow(settings, input, out var exception);
            if (prositInputRow == null)
                throw exception;

            var prositIn = CreatePrositInput(new[] { prositInputRow });
            var prediction = Predict(predictionClient, prositIn, token);
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
        /// the Prosit level
        /// </summary>
        /// <param name="predictionClient">Client to use for prediction</param>
        /// <param name="inputData">Input data, consisting tensors to send for prediction</param>
        /// <param name="token">Token for cancelling prediction</param>
        /// <returns>Predicted tensors from Prosit</returns>
        private TPrositOut Predict(PredictionService.PredictionServiceClient predictionClient, TPrositIn inputData, CancellationToken token)
        {
            var predictRequest = new PredictRequest();
            predictRequest.ModelSpec = new ModelSpec { Name = Model /*, SignatureName = model.Signature*/ };

            try {
                // Copy input
                var inputs = predictRequest.Inputs;
                foreach (var kvp in inputData.PrositTensors)
                    inputs[kvp.Key] = kvp.Value;

                // Make prediction
                var predictResponse = predictionClient.Predict(predictRequest, cancellationToken: token);
                return CreatePrositOutput(predictResponse.Outputs);
            }
            catch (RpcException ex) {
                throw new PrositException(ex.Message, ex);
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
        /// <returns>Predictions from Prosit</returns>
        public TSkylineOutput PredictBatches(PredictionService.PredictionServiceClient predictionClient,
            IProgressMonitor progressMonitor, ref IProgressStatus progressStatus, SrmSettings settings, IList<TSkylineInputRow> inputs, CancellationToken token)
        {

            const int CONSTRUCTING_INPUTS_FRACTION = 50;
            progressMonitor.UpdateProgress(progressStatus = progressStatus
                .ChangeMessage(PrositResources.PrositModel_BatchPredict_Constructing_Prosit_inputs)
                .ChangePercentComplete(0));
              

            inputs = inputs.Distinct().ToArray();

            var processed = 0;
            var totalCount = inputs.Count;

            var inputLock = new object();
            var inputsList =
                new List<TPrositIn>();
            var validInputsList =
                new List<List<TSkylineInputRow>>();

            // Construct batch inputs in parallel
            var localProgressStatus = progressStatus;
            ParallelEx.ForEach(PrositHelpers.EnumerateBatches(inputs, PrositConstants.BATCH_SIZE),
                batchEnumerable =>
                {
                    var batch = batchEnumerable.ToArray();

                    var batchInputs = new List<TPrositInputRow>(batch.Length);
                    var validSkylineInputs = new List<TSkylineInputRow>(batch.Length);

                    foreach (var singleInput in batch)
                    {
                        var input = CreatePrositInputRow(settings, singleInput, out _);
                        if (input != null)
                        {
                            batchInputs.Add(input);
                            validSkylineInputs.Add(singleInput);
                        }
                    }

                    lock (inputLock)
                    {
                        inputsList.Add(CreatePrositInput(batchInputs));
                        validInputsList.Add(validSkylineInputs);

                        // ReSharper disable AccessToModifiedClosure
                        processed += batch.Length;
                        progressMonitor.UpdateProgress(localProgressStatus.ChangePercentComplete(CONSTRUCTING_INPUTS_FRACTION * processed / totalCount));
                        // ReSharper enable AccessToModifiedClosure
                    }
                });

            processed = 0;
            totalCount = inputsList.Sum(pi => pi.InputRows.Count);

            const int REQUESTING_INPUTS_FRACTION = 100 - CONSTRUCTING_INPUTS_FRACTION;
            progressStatus = progressStatus
                .ChangeMessage(PrositResources.PrositModel_BatchPredict_Requesting_predictions_from_Prosit)
                .ChangePercentComplete(CONSTRUCTING_INPUTS_FRACTION);
            progressMonitor.UpdateProgress(progressStatus);

            // Make predictions batch by batch in sequence and merge the outputs
            var prositOutputAll = new TPrositOut();
            foreach (var prositIn in inputsList)
            {
                var prositOutput = Predict(predictionClient, prositIn, token);
                prositOutputAll = prositOutputAll.MergeOutputs(prositOutput);

                processed += prositIn.InputRows.Count;
                progressStatus = progressStatus.ChangeMessage(TextUtil.SpaceSeparate(
                        PrositResources.PrositModel_BatchPredict_Requesting_predictions_from_Prosit,
                        processed.ToString(), @"/", totalCount.ToString()))
                    .ChangePercentComplete(CONSTRUCTING_INPUTS_FRACTION +
                                           REQUESTING_INPUTS_FRACTION * processed / totalCount);
                progressMonitor.UpdateProgress(progressStatus);
            }

            return CreateSkylineOutput(settings, validInputsList.SelectMany(i => i).ToArray(), prositOutputAll);
        }
    }

    public class PrositHelpers
    {
        public class PrositRequest
        {
            protected CancellationTokenSource _tokenSource = new CancellationTokenSource();
            protected Action _updateCallback;

            public PrositRequest(PrositPredictionClient client, PrositIntensityModel intensityModel,
                PrositRetentionTimeModel rtModel, SrmSettings settings,
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

            public PrositRequest(SrmSettings settings, PeptideDocNode peptide, TransitionGroupDocNode precursor,
                IsotopeLabelType labelType, int nce, Action updateCallback) :
                this(PrositPredictionClient.Current, PrositIntensityModel.Instance, PrositRetentionTimeModel.Instance,
                    settings, peptide, precursor, labelType, nce, updateCallback)
            {
            }

            public virtual PrositRequest Predict()
            {
                ActionUtil.RunAsync(() =>
                {
                    try
                    {
                        var labelType = LabelType ?? Precursor.LabelType;
                        var skyIn = new PrositIntensityModel.PeptidePrecursorNCE(Peptide,
                            Precursor, labelType, NCE);
                        var massSpectrum = IntensityModel.PredictSingle(Client,
                            Settings, skyIn,
                            _tokenSource.Token);
                        var iRT = RTModel.PredictSingle(Client,
                            Settings,
                            Peptide, _tokenSource.Token);
                        Spectrum = new SpectrumDisplayInfo(
                            new SpectrumInfoProsit(massSpectrum, Precursor, labelType, NCE),
                            // ReSharper disable once AssignNullToNotNullAttribute
                            Precursor,
                            iRT[Peptide]);
                    }
                    catch (PrositException ex)
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

            public PrositPredictionClient Client { get; protected set; }
            public PrositIntensityModel IntensityModel { get; protected set; }
            public PrositRetentionTimeModel RTModel { get; protected set; }
            public SrmSettings Settings { get; protected set; }
            public PeptideDocNode Peptide { get; protected set; }
            public TransitionGroupDocNode Precursor { get; protected set; }
            public IsotopeLabelType LabelType { get; protected set; }
            public int NCE { get; protected set; }

            protected bool Equals(PrositRequest other)
            {
                return Client.Server == other.Client.Server && ReferenceEquals(IntensityModel, other.IntensityModel) &&
                       ReferenceEquals(RTModel, other.RTModel) && ReferenceEquals(Settings, other.Settings) &&
                       ReferenceEquals(Peptide, other.Peptide) && ReferenceEquals(Precursor, other.Precursor) &&
                       ReferenceEquals(LabelType, other.LabelType) && NCE == other.NCE;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((PrositRequest)obj);
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

        public static bool PrositSettingsValid
        {
            get
            {
                return !string.IsNullOrEmpty(Settings.Default.PrositIntensityModel) &&
                       !string.IsNullOrEmpty(Settings.Default.PrositRetentionTimeModel);
            }
        }

        /// <summary>
        /// Calculates the normalized contrast angle of the square rooted
        /// intensities of the two given spectra.
        /// </summary>
        /// <param name="spectrum1">First spectrum</param>
        /// <param name="spectrum2">Second spectrum</param>
        /// <param name="mzTolerance">Tolerance for considering two mzs the same</param>
        public static double CalculateSpectrumDotpMzMatch(LibraryRankedSpectrumInfo spectrum1, LibraryRankedSpectrumInfo spectrum2, double mzTolerance)
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
                if (Math.Abs(mz1 - mz2) <
                    mzTolerance)
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
                yield return data.Skip(dataIndex).Take(size);
                dataIndex += size;
            }
        }

        /// <summary>
        /// Sequences are passed to Prosit as an array of indices mapping into an array
        /// of amino acids (with modifications). Actually throwing exceptions in this method
        /// slows down constructing inputs (for larger data sets with unknown mods (and aa's)significantly,
        /// which is why PrositExceptions (only) are set as an output parameter and null is returned.
        /// </summary>
        public static int[] EncodeSequence(SrmSettings settings, PeptideDocNode peptide, IsotopeLabelType label, out PrositException exception)
        {
            if (!peptide.Target.IsProteomic)
                throw new PrositSmallMoleculeException(peptide.ModifiedTarget);

            var sequence = peptide.Target.Sequence;
            if (sequence.Length > PrositConstants.PEPTIDE_SEQ_LEN) {
                exception = new PrositPeptideTooLongException(peptide.ModifiedTarget);
                return null;
            }

            var modifiedSequence = ModifiedSequence.GetModifiedSequence(settings, peptide.ModifiedTarget.Sequence, peptide.ExplicitMods, label);
            var result = new int[PrositConstants.PEPTIDE_SEQ_LEN];

            for (var i = 0; i < sequence.Length; ++i) {
                if (!PrositConstants.AMINO_ACIDS.TryGetValue(sequence[i], out var prositAA)) {
                    exception = new PrositUnsupportedAminoAcidException(peptide.ModifiedTarget, i);
                    return null;
                }

                var mods = modifiedSequence.ExplicitMods.Where(m => m.IndexAA == i).ToArray();
                foreach (var mod in mods)
                {
                    if (mod.MonoisotopicMass == 0.0)
                        continue;

                    var staticMod = UniMod.FindMatchingStaticMod(mod.StaticMod, true) ?? mod.StaticMod;
                    if (!PrositConstants.MODIFICATIONS.TryGetValue(staticMod.Name, out var prositAAMod)) {
                        exception = new PrositUnsupportedModificationException(peptide.ModifiedTarget,
                            mod.StaticMod,
                            mod.IndexAA);
                        return null;
                    }

                    result[i] = prositAAMod.PrositIndex;
                    break;
                }

                if(result[i] == 0) {
                    // Not modified
                    result[i] = prositAA.PrositIndex;
                }
            }

            exception = null;
            return result;
        }

        /// <summary>
        /// Decodes "Prosit-encoded" peptide sequences from a tensor. Only used in testing
        /// </summary>
        /// <param name="tensor">Int tensor of shape n x Constants.PEPTIDE_SEQ_LEN</param>
        /// <returns>A list of string representations of the sequence</returns>
        public static string[] DecodeSequences(TensorProto tensor)
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
                    else if (Constants.AMINO_ACIDS_REVERSE.TryGetValue(encodedSeqs[idx + j], out var prositAA))
                        seq.Append(prositAA.AA);
                    else if (Constants.MODIFICATIONS_REVERSE.TryGetValue(encodedSeqs[idx + j], out var prositAAMod))
                        seq.Append(string.Format(@"{0}[{1}]", prositAAMod.AA, prositAAMod.Mod.ShortName));
                    else
                        throw new PrositException(string.Format(@"Unknown Prosit AA index {0}", encodedSeqs[idx + j]));
                }

                result[i] = seq.ToString();
                seq.Clear();
            }

            return result;*/
        }

        /// <summary>
        /// Decodes "Prosit-encoded" peptide sequences from a tensor. Only used in testing
        /// </summary>
        /// <param name="tensor">Int tensor of shape n x Constants.PEPTIDE_SEQ_LEN</param>
        /// <returns>A list of modified sequence objects representing the decoded sequences</returns>
        public static ModifiedSequence[] DecodeSequences2(TensorProto tensor)
        {
            var n = tensor.TensorShape.Dim[0].Size;
            var result = new ModifiedSequence[n];
            var encodedSeqs = tensor.IntVal.ToArray();

            var seq = new StringBuilder(PrositConstants.PEPTIDE_SEQ_LEN);
            for (var i = 0; i < n; ++i)
            {
                var explicitMods = new List<ExplicitMod>();

                var idx = i * PrositConstants.PEPTIDE_SEQ_LEN;
                for (var j = 0; j < PrositConstants.PEPTIDE_SEQ_LEN; ++j)
                {
                    if (encodedSeqs[idx + j] == 0) // Essentially a null terminator
                    {
                        break;
                    }
                    // This essentially prioritizes unmodified AA over modified AA (e.g. unmodified C over Carbamidomethyl C)
                    else if (PrositConstants.AMINO_ACIDS_REVERSE.TryGetValue(encodedSeqs[idx + j], out var prositAA))
                    {
                        seq.Append(prositAA.AA);
                    }
                    // Here a single "first" modification is given precedence over all others for any given AA
                    else if (PrositConstants.MODIFICATIONS_REVERSE.TryGetValue(encodedSeqs[idx + j], out var prositAAMods))
                    {
                        var prositAAMod = prositAAMods[0];
                        explicitMods.Add(new ExplicitMod(j, prositAAMod.Mod));
                        seq.Append(prositAAMod.AA);
                    }
                    else
                    {
                        throw new PrositException(string.Format(@"Unknown Prosit AA index {0}", encodedSeqs[idx + j]));
                    }
                }

                var unmodSeq = seq.ToString();
                var mods = explicitMods.Select(mod => ModifiedSequence.MakeModification(unmodSeq, mod));
                result[i] = new ModifiedSequence(seq.ToString(), mods, MassType.Monoisotopic);
                seq.Clear();
            }

            return result;
        }

        /// <summary>
        /// Decodes one hot encoded charges from a tensor. Only used in testing
        /// </summary>
        /// <param name="tensor">Float Tensor of shape n x Constants.PRECURSOR_CHARGES</param>
        /// <returns></returns>
        public static int[] DecodeCharges(TensorProto tensor)
        {
            var result = new int[tensor.TensorShape.Dim[0].Size];
            for (int i = 0; i < tensor.TensorShape.Dim[0].Size; ++i)
            {
                result[i] = -1;
                for (int j = 0; j < tensor.TensorShape.Dim[1].Size; ++j)
                {
                    if (tensor.FloatVal[i * PrositConstants.PRECURSOR_CHARGES + j] == 1.0f)
                    {
                        result[i] = j + 1;
                        break;
                    }
                }

                if (result[i] == -1)
                {
                    throw new PrositException(string.Format(@"[{0}] is not a valid one-hot encoded charge", string.Join(
                        @", ", tensor.FloatVal.Skip(i * PrositConstants.PRECURSOR_CHARGES).Take(PrositConstants.PRECURSOR_CHARGES))));
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

    /// <summary>
    /// An interface for mapping Prosit inputs (only integers and floats) to
    /// tensors directly fed to the model. The interface
    /// itself represents an entire set of smaller inputs to Prosit and allows
    /// for the conversion Prosit Input -> Prosit Tensor input
    /// </summary>
    /// <typeparam name="TPrositInputRow">The type of a single input for Prosit. For instance
    /// a single precursor</typeparam>
    public abstract class PrositInput<TPrositInputRow>
    {
        public abstract IList<TPrositInputRow> InputRows { get; }
        public abstract MapField<string, TensorProto> PrositTensors { get; }
    }

    /// <summary>
    /// An interface for mapping tensors returned from
    /// Prosit to Skyline data structures.
    /// </summary>
    /// <typeparam name="T">The derived type itself</typeparam>
    /// <typeparam name="TPrositOutputRow">Type of single prosit output, such as an iRT value</typeparam>
    public abstract class PrositOutput<T, TPrositOutputRow> where T : PrositOutput<T, TPrositOutputRow>, new()
    {
        public abstract IList<TPrositOutputRow> OutputRows { get; protected set; }

        public virtual T MergeOutputs(T other)
        {
            return new T
            {
                OutputRows = OutputRows.Concat(other.OutputRows).ToArray()
            };
        }
    }
}
