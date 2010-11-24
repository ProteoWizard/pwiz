//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris, Zeqiang Ma
//

// modified from msconvert source code

#include "writeHighQualSpectra.h"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/utility/misc/IntegerSet.hpp"
#include "pwiz/analysis/spectrum_processing/SpectrumList_Filter.hpp"
#include <iostream>

using namespace std;
using namespace pwiz;
//using namespace pwiz::msdata;
using namespace pwiz::analysis;
using namespace pwiz::util;


namespace freicore
{
namespace directag
{

string Config::outputFilename(const string& filename, const string& outFilename) const
{
    namespace bfs = boost::filesystem;
	bfs::path newFilename = (outFilename.empty()) ? (bfs::basename(filename) + "-HighQualSpectra" + extension) : outFilename;
    bfs::path fullPath = bfs::path(outputPath) / newFilename;
    return fullPath.string(); 
}

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


Config createConfig(const string& inFile, const string& outputFormat)
{
    Config config;
	config.filename = inFile;

    bool format_text = false;
    bool format_mzML = false;
    bool format_mzXML = false;
    bool format_MGF = false;
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
	{
		format_mzXML = true;
	}
	else if ( outputFormat == "mzML" || outputFormat == "mzml" )
	{
		format_mzML = true;
	}
	else if ( outputFormat == "MGF" || outputFormat == "mgf" )
	{
		format_MGF = true;
	}
	else
	{
		throw runtime_error(" No output format specified.");
	}

	if (config.filename.empty())
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

void processFile(const string& filename, const Config& config, const IntegerSet& indexSet, const string& outFilename)
{
    // read in data file

    cout << "processing file: " << filename << endl;

	MSDataFile file( filename );
	file.run.spectrumListPtr = SpectrumListPtr( new SpectrumList_Filter( file.run.spectrumListPtr, SpectrumList_FilterPredicate_IndexSet(indexSet) ) );
	string outputFilename = config.outputFilename(filename,outFilename);
    cout << "writing output file: " << outputFilename << endl;
	file.write(outputFilename, config.writeConfig);
    cout << endl;
}


int go(const Config& config, const IntegerSet& indexSet, const string& outputFilename)
{
    cout << config;
    boost::filesystem::create_directories(config.outputPath);
	try
	{
		processFile( config.filename, config, indexSet, outputFilename );
	}
	catch (exception& e)
   {
        cout << e.what() << endl;
		cout << "Error processing file " << config.filename << "\n\n"; 
   }

	return 0;
}


int writeHighQualSpectra(	const string& inputFilename,
							const vector<int>& spectraIndices,
							const string& outputFormat,
							const string& outputFilename) 
{
	/*
freicore::BaseSpectrum::id.index maps to the pwiz::Spectrum::index
freicore::BaseSpectrum::nativeID maps to pwiz::Spectrum::id
scanNumber assumes it's a thermo nativeID. It's index+1 basically. Unless it has been resorted or filtered somehow. Use index
index is the place that that <scan> appears in the list of scans
*/
	IntegerSet indexSet;
	for( vector<int>::const_iterator itr = spectraIndices.begin(); itr != spectraIndices.end(); ++itr)
	{
		indexSet.insert(*itr);
	}

    try
    {
        Config config = createConfig(inputFilename, outputFormat);        
        return go(config, indexSet, outputFilename);
    }
    catch (exception& e)
    {
        cerr << e.what() << endl;
    }
    catch (...)
    {
        cerr << "Caught unknown exception when writing high quality spectra.\n";
    }

	return 1;
}

}
}

