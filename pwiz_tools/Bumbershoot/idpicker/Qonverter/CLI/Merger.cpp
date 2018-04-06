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

#include "Merger.hpp"
#include "Logger.hpp"

#pragma unmanaged
#include "../Merger.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/Std.hpp"
#pragma managed


namespace IDPicker {


using namespace pwiz::CLI::util;
using namespace pwiz::CLI::chemistry;
using namespace System::IO;
typedef NativeIDPicker::Merger NativeMerger;


namespace {

void ToStdStringVector(IList<String^>^ managedList, std::vector<std::string>& stdVector)
{
    stdVector.clear();
    if (managedList->Count > 0)
    {
        stdVector.reserve(managedList->Count);
        for (size_t i = 0, end = managedList->Count; i < end; ++i)
            stdVector.push_back(ToStdString(managedList[i]));
    }
}

} // namespace


void Merger::Merge(String^ mergeTargetFilepath, IList<String^>^ mergeSourceFilepaths, int maxThreads, pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        vector<string> nativeMergeSourceFilepaths;
        ToStdStringVector(mergeSourceFilepaths, nativeMergeSourceFilepaths);

        NativeMerger merger;
        merger.merge(ToStdString(mergeTargetFilepath), nativeMergeSourceFilepaths, maxThreads, ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}

void Merger::Merge(String^ mergeTargetFilepath, IList<String^>^ mergeSourceFilepaths)
{
    Merge(mergeTargetFilepath, mergeSourceFilepaths, 8, nullptr);
}


void Merger::Merge(String^ mergeTargetFilepath, IntPtr mergeSourceConnection, pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        sqlite3* foo = (sqlite3*) mergeSourceConnection.ToPointer();
        pin_ptr<sqlite3> idpDbPtr = foo;

        NativeMerger merger;
        merger.merge(ToStdString(mergeTargetFilepath), idpDbPtr, ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
        System::GC::KeepAlive(ilr);
    }
    CATCH_AND_FORWARD
}


} // namespace IDPicker
