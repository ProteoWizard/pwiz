
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
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Matthew Chambers, Zeqiang Ma

#ifndef _SCANRANKER_H
#define _SCANRANKER_H

#include "pwiz/data/msdata/MSDataFile.hpp"
#include "stdafx.h"
#include "shared_types.h"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include <iostream>

using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;

using namespace freicore;

namespace freicore
{
namespace directag
{
namespace scanranker
{    
    float                    bestTagScoreMean;
    float                    bestTagTICMean;
    float                    tagMzRangeMean;
    float                    bestTagScoreIQR;
    float                    bestTagTICIQR;
    float                    tagMzRangeIQR;
    size_t                    numTaggedSpectra;

    struct spectraSortByQualScore
    {
        bool operator() ( const Spectrum* a, const Spectrum* b )
        {
            return a->qualScore > b->qualScore;  // descending order
        }
    };

    // configuration for writing msdata
    struct Config
    {
        //index filter defined later
        string filename;
        string outputPath;
        string extension;
        bool verbose;
        MSDataFile::WriteConfig writeConfig;
        string contactFilename;

        Config() : outputPath("."), verbose(false) {}

        Config(const string& inFile, const string& outputFormat, const string& path = ".", const bool verb = false)
        {
            filename = inFile;
            outputPath = path;
            verbose = verb;

            bool format_text = false;
            bool format_mzML = false;
            bool format_mzXML = false;
            bool format_MGF = false;
            bool format_MS2 = false;
            bool precision_32 = true;
            bool precision_64 = false;
            bool mz_precision_32 = true;
            bool mz_precision_64 = false;
            bool intensity_precision_32 = true;
            bool intensity_precision_64 = false;
            bool noindex = false;
            bool zlib = false;
            bool gzip = false;

            if ( outputFormat == "mzXML" || outputFormat == "mzxml" )
                format_mzXML = true;
            else if ( outputFormat == "mzML" || outputFormat == "mzml" )
                format_mzML = true;
            else if ( outputFormat == "MGF" || outputFormat == "mgf" )
                format_MGF = true;
            else if ( outputFormat == "MS2" || outputFormat == "ms2" )
                format_MS2 = true;
            else
                throw runtime_error(" No output format specified.");

            if (filename.empty())
                throw runtime_error("[msconvert] No files specified.");

            int count = format_text + format_mzML + format_mzXML + format_MGF + format_MS2;
            if (count > 1) throw runtime_error("[msconvert] Multiple format flags specified.");
            if (format_text) writeConfig.format = MSDataFile::Format_Text;
            if (format_mzML) writeConfig.format = MSDataFile::Format_mzML;
            if (format_mzXML) writeConfig.format = MSDataFile::Format_mzXML;
            if (format_MGF) writeConfig.format = MSDataFile::Format_MGF;
            if (format_MS2) writeConfig.format = MSDataFile::Format_MS2;

            writeConfig.gzipped = gzip; // if true, file is written as .gz

            if (extension.empty())
            {
                switch (writeConfig.format)
                {
                case MSDataFile::Format_Text:
                    extension = ".txt";
                    break;
                case MSDataFile::Format_mzML:
                    extension = ".mzML";
                    break;
                case MSDataFile::Format_mzXML:
                    extension = ".mzXML";
                    break;
                case MSDataFile::Format_MGF:
                    extension = ".mgf";
                    break;
                case MSDataFile::Format_MS2:
                    extension = ".MS2";
                    break;
                default:
                    throw runtime_error("[msconvert] Unsupported format."); 
                }
                if (writeConfig.gzipped) 
                {
                    extension += ".gz";
                }
            }

            // precision defaults

            writeConfig.binaryDataEncoderConfig.precision = BinaryDataEncoder::Precision_64;
            writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_64;
            writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;

            // handle precision flags

            if (precision_32 && precision_64 ||
                mz_precision_32 && mz_precision_64 ||
                intensity_precision_32 && intensity_precision_64)
                throw runtime_error("[msconvert] Incompatible precision flags.");

            if (precision_32)
            {
                writeConfig.binaryDataEncoderConfig.precision
                    = writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array]
                    = writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] 
                    = BinaryDataEncoder::Precision_32;
            }
            else if (precision_64)
            {
                writeConfig.binaryDataEncoderConfig.precision
                    = writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array]
                    = writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] 
                    = BinaryDataEncoder::Precision_64;
            }

