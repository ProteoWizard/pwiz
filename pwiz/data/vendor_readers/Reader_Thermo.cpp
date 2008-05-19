//
// Reader_Thermo.cpp
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


#define PWIZ_SOURCE

#include "Reader_Thermo.hpp"
#include "utility/misc/Filesystem.hpp"
#include "utility/misc/String.hpp"

namespace {
// helper function used by both forms (real and stubbed) of Reader_Thermo
bool _hasRAWHeader(const std::string& head)
{
    const char rawHeader[] =
    {
        '\x01', '\xA1', 
        'F', '\0', 'i', '\0', 'n', '\0', 'n', '\0', 
        'i', '\0', 'g', '\0', 'a', '\0', 'n', '\0'
    };

    for (size_t i=0; i<sizeof(rawHeader); i++)
        if (head[i] != rawHeader[i]) 
            return false;

    return true;
}
} // namespace


#ifndef PWIZ_NO_READER_RAW
#include "data/msdata/CVTranslator.hpp"
#include "utility/vendor_api/thermo/RawFile.h"
#include "utility/misc/SHA1Calculator.hpp"
#include "boost/shared_ptr.hpp"
#include "boost/lexical_cast.hpp"
#include "boost/algorithm/string.hpp"
#include "boost/filesystem/path.hpp"
#include "Reader_Thermo_Detail.hpp"
#include "SpectrumList_Thermo.hpp"
#include "ChromatogramList_Thermo.hpp"
#include <iostream>
#include <iomanip>
#include <stdexcept>


