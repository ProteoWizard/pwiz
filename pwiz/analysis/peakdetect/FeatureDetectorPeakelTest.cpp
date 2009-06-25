//
// FeatureDetectorPeakelTest.cpp
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


#include "FeatureDetectorPeakel.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include "boost/filesystem/path.hpp"


using namespace std;
using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::msdata;
namespace bfs = boost::filesystem;
using boost::shared_ptr;


ostream* os_ = 0;


void verifyBombessinFeatures(const FeatureField& featureField)
{
    const double epsilon = .01;

    const double mz_bomb2 = 810.415;
    vector<FeaturePtr> bombessin_2_found = featureField.find(mz_bomb2, epsilon, 
        RTMatches_Contains<Feature>(1865));
    unit_assert(bombessin_2_found.size() == 1);
    const Feature& bombessin_2 = *bombessin_2_found[0];
    unit_assert(bombessin_2.charge == 2);
    unit_assert(bombessin_2.peakels.size() == 5);
    unit_assert_equal(bombessin_2.peakels[0]->mz, mz_bomb2, epsilon);
    unit_assert_equal(bombessin_2.peakels[1]->mz, mz_bomb2+.5, epsilon);
    unit_assert_equal(bombessin_2.peakels[2]->mz, mz_bomb2+1, epsilon);
    unit_assert_equal(bombessin_2.peakels[3]->mz, mz_bomb2+1.5, epsilon);
    unit_assert_equal(bombessin_2.peakels[4]->mz, mz_bomb2+2, epsilon);
    //TODO: verify feature metadata

    const double mz_bomb3 = 540.612;
    vector<FeaturePtr> bombessin_3_found = featureField.find(mz_bomb3, epsilon, 
        RTMatches_Contains<Feature>(1865));
    unit_assert(bombessin_3_found.size() == 1);
    const Feature& bombessin_3 = *bombessin_3_found[0];
    unit_assert(bombessin_3.charge == 3);
    unit_assert(bombessin_3.peakels.size() == 3);
    unit_assert_equal(bombessin_3.peakels[0]->mz, mz_bomb3, epsilon);
    unit_assert_equal(bombessin_3.peakels[1]->mz, mz_bomb3+1./3, epsilon);
    unit_assert_equal(bombessin_3.peakels[2]->mz, mz_bomb3+2./3, epsilon);
    //TODO: verify feature metadata
}


void testBombessin(const string& filename)
{
    if (os_) *os_ << "testBombessin()" << endl;

    // open data file and check sanity

    MSDataFile msd(filename);
    unit_assert(msd.run.spectrumListPtr.get());
    unit_assert(msd.run.spectrumListPtr->size() == 8);

    // instantiate FeatureDetector
/*
    shared_ptr<PeakExtractor> peakExtractor = createPeakExtractor();
    vector< vector<Peak> > peaks = extractPeaks(msd, *peakExtractor);
    unit_assert(peaks.size() == 8);

    // grow peakels
    shared_ptr<PeakelGrower> peakelGrower = createPeakelGrower();
    PeakelField peakelField;
    peakelGrower->sowPeaks(peakelField, peaks);

    if (os_) *os_ << "peakelField:\n" << peakelField << endl;
    verifyBombessinPeakels(peakelField);

    // pick peakels

    shared_ptr<PeakelPicker> peakelPicker = createPeakelPicker();
    FeatureField featureField;
    peakelPicker->pick(peakelField, featureField);

    if (os_) *os_ << "featureField:\n" << featureField << endl;
    verifyBombessinFeatures(featureField);
*/
}


void test(const bfs::path& datadir)
{
    testBombessin((datadir / "FeatureDetectorTest_Bombessin.mzML").string());
}


int main(int argc, char* argv[])
{
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
        return 0;
    }

    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
}

