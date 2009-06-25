///
/// eharmony.hpp
///

#ifndef _EHARMONY_HPP_
#define _EHARMONY_HPP_

#include "WarpFunction.hpp"
#include "SearchNeighborhoodCalculator.hpp"
#include "pwiz/data/misc/MinimumPepXML.hpp"
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/tuple/tuple_comparison.hpp"
#include <vector>
#include <iostream>
#include <fstream>
#include <stdexcept>

using namespace pwiz::eharmony;
using namespace pwiz::data::pepxml;
using namespace std;

struct Config
{
    std::vector<std::string> filenames;
    std::string inputPath;
    std::string outputPath;
    std::string batchFileName;

    bool generateAMTDatabase;

    bool rtCalibrate;

    std::string warpFunctionCalculator;
    std::string searchNeighborhoodCalculator;
    std::string normalDistributionSearch;
    std::string distanceAttribute;

    SearchNeighborhoodCalculator parsedSNC;
    NormalDistributionSearch parsedNDS;
    WarpFunctionEnum warpFunction;


    Config() : inputPath("."), outputPath("."), generateAMTDatabase(true) {}
    bool operator==(const Config& that);
    bool operator!=(const Config& that);

};

bool Config::operator==(const Config& that)
{
    return filenames == that.filenames &&
        inputPath == that.inputPath &&
        outputPath == that.outputPath &&
        batchFileName == that.batchFileName &&
        generateAMTDatabase == that.generateAMTDatabase &&
        rtCalibrate == that.rtCalibrate &&
        warpFunctionCalculator == that.warpFunctionCalculator &&
        searchNeighborhoodCalculator == that.searchNeighborhoodCalculator &&
        normalDistributionSearch == that.normalDistributionSearch &&
        parsedSNC == that.parsedSNC &&
        parsedNDS == that.parsedNDS &&
        warpFunction == that.warpFunction;

}


bool Config::operator!=(const Config& that)
{
    return !(*this == that);

}

class Matcher
{

public:

    Matcher(){}
    Matcher(Config& config);

    void checkSourceFiles();
    void readSourceFiles();
    void processFiles();


private:

    Config _config;

    std::map<std::string, PidfPtr> _peptideData;
    std::map<std::string, FdfPtr> _featureData;

};



void processFile(Config& config)
{
    Matcher matcher(config);
    return;

}


Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;
    Config config;

    ostringstream usage;
    usage << "Usage: eharmony [options] [filenames] \n"
          << endl;

    // define command line options
    po::options_description od_config("Options");
    od_config.add_options()
        ("inputPath,i", po::value<string>(&config.inputPath)," : specify location of input files")
        ("outputPath,o", po::value<string>(&config.outputPath), " : specify output path")
        ("filename,f", po::value<string>(&config.batchFileName)," : specify file listing input runIDs (e.g., 20090109-B-Run)")
        
        ("naiveSearchNeighborhood,n", po::value<string>(&config.searchNeighborhoodCalculator), " : specify definition of a naive search neighborhood as naive[mzTolerance, rtTolerance]")
        ("normalDistributionSearchNeighborhood,d", po::value<string>(&config.normalDistributionSearch), " : specify definition of a search neighborhood based on the distribution of retention time differences between shared MS2s in the runs as normalDistribution[numberOfStDevs]")
        ("warpFunctionCalculator,w", po::value<string >(&config.warpFunctionCalculator), " : specify method of calculating the rt-calibrating warp function.\nOptions: linear, piecewiseLinear")
        ("distanceAttribute,r", po::value<string>(&config.distanceAttribute), " : specify distance attribute.\n Options: randomDistance, rtDifferenceDistance, hammingDistance");
    
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
    po::store(po::command_line_parser(argc, (char**)argv).options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // get filenames
    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    // usage if incorrect
    if (config.filenames.empty() && config.batchFileName == "")
        throw runtime_error(usage.str());

    return config;

}

#endif
