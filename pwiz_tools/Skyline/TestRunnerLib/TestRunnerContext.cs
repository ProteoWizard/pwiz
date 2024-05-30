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
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TestRunnerLib
{
    public class TestRunnerContext : TestContext
    {
        private readonly Dictionary<string, string> _dictionary;

        public TestRunnerContext()
        {
            _dictionary = new Dictionary<string, string>();
        }

        public bool HasPassed { get; set; }

        public override UnitTestOutcome CurrentTestOutcome => HasPassed ? UnitTestOutcome.Passed : base.CurrentTestOutcome;

        public override void WriteLine(string format, params object[] args)
        {
            throw new NotImplementedException();
        }

        public override void AddResultFile(string fileName)
        {
            throw new NotImplementedException();
        }

        public override void BeginTimer(string timerName)
        {
            throw new NotImplementedException();
        }

        public override void EndTimer(string timerName)
        {
            throw new NotImplementedException();
        }

        public override IDictionary Properties
        {
            get { return _dictionary; }
        }

        public override DataRow DataRow
        {
            get { throw new NotImplementedException(); }
        }

        public override DbConnection DataConnection
        {
            get { throw new NotImplementedException(); }
        }
    }

    /// <summary>
    /// Test method attribute which specifies a test will be skipped until the given date.
    /// Note that the constructor expects a string explaining why the test is skipped.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class SkipTestUntilAttribute : Attribute
    {
        public DateTime SkipTestUntil { get; private set; }
        public string Reason { get; private set; } // Reason for declaring test as unsuitable for parallel use

        public SkipTestUntilAttribute(int year, int month, int day, string reason)
        {
            SkipTestUntil = new DateTime(year, month, day);
            Reason = reason; // Usually one of the strings in TestExclusionReason
        }
    }

}
