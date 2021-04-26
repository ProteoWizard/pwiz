/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.Text.RegularExpressions;
using pwiz.CLI.util;
using pwiz.Common.SystemUtil;

namespace pwiz.ProteowizardWrapper
{
    internal class IterationListenerToMonitor : IterationListener
    {
        private IProgressMonitor _progressMonitor;
        private IProgressStatus _status;
        private int _stepCount;

        public IterationListenerToMonitor(IProgressMonitor progressMonitor)
        {
            _progressMonitor = progressMonitor;
            _status = new ProgressStatus();
        }

        public override Status update(UpdateMessage updateMessage)
        {
            if (updateMessage.iterationCount > 0)
            {
                updateMessage.message += $@" ({updateMessage.iterationIndex + 1} / {updateMessage.iterationCount})";
                var stepMatcher = Regex.Match(updateMessage.message, @"\[step (?<step>\d+) of (?<count>\d+)]");
                int stepProgress = 0;
                if (stepMatcher.Success)
                {
                    if (_stepCount == 0)
                        _stepCount = Convert.ToInt32(stepMatcher.Groups["count"].Value);
                    stepProgress = (Convert.ToInt32(stepMatcher.Groups["step"].Value) - 1) * 100 / _stepCount;
                }
                else if (updateMessage.message.StartsWith(@"writing chromatograms"))
                    stepProgress = _stepCount * 100 / (_stepCount + 2);
                else if (updateMessage.message.StartsWith(@"writing spectra"))
                    stepProgress = (_stepCount + 1) * 100 / (_stepCount + 2);
                _status = _status.ChangePercentComplete(stepProgress + (updateMessage.iterationIndex * 100 / updateMessage.iterationCount) / _stepCount);
            }
            else
                _status = _status.ChangePercentComplete(-1);

            if (_status.Message != updateMessage.message)
            {
                _status = _status.ChangeMessage(updateMessage.message);
                _progressMonitor.UpdateProgress(_status);
            }

            return _progressMonitor.IsCanceled ? Status.Cancel : Status.Ok;
        }
    }
}