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
