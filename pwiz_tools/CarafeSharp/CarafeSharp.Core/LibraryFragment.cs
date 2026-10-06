/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.LibFragment
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

namespace pwiz.CarafeSharp.Core
{
    /// <summary>One fragment ion of a predicted library spectrum.</summary>
    public sealed class LibraryFragment
    {
        /// <summary>Carafe's loss type for a fragment without a neutral loss.</summary>
        public const string NO_LOSS = @"noloss";

        public LibraryFragment(char ionType, int ordinal, int charge, string lossType, double theoreticalMz,
            float mz, float relativeIntensity)
        {
            IonType = ionType;
            Ordinal = ordinal;
            Charge = charge;
            LossType = lossType;
            TheoreticalMz = theoreticalMz;
            Mz = mz;
            RelativeIntensity = relativeIntensity;
        }

        /// <summary><c>b</c> or <c>y</c>.</summary>
        public char IonType { get; }

        /// <summary>The ion number, counted from the fragment's own terminus.</summary>
        public int Ordinal { get; }

        public int Charge { get; }

        /// <summary><see cref="NO_LOSS"/>, or the neutral loss name.</summary>
        public string LossType { get; }

        public bool HasLoss
        {
            get { return LossType != NO_LOSS; }
        }

        /// <summary>The float64 m/z before alphabase rounds it to float32.</summary>
        public double TheoreticalMz { get; }

        /// <summary>The m/z Carafe writes: alphabase's float32 value.</summary>
        public float Mz { get; }

        /// <summary>Intensity relative to the most intense selected fragment.</summary>
        public float RelativeIntensity { get; }

        public override string ToString()
        {
            return string.Format(@"{0}{1}^{2}", IonType, Ordinal, Charge);
        }
    }
}
