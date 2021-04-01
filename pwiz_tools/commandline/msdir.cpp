//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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
#include "pwiz/Version.hpp"
#include "boost/program_options.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/DateTime.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include "pwiz/data/msdata/TextWriter.hpp"
#include <cstdio>


using namespace pwiz::data;
using namespace pwiz::msdata;
using namespace pwiz::util;


enum VerbosityLevel
{
    VerbosityLevel_Brief,
    VerbosityLevel_Detailed,
    VerbosityLevel_Full
};


struct Config
{
    Config() : verbosityLevel(VerbosityLevel_Brief)
    {}

    vector<bfs::path> paths;
    VerbosityLevel verbosityLevel;
};



Config parseCommandLine(int argc, const char* argv[])
{
    namespace po = boost::program_options;

    ostringstream usage;
    usage << "Usage: msdir [options] [file or directory paths]\n"
          << "Displays summary information about mass spectrometry data sources.\n"
          << "Sources can be files: mzML, Thermo RAW, MGF, etc.\n"
          << "Or they can be directories: Waters RAW, Bruker FID/YEP/BAF, etc.\n"
          << "If no files or directories are specified, the current directory is used.\n"
#ifndef _MSC_VER
          << "\n"
          << "Note: the use of vendor DLLs is not enabled in this \n"
          << "(non-MSVC) build, this means no Thermo, Bruker, or Waters input.\n"
#endif
          << "\n";
        
    Config config;

    bool help = false;
    bool brief = false;
    bool detailed = false;
    bool full = false;

    po::options_description od_config("Options");
    od_config.add_options()
        ("help,h",
            po::value<bool>(&help)->zero_tokens(),
            ": show this usage screen")
        ("brief,b",
            po::value<bool>(&brief)->zero_tokens(),
			": display brief listing [default]")
        ("detailed,d",
            po::value<bool>(&detailed)->zero_tokens(),
			": display detailed listing (tabular)")
        ("full,f",
            po::value<bool>(&full)->zero_tokens(),
			": display all source-level metadata")
        ;

    // append options description to usage string
    usage << od_config;

#ifdef WIN32
    string exampleRoot1 = "c:\\data\\";
    string exampleRoot2 = "d:\\other\\data";
    string exampleRoot3 = "\\\\server\\share\\data";
#else
    string exampleRoot1 = "/data/";
    string exampleRoot2 = "/other/data";
    string exampleRoot3 = "/more/data";
#endif

    // example usage
    usage << "Examples:\n"
          << endl
          << "# show a brief listing of identifiable MS sources in " << exampleRoot1 << endl
          << "msdir " << exampleRoot1 << endl
          << endl
          << "# show a detailed listing of identifiable MS sources in " << exampleRoot2 << endl
          << "msdir -d " << exampleRoot2 << endl
          << endl
          << "# show all metadata for identifiable MS sources in " << exampleRoot2 << endl
          << "msdir " << exampleRoot3 << endl
          << endl
          << endl

          << "Questions, comments, and bug reports:\n"
          << "https://github.com/ProteoWizard\n"
          << "support@proteowizard.org\n"
          << "\n"
          << "ProteoWizard release: " << pwiz::Version::str() << endl
          << "Build date: " << __DATE__ << " " << __TIME__ << endl;

    if (argc <= 1)
        config.paths.push_back(bfs::current_path().string());

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

    if (detailed)
        config.verbosityLevel = VerbosityLevel_Detailed;

    if (full)
        config.verbosityLevel = VerbosityLevel_Full;

    if (help)
    {
        cout << usage.str() << endl;
        return config; // ignore any other arguments
    }

    // expand pathmasks (files or directories) from command line
    if (vm.count(label_args))
    {
        vector<string> paths = vm[label_args].as< vector<string> >();

        vector<bfs::path> expandedPaths;
        BOOST_FOREACH(const string& pathstring, paths)
        {
            bfs::path path(pathstring);
            const static bfs::directory_iterator end_itr;

            try
            {
                if (bfs::exists(path))
                {
                    // if given a directory name, process all its children
                    if (bfs::is_directory(path))
                        for (bfs::directory_iterator itr(path); itr != end_itr; ++itr)
                            config.paths.push_back(*itr);

                    // if given a file name, process it
                    else if (bfs::is_regular_file(path))
                        config.paths.push_back(path);
                }
                // might be a pathmask, try to expand it and process the result
                else
                    expand_pathmask(path, expandedPaths);
            }
            catch(bfs::filesystem_error& e)
            {
                if (e.code() == boost::system::errc::permission_denied)
                    continue; // skip it

                string error = "[processPath()] Error identifying path \"" + path.string() + "\": " + e.what();
                throw runtime_error(error);
            }
        }
        config.paths.insert(config.paths.end(), expandedPaths.begin(), expandedPaths.end());
    }

    return config;
}


boost::uintmax_t calculatePathSize(const bfs::path& path)
{
    const static bfs::directory_iterator end_itr;

    // for a filepath, return the size directly
    if (bfs::is_regular_file(path))
        return bfs::file_size(path);

    // for a directory, return the size recursively?
    if (bfs::is_directory(path))
    {
        boost::uintmax_t dirSize = 0;
        for (bfs::directory_iterator itr(path); itr != end_itr; ++itr)
            dirSize += calculatePathSize(*itr);
        return dirSize;
    }

    return 0;
}


