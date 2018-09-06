/*
    $Id$
    Description: parsing for Thermo/Xcalibur "filter line".
    Date: April 16, 2008

    Originally copyright (C) 2007 Natalie Tasman, ISB Seattle


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


#define PWIZ_SOURCE

#include "ScanFilter.h"
#include "pwiz/utility/misc/Std.hpp"
#include <boost/regex.hpp>

using namespace pwiz::vendor_api::Thermo;

/*

FilterLine dictionary
--From Thermo


Analyzer:

ITMS		Ion Trap
TQMS		Triple Quad
SQMS		Single Quad
TOFMS		TOF
FTMS		ICR
Sector		Sector

Segment Scan Event   (Sectors only)

Polarity
-		Negative
+		Positive


Scan Data
c		centroid
p		profile


Ionization Mode
EI		Electron Impact
CI		Chemical Ionization
FAB		Fast Atom Bombardment
ESI		Electrospray
APCI		Atmospheric Pressure Chemical Ionization
NSI		Nanospray
TSP		Thermospray
FD		Field Desorption
MALDI	Matrix Assisted Laser Desorption Ionization
GD		Glow Discharge

Corona
corona			corona on
!corona		corona off

PhotoIoniziation
pi			photo ionization on
!pi			photo ionization off

Source CID
sid			source cid on
!sid			source cid off
sid=<x>     source cid on at <x> energy

Detector set
det			detector set
!det			detector not set

TurboScan
t			turbo scan on
!t			turob scan off

Enhanced			(Sectors only)
E			enhanced on
!E			enhanced off

Dependent Type
d			data dependent active
!d			data dependent not-active

Supplemental CID
sa			supplemental cid

Wideband
w			wideband activation on
!w			wideband activation off

Accurate Mass
!AM			accurate mass not active
AM			accurate mass active 
AMI			accurate mass with internal calibration
AME			accurate mass with external calibration

Ultra
u			ultra on
!u			ultra off

Rapid
r			rapid on

Scan Type:
full			full scan
SIM			single ion monitor
SRM			single reaction monitor
CRM
z			zoom scan
Q1MS			q1 mass spec scan
Q3MS			q3 mass spec scan 

Sector Scan			(Sectors only)
BSCAN		b scan
ESCAN		e scan


Precursor Ion Scan
pr          yes
MS[#]       no

MSorder
MS2			MSn order
MS3
…
MS15

Activation Type
cid			collision induced dissociation
mpd
ecd			electron capture dissociation
pqd			pulsed q dissociation
etd			electron transfer dissociation
hcd			high energy collision dissociation
ptr			proton transfer reaction

Free Region			(Sectors only)
ffr1			field free region 1
ffr2			field free region 2

Mass range
[low mass – high mass]

*/

ScanFilterMassAnalyzerType
ScanFilter::parseMassAnalyzerType(const string& word)
{
	if (word == "ITMS")
		return ScanFilterMassAnalyzerType_ITMS;
	else if (word == "TQMS")
		return ScanFilterMassAnalyzerType_TQMS;
	else if (word == "SQMS")
		return ScanFilterMassAnalyzerType_SQMS;
	else if (word == "TOFMS")
		return ScanFilterMassAnalyzerType_TOFMS;
	else if (word == "FTMS")
		return ScanFilterMassAnalyzerType_FTMS;
	else if (word == "SECTOR")
		return ScanFilterMassAnalyzerType_Sector;
	else
		return ScanFilterMassAnalyzerType_Unknown;
}

PolarityType 
ScanFilter::parsePolarityType(const string& word)
{
	if (word == "+")
		return PolarityType_Positive;
	else if (word == "-")
		return PolarityType_Negative;
	else
		return PolarityType_Unknown;
}

DataPointType 
ScanFilter::parseDataPointType(const string& word)
{
	if (bal::iequals(word, "C"))
		return DataPointType_Centroid;
	else if (bal::iequals(word, "P"))
		return DataPointType_Profile;
	else
		return DataPointType_Unknown;
}

IonizationType 
ScanFilter::parseIonizationType(const string & word)
{
	if (word == "EI")
		return IonizationType_EI;
	else if (word == "CI")
		return IonizationType_CI;
	else if (word == "FAB")
		return IonizationType_FAB;
	else if (word == "ESI")
		return IonizationType_ESI;
	else if (word == "APCI")
		return IonizationType_APCI;
	else if (word == "NSI")
		return IonizationType_NSI;
	else if (word == "TSP")
		return IonizationType_TSP;
	else if (word == "FD")
		return IonizationType_FD;
	else if (word == "MALDI")
		return IonizationType_MALDI;
	else if (word == "GD")
		return IonizationType_GD;
	else
		return IonizationType_Unknown;
}

