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
