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
using System.Globalization;
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
                new KeyValuePair<string, long>(string.Empty, DateTime.Now.Ticks) // note creation time
            };
            _callstack = new List<int> { 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        }

        /// <summary>
        /// start a timer for some scope of operation (nesting is OK!)
        /// **always** employ using() for these - the timer stops when the object is disposed.
        /// </summary>
        /// <param name="name"></param>
        public IPerfUtilTimer CreateTimer(string name)
        {
            return new Timer(name, this);
        }

        private readonly List<KeyValuePair<string, long>> _perftimersList;
        private readonly List<int> _callstack;
        private readonly string _name;
        public const string HEADERLINE_TITLE = "Performance stats for "; // Not L10N
        public const string HEADERLINE_COLUMNS =
            "method,msecWithoutChildCalls,pctWithoutChildCalls,nCalls,msecAvg,msecMax,msecMin"; // Not L10N
        public const string CSVLINE_FORMAT = "{0},{1},{2},{3},{4},{5},{6}\r\n"; // Not L10N

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
                    if (parent._perftimersList[_startIndex - 1].Key.EndsWith("%")) // Not L10N
                        calldepth--;
                    _parent._callstack[calldepth + 1] = _startIndex;
                    name = cleanupName(name); // watch for reserved characters in name
                    name = _parent._perftimersList[_parent._callstack[calldepth]].Key + " : " + name; // Not L10N
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
                        new KeyValuePair<string, long>(_parent._perftimersList[_startIndex].Key + "%", // Not L10N
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
            if (_perftimersList.Last().Key != _perftimersList.First().Key + "%") // Not L10N
            {
                _perftimersList.Add(new KeyValuePair<string, long>(_perftimersList[0].Key + "%", // Not L10N
                   DateTime.Now.Ticks - _perftimersList[0].Value)); 
            }
            // construct a report
            var times = new Dictionary<string, List<long>>();
            foreach (var keypair in _perftimersList)
            {
                if (keypair.Key.EndsWith("%")) // Not L10N
                {
                    string keyname = keypair.Key.Substring(0, keypair.Key.Length - 1);
                    if (0 == keyname.Count(x => x == ':'))
                    {
                        keyname += " : lifetime"; // that's the root node // Not L10N
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
            string log = HEADERLINE_TITLE + _name + ":\r\n"+HEADERLINE_COLUMNS+"\r\n"; // Not L10N
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
                if (key.StartsWith(" : ")) // Not L10N
                {
                    key = key.Substring(3);
                }
                string info = string.Format(CultureInfo.InvariantCulture, CSVLINE_FORMAT, key,
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

        /// <summary>
        /// produce a summary of a log with repeating themes
        /// </summary>
        /// <param name="logs">the log</param>
        /// <param name="splits">used to combine events - for example if splits 
        /// was ["foo"] then events "bam\foo\bar" and "bam\foo\baz" would combine into
        /// as single event "bam"</param>
        /// <returns></returns>
        public static string SummarizeLogs(IList<String> logs,IList<string> splits)
        {
            var perfItems = new Dictionary<string,PerfItem>();
            const int nameColumn = 0;
            const int durationColumn = 1;
            PerfItem curPerfItem = null;
            foreach (var lines in logs)
            {
                foreach (var subline in lines.Split('\n'))
                {
                    var line = subline;
                    if (line.StartsWith(PerfUtilActual.HEADERLINE_TITLE))
                    {
                        // if split on bar, combine /foo/bar/baz with foo/bar/buz as foo/bar
                        foreach (var s in splits)
                        {
                            var sline = line.Split(new []{s.Replace('/','\\')}, StringSplitOptions.None)[0];
                            if (sline != line)
                                line = sline + s;
                        }
                        // Note any special settings like lockmass or centroiding
                        foreach (var tweak in  new[]
                            { ";lockmassCorrection_True", ";requireVendorCentroidedMS1_True", ";requireVendorCentroidedMS2_True" })  // Not L10N
                        {
                            if (subline.Contains(tweak))
                                line += tweak;
                        }
                        // and strip the leading boilerplate
                        line = line.Substring(PerfUtilActual.HEADERLINE_TITLE.Length).Trim();
                        if (!perfItems.ContainsKey(line))
                        {
                            perfItems.Add(line,new PerfItem {ReplicateCount=0, itemStats = new Dictionary<string, List<double>>() });
                        }
                        curPerfItem = perfItems[line];
                        curPerfItem.ReplicateCount++;
                    }
                    else if (curPerfItem != null)
                    {
                        var columns = line.Split(',');
                        double duration;
                        if ((columns.Count() == PerfUtilActual.CSVLINE_FORMAT.Split(',').Count()) && // looks like one of ours
                            Double.TryParse(columns[durationColumn], NumberStyles.Any, CultureInfo.InvariantCulture, out duration))
                        {
                            string item = columns[nameColumn]; // function name
                            if (curPerfItem.itemStats.ContainsKey(item))
                                curPerfItem.itemStats[item].Add(duration);
                            else
                                curPerfItem.itemStats.Add(item,new List<double>{duration});
                        }
                    }
                }
            }
            string result = string.Empty;
            if (perfItems.Count>0)
            {
                result = string.Empty;
                foreach (var perfItem in perfItems)
                {
                    if (result == string.Empty)
                    {
                        // first one, grab subheadings (yes, this assumes they are all the same throughout)
                        // and lay them out as columns along with name
                        result = "\r\nname"; // Not L10N
                        foreach (var pair in perfItem.Value.itemStats)
                            result += ("," + pair.Key); // Not L10N
                        result += "\r\n"; // Not L10N
                    }
                    result += perfItem.Key;
                    foreach (var pair in perfItem.Value.itemStats)
                        result += string.Format(CultureInfo.InvariantCulture, ",{0}", pair.Value.Sum() / perfItem.Value.ReplicateCount); // Not L10N
                    result += "\r\n"; // Not L10N
                }
            }
            return result;
        }
    }

    internal class PerfItem
    {
        public int ReplicateCount { get; set; } // not quite the same thing as itemstats length
        public Dictionary<string, List<double>> itemStats { get; set; }
    }
}
