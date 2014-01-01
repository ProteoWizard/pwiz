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
#include "pwiz/utility/misc/Std.hpp"
#include <cstring>
#include <boost/algorithm/string/split.hpp>


using namespace pwiz;
using namespace pwiz::util;
using namespace pwiz::analysis;


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
        spectrum->scanList.scans.back().cvParams.push_back(CVParam(MS_scan_start_time, 420+int(i), UO_second));
        spectrum->setMZIntensityPairs((MZIntensityPair*)data_[i], 7, MS_number_of_detector_counts);
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


// verify that changes to unify args for various configs retain backward compatiblity
#include "RegionSIC.hpp"
#include "RegionTIC.hpp"
#include "RegionSlice.hpp"
#include "RunSummary.hpp"
#include "SpectrumBinaryData.hpp"
#include "Pseudo2DGel.hpp"
void testAnalyzerFamilyArgumentBackwardCompatibility()
{
    if (os_) *os_ << "test for backward compatible commandline args:\n"; 
    RegionSIC::Config sicOld("100 5 amu"); //  mzCenter radius ("amu"|"ppm")
    RegionSIC::Config sicNew(SIC_MZCENTER_ARG"=100 "SIC_RADIUS_ARG"=5 "SIC_RADIUSUNITS_ARG"=amu");
    unit_assert(sicOld == sicNew);

    RegionTIC::Config ticOld("delimiter=space 100 200"); //  mzlow mzhigh
    RegionTIC::Config ticNew1(TIC_MZRANGE_ARG"=100-200 delimiter=space");
    RegionTIC::Config ticNew2(TIC_MZRANGE_ARG"=100,200 delimiter=space");
    RegionTIC::Config ticNew3(TIC_MZRANGE_ARG"=[100,200] delimiter=space");
    RegionTIC::Config ticNew4(TIC_MZRANGE_ARG"=[100,200]");
    unit_assert(ticOld == ticNew1);
    unit_assert(ticNew1 == ticNew2);
    unit_assert(ticNew2 == ticNew3);
    unit_assert(!(ticNew3 == ticNew4));

    RegionSlice::Config sliceOld("mz=[20,30] rt=[23,67] index=[45,66]");
    RegionSlice::Config sliceNew("mz=20,30 rt=23,67 index=45,66");
    unit_assert(sliceOld==sliceNew);

    SpectrumBinaryData::Config binaryOld("6- sn precision=4");    
    SpectrumBinaryData::Config binaryNew("sn=[6,] precision=4");    
    unit_assert(binaryOld==binaryNew);
    SpectrumBinaryData::Config binaryOld1("4-9 precision=5");    
    SpectrumBinaryData::Config binaryNew1("precision=5 index=4,9");    
    unit_assert(binaryOld1==binaryNew1);

    Pseudo2DGel::Config gelOld("mzLow=50 mzHigh=500");
    Pseudo2DGel::Config gelNew("mz=50,500");
    unit_assert(gelOld==gelNew);
}

void test()
{
    // test for each output delimiter type
    std::vector<std::string> delimiter_options;
    std::vector<std::string> outputs;
    std::string delimiter_help(TABULARCONFIG_DELIMITER_OPTIONS_STR);
    boost::algorithm::split(delimiter_options, delimiter_help, boost::algorithm::is_any_of("<|>") );
    
    // "delimiter=","fixed","space","comma","tab",""
    for (int n=1; n<(int)delimiter_options.size() &&  delimiter_options[n].size(); n++)
    {
    std::ostringstream txtstream;
    std::string delim = delimiter_options[0] + delimiter_options[n];
    if (os_) *os_ << "test with delimiter style " << delim <<":\n"; 

    if (os_) *os_ << "test index:\n"; 
    RegionAnalyzer::Config config;
    unit_assert(config.checkDelimiter(delim)); // set output delimiter type
    config.dumpRegionData = true;
    config.osDump = &txtstream; // dump to this stream
    config.mzRange = make_pair(.5, 5.5);
    config.indexRange = make_pair(1,3);
    testConfig(config);

    if (os_) *os_ << "test scanNumber:\n"; 
    config = RegionAnalyzer::Config();
    unit_assert(config.checkDelimiter(delim)); // set output delimiter type
    config.dumpRegionData = true;
    config.osDump = &txtstream; // dump to this stream
    config.mzRange = make_pair(.5, 5.5);
    config.scanNumberRange = make_pair(19,21);
    testConfig(config);

    if (os_) *os_ << "test retentionTime:\n"; 
    config = RegionAnalyzer::Config();
    unit_assert(config.checkDelimiter(delim)); // set output delimiter type
    config.dumpRegionData = true;
    config.osDump = &txtstream; // dump to this stream
    config.mzRange = make_pair(.5, 5.5);
    config.rtRange = make_pair(420.5, 423.5);
    testConfig(config);

    // save each output style
    outputs.push_back(txtstream.str());
    }

    // all the outputs should be different
    for (int m=(int)outputs.size();m-->1;)
    {
        unit_assert(outputs[m]!=outputs[m-1]);
    }
    // now convert all delimiters back to a single space
    for (int mm=(int)outputs.size();mm--;)
    {
        // reduce double spaces to single 
        // no, replace_all(outputs[mm],std::string("  "),std::string(" ")); doesn't do it
        for (int n=outputs[mm].size();n-->1;)
        {
            if ((outputs[mm][n]==' ') && (outputs[mm][n-1]==' '))
            {
                outputs[mm] = outputs[mm].substr(0,n-1)+outputs[mm].substr(n);
            }
            if ((outputs[mm][n]==' ') && (outputs[mm][n-1]=='\n'))
            {
                // watch for lines with leading spaces
                outputs[mm] = outputs[mm].substr(0,n-1)+outputs[mm].substr(n);
                outputs[mm][n-1]='\n';
            }
        }
        // and replace single character delimiters
        boost::algorithm::replace_all(outputs[mm],std::string("\t"),std::string(" ")); 
        boost::algorithm::replace_all(outputs[mm],std::string(","),std::string(" "));
    }
    // all the outputs should now be identical
    for (int delimtype=(int)outputs.size();delimtype-->1;)
    {
        unit_assert(outputs[delimtype]==outputs[delimtype-1]);
    }

    // verify that changes to text based configs retains backward compatiblity
    testAnalyzerFamilyArgumentBackwardCompatibility();
}


int main(int argc, char* argv[])
{
    TEST_PROLOG(argc, argv)

    try
    {
        if (argc>1 && !strcmp(argv[1],"-v")) os_ = &cout;
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

