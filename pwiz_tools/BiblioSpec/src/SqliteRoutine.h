//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

/*
 * a program that does routine check table existence,
 * create table, delete table, etc.
 *
 * Adopted from Jimmy Eng's sqt2sqlite.c
 *
 *$Id$
 */


#ifndef SQLITEROUTINE_H
#define SQLITEROUTINE_H

#include <string>
#include <iostream>
#include <fstream>
#include "sqlite3.h"
#include <cstdlib>
#include <cstdio>


namespace BiblioSpec {

class SqliteRoutine{

 public:
  SqliteRoutine();
  ~SqliteRoutine();

  static bool VALIDATE_DATABASE(const char* szSqliteFile);
  static bool TABLE_EXISTS(const char* tableName,sqlite3* db);
  static int TABLE_ROWS(const char* tableName, sqlite3* db);
  static void SQL_STMT(const char* zStmt, sqlite3* db);
  static std::string ESCAPE_APOSTROPHES(const std::string& sql);
  

};

} // namespace

#endif
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