namespace pwiz {
namespace msdata {


using namespace std;
using boost::shared_ptr;
using boost::lexical_cast;
using boost::bad_lexical_cast;
using namespace pwiz::raw;
using namespace pwiz::util;
namespace bfs = boost::filesystem;
using namespace pwiz::msdata::detail;


//
// Reader_Thermo
//


PWIZ_API_DECL bool Reader_Thermo::hasRAWHeader(const string& head)
{
    return _hasRAWHeader(head);
}

namespace {

auto_ptr<RawFileLibrary> rawFileLibrary_;

string creationDateToStartTimeStamp(string creationDate)
{
	// input format: "6/27/2007 15:23:45"
	// output format: "2007-06-27T15:23:45.00035"

	int month, day, year, hour, minute, second;
	char separator;

	istringstream iss(creationDate);
	iss >> month >> separator
	    >> day >> separator
		>> year
		>> hour >> separator
		>> minute >> separator
		>> second;

	ostringstream result;
	result << year << "-"
           << setfill('0')
	       << setw(2) << month << "-"
	       << setw(2) << day << "T"
	       << setw(2) << hour << ":"
	       << setw(2) << minute << ":"
	       << setw(2) << second;

	return result.str();
}


inline char idref_allowed(char c)
{
    return isalnum(c) || c=='-' ? 
           c : 
           '_';
}


string stringToIDREF(const string& s)
{
    string result = s;
    transform(result.begin(), result.end(), result.begin(), idref_allowed);
    return result;
}


void initializeInstrumentConfigurationPtrs(MSData& msd, 
                                           RawFile& rawfile, 
                                           const SoftwarePtr& instrumentSoftware)
{
    CVID cvidModel = translateAsInstrumentModel(rawfile.getInstrumentModel());

    // set common instrument parameters
    ParamGroupPtr commonInstrumentParams(new ParamGroup);
    commonInstrumentParams->id = "CommonInstrumentParams";
    msd.paramGroupPtrs.push_back(commonInstrumentParams);

    if (cvidModel != CVID_Unknown) 
        commonInstrumentParams->set(cvidModel);
    else
    {
        // TODO: add cvParam for "instrument unknown"
        commonInstrumentParams->userParams.push_back(UserParam("instrument model", rawfile.value(InstModel)));
    }

    commonInstrumentParams->set(MS_instrument_serial_number, rawfile.value(InstSerialNumber));

    // create instrument configuration templates based on the instrument model
    vector<InstrumentConfiguration> configurations = createInstrumentConfigurations(rawfile);

    for (size_t i=0; i < configurations.size(); ++i)
    {
        InstrumentConfigurationPtr ic = InstrumentConfigurationPtr(new InstrumentConfiguration(configurations[i]));

        ic->id = (format("IC%d") % (i+1)).str();
        ic->paramGroupPtrs.push_back(commonInstrumentParams);
        ic->softwarePtr = instrumentSoftware;

        msd.instrumentConfigurationPtrs.push_back(ic);
    }
}


void fillInMetadata(const string& filename, RawFile& rawfile, MSData& msd)
{
    msd.cvs.resize(1);
    CV& cv = msd.cvs.front();
    cv.URI = "psi-ms.obo"; 
    cv.id = "MS";
    cv.fullName = "Proteomics Standards Initiative Mass Spectrometry Ontology";
    cv.version = "1.0";

    msd.fileDescription.fileContent.cvParams.push_back(translateAsSpectrumType(rawfile.getScanInfo(1)->scanType()));

    SourceFilePtr sourceFile(new SourceFile);
    bfs::path p(filename);
    sourceFile->id = "RAW1";
    sourceFile->name = p.leaf();
    sourceFile->location = string("file://") + bfs::complete(p.branch_path()).string();
    sourceFile->cvParams.push_back(MS_Xcalibur_RAW_file);
    string sha1 = SHA1Calculator::hashFile(filename);
    sourceFile->cvParams.push_back(CVParam(MS_SHA_1, sha1));
    msd.fileDescription.sourceFilePtrs.push_back(sourceFile);

    msd.id = stringToIDREF(p.leaf());

    SoftwarePtr softwareXcalibur(new Software);
    softwareXcalibur->id = "Xcalibur";
    softwareXcalibur->softwareParam = MS_Xcalibur;
    softwareXcalibur->softwareParamVersion = rawfile.value(InstSoftwareVersion);
    msd.softwarePtrs.push_back(softwareXcalibur);

    SoftwarePtr softwarePwiz(new Software);
    softwarePwiz->id = "pwiz_Reader_Thermo";
    softwarePwiz->softwareParam = MS_pwiz;
    softwarePwiz->softwareParamVersion = "1.0"; 
    msd.softwarePtrs.push_back(softwarePwiz);

    DataProcessingPtr dpPwiz(new DataProcessing);
    dpPwiz->id = "pwiz_Reader_Thermo_conversion";
    dpPwiz->softwarePtr = softwarePwiz;
    dpPwiz->processingMethods.push_back(ProcessingMethod());
    dpPwiz->processingMethods.back().cvParams.push_back(MS_Conversion_to_mzML);
    msd.dataProcessingPtrs.push_back(dpPwiz);

    initializeInstrumentConfigurationPtrs(msd, rawfile, softwareXcalibur);
    if (!msd.instrumentConfigurationPtrs.empty())
        msd.run.defaultInstrumentConfigurationPtr = msd.instrumentConfigurationPtrs[0];

    msd.run.id = boost::to_lower_copy(stringToIDREF(filename));
    msd.run.startTimeStamp = creationDateToStartTimeStamp(rawfile.getCreationDate());
}

} // namespace


PWIZ_API_DECL bool Reader_Thermo::accept(const string& filename, const string& head) const
{
    return hasRAWHeader(head);
}


PWIZ_API_DECL
void Reader_Thermo::read(const string& filename, 
                         const string& head,
                         MSData& result) const
{
    // initialize RawFileLibrary

	if (!rawFileLibrary_.get()) {
		rawFileLibrary_.reset(new RawFileLibrary());
	}

    // instantiate RawFile, share ownership with SpectrumList_Thermo

    shared_ptr<RawFile> rawfile(RawFile::create(filename).release());
    rawfile->setCurrentController(Controller_MS, 1);

    SpectrumList_Thermo* sl = new SpectrumList_Thermo(result, rawfile);
    result.run.spectrumListPtr = SpectrumListPtr(sl);
    result.run.chromatogramListPtr = sl->Chromatograms();

    fillInMetadata(filename, *rawfile, result);
}


} // namespace msdata
} // namespace pwiz


#else // PWIZ_NO_READER_RAW /////////////////////////////////////////////////////////////////////////////

//
// non-MSVC implementation
//

#include "Reader_Thermo.hpp"
#include <stdexcept>

namespace pwiz {
namespace msdata {

using namespace std;

PWIZ_API_DECL bool Reader_Thermo::accept(const string& filename, const string& head) const
{
    return false;
}

PWIZ_API_DECL void Reader_Thermo::read(const string& filename, const string& head, MSData& result) const
{
    throw runtime_error("[Reader_Thermo::read()] Not implemented."); 
}

PWIZ_API_DECL bool Reader_Thermo::hasRAWHeader(const string& head)
{   
    return _hasRAWHeader(head);
}

} // namespace msdata
} // namespace pwiz

#endif // PWIZ_NO_READER_RAW /////////////////////////////////////////////////////////////////////////////

