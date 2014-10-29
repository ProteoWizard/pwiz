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

using System.Collections.Generic;
using System.Linq;
using System.Reflection;

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

        protected bool HasReturnValue(string methodName)
        {
            return (MethodInfos[methodName].ReturnType != typeof (void));
        }
    }
}
