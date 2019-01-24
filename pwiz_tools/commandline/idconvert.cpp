//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2011 Vanderbilt University - Nashville, TN 37232
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


#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/identdata/DefaultReaderList.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
//#include "pwiz/data/identdata/IO.hpp"
#include "pwiz/data/identdata/Version.hpp"
#include "pwiz/Version.hpp"
#include "boost/program_options.hpp"

using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::identdata;
using namespace pwiz::util;


struct Config
{
    vector<string> filenames;
    //vector<string> filters;
    string outputPath;
    string extension;
    bool verbose;
    IdentDataFile::WriteConfig writeConfig;
    //string contactFilename;
    //bool merge;

    Config()
    :   outputPath("."), verbose(false)//, merge(false)
    {}

    string outputFilename(const string& inputFilename, const IdentData& inputIdentData, bool multipleOutputs) const;
};


string Config::outputFilename(const string& filename, const IdentData& idd, bool multipleOutputs) const
{
    if (idd.dataCollection.inputs.spectraData.empty())
        throw runtime_error("[Config::outputFilename] no spectraData elements");

    string sourceName;
    if (multipleOutputs)
        sourceName = idd.id;
    if (sourceName.empty())
        sourceName = bfs::basename(idd.dataCollection.inputs.spectraData[0]->name);
    if (sourceName.empty())
        sourceName = bfs::basename(filename);
    
    // this list is for Windows; it's a superset of the POSIX list
    string illegalFilename = "\\/*:?<>|\"";
    BOOST_FOREACH(char& c, sourceName)
        if (illegalFilename.find(c) != string::npos)
            c = '_';

    bfs::path fullPath = bfs::path(outputPath) / (sourceName + extension);
    return fullPath.string(); 
}


