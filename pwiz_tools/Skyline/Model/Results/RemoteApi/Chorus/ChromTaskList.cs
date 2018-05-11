/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Net;
using System.Threading;
using pwiz.Skyline.Model.Results.RemoteApi.GeneratedCode;

namespace pwiz.Skyline.Model.Results.RemoteApi.Chorus
{
    public class ChromTaskList
    {
        private const int CANCEL_CHECK_MILLIS = 1000;
        private HashSet<ChromatogramGeneratorTask> _executingTasks;
        private List<ChromatogramGeneratorTask> _chromatogramGeneratorTasks;
        private IDictionary<ChromKey, ChromatogramGeneratorTask> _chromKeys;
        private int _completedCount;
        private int _minTaskCount;
        private readonly Action _checkCancelledAction;
        private Exception _exception;

        public ChromTaskList(Action checkCancelledAction, SrmDocument srmDocument, ChorusAccount chorusAccount, ChorusUrl chorusUrl, IEnumerable<ChromatogramRequestDocument> chromatogramRequestDocuments)
        {
            SrmDocument = srmDocument;
            ChorusSession = new ChorusSession(chorusAccount);
            _checkCancelledAction = checkCancelledAction;
            _chromatogramGeneratorTasks = new List<ChromatogramGeneratorTask>();
            _chromKeys = new Dictionary<ChromKey, ChromatogramGeneratorTask>();
            foreach (var chunk in chromatogramRequestDocuments)
            {
                ChromatogramGeneratorTask task = new ChromatogramGeneratorTask(this, chorusAccount, chorusUrl, chunk);
                _chromatogramGeneratorTasks.Add(task);
                foreach (ChromKey chromKey in ListChromKeys(chunk))
                {
                    _chromKeys[chromKey] = task;
                }
            }
            _executingTasks = new HashSet<ChromatogramGeneratorTask>();
        }

        public void SetMinimumSimultaneousTasks(int minTaskCount)
        {
            _minTaskCount = minTaskCount;
            EnsureMinTasksRunning();
        }

        public ChorusSession ChorusSession { get; private set; }

        private void CheckCancelled()
        {
            try
            {
                _checkCancelledAction();
            }
            catch
            {
                ChorusSession.Dispose();
                throw;
            }
        }

        public SrmDocument SrmDocument { get; private set; }

        internal void OnTaskCompleted(ChromatogramGeneratorTask chromatogramGeneratorTask)
        {
            lock (LockObj)
            {
                _completedCount++;
                _executingTasks.Remove(chromatogramGeneratorTask);
                Monitor.PulseAll(LockObj);
                EnsureMinTasksRunning();
            }
        }

        public object LockObj { get { return this; }}

        public int TaskCount
        {
            get { return _chromatogramGeneratorTasks.Count; }
        }

        public int CompletedCount { get
        {
            lock (LockObj)
            {
                return _completedCount;
            }
        } 
        }

        public int PercentComplete
        {
            get
            {
                lock (LockObj)
                {
                    return _completedCount*100/_chromatogramGeneratorTasks.Count;
                }
            }
        }

        public IEnumerable<ChromKeyProviderIdPair> ChromIds
        {
            get { return _chromKeys.Select((key, index) => new ChromKeyProviderIdPair(key.Key, index)); }
        }

        public bool GetChromatogram(ChromKey chromKey, out TimeIntensities timeIntensities)
        {
            ChromatogramGeneratorTask task;
            if (!_chromKeys.TryGetValue(chromKey, out task))
            {
                timeIntensities = null;
                return false;
            }
            lock (LockObj)
            {
                StartTask(task);
                while (!task.IsFinished())
                {
                    Monitor.Wait(LockObj, CANCEL_CHECK_MILLIS);
                    CheckCancelled();
                }
                if (null != _exception)
                {
                    throw new RemoteServerException(_exception.Message, _exception);
                }
            }
            return task.GetChromatogram(chromKey, out timeIntensities);
        }

        private void EnsureMinTasksRunning()
        {
            while (true)
            {
                lock (this)
                {
                    CheckCancelled();
                    int targetTaskCount = Math.Min(_minTaskCount, _chromatogramGeneratorTasks.Count - _completedCount);
                    if (_executingTasks.Count >= targetTaskCount)
                    {
                        return;
                    }
                    var taskToRun = _chromatogramGeneratorTasks.FirstOrDefault(task => !task.IsStarted());
                    if (null == taskToRun)
                    {
                        return;
                    }
                    StartTask(taskToRun);
                }
            }

        }
        private void StartTask(ChromatogramGeneratorTask task)
        {
            lock (this)
            {
                if (task.IsStarted())
                {
                    return;
                }
                task.Start();
                _executingTasks.Add(task);
            }
        }

        internal static IEnumerable<ChromKey> ListChromKeys(ChromatogramRequestDocument chromatogramRequestDocument)
        {
            return ChromatogramRequestProvider.ListChromKeys(chromatogramRequestDocument);
        }

        public static List<ChromatogramRequestDocument> ChunkChromatogramRequest(ChromatogramRequestDocument chromatogramRequestDocument, int targetChromatogramCount)
        {
            var chunks = new List<ChromatogramRequestDocument>();
            List<ChromatogramRequestDocumentChromatogramGroup> currentGroups = new List<ChromatogramRequestDocumentChromatogramGroup>();
            int currentChromatogramCount = 0;
            foreach (var chromatogramGroup in chromatogramRequestDocument.ChromatogramGroup)
            {
                currentGroups.Add(chromatogramGroup);
                currentChromatogramCount += chromatogramGroup.Chromatogram.Length;
                if (currentChromatogramCount >= targetChromatogramCount)
                {
                    chunks.Add(chromatogramRequestDocument.CloneWithChromatogramGroups(currentGroups));
                    currentGroups.Clear();
                    currentChromatogramCount = 0;
                }
            }
            if (currentGroups.Any())
            {
                chunks.Add(chromatogramRequestDocument.CloneWithChromatogramGroups(currentGroups));
            }
            return chunks;
        }

        public IList<Exception> ListExceptions()
        {
            Exception exception = _exception;
            return null == exception ? new Exception[0] : new[] {_exception};
        }

        public IList<ChromatogramGeneratorTask> ListTasks()
        {
            return _chromatogramGeneratorTasks.AsReadOnly();
        }

        public IEnumerable<ChromKey> ChromKeys {get { return _chromKeys.Keys; }}

        public ChromatogramGeneratorTask GetGeneratorTask(ChromKey chromKey)
        {
            ChromatogramGeneratorTask task;
            _chromKeys.TryGetValue(chromKey, out task);
            return task;
        }

        public void HandleException(Exception exception)
        {
            var webException = exception as WebException;
            if (null != webException)
            {
                exception = ChorusSession.WrapWebException(webException);
            }
            lock (LockObj)
            {
                if (null != _exception)
                {
                    return;
                }
                _exception = exception;
                ChorusSession.Abort();
                Monitor.PulseAll(LockObj);
            }
        }

        public interface IRemoteChromLoadMonitor
        {
            void CheckCancelled();
            void HandleError(Exception exception);
        }

        public class DefaultChromLoadMonitor : IRemoteChromLoadMonitor
        {
            public void CheckCancelled()
            {
            }

            public void HandleError(Exception exception)
            {
                throw new RemoteServerException(exception.Message, exception);
            }
        }
    }
}
