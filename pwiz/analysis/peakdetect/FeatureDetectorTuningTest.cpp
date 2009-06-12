//
// FeatureDetectorTuningTest.cpp
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


#include "PeakExtractor.hpp"
#include "PeakelGrower.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
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


shared_ptr<PeakExtractor> createPeakExtractor()
{
    shared_ptr<NoiseCalculator> noiseCalculator(new NoiseCalculator_2Pass);

    PeakFinder_SNR::Config pfsnrConfig;
    pfsnrConfig.windowRadius = 2;
    pfsnrConfig.zValueThreshold = 4;

    shared_ptr<PeakFinder> peakFinder(new PeakFinder_SNR(noiseCalculator, pfsnrConfig));

    PeakFitter_Parabola::Config pfpConfig;
    pfpConfig.windowRadius = 1; // (windowRadius != 1) is not good for real data
    shared_ptr<PeakFitter> peakFitter(new PeakFitter_Parabola(pfpConfig));

    return shared_ptr<PeakExtractor>(new PeakExtractor(peakFinder, peakFitter));
}


struct SetRetentionTime
{
    double rt;
    SetRetentionTime(double _rt) : rt(_rt) {}
    void operator()(Peak& peak) {peak.retentionTime = rt;}
};


vector< vector<Peak> > extractPeaks(const MSData& msd, const PeakExtractor& peakExtractor)
{
    MSDataCache msdCache;
    msdCache.open(msd);

    const size_t spectrumCount = msdCache.size();
    vector< vector<Peak> > result(spectrumCount);

    for (size_t index=0; index<spectrumCount; index++)
    {
        const SpectrumInfo& spectrumInfo = msdCache.spectrumInfo(index, true);

        vector<Peak>& peaks = result[index];
        peakExtractor.extractPeaks(spectrumInfo.data, peaks);
        for_each(peaks.begin(), peaks.end(), SetRetentionTime(spectrumInfo.retentionTime));

        if (os_)
        {
            *os_ << "index: " << index << endl;
            *os_ << "peaks: " << peaks.size() << endl; 
            copy(peaks.begin(), peaks.end(), ostream_iterator<Peak>(*os_, "\n"));
        }
    }

    return result;
}


void testBombessin(const string& filename)
{
    if (os_) *os_ << "testBombessin()" << endl;

    // open data file and check sanity

    MSDataFile msd(filename);
    unit_assert(msd.run.spectrumListPtr.get());
    unit_assert(msd.run.spectrumListPtr->size() == 8);

    // instantiate PeakExtractor and extract peaks

    shared_ptr<PeakExtractor> peakExtractor = createPeakExtractor();
    vector< vector<Peak> > peaks = extractPeaks(msd, *peakExtractor);
    unit_assert(peaks.size() == 8);

    // TODO: grow Peakels, verify Bombessin peaks
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

