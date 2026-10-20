/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec.IO;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;
using pwiz.CarafeSharp.IO;
using pwiz.CarafeSharp.Models;
using pwiz.CarafeSharp.Proteome;
using pwiz.CarafeSharp.Tasks;
using pwiz.Common.Pipeline;
using pwiz.Osprey.Core;
using pwiz.Osprey.IO;
using pwiz.Osprey.Tasks;
using TorchSharp;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// The one command from a protein FASTA to a <c>.blib</c> (<c>--fasta</c>), run in-process on
    /// the Stellar subset: the library is predicted on the CPU from a protein FASTA the test
    /// writes, then searched by the four stages, in one command line. The FASTA is synthetic - each
    /// of the subset library's synthetic proteins becomes one record whose sequence is its tryptic
    /// peptides joined end to end, so trypsin returns them - and the precursor m/z window is set to
    /// the subset's one isolation window, so the predicted library is small and the prediction
    /// takes seconds. The checks are the behavior of the workflow: the products and where they
    /// land, the task lines, the resume (a warm re-run predicts nothing and searches nothing, a
    /// changed digest predicts again), the refusals, entrapment, and a saved model in place of the
    /// pretrained ones.
    /// </summary>
    public partial class SubsetPipelineTest
    {
        private const string FASTA_FILE = @"subset-proteins.fasta";
        private const string SAVED_MODEL_FILE = @"pretrained.carafemodel";
        // The subset's isolation window, as the precursor m/z window the library is predicted for:
        // every peptide of the subset library falls in it, and little else does.
        private const string WINDOW_MIN_MZ = @"592.5";
        private const string WINDOW_MAX_MZ = @"596.6";
        // Floors well under what the data gives: the digest returns 651 peptides (the subset's 335
        // tryptic ones and the missed-cleavage spans between them), of which 337 have a charge
        // state in the window, each with a decoy: 674 precursors predicted.
        private const int MIN_PREDICTED_PRECURSORS = 400;
        // The search of the predicted library reports about 136 precursors at 1% FDR, against
        // about 177 from the curated subset library (predicted spectra, pretrained RT).
        private const int MIN_LIBRARY_BUILD_PRECURSORS = 80;

        [TestMethod, DoNotParallelize]
        public void TestSubsetLibraryBuild()
        {
            string fasta = WriteSubsetProteinFasta();
            string workDir = CreateDir(@"library-build");
            var buildArgs = LibraryBuildArgs(fasta, workDir).ToArray();

            // Mode 1: predict with the pretrained models, then search. Five task lines: the
            // prediction, then the four stages.
            string log = RunOsprey(buildArgs, Verifier(false));
            AssertTasks(log, Array.Empty<string>(), new[] { PredictLibraryTask.TASK_NAME }.Concat(ALL_TASKS));
            string library = Path.Combine(workDir, Path.GetFileNameWithoutExtension(BLIB_FILE) + LibraryBuildWorkflow.LIBRARY_SUFFIX + LibrarySource.EXT_BLIB);
            string libraryWorkDir = Path.Combine(workDir, Path.GetFileNameWithoutExtension(BLIB_FILE) + LibraryBuildWorkflow.LIBRARY_SUFFIX);
            Assert.IsTrue(File.Exists(library), library);
            Assert.IsTrue(Directory.Exists(libraryWorkDir), libraryWorkDir);
            Assert.IsFalse(File.Exists(Path.Combine(libraryWorkDir, LibraryBuildWorkflow.PAIRING_FILE_NAME)),
                @"no pairing manifest without " + OspreyCommandArgs.ARG_ENTRAPMENT.ArgumentText);
            int predicted = AssertPredictedLibrary(library, false, PredictLibraryTask.TASK_NAME);
            int reported = AssertLibraryBuildOutput(Path.Combine(workDir, BLIB_FILE), library, log);
            Assert.IsTrue(reported < predicted, string.Format(@"{0} reported of {1} predicted", reported, predicted));

            // Warm: the library is current, so the prediction is skipped, and the search keys on
            // the same library file, so every stage is skipped too.
            byte[] libraryBefore = HashFile(library);
            byte[] outputBefore = HashFile(Path.Combine(workDir, BLIB_FILE));
            string warm = RunOsprey(buildArgs, Verifier(false));
            AssertTasks(warm, new[] { PredictLibraryTask.TASK_NAME }.Concat(ALL_TASKS), Array.Empty<string>());
            CollectionAssert.AreEqual(libraryBefore, HashFile(library));
            CollectionAssert.AreEqual(outputBefore, HashFile(Path.Combine(workDir, BLIB_FILE)));

            // A changed digest option is a different library: predicted again, and the search
            // runs again because its library file changed.
            string changed = RunOsprey(buildArgs.Concat(new[] { CarafeCommandLine.ARG_MISS_C.ArgumentText, @"0" }).ToArray(), Verifier(false));
            AssertTasks(changed, Array.Empty<string>(), new[] { PredictLibraryTask.TASK_NAME }.Concat(ALL_TASKS));
            CollectionAssert.AreNotEqual(libraryBefore, HashFile(library));
            // Carafe's underscore spelling names the same option, and the same library.
            byte[] missC0 = HashFile(library);
            string underscore = RunOsprey(buildArgs.Concat(new[] { @"--miss_c", @"0" }).ToArray(), Verifier(false));
            AssertTasks(underscore, new[] { PredictLibraryTask.TASK_NAME }.Concat(ALL_TASKS), Array.Empty<string>());
            CollectionAssert.AreEqual(missC0, HashFile(library));

            // Entrapment: a shuffled entrapment peptide per target, each with its own decoy, in the
            // library's DecoyPairs, and the FDRBench pairing manifest beside the working files.
            string entrapmentDir = CreateDir(@"library-build-entrapment");
            string entrapmentLog = RunOsprey(LibraryBuildArgs(fasta, entrapmentDir)
                .Concat(new[] { OspreyCommandArgs.ARG_ENTRAPMENT.ArgumentText }).ToArray(), Verifier(false));
            AssertTasks(entrapmentLog, Array.Empty<string>(), new[] { PredictLibraryTask.TASK_NAME }.Concat(ALL_TASKS));
            string entrapmentLibrary = Path.Combine(entrapmentDir, Path.GetFileNameWithoutExtension(BLIB_FILE) + LibraryBuildWorkflow.LIBRARY_SUFFIX + LibrarySource.EXT_BLIB);
            int withEntrapment = AssertPredictedLibrary(entrapmentLibrary, true, PredictLibraryTask.TASK_NAME);
            Assert.IsTrue(withEntrapment > 1.5 * predicted, string.Format(@"{0} precursors with entrapment, {1} without", withEntrapment, predicted));
            string pairing = Path.Combine(entrapmentDir, Path.GetFileNameWithoutExtension(BLIB_FILE) + LibraryBuildWorkflow.LIBRARY_SUFFIX,
                LibraryBuildWorkflow.PAIRING_FILE_NAME);
            Assert.IsTrue(File.Exists(pairing), pairing);
            Assert.IsTrue(File.ReadLines(pairing).Skip(1).Any(l => l.Contains("\t" + PairingManifest.KIND_P_TARGET + "\t")), @"no p_target rows in " + pairing);
            AssertLibraryBuildOutput(Path.Combine(entrapmentDir, BLIB_FILE), entrapmentLibrary, entrapmentLog);

            // The refusals: each a designed usage error, one Error: line and no stack.
            AssertLibraryBuildRefused(fasta, workDir, OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE));
            AssertLibraryBuildRefused(fasta, workDir, OspreyCommandArgs.ARG_TASK.ArgumentText, PerFileScoringTask.TASK_NAME);
            // The same refusal with no -o spells the library as the help does, instead of failing on the missing path.
            StringAssert.Contains(RunOspreyExpecting(InputArgs(DataInputs()).Concat(new[]
            {
                OspreyCommandArgs.ARG_FASTA.ArgumentText, fasta,
                OspreyCommandArgs.ARG_TASK.ArgumentText, PerFileScoringTask.TASK_NAME
            }).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START), LibraryBuildWorkflow.LIBRARY_EXAMPLE);
            AssertLibraryBuildRefused(fasta, workDir, OspreyCommandArgs.ARG_ENTRAPMENT_FASTA.ArgumentText, fasta);
            AssertLibraryBuildRefused(fasta, workDir, OspreyCommandArgs.ARG_ENTRAPMENT.ArgumentText, @"0.05");
            AssertLibraryBuildRefused(fasta, workDir, OspreyCommandArgs.ARG_DECOY_PAIRING_MANIFEST.ArgumentText, pairing);
            AssertLibraryBuildRefused(Path.Combine(_testDir, @"missing.fasta"), workDir);
            // A library-build option without --fasta changes nothing, and says so rather than being ignored.
            string noFasta = RunOspreyExpecting(InputArgs(DataInputs()).Concat(new[]
            {
                OspreyCommandArgs.ARG_LIBRARY.ArgumentText, Path.Combine(_dataDir, LIBRARY_FILE),
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                CarafeCommandLine.ARG_MISS_C.ArgumentText, @"1", OspreyCommandArgs.ARG_DEVICE.ArgumentText, @"cpu"
            }).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
            StringAssert.Contains(noFasta, string.Format(OspreyResources.Program_LibraryBuildError__0__can_be_used_only_with__1__,
                CarafeCommandLine.ARG_MISS_C.ArgumentText + @", " + OspreyCommandArgs.ARG_DEVICE.ArgumentText, OspreyCommandArgs.ARG_FASTA.ArgumentText));
        }

        /// <summary>
        /// Mode 3: a saved model in place of the pretrained ones. The model is written from the
        /// pretrained weights, as CarafeSharp's training saves a fine-tuned one, so it predicts the
        /// same MS2 spectra as mode 1 and its own RT model's retention times; what is under test is
        /// that <c>--model</c> reaches the prediction and the library is searched.
        /// </summary>
        [TestMethod, DoNotParallelize]
        public void TestSubsetLibraryBuildSavedModel()
        {
            string fasta = WriteSubsetProteinFasta();
            string model = WriteSavedModelFromPretrained(Path.Combine(_testDir, SAVED_MODEL_FILE));
            string workDir = CreateDir(@"library-build-model");
            var buildArgs = LibraryBuildArgs(fasta, workDir).Concat(new[] { OspreyCommandArgs.ARG_MODEL.ArgumentText, model }).ToArray();
            string log = RunOsprey(buildArgs, Verifier(false));
            AssertTasks(log, Array.Empty<string>(), new[] { PredictLibraryTask.TASK_NAME }.Concat(ALL_TASKS));
            string library = Path.Combine(workDir, Path.GetFileNameWithoutExtension(BLIB_FILE) + LibraryBuildWorkflow.LIBRARY_SUFFIX + LibrarySource.EXT_BLIB);
            AssertPredictedLibrary(library, false, PredictLibraryTask.TASK_NAME);
            AssertLibraryBuildOutput(Path.Combine(workDir, BLIB_FILE), library, log);
            // The model file is an input of the prediction: the same command line is current.
            string warm = RunOsprey(buildArgs, Verifier(false));
            AssertTasks(warm, new[] { PredictLibraryTask.TASK_NAME }.Concat(ALL_TASKS), Array.Empty<string>());
            // A missing model is refused before any work.
            AssertLibraryBuildRefused(fasta, workDir, OspreyCommandArgs.ARG_MODEL.ArgumentText, Path.Combine(_testDir, @"missing.carafemodel"));
        }

        /// <summary>The predicted library: stamped by the prediction task <paramref name="task"/>, with its DecoyPairs, and with Osprey's command line recorded.</summary>
        private static int AssertPredictedLibrary(string library, bool entrapment, string task)
        {
            // Plain counts: the comparer's projections are the search output's tables.
            int precursors = BlibComparer.CountWhere(library, @"RefSpectra", @"1");
            Assert.IsTrue(precursors >= MIN_PREDICTED_PRECURSORS, string.Format(@"{0} precursors predicted", precursors));
            // Every precursor is in a pair: the digest made a decoy for every target, and the
            // prediction keeps a pair whole or drops both.
            int pairs = BlibComparer.CountWhere(library, @"DecoyPairs", @"1");
            Assert.AreEqual(precursors, pairs, @"DecoyPairs rows");
            Assert.AreEqual(precursors / 2, BlibComparer.CountWhere(library, @"DecoyPairs", @"IsDecoy = 1"));
            Assert.AreEqual(entrapment ? 1 : 0, Math.Sign(BlibComparer.CountWhere(library, @"DecoyPairs", @"IsEntrapment = 1")));
            var stamp = ArtifactStamp.Parse(BlibMetadata.Read(library, ArtifactStamp.PARQUET_KEY));
            Assert.IsNotNull(stamp);
            Assert.AreEqual(task, stamp.Task);
            StringAssert.StartsWith(BlibMetadata.Read(library, BlibLibraryWriter.KEY_COMMAND), @"Osprey ");
            return precursors;
        }

        /// <summary>The search's output: a result with proteins, searched against the predicted library.</summary>
        private static int AssertLibraryBuildOutput(string output, string library, string log)
        {
            int reported = BlibComparer.CountRows(output, @"RefSpectra");
            Assert.IsTrue(reported >= MIN_LIBRARY_BUILD_PRECURSORS, string.Format(@"{0} precursors reported{1}{2}", reported, Environment.NewLine, log));
            Assert.AreEqual(RUN_NAMES.Length, BlibComparer.CountRows(output, @"SpectrumSourceFiles"));
            Assert.IsTrue(BlibComparer.CountRows(output, @"Proteins") > 0);
            Assert.AreEqual(0, BlibComparer.CountWhere(output, @"RefSpectra", @"PrecursorQValue IS NULL"));
            // The library the result was searched with is the one beside it, by name.
            StringAssert.Contains(BlibMetadata.Read(output, OspreyBlibWriter.KEY_COMMAND), OspreyCommandArgs.ARG_FASTA.ArgumentText);
            Assert.IsTrue(File.Exists(library));
            return reported;
        }

        /// <summary>A one-command line with <paramref name="extra"/> added is refused as a usage error.</summary>
        private void AssertLibraryBuildRefused(string fasta, string workDir, params string[] extra)
        {
            RunOspreyExpecting(LibraryBuildArgs(fasta, workDir).Concat(extra).ToArray(), Verifier(false), Program.EXIT_CODE_FAILURE_TO_START);
        }

        /// <summary>
        /// The one command over the subset runs: predict from <paramref name="fasta"/> on the CPU,
        /// with the lab's digest (trypsin, one missed cleavage, 7-35 residues, carbamidomethyl C,
        /// no variable modifications, charges 2-3) and the subset's isolation window as the
        /// precursor m/z window, then search as the other subset legs do.
        /// </summary>
        private IEnumerable<string> LibraryBuildArgs(string fasta, string workDir)
        {
            return LibraryBuildArgs(fasta, workDir, DataInputs());
        }

        /// <summary><see cref="LibraryBuildArgs(string, string)"/> over <paramref name="inputs"/>.</summary>
        private IEnumerable<string> LibraryBuildArgs(string fasta, string workDir, IEnumerable<string> inputs)
        {
            return InputArgs(inputs).Concat(new[]
            {
                OspreyCommandArgs.ARG_FASTA.ArgumentText, fasta,
                OspreyCommandArgs.ARG_OUTPUT.ArgumentText, Path.Combine(workDir, BLIB_FILE),
                OspreyCommandArgs.ARG_WORK_DIR.ArgumentText, workDir,
                OspreyCommandArgs.ARG_DEVICE.ArgumentText, @"cpu",
                CarafeCommandLine.ARG_ENZYME.ArgumentText, @"1",
                CarafeCommandLine.ARG_MISS_C.ArgumentText, @"1",
                CarafeCommandLine.ARG_MIN_LENGTH.ArgumentText, @"7",
                CarafeCommandLine.ARG_MAX_LENGTH.ArgumentText, @"35",
                CarafeCommandLine.ARG_FIX_MOD.ArgumentText, @"1",
                CarafeCommandLine.ARG_VAR_MOD.ArgumentText, @"0",
                CarafeCommandLine.ARG_MAX_VAR.ArgumentText, @"1",
                CarafeCommandLine.ARG_MIN_PEP_CHARGE.ArgumentText, @"2",
                CarafeCommandLine.ARG_MAX_PEP_CHARGE.ArgumentText, @"3",
                CarafeCommandLine.ARG_MIN_PEP_MZ.ArgumentText, WINDOW_MIN_MZ,
                CarafeCommandLine.ARG_MAX_PEP_MZ.ArgumentText, WINDOW_MAX_MZ,
            }).Concat(CommonArgs());
        }

        /// <summary>
        /// A protein FASTA of the subset library's peptides: one record per synthetic protein,
        /// its tryptic peptides joined end to end. A peptide that does not end in K or R, or
        /// starts with P, would not come back from trypsin as itself and is left out.
        /// </summary>
        private string WriteSubsetProteinFasta()
        {
            string path = Path.Combine(_testDir, FASTA_FILE);
            if (File.Exists(path))
                return path;
            var lines = File.ReadLines(Path.Combine(_dataDir, LIBRARY_FILE)).ToList();
            var header = lines[0].Split('\t');
            int iPeptide = Array.IndexOf(header, @"StrippedPeptide");
            int iProtein = Array.IndexOf(header, @"ProteinID");
            var peptidesByProtein = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var fields in lines.Skip(1).Select(l => l.Split('\t')))
            {
                string peptide = fields[iPeptide];
                if (!(peptide.EndsWith(@"K", StringComparison.Ordinal) || peptide.EndsWith(@"R", StringComparison.Ordinal)) ||
                    peptide.StartsWith(@"P", StringComparison.Ordinal))
                {
                    continue;
                }
                if (!peptidesByProtein.TryGetValue(fields[iProtein], out var peptides))
                    peptidesByProtein.Add(fields[iProtein], peptides = new List<string>());
                if (!peptides.Contains(peptide))
                    peptides.Add(peptide);
            }
            var fasta = new StringBuilder();
            foreach (var protein in peptidesByProtein.OrderBy(kv => kv.Key, StringComparer.Ordinal))
                fasta.Append('>').Append(protein.Key).Append('\n').Append(string.Concat(protein.Value)).Append('\n');
            File.WriteAllText(path, fasta.ToString());
            return path;
        }

        /// <summary>
        /// A saved model holding the pretrained weights - AlphaPeptDeep's MS2 model and Chronologer,
        /// the RT model CarafeSharp fine-tunes by default - written as CarafeSharp's training
        /// writes a fine-tuned one: the safetensors and the metrics in a model folder, packed by
        /// <see cref="CarafeModelFile.Write"/>. So it predicts what mode 1 predicts. (The generic
        /// AlphaPeptDeep RT model reports a fraction of the precursors on this seven-minute window,
        /// which is why Chronologer is the default and the fixture holds it.)
        /// </summary>
        private static string WriteSavedModelFromPretrained(string path)
        {
            string folder = Path.Combine(Path.GetDirectoryName(path) ?? string.Empty, @"pretrained-model");
            Directory.CreateDirectory(folder);
            var pretrained = PretrainedModels.Open();
            using (var ms2 = Ms2Model.FromPretrained(pretrained, torch.CPU))
                ms2.Save(Path.Combine(folder, ModelFiles.MS2_SAFETENSORS));
            using (var rt = ChronologerModel.FromFiles(ChronologerFiles.Open(), torch.CPU))
                rt.Save(Path.Combine(folder, ModelFiles.RT_SAFETENSORS));
            File.WriteAllText(Path.Combine(folder, ModelFiles.METRICS), "{\"ms2\":{\"use_finetuned_for_prediction\":true}}");
            var ms2Origin = new CarafeModelOrigin
            {
                Model = CarafeModelOrigin.ALPHAPEPTDEEP, Version = PretrainedModels.VERSION, Start = CarafeModelOrigin.START_PRETRAINED,
            };
            var rtOrigin = new CarafeModelOrigin
            {
                Model = RtModelType.chronologer.ToString(), Version = ChronologerFiles.VERSION, Start = CarafeModelOrigin.START_PRETRAINED,
            };
            CarafeModelFile.Write(path, CarafeModelDirectory.Open(folder, true), @"all", pretrained.Sha256, null, null,
                AcquisitionVocabulary.DEFAULT, null, ms2Origin, rtOrigin);
            return path;
        }
    }
}
