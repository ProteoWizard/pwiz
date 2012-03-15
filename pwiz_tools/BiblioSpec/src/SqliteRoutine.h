/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
/*
 * a program that does routine check table existence,
 * create table, delete table, etc.
 *
 * Adopted from Jimmy Eng's sqt2sqlite.c
 *
 *$Id: SqliteRoutine.h, v 1.0 2009/02/06 09:53:52 Ning Zhang Exp $
 */


#ifndef SQLITEROUTINE_H
#define SQLITEROUTINE_H

#include <string>
#include <iostream>
#include <fstream>
#include "sqlite3.h"
#include <cstdlib>
#include <cstdio>

using namespace std;

namespace BiblioSpec {

class SqliteRoutine{

 public:
  SqliteRoutine();
  ~SqliteRoutine();

  static bool VALIDATE_DATABASE(const char* szSqliteFile);
  static bool TABLE_EXISTS(const char* tableName,sqlite3* db);
  static int TABLE_ROWS(const char* tableName, sqlite3* db);
  static void SQL_STMT(const char* zStmt, sqlite3* db);
  

};

} // namespace

#endif
/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