AccurateMassType 
ScanFilter::parseAccurateMassType(const string& word)
{
	if (word == "!AM") {
		return AccurateMass_NotActive;
	}
	else if (word == "AM") {
		return AccurateMass_Active;
	}
	else if (word == "AMI") {
		return AccurateMass_ActiveWithInternalCalibration;
	}
	else if (word == "AME") {
		return AccurateMass_ActiveWithExternalCalibration;
	}
	else {
		return AccurateMass_Unknown;
	}
}

TriBool
ScanFilter::parseCompensationVoltage(const string& word, double& voltage) {
  if (!bal::istarts_with(word, "cv="))
    return TriBool_False;
  else {
	vector<string> nameVal;
	boost::split(nameVal, word, boost::is_any_of("="));
    voltage = lexical_cast<double>(nameVal[1]);
    return TriBool_True;
  }
}

ScanType 
ScanFilter::parseScanType(const string& word)
{
	if (bal::iequals(word, "FULL"))
		return ScanType_Full;
	else if (word == "SIM")
		return ScanType_SIM;
	else if (word == "SRM")
		return ScanType_SRM;
	else if (word == "CRM")
		return ScanType_CRM;
	else if (word == "Z")
		return ScanType_Zoom;
	else if (word == "Q1MS")
        return ScanType_Q1MS;
	else if (word == "Q3MS")
        return ScanType_Q3MS;
	else
		return ScanType_Unknown;
}

ActivationType 
ScanFilter::parseActivationType(const string& word)
{
	if (bal::iequals(word, "CID"))
		return ActivationType_CID;
	else if (bal::iequals(word, "MPD"))
		return ActivationType_MPD;
	else if (bal::iequals(word, "ECD"))
		return ActivationType_ECD;
	else if (bal::iequals(word, "PQD"))
		return ActivationType_PQD;
	else if (bal::iequals(word, "ETD"))
		return ActivationType_ETD;
	else if (bal::iequals(word, "HCD"))
		return ActivationType_HCD;
	else if (bal::iequals(word, "PTR"))
		return ActivationType_PTR;
	else
		return ActivationType_Unknown;
}


ScanFilter::ScanFilter()
{
    initialize();
};


ScanFilter::~ScanFilter()
{
}


void 
ScanFilter::print()
{
	if (massAnalyzerType_ > ScanFilterMassAnalyzerType_Unknown) {
        cout << "mass analyzer: " << massAnalyzerType_ << endl;
	}

	if (polarityType_ > PolarityType_Unknown) {
        cout << "polarity: " << polarityType_ << endl;
	}

	if (dataPointType_ > DataPointType_Unknown) {
        cout << "data point type: " << dataPointType_ << endl;
	}

	if (ionizationType_ > IonizationType_Unknown) {
        cout << "ionization type: " << ionizationType_ << endl;
	}

	if (coronaOn_ != TriBool_Unknown) {
		cout << "corona: " << coronaOn_ << endl;
	}

	if (photoIonizationOn_ != TriBool_Unknown) {
		cout << "photoionization: " << photoIonizationOn_ << endl;
	}

	if (sourceCIDOn_ != TriBool_Unknown) {
		cout << "source CID: " << sourceCIDOn_ << endl;
	}

	if (detectorSet_ != TriBool_Unknown) {
		cout << "detector set: " << detectorSet_ << endl;
	}

	if (turboScanOn_ != TriBool_Unknown) {
		cout << "turboscan: " << turboScanOn_ << endl;
	}

    if (enhancedOn_ != TriBool_Unknown) {
        cout << "enhanced mode: " << enhancedOn_ << endl;
    }

	if (dependentActive_ != TriBool_Unknown) {
		cout << "data dependent: " << dependentActive_ << endl;
	}

    if (supplementalCIDOn_ != TriBool_Unknown) {
        cout << "supplemental CID: " << supplementalCIDOn_ << endl;
    }

	if (widebandOn_ != TriBool_Unknown) {
		cout << "wideband: " << widebandOn_ << endl;
	}

	if (faimsOn_ != TriBool_Unknown) {
		cout << "FAIMS: " << faimsOn_ << endl;
	}

	if (accurateMassType_ > AccurateMass_Unknown) {
		cout << "accurate mass: " << accurateMassType_ << endl;
	}

	if (scanType_ > ScanType_Unknown) {
		cout << "scan type: " << scanType_ << endl;
	}

	if (msLevel_ > 0 ) {
		cout << "MS level: " << msLevel_ << endl;
	}

	if (activationType_ > ActivationType_Unknown) {
		cout << "activation type: " << activationType_ << endl;
	}

	cout << endl << endl << endl;
}


