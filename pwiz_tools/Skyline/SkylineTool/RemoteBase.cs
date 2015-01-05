/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
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
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;

namespace SkylineTool
{
    /// <summary>
    /// Base class for RemoteService and RemoteClient that builds a dictionary
    /// of interface methods to be remotely executed.
    /// </summary>
    public class RemoteBase
    {
        protected readonly Dictionary<string, MethodInfo> MethodInfos = new Dictionary<string, MethodInfo>();

        protected RemoteBase()
        {
            foreach (var anInterface in GetType().FindInterfaces((type, criteria) => true, string.Empty))
            {
                if (anInterface.Name == "IDisposable") // Not L10N
                    continue;
                var interfaceMap = GetType().GetInterfaceMap(anInterface);
                foreach (var method in interfaceMap.TargetMethods.Where(method => method.IsPublic))
                    MethodInfos.Add(method.Name, method);
            }
        }

        protected static byte[] ReadAllBytes(PipeStream stream)
        {
            var memoryStream = new MemoryStream();
            do
            {
                var buffer = new byte[65536];
                int count = stream.Read(buffer, 0, buffer.Length);
                memoryStream.Write(buffer, 0, count);
            } while (!stream.IsMessageComplete);
            return memoryStream.ToArray();
        }

        protected object DeserializeObject(byte[] bytes)
        {
            var formatter = new BinaryFormatter();
            return formatter.Deserialize(new MemoryStream(bytes));
        }

        protected byte[] SerializeObject(object o)
        {
            var stream = new MemoryStream();
            var formatter = new BinaryFormatter();
            formatter.Serialize(stream, o);
            return stream.ToArray();
        }

        [Serializable]
        protected class RemoteInvoke
        {
            public string MethodName { get; set; }
            public object[] Arguments { get; set; }
        }

        [Serializable]
        protected class RemoteResponse
        {
            public object ReturnValue { get; set; }
            public Exception Exception { get; set; }
        }
    }
}
