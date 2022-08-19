//
// $Id$
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
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
#include "pwiz/data/msdata/MSDataMerger.hpp"
#include "pwiz/data/msdata/IO.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "pwiz/data/msdata/SpectrumListWrapper.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumListFactory.hpp"
#include "pwiz/analysis/chromatogram_processing/ChromatogramListFactory.hpp"
#include "pwiz/Version.hpp"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/analysis/Version.hpp"
#include "boost/program_options.hpp"
#include "boost/format.hpp"
#include "pwiz/utility/misc/Filesystem.hpp"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/filesystem/detail/utf8_codecvt_facet.hpp>

using namespace pwiz::cv;
using namespace pwiz::data;
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;

ostream* os_ = &cout;


/// Holds the results of the parseCommandLine function. 
struct Config : public Reader::Config
{
    vector<string> filenames;
    vector<string> filters;
    vector<string> chromatogramFilters;
    string outputPath;
    string extension;
    string outputFile;
    bool verbose;
    MSDataFile::WriteConfig writeConfig;
    string contactFilename;
    bool merge;
    IntegerSet runIndexSet;
    bool stripLocationFromSourceFiles;
    bool stripVersionFromSoftware;
    boost::tribool singleThreaded;

    Config()
        : outputPath("."), verbose(false), merge(false)
    {
        simAsSpectra = false;
        srmAsSpectra = false;
        combineIonMobilitySpectra = false;
        reportSonarBins = false;
        unknownInstrumentIsError = true;
        stripLocationFromSourceFiles = false;
        stripVersionFromSoftware = false;
    }

    string outputFilename(const string& inputFilename, const MSData& inputMSData) const;
};


string Config::outputFilename(const string& filename, const MSData& msd) const
{
    string runId = msd.run.id;

    // if necessary, adjust runId so it makes a suitable filename
    if (!outputFile.empty())
    {
        runId = outputFile;
    }
    if (runId.empty())
        runId = bfs::basename(filename);
    else
    {
        string extension = bal::to_lower_copy(bfs::extension(runId));
        if (extension == ".mzml" ||
            extension == ".mzxml" ||
            extension == ".xml" ||
            extension == ".mgf" ||
            extension == ".ms1" ||
            extension == ".cms1" ||
            extension == ".ms2" ||
            extension == ".cms2" ||
            extension == ".mz5")
            runId = bfs::basename(runId);
    }

    // this list is for Windows; it's a superset of the POSIX list
    string illegalFilename = "\\/*:?<>|\"";
    for(char& c : runId)
        if (illegalFilename.find(c) != string::npos)
            c = '_';

    bfs::path newFilename = runId + extension;
    bfs::path fullPath = bfs::path(outputPath) / newFilename;
    return fullPath.string(); 
}


ostream& operator<<(ostream& os, const Config& config)
{
    os << "format: " << config.writeConfig << endl;
    os << "outputPath: " << config.outputPath << endl;
    os << "extension: " << config.extension << endl; 
    os << "contactFilename: " << config.contactFilename << endl;
    os << "runIndexSet: " << config.runIndexSet << endl;
    os << endl;

    os << "spectrum list filters:\n  ";
    copy(config.filters.begin(), config.filters.end(), ostream_iterator<string>(os,"\n  "));
    os << endl;

    os << "chromatogram list filters:\n  ";
    copy(config.chromatogramFilters.begin(), config.chromatogramFilters.end(), ostream_iterator<string>(os, "\n  "));
    os << endl;

    os << "filenames:\n  ";
    copy(config.filenames.begin(), config.filenames.end(), ostream_iterator<string>(os,"\n  "));
    os << endl;

    return os;
}

static double string_to_double( const std::string& str )
{
	errno = 0;
	const char* stringToConvert = str.c_str();
	const char* endOfConversion = stringToConvert;
	double value = STRTOD( stringToConvert, const_cast<char**>(&endOfConversion) );
	if( value == 0.0 && stringToConvert == endOfConversion ) // error: conversion could not be performed
		throw bad_lexical_cast(); // not a double
	if (*endOfConversion)
		throw bad_lexical_cast(); // started out as a double but became something else - like "10foo.raw"
	return value;
}

