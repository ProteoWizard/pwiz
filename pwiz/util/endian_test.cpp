//
// endian_test.cpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "endian.hpp"
#include "util/unit.hpp"
#include <iostream>


using namespace std;
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


int main()
{
    try
    {
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    
    return 1;
}


