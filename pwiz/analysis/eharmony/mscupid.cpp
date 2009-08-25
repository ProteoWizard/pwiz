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
    WarpFunctionEnum _warpFunction;
    vector<string> filenames;

    Config() : _threshold(.9420),
               _warpFunction(Default)
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
    //    AMTDatabase db(amt);;
    IslandizedDatabase id(amt);

    AMTContainer amt2;
    amt2.read(ifs_db);

    FdfPtr dummy(new Feature_dataFetcher());

    DfcPtr dfc(new DataFetcherContainer(pidf_query, id._peptides, fdf_query, dummy));
    dfc->adjustRT((pidf_query->getRtAdjustedFlag()+1)%2, false);

    cout << "[mscupid] Querying database ... " << endl;
    PeptideMatcher pm(pidf_query, id._peptides);

    string outputDir = "./amtdb_query";
    boost::filesystem::create_directory(outputDir);

    NormalDistributionSearch nds;
    nds._threshold = (config._threshold);
    nds.calculateTolerances(dfc);

    id.query(dfc,config._warpFunction,nds,mspa, outputDir);

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
        ("threshold,t", po::value<double>(&config._threshold)->default_value(config._threshold)," : specify threshold for match acceptance.");

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
            cerr << "[cupid] Caught unknown exception." << endl;
 
        }
 
    return 1;

}
