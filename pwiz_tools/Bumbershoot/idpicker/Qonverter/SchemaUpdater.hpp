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
#include "sqlite3.h"


#ifdef IDPICKER_SQLITE_64
#define IDPICKER_SQLITE_PRAGMA_MMAP "PRAGMA mmap_size=70368744177664; -- 2^46"
#else
#define IDPICKER_SQLITE_PRAGMA_MMAP "PRAGMA mmap_size=2147483648; -- 2^31"
#endif

#ifndef IDPICKER_NAMESPACE
#define IDPICKER_NAMESPACE IDPicker
#endif

#ifndef BEGIN_IDPICKER_NAMESPACE
#define BEGIN_IDPICKER_NAMESPACE namespace IDPICKER_NAMESPACE {
#define END_IDPICKER_NAMESPACE } // IDPicker
#endif


BEGIN_IDPICKER_NAMESPACE


class cancellation_exception : public std::exception {};

// convenient macro for one-line status and cancellation updates
#define ITERATION_UPDATE(ilr, index, count, message) \
{ \
    if ((ilr) && (ilr)->broadcastUpdateMessage(pwiz::util::IterationListener::UpdateMessage((index), (count), (message))) == pwiz::util::IterationListener::Status_Cancel) \
    {throw cancellation_exception();} \
}


const extern int CURRENT_SCHEMA_REVISION;
namespace SchemaUpdater {


/// creates user functions used by IDPicker code:
/// DISTINCT_DOUBLE_ARRAY_SUM
void createUserSQLiteFunctions(sqlite3* idpDbConnection);


/// update IDPicker database to the current schema version, or throw an exception if updating is impossible;
/// returns true iff the database schema was updated
bool update(const std::string& idpDbFilepath, const pwiz::util::IterationListenerRegistry* ilr = 0);


/// update IDPicker database connection to the current schema version, or throw an exception if updating is impossible;
/// returns true iff the database schema was updated
bool update(sqlite3* idpDbConnection, const pwiz::util::IterationListenerRegistry* ilr = 0);


/// returns true iff the idpDB has an IntegerSet table (which is the last table to be created)
bool isValidFile(const std::string& idpDbFilepath);


/// If necessary, adds a third backslash to UNC paths to work around a "working as designed" issue in System.Data.SQLite:
/// http://system.data.sqlite.org/index.html/tktview/01a6c83d51a203ff?plaintext
std::string getSQLiteUncCompatiblePath(const std::string& path);


} // namespace SchemaUpdater
END_IDPICKER_NAMESPACE


#endif // _SCHEMAUPDATER_HPP_
