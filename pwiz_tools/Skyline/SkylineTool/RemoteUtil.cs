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
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Threading;

namespace SkylineTool
{
    internal class Channel : IDisposable
    {
        private readonly string _connectionPrefix;
        private readonly object _instance;
        private readonly MethodInfo _method;
        private readonly SharedEvent _serverEvent;
        private readonly SharedEvent _clientEvent;

        public Channel(string connectionName, object instance, MethodInfo method)
            : this(connectionName, method.Name)
        {
            _instance = instance;
            _method = method;
        }

        public Channel(string connectionName, string methodName)
        {
            Name = methodName;
            _connectionPrefix = connectionName + Name;
            _serverEvent = new SharedEvent(_connectionPrefix + "-server"); // Not L10N
            _clientEvent = new SharedEvent(_connectionPrefix + "-client"); // Not L10N
        }

        public void Dispose()
        {
            _serverEvent.Dispose();
            _clientEvent.Dispose();
        }

        public string Name { get; private set; }
        public bool HasArg { get { return _method.GetParameters().Length == 1; }}
        public bool HasReturn { get { return _method.ReturnType != typeof (void); }}

        public void WaitServer()
        {
            _serverEvent.Wait();
        }

        public void ReleaseServer()
        {
            _serverEvent.Release();
        }

        public void WaitClient()
        {
            _clientEvent.Wait();
        }

        public void ReleaseClient()
        {
            _clientEvent.Release();
        }

        public SharedFile Open()
        {
            return new SharedFile(SharedFileName);
        }

        public string SharedFileName
        {
            get { return _connectionPrefix + "-file"; } // Not L10N
        }

        public object RunMethod(string data)
        {
            return _method.Invoke(_instance, data == null ? null : new []{(object)data});
        }
    }

    public class SharedEvent : IDisposable
    {
        private readonly EventWaitHandle _event;

        public SharedEvent(string name)
        {
            Name = "Global\\" + name; // Not L10N
            _event = new EventWaitHandle(false, EventResetMode.AutoReset, Name);
        }

        public string Name { get; private set; }
        public void Dispose() { _event.Dispose(); }
        public void Wait() { _event.WaitOne(); }
        public void Release() { _event.Set(); }
        public override string ToString() { return Name; }
    }

    internal class SharedFile : IDisposable
    {
        private readonly MemoryMappedFile _mappedFile;
        private readonly string _name;
        private const int Megabyte = 1024 * 1024;
        private const int MaxMessageSize = 10 * Megabyte;

        public SharedFile(string name)
        {
            _name = name;
            _mappedFile = MemoryMappedFile.CreateOrOpen(_name, MaxMessageSize);
        }

        public string Read()
        {
            using (var stream = _mappedFile.CreateViewStream())
            {
                using (var reader = new BinaryReader(stream))
                {
                    return reader.ReadString();
                }
            }
        }

        public void Write(object data)
        {
            using (var stream = _mappedFile.CreateViewStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    if (data == null)
                        writer.Write(string.Empty);
                    else
                    {
                        var s = data as string;
                        if (s != null)
                            writer.Write(s);
                        else
                        {
                            var chromatograms = (IChromatogram[])data;
                            writer.Write(chromatograms.Length);
                            foreach (var chromatogram in chromatograms)
                                chromatogram.Write(writer);
                        }
                    }
                }
            }
        }

        public void Dispose() { _mappedFile.Dispose(); }
        public Stream CreateViewStream() { return _mappedFile.CreateViewStream(); }
        public override string ToString() { return _name; }
    }
}