void
ScanFilter::initialize()
{
    segment_ = -1;
    event_ = -1;

    massAnalyzerType_ = ScanFilterMassAnalyzerType_Unknown;
    polarityType_ = PolarityType_Unknown;
    dataPointType_ = DataPointType_Unknown;
    ionizationType_ = IonizationType_Unknown;
    scanType_ = ScanType_Unknown;
    accurateMassType_ = AccurateMass_Unknown;
    activationType_ = ActivationType_Unknown;

    coronaOn_ = TriBool_Unknown;
    photoIonizationOn_ = TriBool_Unknown;
    sourceCIDOn_ = TriBool_Unknown;
    detectorSet_ = TriBool_Unknown;
    turboScanOn_ = TriBool_Unknown;
    enhancedOn_ = TriBool_Unknown;
    dependentActive_ = TriBool_Unknown;
    supplementalCIDOn_ = TriBool_Unknown;
    widebandOn_ = TriBool_Unknown;
    lockMassOn_ = TriBool_Unknown;
    faimsOn_ = TriBool_Unknown;
    spsOn_ = TriBool_Unknown;

    msLevel_ = 0;
    precursorMZs_.clear();
	precursorEnergies_.clear();
    saTypes_.clear();
    saEnergies_.clear();
	scanRangeMin_.clear();
	scanRangeMax_.clear();
    compensationVoltage_ = 0.;
    multiplePrecursorMode_ = false;
    constantNeutralLoss_ = false;
    analyzer_scan_offset_ = 0;
}

TriBool parseThermoBool(const boost::ssub_match& m)
{
    if (m.matched)
        return *m.first == '!' ? TriBool_False : TriBool_True;
    return TriBool_Unknown;
}

const static boost::regex filterRegex("^(?<analyzer>FTMS|ITMS|TQMS|SQMS|TOFMS|SECTOR)?\\s*"
                         "(?:\\{(?<segment>\\d+),(?<event>\\d+)\\})?\\s*"
                         "(?<polarity>\\+|-)\\s*"
                         "(?<dataType>p|c)\\s*"
                         "(?<source>EI|CI|FAB|ESI|APCI|NSI|TSP|FD|MALDI|GD)?\\s*"
                         "(?<corona>!corona|corona)?\\s*"
                         "(?<photoIonization>!pi|pi)?\\s*"
                         "(?<sourceCID>!sid|sid=-?\\d+(?:\\.\\d+))?\\s*"
                         "(?<detectorSet>!det|det=\\d+(?:\\.\\d+))?\\s*"
                         "(?:cv=(?<compensationVoltage>-?\\d+(?:\\.\\d+)?))?\\s*"
                         "(?<rapid>!r|r)?\\s*"
                         "(?<turbo>!t|t)?\\s*"
                         "(?<enhanced>!e|e)?\\s*"
                         "(?<sps>SPS|K)?\\s*"
                         "(?<dependent>\\!d|d)?\\s*"
                         "(?<wideband>!w|w)?\\s*"
                         "(?<ultra>!u|u)?\\s*"
                         "(?<supplementalActivation>sa)?\\s*"
                         "(?<accurateMass>!AM|AM|AMI|AME)?\\s*"
                         "(?<scanType>FULL|SIM|SRM|CRM|Z|Q1MS|Q3MS)?\\s*"
                         "(?<lockmass>lock)?\\s*"
                         "(?<multiplex>msx)?\\s*"
                         "(?<msMode>pr|(?:ms(?<msLevel>\\d+)?)|(?:cnl(?<analyzerScanOffset> \\d+(?:\\.\\d+)?)?)?)"
                         "(?:\\s*(?<precursorMz>\\d+(?:\\.\\d+)?)(?<activationGroup>(?:@(?<activationType>cid|hcd|etd)?(?<activationEnergy>-?\\d+(?:\\.\\d+)?))?)(?<saGroup>(?:@(?<saType>cid|hcd)?(?<saEnergy>-?\\d+(?:\\.\\d+)?))?))*\\s*"
                         "(?:\\[(?:(?:, )?(?<scanRangeStart>\\d+(?:\\.\\d+)?)-(?<scanRangeEnd>\\d+(?:\\.\\d+)?))+\\])?$",
                         boost::regex_constants::icase);

