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


#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/msdata/IO.hpp"
#include "pwiz/data/msdata/SpectrumInfo.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include "pwiz_aux/isb/readers/waters/Reader_Waters.hpp"
#include "pwiz_aux/msrc/data/vendor_readers/Reader_Bruker.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_NativeCentroider.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_PrecursorRecalculator.hpp"
#include "pwiz/Version.hpp"
#include "boost/program_options.hpp"
#include "boost/filesystem/path.hpp"
#include "boost/filesystem/convenience.hpp"
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
    string outputPath;
    string extension;
    bool verbose;
    MSDataFile::WriteConfig writeConfig;
    string contactFilename;
    string msLevelsToCentroid;
    bool stripITScans;
    string indexSubset;
    string scanSubset;
    bool recalculatePrecursors;

    Config()
    :   outputPath("."), verbose(false), stripITScans(false), recalculatePrecursors(false)
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
    os << "msLevelsToCentroid: " << config.msLevelsToCentroid << endl;
    os << "stripITScans: " << boolalpha << config.stripITScans << endl;
    os << "indexSubset: " << config.indexSubset << endl;
    os << "scanSubset: " << config.scanSubset << endl;
    os << "recalculatePrecursors: " << boolalpha << config.recalculatePrecursors << endl;

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
          << "Convert mass spec data file formats.\n"
          << "\n"
          << "Return value: # of failed files.\n"
          << "\n";
        
    Config config;
    string filelistFilename;

    bool format_text = false;
    bool format_mzML = false;
    bool format_mzXML = false;
    bool precision_32 = false;
    bool precision_64 = false;
    bool mz_precision_32 = false;
    bool mz_precision_64 = false;
    bool intensity_precision_32 = false;
    bool intensity_precision_64 = false;
    bool noindex = false;
    bool zlib = false;

    po::options_description od_config("Options");
    od_config.add_options()
        ("filelist,f",
            po::value<string>(&filelistFilename),
            ": specify text file containing filenames")
        ("outdir,o",
            po::value<string>(&config.outputPath)->default_value(config.outputPath),
            ": set output directory [.]")
        ("ext,e",
            po::value<string>(&config.extension)->default_value(config.extension),
			": set extension for output files [mzML|mzXML|txt]")
        ("mzML",
            po::value<bool>(&format_mzML)->zero_tokens(),
			": write mzML format [default]")
        ("mzXML",
            po::value<bool>(&format_mzXML)->zero_tokens(),
			": write mzXML format")
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
        ("centroid,c",
            po::value<string>(&config.msLevelsToCentroid)->default_value(config.msLevelsToCentroid),
			": centroid spectrum data for msLevel ranges (list of closed intervals, e.g. \"[2,3] [5,7]\")")
        ("zlib,z",
            po::value<bool>(&zlib)->zero_tokens(),
			": use zlib compression for binary data")
        ("stripIT",
            po::value<bool>(&config.stripITScans)->zero_tokens(),
			": strip ion trap ms1 scans")
        ("indexSubset",
            po::value<string>(&config.indexSubset)->default_value(config.indexSubset),
			": specify subset of spectrum indices (list of closed intervals, e.g. \"[0,3] [5,5] [11,13]\")")
        ("scanSubset",
            po::value<string>(&config.scanSubset)->default_value(config.scanSubset),
			": specify subset of scan numbers (list of closed intervals, e.g. \"[1,4] [6,6] [12,14]\")")
        ("precursorRecalculation,p",
            po::value<bool>(&config.recalculatePrecursors)->zero_tokens(),
			": recalculate precursor info based on ms1 data (msprefix)")
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

    usage << "\n"
          << "Questions, comments, and bug reports:\n"
          << "http://proteowizard.sourceforge.net\n"
          << "support@proteowizard.org\n"
          << "\n"
          << "ProteoWizard release: " << pwiz::Version::str() << endl
          << "Build date: " << __DATE__ << " " << __TIME__ << endl;

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

    // default BinaryDataEncoder precision

    count = precision_32 + precision_64;
    if (count > 1) throw runtime_error(usage.str());
    config.writeConfig.binaryDataEncoderConfig.precision = precision_32 ? 
                                                           BinaryDataEncoder::Precision_32 :
                                                           BinaryDataEncoder::Precision_64;

    // precision overrides

    count = mz_precision_32 + mz_precision_64 + intensity_precision_32 + intensity_precision_64;
    if (count > 2) throw runtime_error(usage.str());

    config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_64;
    config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;

    if (mz_precision_32)
        config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_32;
    if (mz_precision_64)
        config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_64;
    if (intensity_precision_32)
        config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;
    if (intensity_precision_64)
        config.writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_64;

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


