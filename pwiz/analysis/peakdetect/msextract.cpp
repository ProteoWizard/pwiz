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
#include "FeatureDetectorPeakel.hpp"
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


using namespace std;
using namespace pwiz;
using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using boost::shared_ptr;


MZTolerance translateMzTol(string& curr)
{
    if (curr.find("mz") != string::npos)
        {
            size_t pos = curr.find("mz");
            curr.erase(pos,2);

            double mzTol = boost::lexical_cast<double>(curr);
            MZTolerance mzTolerance(mzTol);
            mzTolerance.units = MZTolerance::MZ;

            return mzTolerance;

        }
    else if (curr.find("ppm") != string::npos)
        {
            size_t pos = curr.find("ppm");
            curr.erase(pos,3);

            double mzTol = boost::lexical_cast<double>(curr);
            MZTolerance mzTolerance(mzTol);
            mzTolerance.units = MZTolerance::MZ;

            return mzTolerance;
        }

    else throw runtime_error("[msextract] Bad MZTolerance");
}

struct Config
{
    vector<string> filenames;
    int maxChargeState;
    string featureDetectorImplementation;
    string inputPath;
    string outputPath;
    bool writeFeatureFile;
    bool writeTSV;
    bool writeLog;
    FeatureDetectorPeakel::Config fdpConfig;

    Config() 
    :   maxChargeState(6), featureDetectorImplementation("Simple"), 
        inputPath("."), outputPath("."),
        writeFeatureFile(true), writeTSV(true), writeLog(false)
    {}

    string outputFileName(const string& inputFileName, const string& extension) const;
};


string Config::outputFileName(const string& inputFileName, const string& extension) const
{
    namespace bfs = boost::filesystem;
    string newFilename = bfs::basename(inputFileName) + extension;
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
    
    // local variables that will be translated to MZTolerance objects
    string pgmz;
    string ppmz;

    // define command line options
    po::options_description od_config("Options");
    od_config.add_options()
        ("featureDetectorImplementation,f", po::value<string>(&config.featureDetectorImplementation)->default_value(config.featureDetectorImplementation), " : specify implementation of FeatureDetector to use.  Options: Simple, PeakelFarmer")
        ("noiseCalculatorZLevel,z", po::value<double>(&config.fdpConfig.noiseCalculator_2Pass.zValueCutoff)->default_value(config.fdpConfig.noiseCalculator_2Pass.zValueCutoff), " : specify cutoff for NoiseCalculator_2Pass")
        ("peakFinderSNRWindowRadius,w", po::value<size_t>(&config.fdpConfig.peakFinder_SNR.windowRadius)->default_value(config.fdpConfig.peakFinder_SNR.windowRadius), " : specify window radius for PeakFinder_SNR")
        ("peakFinderZThreshold,Z", po::value<double>(&config.fdpConfig.peakFinder_SNR.zValueThreshold)->default_value(config.fdpConfig.peakFinder_SNR.zValueThreshold), " : specify z threshold for PeakFinder_SNR")
        ("peakFitterWindowRadius,W", po::value<size_t>(&config.fdpConfig.peakFitter_Parabola.windowRadius)->default_value(config.fdpConfig.peakFitter_Parabola.windowRadius), " : specify window radius for PeakFitter_Parabola")
        ("peakelGrowerMZTol,m", po::value<string>(&pgmz), " : specify mz tolerance for PeakelGrower_Proximity")
        ("peakelGrowerRTTol,r", po::value<double>(&config.fdpConfig.peakelGrower_Proximity.rtTolerance)->default_value(config.fdpConfig.peakelGrower_Proximity.rtTolerance), " : specify rt tolerance for PeakelGrower_Proximity")
        ("peakelPickerMinCharge,c", po::value<size_t>(&config.fdpConfig.peakelPicker_Basic.minCharge)->default_value(config.fdpConfig.peakelPicker_Basic.minCharge), " : specify min charge for PeakelPicker_Basic")
        ("peakelPickerMaxCharge,C", po::value<size_t>(&config.fdpConfig.peakelPicker_Basic.maxCharge)->default_value(config.fdpConfig.peakelPicker_Basic.maxCharge), " : specify max charge for PeakelPicker_Basic")
        ("peakelPickerMinMPS,s", po::value<size_t>(&config.fdpConfig.peakelPicker_Basic.minMonoisotopicPeakelSize)->default_value(config.fdpConfig.peakelPicker_Basic.minMonoisotopicPeakelSize), " : specify min monoisotopic peakel size for PeakelPicker_Basic")
        ("peakelPickerMZTol,M", po::value<string>(&ppmz), " : specify mz tolerance for PeakelPicker_Basic")
        ("peakelPickerRTTol,P", po::value<double>(&config.fdpConfig.peakelPicker_Basic.rtTolerance)->default_value(config.fdpConfig.peakelPicker_Basic.rtTolerance), " : specify rt tolerance for PeakelPicker_Basic")
        ("peakelPickerMinPC,n", po::value<size_t>(&config.fdpConfig.peakelPicker_Basic.minPeakelCount)->default_value(config.fdpConfig.peakelPicker_Basic.minPeakelCount), " : specify min peakel count for PeakelPicker_Basic")
        ("inputPath,i", po::value<string>(&config.inputPath)->default_value(config.inputPath), " : specify input path")
        ("outputPath,o", po::value<string>(&config.outputPath)->default_value(config.outputPath), " : specify output path")
        ("writeFeatureFile", po::value<bool>(&config.writeFeatureFile)->default_value(config.writeFeatureFile), " : write xml representation of detected features (.features file) ")
        ("writeTSV", po::value<bool>(&config.writeTSV)->default_value(config.writeTSV), " : write tab-separated file")
        ("writeLog", po::value<bool>(&config.writeLog)->default_value(config.writeLog), " : write log file (for debugging)");
    
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

    // usage if no files
    if (config.filenames.empty())
        throw runtime_error(usage.str());

    if (pgmz.size()>0) config.fdpConfig.peakelGrower_Proximity.mzTolerance = translateMzTol(pgmz);
    if (ppmz.size()>0) config.fdpConfig.peakelPicker_Basic.mzTolerance = translateMzTol(ppmz);

    return config;
}


