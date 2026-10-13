/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Osprey.Core;
using pwiz.Osprey.Tasks;

namespace pwiz.Osprey.Test
{
    /// <summary>
    /// Pins <see cref="PipelineContext"/>'s byproduct cache + registry-driven
    /// lazy materialization. Focuses on the failure-surfacing contract: a
    /// consumer's <see cref="PipelineContext.Get{TInfo}"/> drives the registered
    /// producer's <see cref="OspreyTask.Rehydrate"/>, and -- unlike the driver
    /// loop, which inspects Run's bool -- must not silently swallow a failed
    /// rehydrate. The discriminator is the requested exit code: a genuine
    /// failure (ExitCode != 0) throws; a success-but-stop (ExitCode == 0, whose
    /// byproducts were published before the stop) is benign.
    /// </summary>
    [TestClass]
    public class ByproductContextTest
    {
        private sealed class StubByproduct
        {
            public int Value { get; set; }
        }

        /// <summary>
        /// Stub producer whose Rehydrate stops the pipeline (returns false). It
        /// optionally publishes its byproduct first and sets a configurable exit
        /// code, so a test can drive either the failure or the benign-stop path.
        /// </summary>
        private sealed class StubProducerTask : OspreyTask
        {
            private readonly bool _publishBeforeStop;
            private readonly int _exitCode;

            public StubProducerTask(bool publishBeforeStop, int exitCode)
            {
                _publishBeforeStop = publishBeforeStop;
                _exitCode = exitCode;
            }

            public override string Name => @"StubProducer";
            public override IEnumerable<Type> Publishes => new[] { typeof(StubByproduct) };
            public override bool Run(PipelineContext ctx) => true;

            public override bool Rehydrate(PipelineContext ctx)
            {
                if (_publishBeforeStop)
                    ctx.Publish(new StubByproduct { Value = 42 });
                ctx.ExitCode = _exitCode;
                return false;
            }
        }

        /// <summary>A second producer type that also publishes
        /// <see cref="StubByproduct"/>, to drive the single-producer
        /// registration check in <see cref="PipelineContext"/>'s constructor.</summary>
        private sealed class SecondStubProducerTask : OspreyTask
        {
            public override string Name => @"SecondStubProducer";
            public override IEnumerable<Type> Publishes => new[] { typeof(StubByproduct) };
            public override bool Run(PipelineContext ctx) => true;
            public override bool Rehydrate(PipelineContext ctx) => true;
        }

        /// <summary>Counts how many times its Rehydrate is driven, so a test can
        /// assert the <see cref="PipelineContext"/> one-shot materialization guard
        /// (driver-Run vs lazy-Demand) holds.</summary>
        private sealed class CountingProducerTask : OspreyTask
        {
            public int RehydrateCount;
            public int RunCount;
            public override string Name => @"CountingProducer";
            public override IEnumerable<Type> Publishes => new[] { typeof(StubByproduct) };
            public override bool Run(PipelineContext ctx) { RunCount++; return true; }

            public override bool Rehydrate(PipelineContext ctx)
            {
                RehydrateCount++;
                ctx.Publish(new StubByproduct { Value = 99 });
                return true;
            }
        }

        /// <summary>A producer whose Rehydrate succeeds (returns true) but neglects
        /// to publish its declared byproduct -- a programming defect that
        /// <see cref="PipelineContext.Get{TInfo}"/> must surface loudly.</summary>
        private sealed class ForgetfulProducerTask : OspreyTask
        {
            public override string Name => @"ForgetfulProducer";
            public override IEnumerable<Type> Publishes => new[] { typeof(StubByproduct) };
            public override bool Run(PipelineContext ctx) => true;
            public override bool Rehydrate(PipelineContext ctx) => true;
        }

        private static PipelineContext ContextFor(OspreyTask task)
        {
            return new PipelineContext(new OspreyConfig(), new[] { task }, null, null, null);
        }

