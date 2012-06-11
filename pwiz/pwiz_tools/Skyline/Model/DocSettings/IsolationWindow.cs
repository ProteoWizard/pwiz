/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.DocSettings
{
    [XmlRoot("isolation_window")]
    public sealed class IsolationWindow : Immutable, IXmlSerializable
    {
        public double Start { get; private set; }
        public double End { get; private set; }
        public double? Target { get; private set; }
        public double? StartMargin { get; private set; }
        public double? EndMargin { get; private set; }

        public double MethodStart { get { return Math.Max(Start - (StartMargin ?? 0), TransitionFullScan.MIN_RES_MZ); } }
        public double MethodEnd { get { return Math.Min(End + (EndMargin ?? (StartMargin ?? 0)), TransitionFullScan.MAX_RES_MZ); } }
        public double MethodCenter { get { return (MethodStart + MethodEnd)/2; } }

        public double IsolationStart { get { return MethodStart + (StartMargin ?? 0); } }
        public double IsolationEnd { get { return MethodEnd - (EndMargin ?? (StartMargin ?? 0)); } }

        public IsolationWindow(double start, double end, double? target = null, double? startMargin = null, double? endMargin = null)
        {
            Start = start;
            End = end;
            Target = target;
            StartMargin = startMargin;
            EndMargin = endMargin;

            DoValidate();
        }

        public IsolationWindow(EditIsolationWindow isolationWindow)
        {
            Start = isolationWindow.Start.HasValue ? isolationWindow.Start.Value : TransitionFullScan.MIN_RES_MZ;
            End = isolationWindow.End.HasValue ? isolationWindow.End.Value : TransitionFullScan.MAX_RES_MZ;
            Target = isolationWindow.Target;
            StartMargin = isolationWindow.StartMargin;
            EndMargin = isolationWindow.EndMargin;

            DoValidate();
        }

        public bool TargetMatches(double isolationTarget, double mzMatchTolerance)
        {
            if (!Target.HasValue)
                throw new InvalidDataException("Isolation window requires a Target value.");
            return Math.Abs(isolationTarget - Target.Value) <= mzMatchTolerance &&
                Contains(isolationTarget);
        }

        public bool Contains(double isolationTarget)
        {
            return Start <= isolationTarget && isolationTarget < End;
        }

        private void DoValidate()
        {
            TransitionFullScan.ValidateRange(Start, TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER, TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER,
                "Isolation window Start must be between {0} and {1}.");
            TransitionFullScan.ValidateRange(End, TransitionFullScan.MIN_PRECURSOR_MULTI_FILTER, TransitionFullScan.MAX_PRECURSOR_MULTI_FILTER,
                "Isolation window End must be between {0} and {1}.");

            if (Start >= End)
            {
                throw new InvalidDataException("Isolation window Start value is greater than the End value.");
            }
            if (Target.HasValue && (Target.Value < Start || Target.Value >= End))
            {
                throw new InvalidDataException("Target value is not within the range of the isolation window.");
            }
            if (StartMargin.HasValue)
            {
                if (StartMargin.Value < 0)
                {
                    throw new InvalidDataException("Isolation window margin must be non-negative.");
                }
                if (EndMargin.HasValue)
                {
                    if (EndMargin.Value < 0)
                    {
                        throw new InvalidDataException("Isolation window margin must be non-negative.");
                    }
                }
                if (IsolationStart >= IsolationEnd)
                {
                    // If the margins are too large, clipping at the ends of the instrument range may result in no useable window area.
                    throw new InvalidDataException(
                        "Isolation window margins cover the entire isolation window at the extremes of the instrument range.");
                }
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private IsolationWindow()
        {
        }

        public static IsolationWindow Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new IsolationWindow());
        }

        private enum ATTR
        {
            start,
            end,
            target,
            margin,
            margin_left,
            margin_right
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            Start = reader.GetDoubleAttribute(ATTR.start);
            End = reader.GetDoubleAttribute(ATTR.end);
            Target = reader.GetNullableDoubleAttribute(ATTR.target);
            StartMargin = reader.GetNullableDoubleAttribute(ATTR.margin);
            if (StartMargin == null)
            {
                StartMargin = reader.GetNullableDoubleAttribute(ATTR.margin_left);
                EndMargin = reader.GetNullableDoubleAttribute(ATTR.margin_right);
            }

            // Consume tag
            reader.Read();

            DoValidate();
        }

        public void WriteXml(XmlWriter writer)
        {
            writer.WriteAttribute(ATTR.start, Start);
            writer.WriteAttribute(ATTR.end, End);
            writer.WriteAttributeNullable(ATTR.target, Target);
            if (StartMargin != null)
            {
                if (EndMargin != null)
                {
                    writer.WriteAttributeNullable(ATTR.margin_left, StartMargin);
                    writer.WriteAttributeNullable(ATTR.margin_right, EndMargin);
                }
                else
                {
                    writer.WriteAttributeNullable(ATTR.margin, StartMargin);
                }
            }
        }

        #endregion

        #region object overrides

        public bool Equals(IsolationWindow other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.Start.Equals(Start) && other.End.Equals(End) && other.Target.Equals(Target) &&
                other.StartMargin.Equals(StartMargin) && other.EndMargin.Equals(EndMargin);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (IsolationWindow)) return false;
            return Equals((IsolationWindow) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Start.GetHashCode();
                result = (result*397) ^ End.GetHashCode();
                result = (result * 397) ^ (Target.HasValue ? Target.Value.GetHashCode() : 0);
                result = (result * 397) ^ (StartMargin.HasValue ? StartMargin.Value.GetHashCode() : 0);
                result = (result * 397) ^ (EndMargin.HasValue ? EndMargin.Value.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
}
