//
// msconvert.cpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#include "pwiz/msdata/MSDataFile.hpp"
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
#include <iostream>
#include <fstream>
#include <iterator>


using namespace std;
using namespace pwiz::msdata;


struct Config
{
    vector<string> filenames;
    string outputPath;
    string extension;
    MSDataFile::WriteConfig writeConfig;

    Config()
    :   outputPath(".")
    {}

    string outputFilename(const string& inputFilename) const;
};


string Config::outputFilename(const string& filename) const
{
    namespace bfs = boost::filesystem;
    bfs::path newFilename = bfs::basename(filename) + extension;
    bfs::path fullPath = bfs::path(outputPath) / newFilename;
    return fullPath.string(); 
}


ostream& operator<<(ostream& os, const Config& config)
{
    os << "format: " << config.writeConfig << endl;
    os << "outputPath: " << config.outputPath << endl;
    os << "extension: " << config.extension << endl; 
    os << "filenames:\n  ";
    copy(config.filenames.begin(), config.filenames.end(), ostream_iterator<string>(os,"\n  "));
    os << endl;
    return os;
}


Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: msconvert [options] [filenames]\n"
          << "Convert mass spec data file formats.\n\n";

    Config config;
    string filelistFilename;

    bool format_text = false;
    bool format_mzML = false;
    bool format_mzXML = false;
    bool precision_32 = false;
    bool precision_64 = false;
    bool noindex = false;

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
        ("mzML",
            po::value<bool>(&format_mzML)->zero_tokens(),
			": write mzML format (default)")
        ("mzXML",
            po::value<bool>(&format_mzXML)->zero_tokens(),
			": write mzXML format")
        ("text",
            po::value<bool>(&format_text)->zero_tokens(),
			": write MSData text format")
        ("64",
            po::value<bool>(&precision_64)->zero_tokens(),
			": write binary data with 64-bit precision (default)")
        ("32",
            po::value<bool>(&precision_32)->zero_tokens(),
			": write binary data with 32-bit precision")
        ("noindex",
            po::value<bool>(&noindex)->zero_tokens(),
			": do not write index")
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

    usage << endl
          << "Spielberg Family Center for Applied Proteomics\n"
          << "Cedars-Sinai Medical Center, Los Angeles, California\n"
          << "http://sfcap.cshs.org\n";

    if (config.filenames.empty())
        throw runtime_error(usage.str());

    int count = format_text + format_mzML + format_mzXML;
    if (count > 1) throw runtime_error(usage.str());
    if (format_text) config.writeConfig.format = MSDataFile::Format_Text;
    if (format_mzML) config.writeConfig.format = MSDataFile::Format_mzML;
    if (format_mzXML) config.writeConfig.format = MSDataFile::Format_mzXML;

    if (config.extension.empty())
    {
        switch (config.writeConfig.format)
        {
            case MSDataFile::Format_Text:
                config.extension = ".txt";
                break;
            case MSDataFile::Format_mzML:
                config.extension = ".mzML";
                break;
            case MSDataFile::Format_mzXML:
                config.extension = ".mzXML";
                break;
            default:
                throw runtime_error("[msconvert] Unsupported format."); 
        }
    }

    count = precision_32 + precision_64;
    if (count > 1) throw runtime_error(usage.str());
    if (precision_32)
        config.writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_32;
    if (precision_64)
        config.writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;

    if (noindex)
        config.writeConfig.indexed = false;

    return config;
}


void processFile(const string& filename, const Config& config)
{
    cout << "processing file: " << filename << endl;
    MSDataFile msd(filename);

    string outputFilename = config.outputFilename(filename);
    cout << "writing output file: " << outputFilename << endl;
    msd.write(outputFilename, config.writeConfig);

    cout << endl;
}


void go(const Config& config)
{
    cout << config;

    boost::filesystem::create_directories(config.outputPath);

    for (vector<string>::const_iterator it=config.filenames.begin(); 
         it!=config.filenames.end(); ++it)
    {
        try
        {
            processFile(*it, config);
        }
        catch (exception& e)
        {
            cout << e.what() << endl;
            cout << "Error processing file " << *it << "\n\n"; 
        }
    }
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
        cerr << "[msdiff] Caught unknown exception.\n";
    }

    return 1;
}

