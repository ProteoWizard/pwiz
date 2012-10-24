//
// $Id$
//
//
// Original author: Robert Burke <robert.burke@proteowizard.org>
//
// Copyright 2010 Spielberg Family Center for Applied Proteomics
//   University of Southern California, Los Angeles, California  90033
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

/// \file idcat.cpp
/// \brief Contains the code for the idcat executable.

#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/data/identdata/DefaultReaderList.hpp"
#include "pwiz/data/identdata/IdentDataFile.hpp"
#include "pwiz/data/identdata/TextWriter.hpp"
#include "pwiz/data/identdata/Serializer_Text.hpp"
#include "pwiz/data/identdata/Version.hpp"
#include "pwiz/Version.hpp"
#include <boost/program_options.hpp>

using namespace std;
using namespace pwiz::identdata;
using namespace pwiz::util;


// This class is used to send messages to the main function. These are
// not error conditions. 
class notice_exception : public exception
{
    string msg;
public:
    notice_exception(const string& what) throw() : msg(what) {}
    virtual ~notice_exception() throw()  {}
    virtual const char* what() const throw() {return msg.c_str();}
};

struct Config
{
    // Selects the field to sort output by. 
    string sortBy;

    // Select fields to output
    vector<string> fields;

    // Files to use as inputs
    vector<string> filenames;

    // Directory or file to output to.
    string output;

    // If true, over write existing files. error out on existing files
    // otherwise.
    bool force;

    // Give extra information of just remain quiet.
    bool verbose;
    bool very;

    // Help string to give back to the user.
    string usageOptions;
    
    Config()
        : force(false), verbose(false), very(false)
    {
        loadNames();
    }

    void loadNames()
    {
        // Load up the field names and convert into lowercase
        size_t idx=0;
        while(!Serializer_Text::getIdFieldNames()[idx].empty())
        {
            string name = Serializer_Text::getIdFieldNames()[idx++];
            boost::to_lower(name);
            idNames.push_back(name);
        }
    }
    
    vector<string> idNames;
};

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

// Mainly used for debugging. Outputs the contents of a Config object.
ostream& operator<<(ostream& os, const Config& config)
{
    namespace algo = boost::algorithm;

    if (config.verbose)
        os << "verbose\n";

    string fields = algo::join(config.fields, ",");
    string filenames = algo::join(config.filenames, "\n");

    os << "fields: " << fields << "\n"
       << "sort: " << config.sortBy << "\n"
       << "output: " << config.output << "\n"
       << "filenames:\n" << filenames << "\n";
    
    return os;
}

string getFields(const Config& config)
{
    namespace algo = boost::algorithm;

    string fields = "\t";
    fields += algo::join(config.fields, "\n\t");

    return algo::join(config.fields, "\n\t");
}

// Returns the help string for this command.
string usage(const Config& config)
{    
    ostringstream oss;
    

    oss << "Usage: idcat [options] [filemasks]\n"
        << "Dumps the contents of analysis files .\n"
        << "\nOptions:\n"
        << config.usageOptions
        << "\nFields:\n\t"
        << getFields(config)
        << "\n";
    return oss.str();
}

// Parses the command line arguments passed in to this command.
Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;
    namespace algo = boost::algorithm;

    Config config;

    po::options_description od_config("");
    od_config.add_options()
        ("fields", po::value<string>(),
         ": comma separated list of fields to display")
        ("force,f", ": overwrite file if it exists.")
        ("sort,s", po::value<string>(&config.sortBy),
          ": sort by (single) column")
        ("output,o", po::value<string>(&config.output),
         ": output filename or directory (for multiple files).")
        ("verbose,v", ": prints extra information.")
        ("help,h",
            ": print this helpful message.")
        ;

    ostringstream oss;
    oss  << od_config;
    config.usageOptions = oss.str();

    const char* label_args = "files";

    po::options_description od_args;
    od_args.add_options()(label_args, po::value< vector<string> >(), "");

    po::positional_options_description pod_args;
    pod_args.add(label_args, -1);
   
    po::options_description od_parse;
    od_parse.add(od_config).add(od_args);
    
    // parse command line

    po::variables_map vm;
    char** argv_hack = (char**)argv;
    po::store(po::command_line_parser(argc, argv_hack).
              options(od_parse).positional(pod_args).run(), vm);
    po::notify(vm);

    if (vm.count("help"))
        throw notice_exception(usage(config).c_str());

    // Take the fields provided, or setup the defaults.
    if (vm.count("fields"))
    {
        // TODO verify these are ligitimate fields
        split(config.fields, vm["fields"].as<string>(),
              boost::algorithm::is_any_of(","),
              algo::token_compress_on);
    }
    else
    {
        // For now, add 'em all.
        Serializer_Text::IdField last = Serializer_Text::Last;
        for (size_t i=1; i<(size_t)last; i++)
        {
            config.fields.push_back(Serializer_Text::getIdFieldNames()[i]);
        }
        
    }

    if (vm.count("force"))
        config.force = true;

    // Set the verbosity of this run.
    if (vm.count("verbose"))
        config.verbose = true;
    
    if (vm.count(label_args))
        config.filenames = vm[label_args].as< vector<string> >();

    if (!config.filenames.size())
        throw runtime_error(usage(config).c_str());

    return config;
}

