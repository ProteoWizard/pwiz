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

using System.Runtime.Serialization;
using System.ServiceModel;

namespace SkylineTool
{
    /// <summary>
    /// ISkylineTool is the main interface for interactive tools to communicate
    /// with the instance of Skyline that started the tool.
    /// </summary>
    [ServiceContract(CallbackContract = typeof(ISkylineToolEvents))]
    public interface ISkylineTool
    {
        [OperationContract]
        string GetReport(string toolName, string reportName);

        [OperationContract]
        void Select(string link);

        string DocumentPath { [OperationContract] get; }

        Version Version { [OperationContract] get; }

        [OperationContract]
        void NotifyDocumentChanged();

        [OperationContract]
        int RunTest(string testName);
    }

    /// <summary>
    /// Interface to communicate Skyline events to an interactive tool.
    /// </summary>
    [ServiceContract]
    public interface ISkylineToolEvents
    {
        [OperationContract(IsOneWay = true)]
        void DocumentChangedEvent();
    }

    [DataContract]
    public class Version
    {
        [DataMember] 
        public int Major { get; private set; }
        [DataMember]
        public int Minor { get; private set; }
        [DataMember]
        public int Build { get; private set; }
        [DataMember]
        public int Revision { get; private set; }

        public Version(int major, int minor, int build, int revision)
        {
            Major = major;
            Minor = minor;
            Build = build;
            Revision = revision;
        }
    }
}