shared_ptr<FeatureDetector> createFeatureDetector(const MSData& msd,
                                                  const Config& config,
                                                  ostream* log)
{
    if (config.featureDetectorImplementation == "Simple") 
    {
        PeakFamilyDetectorFT::Config pfd_config;
        //pfd_config.log = log; // TODO: too much output!

        // set calibration parameters
        bool done = false;
        vector<InstrumentConfigurationPtr>::const_iterator icp_it = msd.instrumentConfigurationPtrs.begin();
        while (!done && icp_it != msd.instrumentConfigurationPtrs.end())
        {
            if ((*icp_it)->componentList.analyzer(0).hasCVParam(MS_FT_ICR))
            {
                pfd_config.cp = CalibrationParameters::thermo_FT();
                if (log) *log << "thermo_FT params set" << endl;
                done = true;
            }

            else if ((*icp_it)->componentList.analyzer(0).hasCVParam(MS_orbitrap))
            {
                pfd_config.cp = CalibrationParameters::thermo_Orbitrap();
                if (log) *log << "thermo_Orbitrap params set" << endl;
                done = true;
            }

            else ++icp_it;
        }
        
        if (!done) throw runtime_error("[FeatureDetectorSimple] Unsupported mass analyzer.");

        // pfd_config.isotopeMaxChargeState = config.maxChargeState; // TODO: Kate, why is this disabled?
            
        shared_ptr<PeakFamilyDetectorFT> detector(new PeakFamilyDetectorFT(pfd_config));
        return shared_ptr<FeatureDetector>(new FeatureDetectorSimple(detector));
    }
    else if (config.featureDetectorImplementation == "PeakelFarmer")
    {           
        FeatureDetectorPeakel::Config temp = config.fdpConfig;
        temp.log = log; // TODO: remove or propagate in FeatureDetectorPeakel::create()
        temp.peakelGrower_Proximity.log = log;
        temp.peakelPicker_Basic.log = log;
        return FeatureDetectorPeakel::create(temp);
    }
    else 
    {
        throw runtime_error(("[msextract]  Unsupported featureDetectorImplementation: " + config.featureDetectorImplementation).c_str());
    }
}


void processFile(const string& file, const Config& config)
{
    string filename = (config.inputPath + "/" + file).c_str(); // TODO: bfs

    ofstream log;
    if (config.writeLog)
        log.open(config.outputFileName(filename, ".log").c_str());

    MSDataFile msd(filename);
    shared_ptr<FeatureDetector> fd = createFeatureDetector(msd, config, config.writeLog ? &log : 0);

    FeatureField output_features;
    fd->detect(msd, output_features);

    if (config.writeFeatureFile)
    {
        vector<FeaturePtr> features;
        FeatureField::iterator it = output_features.begin();
        for( ; it != output_features.end(); ++it) features.push_back(*it);

        FeatureFile featureFile;
        featureFile.features = features;

        ofstream ofs(config.outputFileName(filename, ".features").c_str());
        XMLWriter writer(ofs);
        featureFile.write(writer);
    }

    if (config.writeTSV)
    {
        FeatureField::iterator it = output_features.begin();
        ofstream ofs(config.outputFileName(filename, ".features.tsv").c_str());
        ofs << "mzMonoisotopic\tretentionTime\tretentionTimeMin\tretentionTimeMax\ttotalIntensity\n";
        for(; it != output_features.end(); ++it)
        {
            ofs << (*it)->mz << "\t" << (*it)->retentionTime  << "\t" << (*it)->retentionTimeMin() << "\t" << (*it)->retentionTimeMax() << "\t" << (*it)->totalIntensity << "\n";
        }
    }
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
             cout << "[msextract.cpp::main()] Abnormal termination.\n";

         }

     return 1;

}


