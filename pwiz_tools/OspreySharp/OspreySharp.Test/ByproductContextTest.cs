/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using pwiz.OspreySharp.Core;
using pwiz.OspreySharp.Tasks;

namespace pwiz.OspreySharp.Test
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
    }
}
