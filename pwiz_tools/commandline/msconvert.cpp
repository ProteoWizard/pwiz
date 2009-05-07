//
// msconvert.cpp
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "pwiz_tools/common/FullReaderList.hpp"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/IO.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/Version.hpp"
#include "boost/program_options.hpp"
#include "boost/foreach.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/random_access_compressed_ifstream.hpp"
#include <iostream>
#include <fstream>
#include <iterator>


using namespace std;
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;
using boost::shared_ptr;


struct Config
{
    vector<string> filenames;
    vector<string> filters;
    string outputPath;
    string extension;
    bool verbose;
    MSDataFile::WriteConfig writeConfig;
    string contactFilename;

    Config()
    :   outputPath("."), verbose(false)
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
    os << "contactFilename: " << config.contactFilename << endl;
    os << endl;

    os << "filters:\n  ";
    copy(config.filters.begin(), config.filters.end(), ostream_iterator<string>(os,"\n  "));
    os << endl;

    os << "filenames:\n  ";
    copy(config.filenames.begin(), config.filenames.end(), ostream_iterator<string>(os,"\n  "));
    os << endl;

    return os;
}


Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: msconvert [options] [filemasks]\n"
          << "Convert mass spec data file formats.\n"
#ifndef _MSC_VER
          << "\n"
          << "Note: the use of mass spec vendor DLLs is not enabled in this \n"
          << "(non-MSVC) build, this means no Thermo, Bruker, Waters etc input.\n"
#endif
          << "\n"
          << "Return value: # of failed files.\n"
          << "\n";
        
    Config config;
    string filelistFilename;
    string configFilename;

    bool format_text = false;
    bool format_mzML = false;
    bool format_mzXML = false;
    bool format_MGF = false;
    bool precision_32 = false;
    bool precision_64 = false;
    bool mz_precision_32 = false;
    bool mz_precision_64 = false;
    bool intensity_precision_32 = false;
    bool intensity_precision_64 = false;
    bool noindex = false;
    bool zlib = false;
    bool gzip = false;

    po::options_description od_config("Options");
    od_config.add_options()
        ("filelist,f",
            po::value<string>(&filelistFilename),
            ": specify text file containing filenames")
        ("outdir,o",
            po::value<string>(&config.outputPath)->default_value(config.outputPath),
            ": set output directory [.]")
        ("config,c", 
            po::value<string>(&configFilename),
            ": configuration file (optionName=value)")
        ("ext,e",
            po::value<string>(&config.extension)->default_value(config.extension),
			": set extension for output files [mzML|mzXML|mgf|txt]")
        ("mzML",
            po::value<bool>(&format_mzML)->zero_tokens(),
			": write mzML format [default]")
        ("mzXML",
            po::value<bool>(&format_mzXML)->zero_tokens(),
			": write mzXML format")
        ("mgf",
            po::value<bool>(&format_MGF)->zero_tokens(),
			": write Mascot generic format")
        ("text",
            po::value<bool>(&format_text)->zero_tokens(),
			": write ProteoWizard internal text format")
        ("verbose,v",
            po::value<bool>(&config.verbose)->zero_tokens(),
            ": display detailed progress information")
        ("64",
            po::value<bool>(&precision_64)->zero_tokens(),
			": set default binary encoding to 64-bit precision [default]")
        ("32",
            po::value<bool>(&precision_32)->zero_tokens(),
			": set default binary encoding to 32-bit precision")
        ("mz64",
            po::value<bool>(&mz_precision_64)->zero_tokens(),
			": encode m/z values in 64-bit precision [default]")
        ("mz32",
            po::value<bool>(&mz_precision_32)->zero_tokens(),
			": encode m/z values in 32-bit precision")
        ("inten64",
            po::value<bool>(&intensity_precision_64)->zero_tokens(),
			": encode intensity values in 64-bit precision")
        ("inten32",
            po::value<bool>(&intensity_precision_32)->zero_tokens(),
			": encode intensity values in 32-bit precision [default]")
        ("noindex",
            po::value<bool>(&noindex)->zero_tokens(),
			": do not write index")
        ("contactInfo,i",
            po::value<string>(&config.contactFilename),
			": filename for contact info")
        ("zlib,z",
            po::value<bool>(&zlib)->zero_tokens(),
			": use zlib compression for binary data")
        ("gzip,g",
            po::value<bool>(&gzip)->zero_tokens(),
			": gzip entire output file (adds .gz to filename)")
        ("filter",
            po::value< vector<string> >(&config.filters),
			(": add a spectrum list filter\n" + SpectrumListFactory::usage()).c_str())
        ;

    // append options description to usage string

    usage << od_config;

    // extra usage

    usage << "Examples:\n"
          << endl
          << "# convert data.RAW to data.mzML\n"
          << "msconvert data.RAW\n"
          << endl
          << "# convert data.RAW to data.mzXML\n"
          << "msconvert data.RAW --mzXML\n"
          << endl
          << "# put output file in my_output_dir\n"
          << "msconvert data.RAW -o my_output_dir\n"
          << endl
          << "# extract scan indices 5...10 and 20...25\n"
          << "msconvert data.RAW --filter \"index [5,10] [20,25]\"\n"
          << endl
          << "# multiple filters: select scan numbers and recalculate precursors\n"
          << "msconvert data.RAW --filter \"scanNumber [500,1000]\" --filter \"precursorRecalculation\"\n"
          << endl
          << "# use a configuration file\n"
          << "msconvert data.RAW -c config.txt\n"
          << endl
          << "# example configuration file\n"
          << "mzXML=true\n"
          << "zlib=true\n"
          << "filter=\"index [3,7]\"\n"
          << "filter=\"precursorRecalculation\"\n"
          << endl
          << endl

          << "Questions, comments, and bug reports:\n"
          << "http://proteowizard.sourceforge.net\n"
          << "support@proteowizard.org\n"
          << "\n"
          << "ProteoWizard release: " << pwiz::Version::str() << endl
          << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    if (argc <= 1)
        throw runtime_error(usage.str().c_str());

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
        {
            expand_pathmask(bfs::path(filename), globbedFilenames);
        }

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
        throw runtime_error("[msconvert] No files specified.");

    int count = format_text + format_mzML + format_mzXML + format_MGF;
    if (count > 1) throw runtime_error("[msconvert] Multiple format flags specified.");
    if (format_text) config.writeConfig.format = MSDataFile::Format_Text;
    if (format_mzML) config.writeConfig.format = MSDataFile::Format_mzML;
    if (format_mzXML) config.writeConfig.format = MSDataFile::Format_mzXML;
    if (format_MGF) config.writeConfig.format = MSDataFile::Format_MGF;

    config.writeConfig.gzipped = gzip; // if true, file is written as .gz

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
            case MSDataFile::Format_MGF:
                config.extension = ".mgf";
                break;
            default:
                throw runtime_error("[msconvert] Unsupported format."); 
        }
		if (config.writeConfig.gzipped) 
		{
			config.extension += ".gz";
		}
    }

    // precision defaults

    config.writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;
    config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_64;
    config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;

    // handle precision flags

    if (precision_32 && precision_64 ||
        mz_precision_32 && mz_precision_64 ||
        intensity_precision_32 && intensity_precision_64)
        throw runtime_error("[msconvert] Incompatible precision flags.");

    if (precision_32)
    {
        config.writeConfig.binaryDataEncoderConfig.precision
            = config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array]
            = config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] 
            = BinaryDataEncoder::Precision_32;
    }
    else if (precision_64)
    {
        config.writeConfig.binaryDataEncoderConfig.precision
            = config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array]
            = config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] 
            = BinaryDataEncoder::Precision_64;
    }

    if (mz_precision_32)
        config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_32;
    if (mz_precision_64)
        config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_64;
    if (intensity_precision_32)
        config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;
    if (intensity_precision_64)
        config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_64;

    // other flags

    if (noindex)
        config.writeConfig.indexed = false;

    if (zlib)
        config.writeConfig.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;

    return config;
}