        [TestMethod]
        public void TestGetThrowsWhenRehydrateFailsWithExitCode()
        {
            // Rehydrate fails (returns false) and requests a non-zero exit code,
            // never publishing the byproduct: Get must surface the failure rather
            // than fall through to a default value.
            var ctx = ContextFor(new StubProducerTask(publishBeforeStop: false, exitCode: 7));
            try
            {
                ctx.Get<StubByproduct>();
                Assert.Fail(@"Expected RehydrateFailedException");
            }
            catch (RehydrateFailedException ex)
            {
                Assert.AreEqual(typeof(StubProducerTask), ex.TaskType);
                Assert.AreEqual(7, ex.ExitCode);
            }
        }

        [TestMethod]
        public void TestGetSucceedsWhenRehydrateStopsWithExitCodeZero()
        {
            // Rehydrate returns false but keeps ExitCode == 0 (a success-but-stop,
            // e.g. the --task PerFileScoring boundary) AFTER publishing its byproduct: Get
            // must return the published value without throwing.
            var ctx = ContextFor(new StubProducerTask(publishBeforeStop: true, exitCode: 0));
            var info = ctx.Get<StubByproduct>();
            Assert.IsNotNull(info);
            Assert.AreEqual(42, info.Value);
        }

        [TestMethod]
        public void TestPublishOnceThenTryGetReads()
        {
            var ctx = ContextFor(new StubProducerTask(publishBeforeStop: false, exitCode: 0));
            ctx.Publish(new StubByproduct { Value = 5 });
            Assert.IsTrue(ctx.TryGet<StubByproduct>(out var info));
            Assert.AreEqual(5, info.Value);
            // Publishing the same type twice is a programming defect.
            try
            {
                ctx.Publish(new StubByproduct { Value = 6 });
                Assert.Fail(@"Expected publish-once violation to throw");
            }
            catch (ArgumentException)
            {
                // expected: Dictionary.Add rejects the duplicate key
            }
        }

        [TestMethod]
        public void TestDuplicateProducerThrowsAtConstruction()
        {
            // Two tasks declaring the same byproduct in Publishes is a definition
            // defect: the registry could not pick which task to materialize on a
            // cache miss, so the constructor must fail fast rather than silently
            // drop one producer.
            try
            {
                var unused = new PipelineContext(new OspreyConfig(),
                    new OspreyTask[] { new StubProducerTask(publishBeforeStop: false, exitCode: 0), new SecondStubProducerTask() },
                    null, null, null);
                Assert.Fail(@"Expected duplicate-producer registration to throw");
            }
            catch (ArgumentException)
            {
                // expected: each registered byproduct must have a single producer
            }
        }

        [TestMethod]
        public void TestMarkMaterializedSuppressesRehydrate()
        {
            // The driver calls MarkMaterialized after Run; a later Demand/Get for
            // that task must then NOT drive Rehydrate (its state is already in
            // memory). This is the single guard coordinating the driver-Run path
            // and the lazy-Rehydrate path.
            var task = new CountingProducerTask();
            var ctx = ContextFor(task);
            ctx.MarkMaterialized(task);
            var resolved = ctx.Demand<CountingProducerTask>();
            Assert.AreSame(task, resolved);
            Assert.AreEqual(0, task.RehydrateCount);
        }

        [TestMethod]
        public void TestDemandRehydratesAtMostOnce()
        {
            // The first consumer's Demand drives Rehydrate; subsequent demands
            // return the same instance without re-materializing (the _materialized
            // one-shot guard that replaced the per-task _runOrHydrated field).
            var task = new CountingProducerTask();
            var ctx = ContextFor(task);
            ctx.Demand<CountingProducerTask>();
            ctx.Demand<CountingProducerTask>();
            Assert.AreEqual(1, task.RehydrateCount);
        }