string getExtension(const Config& config)
{
    return ".tab";
}

string getOutputFilename(const string& inFilename, const Config& config)
{
    namespace fs = boost::filesystem;

    fs::path input(inFilename);
    fs::path output(config.output);
    string outputFile = config.output;

    if (!outputFile.empty() && fs::exists(output))
    {
        if (fs::is_regular_file(output))
        {
            if (config.filenames.size()==1 && config.force)
                outputFile = output.string();
            else
                throw notice_exception("File in the way of output: "+
                                       output.string());
        }
        else if (fs::is_directory(output))
        {
            fs::path infile(inFilename);
            string outFilename = input.stem();
            outFilename += getExtension(config);
            output /= fs::path(outFilename);
            outFilename = output.string();
        }
        else
            // TODO keep or check & over write symlinks
            throw runtime_error("Unknown file in the way of output: "+
                                config.output);
    }
    else
    {
        fs::create_directories(output.parent_path());
    }

    return outputFile;
}

/// Converts any values in the idcat's Config that have an effect on
/// the Serializer_Text::Config members.
Serializer_Text::Config getSerializerConfig(const Config& config)
{
    Serializer_Text::Config sConfig;

    //
    // Choosing a sort field
    //

    // Find a match w/ available 
    string sortBy = config.sortBy;
    boost::to_lower(sortBy);
    vector<string>::const_iterator s=find(config.idNames.begin(),
                                          config.idNames.end(),
                                          sortBy);

    if (s != config.idNames.end())
        sConfig.sort = (Serializer_Text::IdField)
            (s-config.idNames.begin());
    
    return sConfig;
}

/// Writes an id file to the output file, or to stdout if the
/// outputFile is empty.
void dumpFile(const string& filename, const string& outputFile,
              const Config& config, const ReaderList& readers)
{
    namespace fs=boost::filesystem;
    
    IterationListenerRegistry iterationListenerRegistry;
    const size_t iterationPeriod = 100;
    iterationListenerRegistry.addListener(
        IterationListenerPtr(new UserFeedbackIterationListener),
        iterationPeriod);
    IterationListenerRegistry* pILR = config.verbose ?
        &iterationListenerRegistry : 0; 

    // handle progress updates if requested
    vector<IdentDataPtr> iddList;
    Reader::Config readerConfig;
    readers.read(filename, iddList, readerConfig);

    // Open up the output file or set cout as the output stream.
    ostream *os;
    if (outputFile.empty())
        os = &cout;
    else
    {
        os = new ofstream(outputFile.c_str());
        (*os) << outputFile << "\n";
    }

    // Verify that the output is valid.
    if (os->bad())
    {
        ostringstream oss("[dumpFile] Bad file: ");
        oss << outputFile.c_str();
        throw runtime_error(oss.str().c_str());
    }

    for (size_t i=0; i < iddList.size(); ++i)
    {
        IdentData& idd = *iddList[i];
        try
        {
            Serializer_Text writer(getSerializerConfig(config));
            writer.write(*os, idd, pILR);
        }
        catch (exception& e)
        {
            cerr << "Error writing analysis " << (i+1)
                 << " in " << fs::path(filename).leaf() << ":\n"
                 << e.what() << endl;
        }
    }

    if (os != &cout)
        delete os;
    
    if (config.verbose)
        cout << endl;
}

int go(const Config& config)
{
    if (config.very)
        cout << config << endl;
    
    DefaultReaderList readers;

    int failedFileCount = 0;

    BOOST_FOREACH(const string& filename, config.filenames)
    {
        string outputFile = getOutputFilename(filename, config);
        
        try
        {
            if (config.very)
                cout << "writing " << filename << " to "
                     << outputFile << "\n";
            
            dumpFile(filename, outputFile, config, readers);
        }
        catch (exception& e)
        {
            failedFileCount++;
            if (config.verbose)
            {
                cout << e.what() << endl;
                cout << "Error processing file " << filename << "\n\n";
            }
        }
    }
    
    return 0;
}

int main(int argc, const char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);
        return go(config);
    }
    catch(notice_exception& e)
    {
        cout << e.what() << endl;
        return 0;
    }
    catch(exception& e)
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
         << "ProteoWizard IdentData: " << pwiz::identdata::Version::str() << " (" << pwiz::identdata::Version::LastModified() << ")" << endl
         << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    return 1;
}
