//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
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

#define PWIZ_SOURCE

#include "pwiz/utility/misc/Std.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/fstream.hpp"
#include "boost/accumulators/accumulators.hpp"
#include "boost/accumulators/statistics/max.hpp"
#include "boost/accumulators/statistics/min.hpp"
#include "boost/accumulators/statistics/stats.hpp"
#include "boost/accumulators/statistics/mean.hpp"
#include "boost/accumulators/statistics/variance.hpp"
#include "boost/program_options.hpp"
#include "pwiz/utility/misc/MSIHandler.hpp"
#include "pwiz/analysis/eharmony/Bin.hpp"

using namespace pwiz::util;
using namespace boost::accumulators;
namespace bfs = boost::filesystem;

namespace {

const char* noLocalMZFeaturesHeader = "# features w/in mz_window\n"
    "scan\tmz\ttime\t# in m/z_window\n";
const char* noLocalTimeFeaturesHeader = "# features w/in time_window\n"
    "scan\tmz\ttime\t# in time_window\n";
const char* nearestMZNeighborHeader = "nearest neighbor in m/z w/in "
    "time_window\nscan\tmz\ttime\tother_scan\tother_m/z\tother_time\n";
const char* nearestTimeNeighborHeader = "nearest neighbor in time "
    "w/in mz_window\nscan\tmz\ttime\tother scan\tother mz\tother time\n";
}

ostream *os_ = NULL;

// struct Config
//
// Holds the current settings for input/output, empty pixel params,
// and windows.
struct Config
{
    double mz_window;
    double time_window;

    bool do_pixel_analysis;
    double pixel_mz;
    double pixel_time;
    
    string in_filename;
    string out_filename;

    bool verbose;
    bool loquacious;
    
    Config()
        : mz_window(5.0), time_window(10.0),
          do_pixel_analysis(true), pixel_mz(5.0), pixel_time(10.0),
          out_filename(""),
          verbose(false), loquacious(false)    {}
};


class MSIStats : public MSIHandler
{
public:
    MSIStats() {}

    virtual bool updateRecord(const std::vector<std::string>& fields);

    void setDmz(double dmz)
    {
        this->dmz = dmz;
    }
    
    double getDmz() const
    {
        return dmz;
    }
    
    void setDtime(double dtime)
    {
        this->dtime = dtime;
    }
    
    double getDtime()
    {
        return dtime;
    }
    
//private:
    typedef accumulator_set< float, features< tag::min, tag::max, tag::mean, tag::variance > > float_acc ;
    typedef accumulator_set< float, features< tag::min, tag::max > > float_minmax ;
    
    // min/max variables
    float_minmax mz;
    float_minmax time;

    // grid variables
    double dmz;
    double dtime;

    pwiz::Bin<size_t> bin;
};

