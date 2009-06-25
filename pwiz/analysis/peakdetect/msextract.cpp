//
// msextract.cpp
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
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
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "boost/iostreams/positioning.hpp"
#include "pwiz/data/msdata/SpectrumIterator.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
#include "PeakFamilyDetectorFT.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "boost/tuple/tuple_comparison.hpp"
#include <string>
#include <vector>
#include <map>
#include <iostream>
#include <fstream>

namespace{

using namespace std;

using namespace pwiz;
using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;

struct Config
{
    vector<string> filenames;
    int maxChargeState;
    string inputPath;
    string outputPath;
    string extension;
    bool writeFeatureFile;
    bool writeTSV;
    Config() : maxChargeState(6), inputPath("."), outputPath(".") {}

    string outputFileName(string inputFileName);
};

string Config::outputFileName(string inputFileName)
{
    namespace bfs = boost::filesystem;
    string newFilename = bfs::basename(inputFileName) + this->extension;
    bfs::path fullPath = bfs::path(this->outputPath) / newFilename;
    return fullPath.string(); 

}

Config parseCommandLine(int argc, char* argv[])
{
    namespace po = boost::program_options;
    Config config;

    ostringstream usage;
    usage << "Usage: msextract [options] [file]\n"
          << endl;
    
    // define command line options
    po::options_description od_config("Options");
    od_config.add_options()
        ("inputPath,i", po::value<string>(&config.inputPath), " : specify input path")
        ("outputPath,o", po::value<string>(&config.outputPath), " : specify output path")
        ("writeFeatureFile"," : write xml representation of detected features (.features file) ")
        ("writeTSV", " : write tab-separated file");

    // append options to usage string
    usage << od_config;

    // handle positional args
    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);

    po::options_description od_parse;
    od_parse.add(od_config).add(od_args);
    
    // parse command line
    po::variables_map vm;
    po::store(po::command_line_parser(argc, (char**)argv).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // get filenames
    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    if (vm.count("writeFeatureFile"))
        config.writeFeatureFile = true;
    
    if (vm.count("writeTSV"))
        config.writeTSV = true;

    // usage if no files
    if (config.filenames.empty())
        throw runtime_error(usage.str());

    return config;
}

void processFile(const string& file, Config& config)
{
    string filename = (config.inputPath + "/" + file).c_str();

    ostream* os_log_ = 0;
    PeakFamilyDetectorFT::Config pfd_config;
    pfd_config.log = os_log_;

    // set calibration parameters

    MSDataFile msd(filename);
    
    bool done = false;
    vector<InstrumentConfigurationPtr>::iterator icp_it = msd.instrumentConfigurationPtrs.begin();
    while (!done && icp_it != msd.instrumentConfigurationPtrs.end())
        {
           

            if ((*icp_it)->componentList.analyzer(0).hasCVParam(MS_FT_ICR))
                {
                    pfd_config.cp = CalibrationParameters::thermo_FT();
		    cout << "thermo_FT params set" << endl;
                    done = true;

                }

            else if ((*icp_it)->componentList.analyzer(0).hasCVParam(MS_orbitrap))
                {
                    pfd_config.cp = CalibrationParameters::thermo_Orbitrap();
		    cout << "thermo_Orbitrap params set" << endl;
                    done = true;

                }

            else ++icp_it;

        }
    
    if (!done) throw runtime_error("[FeatureDetectorSimple] Unsupported mass analyzer.");

    //    pfd_config.isotopeMaxChargeState = config.maxChargeState;
        
    PeakFamilyDetectorFT detector(pfd_config);   
    FeatureDetectorSimple fds(detector);

    FeatureField output_features;
    fds.detect(msd, output_features);

    if (config.writeFeatureFile)
        {
            config.extension = ".features";

            vector<FeaturePtr> features;
            FeatureField::iterator it = output_features.begin();
            for( ; it != output_features.end(); ++it) features.push_back(*it);

            FeatureFile featureFile;
            featureFile.features = features;

            ofstream ofs(config.outputFileName(filename).c_str());
            XMLWriter writer(ofs);
            featureFile.write(writer);

        }

    if (config.writeTSV)
        {
            config.extension = ".features.tsv";
            FeatureField::iterator it = output_features.begin();
            ofstream ofs(config.outputFileName(filename).c_str());
            ofs << "mzMonoisotopic\tretentionTime\tretentionTimeMin\tretentionTimeMax\ttotalIntensity\n";
            for(; it != output_features.end(); ++it)
                {
                    ofs << (*it)->mz << "\t" << (*it)->retentionTime  << "\t" << (*it)->retentionTimeMin() << "\t" << (*it)->retentionTimeMax() << "\t" << (*it)->totalIntensity << "\n";

                }

        }

    return;

}

void go(Config& config)
{
    namespace bfs = boost::filesystem;
    bfs::create_directories(config.outputPath);

    // process each file
    for (vector<string>::const_iterator it=config.filenames.begin(); it!=config.filenames.end(); ++it)
        {
        try
            {
                processFile(*it, config);
            }

        catch (exception& e)

            {
                cout << e.what() << endl;
                cout << "Error processing file " << *it << endl;
            }

        catch (...)
            {
                cout << "Unknown error.\n";
                cout << "Error processing file " << *it << endl;
            }
        }
}

} // anonymous namespace

int main(int argc, char* argv[])
{
     try
         {
             Config config = parseCommandLine(argc, argv);
             go(config);
             return 0;

         }

     catch (exception& e)
         {
             cout << e.what() << endl;

         }

     catch (...)
         {
             cout << "[FeatureDetectorSimple.cpp::main()] Abnormal termination.\n";

         }

     return 1;

}


