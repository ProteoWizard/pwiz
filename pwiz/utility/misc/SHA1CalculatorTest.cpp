//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2007 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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
#include "SHA1Calculator.hpp"
#include "unit.hpp"
#include <boost/filesystem/operations.hpp>
#include <cstring>


using namespace pwiz::util;


ostream* os_ = 0;


char verify_int_is_32_bits[(sizeof(int)==4)*2-1];


const char* hashEmpty_ = "da39a3ee5e6b4b0d3255bfef95601890afd80709";

const char* textBrown_ = "The quick brown fox jumps over the lazy dog";
const char* hashBrown_ = "2fd4e1c67a2d28fced849ee1bb76e7391b93eb12";

const char* textabc_ = "abc";
const char* hashabc_ = "a9993e364706816aba3e25717850c26c9cd0d89d";

const char* textabc2_ = "abcdbcdecdefdefgefghfghighijhijkijkljklmklmnlmnomnopnopq";
const char* hashabc2_ = "84983e441c3bd26ebaae4aa1f95129e5e54670f1";

const char* hashMillion_ = "34aa973cd4c4daa4f61eeb2bdbad27316534016f"; // one million 'a'


void test()
{
    SHA1Calculator sha1;
    sha1.close();
    string temp = sha1.hash();
    if (os_) *os_ << "hash empty: " << temp << endl;
    unit_assert(temp == hashEmpty_);

    sha1.reset();
    sha1.update((const unsigned char*)textBrown_, strlen(textBrown_));
    sha1.close();
    temp = sha1.hash();
    if (os_) *os_ << "hash brown: " << temp << endl;
    unit_assert(temp == hashBrown_);
}


void testStream()
{
    istringstream is(textBrown_);
    string hash = SHA1Calculator::hash(is);
    if (os_) *os_ << "hash stream: " << hash << endl;
    unit_assert(hash == "2fd4e1c67a2d28fced849ee1bb76e7391b93eb12");
}


void testFile()
{
    const char* filename = "sha1test.test.txt";
    ofstream os(filename);
    os << textBrown_; 
    os.close();

    {
        string hash = SHA1Calculator::hashFile(filename);
        if (os_) *os_ << "hash file: " << hash << endl;
        unit_assert(hash == "2fd4e1c67a2d28fced849ee1bb76e7391b93eb12");
    }

    {
        ifstream filestream(filename, ios::binary);
        string hash = SHA1Calculator::hash(filestream);
        if (os_) *os_ << "hash stream: " << hash << endl;
        unit_assert(hash == "2fd4e1c67a2d28fced849ee1bb76e7391b93eb12");
    }

    boost::filesystem::remove(filename);
}


void testStatic()
{
    string temp = SHA1Calculator::hash(textBrown_);
    unit_assert(temp == hashBrown_);

    temp = SHA1Calculator::hash(textabc_);
    if (os_) *os_ << "hash abc: " << temp << endl;
    unit_assert(temp == hashabc_);
     
    temp = SHA1Calculator::hash(textabc2_);
    if (os_) *os_ << "hash abc2: " << temp << endl;
    unit_assert(temp == hashabc2_);
}


void testMillion()
{
    string a(10, 'a');
    SHA1Calculator sha1;

    for (int i=0; i<100000; i++)
        sha1.update(a);
    sha1.close();

    string temp = sha1.hash();
    if (os_) *os_ << "hash million: " << temp << endl;
    unit_assert(temp == hashMillion_);
}


void testProjected()
{
    SHA1Calculator sha1;

    sha1.update((const unsigned char*)textBrown_, strlen(textBrown_));
    string projected = sha1.hashProjected();
    if (os_) *os_ << "projected: " << projected << endl;

    unit_assert(projected == hashBrown_);
    unit_assert(sha1.hashProjected() == hashBrown_); // doesn't change

    sha1.close();
    string final = sha1.hash();
    unit_assert(final == hashBrown_);
    unit_assert(sha1.hash() == hashBrown_); // doesn't change
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) // verbose
            os_ = &cout;

        if (os_) *os_ << "sha1test\n";

        test();
        testStream();
        testFile();
        testStatic();
        testMillion();
        testProjected();
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