        [TestMethod]
        public void TestDemandDrivesRehydrateNeverRun()
        {
            // The decisive dataflow invariant at the machinery level: a lazy
            // Demand materializes a producer through Rehydrate (load), NEVER Run
            // (compute) -- Run is the driver loop's job alone. This is the
            // unit-speed guard for the "Run is outer-loop-only" constraint that
            // the per-task pure-load rehydrate paths uphold (their end-to-end
            // coverage is the straight-through-resume smoke).
            var task = new CountingProducerTask();
            var ctx = ContextFor(task);
            ctx.Demand<CountingProducerTask>();
            Assert.AreEqual(1, task.RehydrateCount);
            Assert.AreEqual(0, task.RunCount);
        }

        [TestMethod]
        public void TestGetThrowsWhenProducerRehydratesButDoesNotPublish()
        {
            // A producer whose Rehydrate succeeds but forgets to publish its
            // declared byproduct is a programming defect; Get must surface it as
            // UnknownByproductException rather than degrade to a silent default.
            var ctx = ContextFor(new ForgetfulProducerTask());
            try
            {
                ctx.Get<StubByproduct>();
                Assert.Fail(@"Expected UnknownByproductException");
            }
            catch (UnknownByproductException ex)
            {
                Assert.AreEqual(typeof(StubByproduct), ex.RequestedType);
            }
        }

        /// <summary>
        /// The byproduct cache is otherwise process-lifetime, which pinned the ~5.7 GiB
        /// first-pass FdrProjections through reconciliation and the blib write (issue
        /// #4405). Release drops the entry so the value becomes collectible.
        /// </summary>
        [TestMethod]
        public void TestReleaseDropsByproduct()
        {
            var producer = new CountingProducerTask();
            var ctx = ContextFor(producer);

            Assert.AreEqual(99, ctx.Get<StubByproduct>().Value);
            Assert.AreEqual(1, producer.RehydrateCount);
            Assert.IsTrue(ctx.TryGet<StubByproduct>(out _));

            Assert.IsTrue(ctx.Release<StubByproduct>(), @"Release must report that it removed the entry");
            Assert.IsFalse(ctx.TryGet<StubByproduct>(out _), @"Released byproduct must not remain cached");

            // Releasing something absent is a no-op, not a throw.
            Assert.IsFalse(ctx.Release<StubByproduct>());
        }

        /// <summary>
        /// Release is FINAL. A producer materializes at most once per context, so a Get
        /// after Release does not rebuild -- it throws. Pins the contract, because the
        /// tempting alternative (evict from _materialized so Get re-demands) would make
        /// Rehydrate republish the producer's *other* byproducts, and Publish throws on a
        /// duplicate key. The failure mode must be a loud exception, never silent
        /// corruption or a null.
        /// </summary>
        [TestMethod]
        public void TestGetAfterReleaseThrowsRatherThanRebuilding()
        {
            var producer = new CountingProducerTask();
            var ctx = ContextFor(producer);

            Assert.AreEqual(99, ctx.Get<StubByproduct>().Value);
            Assert.AreEqual(1, producer.RehydrateCount);

            ctx.Release<StubByproduct>();

            try
            {
                ctx.Get<StubByproduct>();
                Assert.Fail(@"Expected UnknownByproductException after Release");
            }
            catch (UnknownByproductException ex)
            {
                Assert.AreEqual(typeof(StubByproduct), ex.RequestedType);
            }
            Assert.AreEqual(1, producer.RehydrateCount, @"Producer must not be re-demanded after Release");
        }

        /// <summary>
        /// Consume is Get + Release in one call, so a large single-consumer byproduct cannot
        /// be left pinned by a refactor that quietly loses a separate Release call -- the
        /// failure mode that put ~5.7 GiB of FdrProjections through Stages 6-7 (issue #4405).
        /// </summary>
        [TestMethod]
        public void TestConsumeReturnsValueAndDropsIt()
        {
            var producer = new CountingProducerTask();
            var ctx = ContextFor(producer);

            Assert.AreEqual(99, ctx.Consume<StubByproduct>().Value);
            Assert.IsFalse(ctx.TryGet<StubByproduct>(out _), @"Consume must drop the byproduct");

            // Same finality as Release: a second Consume throws rather than rebuilding.
            try
            {
                ctx.Consume<StubByproduct>();
                Assert.Fail(@"Expected UnknownByproductException on a second Consume");
            }
            catch (UnknownByproductException ex)
            {
                Assert.AreEqual(typeof(StubByproduct), ex.RequestedType);
            }
            Assert.AreEqual(1, producer.RehydrateCount);
        }