/// Parses command line arguments to setup execution parameters, and
/// contructs help text. Inputs are the arguments to main. A filled
/// Config object is returned.
Config parseCommandLine(int argc, char** argv)
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
    bool format_MS1 = false;
    bool format_CMS1 = false;
    bool format_MS2 = false;
    bool format_CMS2 = false;
    bool format_mz5 = false;
    bool precision_32 = false;
    bool precision_64 = false;
    bool mz_precision_32 = false;
    bool mz_precision_64 = false;
    bool intensity_precision_32 = false;
    bool intensity_precision_64 = false;
    bool noindex = false;
    bool zlib = false;
    bool gzip = false;
    bool ms_numpress_all = false; // if true, use this numpress compression with default tolerance
    double ms_numpress_linear = -1; // if >= 0, use this numpress linear compression with this tolerance
	string ms_numpress_linear_str; // input as text, to help with the "msconvert --numpresslinear foo.raw" case
    string ms_numpress_linear_default = (boost::format("%4.2g") % BinaryDataEncoder_default_numpressLinearErrorTolerance).str();
    bool ms_numpress_pic = false; // if true, use this numpress Pic compression
    double ms_numpress_slof = -1; // if >= 0, use this numpress slof compression with this tolerance
    double ms_numpress_linear_abs_tolerance = -1; // if >= 0, use this numpress linear compression with this absolute Th tolerance
	string ms_numpress_linear_abs_tolerance_str; // input as text
	string ms_numpress_linear_abs_tolerance_str_default("-1"); // input as text
	string ms_numpress_slof_str; // input as text, to help with the "msconvert --numpressslof foo.raw" case
    string ms_numpress_slof_default = (boost::format("%4.2g") % BinaryDataEncoder_default_numpressSlofErrorTolerance).str();
    string runIndexSet;
    bool detailedHelp = false;
    string helpForFilter;
    bool showExamples = false;

    pair<int, int> consoleBounds = get_console_bounds(); // get platform-specific console bounds, or default values if an error occurs

    po::options_description od_config("Options", consoleBounds.first);
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
        ("outfile",
            po::value<string>(&config.outputFile)->default_value(config.outputFile),
         ": Override the name of output file.")
        ("ext,e",
            po::value<string>(&config.extension)->default_value(config.extension),
            ": set extension for output files [mzML|mzXML|mgf|txt"
#ifndef WITHOUT_MZ5
            "|mz5"
#endif
            "]")
        ("mzML",
            po::value<bool>(&format_mzML)->zero_tokens(),
            ": write mzML format [default]")
        ("mzXML",
            po::value<bool>(&format_mzXML)->zero_tokens(),
            ": write mzXML format")
#ifndef WITHOUT_MZ5
        ("mz5",
            po::value<bool>(&format_mz5)->zero_tokens(),
            ": write mz5 format")
