//
// $Id: mzmlbcat.cpp
//
//
// Original authors: Andrew Dowsey <andrew.dowsey@bristol.ac.uk>
//
// Copyright 2017 biospi Laboratory,
//                University of Bristol, UK
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


#include "pwiz/Version.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/analysis/Version.hpp"
#include "boost/program_options.hpp"
#include "boost/format.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/filesystem/detail/utf8_codecvt_facet.hpp>
#ifndef WITHOUT_MZMLB
#include "pwiz/data/msdata/mzmlb/Connection_mzMLb.hpp"
#endif


using namespace pwiz::util;
using namespace pwiz::msdata::mzmlb;


/// Holds the results of the parseCommandLine function. 
struct Config
{
    vector<string> filenames;
    bool metadata;
    int spectrum;
    int chromatogram;

    Config()
        : metadata(false), spectrum(-1), chromatogram(-1)
    {
    }
};


/// Parses command line arguments to setup execution parameters, and
/// contructs help text. Inputs are the arguments to main. A filled
/// Config object is returned.
Config parseCommandLine(int argc, char** argv)
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: mzmlbcat [options] [filemasks]\n"
          << "Cat mzML contained within mzMLb file(s).\n"
          << "\n"
          << "Return value: # of failed files.\n"
          << "\n";
        
    Config config;
    string filelistFilename;
    string configFilename;

    bool detailedHelp = false;
    bool metadata = false;
    int spectrum = -1;
    int chromatogram = -1;

    pair<int, int> consoleBounds = get_console_bounds(); // get platform-specific console bounds, or default values if an error occurs

    po::options_description od_config("Options", consoleBounds.first);
    od_config.add_options()
        ("metadata",
            po::value<bool>(&metadata)->zero_tokens(),
            ": output only the metadata (no spectra or chromatograms)")
        ("spectrum",
            po::value<int>(&spectrum)->default_value(-1),
            ": output a spectrum")
        ("chromatogram",
            po::value<int>(&chromatogram)->default_value(-1),
            ": output a chromatogram")
        ("help",
            po::value<bool>(&detailedHelp)->zero_tokens(),
            ": show this message")
        ;

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

    // append options description to usage string

    usage << od_config
          << endl
          << "Examples:\n"
          << endl
          << "# output metadata in data.mzMLb\n"
          << "mzmlbcat --metadata data.mzMLb\n"
          << endl
          << endl

          << "Questions, comments, and bug reports:\n"
          << "http://www.biospi.org\n"
          << "andrew.dowsey@bristol.ac.uk\n"
          << "\n"
          << "ProteoWizard release: " << pwiz::Version::str() << " (" << pwiz::Version::LastModified() << ")" << endl
          << "ProteoWizard MSData: " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")" << endl
          << "ProteoWizard Analysis: " << pwiz::analysis::Version::str() << " (" << pwiz::analysis::Version::LastModified() << ")" << endl
          << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    if ((argc <= 1) || detailedHelp)
        throw usage_exception(usage.str().c_str());

    // remember filenames from command line

    if (vm.count(label_args))
    {
        config.filenames = vm[label_args].as< vector<string> >();

        // expand the filenames by globbing to handle wildcards
        vector<bfs::path> globbedFilenames;
        BOOST_FOREACH(const string& filename, config.filenames)
            if (expand_pathmask(bfs::path(filename), globbedFilenames) == 0)
                cout <<  "[msconvert] no files found matching \"" << filename << "\"" << endl;

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
    config.metadata = metadata;
    config.spectrum = spectrum;
    config.chromatogram = chromatogram;

    return config;
}


