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


#include "pwiz/analysis/peakdetect/PeakFamilyDetectorFT.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp" // TODO: pwiz_tools/common/FullReaderList
#include "pwiz/utility/misc/Timer.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
#include "boost/program_options.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/shared_ptr.hpp"
#include <iostream>
#include <sstream>
#include <fstream>
#include <stdexcept>
#include <vector>
#include <map>


using namespace pwiz;
using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;
using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;


struct Config
{
    vector<string> filenames;
    string outputPath;
    string extension;
    size_t indexBegin;
    size_t indexEnd;
    double mzLow;
    double mzHigh;

    Config()
    :   outputPath("."), 
        extension(".peaks"),
        indexBegin(1),
        indexEnd(numeric_limits<int>::max()),
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


CVID getMassAnalyzerType(const MSData& msd)
{
    CVID result = CVID_Unknown;

    for (vector<InstrumentConfigurationPtr>::const_iterator it=msd.instrumentConfigurationPtrs.begin(),
         end=msd.instrumentConfigurationPtrs.end(); it!=end; ++it)
    {
        if (!it->get()) continue;
        const InstrumentConfiguration& ic = **it;
        result = ic.componentList.analyzer(0).cvParamChild(MS_mass_analyzer_type).cvid;

        // return FT or orbi (rather than ion trap) for hybrid instruments
        if (result == MS_FT_ICR ||
            result == MS_orbitrap)
            return result;
    }

    return result;
}


shared_ptr<PeakFamilyDetector> createPeakFamilyDetector(const MSData& msd)
{
    CVID massAnalyzerType = getMassAnalyzerType(msd);

    switch (massAnalyzerType)
    {
        case MS_FT_ICR:
        {
            PeakFamilyDetectorFT::Config config;
            config.cp = CalibrationParameters::thermo_FT();
            return shared_ptr<PeakFamilyDetector>(new PeakFamilyDetectorFT(config));
        }
        case MS_orbitrap:
        {
            PeakFamilyDetectorFT::Config config;
            config.cp = CalibrationParameters::thermo_Orbitrap();
            return shared_ptr<PeakFamilyDetector>(new PeakFamilyDetectorFT(config));
        }
        default:
        {
            throw runtime_error(("[peakaboo] Mass analyzer not supported: " + 
                                cvinfo(massAnalyzerType).name).c_str());
        }
    }
}


void processFile(const string& filename, const Config& config)
{
    cout << "\nProcessing file: " << filename << endl; 
    string outputFilename = config.outputFilename(filename);
    cout << "outputFilename: " << outputFilename << endl; 

    // open file

    ExtendedReaderList readers;
    MSDataFile msd(filename, &readers);

    if (!msd.run.spectrumListPtr)
        throw runtime_error("[peakaboo] No SpectrumList.");

    cout << "scans: " << msd.run.spectrumListPtr->size() << endl;
    
    // instantiate peak family detector

    shared_ptr<PeakFamilyDetector> pfd = createPeakFamilyDetector(msd);    

    // construct peak data

    peakdata::PeakData peakData;
    peakData.sourceFilename = boost::filesystem::path(filename).leaf();

    peakData.software.name = "peakaboo";
    peakData.software.version = "1.1";
    peakData.software.source = "Spielberg Family Center for Applied Proteomics";

    size_t indexBegin = max((size_t)0, config.indexBegin);
    size_t indexEnd = min(msd.run.spectrumListPtr->size(), config.indexEnd+1);

    peakData.software.parameters.push_back(make_pair("indexBegin", lexical_cast<string>(config.indexBegin)));
    peakData.software.parameters.push_back(make_pair("indexEnd", lexical_cast<string>(config.indexEnd)));
    peakData.software.parameters.push_back(make_pair("mzLow", lexical_cast<string>(config.mzLow)));
    peakData.software.parameters.push_back(make_pair("mzHigh", lexical_cast<string>(config.mzHigh)));

    MSDataCache cache;
    cache.open(msd);
    
    for (size_t i=indexBegin; i<indexEnd; i++)
    {
        const SpectrumInfo info = cache.spectrumInfo(i, true);

        // TODO: use a SpectrumList filter here, and for index range
        if (info.massAnalyzerType != MS_FT_ICR && 
            info.massAnalyzerType != MS_orbitrap)
        {
            cerr << "Skipping non-FT index " << i << endl;
            continue;
        }

        peakdata::Scan pdScan;
        //pdScan.scanNumber = i; //TODO: fill in index and nativeID, other metadata
        pdScan.retentionTime = info.retentionTime;

        if (info.data.empty())
        {
            cerr << "Scan index " << i << " has no data.\n";
            continue;
        }

        // TODO: find begin and end MZIntensityPairs properly
        const MZIntensityPair* begin = &info.data[0];
        const MZIntensityPair* end = &info.data[0] + info.data.size();

        pfd->detect(begin, end, pdScan.peakFamilies);
        peakData.scans.push_back(pdScan);
    }

    cout << "peak data scans: " << peakData.scans.size() << endl; // TODO: remove

    ofstream os(outputFilename.c_str());
    XMLWriter writer(os);
    peakData.write(writer);
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
        ("indexBegin",
            po::value<size_t>(&config.indexBegin)->default_value(config.indexBegin),
            ": first 0-based index (n.b. usually scanNumber - 1)")
        ("indexEnd",
            po::value<size_t>(&config.indexEnd)->default_value(config.indexEnd),
            ": last 0-based index")
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

