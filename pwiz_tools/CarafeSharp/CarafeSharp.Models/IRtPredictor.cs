/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using pwiz.CarafeSharp.Core;

namespace pwiz.CarafeSharp.Models
{
    /// <summary>
    /// A retention-time model as library prediction uses it: one finite prediction per peptide form on the
    /// model's own scale, and the linear map from that scale onto iRT.
    /// </summary>
    public interface IRtPredictor : IDisposable
    {
        /// <summary>Predictions in input order.</summary>
        double[] Predict(IReadOnlyList<PeptideForm> peptides);

        /// <summary>The line from this model's scale to iRT, fitted on the iRT kit peptides.</summary>
        (double Slope, double Intercept) FitIrtCalibration();
    }
}