int go(const Config& config)
{
    using boost::iostreams::stream_offset;
    using std::streamsize;
    int failedFileCount = 0;

    for (vector<string>::const_iterator it = config.filenames.begin();
        it != config.filenames.end(); ++it)
    {
        try
        {
#ifndef WITHOUT_MZMLB
            boost::iostreams::stream<Connection_mzMLb>* is = new boost::iostreams::stream<Connection_mzMLb>(Connection_mzMLb(*it));
            if (!is->get() || !*is)
                throw runtime_error(("[go::read] Unable to open file " + *it).c_str());
            is->unget();

            char buf[4097];
            if (config.metadata)
            {
                std::vector<long long> spectrumPositions((*is)->size("mzML_spectrumIndex"));
                (*is)->read("mzML_spectrumIndex", &spectrumPositions[0], spectrumPositions.size());
                std::vector<long long> chromatogramPositions((*is)->size("mzML_chromatogramIndex"));
                (*is)->read("mzML_chromatogramIndex", &chromatogramPositions[0], chromatogramPositions.size());

                {   // before spectra
                    stream_offset n = spectrumPositions.front();
                    while (n > 0)
                    {
                        is->read(buf, n < 4096 ? n : 4096);
                        n -= is->gcount();
                        buf[is->gcount()] = 0;
                        std::cout << buf;
                    }
                }
                {   // between spectra and chromatograms
                    is->seekg(spectrumPositions.back(), std::ios_base::beg);
                    stream_offset n = chromatogramPositions.front() - spectrumPositions.back();
                    while (n > 0)
                    {
                        is->read(buf, n < 4096 ? n : 4096);
                        n -= is->gcount();
                        buf[is->gcount()] = 0;
                        std::cout << buf;
                    }
                }
                {   // after chromatograms to end
                    is->seekg(chromatogramPositions.back(), std::ios_base::beg);
                    while (true)
                    {
                        is->read(buf, 4096);
                        if (is->gcount() <= 0) break;
                        buf[is->gcount()] = 0;
                        std::cout << buf;
                    }
                }
            }
            else if (config.spectrum >= 0)
            {   
                std::vector<long long> spectrumPositions((*is)->size("mzML_spectrumIndex"));
                (*is)->read("mzML_spectrumIndex", &spectrumPositions[0], spectrumPositions.size());
                if (config.spectrum >= spectrumPositions.size() - 1) break;

                // output spectrum
                is->seekg(spectrumPositions[config.spectrum], std::ios_base::beg);
                stream_offset n = spectrumPositions[config.spectrum + 1] - spectrumPositions[config.spectrum];
                while (n > 0)
                {
                    is->read(buf, n < 4096 ? n : 4096);
                    n -= is->gcount();
                    buf[is->gcount()] = 0;
                    std::cout << buf;
                }
            }
            else if (config.chromatogram >= 0)
            {
                std::vector<long long> chromatogramPositions((*is)->size("mzML_chromatogramIndex"));
                (*is)->read("mzML_chromatogramIndex", &chromatogramPositions[0], chromatogramPositions.size());
                if (config.chromatogram >= chromatogramPositions.size() - 1) break;

                // output chromatogram
                is->seekg(chromatogramPositions[config.chromatogram], std::ios_base::beg);
                stream_offset n = chromatogramPositions[config.chromatogram + 1] - chromatogramPositions[config.chromatogram];
                while (n > 0)
                {
                    is->read(buf, n < 4096 ? n : 4096);
                    n -= is->gcount();
                    buf[is->gcount()] = 0;
                    std::cout << buf;
                }
            }
            else
            {   // dump whole mzML
                while (true)
                {
                    is->read(buf, 4096);
                    if (is->gcount() <= 0) break;
                    buf[is->gcount()] = 0;
                    std::cout << buf;
                }
            }
            
            delete is;
#endif
        }
        catch (exception& e)
        {
            failedFileCount++;
            cerr << e.what() << endl;
            cerr << "Error processing file " << *it << "\n\n";
        }
    }

    return failedFileCount;
}


int main(int argc, char* argv[])
{
    bnw::args args(argc, argv);

    std::locale global_loc = std::locale();
    std::locale loc(global_loc, new bfs::detail::utf8_codecvt_facet);
    bfs::path::imbue(loc);

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

    cerr << "Please report this error to andrew.dowsey@bristol.ac.uk.\n"
         << "Attach the command output and this version information in your report:\n"
         << "\n"
         << "ProteoWizard release: " << pwiz::Version::str() << " (" << pwiz::Version::LastModified() << ")" << endl
         << "ProteoWizard MSData: " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")" << endl
         << "ProteoWizard Analysis: " << pwiz::analysis::Version::str() << " (" << pwiz::analysis::Version::LastModified() << ")" << endl
         << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    return 1;
}

