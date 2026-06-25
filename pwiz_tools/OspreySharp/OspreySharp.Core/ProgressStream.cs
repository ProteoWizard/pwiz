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
using System.IO;

namespace pwiz.OspreySharp.Core
{
    /// <summary>
    /// A read-only <see cref="Stream"/> decorator that drives a <see cref="ProgressReporter"/>
    /// by the number of bytes that have passed through it, so a long byte-oriented read (the
    /// Astral mzML parse, where the file is ~200k MS/MS spectra and the parse runs 40+ seconds
    /// with no other console output) reports a throttled percent against the total file length.
    ///
    /// Mirrors the role of Skyline's <c>pwiz.Common.SystemUtil.ProgressStream</c>, but reports
    /// through OspreySharp's lightweight <see cref="ProgressReporter"/> rather than the
    /// IProgressMonitor / ProgressStatus machinery (which OspreySharp deliberately does not yet
    /// pull into PortableUtil). Counting is the only added work on the hot path -- there are no
    /// extra buffer copies -- so it does not perturb the read throughput that overlaps with the
    /// parallel decode in <c>MzmlReader.LoadAllSpectra</c>.
    ///
    /// Forward-only: <see cref="CanSeek"/> is false (an <c>XmlReader</c> over the wrapped
    /// stream reads forward only, exactly as Skyline's ProgressStream is used). The decorator does
    /// NOT own the inner stream -- the caller keeps the inner stream in its own <c>using</c> and
    /// disposes it; disposing the ProgressStream is a no-op so an early dispose cannot truncate the
    /// underlying file handle.
    /// </summary>
    public sealed class ProgressStream : Stream
    {
        private readonly Stream _inner;
        private readonly ProgressReporter _reporter;
        private long _bytesRead;

        public ProgressStream(Stream inner, ProgressReporter reporter)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = _inner.Read(buffer, offset, count);
            _bytesRead += n;
            _reporter.Report(_bytesRead);
            return n;
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;

        public override long Position
        {
            get { return _bytesRead; }
            set { throw new NotSupportedException(); }
        }

        public override void Flush()
        {
            _inner.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
