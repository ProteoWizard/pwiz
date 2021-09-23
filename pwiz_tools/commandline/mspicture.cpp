//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
// Modifieing author: Robert Burke <robert.burke@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


#include "pwiz_tools/common/MSDataAnalyzerApplication.hpp"
#include "pwiz/analysis/passive/MSDataCache.hpp"
#include "pwiz/analysis/passive/MetadataReporter.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz/analysis/passive/Pseudo2DGel.hpp"
#include "pwiz/analysis/peptideid/PeptideID_pepXML.hpp"
#include "pwiz/analysis/peptideid/PeptideID_flat.hpp"
#include "pwiz/Version.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include <boost/program_options.hpp>
#include <boost/filesystem.hpp>


using namespace pwiz::analysis;
using namespace pwiz::peptideid;
using namespace boost::filesystem;

struct Config
{
    vector<string> filenames;
    string configFilename;
    string outputDirectory;
    string usageOptions;
    string peptideFilename;
    string shape;
    string commands;
    Pseudo2DGel::Config pseudo2dConfig;
    bool verbose;

    Config() : outputDirectory("."), shape("circle"), verbose(false) {}
};

template <typename analyzer_type>
void printCommandUsage(ostream& os)
{
    os << "  " << analyzer_strings<analyzer_type>::id()
       << " " << analyzer_strings<analyzer_type>::argsFormat() << endl
       << "    (" << analyzer_strings<analyzer_type>::description() << ")\n";

    vector<string> usage = analyzer_strings<analyzer_type>::argsUsage();
    for (vector<string>::const_iterator it=usage.begin(); it!=usage.end(); ++it)
        os << "      " << *it << endl;

    os << endl;
}

string usage(const Config& config)
{
    ostringstream oss;
    
    oss << "Usage: mspicture [options] [input_filenames]\n"
        << "Mass Spec Picture - command line accessgeneration of pseudo2D gels from mass spec data files with optional peptide annotation\n"
        << "\n"
        << "Returns:\n"
        << "0 on success, or the number of input files that generated processing errors.\n"
        << "\n"
        << "Options:\n" 
        << "\n"
        << config.usageOptions
        << "\n"
        << "Commands:\n";
    
    vector<string> usage = analyzer_strings<Pseudo2DGel>::argsUsage();
    for (vector<string>::const_iterator it=usage.begin(); it!=usage.end(); ++it)
        oss << "      " << *it << endl;
    
    oss << "\n";
    
    // TODO return -x options processing
    //printCommandUsage<Pseudo2DGel>(oss);

    oss << endl
        << "Questions, comments, and bug reports:\n"
        << "https://github.com/ProteoWizard\n"
        << "support@proteowizard.org\n"
        << "\n"
        << "ProteoWizard release: " << pwiz::Version::str() << endl
        << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    return oss.str();
}

Config parseCommandArgs(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    Config config;

    string usageOptions;

    po::options_description hidden("hidden stuff");
    hidden.add_options()
        ("gray",
         ": use gray-scale gradient slightly grayer than the grey option");
    
    po::options_description od_config("");
    od_config.add_options()
        ("outdir,o",
            po::value<string>(&config.outputDirectory)->default_value(
                config.outputDirectory),
            ": output directory")
        ("config,c", 
            po::value<string>(&config.configFilename),
            ": configuration file (optionName=value) (ignored)")
        ("label,l",
            po::value<string>(&config.pseudo2dConfig.label),
            ": set filename label to xxx")
        ("mzLow",
            po::value<float>(&config.pseudo2dConfig.mzLow),
            ": set low m/z cutoff")
        ("mzHigh",
            po::value<float>(&config.pseudo2dConfig.mzHigh),
            ": set high m/z cutoff")
        ("timeScale",
            po::value<float>(&config.pseudo2dConfig.timeScale),
            ": set scale of time axis")
        ("binCount,b",
            po::value<int>(&config.pseudo2dConfig.binCount),
            ": set histogram bin count")
        ("time,t",
            ": render linearly to time")
        ("scan,s",
            ": render linearly to scans")
        ("zRadius,z",
            po::value<float>(&config.pseudo2dConfig.zRadius),
            ": set intensity function z-score radius [=2]")
        ("width,w",
            po::value<int>(&config.pseudo2dConfig.output_width),
            ": set output bitmap width (default is calculated)")
        ("height,h",
            po::value<int>(&config.pseudo2dConfig.output_height),
            ": set output bitmap height (default is calculated)")
        ("bry",
            ": use blue-red-yellow gradient")
        ("grey",
            ": use grey-scale gradient")
        ("binSum",
            ": sum intensity in bins [default = max intensity]")
        ("ms2locs,m",
            ": indicate masses selected for ms2")
        ("shape",
            po::value<string>(&config.shape),
            ": shape of the pseudo2d gel markup [circle(default)|square].")
        ("pepxml,p",
            po::value<string>(&config.peptideFilename),
            ": pepxml file location")
        ("msi,i",
            po::value<string>(&config.peptideFilename),
            ": msInspect output file location")
        ("flat,f",
            po::value<string>(&config.peptideFilename),
            ": peptide file location (nativeID rt mz score seq)")
        ("commands,x",
         po::value<string>(&config.commands),
            ": processes commands")
        ("verbose,v", ": prints extra information.")
        ("help,h",
            ": print this helpful message.")
        ;

    // save options description

    ostringstream temp;
    temp << od_config;
    usageOptions = temp.str();

    // handle positional arguments


    const char* label_args = "args";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);
   
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args).add(hidden);
    
    po::options_description od_visible;
    od_visible.add(od_config).add(od_args);
    
    // parse command line

    po::variables_map vm;
    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, argv_hack).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    // Set the boolean values
    if (vm.count("bry"))
        config.pseudo2dConfig.bry = true;
    
    if (vm.count("grey"))
        config.pseudo2dConfig.grey = true;
    
    if (vm.count("gray"))
        config.pseudo2dConfig.grey = true;
    
    if (vm.count("binSum"))
        config.pseudo2dConfig.binSum = true;
    
    if (vm.count("ms2locs"))
        config.pseudo2dConfig.ms2 = true;

    if (vm.count("verbose"))
        config.verbose = true;
    
    // remember filenames from command line

    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    // Add the pepxml file if available.
    
    bool ids = false;
    
    if (vm.count("pepxml"))
    {
        ids = true;
        config.pseudo2dConfig.peptide_id =
            shared_ptr<PeptideID>(
                new PeptideID_pepXml(config.peptideFilename)
                );
        //config.pseudo2dConfig.ms2 = true;
    }
    else if (vm.count("flat"))
    {
        ids = true;
        config.pseudo2dConfig.peptide_id =
            shared_ptr<PeptideID>(
                new PeptideID_flat(
                    config.peptideFilename, shared_ptr<FlatRecordBuilder>(
                        new FlatRecordBuilder())));
        //config.pseudo2dConfig.ms2 = true;
    }
    else if (vm.count("msi"))
    {
        ids = true;
        config.pseudo2dConfig.peptide_id =
            shared_ptr<PeptideID>(
                new PeptideID_flat(
                    config.peptideFilename, shared_ptr<FlatRecordBuilder>(
                        new MSInspectRecordBuilder())));
        //config.pseudo2dConfig.ms2 = true;
    }
    
    if (vm.count("scan"))
        config.pseudo2dConfig.binScan = true;

    if (vm.count("time"))
        config.pseudo2dConfig.binScan = false;

    if (config.shape == "square")
        config.pseudo2dConfig.markupShape = Pseudo2DGel::square;
    else
        config.pseudo2dConfig.markupShape = Pseudo2DGel::circle;
    
    //config.pseudo2dConfig.positiveMs2Only = !vm.count("ms2locs") && ids;

    if (vm.count("commands"))
    {
        config.pseudo2dConfig.process(config.commands);
    }
    
    if (vm.count("config"))
    {
        std::ifstream is(config.configFilename.c_str());
        ostringstream oss;
        string line;
        while(getlinePortable(is, line).good())
            oss << line << " ";

        config.pseudo2dConfig.process(oss.str());
    }
    
    config.usageOptions = usageOptions;

    if (vm.count("help"))
        throw runtime_error(usage(config).c_str());


    return config;
}