        /// <summary>
        /// Publishing <see cref="RescoredEntries"/> materializes nothing, and reading its
        /// <c>Value</c> is refused. Its runs are rebuilt one at a time by the consumer that
        /// reads them (issue #4597), so a <c>--task PerFileRescoring</c> worker, which reads
        /// nothing, pays for nothing.
        ///
        /// <para>Publishing must not materialize. The DEBUG milestone-ordering guard inspects
        /// every published <see cref="PerFileEntries"/>, so a guard that reached through the
        /// entries would make the DEBUG build pay for a run before the rescore that fills it has
        /// even run.</para>
        ///
        /// <para><c>Value</c> must throw rather than hand back the buffer: its lists are EMPTY
        /// by design, so a consumer that read it would receive one empty list per file with no
        /// exception and no warning - the blib-with-no-precursors failure
        /// <see cref="PerFileEntries"/> warns about.</para>
        /// </summary>
        [TestMethod]
        public void TestRescoredMilestoneMaterializesOnlyPerFile()
        {
            var ctx = ContextFor(new StubProducerTask(publishBeforeStop: false, exitCode: 0));
            var buffer = BufferWithOneFile();
            int materialized = 0;
            // Over a buffer that already carries an earlier milestone, which is the real
            // pipeline shape: PerFileScoring -> FirstPassFDR -> PerFileRescore all publish over
            // ONE list, and the DEBUG ordering guard runs on the second publish, not the first.
            ctx.Publish(new CompactedEntries(buffer));
            Assert.IsTrue(ctx.TryGet<CompactedEntries>(out _));
            ctx.Publish(new RescoredEntries(buffer, (fileName, entries) =>
            {
                materialized++;
                entries.Add(new FdrEntry());
            }));
            Assert.AreEqual(0, materialized, @"Publishing the milestone must not materialize a run");

            Assert.IsTrue(ctx.TryGet<RescoredEntries>(out var milestone));
            Assert.AreSame(buffer, milestone.BufferIdentity);
            Assert.AreEqual(0, materialized, @"Reading the buffer's identity is not a pull");
            Assert.ThrowsException<InvalidOperationException>(() => milestone.Value);
            Assert.AreEqual(0, materialized, @"A refused Value read must not materialize either");

            Assert.AreEqual(1, milestone.MaterializeFile(@"file1").Count);
            Assert.AreEqual(1, materialized);
            milestone.DropFile(@"file1");
            Assert.AreEqual(0, buffer[0].Value.Count, @"DropFile empties the run again");
        }

        /// <summary>
        /// <see cref="RescoredEntries.StreamFiles"/> is the per-file source that lets Stage 7
        /// fold without holding the run: each file is materialized as the consumer reaches it
        /// and DROPPED when the consumer moves on, so the peak is one file's survivors rather
        /// than every file's at once.
        /// </summary>
        [TestMethod]
        public void TestStreamFilesDropsEachFile()
        {
            // Exactly ONE file is resident at any point in the walk - the property the whole
            // change exists for.
            var streamed = BufferWithFiles(@"file1", @"file2");
            var materialized = new List<string>();
            var milestone = new RescoredEntries(streamed,
                (fileName, entries) =>
                {
                    materialized.Add(fileName);
                    entries.Add(new FdrEntry());
                });
            var residentDuringWalk = new List<int>();
            foreach (var kv in milestone.StreamFiles())
            {
                Assert.AreEqual(1, kv.Value.Count, @"The current file must arrive materialized");
                residentDuringWalk.Add(ResidentEntryCount(streamed));
            }
            CollectionAssert.AreEqual(new[] { @"file1", @"file2" }, materialized);
            CollectionAssert.AreEqual(new[] { 1, 1 }, residentDuringWalk);
            Assert.AreEqual(0, ResidentEntryCount(streamed), @"The last file is dropped too");

            // Re-enumerable, because a fold-then-apply consumer needs two passes: accumulate
            // O(distinct) floors over every file, then apply them to every file.
            foreach (var kv in milestone.StreamFiles())
                Assert.AreEqual(1, kv.Value.Count);
            CollectionAssert.AreEqual(new[] { @"file1", @"file2", @"file1", @"file2" }, materialized);
        }

