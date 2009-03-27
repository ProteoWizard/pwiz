///
/// eharmony.cpp
///

//my stuff
#include "Matcher.hpp"
#include "pwiz/utility/math/WarpFunction.hpp"
//boost stuff
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/tuple/tuple_comparison.hpp"
// mechanical
#include <vector>
// file i/o
#include <iostream>
#include <fstream>
// QC
#include <stdexcept>

using namespace pwiz::math;
using namespace pwiz::eharmony;
using namespace std;

// look at peakaboo to see how it's ok to define the Config struct here and then do it if it makes sense

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
    usage << "Usage: eharmony [options] [first runID (e.g., 20090109-A-Run)] [second runID (e.g., 20090109-B-Run)]\n"
          << endl;

    // define command line options
    po::options_description od_config("Options");
    od_config.add_options()
        ("inputPath,i", po::value<string>(&config.inputPath)," : specify location of input files")
        ("outputPath,o", po::value<string>(&config.outputPath), " : specify output path")
        ("filename,f", po::value<string>(&config.batchFileName)," : specify file listing input runIDs (e.g., 20090109-B-Run)")
        ("searchNbhdCalculator,n", po::value<vector<string> >(&config.searchNbhdCalculators), " : specify method of defining the neighborhood of a peptide in first run that will be searched for features in second run. \nOptions:\"naive[mzTolerance,rtTolerance]\", \"normalDistribution[stdDevs]\""); // and list options
    // ("warpFunctionCalculator,w", po::value<vector<string> >(&config.warpFunctionCalculators), " : specify method of calculating the rt-calibrating warp function.\nOptions: linear, piecewiseLinear");
    
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


int main(int argc, const char* argv[])
{
    namespace bfs = boost::filesystem;
    bfs::path::default_name_check(bfs::native);

    try
        {
            Config config = parseCommandLine(argc, argv);        
            processFile(config);
            return 0;

        }

    catch (exception& e)
        {
            cout << e.what() << endl;

        }

    catch (...)
        {
            cout << "[eharmony.cpp::main()] Abnormal termination.\n";

        }

    return 1;

}
