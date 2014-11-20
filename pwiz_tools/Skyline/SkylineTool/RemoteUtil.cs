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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
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
            : this(connectionName, method.Name, true)
        {
            _instance = instance;
            _method = method;
        }

        public Channel(string connectionName, string methodName, bool isServer = false)
        {
            Name = methodName;
            _connectionPrefix = connectionName + Name;
            _serverEvent = new SharedEvent(_connectionPrefix + "-server", isServer); // Not L10N
            _clientEvent = new SharedEvent(_connectionPrefix + "-client", isServer); // Not L10N
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

        public object RunMethod(object data)
        {
            return _method.Invoke(_instance, data == null ? null : new []{data});
        }
    }

    public class SharedEvent : IDisposable
    {
        private readonly EventWaitHandle _event;

        public SharedEvent(string name, bool createExpected)
        {
            Name = "Global\\" + name; // Not L10N
            bool createdNew;
            _event = new EventWaitHandle(false, EventResetMode.AutoReset, Name, out createdNew);
            if (createdNew != createExpected)
                throw new InvalidOperationException("SharedEvent " + name + (createExpected ? " was already created" : " already exists")); // Not L10N
        }

        public string Name { get; private set; }
        public void Dispose() { _event.Dispose(); }
        public void Wait() { _event.WaitOne(); }
        public void Release() { _event.Set(); }
        public override string ToString() { return Name; }
    }

    public interface IStreamable
    {
        void Read(BinaryReader reader);
        void Write(BinaryWriter writer);
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

        public object Read()
        {
            using (var stream = _mappedFile.CreateViewStream())
            {
                using (var reader = new BinaryReader(stream))
                {
                    return StreamIn(reader);
                }
            }
        }

        public void Write(object data)
        {
            using (var stream = _mappedFile.CreateViewStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    StreamOut(data, writer);
                }
            }
        }

        public static void StreamOut(object data, BinaryWriter writer)
        {
            var type = data.GetType();
            writer.Write(type.ToString());

            var s = data as string;
            if (s != null)
            {
                writer.Write(s);
                return;
            }

            var array = data as Array;
            if (array != null)
            {
                writer.Write(array.Length);
                foreach (var element in array)
                    StreamOutElement(element, writer);
                return;
            }

            StreamOutElement(data, writer);
        }

        private static void StreamOutElement(object element, BinaryWriter writer)
        {
// ReSharper disable CanBeReplacedWithTryCastAndCheckForNull
            if (element is int)
                writer.Write((int) element);
            else if (element is float)
                writer.Write((float) element);
            else if (element is double)
                writer.Write((double) element);
            else if (element is string)
                writer.Write((string) element);
            else if (element is bool)
                writer.Write((bool) element);
            else if (element is IStreamable)
                ((IStreamable) element).Write(writer);
            else
                throw new ArgumentException();
// ReSharper restore CanBeReplacedWithTryCastAndCheckForNull
        }


        public static object StreamIn(BinaryReader reader)
        {
            var type = Type.GetType(reader.ReadString());
            if (type == null)
                throw new InvalidDataException();

            if (type.IsArray)
            {
                int length = reader.ReadInt32();
                var elementType = type.GetElementType();
                var array = Array.CreateInstance(elementType, length);
                for (int i = 0; i < length; i++)
                    array.SetValue(StreamInElement(elementType, reader), i);
                return array;
            }

            return StreamInElement(type, reader);
        }

        private static object StreamInElement(Type type, BinaryReader reader)
        {
            if (type == typeof (int))
                return reader.ReadInt32();
            if (type == typeof (float))
                return reader.ReadSingle();
            if (type == typeof (double))
                return reader.ReadDouble();
            if (type == typeof (string))
                return reader.ReadString();
            if (type == typeof (bool))
                return reader.ReadBoolean();
            if (type == typeof (int))
                return reader.ReadInt32();
            if (type == typeof (int))
                return reader.ReadInt32();
            var instance = Activator.CreateInstance(type);
            var streamable = instance as IStreamable;
            if (streamable == null) 
                throw new InvalidDataException();
            streamable.Read(reader);
            return streamable;
        }

        public void Dispose() { _mappedFile.Dispose(); }
        public override string ToString() { return _name; }
    }
}
