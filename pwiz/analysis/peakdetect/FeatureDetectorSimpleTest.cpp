//
// FeatureDetectorSimpleTest.cpp
//
//
// Original author: Kate Hoff <Katherine.Hoff@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Cnter, Los Angeles, California  90048
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

#include "FeatureDetectorSimple.hpp"
#include "PeakFamilyDetectorFT.hpp"

#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "boost/iostreams/positioning.hpp"

#include "boost/filesystem/path.hpp"
#include "pwiz/utility/misc/unit.hpp"

using namespace std;

using namespace pwiz::util;
using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::msdata;

namespace bfs = boost::filesystem;

ostream* os_ = 0;

double mz_epsilon = .005; // tolerance for mz differences
double rt_epsilon = 10; // tolerance for rt differences

struct mzrtEqual
{ 

    mzrtEqual(Feature& a_)
        : a(a_) {}

    bool operator()(Feature& b) const // should be enough info to uniquely identify a feature
    {
        return fabs(a.mzMonoisotopic-b.mzMonoisotopic) < mz_epsilon &&
            fabs(a.retentionTime - b.retentionTime) < rt_epsilon;

    }

    Feature a;
};

void testFeatureDetectorSimple(const bfs::path& datadir)
{
 
    if (os_) *os_ << "testFeatureDetectorSimple() ... " << endl;

   // instantiate PeakFamilyDetectorFT
   // (from PeakFamilyDetectorFTTest.cpp)

    ostream* os_log_ = 0; // don't log peak family detection

    PeakFamilyDetectorFT::Config config;
    config.log = os_log_;
    config.cp = CalibrationParameters::thermo_FT();
    PeakFamilyDetectorFT detector(config); 
    FeatureDetectorSimple fds(detector);
   
    // instantiate MSData from test file
   
    MSDataFile msd((datadir / "FeatureDetectorTest_Bombessin.mzML").string());

    vector<Feature> output_features;
    fds.detect(msd, output_features);
    
    // instantiate the bombessin +2 feature that we know is correct, with calculated mzMonoisotopic and eyeballed retentionTime
    
    Feature bombessin2_truth;
    bombessin2_truth.mzMonoisotopic = 810.4148;
    bombessin2_truth.retentionTime = 1866;

    vector<Feature>::iterator bombessin2_hopeful = find_if(output_features.begin(), output_features.end(), mzrtEqual(bombessin2_truth));
    
    unit_assert(bombessin2_hopeful != output_features.end());

    // assert that it is found, correctly, in the data
    if (os_) *os_ << "\n[FeatureDetectorSimple] Bombessin detected at charge state +2 ... " << endl << *bombessin2_hopeful << endl;


    // do the same for the +3 feature

    Feature bombessin3_truth;
    bombessin3_truth.mzMonoisotopic = 540.6123;
    bombessin3_truth.retentionTime = 1866;
    
    vector<Feature>::iterator bombessin3_hopeful = find_if(output_features.begin(), output_features.end(), mzrtEqual(bombessin3_truth));

    unit_assert(bombessin3_hopeful != output_features.end());

    // assert that it is found, correctly, in the data
    if (os_) *os_ << "\n[FeatureDetectorSimple] Bombessin detected at charge state +3 ... " << endl << *bombessin3_hopeful << endl;

    return;

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
            
            testFeatureDetectorSimple(datadir);
            return 0;
        }

    catch (exception& e)
        {
            cerr << e.what() << endl;
            return 1;
        }
}
