//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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


#include "FeatureModeler.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/filesystem/path.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>


using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data::peakdata;
namespace bfs = boost::filesystem;


ostream* os_ = 0;


FeaturePtr getFeature(const string& filename)
{
    std::ifstream is(filename.c_str());
    if (!is) throw runtime_error(("Unable to open file " + filename).c_str());

    FeaturePtr feature(new Feature);
    is >> *feature;

    return feature;
}


void testGaussian_Bombesin2(const Feature& bombesin2)
{
    if (os_) *os_ << "testGaussian_Bombesin2()\n";
    unit_assert(bombesin2.peakels.size() == 5);

    if (os_) *os_ << "before:\n" << bombesin2;

    FeatureModeler_Gaussian fm;
    Feature result;
    fm.fitFeature(bombesin2, result);

    if (os_) *os_ << "after:\n" << result << endl;
}


void testGaussian_Bombesin3(const Feature& bombesin3)
{
    if (os_) *os_ << "testGaussian_Bombesin3()\n";
    unit_assert(bombesin3.peakels.size() == 3);

    if (os_) *os_ << "before:\n" << bombesin3;

    FeatureModeler_Gaussian fm;
    Feature result;
    fm.fitFeature(bombesin3, result);

    if (os_) *os_ << "after:\n" << result << endl;
}


void testMulti(const FeatureField& ff)
{
    if (os_) *os_ << "testMulti()\n";

    FeatureField result;
    unit_assert(result.empty());

    FeatureModeler_Gaussian fm;
    fm.fitFeatures(ff, result);
    
    if (os_) *os_ << result << endl;
    unit_assert(result.size() == 2);
}


void test(const bfs::path& datadir)
{
    FeaturePtr bombesin2 = getFeature((datadir / "Bombesin2.feature").string());
    FeaturePtr bombesin3 = getFeature((datadir / "Bombesin3.feature").string());

    testGaussian_Bombesin2(*bombesin2);
    testGaussian_Bombesin3(*bombesin3);

    FeatureField ff;
    ff.insert(bombesin2);
    ff.insert(bombesin3);
    testMulti(ff);
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        bfs::path datadir = ".";

        for (int i=1; i<argc; i++)
        {
            if (!strcmp(argv[i],"-v")) 
                os_ = &cout;
            else
                // hack to allow running unit test from a different directory:
                // Jamfile passes full path to specified input file.
                // we want the path, so we can ignore filename
                datadir = bfs::path(argv[i]).branch_path(); 
        }   
        
        test(datadir);
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

