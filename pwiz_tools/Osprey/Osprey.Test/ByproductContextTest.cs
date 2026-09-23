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
using System.IO;
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
        /// A DEFERRED milestone does its work on the first <c>Value</c> read, not when it is
        /// published (issue #4597). PerFileRescore publishes <see cref="RescoredEntries"/>
        /// that way because reaching the milestone after the streamed rescore means
        /// re-reading every file's artifacts - whole-run join work at the end of a per-file
        /// HPC task whose process exits there. Three things are pinned:
        ///
        /// <para>Publishing must not build. The DEBUG milestone-ordering guard inspects every
        /// published <see cref="PerFileEntries"/>, so a guard that reached through
        /// <c>Value</c> would make the DEBUG build pay the whole join at publish time - and
        /// pay it before the rescore that fills the buffer has even run.</para>
        ///
        /// <para>Reading builds, exactly once. The build overlays reconciled parquets and
        /// appends gap-fill rows; running it twice over one buffer would duplicate them.</para>
        ///
        /// <para>Not reading builds nothing. That is the whole point: a
        /// <c>--task PerFileRescoring</c> worker skips the join because nothing pulled it.</para>
        /// </summary>
        [TestMethod]
        public void TestDeferredMilestoneBuildsOnFirstValueRead()
        {
            var ctx = ContextFor(new StubProducerTask(publishBeforeStop: false, exitCode: 0));
            var buffer = BufferWithOneFile();
            int builds = 0;
            // Over a buffer that already carries an earlier milestone, which is the real
            // pipeline shape: PerFileScoring -> FirstPassFDR -> PerFileRescore all publish over
            // ONE list, and the DEBUG ordering guard runs on the second publish, not the first.
            ctx.Publish(new CompactedEntries(buffer));
            Assert.IsTrue(ctx.TryGet<CompactedEntries>(out _));
            ctx.Publish(new RescoredEntries(buffer, () =>
            {
                builds++;
                buffer[0].Value.Add(new FdrEntry());
            }));
            Assert.AreEqual(0, builds, @"Publishing a deferred milestone must not build it");

            Assert.IsTrue(ctx.TryGet<RescoredEntries>(out var milestone));
            Assert.AreEqual(0, builds, @"Holding the milestone token is not a pull");
            Assert.AreSame(buffer, milestone.BufferIdentity);
            Assert.AreEqual(0, builds, @"Reading the buffer's identity is not a pull either");

            Assert.AreSame(buffer, milestone.Value);
            Assert.AreEqual(1, builds);
            Assert.AreEqual(1, buffer[0].Value.Count, @"The build fills the shared buffer in place");

            var unused = milestone.Value;
            Assert.AreEqual(1, builds, @"A second read must not re-run the build");
            Assert.AreEqual(1, buffer[0].Value.Count);
        }

        /// <summary>
        /// A build that THROWS stays thrown. The build refills survivor lists, then overlays
        /// reconciled parquets and appends gap-fill rows, so a second attempt over the same
        /// buffer would duplicate whatever the first one finished - and a reader who quietly
        /// got the half-filled pool instead would write a blib from part of the run and report
        /// a plausible wrong number. The failure is cached and re-thrown, and the partial work
        /// is never resumed.
        /// </summary>
        [TestMethod]
        public void TestDeferredMilestoneRethrowsAndDoesNotRebuildAfterFailure()
        {
            var buffer = BufferWithOneFile();
            int builds = 0;
            var milestone = new RescoredEntries(buffer, () =>
            {
                builds++;
                buffer[0].Value.Add(new FdrEntry());   // the partial fill a retry must not repeat
                throw new InvalidDataException(@"survivor refill failed");
            });

            for (int read = 0; read < 2; read++)
            {
                try
                {
                    var unused = milestone.Value;
                    Assert.Fail(@"Expected the deferred build's failure to surface on read");
                }
                catch (InvalidDataException)
                {
                    // expected on BOTH reads: the exception is cached, not re-attempted
                }
            }
            Assert.AreEqual(1, builds, @"A failed build must not be retried by the next reader");
            Assert.AreEqual(1, buffer[0].Value.Count, @"and must not add to its own partial work");
        }

        /// <summary>
        /// The undeferred constructor is the arm where the buffer already holds its
        /// post-rescore state (the resident survivor pool, and both rehydrate paths): reading
        /// it hands back the same list with nothing to run.
        /// </summary>
        [TestMethod]
        public void TestUndeferredMilestoneReadsStraightThrough()
        {
            var buffer = BufferWithOneFile();
            buffer[0].Value.Add(new FdrEntry());
            var milestone = new RescoredEntries(buffer);
            Assert.AreSame(buffer, milestone.Value);
            Assert.AreSame(buffer, milestone.BufferIdentity);
            Assert.AreEqual(1, milestone.Value[0].Value.Count);
        }

        /// <summary>
        /// <see cref="RescoredEntries.StreamFiles"/> is the per-file source that lets Stage 7
        /// fold without holding the run: each file is materialized as the consumer reaches it
        /// and DROPPED when the consumer moves on, so the peak is one file's survivors rather
        /// than every file's at once.
        ///
        /// <para>The last assertion is why this is a test and not a comment. After a stream the
        /// buffer's lists are EMPTY rather than unbuilt, so a consumer that then read
        /// <c>Value</c> would receive one empty list per file with no exception and no warning
        /// - the blib-with-no-precursors failure <see cref="PerFileEntries"/> warns about. It
        /// has to throw instead, because there is no honest value to return and rebuilding
        /// silently would restore the very peak the stream exists to avoid.</para>
        /// </summary>
        [TestMethod]
        public void TestStreamFilesDropsEachFileAndRefusesALaterValueRead()
        {
            // No per-file source - the resident A/B oracle, where the run kept its buffer and
            // has nothing to rebuild a dropped file from. StreamFiles falls back to the
            // whole-run build and drops nothing.
            var resident = BufferWithFiles(@"file1", @"file2");
            int residentBuilds = 0;
            var residentMilestone = new RescoredEntries(resident, () =>
            {
                residentBuilds++;
                foreach (var kv in resident)
                    kv.Value.Add(new FdrEntry());
            });
            var residentWalk = new List<string>();
            foreach (var kv in residentMilestone.StreamFiles())
                residentWalk.Add(kv.Key);
            Assert.AreEqual(1, residentBuilds);
            CollectionAssert.AreEqual(new[] { @"file1", @"file2" }, residentWalk);
            Assert.AreEqual(2, ResidentEntryCount(resident), @"The fallback walk must not drop");
            Assert.AreSame(resident, residentMilestone.Value);

            // With a per-file source: the whole-run build never runs, and exactly ONE file is
            // resident at any point in the walk - the property the whole change exists for.
            var streamed = BufferWithFiles(@"file1", @"file2");
            int wholeRunBuilds = 0;
            var materialized = new List<string>();
            var milestone = new RescoredEntries(streamed, () => wholeRunBuilds++,
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
            Assert.AreEqual(0, wholeRunBuilds, @"A streamed walk must not build the whole-run pool");
            CollectionAssert.AreEqual(new[] { @"file1", @"file2" }, materialized);
            CollectionAssert.AreEqual(new[] { 1, 1 }, residentDuringWalk);
            Assert.AreEqual(0, ResidentEntryCount(streamed), @"The last file is dropped too");

            // Re-enumerable, because a fold-then-apply consumer needs two passes: accumulate
            // O(distinct) floors over every file, then apply them to every file.
            foreach (var kv in milestone.StreamFiles())
                Assert.AreEqual(1, kv.Value.Count);
            CollectionAssert.AreEqual(new[] { @"file1", @"file2", @"file1", @"file2" }, materialized);

            Assert.ThrowsException<InvalidOperationException>(() => milestone.Value);
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
        /// <para>The refusal is the half that would otherwise fail silently. A resident run
        /// stamps its entries in place and keeps them, so an overlay handed to it is simply
        /// never called - the stage believes it applied something it did not, which is the
        /// shape of defect this area has produced repeatedly.</para>
        /// </summary>
        [TestMethod]
        public void TestPostMaterializeOverlaysRunInOrderAndOnlyWithAPerFileSource()
        {
            var buffer = BufferWithFiles(@"file1", @"file2");
            var applied = new List<string>();
            var milestone = new RescoredEntries(buffer, () => { },
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

            var resident = new RescoredEntries(BufferWithFiles(@"file1"));
            Assert.ThrowsException<InvalidOperationException>(
                () => resident.AddPostMaterialize((fileName, entries) => { }));
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