Config processCommandline(int argc, char** argv)
{
    namespace po=boost::program_options;
    
    Config config;

    ostringstream usage;
    usage << "Usage: msistats [options] <filename>\n";

    po::options_description od_config("Options");
    od_config.add_options()
        ("mz,m",
         po::value<double>(&config.mz_window)->default_value(config.mz_window),
         ": Windows to use for finding nearest time neighbor.")
        ("time,t",
         po::value<double>(&config.time_window)->default_value(
             config.time_window),
         ": Window to use for finding nearest mz neighbors")
        ("out,o",
         po::value<string>(&config.out_filename)->default_value(
             config.out_filename),
         ": Name of file to write output to. ")
        ("verbose,v", ": Be verbose.")
        ("help,h", ": Output this help.");

    po::options_description od_pix("Empty Pixel Options");
    od_pix.add_options()
        ("pmz",
         po::value<double>(&config.pixel_mz),
         ": width for empty pixel analysis. Otherwise set to mz option value")
        ("ptime",
         po::value<double>(&config.pixel_time),
         ": width for empty pixel analysis. Otherwise set to time option value");
    
    // Add positional arguments.
  
	usage << od_config << od_pix;

    // handle positional arguments

    const char* label_in_file = "in_filename";

    po::options_description od_args;
    od_args.add_options()
        (label_in_file,
         po::value<string>(&config.in_filename) , "");

	po::positional_options_description pod_args;
    pod_args.add(label_in_file, 1);
   
    po::options_description od_parse;
    od_parse.add(od_config).add(od_pix).add(od_args);
  
    // parse command line
  
    po::variables_map vm;
    po::store(po::command_line_parser(argc, (char**)argv).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    if (vm.count("pmz") == 0)
        config.pixel_mz = config.mz_window;
    
    if (vm.count("ptime") == 0)
        config.pixel_time = config.time_window;
    
    if (vm.count("verbose") > 0)
        config.verbose = true;

    if (vm.count("verbose") > 1)
        config.loquacious = true;
    
    if (config.in_filename.size() == 0 || vm.count("help") > 0)
        throw runtime_error(usage.str());

    return config;
}

bool MSIStats::updateRecord(const std::vector<std::string>& fields)
{
    bool result = MSIHandler::updateRecord(fields);

    if (result)
    {
        MSIHandler::Record record = MSIHandler::lastRecord();
        time(record.time);
        mz(record.mz);

        bin.update(1, pair<int, int>(record.mz, record.time));
    }
    
    return result;
}

MSIStats getStats(const Config& config)
{
    MSIStats handler;
    handler.setDmz(config.mz_window);
    handler.setDtime(config.time_window);
    handler.bin.rebin(config.mz_window, config.time_window);
        
    TabReader reader;
    
    reader.setHandler(&handler);

    reader.process(config.in_filename.c_str());

    return handler;
}

void printStats(const Config& config, const MSIStats& handler)
{
    double minTime = boost::accumulators::min(handler.time),
        maxTime = boost::accumulators::max(handler.time),
        minMz = boost::accumulators::min(handler.mz),
        maxMz = boost::accumulators::max(handler.mz);
    
    cout << "[time] " << minTime
         << "\t" << maxTime
         << "\n";

    cout << "[m/z] " << minMz
         << "\t" << maxMz
         << "\n";

    cout << "window(mz, time)=" << config.mz_window
         << ", " << config.time_window << "\n";

    ostringstream oss;
    vector<string> lines;

    int minBinTime = minTime / config.time_window;
    int maxBinTime = maxTime / config.time_window;
    int minBinMz = minMz / config.mz_window;
    int maxBinMz = maxMz / config.mz_window;

    vector<size_t> xTotals(maxBinMz-minBinMz, 0.),
        yTotals(maxBinTime-minBinTime, 0.);

    for (int y=minBinTime; y<=maxBinTime; y++)
    {
        for (int x=minBinMz; x<=maxBinMz; x++)
        {
            size_t count = handler.bin.count(pair<int, int>(x, y));
            oss << count;
//            xTotals[x - minBinMz] += count;
//            yTotals[y - minBinTime] += count;

            if (x != maxBinMz)
                oss << "\t";
        }
        string line = oss.str();
        lines.push_back(line);
        oss.clear();
    }

    cout << "\t";
    for (size_t i=0; i< yTotals.size(); i++)
    {
        cout << yTotals.at(i);
        if (i != yTotals.size()-1)
            cout << "\t";
    }
    cout << "\n";

    
    for (size_t i=0; i< lines.size(); i++)
    {
        cout << xTotals.at(i) << "\t";
        cout << lines.at(i) << "\n";
    }

    cerr << "end of function\n";
}

///
/// Test code for MSInspect stats generator.
///
int main(int argc, char** argv)
{
    Config config;
    
    try
    {
        bfs::path datapath = ".";

        config = processCommandline(argc, argv);

        datapath = config.in_filename;
        
        MSIStats stats = getStats(config);
        printStats(config, stats);

        return 0;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    
    return 0;
}
