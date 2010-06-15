//
// $Id$
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
#include "pwiz/utility/misc/unit.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "boost/iostreams/positioning.hpp"
#include "boost/filesystem/path.hpp"
#include "pwiz/utility/misc/Std.hpp"

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

    mzrtEqual(FeaturePtr a_)
        : a(a_) {}

    bool operator()(FeaturePtr b) const // should be enough info to uniquely identify a feature
    {
        return fabs(a->mz-b->mz) < mz_epsilon &&
            fabs(a->retentionTime - b->retentionTime) < rt_epsilon;

    }

    FeaturePtr a;
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
    boost::shared_ptr<PeakFamilyDetectorFT> detector(new PeakFamilyDetectorFT(config)); 
    FeatureDetectorSimple fds(detector);
   
    // instantiate MSData from test file
   
    MSDataFile msd((datadir / "FeatureDetectorTest_Bombesin.mzML").string());

    FeatureField output_features;
    fds.detect(msd, output_features);

    // instantiate the bombesin +2 feature that we know is correct, with calculated mzMonoisotopic and eyeballed retentionTime
    
    FeaturePtr bombesin2_truth(new Feature());
    bombesin2_truth->mz = 810.4148;
    bombesin2_truth->retentionTime = 1866;

    FeatureField::iterator bombesin2_hopeful = find_if(output_features.begin(), output_features.end(), mzrtEqual(bombesin2_truth));
    
    // assert that it is found, correctly, in the data
    unit_assert(bombesin2_hopeful != output_features.end());


    if (os_) *os_ << "\n[FeatureDetectorSimple] Bombesin detected at charge state +2 ... " << endl << *bombesin2_hopeful << endl;

    
    // do the same for the +3 feature

    FeaturePtr bombesin3_truth(new Feature());
    bombesin3_truth->mz = 540.6123;
    bombesin3_truth->retentionTime = 1866;
    
    FeatureField::iterator bombesin3_hopeful = find_if(output_features.begin(), output_features.end(), mzrtEqual(bombesin3_truth));
  
    // assert that it is found, correctly, in the data
    unit_assert(bombesin3_hopeful != output_features.end());

   
    if (os_) *os_ << "\n[FeatureDetectorSimple] Bombesin detected at charge state +3 ... " << endl << *bombesin3_hopeful << endl;

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
