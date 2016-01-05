/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model
{
    public class MultiProgressStatus : Immutable, IProgressStatus
    {
        private readonly bool _synchronousMode;

        public MultiProgressStatus(bool synchronousMode)
        {
            _synchronousMode = synchronousMode;
            ProgressList = ImmutableList<ChromatogramLoadingStatus>.EMPTY;
            Id = new object();
            Tag = 0;
        }

        public IProgressStatus ChangePercentComplete(int percent)
        {
            throw new NotImplementedException();
        }

        public IProgressStatus ChangeMessage(string prop)
        {
            throw new NotImplementedException();
        }

        public IProgressStatus Complete()
        {
            throw new NotImplementedException();
        }

        IProgressStatus IProgressStatus.ChangeErrorException(Exception prop)
        {
            throw new NotImplementedException();
        }

        public IProgressStatus ChangeSegments(int segment, int segmentCount)
        {
            throw new NotImplementedException();
        }

        public IProgressStatus NextSegment()
        {
            throw new NotImplementedException();
        }

        public IProgressStatus UpdatePercentCompleteProgress(IProgressMonitor progressMonitor, long currentCount, long totalCount)
        {
            throw new NotImplementedException();
        }

        public bool IsPercentComplete(int percent)
        {
            throw new NotImplementedException();
        }

        public string Message { get { return string.Empty; } }
        public ImmutableList<ChromatogramLoadingStatus> ProgressList { get; private set; }

        public Exception ErrorException
        {
            get
            {
                MultiException exception = null;
                foreach (var progressStatus in ProgressList)
                {
                    if (progressStatus.IsError)
                    {
                        if (exception == null)
                            exception = new MultiException();
                        exception.Add(progressStatus.ErrorException);
                    }
                }
                return exception;
            }
        }

        public int SegmentCount { get { return 1; } }
        public object Id { get; private set; }
        public int Tag { get; private set; }

        public bool IsFinal { get
        {
            var state = State; 
            return state != ProgressState.begin && state != ProgressState.running; }
        }
        public bool IsComplete { get { return State == ProgressState.complete; } }
        public bool IsError { get { return State == ProgressState.error; } }
        public bool IsCanceled { get { return State == ProgressState.cancelled; } }
        public bool IsBegin { get { return State == ProgressState.begin; } }

        public MultiProgressStatus Add(ChromatogramLoadingStatus status)
        {
            return ChangeProp(ImClone(this), s =>
            {
                s.ProgressList = ImmutableList.ValueOf(ProgressList.Concat(new[]{status}));
            });
        }

        public MultiProgressStatus Remove(IProgressStatus status)
        {
            return ChangeProp(ImClone(this), s =>
            {
                s.ProgressList = ImmutableList.ValueOf(ProgressList.Where(e => !ReferenceEquals(e.Id, status.Id)));
            });
        }

        public ProgressState State
        {
            get
            {
                bool begin = true;
                bool anyBegin = false;
                bool cancelled = false;
                bool error = false;
                foreach (var progressStatus in ProgressList)
                {
                    switch (progressStatus.State)
                    {
                        case ProgressState.begin:
                            anyBegin = true;
                            break;
                        case ProgressState.running:
                            return ProgressState.running;
                        case ProgressState.complete:
                            begin = false;
                            break;
                        case ProgressState.cancelled:
                            begin = false;
                            cancelled = true;
                            break;
                        case ProgressState.error:
                            if (_synchronousMode)
                                return ProgressState.error;
                            begin = false;
                            error = true;
                            break;
                    }
                }
                return 
                    begin ? ProgressState.begin : 
                    anyBegin ? ProgressState.running : 
                    error ? ProgressState.error :
                    cancelled ? ProgressState.cancelled : 
                    ProgressState.complete;
            }
        }

        public int PercentComplete
        {
            get
            {
                if (ProgressList.Count == 0)
                    return 0;
                int percent = 0;
                foreach (var progressStatus in ProgressList)
                    percent += progressStatus.PercentComplete;
                return percent / ProgressList.Count;
            }
        }

        public MultiProgressStatus ChangeStatus(ChromatogramLoadingStatus status)
        {
            var progressList = new List<ChromatogramLoadingStatus>(ProgressList.Count);
            foreach (var progressStatus in ProgressList)
                progressList.Add(ReferenceEquals(status.Id, progressStatus.Id) ? status : progressStatus);
            return ChangeProp(ImClone(this), s => s.ProgressList = ImmutableList.ValueOf(progressList));
        }

        public IProgressStatus Cancel()
        {
            var progressList = new List<ChromatogramLoadingStatus>(ProgressList.Count);
            foreach (var progressStatus in ProgressList)
                progressList.Add((ChromatogramLoadingStatus)progressStatus.Cancel());
            return ChangeProp(ImClone(this), s =>
            {
                s.ProgressList = ImmutableList.ValueOf(progressList);
            });
        }

        public IProgressStatus GetStatus(MsDataFileUri filePath)
        {
            foreach (var loadingStatus in ProgressList)
            {
                if (loadingStatus.FilePath.Equals(filePath))
                    return loadingStatus;
            }
            return null;
        }
    }

    public class MultiException : Exception
    {
        public List<Exception> Exceptions { get; private set; }

        public MultiException()
        {
            Exceptions = new List<Exception>();
        }

        public void Add(Exception exception)
        {
            Exceptions.Add(exception);
        }

        public override string Message
        {
            get { return Exceptions[0].Message; }
        }

        public override string ToString()
        {
            return Exceptions[0].ToString();
        }
    }
}
