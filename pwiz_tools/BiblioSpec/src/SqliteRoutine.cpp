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

#include "SqliteRoutine.h"

using namespace std;

namespace BiblioSpec {

SqliteRoutine::SqliteRoutine() {}

SqliteRoutine::~SqliteRoutine() {}

bool SqliteRoutine::VALIDATE_DATABASE(const char* szSqliteFile)
{
    sqlite3 *db;

    sqlite3_open(szSqliteFile, &db);

    if (db == 0) {
        printf("\n");
        printf(" Could not open database file %s.", szSqliteFile);
        printf(" Check to see if it is really an sqlite database.\n\n");
        return false;
    } else
        sqlite3_close(db);
    
    return true;
}

bool SqliteRoutine:: TABLE_EXISTS(const char* tableName,sqlite3* db)
{
    char szSql[8192];
    int  rc;
    int  iRow;
    int  iCol;
    int count;
    char **result;
    
    sprintf(szSql, "select count(*) FROM sqlite_master WHERE name='%s' and type='table';", tableName);
    
    rc = sqlite3_get_table(db, szSql, &result, &iRow, &iCol, 0);
    
    if(rc == SQLITE_OK) {
        count = atoi(result[1]);
    } else {
        cout<<"Can't execute the SQL statement"<<szSql<<endl;
    }
    
    if(count==1)
        return true;
    else
        return false;
}

int SqliteRoutine::TABLE_ROWS(const char* tableName, sqlite3* db)
{
    char zSql[2048];
    int rc;
    int iRow;
    int iCol;
    int count;
    char** result;
    
    sprintf(zSql, "select count(*) from '%s'", tableName);
    rc = sqlite3_get_table(db, zSql, &result, &iRow, &iCol, 0);
    
    
    if(rc == SQLITE_OK) {
        count = atoi(result[1]);
    } else {
        cout<<"Can't execute the SQL statement"<<zSql<<endl;
    }
    
    return count;
}

void SqliteRoutine::SQL_STMT(const char* stmt, sqlite3* db)
{
    char *errmsg;
    int   ret;
    
    ret = sqlite3_exec(db, stmt, 0, 0, &errmsg);
    
    if (ret != SQLITE_OK) {
        printf("Error in statement: %s [%s].\n", stmt, errmsg);
    }
    
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
