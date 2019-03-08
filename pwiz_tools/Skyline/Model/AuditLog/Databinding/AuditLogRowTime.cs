/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Globalization;
using pwiz.Common.DataBinding.Attributes;

namespace pwiz.Skyline.Model.AuditLog.Databinding
{
    public class AuditLogRowTime
    {
        private DateTime _timeStampUTC; // UTC time of log entry creation
        private TimeSpan _timeZoneOffset; // Offset to local time at moment of log entry creation
        public AuditLogRowTime(DateTime timeStampUTC, TimeSpan timeZoneOffset)
        {
            _timeStampUTC = timeStampUTC;
            _timeZoneOffset = timeZoneOffset;

        }

        [Format(@"yyyy-MM-dd HH:mm:ss")]
        public DateTime UTCTime
        {
            get { return _timeStampUTC; }
        }

        // Do NOT make this formattable, it's an ISO standard format
        public string TimeStamp
        {
            get { return AuditLogEntry.FormatSerializationString(_timeStampUTC, _timeZoneOffset); }
        }

        public override string ToString() // CONSIDER(nicksh) make this IFormattable
        {
            return _timeStampUTC.ToLocalTime().ToString(@"yyyy-MM-dd HH:mm:ss", DateTimeFormatInfo.InvariantInfo);
        }
    }
}
