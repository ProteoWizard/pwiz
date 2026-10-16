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

namespace pwiz.Common.Pipeline
{
    /// <summary>
    /// One step in a command-line tool's pipeline: a piece of work with a name, the files it
    /// reads and writes, and a validity key that says when its outputs may be reused. Extracted
    /// from Osprey's task, which carries this contract for its search stages, so CarafeSharp's
    /// library stages carry the same one and the two can run as one pipeline.
    ///
    /// <para>Resume on restart: the <see cref="PipelineDriver"/> skips a task whose every
    /// <see cref="Outputs"/> file exists carrying an <see cref="ArtifactStamp"/> current for the
    /// task, the build and its <see cref="OutputValidityKey"/>. Each writer embeds the stamp in
    /// the same atomic commit as the content, and the driver writes nothing after
    /// <see cref="Run"/>. The stamp names its task, so a file another task wrote is never taken
    /// as current.</para>
    ///
    /// <para>Dataflow between tasks goes through the <see cref="TaskContext"/>: a task publishes
    /// what downstream tasks consume, by type (<see cref="Publishes"/>), and a consumer's first
    /// reach for a byproduct whose producer has not run drives that producer's
    /// <see cref="Rehydrate"/>, which brings its state in from disk without recomputing it.</para>
    /// </summary>
    public abstract class PipelineTask
    {
        /// <summary>
        /// Short identifier used in log lines, a task selector on the command line and each
        /// output's validity stamp. One stable spelling.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Execute this task against the shared context. May read upstream tasks' outputs from
        /// the context and must publish any state downstream tasks depend on back to it.
        /// Returns <c>true</c> on success (the pipeline continues to the next task) or
        /// <c>false</c> to stop the driver. A task returning <c>false</c> is responsible for
        /// having already logged the reason; if the stop should produce a non-zero process exit
        /// code, the task sets <see cref="TaskContext.ExitCode"/> before returning.
        /// </summary>
        public abstract bool Run(TaskContext ctx);

        /// <summary>
        /// Lazily bring this task's outputs into memory from its on-disk artifacts, without
        /// recomputing them - the disk-load counterpart to the compute-only <see cref="Run"/>.
        /// Invoked at most once per run by <see cref="TaskContext.Demand{T}"/> when a consumer
        /// first reaches for this producer's state and the producer's own <see cref="Run"/> was
        /// never called (an invocation that starts mid-pipeline). Returns the same <c>bool</c>
        /// contract as <see cref="Run"/>. A task that produces nothing another task consumes
        /// implements this as a no-op returning <c>true</c>.
        /// </summary>
        public abstract bool Rehydrate(TaskContext ctx);

        /// <summary>
        /// The byproduct types this task publishes for downstream tasks to consume by type
        /// through <see cref="TaskContext.Get{TInfo}"/>. The context inverts these at
        /// construction into the byproduct-to-producer registry, so a consumer's cache miss can
        /// lazily materialize this task. Each declared type must be published on every path
        /// this task can run - both <see cref="Run"/> and <see cref="Rehydrate"/>. Default empty.
        /// </summary>
        public virtual IEnumerable<Type> Publishes => Array.Empty<Type>();

        /// <summary>
        /// File paths this task reads. Declarative: it states what the task consumes, for the
        /// file-contract printout and for a harness that stages a node's files.
        /// </summary>
        public virtual IEnumerable<string> Inputs(TaskContext ctx) => Array.Empty<string>();

        /// <summary>
        /// File paths this task produces. Before running, the driver checks the validity stamp
        /// embedded in each one; if every output carries a current stamp, <see cref="Run"/> is
        /// skipped. A task that returns no outputs cannot be skipped; the driver always runs it.
        /// That is the posture for work that is not durably represented on disk.
        /// </summary>
        public virtual IEnumerable<string> Outputs(TaskContext ctx) => Array.Empty<string>();

        /// <summary>
        /// Identifier that distinguishes "outputs from a different invocation that happens to
        /// share these paths" from "outputs from this invocation that I should reuse": a hash
        /// of every input and setting the outputs depend on. Embedded in each output's stamp by
        /// its writer; checked on the next invocation before deciding whether to skip
        /// <see cref="Run"/>.
        /// </summary>
        public abstract string ValidityKey(TaskContext ctx);

        /// <summary>
        /// The key one declared output is stamped and checked with: <paramref name="taskKey"/>
        /// (this task's <see cref="ValidityKey"/>, computed once by the caller) for every task
        /// whose outputs all depend on the same inputs. A fan-out task whose output for one run
        /// also depends on that run's own artifacts appends their identities here, so a
        /// rewritten input invalidates that run's output alone.
        /// </summary>
        public virtual string OutputValidityKey(TaskContext ctx, string taskKey, string output) => taskKey;

        /// <summary>
        /// The <see cref="ArtifactStamp"/> this task embeds in an output it writes now: this
        /// task's name, the context's build version and its <see cref="ValidityKey"/>. A writer
        /// passes it into the same atomic commit as the content, so the two cannot disagree.
        /// </summary>
        public ArtifactStamp OutputStamp(TaskContext ctx)
        {
            return ctx.Stamp(Name, ValidityKey(ctx));
        }

        /// <summary>
        /// The stamp for one declared output whose key is its own
        /// (<see cref="OutputValidityKey"/>) rather than the task's.
        /// </summary>
        public ArtifactStamp OutputStamp(TaskContext ctx, string output)
        {
            return ctx.Stamp(Name, OutputValidityKey(ctx, ValidityKey(ctx), output));
        }
    }
}
