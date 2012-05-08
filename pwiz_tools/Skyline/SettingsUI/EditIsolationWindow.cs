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

using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.SettingsUI
{
    public class EditIsolationWindow
    {
        public double? Start { get; set; }
        public double? End { get; set; }
        public double? Target { get; set; }
        public double? StartMargin { get; set; }
        public double? EndMargin { get; set; }

        public EditIsolationWindow()
        {
        }

        public EditIsolationWindow(IsolationWindow window)
        {
            Start = window.Start;
            End = window.End;
            Target = window.Target;
            StartMargin = window.StartMargin;
            EndMargin = window.EndMargin;
        }

        public void Validate()
        {
            // Construct IsolationWindow to perform validation.
            new IsolationWindow(this);
        }

        #region object overrides

        public bool Equals(EditIsolationWindow other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return other.Start.Equals(Start) && other.End.Equals(End) && other.Target.Equals(Target) && other.StartMargin.Equals(StartMargin) && other.EndMargin.Equals(EndMargin);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (EditIsolationWindow)) return false;
            return Equals((EditIsolationWindow) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = Start.GetHashCode();
                result = (result*397) ^ (End.HasValue ? End.Value.GetHashCode() : 0);
                result = (result*397) ^ (Target.HasValue ? Target.Value.GetHashCode() : 0);
                result = (result*397) ^ (StartMargin.HasValue ? StartMargin.Value.GetHashCode() : 0);
                result = (result*397) ^ (EndMargin.HasValue ? EndMargin.Value.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
}
