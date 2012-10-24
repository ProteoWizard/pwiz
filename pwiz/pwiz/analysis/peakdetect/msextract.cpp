//
// $Id$
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
#include "PeakFamilyDetectorFT.hpp"
#include "FeatureModeler.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/data/msdata/Serializer_mzML.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/SpectrumIterator.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "pwiz/data/misc/PeakData.hpp"
#include "pwiz/utility/minimxml/XMLWriter.hpp"
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
#include "boost/iostreams/positioning.hpp"
#include "boost/tuple/tuple_comparison.hpp"
#include "pwiz/utility/misc/Std.hpp"


using namespace pwiz::cv;
using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::data::peakdata;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;
namespace bfs = boost::filesystem;


struct Config
{
    vector<string> filenames;
    vector<string> filters;

    string featureDetectorImplementation;
    string outputPath;

    bool writeFeatureFile;
    bool writeTSV;
    bool writeLog;

    bool useFeatureModeler;

    FeatureDetectorPeakel::Config fdpConfig;
    int maxChargeState;

    Config() 
    :   featureDetectorImplementation("Simple"), 
        outputPath("."),
        writeFeatureFile(true), writeTSV(true), writeLog(false), 
        useFeatureModeler(false),
        maxChargeState(6)
    {}

    void write_program_options_config(ostream& os) const;
    string outputFileName(const string& inputFileName, const string& extension) const;
};


void Config::write_program_options_config(ostream& os) const
{
    // write out configuration options in format parseable by program_options

    os << "featureDetectorImplementation=" << featureDetectorImplementation << endl;
    os << "useFeatureModeler=" << useFeatureModeler << endl;
    os << "noiseCalculator_2Pass.zValueCutoff=" << fdpConfig.noiseCalculator_2Pass.zValueCutoff << endl;
    os << "peakFinder_SNR.windowRadius=" << fdpConfig.peakFinder_SNR.windowRadius << endl;
    os << "peakFinder_SNR.zValueThreshold=" << fdpConfig.peakFinder_SNR.zValueThreshold << endl;
    os << "peakFinder_SNR.preprocessWithLogarithm=" << fdpConfig.peakFinder_SNR.preprocessWithLogarithm << endl;
    os << "peakFitter_Parabola.windowRadius=" << fdpConfig.peakFitter_Parabola.windowRadius << endl;
    os << "peakelGrower_Proximity.mzTolerance=" << fdpConfig.peakelGrower_Proximity.mzTolerance << endl;
    os << "peakelGrower_Proximity.rtTolerance=" << fdpConfig.peakelGrower_Proximity.rtTolerance << endl;
    os << "peakelPicker_Basic.minCharge=" << fdpConfig.peakelPicker_Basic.minCharge << endl;
    os << "peakelPicker_Basic.maxCharge=" << fdpConfig.peakelPicker_Basic.maxCharge << endl;
    os << "peakelPicker_Basic.minMonoisotopicPeakelSize=" << fdpConfig.peakelPicker_Basic.minMonoisotopicPeakelSize << endl;
    os << "peakelPicker_Basic.mzTolerance=" << fdpConfig.peakelPicker_Basic.mzTolerance << endl;
    os << "peakelPicker_Basic.rtTolerance=" << fdpConfig.peakelPicker_Basic.rtTolerance << endl;
    os << "peakelPicker_Basic.minPeakelCount=" << fdpConfig.peakelPicker_Basic.minPeakelCount << endl;
    os << "#maxChargeState=TODO" << endl; // TODO
}


ostream& operator<<(ostream& os, const Config& config)
{
    config.write_program_options_config(os);
    
    os << "filenames:\n  ";
    copy(config.filenames.begin(), config.filenames.end(), ostream_iterator<string>(os, "\n  "));
    os << endl;

    os << "filters:\n  ";
    copy(config.filters.begin(), config.filters.end(), ostream_iterator<string>(os,"\n  "));
    os << endl;

    return os;
}


