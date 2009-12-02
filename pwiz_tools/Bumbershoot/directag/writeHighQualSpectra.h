#ifndef _WRITEHIGHQUALSPECTRA_H
#define _WRITEHIGHQUALSPECTRA_H

// Code for ScanRanker, use pwiz to write high quality spectra
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "stdafx.h"

using namespace pwiz::msdata;

namespace freicore
{
namespace directag
{

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

    Config()
    :   outputPath("."), verbose(false)
	{}

    string outputFilename(const string& inputFilename, const string& outFilename) const;
};

int writeHighQualSpectra(const string& inputFilename, const vector<int>& spectraIndices, const string& outputFormat, const string& outputFilename);

}
}
#endif
