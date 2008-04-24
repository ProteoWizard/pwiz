/*
    File: ScanFilter.h
    Description: parsing for Thermo/Xcalibur "filter line".
    Date: July 25, 2007

    Copyright (C) 2007 Joshua Tasman, ISB Seattle

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

#include <string>
#include <vector>

namespace pwiz {
namespace raw {

enum MassAnalyzerType
{
    MassAnalyzerType_Unknown = -1,
    MassAnalyzerType_ITMS = 0,      // Ion Trap
    MassAnalyzerType_FTMS,          // Fourier Transform
    MassAnalyzerType_TOFMS,         // Time of Flight
    MassAnalyzerType_TQMS,          // Triple Quadrupole
    MassAnalyzerType_SQMS,          // Single Quadrupole
    MassAnalyzerType_Sector,
    MassAnalyzerType_Count
};


inline std::string toString(MassAnalyzerType type)
{
    switch (type)
    {
        case MassAnalyzerType_ITMS: return "ITMS";
        case MassAnalyzerType_FTMS: return "FTMS";
        case MassAnalyzerType_TOFMS: return "TOFMS";
        case MassAnalyzerType_TQMS: return "TQMS";
        case MassAnalyzerType_SQMS: return "SQMS";
        case MassAnalyzerType_Sector: return "Sector";
        case MassAnalyzerType_Unknown: default: return "Unknown";
    }
}

enum IonizationType
{
    IonizationType_Unknown = -1,
    IonizationType_EI = 0,       // Electron Ionization
    IonizationType_CI,           // Chemical Ionization
    IonizationType_FAB,          // Fast Atom Bombardment
    IonizationType_ESI,          // Electrospray Ionization
    IonizationType_NSI,          // Nanospray Ionization
    IonizationType_APCI,         // Atmospheric Pressure Chemical Ionization
    IonizationType_TSP,          // Thermospray
    IonizationType_FD,           // Field Desorption
    IonizationType_MALDI,        // Matrix-assisted Laser Desorption Ionization
    IonizationType_GD,           // Glow Discharge
    IonizationType_Count
};

inline std::string toString(IonizationType type)
{
    switch (type)
    {
        case IonizationType_EI: return "Electron Impact";
        case IonizationType_CI: return "Chemical Ionization";
        case IonizationType_FAB: return "Fast Atom Bombardment";
        case IonizationType_ESI: return "Electrospray Ionization";
        case IonizationType_NSI: return "Nanospray Ionization";
        case IonizationType_APCI: return "Atmospheric Pressure Chemical Ionization";
        case IonizationType_TSP: return "Thermospray";
        case IonizationType_FD: return "Field Desorption";
        case IonizationType_MALDI: return "Matrix-assisted Laser Desorption Ionization";
        case IonizationType_GD: return "Glow Discharge";
        case IonizationType_Unknown: default: return "Unknown";
    }
}

enum ActivationType
{
    ActivationType_Unknown = -1,
    ActivationType_CID = 0,     // Collision Induced Dissociation
    ActivationType_ETD,         // Electron Transfer Dissociation
    ActivationType_ECD,         // Electron Capture Dissociation
    ActivationType_MPD,         // TODO: what is this?
    ActivationType_PQD,         // Pulsed Q Dissociation
    ActivationType_HCD,         // High Energy CID
    ActivationType_SA,          // Supplemental CID
    ActivationType_PTR,         // Proton Transfer Reaction
    ActivationType_Count
};

inline std::string toString(ActivationType type)
{
    switch (type)
    {
        case ActivationType_CID: return "Collision Induced Dissociation";
        case ActivationType_ETD: return "Electron Transfer Dissociation";
        case ActivationType_ECD: return "Electron Capture Dissociation";
        case ActivationType_MPD: return "MPD"; // TODO: what is this?
        case ActivationType_PQD: return "Pulsed Q Dissociation";
        case ActivationType_HCD: return "High Energy CID";
        case ActivationType_SA: return "Supplemental CID";
        case ActivationType_PTR: return "Proton Transfer Reaction";
        case ActivationType_Unknown: default: return "Unknown";
    }
}

enum ScanType
{
    ScanType_Unknown = -1,
    ScanType_Full = 0,
    ScanType_SIM,
    ScanType_SRM,
    ScanType_CRM,
    ScanType_Q1MS,
    ScanType_Q3MS,
    ScanType_Zoom,
    ScanType_Count
};

inline std::string toString(ScanType type)
{
    switch (type)
    {
        case ScanType_Full: return "Full";
        case ScanType_SIM: return "Single ion monitoring";
        case ScanType_SRM: return "Single reaction monitoring";
        case ScanType_CRM: return "Constant reaction monitoring";
        case ScanType_Q1MS: return "Q1MS";
        case ScanType_Q3MS: return "Q3MS";
        case ScanType_Zoom: return "Zoom";
        case ScanType_Unknown: default: return "Unknown";
    }
}

enum PolarityType
{
    PolarityType_Unknown = -1,
    PolarityType_Positive = 0,
    PolarityType_Negative,
    PolarityType_Count
};

inline std::string toString(PolarityType type)
{
    switch (type)
    {
        case PolarityType_Positive: return "+";
        case PolarityType_Negative: return "-";
        case PolarityType_Unknown: default: return "Unknown";
    }
}

enum DataPointType
{
	DataPointType_Unknown = -1,
	DataPointType_Centroid = 0,
	DataPointType_Profile,
    DataPointType_Count
};

enum AccurateMassType
{
	AccurateMass_Unknown = -1,
	AccurateMass_NotActive = 0,                 // NOTE: in filter as "!AM": accurate mass not active
	AccurateMass_Active,                        // accurate mass active 
	AccurateMass_ActiveWithInternalCalibration, // accurate mass with internal calibration
	AccurateMass_ActiveWithExternalCalibration // accurate mass with external calibration
};

enum TriBool
{
	TriBool_Unknown = -1,
	TriBool_False = 0,
	TriBool_True = 1
};

class ScanFilter
{
    public:

	


	MassAnalyzerType parseMassAnalyzerType(const std::string& word);
	PolarityType parsePolarityType(const std::string& word);
	DataPointType parseDataPointType(const std::string& word);
	IonizationType parseIonizationType(const std::string & word);
	ScanType parseScanType(const std::string& word);
	ActivationType parseActivationType(const std::string& word);
	AccurateMassType parseAccurateMassType(const std::string& word);

	MassAnalyzerType massAnalyzerType_;
	PolarityType polarityType_;
	DataPointType dataPointType_;
	IonizationType ionizationType_;
	TriBool coronaOn_;
	TriBool photoIonizationOn_;
	TriBool sourceCIDOn_;
	TriBool detectorSet_;
	TriBool turboScanOn_;
	TriBool dependentActive_; // t: data-dependent active; f: non active
	TriBool widebandOn_; // wideband activation
	AccurateMassType accurateMassType_;
	ScanType scanType_;
	int msLevel_; // n, in MSn: >0
	ActivationType activationType_;

	std::vector<double> cidParentMass_; // one entry per ms level for level >= 2
	std::vector<double> cidEnergy_; // relative units; one entry per ms level for level >= 2

	std::vector<double> scanRangeMin_;
	std::vector<double> scanRangeMax_;


	ScanFilter();
	~ScanFilter();

	void print();

    void initialize();
	bool parse(std::string filterLine);

};

} // namespace raw
} // namespace pwiz

#endif // _SCANFILTER_H_
