//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2017 Matt Chambers
//

using System;
using System.Security;
using System.Security.Policy;

namespace IDPicker
{
    /// <summary>
    /// A least-evil (?) way of customizing the default location of the application's user.config files.
    /// </summary>
    public class CustomEvidenceHostSecurityManager : HostSecurityManager
    {
        public override HostSecurityManagerOptions Flags
        {
            get
            {
                return HostSecurityManagerOptions.HostAssemblyEvidence;
            }
        }

        public override Evidence ProvideAssemblyEvidence(System.Reflection.Assembly loadedAssembly, Evidence inputEvidence)
        {
            if (!loadedAssembly.Location.EndsWith("IDPicker.exe"))
                return base.ProvideAssemblyEvidence(loadedAssembly, inputEvidence);

            // override the full Url used in Evidence to just "IDPicker.exe" so it remains the same no matter where the exe is located
            var zoneEvidence = inputEvidence.GetHostEvidence<Zone>();
            return new Evidence(new EvidenceBase[] { zoneEvidence, new Url("IDPicker.exe") }, null);
        }
    }

    public class DefaultAppDomainManager : AppDomainManager
    {
        private CustomEvidenceHostSecurityManager hostSecurityManager;
        public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
        {
            base.InitializeNewDomain(appDomainInfo);

            hostSecurityManager = new CustomEvidenceHostSecurityManager();
        }

        public override HostSecurityManager HostSecurityManager
        {
            get
            {
                return hostSecurityManager;
            }
        }
    }
}