void 
ScanFilter::parse(const string& filterLine)
{
    initialize();

    try
    {
        boost::smatch what;
        if (!boost::regex_match(filterLine, what, filterRegex, boost::match_extra))
            throw runtime_error("[ScanFilter::parse()] error parsing \"" + filterLine + "\"");

        massAnalyzerType_ = parseMassAnalyzerType(what["analyzer"]);

        const auto& segment = what["segment"];
        if (segment.matched)
        {
            segment_ = (int)lexical_cast<double>(segment);
            event_ = (int)lexical_cast<double>(what["event"]);
        }

        polarityType_ = parsePolarityType(what["polarity"]);
        dataPointType_ = parseDataPointType(what["dataType"]);
        ionizationType_ = parseIonizationType(what["source"]);

        coronaOn_ = parseThermoBool(what["corona"]);
        photoIonizationOn_ = parseThermoBool(what["photoIonization"]);
        sourceCIDOn_ = parseThermoBool(what["sourceCID"]);
        detectorSet_ = parseThermoBool(what["detectorSet"]);

        turboScanOn_ = parseThermoBool(what["rapid"]);
        if (turboScanOn_ == TriBool_Unknown)
            turboScanOn_ = parseThermoBool(what["turbo"]);

        spsOn_ = what["sps"].matched ? TriBool_True : TriBool_Unknown;

        enhancedOn_ = parseThermoBool(what["enhanced"]);
        dependentActive_ = parseThermoBool(what["dependent"]);
        widebandOn_ = parseThermoBool(what["wideband"]);
        ultraOn_ = parseThermoBool(what["ultra"]);
        supplementalCIDOn_ = parseThermoBool(what["supplementalActivation"]);
        lockMassOn_ = parseThermoBool(what["lockmass"]);
        multiplePrecursorMode_ = what["multiplex"].matched;

        const auto& compensationVoltage = what["compensationVoltage"];
        if (compensationVoltage.matched)
        {
            faimsOn_ = TriBool_True;
            compensationVoltage_ = lexical_cast<double>(compensationVoltage);
        }

        accurateMassType_ = parseAccurateMassType(what["accurateMass"]);

        scanType_ = parseScanType(what["scanType"]);
        if (scanType_ == ScanType_Q1MS || scanType_ == ScanType_Q3MS)
        {
            msLevel_ = 1;
            scanType_ = ScanType_Full;
        }

        const auto& msMode = what["msMode"].str();
        const auto& msLevel = what["msLevel"];
        if (bal::iequals(msMode, "pr"))
        {
            msLevel_ = -1; // special value in lieu of an extra boolean + msLevel=0
        }
        else if (bal::istarts_with(msMode, "ms"))
        {
            if (!msLevel.matched)
                msLevel_ = 1; // just "MS"
            else
                msLevel_ = lexical_cast<int>(msLevel); // take number after "ms"
        }
        else if (bal::istarts_with(msMode, "cnl"))
        {
            msLevel_ = 2;
            constantNeutralLoss_ = true;

            // for CNL expect to find MS_analyzer_scan_offset
            analyzer_scan_offset_ = lexical_cast<double>(bal::trim_copy(what["analyzerScanOffset"].str()));
        }

        // for MS level > 1, expect one or more <isolation m/z>@<activation><energy> tuples (but there may be 0, 1, or 2 @<activation><energy> suffixes)
        if (msLevel_ == -1 || msLevel_ > 1)
        {
            const auto& precursorMZs = what["precursorMz"].captures();
            for (const auto& precursorMz : precursorMZs) precursorMZs_.push_back(lexical_cast<double>(precursorMz));

            // if there are less activationTypes and/or activationEnergies than precursorMZs, check the activationGroups (which will be empty strings if it was missing)
            const auto& activationTypes = what["activationType"].captures();
            if (!activationTypes.empty())
            {
                if (activationTypes.size() < precursorMZs.size())
                {
                    const auto& activationGroups = what["activationGroup"].captures();
                    if (!activationGroups.empty() && activationGroups[0].length() > 0)
                        activationType_ = parseActivationType(activationTypes[0]);  // only use activation type from current (first) precursor
                }
                else
                    activationType_ = parseActivationType(activationTypes[0]); // only use activation type from current (first) precursor
            }

            const auto& activationEnergies = what["activationEnergy"].captures();
            if (activationEnergies.size() < precursorMZs.size())
            {
                precursorEnergies_.resize(precursorMZs.size(), 0); // every precursor must have an energy and it defaults to 0 if not present

                const auto& activationGroups = what["activationGroup"].captures();
                if (precursorMZs.size() != activationGroups.size())
                    throw runtime_error("number of precursorMZs and activationGroups must be the same");

                for (size_t i = 0, j = 0; i < activationEnergies.size() && j < activationGroups.size(); ++i)
                {
                    while (j < activationGroups.size() && activationGroups[j].length() == 0)
                        ++j; // skip to the next precursor with an activation group
                    if (j < activationGroups.size())
                        precursorEnergies_[j] = lexical_cast<double>(activationEnergies[i]);
                }
            }
            else // if every precursor has an energy, life is simpler
            {
                for (const auto& activationEnergy : activationEnergies) precursorEnergies_.push_back(lexical_cast<double>(activationEnergy));
            }

            if (supplementalCIDOn_ == TriBool_True &&
                activationType_ & ActivationType_ETD &&
                !(activationType_ & ActivationType_CID || activationType_ & ActivationType_HCD))
            {
                const auto& saTypes = what["saType"].captures();
                const auto& saEnergies = what["saEnergy"].captures();
                if (saTypes.size() != saEnergies.size())
                    throw runtime_error("number of saType and saEnergy groups must be the same");

                // check for Lumos style supplemental activation filter format: it looks like multiple precursors
                if (msLevel_ == 2 && saTypes.empty() &&
                    (int) precursorMZs_.size() == msLevel_ && precursorMZs_[0] == precursorMZs_[1] &&
                    activationTypes.size() > 1 && precursorEnergies_.size() > 1)
                {
                    saTypes_.resize(1, parseActivationType(activationTypes[1]));
                    saEnergies_.resize(1, precursorEnergies_[1]);
                    precursorMZs_.erase(precursorMZs_.begin() + 1);
                    precursorEnergies_.erase(precursorEnergies_.begin() + 1);
                    activationType_ = static_cast<ActivationType>(activationType_ | saTypes_[0]); // only use SA type from first precursor
                }
                else if (!saTypes.empty())
                {
                    if (saTypes.size() < precursorMZs.size())
                    {
                        saTypes_.resize(precursorMZs.size(), ActivationType_Unknown);
                        saEnergies_.resize(precursorMZs.size(), 0); // every precursor must have an energy and it defaults to 0 if not present

                        const auto& saGroups = what["saGroup"].captures();
                        if (precursorMZs.size() != saGroups.size())
                            throw runtime_error("number of precursorMZs and saGroups must be the same");

                        for (size_t i = 0, j = 0; i < saEnergies_.size() && j < saGroups.size(); ++i)
                        {
                            while (j < saGroups.size() && saGroups[j].length() == 0)
                                ++j; // skip to the next precursor with an SA group
                            if (j < saGroups.size())
                            {
                                saTypes_[j] = parseActivationType(saTypes[i]);
                                saEnergies_[j] = lexical_cast<double>(saEnergies[i]);
                            }
                        }
                    }
                    else // if every precursor has a populated saGroup, life is simpler
                    {
                        for (const auto& saType : saTypes) saTypes_.push_back(parseActivationType(saType));
                        for (const auto& saEnergy : saEnergies) saEnergies_.push_back(lexical_cast<double>(saEnergy));
                    }

                    activationType_ = static_cast<ActivationType>(activationType_ | saTypes_[0]); // only use SA type from current precursor
                }
                
                if (msLevel_ == 2 && saTypes_.empty()) // if sa flag is set on ms2 scan with no saTypes, it's still supplemental CID or HCD
                {
                    // CONSIDER: does detector set always mean CID is really HCD?
                    ActivationType saType;
                    if (detectorSet_ == TriBool_True &&
                        massAnalyzerType_ == ScanFilterMassAnalyzerType_FTMS)
                        saType = ActivationType_HCD;
                    else
                        saType = ActivationType_CID;

                    activationType_ = static_cast<ActivationType>(activationType_ | saType);
                    saTypes_.resize(1, saType);
                    saEnergies_.resize(precursorMZs.size(), 0); // every precursor must have an energy and it defaults to 0 if not present
                }
            }
            else if (detectorSet_ == TriBool_True &&
                     massAnalyzerType_ == ScanFilterMassAnalyzerType_FTMS &&
                     activationType_ == ActivationType_CID)
            {
                activationType_ = ActivationType_HCD;
            }
        }

        const auto& scanRangeStarts = what["scanRangeStart"].captures();
        for (const auto& scanRangeStart : scanRangeStarts) scanRangeMin_.push_back(lexical_cast<double>(scanRangeStart));

        const auto& scanRangeEnds = what["scanRangeEnd"].captures();
        for (const auto& scanRangeEnd : scanRangeEnds) scanRangeMax_.push_back(lexical_cast<double>(scanRangeEnd));
    }
    catch (exception& e)
    {
        throw runtime_error("[ScanFilter::parse()] error parsing \"" + filterLine + "\": " + e.what());
    }
}
