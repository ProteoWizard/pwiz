//
// peakaboo.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


//#include "MassPeakDetectorFT.hpp"

#include "pwiz/analysis/peakdetect/PeakFamilyDetectorFT.hpp"
#include "pwiz/utility/misc/Timer.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
#include "boost/program_options.hpp"
#include "boost/lexical_cast.hpp"
#include <iostream>
#include <sstream>
#include <fstream>
#include <stdexcept>
#include <vector>
#include <map>


using namespace pwiz;
using namespace pwiz::data;
using namespace pwiz::util;
using namespace std;


struct Config
{
    vector<string> filenames;
    string outputPath;
    string extension;
    int scanBegin;
    int scanEnd;
    double mzLow;
    double mzHigh;

    Config()
    :   outputPath("."), 
        extension(".peaks"),
        scanBegin(1),
        scanEnd(numeric_limits<int>::max()),
        mzLow(200),
        mzHigh(2000)
    {}

    string outputFilename(const string& inputFilename) const;
};


string Config::outputFilename(const string& filename) const
{
    namespace bfs = boost::filesystem;
    string newFilename = bfs::basename(filename) + this->extension;
    bfs::path fullPath = bfs::path(this->outputPath) / newFilename;
    return fullPath.string(); 
}


void processFile(const string& filename, const Config& config)
{
    cout << "\nProcessing file: " << filename << endl; 
    string outputFilename = config.outputFilename(filename);
    cout << "outputFilename: " << outputFilename << endl; 

/*    
    // TODO: use MSData
    using msrun::MSRun;
    auto_ptr<MSRun> msrun = MSRun::create(filename);
    cout << "scans: " << msrun->scanCount() << endl;

    // TODO: use PeakFamilyDetector
    // instantiate MassPeakDetector
    MassPeakDetectorFT::Config mpdConfig;
    //mpdConfig.log = &cout;
    mpdConfig.cp = data::CalibrationParameters::thermo(); // TODO: grab cp from mzxml
    if (msrun->instrument(0)->model()=="LTQ Orbitrap XL")
    {
        cout << "Using Orbitrap settings." << endl;
        mpdConfig.cp.setOrbi();
    }
    auto_ptr<MassPeakDetector> mpd = MassPeakDetectorFT::create(mpdConfig);

    // construct peak data

    peakdata::PeakData peakData;
    peakData.sourceFilename = boost::filesystem::path(filename).leaf();

    peakData.software.name = "peakaboo";
    peakData.software.version = "1.0";
    peakData.software.source = "Spielberg Family Center for Applied Proteomics";

    int scanBegin = max(1, config.scanBegin);
    int scanEnd = min(msrun->scanCount(), (long)config.scanEnd);

    using boost::lexical_cast;
    peakData.software.parameters.push_back(make_pair("scanBegin", lexical_cast<string>(scanBegin)));
    peakData.software.parameters.push_back(make_pair("scanEnd", lexical_cast<string>(scanEnd)));
    peakData.software.parameters.push_back(make_pair("mzLow", lexical_cast<string>(config.mzLow)));
    peakData.software.parameters.push_back(make_pair("mzHigh", lexical_cast<string>(config.mzHigh)));

    for (int i=scanBegin; i<=scanEnd; i++)
    {
        // TODO: use MSData, probably MSDataCache
        auto_ptr<msrun::Scan> scan = msrun->scan(i);
        if (!scan.get())
        {
            cerr << "Warning: Missing scan " << i << endl;
            continue;
        }

        if (scan->instrumentType() != msrun::Instrument::Type_FT) // TODO: handle Orbi also
        {
            cerr << "Skipping non-FT scan " << i << endl;
            continue;
        }

        // TODO: use PeakFamilyDetector to fill in pdScan.peakFamilies
        // TODO: fill in pdScan metadata
        peakdata::Scan pdScan;
        pdScan.scanNumber = i;
        pdScan.retentionTime = scan->retentionTime();
        vector<MassPeakDetector::MassPeak> peaks = mpd->findPeaks(*scan, 
                                                                 config.mzLow, 
                                                                 config.mzHigh, 
                                                                 pdScan);
        
        peakData.scans.push_back(pdScan);
    }

    // TODO: re-implement peakdata IO functions using new minimxml writer
    // output peak data XML

    ofstream os(outputFilename.c_str());
    peakData.writeXML(os);
*/
}


void go(const Config& config)
{
    cout << "outputPath: " << config.outputPath << endl;
    cout << "extension: " << config.extension << endl;

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


Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: peakaboo [options] [files]+\n"
          << endl;

    Config config;
    string filelistFilename;

    po::options_description od_config("Options");
    od_config.add_options()
        ("filelist,f",
            po::value<string>(&filelistFilename),
            ": specify text file containing filenames")
        ("outdir,o",
            po::value<string>(&config.outputPath)->default_value(config.outputPath),
            ": set output directory")
        ("ext,e",
            po::value<string>(&config.extension)->default_value(config.extension),
            ": set extension for output files")
        ("scanBegin",
            po::value<int>(&config.scanBegin)->default_value(config.scanBegin),
            ": beginning scan")
        ("scanEnd",
            po::value<int>(&config.scanEnd)->default_value(config.scanEnd),
            ": ending scan")
        ("mzLow",
            po::value<double>(&config.mzLow)->default_value(config.mzLow),
            ": set mz low cutoff")
        ("mzHigh",
            po::value<double>(&config.mzHigh)->default_value(config.mzHigh),
            ": set mz high cutoff")
        ;

    // append options description to usage string

    usage << od_config;

    // handle positional arguments

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

    // remember filenames from command line

    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    // parse filelist if required

    if (!filelistFilename.empty())
    {
        ifstream is(filelistFilename.c_str());
        while (is)
        {
            string filename;
            getline(is, filename);
            if (is) config.filenames.push_back(filename);
        }
    }

    // check stuff

    if (config.filenames.empty())
        throw runtime_error(usage.str());

    return config;
}


int main(int argc, const char* argv[])
{
    Timer timer;

    namespace bfs = boost::filesystem;
    bfs::path::default_name_check(bfs::native);

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
        cout << "[peakaboo.cpp::main()] Abnormal termination.\n";
    }

    return 1;
}

