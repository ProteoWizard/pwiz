/*
    $Id$
    Description: parsing for Thermo/Xcalibur "filter line".
    Date: July 25, 2007

    Copyright (C) 2007 Natalie Tasman, ISB Seattle

    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

*/


#ifndef _SCANFILTER_H_
#define _SCANFILTER_H_


#ifdef _MSC_VER
// disable warning "class needs to have dll-interface to be used by clients of class"
#pragma warning(push)
#pragma warning(disable:4251)
#endif


#include "RawFileTypes.h"
#include <string>
#include <vector>


namespace pwiz {
namespace vendor_api {
namespace Thermo {


class PWIZ_API_DECL ScanFilter
{
    public:

	ScanFilterMassAnalyzerType parseMassAnalyzerType(const std::string& word);
	PolarityType parsePolarityType(const std::string& word);
	DataPointType parseDataPointType(const std::string& word);
	IonizationType parseIonizationType(const std::string & word);
	ScanType parseScanType(const std::string& word);
	ActivationType parseActivationType(const std::string& word);
	AccurateMassType parseAccurateMassType(const std::string& word);
    TriBool parseCompensationVoltage(const std::string& word, double& voltage);

    // these two fields are only in Exactive files: {<segment>,<event>}
    // -1 means "unknown"
    int segment_;
    int event_;

	ScanFilterMassAnalyzerType massAnalyzerType_;
	PolarityType polarityType_;
	DataPointType dataPointType_;
	IonizationType ionizationType_;
	TriBool coronaOn_;
	TriBool photoIonizationOn_;
	TriBool sourceCIDOn_;
	TriBool detectorSet_;
	TriBool turboScanOn_;
    TriBool enhancedOn_; // enhanced resolution
	TriBool dependentActive_; // t: data-dependent active; f: non active
    TriBool supplementalCIDOn_;
	TriBool widebandOn_; // wideband activation
    TriBool ultraOn_;
    TriBool lockMassOn_;
    TriBool faimsOn_;
    TriBool spsOn_;
	AccurateMassType accurateMassType_;
	ScanType scanType_;
	int msLevel_; // n, in MSn: >0; msLevel == -1 for precursor ion scans
	ActivationType activationType_;
    double compensationVoltage_;
    bool multiplePrecursorMode_; // true for "MSX"
    bool constantNeutralLoss_; // true for "CNL"
    double analyzer_scan_offset_; // found with CNL

	std::vector<double> precursorMZs_; // one entry per ms level for level >= 2
	std::vector<double> precursorEnergies_; // relative units; one entry per ms level for level >= 2

    std::vector<ActivationType> saTypes_; // only seen with Fusion Lumos so far
    std::vector<double> saEnergies_;

	std::vector<double> scanRangeMin_;
	std::vector<double> scanRangeMax_;


	ScanFilter();
	~ScanFilter();

	void print();

    void initialize();
	void parse(const std::string& filterLine);

};


} // namespace Thermo
} // namespace vendor_api
} // namespace pwiz

#ifdef _MSC_VER
#pragma warning(pop)
#endif

#endif // _SCANFILTER_H_
