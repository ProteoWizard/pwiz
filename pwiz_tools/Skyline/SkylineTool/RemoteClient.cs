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
using System.IO.Pipes;
using System.Reflection;

namespace SkylineTool
{
    public class RemoteClient : RemoteBase
    {
        public RemoteClient(string connectionName)
        {
            Timeout = 1000;
            ConnectionName = connectionName;
        }

        public string ConnectionName { get; private set; }
        public int Timeout { get; set; }

        /// <summary>
        /// Make a remote call to the server.
        /// </summary>
        /// <param name="methodName">Name of method to execute on the server.</param>
        /// <param name="arguments">Data to pass to the server method.</param>
        /// <returns>Result from server method.</returns>
        public object RemoteCallName(string methodName, object[] arguments)
        {
            using (var client = new NamedPipeClientStream(@".", ConnectionName, PipeDirection.InOut))
            {
                client.Connect(Timeout);
                client.ReadMode = PipeTransmissionMode.Message;
                var remoteInvoke = new RemoteInvoke
                {
                    MethodName = methodName,
                    Arguments = arguments
                };

                var messageBytes = SerializeObject(remoteInvoke);
                client.Write(messageBytes, 0, messageBytes.Length);
                var response = (RemoteResponse) DeserializeObject(ReadAllBytes(client));
                if (null != response.Exception)
                {
                    throw new TargetInvocationException(response.Exception);
                }
                return response.ReturnValue;
            }
        }

        public void RemoteCall(Action action)
        {
            RemoteCallName(action.Method.Name, new object[0]);
        }

        public void RemoteCall<T>(Action<T> action, T arg)
        {
            RemoteCallName(action.Method.Name, new object[]{arg});
        }

        public void RemoteCall<T1, T2>(Action<T1, T2> action, T1 arg1, T2 arg2)
        {
            RemoteCallName(action.Method.Name, new object[]{arg1, arg2});
        }

        public T RemoteCallFunction<T>(Func<T> func)
        {
            return (T) RemoteCallName(func.Method.Name, new object[0]);
        }

        public TReturn RemoteCallFunction<T, TReturn>(Func<T, TReturn> func, T arg)
        {
            return (TReturn) RemoteCallName(func.Method.Name, new object[]{arg});
        }

        public TReturn RemoteCallFunction<T1, T2, TReturn>(Func<T1, T2, TReturn> func, T1 arg1, T2 arg2)
        {
            return (TReturn) RemoteCallName(func.Method.Name, new object[]{arg1, arg2});
        }
    }
}