#endif
        ("mgf",
            po::value<bool>(&format_MGF)->zero_tokens(),
            ": write Mascot generic format")
        ("text",
            po::value<bool>(&format_text)->zero_tokens(),
            ": write ProteoWizard internal text format")
        ("ms1",
            po::value<bool>(&format_MS1)->zero_tokens(),
            ": write MS1 format")
        ("cms1",
            po::value<bool>(&format_CMS1)->zero_tokens(),
            ": write CMS1 format")
        ("ms2",
            po::value<bool>(&format_MS2)->zero_tokens(),
            ": write MS2 format")
        ("cms2",
            po::value<bool>(&format_CMS2)->zero_tokens(),
            ": write CMS2 format")
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
        ("numpressLinear",
            po::value<std::string>(&ms_numpress_linear_str)->implicit_value(ms_numpress_linear_default),
            ": use numpress linear prediction compression for binary mz and rt data (relative accuracy loss will not exceed given tolerance arg, unless set to 0)")
        ("numpressLinearAbsTol",
            po::value<std::string>(&ms_numpress_linear_abs_tolerance_str)->implicit_value(ms_numpress_linear_abs_tolerance_str_default),
            ": desired absolute tolerance for linear numpress prediction (e.g. use 1e-4 for a mass accuracy of 0.2 ppm at 500 m/z, default uses -1.0 for maximal accuracy). Note: setting this value may substantially reduce file size, this overrides relative accuracy tolerance.")
        ("numpressPic",
            po::value<bool>(&ms_numpress_pic)->zero_tokens(),
            ": use numpress positive integer compression for binary intensities (absolute accuracy loss will not exceed 0.5)")
        ("numpressSlof",
            po::value<std::string>(&ms_numpress_slof_str)->implicit_value(ms_numpress_slof_default),
            ": use numpress short logged float compression for binary intensities (relative accuracy loss will not exceed given tolerance arg, unless set to 0)")
        ("numpressAll,n",
            po::value<bool>(&ms_numpress_all)->zero_tokens(),
            ": same as --numpressLinear --numpressSlof (see https://github.com/fickludd/ms-numpress for more info)")
        ("gzip,g",
            po::value<bool>(&gzip)->zero_tokens(),
            ": gzip entire output file (adds .gz to filename)")
        ("filter",
            po::value< vector<string> >(&config.filters),
            ": add a spectrum list filter")
        ("chromatogramFilter",
            po::value< vector<string> >(&config.chromatogramFilters),
            ": add a chromatogram list filter")
        ("merge",
            po::value<bool>(&config.merge)->zero_tokens(),
            ": create a single output file from multiple input files by merging file-level metadata and concatenating spectrum lists")
        ("runIndexSet",
            po::value<string>(&runIndexSet),
            ": for multi-run sources, select only the specified run indices")
        ("simAsSpectra",
            po::value<bool>(&config.simAsSpectra)->zero_tokens(),
            ": write selected ion monitoring as spectra, not chromatograms")
        ("srmAsSpectra",
            po::value<bool>(&config.srmAsSpectra)->zero_tokens(),
            ": write selected reaction monitoring as spectra, not chromatograms")
        ("combineIonMobilitySpectra",
            po::value<bool>(&config.combineIonMobilitySpectra)->zero_tokens(),
            ": write all ion mobility or Waters SONAR bins/scans in a frame/block as one spectrum instead of individual spectra")
        ("acceptZeroLengthSpectra",
            po::value<bool>(&config.acceptZeroLengthSpectra)->zero_tokens(),
            ": some vendor readers have an efficient way of filtering out empty spectra, but it takes more time to open the file")
        ("ignoreMissingZeroSamples",
            po::value<bool>(&config.ignoreZeroIntensityPoints)->zero_tokens()->default_value(config.ignoreZeroIntensityPoints),
            ": some vendor readers do not include zero samples in their profile data; the default behavior is to add the zero samples but this option disables that")
        ("ignoreUnknownInstrumentError",
            po::value<bool>(&config.unknownInstrumentIsError)->zero_tokens()->default_value(!config.unknownInstrumentIsError),
            ": if true, if an instrument cannot be determined from a vendor file, it will not be an error")
        ("stripLocationFromSourceFiles",
            po::value<bool>(&config.stripLocationFromSourceFiles)->zero_tokens(),
            ": if true, sourceFile elements will be stripped of location information, so the same file converted from different locations will produce the same mzML")
        ("stripVersionFromSoftware",
            po::value<bool>(&config.stripVersionFromSoftware)->zero_tokens(),
            ": if true, software elements will be stripped of version information, so the same file converted with different versions will produce the same mzML")
        ("singleThreaded",
            po::value<boost::tribool>(&config.singleThreaded)->implicit_value(true)->default_value(boost::indeterminate),
            ": if true, reading and writing spectra will be done on a single thread")
        ("help",
            po::value<bool>(&detailedHelp)->zero_tokens(),
            ": show this message, with extra detail on filter options")
        ("help-filter",
            po::value<string>(&helpForFilter),
            ": name of a single filter to get detailed help for")
        ("show-examples",
            po::value<bool>(&showExamples)->zero_tokens(),
            ": show examples of how to run msconvert.exe")
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

    // negate unknownInstrumentIsError value since command-line parameter (ignoreUnknownInstrumentError) and the Config parameters use inverse semantics
    config.unknownInstrumentIsError = !config.unknownInstrumentIsError;

    if (!runIndexSet.empty())
        config.runIndexSet.parse(runIndexSet);

    if (!helpForFilter.empty())
    {
        usage << SpectrumListFactory::usage(helpForFilter) << endl;
    }
    else if (showExamples)
    {
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
              << "# combining options to create a smaller mzML file, much like the old ReAdW converter program\n"
              << "msconvert data.RAW --32 --zlib --filter \"peakPicking true 1-\" --filter \"zeroSamples removeExtra\"\n"
              << endl
              << "# extract scan indices 5...10 and 20...25\n"
              << "msconvert data.RAW --filter \"index [5,10] [20,25]\"\n"
              << endl
              << "# extract MS1 scans only\n"
              << "msconvert data.RAW --filter \"msLevel 1\"\n"
              << endl
              << "# extract MS2 and MS3 scans only\n"
              << "msconvert data.RAW --filter \"msLevel 2-3\"\n"
              << endl
              << "# extract MSn scans for n>1\n"
              << "msconvert data.RAW --filter \"msLevel 2-\"\n"
              << endl
              << "# apply ETD precursor mass filter\n"
              << "msconvert data.RAW --filter ETDFilter\n"
              << endl
              << "# remove non-flanking zero value samples\n"
              << "msconvert data.RAW --filter \"zeroSamples removeExtra\"\n"
              << endl
              << "# remove non-flanking zero value samples in MS2 and MS3 only\n"
              << "msconvert data.RAW --filter \"zeroSamples removeExtra 2 3\"\n"
              << endl
              << "# add missing zero value samples (with 5 flanking zeros) in MS2 and MS3 only\n"
              << "msconvert data.RAW --filter \"zeroSamples addMissing=5 2 3\"\n"
              << endl
              << "# keep only HCD spectra from a decision tree data file\n"
              << "msconvert data.RAW --filter \"activation HCD\"\n"
              << endl
              << "# keep the top 42 peaks or samples (depending on whether spectra are centroid or profile):\n"
              << "msconvert data.RAW --filter \"threshold count 42 most-intense\"\n"
              << endl
              << "# multiple filters: select scan numbers and recalculate precursors\n"
              << "msconvert data.RAW --filter \"scanNumber [500,1000]\" --filter \"precursorRecalculation\"\n"
              << endl
              << "# multiple filters: apply peak picking and then keep the bottom 100 peaks:\n"
              << "msconvert data.RAW --filter \"peakPicking true 1-\" --filter \"threshold count 100 least-intense\"\n"
              << endl
              << "# multiple filters: apply peak picking and then keep all peaks that are at least 50% of the intensity of the base peak:\n"
              << "msconvert data.RAW --filter \"peakPicking true 1-\" --filter \"threshold bpi-relative .5 most-intense\"\n"
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
              << endl;
    }
    else
    {
        // append options description to usage string
        usage << od_config;

        // extra usage
        usage << SpectrumListFactory::usage(detailedHelp, "(run this program with --help to see details for all filters)", consoleBounds.first);
        usage << ChromatogramListFactory::usage(detailedHelp, nullptr, consoleBounds.first) << endl;

        usage << "Questions, comments, and bug reports:\n"
              << "https://github.com/ProteoWizard\n"
              << "support@proteowizard.org\n"
              << "\n"
              << "ProteoWizard release: " << pwiz::Version::str() << " (" << pwiz::Version::LastModified() << ")" << endl
              << "Build date: " << __DATE__ << " " << __TIME__ << endl;
    }

    if ((argc <= 1) || detailedHelp || !helpForFilter.empty() || showExamples)
        throw usage_exception(usage.str().c_str());

    // parse config file if required

    if (!configFilename.empty())
    {
        ifstream is(configFilename.c_str());

        if (is)
        {
            *os_ << "Reading configuration file " << configFilename << "\n\n";
            po::store(parse_config_file(is, od_config), vm);
            po::notify(vm);
        }
        else
        {
            *os_ << "Unable to read configuration file " << configFilename << "\n\n";
        }
    }

    // remember filenames from command line

    if (vm.count(label_args))
    {
        config.filenames = vm[label_args].as< vector<string> >();

        // expand the filenames by globbing to handle wildcards
        vector<bfs::path> globbedFilenames;
        for(const string& filename : config.filenames)
        {
            if (isHTTP(filename))
                globbedFilenames.push_back(filename);
            else if (expand_pathmask(bfs::path(filename), globbedFilenames) == 0)
                cout << "[msconvert] no files found matching \"" << filename << "\"" << endl;
        }

        config.filenames.clear();
        for(const bfs::path& filename : globbedFilenames)
            config.filenames.push_back(filename.string());
    }

    // parse filelist if required

    if (!filelistFilename.empty())
    {
        ifstream is(filelistFilename.c_str());
        while (is)
        {
            string filename;
            getlinePortable(is, filename);
            if (is) config.filenames.push_back(filename);
        }
    }

    // check stuff

	if (ms_numpress_slof_str.length()) // was that a numerical arg to --numpressSlof, or a filename?
	{
		try 
		{
			ms_numpress_slof = string_to_double(ms_numpress_slof_str); 
		}
		catch(...) {
			config.filenames.push_back(ms_numpress_slof_str); // actually that was a filename
            ms_numpress_slof = BinaryDataEncoder_default_numpressSlofErrorTolerance;
		}
	}
	if (ms_numpress_linear_abs_tolerance_str.length()) // this argument needs to be numerical
	{
		ms_numpress_linear_abs_tolerance = string_to_double(ms_numpress_linear_abs_tolerance_str);
	}
	if (ms_numpress_linear_str.length()) // was that a numerical arg to --numpressLinear, or a filename?
	{
		try 
		{
			ms_numpress_linear = string_to_double(ms_numpress_linear_str); 
		}
		catch(...) {
			config.filenames.push_back(ms_numpress_linear_str); // actually that was a filename
            ms_numpress_linear = BinaryDataEncoder_default_numpressLinearErrorTolerance;
		}
	}

    if (config.filenames.empty())
        throw user_error("[msconvert] No files specified.");

    int count = format_text + format_mzML + format_mzXML + format_MGF + format_MS2 + format_CMS2 + format_mz5;
    if (count > 1) throw user_error("[msconvert] Multiple format flags specified.");
    if (format_text) config.writeConfig.format = MSDataFile::Format_Text;
    if (format_mzML) config.writeConfig.format = MSDataFile::Format_mzML;
    if (format_mzXML) config.writeConfig.format = MSDataFile::Format_mzXML;
    if (format_MGF) config.writeConfig.format = MSDataFile::Format_MGF;
    if (format_MS1) config.writeConfig.format = MSDataFile::Format_MS1;
    if (format_CMS1) config.writeConfig.format = MSDataFile::Format_CMS1;
    if (format_MS2) config.writeConfig.format = MSDataFile::Format_MS2;
    if (format_CMS2) config.writeConfig.format = MSDataFile::Format_CMS2;
    if (format_mz5) config.writeConfig.format = MSDataFile::Format_MZ5;

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
            case MSDataFile::Format_MS1:
                config.extension = ".ms1";
                break;
            case MSDataFile::Format_CMS1:
                config.extension = ".cms1";
                break;
            case MSDataFile::Format_MS2:
                config.extension = ".ms2";
                break;
            case MSDataFile::Format_CMS2:
                config.extension = ".cms2";
                break;
            case MSDataFile::Format_MZ5:
