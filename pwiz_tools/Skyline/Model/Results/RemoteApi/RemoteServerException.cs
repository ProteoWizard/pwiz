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
using System.Runtime.Serialization;

namespace pwiz.Skyline.Model.Results.RemoteApi
{
    public class RemoteServerException : ApplicationException
    {
        public RemoteServerException(string message) : base(message)
        {
        }

        public RemoteServerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected RemoteServerException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