void addContactInfo(MSData& msd, const string& contactFilename)
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
}


class UserFeedbackIterationListener : public IterationListener
{
    public:

    virtual Status update(const UpdateMessage& updateMessage)
    {
        // add tabs to erase all of the previous line
        cout << updateMessage.iterationIndex+1 << "/" << updateMessage.iterationCount << "\t\t\t\r" << flush;

        // spectrum and chromatogram lists both iterate; put them on different lines
        if (updateMessage.iterationIndex+1 == updateMessage.iterationCount)
            cout << endl;
        return Status_Ok;
    }
};


void processFile(const string& filename, const Config& config, const ReaderList& readers)
{
    // read in data file

    cout << "processing file: " << filename << endl;

    string head;
    if (!bfs::is_directory(filename))
    {
        pwiz::util::random_access_compressed_ifstream is(filename.c_str());
        if (!is)
            throw runtime_error(("[processFile()] Unable to open file " + filename).c_str());

        head.resize(512, '\0');
        is.read(&head[0], (std::streamsize)head.size());
    }

    vector<MSDataPtr> msdList;
    readers.read(filename, head, msdList);

    for (size_t i=0; i < msdList.size(); ++i)
    {
        MSData& msd = *msdList[i];

        // calculate SHA1 checksum
        if (!msd.fileDescription.sourceFilePtrs.empty())
            calculateSourceFileSHA1(*msd.fileDescription.sourceFilePtrs.back());

        // process the data 

        if (!config.contactFilename.empty())
            addContactInfo(msd, config.contactFilename);

        SpectrumListFactory::wrap(msd, config.filters);

        // handle progress updates if requested

        IterationListenerRegistry iterationListenerRegistry;
        UserFeedbackIterationListener feedback;
        // update on the first spectrum, the last spectrum, the 100th spectrum, the 200th spectrum, etc.
        const size_t iterationPeriod = 100;
        iterationListenerRegistry.addListener(feedback, iterationPeriod);
        IterationListenerRegistry* pILR = config.verbose ? &iterationListenerRegistry : 0; 
     
        // write out the new data file

        string outputFilename = config.outputFilename(filename);
        if (msdList.size() > 1)
            outputFilename = bfs::change_extension(outputFilename, "-" + msd.run.id + config.extension).string();
        cout << "writing output file: " << outputFilename << endl;
        MSDataFile::write(msd, outputFilename, config.writeConfig, pILR);
    }
    cout << endl;
}


int go(const Config& config)
{
    cout << config;

    boost::filesystem::create_directories(config.outputPath);
//cin.get();
    FullReaderList readers;

    int failedFileCount = 0;

    for (vector<string>::const_iterator it=config.filenames.begin(); 
         it!=config.filenames.end(); ++it)
    {
        try
        {
            processFile(*it, config, readers);
        }
        catch (exception& e)
        {
            failedFileCount++;
            cout << e.what() << endl;
            cout << "Error processing file " << *it << "\n\n"; 
        }
    }
//cin.get();
    return failedFileCount;
}


int main(int argc, const char* argv[])
{
    try
    {
        Config config = parseCommandLine(argc, argv);        
        return go(config);
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "[" << argv[0] << "] Caught unknown exception.\n";
    }

    return 1;
}