#ifdef WITHOUT_MZ5
                throw user_error("[msconvert] Not built with mz5 support."); 
#endif
                config.extension = ".mz5";
                break;
            default:
                throw user_error("[msconvert] Unsupported format."); 
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
        throw user_error("[msconvert] Incompatible precision flags.");

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

    if ((ms_numpress_slof>=0) && ms_numpress_pic)
        throw user_error("[msconvert] Incompatible compression flags 'numpressPic' and 'numpressSlof'.");

    if (ms_numpress_all && ms_numpress_pic)
        throw user_error("[msconvert] Incompatible compression flags 'numpressPic' and 'numpressAll'.");

    if (ms_numpress_all) {
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_m_z_array] = BinaryDataEncoder::Numpress_Linear;
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_time_array] = BinaryDataEncoder::Numpress_Linear;
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_intensity_array] = BinaryDataEncoder::Numpress_Slof;
        config.writeConfig.binaryDataEncoderConfig.numpressLinearErrorTolerance = BinaryDataEncoder_default_numpressLinearErrorTolerance;
        config.writeConfig.binaryDataEncoderConfig.numpressSlofErrorTolerance = BinaryDataEncoder_default_numpressSlofErrorTolerance;
    }
    if (ms_numpress_pic) 
    {
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_intensity_array] = BinaryDataEncoder::Numpress_Pic;
    }    
    if (ms_numpress_slof>=0) 
    {
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_intensity_array] = BinaryDataEncoder::Numpress_Slof;
        config.writeConfig.binaryDataEncoderConfig.numpressSlofErrorTolerance = ms_numpress_slof;
    }
    if (ms_numpress_linear>=0) 
    {
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_m_z_array] = BinaryDataEncoder::Numpress_Linear;
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_time_array] = BinaryDataEncoder::Numpress_Linear;
        config.writeConfig.binaryDataEncoderConfig.numpressLinearErrorTolerance = ms_numpress_linear;
    }
    if (ms_numpress_linear_abs_tolerance>=0) 
    {
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_m_z_array] = BinaryDataEncoder::Numpress_Linear;
        config.writeConfig.binaryDataEncoderConfig.numpressOverrides[MS_time_array] = BinaryDataEncoder::Numpress_Linear;
        config.writeConfig.binaryDataEncoderConfig.numpressLinearAbsMassAcc = ms_numpress_linear_abs_tolerance;
        // this overrides any relative guarantees as the user specifically wants an absolute mass error guarantee
        config.writeConfig.binaryDataEncoderConfig.numpressLinearErrorTolerance = 0;
    }

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


