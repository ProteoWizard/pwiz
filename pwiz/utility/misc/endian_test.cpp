//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
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


#include "Std.hpp"
#include "endian.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace pwiz::util;


void test()
{
    unsigned char bytes[] = {0xce, 0xfa, 0xca, 0xca, 0xfe, 0xca, 0x20, 0x04};
    unsigned int n = *reinterpret_cast<unsigned int*>(bytes);

    #if defined(PWIZ_LITTLE_ENDIAN)
    unit_assert(n == 0xcacaface);
    unit_assert(endianize32(n) == 0xcefacaca);
    #elif defined(PWIZ_BIG_ENDIAN)
    unit_assert(n == 0xcefacaca);
    unit_assert(endianize32(n) == 0xcacaface);
    #endif

    unsigned long long m = *reinterpret_cast<unsigned long long*>(bytes);

    #if defined(PWIZ_LITTLE_ENDIAN)
    unit_assert(m == 0x420cafecacafacell);
    unit_assert(endianize64(m) == 0xcefacacafeca2004ll);
    #elif defined(PWIZ_BIG_ENDIAN)
    unit_assert(m ==  0xcefacacafeca2004ll);
    unit_assert(endianize64(m) ==  0x420cafecacafacell);
    #endif
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        test();
    }
    catch (exception& e)
    {
        TEST_FAILED(e.what())
    }
    catch (...)
    {
        TEST_FAILED("Caught unknown exception.")
    }

    TEST_EPILOG
}


