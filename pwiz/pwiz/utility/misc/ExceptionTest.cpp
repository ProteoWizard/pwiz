//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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


#include "Exception.hpp"
#include "Std.hpp"
#include "unit.hpp"
#include <cassert>


using namespace pwiz::util;


ostream* os_ = 0;


void test()
{
#ifndef NDEBUG
    boost::shared_ptr<int> foo;
    unit_assert_throws(*foo, runtime_error);
#endif

#ifdef _DEBUG
    unit_assert_throws(_ASSERTE(1+1 == 4), runtime_error);
#endif
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        if (os_) *os_ << "ExceptionTest\n";
        test();
        return 0;
    }
    catch (runtime_error& e)
    {
        cerr << e.what() << endl;
    }
    catch (exception& e)
    {
        cerr << "Unhandled exception: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Unhandled unknown exception." << endl;
    }
    return 1;
}
