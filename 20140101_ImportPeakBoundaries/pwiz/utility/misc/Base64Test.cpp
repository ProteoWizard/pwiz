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
#include "Base64.hpp"
#include "unit.hpp"
#include <cstring>


using namespace pwiz::util;


ostream* os_ = 0;


struct TestPair
{
    const char* binary;
    const char* text;
};


TestPair testPairs_[] = 
{
    {"", ""},
    {"A", "QQ=="},
    {"AB", "QUI="},
    {"ABC", "QUJD"},
    {"The quick brown fox jumped over the lazy dog.", 
        "VGhlIHF1aWNrIGJyb3duIGZveCBqdW1wZWQgb3ZlciB0aGUgbGF6eSBkb2cu"},
    {"Darren", "RGFycmVu"},
};


const int testPairCount_ = sizeof(testPairs_)/sizeof(TestPair);


void checkTestPair(const TestPair& testPair)
{
    const string& from = testPair.binary;
    const string& to = testPair.text;
    if (os_) *os_ << from << " <--> " << to << endl;

    // convert binary -> text
    vector<char> textBuffer;
    textBuffer.resize(Base64::binaryToTextSize(from.size()) + 1, '\0');
    size_t textCount = Base64::binaryToText(from.c_str(), from.size(), &textBuffer[0]);

    // verify binary -> text
    string textString = !textBuffer.empty() ? &textBuffer[0] : "";
    unit_assert(textCount == (unsigned int)to.size());
    unit_assert(textString == to);

    // convert text -> binary
    vector<char> binaryBuffer;
    binaryBuffer.resize(Base64::textToBinarySize(to.size()) + 1, '\0');
    size_t binaryCount = Base64::textToBinary(to.c_str(), to.size(), &binaryBuffer[0]);

    // verify text -> binary
    string binaryString = !binaryBuffer.empty() ? &binaryBuffer[0] : "";
    unit_assert(binaryCount == (unsigned int)from.size());
    unit_assert(binaryString == from);
}


void test256()
{
    if (os_) *os_ << "test256()\n" << flush;

    // chars from 0 to 255
    vector<char> from(256);
    for (int i=0; i<256; i++)
        from[i] = (char)i;

    char to[] = "AAECAwQFBgcICQoLDA0ODxAREhMUFRYXGBkaGxwdHh8gISIj"
                "JCUmJygpKissLS4vMDEyMzQ1Njc4OTo7PD0+P0BBQkNERUZH"
                "SElKS0xNTk9QUVJTVFVWV1hZWltcXV5fYGFiY2RlZmdoaWpr"
                "bG1ub3BxcnN0dXZ3eHl6e3x9fn+AgYKDhIWGh4iJiouMjY6P"
                "kJGSk5SVlpeYmZqbnJ2en6ChoqOkpaanqKmqq6ytrq+wsbKz"
                "tLW2t7i5uru8vb6/wMHCw8TFxsfIycrLzM3Oz9DR0tPU1dbX"
                "2Nna29zd3t/g4eLj5OXm5+jp6uvs7e7v8PHy8/T19vf4+fr7"
                "/P3+/w==";

    // convert binary -> text
    vector<char> textBuffer;
    textBuffer.resize(Base64::binaryToTextSize(from.size()) + 1, '\0');
    size_t textCount = Base64::binaryToText(&from[0], 256, &textBuffer[0]);
    textBuffer[textCount] = '\0';
    unit_assert(textCount == (unsigned int)strlen(to));
    unit_assert(!strcmp(to, &textBuffer[0])); 

    // convert text -> binary
    vector<char> binaryBuffer;
    binaryBuffer.resize(300);
    size_t binaryCount = Base64::textToBinary(to, strlen(to), &binaryBuffer[0]);
    unit_assert(binaryCount == 256);
    for (int i=0; i<256; i++)
        unit_assert(binaryBuffer[i] == from[i]);
}


void test()
{
    for_each(testPairs_, testPairs_+testPairCount_, checkTestPair);
    test256();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) // verbose
            os_ = &cout;

        if (os_) *os_ << "Base64Test\n";
        
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

