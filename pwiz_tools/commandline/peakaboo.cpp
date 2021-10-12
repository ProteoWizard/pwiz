//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, California
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


#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/analysis/peakdetect/PeakFamilyDetectorFT.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "boost/program_options.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Timer.hpp"

using namespace pwiz::analysis;
using namespace pwiz::data;
using namespace pwiz::cv;
using namespace pwiz::msdata;
using namespace pwiz::util;
using namespace pwiz::minimxml;


struct Config
{
    vector<string> filenames;
    string outputPath;
    string extension;
    size_t scanBegin;
    size_t scanEnd;
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
                                cvTermInfo(massAnalyzerType).name).c_str());
        }
    }
}


struct HasLowerMZ
{
    bool operator()(const MZIntensityPair& a, const MZIntensityPair& b) {return a.mz < b.mz;}
};


void processFile(const string& filename, const Config& config)
{
    cout << "\nProcessing file: " << filename << endl; 
    string outputFilename = config.outputFilename(filename);
    cout << "outputFilename: " << outputFilename << endl; 

    // open file

    FullReaderList readers;
    MSDataFile msd(filename, &readers);

    if (!msd.run.spectrumListPtr)
        throw runtime_error("[peakaboo] No SpectrumList.");

    cout << "scans: " << msd.run.spectrumListPtr->size() << endl;
    
    // instantiate peak family detector

    shared_ptr<PeakFamilyDetector> pfd = createPeakFamilyDetector(msd);    

    // construct peak data

    peakdata::PeakData peakData;
    peakData.sourceFilename = BFS_STRING(boost::filesystem::path(filename).filename());

    peakData.software.name = "peakaboo";
    peakData.software.version = "1.2";
    peakData.software.source = "Center for Applied Molecular Medicine, University of Southern California";

    peakData.software.parameters.push_back(peakdata::Software::Parameter("scanBegin", lexical_cast<string>(config.scanBegin)));
    peakData.software.parameters.push_back(peakdata::Software::Parameter("scanEnd", lexical_cast<string>(config.scanEnd)));
    peakData.software.parameters.push_back(peakdata::Software::Parameter("mzLow", lexical_cast<string>(config.mzLow)));
    peakData.software.parameters.push_back(peakdata::Software::Parameter("mzHigh", lexical_cast<string>(config.mzHigh)));

    // scan number filtering

    IntegerSet scanNumberSet(config.scanBegin, config.scanEnd);

    msd.run.spectrumListPtr = SpectrumListPtr(new SpectrumList_Filter(msd.run.spectrumListPtr,
                                  SpectrumList_FilterPredicate_ScanNumberSet(scanNumberSet)));

    // initialize cache

    MSDataCache cache;
    cache.open(msd);
    
    for (size_t i=0; i<cache.size(); i++)
    {
        try
        {
            // get spectrum info from cache

            const SpectrumInfo info = cache.spectrumInfo(i, true);

            // TODO: use general SpectrumList filtering

            if (info.msLevel != 1) continue;

            if (info.massAnalyzerType != MS_FT_ICR && 
                info.massAnalyzerType != MS_orbitrap)
            {
                cerr << "Skipping non-FT index " << i << endl;
                continue;
            }

            // fill in scan metadata

            peakdata::Scan pdScan;
            pdScan.index = info.index;
            pdScan.nativeID = info.id;
            pdScan.scanNumber = id::valueAs<int>(info.id, "scan"); // TODO: decide what to do with scan numbers
            pdScan.retentionTime = info.retentionTime;

            if (info.data.empty())
            {
                cerr << "Scan index " << i << " has no data.\n";
                continue;
            }

            // find peaks

            const MZIntensityPair* begin = lower_bound(&info.data.front(), &info.data.back(), 
                                                       MZIntensityPair(config.mzLow,0), HasLowerMZ());

            const MZIntensityPair* end = lower_bound(&info.data.front(), &info.data.back(), 
                                                     MZIntensityPair(config.mzHigh,0), HasLowerMZ());

            pfd->detect(begin, end, pdScan.peakFamilies);
            peakData.scans.push_back(pdScan);
        }
        catch (...)
        {
            cerr << "Caught exception in scan index " << i << endl;
        }
    }

    ofstream os(outputFilename.c_str());
    os.precision(12);
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
        ("scanBegin",
            po::value<size_t>(&config.scanBegin)->default_value(config.scanBegin),
            ": set first scan")
        ("scanEnd",
            po::value<size_t>(&config.scanEnd)->default_value(config.scanEnd),
            ": set last scan")
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
            getlinePortable(is, filename);
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

