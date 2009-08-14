//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
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


#include "RegionAnalyzer.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include "pwiz/utility/misc/unit.hpp"
#include <iostream>
#include <cstring>


using namespace std;
using namespace pwiz;
using namespace pwiz::util;
using namespace pwiz::analysis;
using boost::shared_ptr;
using boost::lexical_cast;


ostream* os_ = 0;


// s0 .......
// s1 |.-^-.| (+.1 m/z)
// s2 |.-^-.| (-.1 m/z, x2 intensity)
// s3 |.-^-.| (+.1 m/z)
// s4 .......


double data_[5][14] =
{
    {0,9, 1,9, 2,9, 3,9, 4,9, 5,9, 6,9},
    {0.1,9, 1.1,0, 2.1,1.5, 3.1,2, 4.1,1.5, 5.1,0, 6.1,9},
    {-.1,9, .9,0, 1.9,3, /*2.9,4,*/ 3.9,3, 4.9,0, 5.9,9, 6,0},  // we still find interpolated peak
    {0.1,9, 1.1,0, 2.1,1.5, 3.1,2, 4.1,1.5, 5.1,0, 6.1,9},
    {0,9, 1,9, 2,9, 3,9, 4,9, 5,9, 6,9}
};


void initialize(MSData& msd)
{
    SpectrumListSimplePtr sl(new SpectrumListSimple);
    msd.run.spectrumListPtr = sl;

    for (size_t i=0; i<5; i++)
    {
        SpectrumPtr spectrum(new Spectrum);
        sl->spectra.push_back(spectrum);
        spectrum->index = i;
        spectrum->id = "scan=" + lexical_cast<string>(18+(int)i);
        spectrum->scanList.scans.push_back(Scan());
        spectrum->scanList.scans.back().cvParams.push_back(CVParam(MS_scan_start_time, 420+i, UO_second));
        spectrum->setMZIntensityPairs((MZIntensityPair*)data_[i], 7, MS_number_of_counts);
    }
}


const double epsilon_ = 1e-14; 


ostream& operator<<(ostream& os, const RegionAnalyzer::SpectrumStats& ss)
{
    os << ss.sumIntensity << " "
       << "(" << ss.max.mz << "," << ss.max.intensity << ")" << " "
       << "(" << ss.peak.mz << "," << ss.peak.intensity << ")";
    return os;
}


ostream& operator<<(ostream& os, const RegionAnalyzer::Stats& stats)
{
    os << "nonzeroCount: " << stats.nonzeroCount << endl
       << "sum_sumIntensity: " << stats.sum_sumIntensity << endl
       << "sum_peak_intensity: " << stats.sum_peak_intensity << endl
       << "mean_peak_mz: " << stats.mean_peak_mz << endl
       << "variance_peak_mz: " << stats.variance_peak_mz << endl
       << "sd_peak_mz: " << stats.sd_peak_mz << endl
       << "indexApex: " << stats.indexApex << endl;
    return os;
}

 
void testConfig(const RegionAnalyzer::Config& config)
{
    MSData msd;
    initialize(msd);

    shared_ptr<MSDataCache> cache(new MSDataCache);
    shared_ptr<RegionAnalyzer> regionAnalyzer(new RegionAnalyzer(config, *cache));

    MSDataAnalyzerContainer analyzers;
    analyzers.push_back(cache);
    analyzers.push_back(regionAnalyzer);

    MSDataAnalyzerDriver driver(analyzers);
    driver.analyze(msd);

    unit_assert(regionAnalyzer->spectrumStats().size() == 5);

    if (os_) *os_ << "sumIntensity (max) (peak):\n";

    vector<RegionAnalyzer::SpectrumStats>::const_iterator it = 
        regionAnalyzer->spectrumStats().begin();
    if (os_) *os_ << *it << endl;
    unit_assert(it->sumIntensity == 0);
    unit_assert_equal(it->max.mz, 0, epsilon_);
    unit_assert_equal(it->max.intensity, 0, epsilon_);
    unit_assert_equal(it->peak.mz, 0, epsilon_);
    unit_assert_equal(it->peak.intensity, 0, epsilon_);

    ++it;
    if (os_) *os_ << *it << endl;
    unit_assert(it->sumIntensity == 5);
    unit_assert_equal(it->max.mz, 3.1, epsilon_);
    unit_assert_equal(it->max.intensity, 2, epsilon_);
    unit_assert_equal(it->peak.mz, 3.1, epsilon_);
    unit_assert_equal(it->peak.intensity, 2, epsilon_);

    ++it;
    if (os_) *os_ << *it << endl;
    unit_assert(it->sumIntensity == 6); // with omitted sample intensity 4
    unit_assert_equal(it->max.mz, 1.9, epsilon_);
    unit_assert_equal(it->max.intensity, 3, epsilon_);
    unit_assert_equal(it->peak.mz, 2.9, epsilon_); // found the peak by interpolation
    unit_assert_equal(it->peak.intensity, 4, epsilon_);

    ++it;
    if (os_) *os_ << *it << endl;
    unit_assert(it->sumIntensity == 5);
    unit_assert_equal(it->max.mz, 3.1, epsilon_);
    unit_assert_equal(it->max.intensity, 2, epsilon_);
    unit_assert_equal(it->peak.mz, 3.1, epsilon_);
    unit_assert_equal(it->peak.intensity, 2, epsilon_);

    ++it;
    if (os_) *os_ << *it << endl;
    unit_assert(it->sumIntensity == 0);
    unit_assert_equal(it->max.mz, 0, epsilon_);
    unit_assert_equal(it->max.intensity, 0, epsilon_);
    unit_assert_equal(it->peak.mz, 0, epsilon_);
    unit_assert_equal(it->peak.intensity, 0, epsilon_);

    // Stats

    const RegionAnalyzer::Stats& stats = regionAnalyzer->stats();
    if (os_) *os_ << stats << endl; 
    unit_assert(stats.nonzeroCount == 3);
    unit_assert_equal(stats.sum_sumIntensity, 16, epsilon_);
    unit_assert_equal(stats.sum_peak_intensity, 8, epsilon_);
    unit_assert_equal(stats.mean_peak_mz, 3, epsilon_);
    unit_assert_equal(stats.variance_peak_mz, .01, epsilon_);
    unit_assert_equal(stats.sd_peak_mz, .1, epsilon_);
    unit_assert(stats.indexApex == 2);
}


void test()
{
    if (os_) *os_ << "test index:\n"; 
    RegionAnalyzer::Config config;
    config.mzRange = make_pair(.5, 5.5);
    config.indexRange = make_pair(1,3);
    testConfig(config);

    if (os_) *os_ << "test scanNumber:\n"; 
    config = RegionAnalyzer::Config();
    config.mzRange = make_pair(.5, 5.5);
    config.scanNumberRange = make_pair(19,21);
    testConfig(config);

    if (os_) *os_ << "test retentionTime:\n"; 
    config = RegionAnalyzer::Config();
    config.mzRange = make_pair(.5, 5.5);
    config.rtRange = make_pair(420.5, 423.5);
    testConfig(config);
}


int main(int argc, char* argv[])
{
    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
        test();
        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }
    
    return 1;
}