string getStringFromTimeT(time_t time)
{
    boost::local_time::local_date_time ldt(from_time_t(time), boost::local_time::time_zone_ptr());
    boost::local_time::local_time_facet* output_facet = new boost::local_time::local_time_facet;
    output_facet->format("%Y-%m-%d %H:%M");
    stringstream ss;
    ss.imbue(locale(locale::classic(), output_facet));
    ss << ldt;
    return ss.str();
}


bool processPath(const bfs::path& path, const Config& config, const ReaderList& readers, int longestPath)
{
    string head;
    if (bfs::is_regular_file(path))
    {
        //pwiz::util::random_access_compressed_ifstream is(path.string().c_str());
        //if (!is)
        //    throw runtime_error(("[processPath()] Unable to open file \"" + path.string() + "\"").c_str());

        head.resize(512, '\0');
        FILE* file = fopen(path.string().c_str(), "r");
        fread(&head[0], 512, 1, file);
        fclose(file);

        //is.read(&head[0], (std::streamsize)head.size());
    }

    //for (ReaderList::const_iterator itr = readers.begin(); itr != readers.end(); ++itr)
    //    cout << (*itr)->getType() << endl;

    string pathType = readers.identify(path.string(), head);
    if (pathType == "Mascot Generic")
        pathType = "MGF";

    if (pathType.empty()) // unidentifiable path
        return false;

    boost::uintmax_t pathSize = calculatePathSize(path);
    string pathLastModified = getStringFromTimeT(bfs::last_write_time(path));

    Reader::Config readerConfig;
    readerConfig.acceptZeroLengthSpectra = true;
    readerConfig.ignoreZeroIntensityPoints = true;

    switch (config.verbosityLevel)
    {
        case VerbosityLevel_Brief:
            cout << (format("\n%|1$-14| %|15t| %|2$+8| %|24t| %3% %|42t| %4%")
                    % pathType
                    % abbreviate_byte_size(pathSize)
                    % pathLastModified
                    % path.leaf()
                    ).str() << flush;
            break;

        case VerbosityLevel_Detailed:
            try
            {
                vector<MSDataPtr> msdList;
                readers.read(path.string(), head, msdList, readerConfig);
                for (const auto& msd : msdList)
                {
                    const auto& source = *msd;
                    size_t spectraCount = source.run.spectrumListPtr->size();
                    cout << (format("\n%|1$-14| %|15t| %|2$+8| %|24t| %3% %|42t| %|4$+6| %|50t| %|" + lexical_cast<string>(longestPath+3) + "t| %6%")
                        % pathType
                        % abbreviate_byte_size(pathSize)
                        % pathLastModified
                        % spectraCount
                        % path.leaf()
                        % source.run.id
                        ).str() << flush;
                }
            }
            catch(exception& e)
            {
                cout << (format("\n%|1$-14| %|15t| %|2$+8| %|24t| %3% %|42t| %|4$+6| %|50t| %|" + lexical_cast<string>(longestPath + 3) + "t| %6%")
                        % pathType
                        % abbreviate_byte_size(pathSize)
                        % pathLastModified
                        % "error"
                        % path.leaf()
                        % "error"
                        ).str() << endl << e.what() << endl; 
            }
            break;

        case VerbosityLevel_Full:
            {
                vector<MSDataPtr> msdList;
                readers.read(path.string(), head, msdList, readerConfig);
                for (const auto& msd : msdList)
                {
                    const auto& source = *msd;
                    // display all source-level metadata
                    cout << "Source-level metadata for \"" << path.string() << " (" << source.run.id << ")\"" << endl;
                    TextWriter(cout, 0)(source, true);
                    cout << endl << endl;
                }
            }
            break;
    }

    return true;
}


void go(const Config& config)
{
    FullReaderList readers;

    switch (config.verbosityLevel)
    {
        case VerbosityLevel_Brief:
            cout << (format("%|1$=14| %|15t| %|2$=8| %|24t| %|3$=16| %|42t| %4%")
                    % "Type" % "Size" % "Last Modified" % "Name").str();
            break;
        case VerbosityLevel_Detailed:
            cout << (format("%|1$=14| %|15t| %|2$=8| %|24t| %|3$=16| %|42t| %|4$=6| %|50t| %5%")
                    % "Type" % "Size" % "Last Modified" % "Spectra" % "Name").str();
            break;
        case VerbosityLevel_Full:
            // no global header
            break;
    }

    int longestPath = 0;
    BOOST_FOREACH(const bfs::path& path, config.paths)
        if (path.size() > longestPath)
            longestPath = path.size();

    size_t sourcesFound = 0;
    BOOST_FOREACH(const bfs::path& path, config.paths)
    {
        try
        {
            if (processPath(path, config, readers, longestPath))
                ++sourcesFound;
        }
        catch(bfs::filesystem_error& e)
        {
            if (e.code() == boost::system::errc::permission_denied)
                continue; // skip it

            string error = "\n[processPath()] Error identifying path \"" + path.string() + "\": " + e.what();
            throw runtime_error(error);
        }

    }

    if (sourcesFound == 0) // overwrite header row with message
        cout << "\rNo MS sources found.                                           " << endl;
    else
    {
        cout << "\nFound " << sourcesFound << " source";
        if (sourcesFound > 1) cout << "s";
        cout << "." << endl;
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
        cerr << "[" << argv[0] << "] Caught unknown exception.\n";
    }

    return 1;
}