void wrapSpectrumList_indexSubset(MSData& msd, const string& indexSubsetString)
{
    IntegerSet indexSet;
    indexSet.parse(indexSubsetString);

    shared_ptr<SpectrumList_Filter> 
        filter(new SpectrumList_Filter(msd.run.spectrumListPtr, 
                                       SpectrumList_FilterPredicate_IndexSet(indexSet)));

    msd.run.spectrumListPtr = filter;
}


void wrapSpectrumList_scanSubset(MSData& msd, const string& scanSubsetString)
{
    IntegerSet scanNumberSet;
    scanNumberSet.parse(scanSubsetString);

    shared_ptr<SpectrumList_Filter> 
        filter(new SpectrumList_Filter(msd.run.spectrumListPtr, 
                                       SpectrumList_FilterPredicate_ScanNumberSet(scanNumberSet)));

    msd.run.spectrumListPtr = filter;
}


void wrapSpectrumList_nativeCentroider(MSData& msd, const string& msLevelsToCentroidString)
{
    IntegerSet msLevelsToCentroid;
    msLevelsToCentroid.parse(msLevelsToCentroidString);

    shared_ptr<SpectrumList_NativeCentroider> 
        nativeCentroider(new SpectrumList_NativeCentroider(msd.run.spectrumListPtr,
                                                           msLevelsToCentroid));
    msd.run.spectrumListPtr = nativeCentroider;
}


struct StripIonTrapSurveyScans : public SpectrumList_Filter::Predicate
{
    virtual boost::logic::tribool accept(const SpectrumIdentity& spectrumIdentity) const
    {
        return boost::logic::indeterminate; // need full Spectrum
    }

    virtual bool accept(const Spectrum& spectrum) const
    {
        SpectrumInfo info(spectrum);
        return !(info.msLevel==1 && cvIsA(info.massAnalyzerType, MS_ion_trap));
    }
};


void wrapSpectrumList_stripIT(MSData& msd)
{
    shared_ptr<SpectrumList_Filter> 
        filter(new SpectrumList_Filter(msd.run.spectrumListPtr, 
                                       StripIonTrapSurveyScans()));
    msd.run.spectrumListPtr = filter;    
}


void wrapSpectrumList_recalculatePrecursors(MSData& msd)
{
    shared_ptr<SpectrumList_PrecursorRecalculator> pr(new SpectrumList_PrecursorRecalculator(msd));
    msd.run.spectrumListPtr = pr;    
}


class UserFeedbackIterationListener : public IterationListener
{
    public:

    virtual Status update(const UpdateMessage& updateMessage)
    {
        cout << updateMessage.iterationIndex << "/" << updateMessage.iterationCount << endl;
        return Status_Ok;
    }
};


void processFile(const string& filename, const Config& config, const ReaderList& readers)
{
    // read in data file

    cout << "processing file: " << filename << endl;

    const bool calculateSHA1 = true;
    MSDataFile msd(filename, &readers, calculateSHA1);

    // process the data 

    if (!config.contactFilename.empty())
        addContactInfo(msd, config.contactFilename);

    if (!config.msLevelsToCentroid.empty()) // wrap immediately above native SpectrumList
        wrapSpectrumList_nativeCentroider(msd, config.msLevelsToCentroid);

    if (!config.indexSubset.empty())
        wrapSpectrumList_indexSubset(msd, config.indexSubset); 

    if (!config.scanSubset.empty())
        wrapSpectrumList_scanSubset(msd, config.scanSubset); 

    if (config.stripITScans)
        wrapSpectrumList_stripIT(msd);

    if (config.recalculatePrecursors)
        wrapSpectrumList_recalculatePrecursors(msd);

    // handle progress updates if requested

    IterationListenerRegistry iterationListenerRegistry;
    UserFeedbackIterationListener feedback;
    const size_t iterationPeriod = 100;
    iterationListenerRegistry.addListener(feedback, iterationPeriod);
    IterationListenerRegistry* pILR = config.verbose ? &iterationListenerRegistry : 0; 
 
    // write out the new data file

    string outputFilename = config.outputFilename(filename);
    cout << "writing output file: " << outputFilename << endl;
    msd.write(outputFilename, config.writeConfig, pILR);

    cout << endl;
}


int go(const Config& config)
{
    cout << config;

    boost::filesystem::create_directories(config.outputPath);

    ExtendedReaderList readers;
    readers += ReaderPtr(new Reader_Waters) + ReaderPtr(new Reader_Bruker);

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

