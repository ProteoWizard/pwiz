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

namespace SkylineTool
{
    public class RemoteClient : RemoteBase
    {
        protected RemoteClient(string connectionName)
        {
            ConnectionName = connectionName;
        }

        public string ConnectionName { get; private set; }

        /// <summary>
        /// Make a remote call to the server.
        /// </summary>
        /// <param name="methodName">Name of method to execute on the server.</param>
        /// <param name="arg">Data to pass to the server method.</param>
        /// <returns>Result from server method.</returns>
        protected object RemoteCallName(string methodName, object arg)
        {
            // Create channel for communicating with the server.
            using (var channel = new Channel(ConnectionName, methodName))
            {
                using (var sharedFile = channel.Open())
                {
                    // Write string argument to shared file.
                    if (arg != null)
                        sharedFile.Write(arg);

                    // Notify server that it can process the data.
                    channel.ReleaseServer();

                    // Wait for server to finish.
                    channel.WaitClient();

                    // Read result from shared file.
                    if (HasReturnValue(methodName))
                        return sharedFile.Read();
                }
            }

            return null;
        }

        protected void RemoteCall(Action action)
        {
            RemoteCallName(action.Method.Name, null);
        }

        protected void RemoteCall<T>(Action<T> action, T arg)
        {
            RemoteCallName(action.Method.Name, arg);
        }

        protected T RemoteCallFunction<T>(Func<T> func)
        {
            return (T) RemoteCallName(func.Method.Name, null);
        }

        protected TReturn RemoteCallFunction<T, TReturn>(Func<T, TReturn> func, T arg)
        {
            return (TReturn) RemoteCallName(func.Method.Name, arg);
        }
    }
}
