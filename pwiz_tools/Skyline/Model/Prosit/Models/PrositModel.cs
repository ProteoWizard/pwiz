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
using Grpc.Core;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;
using Tensorflow;
using Tensorflow.Serving;

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

    public abstract class PrositModel<TPrositInputRow, TPrositIn, TSkylineInputRow, TPrositOutputRow, TPrositOut, TSkylineOutput> where TPrositIn : PrositInput<TPrositInputRow> where TPrositOut : PrositOutput<TPrositOut, TPrositOutputRow>, new()
    {
        public abstract string Signature { get; }
        public abstract string Model { get; protected set; }

        // Input
        public abstract TPrositInputRow CreatePrositInputRow(SrmSettings settings, TSkylineInputRow skylineInput, out PrositException exception);
        public abstract TPrositIn CreatePrositInput(IList<TPrositInputRow> prositInputRows);

        // Output
        public abstract TPrositOut CreatePrositOutput(MapField<string, TensorProto> prositOutputData);
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

        public TSkylineOutput Predict(PredictionService.PredictionServiceClient predictionClient, SrmSettings settings, IList<TSkylineInputRow> inputs)
        {
            inputs = inputs.Distinct().ToArray();

            var validSkylineInputs = new List<TSkylineInputRow>(inputs.Count);
            var prositInputs = new List<TPrositInputRow>(inputs.Count);

            foreach (var singleInput in inputs) {
                var input = CreatePrositInputRow(settings, singleInput, out _);
                if (input != null)
                {
                    prositInputs.Add(input);
                    validSkylineInputs.Add(singleInput);
                }
            }

            var prositIn = CreatePrositInput(prositInputs);
            return CreateSkylineOutput(settings, validSkylineInputs, Predict(predictionClient, prositIn));
        }

        public TSkylineOutput PredictSingle(PredictionService.PredictionServiceClient predictionClient,
            SrmSettings settings, TSkylineInputRow input)
        {
            var prositInputRow = CreatePrositInputRow(settings, input, out var exception);
            if (prositInputRow == null)
                throw exception;

            var prositIn = CreatePrositInput(new[] { prositInputRow });
            return CreateSkylineOutput(settings, new[] { input }, Predict(predictionClient, prositIn));
        }

        public TPrositOut Predict(PredictionService.PredictionServiceClient predictionClient, TPrositIn inputData)
        {
            var predictRequest = new PredictRequest();
            predictRequest.ModelSpec = new ModelSpec { Name = Model /*, SignatureName = model.Signature*/ };

            try {
                // Copy input
                var inputs = predictRequest.Inputs;
                foreach (var kvp in inputData.PrositTensors)
                    inputs[kvp.Key] = kvp.Value;

                // Make prediction
                var predictResponse = predictionClient.Predict(predictRequest);
                return CreatePrositOutput(predictResponse.Outputs);
            }
            catch (RpcException ex) {
                throw new PrositException(ex.Message);
            }
        }

        public TSkylineOutput PredictBatches(PredictionService.PredictionServiceClient predictionClient, IProgressMonitor progressMonitor, SrmSettings settings, IList<TSkylineInputRow> inputs)
        {
            IProgressStatus progressStatus = new ProgressStatus(PrositResources.PrositModel_BatchPredict_Constructing_Prosit_Inputs);
            progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(0));
            
            inputs = inputs.Distinct().ToArray();

            var processed = 0;
            var totalCount = inputs.Count;

            var inputLock = new object();
            var inputsList =
                new List<TPrositIn>();
            var validInputsList =
                new List<List<TSkylineInputRow>>();

            // Construct batch inputs in parallel
            ParallelEx.ForEach(PrositHelpers.EnumerateBatches(inputs, Constants.BATCH_SIZE),
                batchEnumerable =>
                {
                    var batch = batchEnumerable.ToArray();

                    var batchInputs = new List<TPrositInputRow>(batch.Length);
                    var validSkylineInputs = new List<TSkylineInputRow>(batch.Length);

                    foreach (var singleInput in batch)
                    {
                        var input = CreatePrositInputRow(settings, singleInput, out _);
                        if (input != null) {
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
                        progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * processed / totalCount));
                        // ReSharper enable AccessToModifiedClosure
                    }
                });

            processed = 0;
            totalCount = inputsList.Sum(pi => pi.InputRows.Count);

            progressStatus = new ProgressStatus(PrositResources.PrositModel_BatchPredict_Requesting_predictions_from_Prosit);
            progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(0));

            // Make predictions batch by batch in sequence and merge the outputs
            var prositOutputAll = new TPrositOut();
            foreach (var prositIn in inputsList)
            {
                var prositOutput = Predict(predictionClient, prositIn);
                prositOutputAll = prositOutputAll.MergeOutputs(prositOutput);

                processed += prositIn.InputRows.Count;
                progressMonitor.UpdateProgress(progressStatus = progressStatus.ChangePercentComplete(100 * processed / totalCount));
            }

            return CreateSkylineOutput(settings, validInputsList.SelectMany(i => i).ToArray(), prositOutputAll);
        }
    }

    public class PrositHelpers
    {
        public static float ReLu(float f)
        {
            return Math.Max(f, 0.0f);
        }

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
        public static int[] ParseSequence(SrmSettings settings, PeptideDocNode peptide, IsotopeLabelType label, out PrositException exception)
        {
            var sequence = peptide.Target.Sequence;
            if (sequence.Length > Constants.PEPTIDE_SEQ_LEN) {
                exception = new PrositPeptideTooLongException(peptide.ModifiedTarget);
                return null;
            }

            var modifiedSequence = ModifiedSequence.GetModifiedSequence(settings, peptide, label);
            var result = new int[Constants.PEPTIDE_SEQ_LEN];

            for (var i = 0; i < sequence.Length; ++i) {
                if (!Constants.AMINO_ACIDS.TryGetValue(sequence[i], out var index)) {
                    exception = new PrositUnsupportedAminoAcidException(peptide.ModifiedTarget, i);
                    return null;
                }

                var mods = modifiedSequence.ExplicitMods.Where(m => m.IndexAA == i).ToArray();
                foreach (var mod in mods)
                {
                    if (mod.MonoisotopicMass == 0.0)
                        continue;

                    if (!Constants.MODIFICATIONS.TryGetValue(mod.StaticMod.Name, out var idx)) {
                        exception = new PrositUnsupportedModificationException(peptide.ModifiedTarget,
                            mod.StaticMod,
                            mod.IndexAA);
                        return null;
                    }

                    result[i] = idx;
                    break;
                }

                if(result[i] == 0) {
                    // Not modified
                    result[i] = index;
                }
            }

            exception = null;
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
