/*
 * Author: David Shteynberg <dshteynberg .at. gmail.com>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.IO;
using System.Text;

namespace pwiz.Common.SystemUtil
{
    public class SimpleUserMessageWriter : TextWriter
    {
        public override void WriteLine(string line)
        {
            Messages.WriteAsyncUserMessage(line);
        }

        public override Encoding Encoding => Encoding.Unicode;  // In memory only
    }
}
