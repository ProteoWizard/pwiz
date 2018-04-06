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
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//


#pragma warning( push )
#pragma warning( disable : 4634 4635 )
#include "Qonverter.hpp"
#using <system.dll>
#pragma warning( pop )


namespace IDPicker {


using namespace System;
using namespace System::Collections::Generic;


public ref struct Merger abstract
{

static void Merge(String^ mergeTargetFilepath, IList<String^>^ mergeSourceFilepaths, int maxThreads, pwiz::CLI::util::IterationListenerRegistry^ ilr);
static void Merge(String^ mergeTargetFilepath, IList<String^>^ mergeSourceFilepaths);

static void Merge(String^ mergeTargetFilepath, IntPtr mergeSourceConnection, pwiz::CLI::util::IterationListenerRegistry^ ilr);

};


} // namespace IDPicker
