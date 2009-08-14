//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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

#include "Filesystem.hpp"
#include "Stream.hpp"
#include "Container.hpp"
#include "Exception.hpp"
#include "pwiz/utility/misc/unit.hpp"


using namespace pwiz::util;
using boost::system::error_code;
using boost::system::system_category;
using boost::system::system_error;


// platform-specific path elements
#ifdef WIN32
#   define ABS "c:/"         // test at c:\ because FindFile behaves a little tricky there
#   define REL ".\\relative"
#   define A "\\"             // both slash types should work
#   define D ";"              // path separator
#else
#   define ABS "./"           // POSIX filesystems don't have the same problem,
#   define REL "./relative"   // so all tests are relative (avoids permission issues)
#   define A "/"
#   define D ":"
#endif


const char* testPathContentPairArray[] =
{
    // path                                        content (empty indicates directory)
    ABS"pwiz_foofoo_test",                           "root file",
    ABS"pwiz_foo_test",                              "",
    ABS"pwiz_foo_test"A"this file",                  "has content",
    ABS"pwiz_foo_test"A"this dir has",               "",
    ABS"pwiz_foo_test"A"this dir has"A"a test file", "with content",
    ABS"pwiz_bar_test",                              "",
    ABS"pwiz_bar_test"A"some file",                  "12345",
    ABS"pwiz_bar_test"A"some dir",                   "",

    REL"pwiz_foofoo_test",                           "root file",
    REL"pwiz_foo_test",                              "",
    REL"pwiz_foo_test"A"this file",                  "has content",
    REL"pwiz_foo_test"A"this dir has",               "",
    REL"pwiz_foo_test"A"this dir has"A"a test file", "with content",
    REL"pwiz_bar_test",                              "",
    REL"pwiz_bar_test"A"some file",                  "12345",
    REL"pwiz_bar_test"A"some dir",                   ""
};


const int testPathContentPairArraySize = sizeof(testPathContentPairArray) / sizeof(const char*);


struct TestPathmask
{
    const char* pathmask;      // mask to be passed to expand_pathmask()
    const char* pathnameArray; // paths that should match with expand_pathmask()
};


const TestPathmask testPathmaskArray[] =
{
    { ABS"pwiz_f??f??_test",       ABS"pwiz_foofoo_test" },
    { ABS"pwiz_???_test",          ABS"pwiz_foo_test"D ABS"pwiz_bar_test" },
    { ABS"pwiz_f*o_test",          ABS"pwiz_foo_test"D ABS"pwiz_foofoo_test" },
    { ABS"pwiz_foobar_test",       "" },
    { ABS"pwiz_foo_test"A"no*hit", "" },
    { ABS"pwiz_foo_test"A"*",      ABS"pwiz_foo_test"A"this file"D ABS"pwiz_foo_test"A"this dir has" },
    { ABS"pwiz_foo_test"A"this *", ABS"pwiz_foo_test"A"this file"D ABS"pwiz_foo_test"A"this dir has" },

    { REL"pwiz_f??f??_test",       REL"pwiz_foofoo_test" },
    { REL"pwiz_???_test",          REL"pwiz_foo_test"D REL"pwiz_bar_test" },
    { REL"pwiz_f*o_test",          REL"pwiz_foo_test"D REL"pwiz_foofoo_test" },
    { REL"pwiz_foobar_test",       "" },
    { REL"pwiz_foo_test"A"no*hit", "" },
    { REL"pwiz_foo_test"A"*",      REL"pwiz_foo_test"A"this file"D REL"pwiz_foo_test"A"this dir has" },
    { REL"pwiz_foo_test"A"this *", REL"pwiz_foo_test"A"this file"D REL"pwiz_foo_test"A"this dir has" }
};


const int testPathmaskArraySize = sizeof(testPathmaskArray) / sizeof(TestPathmask);


void create_file(const bfs::path& ph, const string& contents)
{
    ofstream f(ph.file_string().c_str());
    if (!f)
        throw bfs::filesystem_error("create_file", ph, error_code(errno, system_category));
    if (!contents.empty()) f << contents;
}


void createTestPath()
{
    for (int i=0; i < testPathContentPairArraySize; i += 2)
    {
        // if content is empty, create a directory
        if (strlen(testPathContentPairArray[i+1]) == 0)
            bfs::create_directory(testPathContentPairArray[i]);
        else
            create_file(testPathContentPairArray[i], testPathContentPairArray[i+1]);

        // test that the directory/file was really created
        unit_assert(bfs::exists(testPathContentPairArray[i]));
    }
}


void deleteTestPath()
{
    for (int i=0; i < testPathContentPairArraySize; i += 2)
        if (bfs::exists(testPathContentPairArray[i]))
            bfs::remove_all(testPathContentPairArray[i]);
}