            if (mz_precision_32)
                writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_32;
            if (mz_precision_64)
                writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_m_z_array] = BinaryDataEncoder::Precision_64;
            if (intensity_precision_32)
                writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_32;
            if (intensity_precision_64)
                writeConfig.binaryDataEncoderConfig.precisionOverrides[MS_intensity_array] = BinaryDataEncoder::Precision_64;

            // other flags

            if (noindex)
                writeConfig.indexed = false;

            if (zlib)
                writeConfig.binaryDataEncoderConfig.compression = BinaryDataEncoder::Compression_Zlib;
        }

        string outputFilename(const string& filename, const string& outFilename) const
        {
            namespace bfs = boost::filesystem;
            bfs::path newFilename = (outFilename.empty()) ? (bfs::basename(filename) + "-HighQualSpectra" + extension) : outFilename;
            bfs::path fullPath = bfs::path(outputPath) / newFilename;
            return fullPath.string(); 
        }
    };
    
    ostream& operator<<(ostream& os, const Config& config)
    {
        os << "writing out high quality spectra" << endl;
        os << "format: " << config.writeConfig << endl;
        os << "outputPath: " << config.outputPath << endl;
        os << "extension: " << config.extension << endl; 
        os << "contactFilename: " << config.contactFilename << endl;
        os << endl;
        /*   os << "filters:\n  ";
        copy(config.filters.begin(), config.filters.end(), ostream_iterator<string>(os,"\n  "));
        os << endl;*/
        os << "filename:\n " << config.filename << endl;
        return os;
    }

    struct SpectrumList_FilterPredicate_NativeIDSet : public SpectrumList_Filter::Predicate
    {
        template <typename ForwardIterator>
        SpectrumList_FilterPredicate_NativeIDSet(const ForwardIterator& begin, const ForwardIterator& end)
            : nativeIDSet_(begin, end)
        {}

        virtual boost::logic::tribool accept(const SpectrumIdentity& spectrumIdentity) const
        {
            return nativeIDSet_.count(spectrumIdentity.id) > 0;
        }

        virtual bool done() const {return false;}

    private:
        set<NativeID> nativeIDSet_;
    };

    int writeHighQualSpectra( const string& inputFilename, const vector<NativeID>& spectrumIDs,
                              const string& outputFormat, const string& outFilename) 
    {
        try
        {
            Config config(inputFilename, outputFormat);
            cout << config;
            boost::filesystem::create_directories(config.outputPath);
            cout << "processing file: " << config.filename << endl;
            MSDataFile file( config.filename );
            SpectrumList_FilterPredicate_NativeIDSet nativeIDFilter(spectrumIDs.begin(), spectrumIDs.end());
            file.run.spectrumListPtr = SpectrumListPtr( new SpectrumList_Filter( file.run.spectrumListPtr, nativeIDFilter ) );
            string outputFilename = config.outputFilename(config.filename,outFilename);
            cout << "writing output file: " << outputFilename << endl;
            file.write(outputFilename, config.writeConfig);
            cout << endl;
            return 0;
        }
        catch (exception& e)
        {
            cerr << e.what() << endl;
            cout << "Error processing file " << inputFilename << endl;
        }
        catch (...)
        {
            cerr << "Caught unknown exception when writing high quality spectra" << endl;
        }

        return 1;
    }
    // Code for calculating ScanRanker score
    void CalculateQualScore( SpectraList& instance)
    {
        vector<float> bestTagScoreList;
        vector<float> bestTagTICList;
        vector<float> tagMzRangeList;
        //vector<float> rankedBestTagScoreList;
        //vector<float> rankedBestTagTICList;
        //vector<float> rankedTagMzRangeList;

        Spectrum* s;
        // string rankMethod = "average"; //Can also be "min" or "max" or "default"

        // use log transformed mean and IQR of spectra with at least 1 tag for normalization
        for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
        {
            s = *sItr;
            if ( s->bestTagScore != 0 )    // at least 1 tag generated and <= MaxTagScore
            {  
                bestTagScoreList.push_back( s->bestTagScore );  // bestTagScore is the chisqured value
                bestTagTICList.push_back( log( s->bestTagTIC ));
                tagMzRangeList.push_back( s->tagMzRange );
            }
        }

        //rankhigh( bestTagScoreList, rankedBestTagScoreList, rankMethod );
        //rank( bestTagTICList, rankedBestTagTICList, rankMethod );
        //rank( tagMzRangeList, rankedTagMzRangeList, rankMethod );

        float bestTagScoreSum = accumulate( bestTagScoreList.begin(), bestTagScoreList.end(), 0.0 );
        float bestTagTICSum = accumulate( bestTagTICList.begin(), bestTagTICList.end(), 0.0 );
        float tagMzRangeSum = accumulate( tagMzRangeList.begin(), tagMzRangeList.end(), 0.0 );

        std::sort( bestTagScoreList.begin(), bestTagScoreList.end() );
        bestTagScoreIQR = bestTagScoreList[(int)(bestTagScoreList.size() * 0.75)] - bestTagScoreList[(int)(bestTagScoreList.size() * 0.25)];
        std::sort( bestTagTICList.begin(), bestTagTICList.end() );
        bestTagTICIQR = bestTagTICList[(int)(bestTagTICList.size() * 0.75)] - bestTagTICList[(int)(bestTagTICList.size() * 0.25)];
        std::sort( tagMzRangeList.begin(), tagMzRangeList.end() );
        tagMzRangeIQR = tagMzRangeList[(int)(tagMzRangeList.size() * 0.75)] - tagMzRangeList[(int)(tagMzRangeList.size() * 0.25)];
        //int i = 0;
        //size_t numSpectra = instance.size();
        numTaggedSpectra = bestTagScoreList.size();
        bestTagScoreMean = bestTagScoreSum / (float) numTaggedSpectra;
        bestTagTICMean = bestTagTICSum / (float) numTaggedSpectra;
        tagMzRangeMean = tagMzRangeSum / (float) numTaggedSpectra;

        for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
        {
            s = *sItr;
            s->bestTagScoreNorm = (s->bestTagScore - bestTagScoreMean) / bestTagScoreIQR;
            s->bestTagTICNorm = ( s->bestTagScore == 0 ) ? ( 0 - bestTagTICMean) / bestTagTICIQR : (log( s->bestTagTIC ) - bestTagTICMean) / bestTagTICIQR;
            s->tagMzRangeNorm = ( s->bestTagScore == 0 ) ? ( 0 - tagMzRangeMean) / tagMzRangeIQR : ( s->tagMzRange - tagMzRangeMean) /tagMzRangeIQR;

            //s->bestTagScoreNorm = (rankedBestTagScoreList[i]-1) / (float) numTotalSpectra;
            //s->bestTagTICNorm = (rankedBestTagTICList[i]-1) / (float) numTotalSpectra;
            //s->tagMzRangeNorm = (rankedTagMzRangeList[i]-1) / (float) numTotalSpectra;
            //s->qualScore = ( rankedBestTagScoreList[i] + rankedBestTagTICList[i] + rankedTagMzRangeList[i] ) / (3 * (float) numTotalSpectra);
            s->qualScore = (s->bestTagScoreNorm + s->bestTagTICNorm + s->tagMzRangeNorm ) / 3;
            //++i;
        }
    }

    // Code for writing ScanRanker metrics file
    void WriteSpecQualMetrics( const string& inputFilename, SpectraList& instance, const string& outFilename)
    {
        cout << "Generating output of quality metrics." << endl;
        string filenameAsScanName;
        filenameAsScanName =    inputFilename.substr( inputFilename.find_last_of( SYS_PATH_SEPARATOR )+1,
            inputFilename.find_last_of( '.' ) - inputFilename.find_last_of( SYS_PATH_SEPARATOR )-1 );

        string outputFilename = (outFilename.empty()) ? (filenameAsScanName + "-ScanRankerMetrics" + ".txt") :  outFilename;

        ofstream fileStream( outputFilename.c_str() );

        fileStream << "H\tBestTagScoreMean\tBestTagTICMean\tTagMzRangeMean\tBestTagScoreIQR\tBestTagTICIQR\tTagMzRangeIQR\tnumTaggedSpectra\n";
        fileStream << "H"<< '\t'
            << bestTagScoreMean << '\t'
            << bestTagTICMean << '\t'
            << tagMzRangeMean << '\t'
            << bestTagScoreIQR << '\t'
            << bestTagTICIQR << '\t'
            << tagMzRangeIQR << '\t'
            << numTaggedSpectra << "\n";
        //fileStream << "H\tIndex\tNativeID\tPrecursorMZ\tCharge\tPrecursorMass\tBestTagScore\tBestTagTIC\tTagMzRange\tScanRankerScore\n" ;
        fileStream << "H\tNativeID\tPrecursorMZ\tCharge\tPrecursorMass\tBestTagScore\tBestTagTIC\tTagMzRange\tScanRankerScore\n" ;
        set<NativeID> seen;
        Spectrum* s;
        for( SpectraList::iterator sItr = instance.begin(); sItr != instance.end(); ++sItr )
        {
            s = *sItr;
            float logBestTagTIC = (s->bestTagTIC == 0) ? 0 : (log( s->bestTagTIC ));
            pair<set<NativeID>::iterator, bool> insertResult = seen.insert(s->id.nativeID);
            if( insertResult.second ) // only write out metrics of best scored spectrum if existing multiple charge states
            {
                fileStream << "S" << '\t'
                    //<< s->nativeID << '\t'
                    << s->nativeID << '\t'
                    << s->mzOfPrecursor << '\t'
                    << s->id.charge << '\t'
                    << s->mOfPrecursor << '\t'
                    << s->bestTagScore << '\t'
                    << logBestTagTIC << '\t'
                    << s->tagMzRange << '\t'
                    //<< s->bestTagScoreNorm << '\t'
                    //<< s->bestTagTICNorm << '\t'
                    //<< s->tagMzRangeNorm << '\t'
                    << s->qualScore << '\n';
            }
        }
    }
}
}
}

#endif
