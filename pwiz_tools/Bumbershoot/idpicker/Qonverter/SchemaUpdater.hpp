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


#ifndef _SCHEMAUPDATER_HPP_
#define _SCHEMAUPDATER_HPP_


#include <string>
#include "pwiz/utility/misc/IterationListener.hpp"


#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE
const extern int CURRENT_SCHEMA_REVISION;
namespace SchemaUpdater {


/// update IDPicker database to the current schema version, or throw an exception if updating is impossible;
/// returns true if the database schema was updated
bool update(const std::string& idpDbFilepath, pwiz::util::IterationListenerRegistry* ilr = 0);


} // namespace SchemaUpdater
END_IDPICKER_NAMESPACE


#endif // _SCHEMAUPDATER_HPP_
