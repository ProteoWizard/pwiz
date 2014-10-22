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
	if (word == "C")
		return DataPointType_Centroid;
	else if (word == "P")
		return DataPointType_Profile;
	else
		return DataPointType_Unknown;
}

IonizationType 
ScanFilter::parseIonizationType(const string & word)
{
	if (word == "EI" )
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
  if (word.find("CV=") == string::npos)
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
	if (word == "FULL")
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
	if (word == "CID")
		return ActivationType_CID;
	else if (word == "MPD")
		return ActivationType_MPD;
	else if (word == "ECD")
		return ActivationType_ECD;
	else if (word == "PQD")
		return ActivationType_PQD;
	else if (word == "ETD")
		return ActivationType_ETD;
	else if (word == "HCD")
		return ActivationType_HCD;
	else if (word == "PTR")
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
	scanRangeMin_.clear();
	scanRangeMax_.clear();
    compensationVoltage_ = 0.;
    multiplePrecursorMode_ = false;
    constantNeutralLoss_ = false;
    analyzer_scan_offset_ = 0;
}


void 
ScanFilter::parse(const string& filterLine)
{
    initialize();

    try
    {

	    // almost all of the fields are optional
        string filterLineCopy(filterLine);
	    boost::to_upper(filterLineCopy);
	    stringstream s(filterLineCopy);
	    string w;

	    if (s.eof())
		    return; // ok, empty line

	    s >> w;

	    massAnalyzerType_ = parseMassAnalyzerType(w);
	    if (massAnalyzerType_ > ScanFilterMassAnalyzerType_Unknown) {
		    // "analyzer" field was present
		    if (s.eof())
			    return;

		    s >> w;
	    }

        // read Exactive scan segment and scan event
        if (*w.begin() == '{' && *w.rbegin() == '}')
        {
            boost::trim_if(w, boost::is_any_of("{ }")); // trim flanking brackets and whitespace
	        vector<string> segmentEventPair;
	        boost::split(segmentEventPair, w, boost::is_any_of(","));
		    segment_ = (int)lexical_cast<double>(segmentEventPair[0]);
		    event_ = (int)lexical_cast<double>(segmentEventPair[1]);
		    s >> w;
	    }

	    polarityType_ = parsePolarityType(w);
	    if (polarityType_ > PolarityType_Unknown) {
		    // "polarity" field was present
		    if (s.eof())
			    return;

		    s >> w;
	    }

	    dataPointType_ = parseDataPointType(w);
	    if (dataPointType_ > DataPointType_Unknown) {
		    // "scan data type" field present
		    if (s.eof())
			    return;

		    s >> w;
	    }

	    ionizationType_ = parseIonizationType(w);
	    if (ionizationType_ > IonizationType_Unknown) {
		    // "ionization mode" field present
		    if (s.eof())
			    return;

		    s >> w;
	    }

	    bool advance = false;

	    // corona
	    if (w == "!CORONA") {
		    coronaOn_ = TriBool_False;
		    advance = true;
	    }
	    else if (w == "CORONA") {
		    coronaOn_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }

	    // photoIonization
	    if (w == "!PI") {
		    photoIonizationOn_ = TriBool_False;
		    advance = true;
	    }
	    else if (w == "PI") {
		    photoIonizationOn_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }

	    // source CID
	    if (w == "!SID") {
		    sourceCIDOn_ = TriBool_False;
		    advance = true;
	    }
	    else if (w.find("SID") == 0) { // handle cases where SID energy is explicit
		    sourceCIDOn_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }


	    // detector
	    if (w == "!DET") {
		    detectorSet_ = TriBool_False;
		    advance = true;
	    }
        else if (bal::starts_with(w, "DET")) {
		    detectorSet_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }


        faimsOn_ = parseCompensationVoltage(w, compensationVoltage_) ? TriBool_True : TriBool_Unknown;

        if (faimsOn_ == TriBool_True)
            s >> w;


	    // rapid
	    if (w == "R") {
		    turboScanOn_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }


	    // turboscan
	    if (w == "!T") {
		    turboScanOn_ = TriBool_False;
		    advance = true;
	    }
	    else if (w == "T") {
		    turboScanOn_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }


        // enhanced mode
	    if (w == "!E") {
		    enhancedOn_ = TriBool_False;
		    advance = true;
	    }
	    else if (w == "E") {
		    enhancedOn_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
        }


        // SPS mode
        if (w == "SPS" || w == "K") {
            spsOn_ = TriBool_True;
            advance = true;
        }
        if (advance) {
            if (s.eof())
                return;

            s >> w;
            advance = false;
        }


	    // dependent type
	    if (w == "!D") {
		    dependentActive_ = TriBool_False;
		    advance = true;
	    }
	    else if (w == "D") {
		    dependentActive_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }

	    // wideband
	    if (w == "!W") {
		    widebandOn_ = TriBool_False;
		    advance = true;
	    }
	    else if (w == "W") {
		    widebandOn_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }

	    // ultra mode
	    if (w == "!U") {
		    ultraOn_ = TriBool_False;
		    advance = true;
	    }
	    else if (w == "U") {
            ultraOn_ = TriBool_True;
		    advance = true;
	    }
	    if (advance) {
		    if (s.eof())
			    return;

		    s >> w;
		    advance = false;
	    }

        // supplemental CID activation
        if (w == "SA") {
            supplementalCIDOn_ = TriBool_True;
            advance = true;
        }
        if (advance) {
            if (s.eof())
                return;

            s >> w;
            advance = false;
        }

	    accurateMassType_ = parseAccurateMassType(w);
	    if (accurateMassType_ > AccurateMass_Unknown) {
		    // "accurate mass" field present
		    if (s.eof())
			    return;

		    s >> w;
	    }

	    scanType_ = parseScanType(w);
	    if (scanType_ > ScanType_Unknown) {
		    if (scanType_ == ScanType_Q1MS || scanType_ == ScanType_Q3MS)
            {
			    msLevel_ = 1;
                scanType_ = ScanType_Full;
            }

		    // "scan type" field present
		    if (s.eof())
			    return;

            s >> w;
	    }

        if (w == "LOCK")
        {
            lockMassOn_ = TriBool_True;

            if (s.eof())
                return;

            s >> w;
        }

        if (w == "MSX")
        {
            multiplePrecursorMode_ = true;

            if (s.eof())
                return;

            s >> w;
        }


        // MS order or PR keyword
        if (w == "PR")
        {
            msLevel_ = -1; // special value in lieu of an extra boolean + msLevel=0

            if (s.eof())
                return;

            s >> w;
        }
        else if (bal::starts_with(w, "MS"))
        {
            if (w.length() == 2) {
                msLevel_ = 1; // just "MS"
            } else {
                // MSn: extract int n
                w.erase(0, 2);
                msLevel_ = lexical_cast<int>(w); // take number after "ms"
            }
            if (s.eof())
                return;

            s >> w;
        }
        else if (bal::starts_with(w, "CNL"))
        {
            msLevel_ = 2; 
            constantNeutralLoss_ = true;
            if (s.eof())
                return;
            s >> w;
        }

        // for CNL expect to find MS_analyzer_scan_offset
        if (constantNeutralLoss_)
        {
            analyzer_scan_offset_ = lexical_cast<double>(w);
            s >> w;
        }
	    // for MS level > 1, expect one or more <isolation m/z>@<activation><energy> tuples
	    else if (msLevel_ == -1 || msLevel_ > 1)
        {
            // if the marker is found, expect a <isolation m/z>@<activation><energy> tuple
		    size_t markerPos = w.find('@');

            if (markerPos == string::npos)
            {
                precursorMZs_.push_back(lexical_cast<double>(w));
                precursorEnergies_.push_back(0.0);

	            if (s.eof())
		            return;

	            s >> w;
            }
            else
            {
                do
                {
		            char c=w[0];
		            if (!(c >= '0' && c <= '9'))
			            throw runtime_error("don't know how to parse \"" + w + "\"");

	                size_t energyPos = markerPos+1;
	                c = w[energyPos];
	                if ((c < '0' || c > '9') && c != '-' && c != '+')
                    {
		                energyPos = w.find_first_of("0123456789-+", energyPos); // find first numeric character after the "@"
		                if (energyPos != string::npos) {

                            ActivationType currentType = parseActivationType(w.substr(markerPos+1, energyPos-markerPos-1));

                            // CONSIDER: does detector set always mean CID is really HCD?
                            if (detectorSet_ == TriBool_True &&
                                massAnalyzerType_ == ScanFilterMassAnalyzerType_FTMS &&
                                currentType == ActivationType_CID)
                                currentType = ActivationType_HCD;

                            // only parse the left-most activation type because that corresponds to the current MS level
                            if (activationType_ == ActivationType_Unknown)
                            {
                                activationType_ = currentType;
                                if (supplementalCIDOn_ == TriBool_True)
                                    activationType_ = static_cast<ActivationType>(activationType_ | ActivationType_CID);
                                if (activationType_ == ActivationType_Unknown)
                                    throw runtime_error("failed to parse activation type");
                            }
		                } else
			                throw runtime_error("failed to find activation energy");
	                }

	                string mass = w.substr(0, markerPos);
	                string energy = w.substr(energyPos);
	                // cout << "got mass " << mass << " at " << energy << " energy using activation " << (int) activationMethod_ << " (from " << w << ")" << endl;
	                precursorMZs_.push_back(lexical_cast<double>(mass));
	                precursorEnergies_.push_back(lexical_cast<double>(energy));

	                if (s.eof())
		                return;

	                s >> w;

                    markerPos = w.find('@');
                } while (markerPos != string::npos);
            }
	    }

	    // try to get activation type if not already set
	    if (activationType_ == ActivationType_Unknown) {
		    activationType_ = parseActivationType(w);

            // CONSIDER: does detector set always mean CID is really HCD?
            if (detectorSet_ == TriBool_True &&
                massAnalyzerType_ == ScanFilterMassAnalyzerType_FTMS &&
                activationType_ == ActivationType_CID)
                activationType_ = ActivationType_HCD;
		    else if (activationType_ > ActivationType_Unknown) {
			    // "activation type" field present
			    if (s.eof())
				    return;

			    s >> w;
		    }
	    }


	    // product masses or mass ranges
	    // TODO: parse single values, for SIM, SRM, CRM
	    // some test based on ms level?

	    string w2;
	    std::getline(s, w2); // get all tokens until closing bracket
	    w.append(w2);
	    boost::trim_if(w, boost::is_any_of("[ ]")); // trim flanking brackets and whitespace
	    vector<string> massRangeStrs;
	    boost::split(massRangeStrs, w, boost::is_any_of(","));
	    for(size_t i=0; i < massRangeStrs.size(); ++i)
	    {
		    string& massRangeStr = massRangeStrs[i]; // "<rangeMin>-<rangeMax>"
		    boost::trim(massRangeStr); // trim flanking whitespace
		    vector<string> rangeMinMaxStrs;
		    boost::split(rangeMinMaxStrs, massRangeStr, boost::is_any_of("-"));
		    scanRangeMin_.push_back(lexical_cast<double>(rangeMinMaxStrs[0]));
		    scanRangeMax_.push_back(lexical_cast<double>(rangeMinMaxStrs[1]));
	    }

	    if (s.eof())
		    return;
	    else {
            ostringstream oss("unparsed scan filter elements: ");
		    do {
			    oss << w << endl;
		    } while (s >> w);
            throw runtime_error(oss.str());
	    }
    }
    catch (exception& e)
    {
        throw runtime_error("[ScanFilter::parse()] error parsing \"" + filterLine + "\": " + e.what());
    }
}