void initializeAnalyzers(MSDataAnalyzerContainer& analyzers,
                         const Config& config)
{
    shared_ptr<MSDataCache> cache(new MSDataCache);
    analyzers.push_back(cache);
    
    MSDataAnalyzerPtr anal(new Pseudo2DGel(*cache,
                                           config.pseudo2dConfig));
    analyzers.push_back(anal);
}

struct testOut
{
    ostream& operator()(const string s)
    {
        cout << s << endl;
        return cout;
    }
};

int main(int argc, const char* argv[])
{
    size_t tick1 = clock();
    namespace bfs = boost::filesystem;
    try
    {
        int returncode = 0;
        Config config = parseCommandArgs(argc, argv);

        if (config.filenames.empty())
            throw runtime_error(usage(config).c_str());
        // handle multiple filenames, in code written for single filename
        std::vector<std::string> filenames = config.filenames;
        if (filenames.size() > 1)
        {
            config.filenames.resize(1);
            if (config.verbose)
            {
                cout << "Multiple files selected for processing: \n";
                for (int n=0;n<(int)filenames.size();n++)
                    cout << filenames.at(n) << "\n";
            }
        }
        for (int n=0;n<(int)filenames.size();n++)
        {
            try
            {
                // code below is set up to deal with a single
                // file at a time, so just make our current file
                // name be the 0th one
                config.filenames[0] = filenames[n];

                if (config.verbose)
                    cout << "Processing " 
                         << config.filenames.at(0)
                         << " for pictures.\n";
        
                if (!config.filenames.empty())
                    bfs::create_directories(config.outputDirectory);
        
                // Construct the Pseudo2DGel object with an MSDataCache object

                MSDataAnalyzerContainer analyzers;
                initializeAnalyzers(analyzers, config);
        
                // take one file at a time - we manage list so the 0th one is the current one.
                ExtendedReaderList readers;

                MSDataFile msd(config.filenames.at(0), &readers);
                MSDataAnalyzer::DataInfo dataInfo(msd);
        
                dataInfo.sourceFilename = BFS_STRING(path(config.filenames.at(0)).leaf());
                dataInfo.outputDirectory = config.outputDirectory;
                if (config.verbose)
                    dataInfo.log = &cout;
                else
                    dataInfo.log = NULL;

                MSDataAnalyzerDriver driver(analyzers);
        
                driver.analyze(dataInfo);
            }        
            catch (exception& e)
            {
                cerr << e.what() << endl;
                returncode++;
            }
            catch (...)
            {
                cerr << "Caught unknown exception.\n";
                returncode++;
            }
        }
        size_t tick2 = clock();

        if (config.verbose)
        {
            cout << " *** run time:" << 1.*tick2/CLOCKS_PER_SEC
                 << " - " << 1.*tick1/CLOCKS_PER_SEC << " = "
                 << 1.*(tick2 - tick1)/CLOCKS_PER_SEC << endl;
        }
        
        return returncode;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception.\n";
    }

    return 1;
}