ostream& operator<<(ostream& os, const Config& config)
{
    os << "format: " << config.writeConfig << endl;
    os << "outputPath: " << config.outputPath << endl;
    os << "extension: " << config.extension << endl; 
    //os << "contactFilename: " << config.contactFilename << endl;
    os << endl;

    /*os << "filters:\n  ";
    copy(config.filters.begin(), config.filters.end(), ostream_iterator<string>(os,"\n  "));
    os << endl;*/

    os << "filenames:\n  ";
    copy(config.filenames.begin(), config.filenames.end(), ostream_iterator<string>(os,"\n  "));
    os << endl;

    return os;
}


Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: idconvert [options] [filemasks]\n"
          << "Convert mass spec identification file formats.\n"
          << "\n"
          << "Return value: # of failed files.\n"
          << "\n";
        
    Config config;
    string filelistFilename;
    string configFilename;

    bool format_text = false;
    bool format_mzIdentML = false;
    bool format_pepXML = false;
    //bool gzip = false;

    po::options_description od_config("Options");
    od_config.add_options()
        ("filelist,f",
            po::value<string>(&filelistFilename),
            ": specify text file containing filenames")
        ("outdir,o",
            po::value<string>(&config.outputPath)->default_value(config.outputPath),
            ": set output directory ('-' for stdout) [.]")
        ("config,c", 
            po::value<string>(&configFilename),
            ": configuration file (optionName=value)")
        ("ext,e",
            po::value<string>(&config.extension)->default_value(config.extension),
            ": set extension for output files [mzid|pepXML|txt]")
        ("mzIdentML",
            po::value<bool>(&format_mzIdentML)->zero_tokens(),
            ": write mzIdentML format [default]")
        ("pepXML",
            po::value<bool>(&format_pepXML)->zero_tokens(),
            ": write pepXML format")
        ("text",
            po::value<bool>(&format_text)->zero_tokens(),
            ": write hierarchical text format")
        ("verbose,v",
            po::value<bool>(&config.verbose)->zero_tokens(),
            ": display detailed progress information")
        /*("contactInfo,i",
            po::value<string>(&config.contactFilename),
            ": filename for contact info")
        ("gzip,g",
            po::value<bool>(&gzip)->zero_tokens(),
            ": gzip entire output file (adds .gz to filename)")
        ("filter",
            po::value< vector<string> >(&config.filters),
            ": add a spectrum list filter")
        ("merge",
            po::value<bool>(&config.merge)->zero_tokens(),
            ": create a single output file from multiple input files by merging file-level metadata and concatenating spectrum lists")*/
        ;

    // append options description to usage string

    usage << od_config;

    // extra usage

    usage << "Examples:\n"
          << endl
          << "# convert sequest.pepXML to sequest.mzid\n"
          << "idconvert sequest.pepXML\n"
          << endl
          << "# convert sequest.protXML to sequest.mzid\n"
          << "# Also reads any pepXML file referenced in the \n"
          << "# protXML file if available.  If the protXML \n"
          << "# file has been moved from its original location, \n"
          << "# the pepXML will still be found if it has also \n"
          << "# been moved to the same position relative to the \n"
          << "# protXML file. This relative position is determined \n"
          << "# by reading the protXML protein_summary:summary_xml \n"
          << "# and protein_summary_header:source_files values.\n"
          << "idconvert sequest.protXML\n"
          << endl
          << "# convert mascot.mzid to mascot.pepXML\n"
          << "idconvert mascot.mzid --pepXML\n"
          << endl
          << "# put output file in my_output_dir\n"
          << "idconvert omssa.pepXML -o my_output_dir\n"
          << endl
          << "# use a configuration file\n"
          << "idconvert xtandem.pep.xml -c config.txt\n"
          << endl
          << "# example configuration file\n"
          << "pepXML=true\n"
          //<< "gzip=true\n"
          << endl
          << endl

          << "Questions, comments, and bug reports:\n"
          << "https://github.com/ProteoWizard\n"
          << "support@proteowizard.org\n"
          << "\n"
          << "ProteoWizard release: " << pwiz::Version::str() << " (" << pwiz::Version::LastModified() << ")" << endl
          << "ProteoWizard IdentData: " << pwiz::identdata::Version::str() << " (" << pwiz::identdata::Version::LastModified() << ")" << endl
          << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    if (argc <= 1)
        throw usage_exception(usage.str());

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

    // parse config file if required

    if (!configFilename.empty())
    {
        ifstream is(configFilename.c_str());

        if (is)
        {
            cout << "Reading configuration file " << configFilename << "\n\n";
            po::store(parse_config_file(is, od_config), vm);
            po::notify(vm);
        }
        else
        {
            cout << "Unable to read configuration file " << configFilename << "\n\n";
        }
    }

    // remember filenames from command line

    if (vm.count(label_args))
    {
        config.filenames = vm[label_args].as< vector<string> >();

        // expand the filenames by globbing to handle wildcards
        vector<bfs::path> globbedFilenames;
        BOOST_FOREACH(const string& filename, config.filenames)
            if (expand_pathmask(bfs::path(filename), globbedFilenames) == 0)
                cout <<  "[idconvert] no files found matching \"" << filename << "\"" << endl;

        config.filenames.clear();
        BOOST_FOREACH(const bfs::path& filename, globbedFilenames)
            config.filenames.push_back(filename.string());
    }

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
        throw user_error("[idconvert] No files specified.");

    int count = format_text + format_mzIdentML + format_pepXML;
    if (count > 1) throw user_error("[idconvert] Multiple format flags specified.");
    if (format_text) config.writeConfig.format = IdentDataFile::Format_Text;
    if (format_mzIdentML) config.writeConfig.format = IdentDataFile::Format_MzIdentML;
    if (format_pepXML) config.writeConfig.format = IdentDataFile::Format_pepXML;

    //config.writeConfig.gzipped = gzip; // if true, file is written as .gz

    if (config.extension.empty())
    {
        switch (config.writeConfig.format)
        {
            case IdentDataFile::Format_Text:
                config.extension = ".txt";
                break;
            case IdentDataFile::Format_MzIdentML:
                config.extension = ".mzid";
                break;
            case IdentDataFile::Format_pepXML:
                config.extension = ".pepXML";
                break;
            default:
                throw user_error("[idconvert] Unsupported format."); 
        }
        /*if (config.writeConfig.gzipped) 
        {
            config.extension += ".gz";
        }*/
    }

    return config;
}


/*void addContactInfo(MSData& msd, const string& contactFilename)
{
    ifstream is(contactFilename.c_str());
    if (!is)
    {
        cerr << "unable to read contact info: " << contactFilename << endl; 
        return;
    }

    Contact contact;
    IO::read(is, contact);
    msd.fileDescription.contacts.push_back(contact);
}*/


struct UserFeedbackIterationListener : public IterationListener
{
    virtual Status update(const UpdateMessage& updateMessage)
    {
        int index = updateMessage.iterationIndex;
        int count = updateMessage.iterationCount;

        
        // when the index is 0, create a new line
        if (index == 0)
            cout << endl;

        cout << updateMessage.message;
        if (index > 0)
        {
            cout << ": " << index+1;
            if (count > 0)
                cout << "/" << count;
        }

        // add tabs to erase all of the previous line
        cout << "\t\t\t\r" << flush;

        return Status_Ok;
    }
};


/*void calculateSourceFilePtrSHA1(const SourceFilePtr& sourceFilePtr)
{
    calculateSourceFileSHA1(*sourceFilePtr);
}*/