void stripSourceFileLocation(MSData& msd)
{
    for (const auto& sourceFilePtr : msd.fileDescription.sourceFilePtrs)
        sourceFilePtr->location = "file:///";
}


void stripSoftwareVersion(MSData& msd)
{
    for (const auto& softwarePtr : msd.softwarePtrs)
        softwarePtr->version = "";
}


class UserFeedbackIterationListener : public IterationListener
{
    std::streamoff longestMessage;
    std::hash<string> hasher;
    size_t lastMessageHash;
    size_t lastIterationIndex;
    size_t lastIterationCount;

    bool updateHashIfNewMessage(const string& newMessage)
    {
        size_t newMessageHash = hasher(newMessage);
        if (newMessageHash == lastMessageHash)
            return false;
        lastMessageHash = newMessageHash;
        return true;
    }

    public:

    UserFeedbackIterationListener()
    {
        longestMessage = 0;
        lastMessageHash = 0;
        lastIterationIndex = 0;
        lastIterationCount = 0;
    }

    virtual Status update(const UpdateMessage& updateMessage)
    {
        bool messageIsChanged = updateHashIfNewMessage(updateMessage.message);

        // skip update if nothing has changed (update was purely to allow for cancellation)
        if (!messageIsChanged && updateMessage.iterationIndex == lastIterationIndex && updateMessage.iterationCount == lastIterationCount)
            return Status_Ok;

        lastIterationIndex = updateMessage.iterationIndex;
        lastIterationCount = updateMessage.iterationCount;

        // spectrum and chromatogram lists both iterate; put them on different lines
        if (messageIsChanged || (updateMessage.message.empty() && updateMessage.iterationIndex + 1 >= updateMessage.iterationCount))
            *os_ << endl;

        stringstream updateString;
        if (updateMessage.message.empty())
            updateString << updateMessage.iterationIndex + 1 << "/" << updateMessage.iterationCount;
        else
            updateString << updateMessage.message << ": " << updateMessage.iterationIndex + 1 << "/" << updateMessage.iterationCount;

        longestMessage = max(longestMessage, (std::streamoff) updateString.tellp());
        updateString << string(longestMessage - updateString.tellp(), ' '); // add whitespace to erase all of the previous line
        *os_ << updateString.str() << "\r" << flush;

        return Status_Ok;
    }
};