        /// <summary>
        /// The per-file overlays run after the source, in the order they were added, on every
        /// pass - and a milestone with no per-file source REFUSES one rather than accepting an
        /// overlay nothing would ever invoke.
        ///
        /// <para>Order is the correctness argument in Stage 7, not a detail: the second-pass
        /// sidecar overlay writes an experiment q and the experiment-q floor then raises it, so
        /// running them the other way round would apply a floor to a value about to be
        /// overwritten and report q-values no run computed. Asserted by composing two overlays
        /// that both append, and reading back the sequence.</para>
        ///
        /// <para>The refusal is the half that would otherwise fail silently: an overlay handed
        /// to a milestone with no source is never called, and the stage believes it applied
        /// something it did not, which is the shape of defect this area has produced
        /// repeatedly.</para>
        /// </summary>
        [TestMethod]
        public void TestPostMaterializeOverlaysRunInOrderAndOnlyWithAPerFileSource()
        {
            var buffer = BufferWithFiles(@"file1", @"file2");
            var applied = new List<string>();
            var milestone = new RescoredEntries(buffer,
                (fileName, entries) => applied.Add(fileName + @":source"));
            milestone.AddPostMaterialize((fileName, entries) => applied.Add(fileName + @":first"));
            milestone.AddPostMaterialize((fileName, entries) => applied.Add(fileName + @":second"));

            foreach (var kv in milestone.StreamFiles())
                Assert.IsNotNull(kv.Value);
            CollectionAssert.AreEqual(
                new[]
                {
                    @"file1:source", @"file1:first", @"file1:second",
                    @"file2:source", @"file2:first", @"file2:second",
                },
                applied,
                @"Source then overlays, in the order added, for each file in turn");

            // A second pass re-applies both, because the entries it applied to are gone.
            applied.Clear();
            foreach (var kv in milestone.StreamFiles())
                Assert.IsNotNull(kv.Value);
            Assert.AreEqual(6, applied.Count, @"Every pass re-applies the whole overlay chain");

            // MaterializeFile - the by-name accessor the streamed competition uses - runs the
            // same chain, so a consumer driven by the FDR layer's file order is not a second
            // path with its own overlay semantics.
            applied.Clear();
            milestone.MaterializeFile(@"file2");
            CollectionAssert.AreEqual(
                new[] { @"file2:source", @"file2:first", @"file2:second" }, applied);

            var sourceless = new RescoredEntries(BufferWithFiles(@"file1"), null);
            Assert.ThrowsException<InvalidOperationException>(
                () => sourceless.AddPostMaterialize((fileName, entries) => { }));
        }

        private static List<KeyValuePair<string, List<FdrEntry>>> BufferWithOneFile()
        {
            return BufferWithFiles(@"file1");
        }

        private static List<KeyValuePair<string, List<FdrEntry>>> BufferWithFiles(
            params string[] fileNames)
        {
            var buffer = new List<KeyValuePair<string, List<FdrEntry>>>();
            foreach (string fileName in fileNames)
                buffer.Add(new KeyValuePair<string, List<FdrEntry>>(fileName, new List<FdrEntry>()));
            return buffer;
        }

        /// <summary>Entries resident across the WHOLE buffer, which is what a streamed walk
        /// must hold at one file's worth however many files the run has.</summary>
        private static int ResidentEntryCount(
            List<KeyValuePair<string, List<FdrEntry>>> buffer)
        {
            int count = 0;
            foreach (var kv in buffer)
                count += kv.Value.Count;
            return count;
        }
    }
}
