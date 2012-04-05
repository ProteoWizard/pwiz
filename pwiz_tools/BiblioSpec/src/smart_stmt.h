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

#pragma once

#include "sqlite3.h"

struct sqlite3;
struct sqlite3_stmt;

namespace BiblioSpec {

/**
 * Wrapper for the sqlite3_stmt class.  Deleting the smart_stmt
 * automatically calls sqlite3_finalize() to free the sqlite3_stmt.
 * Also frees any existing value when the address of the smart_stmt is
 * taken.
 */
class smart_stmt
{
public:

    smart_stmt(sqlite3_stmt* p = NULL) : _ptr(p) {}
    ~smart_stmt(void)
    {
        if (_ptr != NULL)
            sqlite3_finalize(_ptr);
    }

    sqlite3_stmt* get() const { return _ptr; }
    operator sqlite3_stmt*() { return _ptr; }

    sqlite3_stmt** operator&()
    {
        // Address taken for assignment, so clean-up previous value
        if (_ptr != NULL)
            sqlite3_finalize(_ptr);
        _ptr = NULL;
        return &_ptr;
    }

private:
    sqlite3_stmt* _ptr;
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
