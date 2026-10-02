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
using System.IO;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Why a <c>.spectra.bin</c> cache was refused. The reasons are NOT interchangeable and
    /// their remedies differ - <see cref="Absent"/> usually means the cache is somewhere else
    /// (<c>--cache-dir</c>), while <see cref="SourceChanged"/> means the one that is there must
    /// be rebuilt - so a caller that fails on a refusal has to be able to say which fired.
    ///
    /// <para>This exists because it did not. <c>SpectraWindowIndex.BuildFromCache</c> collapsed
    /// all six conditions into a null, and the only caller that treats a refusal as fatal could
    /// therefore say no more than "absent, stale, or wrong version" - a list that omitted two of
    /// the six and whose suggested remedy ("re-run PerFileScoring") was wrong for the case that
    /// actually occurred. A run then spent 13 minutes reaching a one-line failure that did not
    /// say what to fix.</para>
    /// </summary>
    public enum SpectraCacheRejection
    {
        /// <summary>The cache was accepted.</summary>
        None,
        /// <summary>No file at the resolved cache path.</summary>
        Absent,
        /// <summary>Fewer than 8 bytes of magic - the file exists but is truncated or empty.</summary>
        TruncatedHeader,
        /// <summary>Magic bytes are not <c>OSPRSPC\0</c>: this is not a spectra cache at all.</summary>
        NotASpectraCache,
        /// <summary>
        /// A spectra cache written in a different FORMAT version (the header's own
        /// <c>uint</c>, currently 4). Unrelated to the Osprey build stamp, so
        /// <c>OSPREY_VERSION_OVERRIDE</c> neither causes nor cures it.
        /// </summary>
        WrongFormatVersion,
        /// <summary>
        /// The writer could not measure the source file and stored the unmeasurable sentinel, so
        /// the cache can never be validated against its source and is refused rather than
        /// trusted forever after one I/O error.
        /// </summary>
        FingerprintUnmeasurableAtWrite,
        /// <summary>The source file exists now but cannot be measured, so staleness is unknowable.</summary>
        SourceUnmeasurable,
        /// <summary>
        /// The source file's size or last-write time no longer matches what the cache recorded:
        /// the data changed underneath it and the cache is stale.
        /// </summary>
        SourceChanged
    }

    /// <summary>
    /// Thrown when a stage that REQUIRES a <c>.spectra.bin</c> cache cannot use the one it
    /// resolved. Carries the machine-readable <see cref="Reason"/> and the
    /// <see cref="CachePath"/> alongside the message, so a caller can branch on the cause
    /// instead of matching on prose.
    ///
    /// <para>An <see cref="IOException"/>: every refusal is a problem with a file - absent,
    /// unreadable, too short, or stale against its source - and the message is written for the
    /// user, so it is reported as a message rather than a defect (Skyline's
    /// <c>IsProgrammingDefect</c>). It replaced an <see cref="InvalidDataException"/>, which is
    /// sealed. A caller that wants to recover should catch THIS type and branch on
    /// <see cref="Reason"/>.</para>
    /// </summary>
    public class SpectraCacheException : IOException
    {
        public SpectraCacheException(string message, SpectraCacheRejection reason, string cachePath)
            : base(message)
        {
            Reason = reason;
            CachePath = cachePath;
        }

        public SpectraCacheException(string message, SpectraCacheRejection reason, string cachePath,
            Exception innerException)
            : base(message, innerException)
        {
            Reason = reason;
            CachePath = cachePath;
        }

        /// <summary>Which validation rule refused the cache.</summary>
        public SpectraCacheRejection Reason { get; }

        /// <summary>The path that was probed, as resolved by the caller.</summary>
        public string CachePath { get; }

        /// <summary>
        /// One operator-facing clause naming what is wrong with THIS cache. Kept beside the enum
        /// rather than at the throw site so every future caller reports a reason identically
        /// instead of re-wording the same six cases.
        /// </summary>
        public static string Describe(SpectraCacheRejection reason)
        {
            switch (reason)
            {
                case SpectraCacheRejection.Absent:
                    return OspreyIOResources.SpectraCacheException_Describe_no_file_exists_at_that_path;
                case SpectraCacheRejection.TruncatedHeader:
                    return OspreyIOResources.SpectraCacheException_Describe_the_file_is_truncated___it_is_too_short_to_hold_a_cache_header;
                case SpectraCacheRejection.NotASpectraCache:
                    return OspreyIOResources.SpectraCacheException_Describe_the_file_is_not_a_spectra_cache__wrong_magic_bytes_;
                case SpectraCacheRejection.WrongFormatVersion:
                    return OspreyIOResources.SpectraCacheException_Describe_the_file_was_written_in_an_older_spectra_cache_format_and_must_be_rebuilt;
                case SpectraCacheRejection.FingerprintUnmeasurableAtWrite:
                    return OspreyIOResources.SpectraCacheException_Describe_the_cache_was_written_without_a_usable_source_fingerprint__so_it_can_never_be_validated_;
                case SpectraCacheRejection.SourceUnmeasurable:
                    return OspreyIOResources.SpectraCacheException_Describe_the_source_file_cannot_be_measured__so_the_cache_cannot_be_checked_for_staleness;
                case SpectraCacheRejection.SourceChanged:
                    return OspreyIOResources.SpectraCacheException_Describe_the_source_file_s_size_or_timestamp_has_changed_since_the_cache_was_written__so_the_cache_;
                default:
                    return OspreyIOResources.SpectraCacheException_Describe_the_cache_was_refused_for_an_unrecorded_reason;
            }
        }
    }
}