/*int mergeFiles(const vector<string>& filenames, const Config& config, const ReaderList& readers)
{
    vector<MSDataPtr> msdList;
    int failedFileCount = 0;

    BOOST_FOREACH(const string& filename, filenames)
    {
        try
        {
            cout << "processing file: " << filename << endl;
            readers.read(filename, msdList);
        }
        catch (exception& e)
        {
            ++failedFileCount;
            cerr << "Error reading file " << filename << ":\n" << e.what() << endl;
        }
    }

    // handle progress updates if requested

    IterationListenerRegistry iterationListenerRegistry;
    UserFeedbackIterationListener feedback;
    // update on the first spectrum, the last spectrum, the 100th spectrum, the 200th spectrum, etc.
    const size_t iterationPeriod = 100;
    iterationListenerRegistry.addListener(feedback, iterationPeriod);
    IterationListenerRegistry* pILR = config.verbose ? &iterationListenerRegistry : 0;

    try
    {
        MSDataMerger msd(msdList);

        cout << "calculating source file checksums" << endl;
        // calculate SHA1 checksums
        for_each(msd.fileDescription.sourceFilePtrs.begin(),
                 msd.fileDescription.sourceFilePtrs.end(),
                 &calculateSourceFilePtrSHA1);

        if (!config.contactFilename.empty())
            addContactInfo(msd, config.contactFilename);

        SpectrumListFactory::wrap(msd, config.filters);

        string outputFilename = config.outputFilename("merged-spectra", msd);
        cout << "writing output file: " << outputFilename << endl;

        if (config.outputPath == "-")
            MSDataFile::write(msd, cout, config.writeConfig, pILR);
        else
            MSDataFile::write(msd, outputFilename, config.writeConfig, pILR);
    }
    catch (exception& e)
    {
        failedFileCount = (int)filenames.size();
        cerr << "Error merging files: " << e.what() << endl;
    }

    return failedFileCount;
}*/


void processFile(const string& filename, const Config& config, const ReaderList& readers)
{
    // handle progress updates if requested

    IterationListenerRegistry iterationListenerRegistry;
    // update on the first spectrum, the last spectrum, the 100th spectrum, the 200th spectrum, etc.
    const double iterationPeriod = 0.5;
    iterationListenerRegistry.addListenerWithTimer(IterationListenerPtr(new UserFeedbackIterationListener), iterationPeriod);
    IterationListenerRegistry* pILR = config.verbose ? &iterationListenerRegistry : 0; 

    // read in data file

    cout << "processing file: " << filename << endl;

    vector<IdentDataPtr> iddList;
    Reader::Config readerConfig;
    readerConfig.iterationListenerRegistry = pILR;
    readerConfig.ignoreProteinDetectionList = config.writeConfig.format == IdentDataFile::Format_pepXML;
    readers.read(filename, iddList, readerConfig);

    for (size_t i=0; i < iddList.size(); ++i)
    {
        IdentData& idd = *iddList[i];
        try
        {
            // process the data 

            /*if (!config.contactFilename.empty())
                addContactInfo(msd, config.contactFilename);*/

            // write out the new data file
            string outputFilename = config.outputFilename(filename, idd, iddList.size() > 1);
            cout << "writing output file: " << outputFilename << endl;

            if (config.outputPath == "-")
                IdentDataFile::write(idd, outputFilename, cout, config.writeConfig);
            else
                IdentDataFile::write(idd, outputFilename, config.writeConfig, pILR);
        }
        catch (exception& e)
        {
            cerr << "Error writing analysis " << (i+1) << " in " << bfs::path(filename).leaf() << ":\n" << e.what() << endl;
        }
    }
    cout << endl;
}


int go(const Config& config)
{
    cout << config;

    if (!bfs::exists(config.outputPath))
        boost::filesystem::create_directories(config.outputPath);

    DefaultReaderList readers;

    int failedFileCount = 0;

    /*if (config.merge)
        failedFileCount = mergeFiles(config.filenames, config, readers);
    else*/
    {

        BOOST_FOREACH(const string& filename, config.filenames)
        {
            try
            {
                processFile(filename, config, readers);
            }
            catch (exception& e)
            {
                failedFileCount++;
                cout << e.what() << endl;
                cout << "Error processing file " << filename << "\n\n"; 
            }
        }
    }

    return failedFileCount;
}


int main(int argc, const char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);        
        return go(config);
    }
    catch (usage_exception& e)
    {
        cerr << e.what() << endl;
        return 0;
    }
    catch (user_error& e)
    {
        cerr << e.what() << endl;
        return 1;
    }
    catch (boost::program_options::error& e)
    {
        cerr << "Invalid command-line: " << e.what() << endl;
        return 1;
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "[" << argv[0] << "] Caught unknown exception.\n";
    }

    cerr << "Please report this error to support@proteowizard.org.\n"
         << "Attach the command output and this version information in your report:\n"
         << "\n"
         << "ProteoWizard release: " << pwiz::Version::str() << " (" << pwiz::Version::LastModified() << ")" << endl
         << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    return 1;
}

