//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#define PWIZ_SOURCE

#include "FrequencyEstimatorPhysicalModel.hpp"
#include "TruncatedLorentzianEstimator.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include <iostream>
#include <iomanip>
#include <iterator>
#include <sstream>
#include <fstream>


#include "boost/filesystem/operations.hpp"
#include "boost/filesystem/convenience.hpp"
#include "boost/filesystem/fstream.hpp"
#include "boost/filesystem/exception.hpp"


using namespace std;
using namespace pwiz::frequency;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
namespace bfs = boost::filesystem;


class FrequencyEstimatorPhysicalModelImpl : public FrequencyEstimatorPhysicalModel
{
    public:

    FrequencyEstimatorPhysicalModelImpl(const Config& config);

    virtual Peak estimate(const FrequencyData& fd, 
                          const Peak& initialEstimate) const;

    private:

    Config config_;
    bool logging_;
};


PWIZ_API_DECL
auto_ptr<FrequencyEstimatorPhysicalModel> 
FrequencyEstimatorPhysicalModel::create(const Config& config)
{
    return auto_ptr<FrequencyEstimatorPhysicalModel>(
        new FrequencyEstimatorPhysicalModelImpl(config));
}


FrequencyEstimatorPhysicalModelImpl::FrequencyEstimatorPhysicalModelImpl(const Config& config)
:   config_(config),
    logging_(!config.outputDirectory.empty())
{
    if (logging_) 
        bfs::create_directories(config_.outputDirectory);
}


namespace {
string getFilenameBase()
{
    static int index = 0;
    ostringstream result;
    result << "peak." << index++; // increment peak index
    return result.str();
}
} // namespace


Peak FrequencyEstimatorPhysicalModelImpl::estimate(const FrequencyData& fd, 
                                                   const Peak& initialEstimate) const
{
    string filenameBase = getFilenameBase();
    const string& outputDirectory = config_.outputDirectory;

    // create a window around the peak
    FrequencyData::const_iterator center = fd.findNearest(initialEstimate.getAttribute(Peak::Attribute_Frequency));
    FrequencyData window(fd, center, config_.windowRadius);
    window.noiseFloor(sqrt(fd.variance()));

    // create the estimator
    auto_ptr<TruncatedLorentzianEstimator> estimator = TruncatedLorentzianEstimator::create(); 

    // TODO: clean up TruncatedLorentzianEstimator interface w/Config struct
    // set up estimator intermediate output
    if (logging_)
    {
        bfs::path pathEstimatorOutput = (bfs::path)outputDirectory / (filenameBase + ".est_output"); 
        create_directory(pathEstimatorOutput);
        estimator->outputDirectory(pathEstimatorOutput.string());
    }

    // set up estimator log 
    bfs::path pathEstimatorLog = (bfs::path)outputDirectory / (filenameBase + ".log");
    bfs::ofstream osEstimatorLog;
    if (logging_) osEstimatorLog.open(pathEstimatorLog);
    estimator->log(&osEstimatorLog);

    // run the estimator
    TruncatedLorentzianParameters tlpInit = estimator->initialEstimate(window);
    TruncatedLorentzianParameters tlpFinal = estimator->iteratedEstimate(window, 
                                                                         tlpInit, 
                                                                         config_.iterationCount);
    // write out log files
    if (logging_)
    {
        bfs::path pathWindow = (bfs::path)outputDirectory / (filenameBase + ".cfd");
        window.write(pathWindow.string());

        bfs::path pathWindowSample = (bfs::path)outputDirectory / (filenameBase + ".cfd.sample");
        window.write(pathWindowSample.string(), FrequencyData::Text);

        bfs::path pathTlpInit = (bfs::path)outputDirectory / (filenameBase + ".init.tlp");
        tlpInit.write(pathTlpInit.string());

        bfs::path pathTlpFinal = (bfs::path)outputDirectory / (filenameBase + ".final.tlp");
        tlpFinal.write(pathTlpFinal.string());

        bfs::path pathTlpFinalSample = (bfs::path)outputDirectory / (filenameBase + ".final.tlp.sample");
        bfs::ofstream osTlpFinalSample(pathTlpFinalSample);
        tlpFinal.writeSamples(osTlpFinalSample);
   }

    // return info
    Peak result;
    result.attributes[Peak::Attribute_Frequency] = tlpFinal.f0;
    result.intensity = abs(tlpFinal.alpha);
    result.attributes[Peak::Attribute_Phase] = arg(tlpFinal.alpha);
    result.attributes[Peak::Attribute_Decay] = tlpFinal.tau;
    //TODO: fill in rest of result
    //result.error = ?
    //result.area = ?

    return result;
}


// TODO: recycle
/*    
    bfs::ofstream osSummary;
    if (!outputDirectory.empty()) 
    {
        bfs::path pathSummary = (bfs::path)outputDirectory / "summary.txt";
        osSummary.open(pathSummary); 
        osSummary << "#" // <<  T  tau  alpha  f0    amplitude      phase\n";
                  << setw(4) << "T" << " "  
                  << setw(8) << "tau" << " " 
                  << setw(30) << "alpha" << " "
                  << setw(12) << "f0" << " " 
                  << setw(12) << "magnitude" << " "
                  << setw(7) << "phase" << endl;
    }
*/

//TODO: recycle

/*
osSummary << fixed << setprecision(4)
          << setw(6) << tlpFinal.T << " " 
          << setw(8) << tlpFinal.tau << " " 
          << setw(30) << tlpFinal.alpha << " "
          << setw(12) << tlpFinal.f0 << " " 
          << setw(12) << abs(tlpFinal.alpha) << " "
          << setw(7) << arg(tlpFinal.alpha) << endl;
 */