void calculateSourceFilePtrSHA1(const SourceFilePtr& sourceFilePtr)
{
    calculateSourceFileSHA1(*sourceFilePtr);
}


/// Combines multiple input files into a single MSData object. Called
/// when the --merge argument is present on the command line.
int mergeFiles(const vector<string>& filenames, const Config& config, const ReaderList& readers)
{
    vector<MSDataPtr> msdList;
    int failedFileCount = 0;

    ReaderList::Config readerConfig(config);

    // Each file is read in separately in MSData objects in the msdList list.
    for(const string& filename : filenames)
    {
        try
        {
            *os_ << "processing file: " << filename << endl;
            readers.read(filename, msdList, readerConfig);
        }
        catch (exception& e)
        {
            ++failedFileCount;
            cerr << "Error reading file " << filename << ":\n" << e.what() << endl;
        }
    }

    // handle progress updates if requested

    IterationListenerRegistry iterationListenerRegistry;
    // update on the first spectrum, the last spectrum, the 100th spectrum, the 200th spectrum, etc.
    const size_t iterationPeriod = 100;
    iterationListenerRegistry.addListener(IterationListenerPtr(new UserFeedbackIterationListener), iterationPeriod);
    IterationListenerRegistry* pILR = config.verbose ? &iterationListenerRegistry : 0;

    if (!config.runIndexSet.empty())
    {
        vector<MSDataPtr> msdListFiltered;
        for (int i = 0; i < msdList.size(); ++i)
            if (config.runIndexSet.contains(i))
                msdListFiltered.push_back(msdList[i]);
        msdList.swap(msdListFiltered);

        if (msdList.empty())
            throw user_error("[msconvert] No runs correspond to the specified indices");
    }

    // MSDataMerger handles combining all files in msdList into a single MSDataFile object.
    try
    {
        MSDataMerger msd(msdList);

        if (!config.contactFilename.empty())
            addContactInfo(msd, config.contactFilename);

        SpectrumListFactory::wrap(msd, config.filters, pILR);
        ChromatogramListFactory::wrap(msd, config.chromatogramFilters, pILR);

        *os_ << "calculating source file checksums" << endl;
        calculateSHA1Checksums(msd);

        // if config.singleThreaded is not explicitly set, determine whether to use worker threads by querying SpectrumListWrappers
        Config configCopy(config);
        if (boost::indeterminate(config.singleThreaded) && boost::dynamic_pointer_cast<SpectrumListWrapper>(msd.run.spectrumListPtr) != nullptr)
            configCopy.singleThreaded = !boost::dynamic_pointer_cast<SpectrumListWrapper>(msd.run.spectrumListPtr)->benefitsFromWorkerThreads();
        configCopy.writeConfig.useWorkerThreads = !bool(config.singleThreaded);

        string outputFilename = config.outputFilename("merged-spectra", msd);
        *os_ << endl << "writing output file: " << outputFilename << endl;

        if (config.stripLocationFromSourceFiles)
            stripSourceFileLocation(msd);

        if (config.stripVersionFromSoftware)
            stripSoftwareVersion(msd);

        if (config.outputPath == "-")
            MSDataFile::write(msd, cout, configCopy.writeConfig);
        else
            MSDataFile::write(msd, outputFilename, configCopy.writeConfig, pILR);
    }
    catch (exception& e)
    {
        failedFileCount = (int)filenames.size();
        cerr << "Error merging files: " << e.what() << endl;
    }

    return failedFileCount;
}

