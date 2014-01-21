/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Interface for utility for measuring performance
    /// 
    /// create one of these at the start of your operation, then
    /// ask it for timers via CreateTimer to get information about
    /// duration of subtasks in the operation.
    /// When all is done, use GetLog() to obtain a CSV-formatted
    /// multiline string with performance info - min, max and average times
    /// for each repeated subtask.
    /// </summary>
    public interface IPerfUtil
    {
        IPerfUtilTimer CreateTimer(string name);
        string GetLog();
    }

    public interface IPerfUtilTimer : IDisposable
    {
    }

    /// <summary>
    /// implementation of IPerfUtil that does nothing
    /// </summary>
    public class PerfUtilDummy : IPerfUtil
    {
        public PerfUtilDummy(string name)
        {
        }
        public IPerfUtilTimer CreateTimer(string name)
        {
            return null;
        }
        public string GetLog()
        {
            return null;
        }
    }

    /// <summary>
    /// implementation of IPerfUtil that actually does measurements
    /// </summary>
    public class PerfUtilActual : IPerfUtil
    {
        /// <summary>
        /// this object manages the timers for measuring performance
        /// </summary>
        /// <param name="name">the name you want to appear in the log string</param>
        public PerfUtilActual(string name)
        {
            _name = cleanupName(name);
            _perftimersList = new List<KeyValuePair<string, long>>
            {
                new KeyValuePair<string, long>("", DateTime.Now.Ticks) // note creation time
            };
            _callstack = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        }

        /// <summary>
        /// start a timer for some scope of operation (nesting is OK!)
        /// **always** employ using() for these - the timer stops when the object is disposed.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public IPerfUtilTimer CreateTimer(string name)
        {
            return new Timer(name, this);
        }

        private readonly List<KeyValuePair<string, long>> _perftimersList;
        private readonly List<int> _callstack;
        private readonly string _name;

        static private string cleanupName(string name)
        {
            name = name.Replace(':', '_'); // colon is reserved
            name = name.Replace(',', ';'); // comma is reserved
            return name;
        }

        private sealed class Timer : IPerfUtilTimer
        {
            private readonly int _startIndex;
            private readonly PerfUtilActual _parent;

            public Timer(string name, PerfUtilActual parent) // start timer
            {
                _parent = parent;
                if (_parent._perftimersList != null)
                {
                    _startIndex = _parent._perftimersList.Count; // note where we began
                    // find outer event, set name as outer:name
                    int calldepth = parent._perftimersList[_startIndex - 1].Key.Count(x => x == ':');
                    if (parent._perftimersList[_startIndex - 1].Key.EndsWith("%"))
                        calldepth--;
                    _parent._callstack[calldepth + 1] = _startIndex;
                    name = cleanupName(name); // watch for reserved characters in name
                    name = _parent._perftimersList[_parent._callstack[calldepth]].Key + " : " + name;
                    // add this timer start event to the parent's list of timer events
                    _parent._perftimersList.Add(new KeyValuePair<string, long>(name, DateTime.Now.Ticks));
                }
            }

            public void Dispose() // end timer
            {
                if (_parent._perftimersList != null)
                {
                    // add this timer stop event to the parent's list of timer events
                    _parent._perftimersList.Add(
                        new KeyValuePair<string, long>(_parent._perftimersList[_startIndex].Key + "%",
                        DateTime.Now.Ticks - _parent._perftimersList[_startIndex].Value));
                }
            }
        }

        /// <summary>
        /// use this to obtain a CSV-formatted
        /// multiline string with performance info - min, max and average times
        /// for each repeated subtask.
        /// </summary>
        /// <returns>CSV-formatted multiline string with performance info</returns>
        public string GetLog()
        {
            // did the list close?
            if (_perftimersList.Last().Key != _perftimersList.First().Key + "%")
            {
                _perftimersList.Add(new KeyValuePair<string, long>(_perftimersList[0].Key + "%",
                   DateTime.Now.Ticks - _perftimersList[0].Value)); 
            }
            // construct a report
            var times = new Dictionary<string, List<long>>();
            foreach (var keypair in _perftimersList)
            {
                if (keypair.Key.EndsWith("%"))
                {
                    string keyname = keypair.Key.Substring(0, keypair.Key.Length - 1);
                    if (0 == keyname.Count(x => x == ':'))
                    {
                        keyname += " : lifetime"; // that's the root node
                    }
                    if (!times.ContainsKey(keyname))
                    {
                        times.Add(keyname, new List<long> { keypair.Value });
                    }
                    else
                    {
                        times[keyname].Add(keypair.Value);
                    }
                }
            }
            // assemble a report for each method 
            string log = "Performance stats for " + _name + ":\r\nmethod,msecWithoutChildCalls,pctWithoutChildCalls,nCalls,msecAvg,msecMax,msecMin\r\n"; // Not L10N
            foreach (var t in times)
            {
                // total the leaf times to determine the unaccounted time
                long leaftime = 0;
                foreach (var l in times)
                {
                    if (l.Key.StartsWith(t.Key) && (l.Key.Count(x => x == ':') == t.Key.Count(x => x == ':') + 1))
                    {  // immediate child, note its total time
                        leaftime += l.Value.Sum();
                    }
                }
                string key = t.Key;
                if (key.StartsWith(" : "))
                {
                    key = key.Substring(3);
                }
                string info = String.Format("{0},{1},{2},{3},{4},{5},{6}\r\n", key,
                    (double)(t.Value.Sum() - leaftime) / TimeSpan.TicksPerMillisecond,
                    (t.Value.Sum()!=0) ? 100.0 * (double)(t.Value.Sum() - leaftime) / t.Value.Sum() : 100.0,
                    t.Value.Count(),
                    t.Value.Average() / TimeSpan.TicksPerMillisecond, (double)t.Value.Max() / TimeSpan.TicksPerMillisecond,
                    (double)t.Value.Min() / TimeSpan.TicksPerMillisecond);
                log += info;
            }
            return log;
        }
    }

    /// <summary>
    /// allows us, for example, to turn MsDataFileImpl performance measurement on or off with ease
    /// </summary>
    public class PerfUtilFactory
    {
        public bool IssueDummyPerfUtils { set; get; }

        public PerfUtilFactory()
        {
            IssueDummyPerfUtils = true; // default to doing no work
        }

        public PerfUtilFactory(bool issueDummyPerfUtils)
        {
            IssueDummyPerfUtils = issueDummyPerfUtils; // "true" means do no work
        }

        public IPerfUtil CreatePerfUtil(string name)
        {
            if (IssueDummyPerfUtils)
            {
                return new PerfUtilDummy(name);
            }
            else
            {
                return new PerfUtilActual(name);
            }
        }
    }
}
