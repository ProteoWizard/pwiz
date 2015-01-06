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
using System.Diagnostics;
using System.IO.Pipes;
using System.Threading;

namespace SkylineTool
{
    public class RemoteService : RemoteBase
    {
        private bool _stop;

        /// <summary>
        /// Create remote service that communicates with a client.
        /// </summary>
        /// <param name="connectionName">Channel name (used to match with the client).</param>
        protected RemoteService(string connectionName)
        {
            ConnectionName = connectionName;
        }

        public void RunAsync()
        {
            var serverThread = new Thread(Run)
            {
                Name = "RemoteServiceThread-" + ConnectionName, // Not L10N
                IsBackground = true
            };
            serverThread.Start();
        }

        public void Stop()
        {
            _stop = true;
            using (var closingStream = new NamedPipeClientStream(ConnectionName))
            {
                closingStream.Connect(100);
            }
        }

        public void Run()
        {
            while (true)
            {
                NamedPipeServerStream pipeStream = null;
                try
                {
                    pipeStream = new NamedPipeServerStream(ConnectionName, PipeDirection.InOut, -1, PipeTransmissionMode.Message);
                    pipeStream.WaitForConnection();
                    if (_stop)
                        return;

                    // Spawn a new thread for each request and continue waiting
                    var t = new Thread(ProcessClientThread);
                    t.Start(pipeStream);
                }
                catch (Exception)
                {  
                    if (pipeStream != null)
                        pipeStream.Dispose();
                    throw;
                }
            }
// ReSharper disable once FunctionNeverReturns
        }

        private void ProcessClientThread(object streamArg)
        {
            try
            {
                using (var stream = (NamedPipeServerStream) streamArg)
                {
                    byte[] bytesResponse;
                    try
                    {
                        var remoteInvoke = (RemoteInvoke) DeserializeObject(ReadAllBytes(stream));
                        var method = GetType().GetMethod(remoteInvoke.MethodName);
                        var returnValue = method.Invoke(this, remoteInvoke.Arguments);
                        bytesResponse = SerializeObject(new RemoteResponse {ReturnValue = returnValue});
                    }
                    catch (Exception e)
                    {
                        try
                        {
                            bytesResponse = SerializeObject(new RemoteResponse
                            {
                                Exception = e
                            });
                        }
                        catch (Exception e2)
                        {
                            bytesResponse = SerializeObject(new RemoteResponse
                            {
                                Exception = new Exception(e2.ToString())
                            });
                        }
                    }
                    stream.Write(bytesResponse, 0, bytesResponse.Length);
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
            }
        }

        public string ConnectionName { get; private set; }
    }
}