string Config::outputFileName(const string& inputFileName, const string& extension) const
{
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

    // input file of config parameters
    string configFilename;
    bool printDefaultConfig = false;

    // define command line options

    po::options_description od_config("Options");
    od_config.add_options()
        ("config,c", po::value<string>(&configFilename), ": specify file of config options, in format optionName=optionValue")
        ("defaults,d", po::value<bool>(&printDefaultConfig)->zero_tokens(), ": print configuration defaults")
        ("outputPath,o", po::value<string>(&config.outputPath)->default_value(config.outputPath), ": specify output path")
        ("featureDetectorImplementation,f", po::value<string>(&config.featureDetectorImplementation)->default_value(config.featureDetectorImplementation), ": specify implementation of FeatureDetector to use.  Options: Simple, PeakelFarmer")
        ("useFeatureModeler,m", po::value<bool>(&config.useFeatureModeler)->default_value(config.useFeatureModeler)->zero_tokens(), ": post-process with feature modeler")
        ("writeFeatureFile", po::value<bool>(&config.writeFeatureFile)->default_value(config.writeFeatureFile), ": write xml representation of detected features (.features file) ")
        ("writeTSV", po::value<bool>(&config.writeTSV)->default_value(config.writeTSV), ": write tab-separated file")
        ("writeLog", po::value<bool>(&config.writeLog)->default_value(config.writeLog), ": write log file (for debugging)")
        ("filter", po::value< vector<string> >(&config.filters), (": add a spectrum list filter\n" + SpectrumListFactory::usage()).c_str())
        ;

    po::options_description od_config_peakel("FeatureDetectorPeakel Options");
    od_config_peakel.add_options()
        ("noiseCalculator_2Pass.zValueCutoff", po::value<double>(&config.fdpConfig.noiseCalculator_2Pass.zValueCutoff)->default_value(config.fdpConfig.noiseCalculator_2Pass.zValueCutoff), "")
        ("peakFinder_SNR.windowRadius", po::value<size_t>(&config.fdpConfig.peakFinder_SNR.windowRadius)->default_value(config.fdpConfig.peakFinder_SNR.windowRadius), "")
        ("peakFinder_SNR.zValueThreshold", po::value<double>(&config.fdpConfig.peakFinder_SNR.zValueThreshold)->default_value(config.fdpConfig.peakFinder_SNR.zValueThreshold), "")
        ("peakFinder_SNR.preprocessWithLogarithm", po::value<bool>(&config.fdpConfig.peakFinder_SNR.preprocessWithLogarithm)->default_value(config.fdpConfig.peakFinder_SNR.preprocessWithLogarithm), "")
        ("peakFitter_Parabola.windowRadius", po::value<size_t>(&config.fdpConfig.peakFitter_Parabola.windowRadius)->default_value(config.fdpConfig.peakFitter_Parabola.windowRadius), "")
        ("peakelGrower_Proximity.mzTolerance", po::value<MZTolerance>(&config.fdpConfig.peakelGrower_Proximity.mzTolerance), "")
        ("peakelGrower_Proximity.rtTolerance", po::value<double>(&config.fdpConfig.peakelGrower_Proximity.rtTolerance)->default_value(config.fdpConfig.peakelGrower_Proximity.rtTolerance), "")
        ("peakelPicker_Basic.minCharge", po::value<size_t>(&config.fdpConfig.peakelPicker_Basic.minCharge)->default_value(config.fdpConfig.peakelPicker_Basic.minCharge), "")
        ("peakelPicker_Basic.maxCharge", po::value<size_t>(&config.fdpConfig.peakelPicker_Basic.maxCharge)->default_value(config.fdpConfig.peakelPicker_Basic.maxCharge), "")
        ("peakelPicker_Basic.minMonoisotopicPeakelSize", po::value<size_t>(&config.fdpConfig.peakelPicker_Basic.minMonoisotopicPeakelSize)->default_value(config.fdpConfig.peakelPicker_Basic.minMonoisotopicPeakelSize), "")
        ("peakelPicker_Basic.mzTolerance", po::value<MZTolerance>(&config.fdpConfig.peakelPicker_Basic.mzTolerance), "")
        ("peakelPicker_Basic.rtTolerance", po::value<double>(&config.fdpConfig.peakelPicker_Basic.rtTolerance)->default_value(config.fdpConfig.peakelPicker_Basic.rtTolerance), "")
        ("peakelPicker_Basic.minPeakelCount", po::value<size_t>(&config.fdpConfig.peakelPicker_Basic.minPeakelCount)->default_value(config.fdpConfig.peakelPicker_Basic.minPeakelCount), "")
        ;    
    
    // append options to usage string

    usage << od_config << endl << od_config_peakel;

    usage << "\n\nExamples:\n"
          << "\n"
          << "# print default configuration parameters to config.txt\n"
          << "msextract -d > config.txt\n"
          << "\n"
          << "# run using parameters in config.txt, output in outputdir\n"
          << "msextract -c config.txt -o outputdir file1.mzML file2.mzML\n"
          << "\n"
          << "# filters: select scan numbers\n"
          << "msextract file1.mzML --filter \"scanNumber [500,1000]\"\n"
          << endl
          << "\n";   


    // handle positional args
    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);

    po::options_description od_parse;
    od_parse.add(od_config).add(od_config_peakel).add(od_args);
    
    // parse command line
    po::variables_map vm;
    po::store(po::command_line_parser(argc, (char**)argv).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    if (configFilename.size() > 0)
        {
            ifstream is(configFilename.c_str());

            if (is)
                {
                    cout << "Reading configuration file " << configFilename << "\n\n";
                    po::store(parse_config_file(is, od_parse), vm);
                    po::notify(vm);
                }
            else
                {
                    cout << "Unable to read configuration file " << configFilename << "\n\n";
                }
        }

    // get filenames
    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    // special handling

    if (printDefaultConfig)
    {
        Config defaultConfig;
        defaultConfig.write_program_options_config(cout);
        throw runtime_error("");
    }

    if (config.filenames.empty())
        throw runtime_error(usage.str());

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
        temp.log = log; // specific to the file we're processing
        return FeatureDetectorPeakel::create(temp);
    }
    else 
    {
        throw runtime_error(("[msextract]  Unsupported featureDetectorImplementation: " + config.featureDetectorImplementation).c_str());
    }
}


