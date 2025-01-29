/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using pwiz.Common.PeakFinding;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class FormattablePeakBounds : IFormattable
    {
        public FormattablePeakBounds(double startTime, double endTime)
        {
            StartTime = startTime;
            EndTime = endTime;
        }

        public double StartTime { get; }
        public double EndTime { get; }
        public double MidTime
        {
            get { return (StartTime + EndTime) / 2; }
        }
        public double Width
        {
            get { return EndTime - StartTime; }
        }

        protected bool Equals(FormattablePeakBounds other)
        {
            return StartTime.Equals(other.StartTime) && EndTime.Equals(other.EndTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FormattablePeakBounds)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (StartTime.GetHashCode() * 397) ^ EndTime.GetHashCode();
            }
        }

        public FormattablePeakBounds Align(AlignmentFunction alignmentFunction)
        {
            if (alignmentFunction == null)
            {
                return this;
            }

            return new FormattablePeakBounds(alignmentFunction.GetY(StartTime), alignmentFunction.GetY(EndTime));
        }

        public FormattablePeakBounds AlignPreservingWidth(AlignmentFunction alignmentFunction)
        {
            if (alignmentFunction == null)
            {
                return this;
            }

            var newMidPoint = alignmentFunction.GetY(MidTime);
            return new FormattablePeakBounds(newMidPoint - Width / 2, newMidPoint + Width / 2);
        }

        public FormattablePeakBounds ReverseAlign(AlignmentFunction alignmentFunction)
        {
            if (alignmentFunction == null)
            {
                return this;
            }

            return new FormattablePeakBounds(alignmentFunction.GetX(StartTime), alignmentFunction.GetX(EndTime));
        }

        public FormattablePeakBounds ReverseAlignPreservingWidth(AlignmentFunction alignmentFunction)
        {
            if (alignmentFunction == null)
            {
                return this;
            }

            var newMidPoint = alignmentFunction.GetX(MidTime);
            return new FormattablePeakBounds(newMidPoint - Width / 2, newMidPoint + Width / 2);
        }

        public override string ToString()
        {
            return string.Format(@"[{0},{1}]", StartTime.ToString(Formats.RETENTION_TIME),
                EndTime.ToString(Formats.RETENTION_TIME));
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            return string.Format(@"[{0},{1}]", StartTime.ToString(format, formatProvider),
                EndTime.ToString(format, formatProvider));
        }

        public PeakBounds ToPeakBounds()
        {
            return new PeakBounds(StartTime, EndTime);
        }
    }
}