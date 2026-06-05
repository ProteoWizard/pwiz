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
            // e.g. the --no-join boundary) AFTER publishing its byproduct: Get
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
    }
}
