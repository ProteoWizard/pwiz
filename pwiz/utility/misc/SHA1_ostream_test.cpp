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
#include "SHA1_ostream.hpp"
#include "unit.hpp"
#include "boost/iostreams/flush.hpp"
#include <boost/filesystem/operations.hpp>


using namespace pwiz::util;


ostream* os_ = 0;


const char* textBrown_ = "The quick brown fox jumps over the lazy dog";
const char* hashBrown_ = "2fd4e1c67a2d28fced849ee1bb76e7391b93eb12";


void test()
{
    ostringstream oss;
    SHA1_ostream sha1os(oss);

    sha1os << textBrown_ << flush;
    string hash = sha1os.hash();
    sha1os.explicitFlush();

    if (os_) *os_ << "str: " << oss.str() << endl
                  << "hash: " << hash << endl;

    unit_assert(hash == hashBrown_);
    unit_assert(sha1os.hash() == hashBrown_);

    sha1os << textBrown_ << flush;
    sha1os.explicitFlush();

    hash = sha1os.hash();
   
    if (os_) *os_ << "str: " << oss.str() << endl
                  << "hash: " << hash << endl;

    string hash2 = SHA1Calculator::hash(string(textBrown_) + textBrown_);
    unit_assert(sha1os.hash() == hash2);
}


void testFile()
{
    string filename = "SHA1_ostream.temp.txt";
    ofstream ofs(filename.c_str(), ios::binary); // binary necessary on Windows to avoid \n -> \r\n translation     
    SHA1_ostream sha1os(ofs);

    sha1os << textBrown_ << '\n' << textBrown_ << flush;
    string hashStream = sha1os.hash();

    sha1os.explicitFlush();
    string hashFile = SHA1Calculator::hashFile(filename);

    if (os_) *os_ << "stream: " << hashStream << endl
                  << "file  : " << hashFile << endl; 

    unit_assert(hashStream == hashFile);
    unit_assert(hashStream == "a159e6cde4e50e51713700d1fe4d0ce553eace87");
    ofs.close();
    boost::filesystem::remove(filename);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        testFile();
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

