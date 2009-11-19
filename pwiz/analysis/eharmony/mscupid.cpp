//
// $Id$
//
//
// Original author: Kate Hoff <katherine.hoff@proteowizard.org>
//
// Copyright 2009 Center for Applied Molecular Medicine
//   University of Southern California, Los Angeles, CA
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

///
/// mscupid.cpp
///

#include "AMTDatabase.hpp"
#include "WarpFunction.hpp"
#include "boost/filesystem.hpp"
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"

using namespace std;
using namespace pwiz;
using namespace pwiz::eharmony;

namespace{

    WarpFunctionEnum translateWarpFunction(const string& wfe_string)
    {

        const char* linear = "linear";
        const char* piecewiseLinear = "piecewiseLinear";
        const char* curr = wfe_string.c_str();
        if (!strncmp(linear, curr, 6))
            {
                cout << "Translated: linear" << endl;
                return Linear;

            }

        if (!strncmp(piecewiseLinear, curr, 15))
            {
                cout << "Translated: piecewiseLinear" << endl;
                return PiecewiseLinear;

            }
        cout << "Translated: default" << endl;

        return Default;

    }

}
struct Config
{    
    double _threshold;
    int _islandize;
    int _roc;
    WarpFunctionEnum _warpFunction;
    vector<string> filenames;

    Config() : _threshold(.9420),
               _warpFunction(Default),
               _islandize(0),
               _roc(0)
    {}

};

void go(const Config& config)
{
    ifstream ifs_pep((config.filenames.at(0).c_str()));
    ifstream ifs_feat((config.filenames.at(1).c_str()));
    ifstream ifs_db((config.filenames.at(2).c_str()));

    cout << "[mscupid] Reading pep.xml file ... " << endl;
    MSMSPipelineAnalysis mspa;
    mspa.read(ifs_pep);
    PidfPtr pidf_query(new PeptideID_dataFetcher(mspa));

    cout << "[mscupid] Reading .features file ... " << endl;
    FdfPtr fdf_query(new Feature_dataFetcher(ifs_feat));
    
    cout << "[mscupid] Reading AMT database ... " << endl;

    boost::shared_ptr<AMTContainer> amt(new AMTContainer());
    amt->read(ifs_db);

    if (config._islandize)
        {
            cout << "[mscupid] Islandizing database ..." << endl;

            IslandizedDatabase db(amt);

            AMTContainer amt2;
            amt2.read(ifs_db);

            FdfPtr dummy(new Feature_dataFetcher());

            DfcPtr dfc(new DataFetcherContainer(pidf_query, db._peptides, fdf_query, dummy));
            dfc->adjustRT((pidf_query->getRtAdjustedFlag()+1)%2, false);
            dfc->warpRT(config._warpFunction);

            cout << "[mscupid] Querying database ... " << endl;
            PeptideMatcher pm(pidf_query, db._peptides);

            string outputDir = "./amtdb_query";
            boost::filesystem::create_directory(outputDir);

            NormalDistributionSearch nds;
            nds._threshold = (config._threshold);
            nds.calculateTolerances(dfc);

            db.query(dfc,config._warpFunction,nds,mspa, outputDir);


            return;
        }

    cout << "[mscupid] Using un-Islandized database ... "  << endl;

    AMTDatabase db(*amt);

    AMTContainer amt2;
    amt2.read(ifs_db);

    FdfPtr dummy(new Feature_dataFetcher());

    DfcPtr dfc(new DataFetcherContainer(pidf_query, db._peptides, fdf_query, dummy));
    dfc->adjustRT((pidf_query->getRtAdjustedFlag()+1)%2, false);
    dfc->warpRT(config._warpFunction);

    cout << "[mscupid] Querying database ... " << endl;
    PeptideMatcher pm(pidf_query, db._peptides);

    string outputDir = "./amtdb_query";
    boost::filesystem::create_directory(outputDir);

    NormalDistributionSearch nds;
    nds._threshold = (config._threshold);
    nds.calculateTolerances(dfc);

    db.query(dfc,config._warpFunction,nds,mspa, outputDir);

    return;

}

Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;
    Config config;

    ostringstream usage;
    usage << "Usage: mscupid [options] filename.pep.xml filename.features database.xml\n"
          << endl;
    
    // define local variables to be read in as strings and translated
    string warpFunctionCalculator;

    // define command line options
    po::options_description od_config("Options");
    od_config.add_options()
        ("warpFunctionCalculator,w", po::value<string>(&warpFunctionCalculator), " : specify method of calculating the rt-calibrating warp function. \nOptions:\nlinear, piecewiseLinear\nDefault:\nno calibration")
        ("threshold,t", po::value<double>(&config._threshold)->default_value(config._threshold)," : specify threshold for match acceptance.")
        ("generateROCStats,r", po::value<int>(&config._roc)->default_value(config._roc)," : calculate ROC curve statistics using MS2 identifications to determine true/false positives/negatives. 0 = false, 1 = true.")
        ("islandize", po::value<int>(&config._islandize)->default_value(config._islandize), " : Postprocess AMT database with islandization. 0 = false, 1 = true.");

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

    // translate local variables
    if (warpFunctionCalculator.size() > 0) config._warpFunction = translateWarpFunction(warpFunctionCalculator);

    // usage if incorrect                                                                                                
    if (config.filenames.empty())
        throw runtime_error(usage.str());

    return config;

}

int main(int argc, const char* argv[])
{
    try
        {
            Config config = parseCommandLine(argc, argv);
            go(config);

            return 0;
        }

    catch (exception& e)
        {
            cerr << e.what() << endl;
 
        }

   
    catch (...)
        {
            cerr << "[mscupid] Caught unknown exception." << endl;
 
        }
 
    return 1;

}
