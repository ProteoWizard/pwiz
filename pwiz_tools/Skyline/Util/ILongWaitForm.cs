/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// A form that, like a <see cref="pwiz.Skyline.Controls.LongWaitDlg"/>, may be showing the
    /// progress of a long-running operation that it (not the connector) drives. The connector's
    /// no-progress watchdog treats such a form the same way it treats a LongWaitDlg: while
    /// <see cref="IsBusy"/> is true, an actively-pumping message loop with no other sign of
    /// completion is taken as work advancing rather than a hang, so the watchdog does NOT trip.
    ///
    /// A <see cref="pwiz.Skyline.Controls.LongWaitDlg"/> exists only while working, so its
    /// <see cref="IsBusy"/> is always true. A multi-step form such as the Import Peptide Search
    /// wizard is busy only while it is running a background operation in its own progress display.
    /// </summary>
    public interface ILongWaitForm
    {
        /// <summary>
        /// True while the form is performing long-running work during which the connector must not
        /// trip its no-progress watchdog (there is no LongWaitDlg because the form shows the progress
        /// itself). False when the form is idle and simply waiting on the user.
        /// </summary>
        bool IsBusy { get; }
    }
}