set<bfs::path> parsePathArray(const string& pathArray)
{
    set<bfs::path> pathSet;
    vector<string> tokens;
    bal::split(tokens, pathArray, bal::is_any_of(D));
    if (!tokens.empty() && !tokens[0].empty())
        for (size_t i=0; i < tokens.size(); ++i)
            pathSet.insert(tokens[i]);
    return pathSet;
}


void testExpandPathmask()
{
    // create a filesystem tree for testing
    createTestPath();

    for (int i=0; i < testPathmaskArraySize; ++i)
    {
        try
        {
            vector<bfs::path> matchingPaths;
            expand_pathmask(testPathmaskArray[i].pathmask, matchingPaths);

            set<bfs::path> targetPathSet = parsePathArray(testPathmaskArray[i].pathnameArray);
            unit_assert(matchingPaths.size() == targetPathSet.size());

            set<bfs::path> matchingPathSet(matchingPaths.begin(), matchingPaths.end());
            vector<bfs::path> xorSet;
            std::set_symmetric_difference(targetPathSet.begin(), targetPathSet.end(),
                                          matchingPathSet.begin(), matchingPathSet.end(),
                                          xorSet.end());
            unit_assert(xorSet.empty());
        }
        catch (exception& e)
        {
            cout << "Unit test on pathmask \"" << testPathmaskArray[i].pathmask << "\" failed:\n"
                 << e.what() << endl;
        }
    }

    // special test of wildcard in the root (on Windows)
    vector<bfs::path> matchingPaths;
    expand_pathmask(ABS"*", matchingPaths);
    unit_assert(find(matchingPaths.begin(), matchingPaths.end(), ABS"pwiz_foofoo_test") != matchingPaths.end());
    unit_assert(find(matchingPaths.begin(), matchingPaths.end(), ABS"pwiz_foo_test") != matchingPaths.end());
    unit_assert(find(matchingPaths.begin(), matchingPaths.end(), ABS"pwiz_bar_test") != matchingPaths.end());

    // cleanup test tree
    deleteTestPath();
}


void testAbbreviateByteSize()
{
    unit_assert(abbreviate_byte_size(1) == "1 B");
    unit_assert(abbreviate_byte_size(999) == "999 B");
    unit_assert(abbreviate_byte_size(1000) == "1 KB");
    unit_assert(abbreviate_byte_size(999999) == "999 KB");
    unit_assert(abbreviate_byte_size(1000000) == "1 MB");
    unit_assert(abbreviate_byte_size(999999999) == "999 MB");
    unit_assert(abbreviate_byte_size(1000000000) == "1 GB");

    unit_assert(abbreviate_byte_size(1, ByteSizeAbbreviation_IEC) == "1 B");
    unit_assert(abbreviate_byte_size(1023, ByteSizeAbbreviation_IEC) == "1023 B");
    unit_assert(abbreviate_byte_size(1024, ByteSizeAbbreviation_IEC) == "1 KiB");
    unit_assert(abbreviate_byte_size((1024 << 10)-1, ByteSizeAbbreviation_IEC) == "1023 KiB");
    unit_assert(abbreviate_byte_size((1024 << 10), ByteSizeAbbreviation_IEC) == "1 MiB");
    unit_assert(abbreviate_byte_size((1024 << 20)-1, ByteSizeAbbreviation_IEC) == "1023 MiB");
    unit_assert(abbreviate_byte_size((1024 << 20), ByteSizeAbbreviation_IEC) == "1 GiB");

    unit_assert(abbreviate_byte_size(1, ByteSizeAbbreviation_JEDEC) == "1 B");
    unit_assert(abbreviate_byte_size(1023, ByteSizeAbbreviation_JEDEC) == "1023 B");
    unit_assert(abbreviate_byte_size(1024, ByteSizeAbbreviation_JEDEC) == "1 KB");
    unit_assert(abbreviate_byte_size((1024 << 10)-1, ByteSizeAbbreviation_JEDEC) == "1023 KB");
    unit_assert(abbreviate_byte_size((1024 << 10), ByteSizeAbbreviation_JEDEC) == "1 MB");
    unit_assert(abbreviate_byte_size((1024 << 20)-1, ByteSizeAbbreviation_JEDEC) == "1023 MB");
    unit_assert(abbreviate_byte_size((1024 << 20), ByteSizeAbbreviation_JEDEC) == "1 GB");
}


int main()
{
    try
    {
        testExpandPathmask();
        testAbbreviateByteSize();
        return 0;
    }
    catch (exception& e)
    {
        cerr << "Caught exception: " << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception" << endl;
    }

    deleteTestPath();
    return 1;
}