/// Handles the reading of a single input file. Called once for each
/// input file when the --merge arguement is absent.
void processFile(const string& filename, const Config& config, const ReaderList& readers)
{
    // read in data file

    *os_ << "processing file: " << filename << endl;

    // handle progress updates if requested

    IterationListenerRegistry iterationListenerRegistry;
    // update on the first spectrum, the last spectrum, the 100th spectrum, the 200th spectrum, etc.
    //const size_t iterationPeriod = 100;
    iterationListenerRegistry.addListenerWithTimer(IterationListenerPtr(new UserFeedbackIterationListener), 0.5);
    IterationListenerRegistry* pILR = config.verbose ? &iterationListenerRegistry : 0;

    ReaderList::Config readerConfig(config);

    readerConfig.iterationListenerRegistry = pILR;

    vector<MSDataPtr> msdList;
    readers.read(filename, msdList, readerConfig);

    if (!config.runIndexSet.empty())
    {
        vector<MSDataPtr> msdListFiltered;
        for (int i = 0; i < msdList.size(); ++i)
            if (config.runIndexSet.contains(i))
                msdListFiltered.push_back(msdList[i]);
        msdList.swap(msdListFiltered);

        if (msdList.empty())
            throw user_error("[msconvert] No runs correspond to the specified indices");
    }

    int failedRuns = 0;

    for (size_t i=0; i < msdList.size(); ++i)
    {
        MSData& msd = *msdList[i];
        try
        {
            // process the data 

            if (!config.contactFilename.empty())
                addContactInfo(msd, config.contactFilename);

            SpectrumListFactory::wrap(msd, config.filters, pILR);
            ChromatogramListFactory::wrap(msd, config.chromatogramFilters, pILR);

            *os_ << "calculating source file checksums" << endl;
            calculateSHA1Checksums(msd);

            // if config.singleThreaded is not explicitly set, determine whether to use worker threads by querying SpectrumListWrappers
            Config configCopy(config);
            if (boost::indeterminate(config.singleThreaded) && boost::dynamic_pointer_cast<SpectrumListWrapper>(msd.run.spectrumListPtr) != nullptr)
                configCopy.singleThreaded = !boost::dynamic_pointer_cast<SpectrumListWrapper>(msd.run.spectrumListPtr)->benefitsFromWorkerThreads();
            configCopy.writeConfig.useWorkerThreads = !bool(configCopy.singleThreaded);

            // write out the new data file
            string outputFilename = config.outputFilename(filename, msd);
            //*os_ << "writing output file" << (configCopy.writeConfig.useWorkerThreads ? " (multithreaded)" : "") << ": " << outputFilename << endl;
            *os_ << endl << "writing output file: " << outputFilename << endl;

            if (config.stripLocationFromSourceFiles)
                stripSourceFileLocation(msd);

            if (config.stripVersionFromSoftware)
                stripSoftwareVersion(msd);

            if (config.outputPath == "-")
                MSDataFile::write(msd, cout, configCopy.writeConfig, pILR);
            else
            {
                // String compare of filenames is case-sensitive, which is a problem on Windows. bfs::equivalent() fixes this.
                if (!isHTTP(filename) && (filename == outputFilename || bfs::equivalent(filename, outputFilename)))
                {
                    throw user_error("[msconvert] Output filepath is the same as input filepath");
                }
                MSDataFile::write(msd, outputFilename, configCopy.writeConfig, pILR);
            }
        }
        catch (user_error&)
        {
            throw;
        }
        catch (exception& e)
        {
            cerr << "Error writing run " << (i+1) << ":\n" << e.what() << endl;
            ++failedRuns;
        }
    }
    *os_ << endl;

    if (failedRuns > 0)
        throw runtime_error("Conversion failed for " + toString(failedRuns) + " runs in " + bfs::path(filename).leaf().string() + ".");
}


