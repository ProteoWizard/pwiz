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


#include "SqliteRoutine.h"
#include "pwiz/utility/misc/Std.hpp"


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

string SqliteRoutine::ESCAPE_APOSTROPHES(const string& sql)
{
    string escapedString;
    for (size_t i = 0; i < sql.length(); ++i)
    {
        escapedString += sql[i];
        if (sql[i] == '\'')
        {
            escapedString += sql[i];
        }
    }
    return escapedString;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