void writeOutputFiles(const FeatureField& features, const string& filename, const Config& config)
{
    if (config.writeFeatureFile)
    {
        FeatureFile featureFile;
        copy(features.begin(), features.end(), back_inserter(featureFile.features));

        ofstream ofs(config.outputFileName(filename, ".features").c_str());
        XMLWriter writer(ofs);
        featureFile.write(writer);
    }

    if (config.writeTSV)
    {
        ofstream ofs(config.outputFileName(filename, ".features.tsv").c_str());
        ofs << "# mzMonoisotopic\tretentionTime\tretentionTimeMin\tretentionTimeMax\ttotalIntensity\tscore\terror\n";
        for (FeatureField::const_iterator it=features.begin(); it!=features.end(); ++it)
            ofs << (*it)->mz << "\t" << (*it)->retentionTime  << "\t" << (*it)->retentionTimeMin() << "\t" 
                << (*it)->retentionTimeMax() << "\t" << (*it)->totalIntensity << "\t"
                << (*it)->score << "\t" << (*it)->error
                << "\n";
    }
}


void processFile(const string& filename, const Config& config)
{
    shared_ptr<ofstream> log;
    if (config.writeLog)
        log = shared_ptr<ofstream>(new ofstream(config.outputFileName(filename,".log").c_str()));

    MSDataFile msd(filename);
    SpectrumListFactory::wrap(msd, config.filters);

    shared_ptr<FeatureDetector> fd = createFeatureDetector(msd, config, log.get());

    FeatureField features;
    fd->detect(msd, features);
    FeatureField* features_to_output = &features;

    FeatureField features_modeled;

    if (config.useFeatureModeler)
    {
        FeatureModeler_Gaussian fm;
        fm.fitFeatures(features, features_modeled);
        features_to_output = &features_modeled;
    }

    writeOutputFiles(*features_to_output, filename, config);
}


void go(Config& config)
{
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
         cout << "Config:\n" << config << endl;
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