/// Handles the high level logic of msconvert. Constructs the output
/// directory, reads files into memory and writes them out consistent
/// with the options in the supplied Config.
int go(const Config& config)
{
    *os_ << config;

    if (config.outputPath != "-" && !bfs::exists(config.outputPath))
        boost::filesystem::create_directories(config.outputPath);

    FullReaderList readers;

    int failedFileCount = 0;

    if (config.merge)
        failedFileCount = mergeFiles(config.filenames, config, readers);
    else
    {
        if (config.filenames.size() > 1 && running_on_wine())
            *os_ << "Warning: when running on Wine it is recommended to only process one file at a time" << endl;

        for (vector<string>::const_iterator it=config.filenames.begin(); 
             it!=config.filenames.end(); ++it)
        {
            try
            {
                processFile(*it, config, readers);
            }
            catch (user_error&)
            {
                throw;
            }
            catch (exception& e)
            {
                failedFileCount++;
                *os_ << e.what() << endl;
                *os_ << "Error processing file " << *it << "\n\n"; 
            }
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
    cout.imbue(loc);
    cerr.imbue(loc);

    try
    {
        Config config = parseCommandLine(argc, argv);

        // if redirecting conversion to cout, use cerr for all console output
        if (config.outputPath == "-")
            os_ = &cerr;

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

