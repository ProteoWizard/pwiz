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

#include "SchemaUpdater.hpp"
#include "Logger.hpp"

#pragma unmanaged
#include "../SchemaUpdater.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/Std.hpp"
#pragma managed


namespace IDPicker {


using namespace pwiz::CLI::util;
namespace NativeSchemaUpdater = NativeIDPicker::SchemaUpdater;


int SchemaUpdater::CurrentSchemaRevision::get() { return NativeIDPicker::CURRENT_SCHEMA_REVISION; }


bool SchemaUpdater::Update(String^ idpDbFilepath, pwiz::CLI::util::IterationListenerRegistry^ ilr)
{
    Logger::Initialize(); // make sure the logger is initialized

    try
    {
        return NativeSchemaUpdater::update(ToStdString(idpDbFilepath),
                                           ilr == nullptr ? 0 : (pwiz::util::IterationListenerRegistry*) ilr->void_base().ToPointer());
    }
    CATCH_AND_FORWARD;
    return false;
}


} // namespace IDPicker
