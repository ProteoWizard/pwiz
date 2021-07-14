//
// $Id$
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
// The Original Code is greazy.
//
// The Initial Developer of the Original Code is Mike Kochen.
//
// Copyright 2015 Vanderbilt University
//
// Contributor(s):
//

#include "freicore.h"
#include "pwiz/data/msdata/MSDataFile.hpp"
#include "pwiz/data/vendor_readers/ExtendedReaderList.hpp"
#include <iostream>
#include <map>
#include <fstream>
#include <string>
#include <sstream>
#include <vector>
#include <cmath>
#include <ctime>
#include <algorithm>
#include <boost/filesystem.hpp>

using namespace pwiz::msdata;

// atomic masses of relevant elements
double H = 1.007825;
double Li = 7.016005;
double C = 12;
double N = 14.003074;
double O = 15.994915;
double Na = 22.989770;
double P = 30.973763;
double Cl = 34.968853;
double K = 38.963708;

// Holds adduct information
struct Adduct {
    string name;
    string description;
    string type;
    double mass;
    string polarity;
};

// Holds head-group information
struct HeadGroup {
    string name;
    string common;
    string formula;
    double mass;
    double search;
};

// Holds backbone information
struct BackBone {
    string name;
    string formula;
    double mass;
    double search;
    int length;
    int ca;
    int hy;
    int ni;
    int ox;
    int ph;
};

// Holds fragment information
struct Fragment {
    string fragType;
    string fragDescription;
    double fragmentMass;
};

typedef vector<Fragment> Fragments; 

// Holds precursor information
struct PreCursor {
    string description;
    string adduct;
    double adductMass;
    string adductType;
    string ESI;
    double precursorMass;
    Fragments fragments;
};

typedef vector<PreCursor> PreCursors;

// Holds lipid information
struct Lipid {
    string name;
    bool decoy;
    double totalMass;
    string lipidFormula;
    string backbone;
    string backboneFormula;
    double backboneMass;
    string headGroup;
    string headGroupFormula;
    double headGroupMass;
    string FA1;
    double FA1Mass;
    double FA1DoubleBonds;
    string SN1bond;
    string FA2;
    double FA2Mass;
    double FA2DoubleBonds;
    string SN2bond;
    string FA3;
    double FA3Mass;
    double FA3DoubleBonds;
    string SN3bond;
    string FA4;
    double FA4Mass;
    double FA4DoubleBonds;
    string SN4bond;
    PreCursors precursors;
};

struct SpectraHolder {
    double preMassN;
    double preMassP;
    int spectraIndex;
    double retentionTime;
};

// variable shorthand
typedef map<string, double> Map;
typedef vector<HeadGroup> HeadGroups;
typedef vector<BackBone> BackBones;

struct Match {
    double mz;
    double intensity;
    bool match;
    int peakIndex;
    string fragmentD;
    string fragmentT;
};

struct Score {
    int spectrumNumber;
    bool decoy;
    double spectrumPrecursorMass;
    string lipidName;
    string chemFormula;
    double lipidMass;
    string precursorDescription;
    double precursorMass;
    double peakScore;
    double intensityScore;
    double totalScore;
    double retTime;
    string charge;
    string mod;
    vector<Match> match;
    vector<Match> ms2list;
};

struct Secondary {
    vector<double> PC;
    vector<double> PE;
    vector<double> PI;
    vector<double> PS;
    vector<double> PG;
    vector<double> PA;
    vector<double> CL;
    vector<double> CL2;
    vector<double> SM;
    vector<double> PIP;
    vector<double> PIP2;
    vector<double> MPC;
    vector<double> MPE;
    vector<double> MPI;
    vector<double> MPS;
    vector<double> MPG;
    vector<double> MPA;
    vector<double> MCL;
    //vector<double> M3CL;
    vector<double> MSM;
    vector<double> MPIP;
    vector<double> PEC;
    vector<double> PIC;
    vector<double> MPEC;
    vector<double> MPIC;
};

// Parameter map from configuration file
Map processConfig(const string & fname);

// lipid count 
void lipidCount(vector<Adduct> adducts, Map & paraMap, const HeadGroups & head, const BackBones & back, const HeadGroups & sphingoHead, const HeadGroups & cardioHead, double* count, int* pre);

// precursor count
void precursorCount(vector<Adduct> adducts, Lipid lipid, Map & paraMap, int* pre);

// GP list construction 
void lipidConstructionAndScoring(vector<Adduct> adducts, Map & paraMap, const HeadGroups & head, const BackBones & back, const HeadGroups & sphingoHead, const HeadGroups & cardioHead, vector<SpectraHolder> spectraHolder, const SpectrumListPtr& SL, double* count, int* pre);

// precursor list construction
void precursorListConstruction(vector<Adduct> adducts, Lipid lipid, Map & paraMap, vector<SpectraHolder> spectraHolder, const SpectrumListPtr& SL, int* pre);

// fragment list construction
void fragmentListConstruction(Lipid lipid, Map & paraMap, vector<SpectraHolder> spectraHolder, const SpectrumListPtr& SL);

// scoring algorithm
void scoring(Lipid lipid, Map & paraMap, vector<SpectraHolder> spectraHolder, const SpectrumListPtr& SL);

// various sorting functions
bool sortByIntensity(const MZIntensityPair &lhs, const MZIntensityPair &rhs) 
{
    return lhs.intensity > rhs.intensity;
}
bool sortByMZr(const Match &lhs, const Match &rhs) 
{
    return lhs.mz < rhs.mz;
}
bool sortByTotalScore(const Score &lhs, const Score &rhs)
{
    return (lhs.totalScore > rhs.totalScore) || ((lhs.totalScore == rhs.totalScore) && (lhs.intensityScore > rhs.intensityScore));
}
bool sortByName(const Score &lhs, const Score &rhs)
{
    return lhs.lipidName < rhs.lipidName;
}
vector<vector<Score> > spec, spec2;
vector<Secondary> secondary;
vector<vector<double> > firstSecondFull;
int lipCount;
bool sortByHighScore(const vector<Score> &lhs, const vector<Score> &rhs)
{
    return lhs[0].totalScore > rhs[0].totalScore;
}
bool sortBySpecMZ(const SpectraHolder &lhs, const SpectraHolder &rhs) 
{
    return lhs.preMassN < rhs.preMassN;
}

// binary search algorithm
int binarySearch(vector<SpectraHolder> spectraHolder, double mass, int min, int max)
{
    int mid;
    if ((max-min) > 1)
    {
        mid = (max+min)/2;
        if (mass > spectraHolder[mid].preMassN)
        {
            return binarySearch(spectraHolder, mass, mid, max);
        }
        else
        {
            return binarySearch(spectraHolder, mass, min, mid);
        }
    }
    else
    {
        if (mass > max)
        {
            return max;
        }
        else
        {
            return min;
        }
    }
}

 //initialize vectors containing CL combinations
 vector<vector<size_t> > CLspecies;

typedef vector<Match> match;
// combinatorial function for computing intensity score
void combo (int index, double totalMatchedIntensity, double intensity, int matchNum, match & ms2list, int* num)
{     
    if (index + 1 < matchNum)
    {
        for (size_t i=index; i<ms2list.size(); i++)
        {
            combo(i+1, totalMatchedIntensity, intensity + ms2list[i].intensity, matchNum, ms2list, num);
        }
    }
    else
    {
        for (size_t i=index; i<ms2list.size(); i++)
        {
            if (intensity + ms2list[i].intensity + 0.000000000001 >= totalMatchedIntensity)
            {
                (*num)++;
            }
        }
    }
}

// initialize factorials
vector<double> factorials(10000000);

 /////////// CLEANED TO HERE //////////////////////////////////////////////////////////////////
int main(int argc, const char* argv[])
{
    /*
    time_t now;
    struct tm *current;
    now = time(0);
    current = localtime(&now);
    cout << "hour:" << current->tm_hour << " min:" << current->tm_min << " sec:" << current->tm_sec << endl;
    */

    if (argc < 2)
    {
        cout << "Please choose a data set." << endl; // give accepted formats
        cout << "Press ENTER to continue...";
        cin.get();
        return 1;
    }

    string help = argv[1];
    if (help == "-h" || help == "-help")
    {
        cout << "help me!!!!" << endl;
        cin.get();
        return 1;
    }

    if (!bfs::exists(argv[1]))
    {
        cout << "Please choose a data set." << endl; // give accepted formats
        cout << "Press ENTER to continue...";
        cin.get();
        return 1;
    }

    
    cout << "Greazy: Phospholipid Identification" << endl << endl;
    Map paraMap = processConfig("lipidConfig.txt");

    // adduct information
    vector<Adduct> adducts;
    if (paraMap["Add.Pro"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "+H";
        adducts[adducts.size()-1].description = "protonated";
        adducts[adducts.size()-1].type = "nonMetal";
        adducts[adducts.size()-1].mass = H;
        adducts[adducts.size()-1].polarity = "positive";
    }

    if (paraMap["Add.Na"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "+Na";
        adducts[adducts.size()-1].description = "Sodium adduct";
        adducts[adducts.size()-1].type = "Metal";
        adducts[adducts.size()-1].mass = Na;
        adducts[adducts.size()-1].polarity = "positive";
    }

    if (paraMap["Add.NH4"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "+NH4";
        adducts[adducts.size()-1].description = "Ammonium adduct";
        adducts[adducts.size()-1].type = "nonMetal";
        adducts[adducts.size()-1].mass = N + 4*H;
        adducts[adducts.size()-1].polarity = "positive";
    }

    if (paraMap["Add.Li"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "+Li";
        adducts[adducts.size()-1].description = "Lithium adduct";
        adducts[adducts.size()-1].type = "Metal";
        adducts[adducts.size()-1].mass = Li;
        adducts[adducts.size()-1].polarity = "positive";
    }

    if (paraMap["Add.K"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "+K";
        adducts[adducts.size()-1].description = "Potassium adduct";
        adducts[adducts.size()-1].type = "Metal";
        adducts[adducts.size()-1].mass = K;
        adducts[adducts.size()-1].polarity = "positive";
    }

    if (paraMap["Add.Depro"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "Deprotonated";
        adducts[adducts.size()-1].description = "Deprotonated";
        adducts[adducts.size()-1].type = "nonMetal";
        adducts[adducts.size()-1].mass = -H;
        adducts[adducts.size()-1].polarity = "negative";
    }

    if (paraMap["Add.Cl"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "Cl";
        adducts[adducts.size()-1].description = "Chlorine adduct";
        adducts[adducts.size()-1].type = "nonMetal";
        adducts[adducts.size()-1].mass = Cl;
        adducts[adducts.size()-1].polarity = "negative";
    }

    if (paraMap["Add.HCOO"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "Formate";
        adducts[adducts.size()-1].description = "Formate adduct";
        adducts[adducts.size()-1].type = "nonMetal";
        adducts[adducts.size()-1].mass = C + H + 2*O;
        adducts[adducts.size()-1].polarity = "negative";
    }

    if (paraMap["Add.CH3COO"] == 1)
    {
        adducts.push_back(Adduct());
        adducts[adducts.size()-1].name = "Acetate";
        adducts[adducts.size()-1].description = "Acetate adduct";
        adducts[adducts.size()-1].type = "nonMetal";
        adducts[adducts.size()-1].mass = 2*C + 2*O + 3*H;
        adducts[adducts.size()-1].polarity = "negative";
    }

    vector<string> modifications;
    if ((paraMap["Add.Pro"] == 1) && (paraMap["ESI.pos"] == 1))
    {
        modifications.push_back("+H");
    }

    if ((paraMap["Add.Na"] == 1) && (paraMap["ESI.pos"] == 1))
    {
        modifications.push_back("+Na");
    }

    if ((paraMap["CL"] == 1) && (paraMap["Add.Na"] == 1) && (paraMap["ESI.neg"] == 1))
    {
        modifications.push_back("-2H + Na");
    }

    if ((paraMap["Add.NH4"] == 1) && (paraMap["ESI.pos"] == 1))
    {
        modifications.push_back("+NH4");
    }

    if ((paraMap["Add.Li"] == 1) && (paraMap["ESI.pos"] == 1))
    {
        modifications.push_back("+Li");
    }

    if ((paraMap["Add.K"] == 1) && (paraMap["ESI.pos"] == 1))
    {
        modifications.push_back("+K");
    }

    if ((paraMap["Add.Depro"] == 1) && (paraMap["ESI.neg"] == 1))
    {
        modifications.push_back("-H");
    }

    if ((paraMap["Add.Cl"] == 1) && (paraMap["ESI.neg"] == 1))
    {
        modifications.push_back("+Cl");
    }

    if ((paraMap["Add.HCOO"] == 1) && (paraMap["ESI.neg"] == 1))
    {
        modifications.push_back("+HCOO");
    }

    if ((paraMap["Add.CH3COO"] == 1) && (paraMap["ESI.neg"] == 1))
    {
        modifications.push_back("+CH3COO");
    }

    if ((paraMap["GP.Cholines"] == 1) && (paraMap["ESI.neg"] == 1) && (paraMap["Add.Cl"] == 1 || paraMap["Add.CH3COO"] == 1 || paraMap["Add.HCOO"] == 1))
    {
        modifications.push_back("-CH3");
    }

    if (((paraMap["GP"] == 1) && (paraMap["GP.InositolPhosphates"] == 1) && (paraMap["ESI.neg"] == 1) ) || ((paraMap["CL"] == 1) && (paraMap["ESI.neg"] == 1) && (paraMap["CL.double"] == 1)))
    {
        modifications.push_back("-2H");
    }

    // backbone information
    int sphingoBack = (paraMap["SP.UpperBack"] - paraMap["SP.LowerBack"] + 1)*(paraMap["SP.Sphingosine"] + paraMap["SP.Sphinganine"] + paraMap["SP.Phytosphingosine"] + paraMap["SP.Sphingadienine"]) + 2;
    BackBones back (sphingoBack);

    back[0].name = "Glycerol";
    back[0].formula = "C3H5O3";
    back[0].mass = 3*C + 5*H + 3*O;

    back[1].name = "Cardiolipin";
    back[1].formula = "C9H17O13P2";
    back[1].mass = 9*C + 17*H + 13*O + 2*P;
    
    int backIndex = 2;
    for (size_t i=paraMap["SP.LowerBack"]; i<paraMap["SP.UpperBack"]+1; i++)
    {
        if (paraMap["SP.Sphingosine"])
        {
            stringstream name;
            name << "(C" << i << ")Sphingosine";
            back[backIndex].name = name.str();
            back[backIndex].formula = "Sphingosine";
            back[backIndex].mass = i*C + (2*i - 1)*H + 2*O + N;
            back[backIndex].ca = i;
            back[backIndex].hy = 2*i - 1;
            back[backIndex].ox = 2;
            back[backIndex].ni = 1;
            back[backIndex].search = paraMap["SP.Sphingosine"];
            back[backIndex].length = i;
            backIndex++;
        }
        
        if (paraMap["SP.Sphinganine"])
        {
            stringstream name;
            name << "(C" << i << ")Sphinganine";
            back[backIndex].name = name.str();
            back[backIndex].formula = "Sphinganine";
            back[backIndex].mass = i*C + (2*i + 1)*H + 2*O + N;
            back[backIndex].ca = i;
            back[backIndex].hy = 2*i + 1;
            back[backIndex].ox = 2;
            back[backIndex].ni = 1;
            back[backIndex].search = paraMap["SP.Sphinganine"];
            back[backIndex].length = i;
            backIndex++;
        }

        if (paraMap["SP.Phytosphingosine"])
        {
            stringstream name;
            name << "(C" << i << ")Phytosphingosine";
            back[backIndex].name = name.str();
            back[backIndex].formula = "Phytosphingosine";
            back[backIndex].mass = i*C + (2*i + 1)*H + 3*O + N;
            back[backIndex].ca = i;
            back[backIndex].hy = 2*i + 1;
            back[backIndex].ox = 3;
            back[backIndex].ni = 1;
            back[backIndex].search = paraMap["SP.Phytosphingosine"];
            back[backIndex].length = i;
            backIndex++;
        }

        if (paraMap["SP.Sphingadienine"])
        {
            stringstream name;
            name << "(C" << i << ")Sphingadienine";
            back[backIndex].name = name.str();
            back[backIndex].formula = "Sphingadienine";
            back[backIndex].mass = i*C + (2*i - 3)*H + 2*O + N;
            back[backIndex].ca = i;
            back[backIndex].hy = 2*i - 3;
            back[backIndex].ox = 2;
            back[backIndex].ni = 1;
            back[backIndex].search = paraMap["SP.Sphingadienine"];
            back[backIndex].length = i;
            backIndex++;
        }
    }
     
    // vector of structs holding glycerophospholipid head-group information
    HeadGroups head (9);

    head[0].name = "Choline";
    head[0].common = "PC";
    head[0].formula = "C5H13O3PN";
    head[0].mass = 5*C + 13*H + 3*O + P + N;
    head[0].search = paraMap["GP.Cholines"];

    head[1].name = "Ethanolamine";
    head[1].common = "PE";
    head[1].formula = "C2H7O3PN";
    head[1].mass = 2*C + 7*H + 3*O + P + N;
    head[1].search = paraMap["GP.Ethanolamines"];
    
    head[2].name = "Serine";
    head[2].common = "PS";
    head[2].formula = "C3H7O5PN";
    head[2].mass = 3*C + 7*H + 5*O + P + N;
    head[2].search = paraMap["GP.Serines"];

    head[3].name = "Glycerol";
    head[3].common = "PG";
    head[3].formula = "C3H8O5P";
    head[3].mass = 3*C + 8*H + 5*O + P;
    head[3].search = paraMap["GP.Glycerols"];

    head[4].name = "Inositol";
    head[4].common = "PI";
    head[4].formula = "C6H12O8P";
    head[4].mass = 6*C + 12*H + 8*O + P;
    head[4].search = paraMap["GP.Inositols"];

    head[5].name = "Phosphate";
    head[5].common = "PA";
    head[5].formula = "H2O3P";
    head[5].mass = 2*H + 3*O + P;
    head[5].search = paraMap["GP.Phosphates"];
    
    head[6].name = "PIP";
    head[6].common = "PIP";
    head[6].formula = "C6H13O11P2";
    head[6].mass = 6*C + 13*H + 11*O + 2*P;
    head[6].search = paraMap["GP.InositolPhosphates"];
    
    head[7].name = "PIP2";
    head[7].common = "PIP2";
    head[7].formula = "C6H14O14P3";
    head[7].mass = 6*C + 14*H + 14*O + 3*P;
    head[7].search = paraMap["GP.InositolPhosphates"];
    
    head[8].name = "PIP3";
    head[8].common = "PIP3";
    head[8].formula = "C6H15O17P4";
    head[8].mass = 6*C + 15*H + 17*O + 4*P;
    head[8].search = paraMap["GP.InositolPhosphates"];
    
    // vector of structs holding sphingolipid head-group information
    HeadGroups sphingoHead (3);

    sphingoHead[0].name = "Choline";
    sphingoHead[0].common = "SM";
    sphingoHead[0].formula = "C5H13O3PN";
    sphingoHead[0].mass = 5*C + 13*H + 3*O + P + N;
    sphingoHead[0].search = paraMap["SP.Cholines"];

    sphingoHead[1].name = "Ethanolamine";
    sphingoHead[1].common = "PE-Cer";
    sphingoHead[1].formula = "C2H7O3PN";
    sphingoHead[1].mass = 2*C + 7*H + 3*O + P + N;
    sphingoHead[1].search = paraMap["SP.Ethanolamines"];

    sphingoHead[2].name = "Inositol";
    sphingoHead[2].common = "PI-Cer";
    sphingoHead[2].formula = "C6H12O8P";
    sphingoHead[2].mass = 6*C + 12*H + 8*O + P;
    sphingoHead[2].search = paraMap["SP.Inositols"];

    // vector of structs holding cardiolipin head-group information
    HeadGroups cardioHead (2);

    cardioHead[0].name = "None";
    cardioHead[0].common = "";
    cardioHead[0].formula = "H";
    cardioHead[0].mass = H;
    cardioHead[0].search = paraMap["CL.None"];

    /*
    cardioHead[1].name = "Glucose";
    cardioHead[1].common = "Glucosyl";
    cardioHead[1].formula = "C6H11O5";
    cardioHead[1].mass = 6*C + 11*H + 5*O;
    cardioHead[1].search = paraMap["CL.Glucosyl"];
    */

    // populate vector of log factorials
    factorials[0] = 0;
    for (double i=1; i<10000000; i++)
    {
        factorials[i] = factorials[i-1] + log10(i);
    }

    /*
    // define fragment tolerances
    double fragTolerance;
    if (paraMap["fragTolmz"] == 1)
    {
        fragTolerance = paraMap["mzFragTol"];
    }
    else if (paraMap["fragTolmz"] == 0)
    {
        fragTolerance = paraMap["ppmFragTol"];
    }
    */

    // collect spectra information    
    ExtendedReaderList readerList;
    MSDataFile msd(argv[1], &readerList);
    const SpectrumListPtr& SL = msd.run.spectrumListPtr;
    size_t numSpectra = msd.run.spectrumListPtr->size();
    spec.resize(numSpectra);
    secondary.resize(numSpectra);
    firstSecondFull.resize(numSpectra);
    for (size_t i=0; i<firstSecondFull.size(); i++)
    {
        firstSecondFull[i].resize(2, 0);
    }


    for (size_t i=0; i<secondary.size(); i++)
    {
        secondary[i].PC.resize(2,0.0);
        secondary[i].PE.resize(2,0.0);
        secondary[i].PI.resize(2,0.0);
        secondary[i].PS.resize(2,0.0);
        secondary[i].PG.resize(2,0.0);
        secondary[i].PA.resize(2,0.0);
        secondary[i].CL.resize(2,0.0);
        secondary[i].CL2.resize(2,0.0);
        secondary[i].SM.resize(2,0.0);
        secondary[i].PIP.resize(2,0.0);
        secondary[i].PIP2.resize(2,0.0);
        secondary[i].MPC.resize(2,0.0);
        secondary[i].MPE.resize(2,0.0);
        secondary[i].MPI.resize(2,0.0);
        secondary[i].MPS.resize(2,0.0);
        secondary[i].MPG.resize(2,0.0);
        secondary[i].MPA.resize(2,0.0);
        secondary[i].MCL.resize(2,0.0);
        //secondary[i].M3CL.resize(2,0.0);
        secondary[i].MSM.resize(2,0.0);
        secondary[i].MPIP.resize(2,0.0);
        secondary[i].PEC.resize(2,0.0);
        secondary[i].PIC.resize(2,0.0);
        secondary[i].MPEC.resize(2,0.0);
        secondary[i].MPIC.resize(2,0.0);
    }

    vector<SpectraHolder> spectraHolder;
    int spectraIndex = 0;
    double retTime;
    for (size_t i=0; i < numSpectra; ++i)
    {
        SpectrumPtr s = SL->spectrum(i);
        if (s->precursors.empty() == false)
        {    
            retTime = s->scanList.scans[0].cvParam(MS_scan_start_time).valueAs<double>();
            double mass = s->precursors[0].selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>();
            
            double preTol;
            if (paraMap["preTolmz"] == 1)
            {
                preTol = paraMap["mzTol"];
            }
            else if (paraMap["preTolmz"] == 0)
            {
                preTol = paraMap["ppmTol"]*mass/1000000;
            }
            spectraHolder.push_back(SpectraHolder());
            spectraHolder[spectraIndex].retentionTime = retTime;
            spectraHolder[spectraIndex].preMassN = mass - preTol;
            spectraHolder[spectraIndex].preMassP = mass + preTol;
            spectraHolder[spectraIndex].spectraIndex = i;
            spectraIndex++;

        }
    }
    if (spectraHolder.size() == 0)
    {
        cout << endl << "NO PRECURSOR DATA" << endl;
        return 1;
    }
    sort(spectraHolder.begin(), spectraHolder.end(), sortBySpecMZ);

    // creation of the lipid search space and scoring vector
    double count = 0;
    double* pCount = &count;
    int pre = 0;
    int* pPre = &pre;
    
    lipidCount(adducts, paraMap, head, back, sphingoHead, cardioHead, pCount, pPre);
    
    lipCount = count;
    cout << "There are " << lipCount << " lipids in the search space." << endl;
    count = 0;
    lipidConstructionAndScoring(adducts, paraMap, head, back, sphingoHead, cardioHead, spectraHolder, SL, pCount, pPre);
    cout << endl << "writing results to .lama file and spectral svg's to embedded .html files ... ";

    // eliminate empty vector entries
    vector<vector<Score> >::iterator it = spec.begin();
    while (it != spec.end())
    {
        if (it->empty())
        {
            it = spec.erase(it);
        }
        else
        {
            ++it;
        }
    }

    // Score vector sorting
    for (size_t i=0; i<spec.size(); i++)
    {
        sort(spec[i].begin(), spec[i].end(), sortByTotalScore);
    }
    sort(spec.begin(), spec.end(), sortByHighScore);

    // print list of scores for R and LipidLama and SVG
    string hist = argv[1];
    size_t dot = hist.find_last_of(".");
    string dotless = hist.substr(0,dot);
    dotless += ".lama";
    ofstream hfile(dotless.c_str());
    hfile << argv[1] << endl;
    hfile << setprecision(10);    
    for (size_t i=0; i<spec.size(); i++)
    {
        double scoreTest = spec[i][0].totalScore;
        spec2.push_back(vector<Score>());
        for (size_t j=0; j<spec[i].size(); j++)
        {
            if (spec[i][j].totalScore >= scoreTest)
            {
                spec2[i].push_back(Score());
                spec2[i][j]=spec[i][j];
            }
        }
    }

    for (size_t i=0; i<spec2.size(); i++)
    {
        sort(spec2[i].begin(), spec2[i].end(), sortByName);
    }

    for (size_t i=0; i<spec2.size(); i++)
    {
        if (spec2[i][0].totalScore > 0)
        {
            hfile << "---" << endl;
            hfile << spec2[i][0].spectrumNumber << endl;
            hfile << spec2[i][0].lipidName << endl;
            hfile << spec2[i][0].chemFormula << endl;
            hfile << spec2[i][0].precursorMass << endl;            
            hfile << spec2[i][0].spectrumPrecursorMass << endl;
            hfile << spec2[i][0].charge << endl;
            hfile << spec2[i][0].retTime << endl;
            hfile << spec2[i][0].mod << endl;
            hfile << spec2[i][0].totalScore << endl;
        }
    }
    hfile << "SECONDHIGHESTSCORES" << endl;

    
    for (size_t i=0; i<secondary.size(); i++)
    {
        if (secondary[i].PC[0] != 0)
        {
            hfile << "PC " << secondary[i].PC[0] << " " << secondary[i].PC[1] << endl;
        }
        if (secondary[i].MPC[0] != 0)
        {
            hfile << "MPC " << secondary[i].MPC[0] << " " << secondary[i].MPC[1] << endl;
        }
        if (secondary[i].PE[0] != 0)
        {
            hfile << "PE " << secondary[i].PE[0] << " " << secondary[i].PE[1] << endl;
        }
        if (secondary[i].MPE[0] != 0)
        {
            hfile << "MPE " << secondary[i].MPE[0] << " " << secondary[i].MPE[1] << endl;
        }
        if (secondary[i].PI[0] != 0)
        {
            hfile << "PI " << secondary[i].PI[0] << " " << secondary[i].PI[1] << endl;
        }
        if (secondary[i].MPI[0] != 0)
        {
            hfile << "MPI " << secondary[i].MPI[0] << " " << secondary[i].MPI[1] << endl;
        }
        if (secondary[i].PG[0] != 0)
        {
            hfile << "PG " << secondary[i].PG[0] << " " << secondary[i].PG[1] << endl;
        }
        if (secondary[i].MPG[0] != 0)
        {
            hfile << "MPG " << secondary[i].MPG[0] << " " << secondary[i].MPG[1] << endl;
        }
        if (secondary[i].PS[0] != 0)
        {
            hfile << "PS " << secondary[i].PS[0] << " " << secondary[i].PS[1] << endl;
        }
        if (secondary[i].MPS[0] != 0)
        {
            hfile << "MPS " << secondary[i].MPS[0] << " " << secondary[i].MPS[1] << endl;
        }
        if (secondary[i].PA[0] != 0)
        {
            hfile << "PA " << secondary[i].PA[0] << " " << secondary[i].PA[1] << endl;
        }
        if (secondary[i].MPA[0] != 0)
        {
            hfile << "MPA " << secondary[i].MPA[0] << " " << secondary[i].MPA[1] << endl;
        }
        if (secondary[i].SM[0] != 0)
        {
            hfile << "SM " << secondary[i].SM[0] << " " << secondary[i].SM[1] << endl;
        }
        if (secondary[i].MSM[0] != 0)
        {
            hfile << "MSM " << secondary[i].MSM[0] << " " << secondary[i].MSM[1] << endl;
        }
        if (secondary[i].CL[0] != 0)
        {
            hfile << "CL " << secondary[i].CL[0] << " " << secondary[i].CL[1] << endl;
        }
        if (secondary[i].MCL[0] != 0)
        {
            hfile << "MCL " << secondary[i].MCL[0] << " " << secondary[i].MCL[1] << endl;
        }
        if (secondary[i].CL2[0] != 0)
        {
            hfile << "CL2 " << secondary[i].CL2[0] << " " << secondary[i].CL2[1] << endl;
        }
        if (secondary[i].PIP[0] != 0)
        {
            hfile << "PIP " << secondary[i].PIP[0] << " " << secondary[i].PIP[1] << endl;
        }
        if (secondary[i].MPIP[0] != 0)
        {
            hfile << "MPIP " << secondary[i].MPIP[0] << " " << secondary[i].MPIP[1] << endl;
        }
        if (secondary[i].PIP2[0] != 0)
        {
            hfile << "PIP2 " << secondary[i].PIP2[0] << " " << secondary[i].PIP2[1] << endl;
        }
        if (secondary[i].PEC[0] != 0)
        {
            hfile << "PEC " << secondary[i].PEC[0] << " " << secondary[i].PEC[1] << endl;
        }
        if (secondary[i].PIC[0] != 0)
        {
            hfile << "PIC " << secondary[i].PIC[0] << " " << secondary[i].PIC[1] << endl;
        }
        if (secondary[i].MPEC[0] != 0)
        {
            hfile << "MPEC " << secondary[i].MPEC[0] << " " << secondary[i].MPEC[1] << endl;
        }
        if (secondary[i].MPIC[0] != 0)
        {
            hfile << "MPIC " << secondary[i].MPIC[0] << " " << secondary[i].MPIC[1] << endl;
        }
    }
    hfile << "MODIFICATIONS" << endl;
    for (size_t i=0; i<modifications.size(); i++)
    {
        hfile << modifications[i] << endl;
    }

    /*
    string dirString = argv[1];
    size_t dirInd = 0;
    for (size_t i=0; i<dirString.length(); i++)
    {
        if (dirString[i] == '\\')
        {
            dirInd = i;
        }
    }
    */

    int i = 0;
    string direct = argv[1];
    while(direct[i] != '.')
    {
        i++;
    }
    direct = direct.substr(0, i);
    boost::filesystem::path dir(direct);
    if (!(boost::filesystem::exists(dir)))
    {
        boost::filesystem::create_directory(dir);
    }
    for (boost::filesystem::directory_iterator end_dir_it, it(dir); it!=end_dir_it; ++it)
    {
        remove_all(it->path());
    }

    const unsigned WIDTH = 1800;
    const unsigned HEIGHT = 1000;

    for (size_t i=0; i<spec.size(); i++)
    {
        double tScore = spec[i][0].totalScore;
        //size_t j = 0;

        for (size_t j=0; j<spec[i].size(); j++)
        {
            if (spec[i][j].totalScore >= tScore)
            {
                double cutoff;
                if (spec[i][j].precursorDescription == "deprotonatedX2")
                {
                    cutoff = spec[i][j].precursorMass*2;
                }
                else
                {
                    cutoff = spec[i][j].precursorMass;
                }

                for (size_t k=0; k<spec[i][j].match.size(); k++)
                {
                    if (spec[i][j].match[k].peakIndex != 99999)
                    {
                        spec[i][j].ms2list[spec[i][j].match[k].peakIndex].match = 1;
                        spec[i][j].ms2list[spec[i][j].match[k].peakIndex].fragmentD = spec[i][j].match[k].fragmentD;
                    }
                }
                double left = spec[i][j].match[0].mz;
                double right = max(spec[i][j].match[spec[i][j].match.size()-1].mz, spec[i][j].precursorMass);
                for (size_t k=0; k<spec[i][j].ms2list.size(); k++)
                {
                
                    if (spec[i][j].ms2list[k].mz < left)
                    {
                        left = spec[i][j].ms2list[k].mz;
                    }
                    if ((spec[i][j].ms2list[k].mz > right) && (spec[i][j].ms2list[k].mz < cutoff+10))
                    {
                        right = spec[i][j].ms2list[k].mz;
                    }
                }
                left = floor(left/100)*100;
                right = ceil(right/100)*100;
                double range = right - left;
                double yHeight = spec[i][j].ms2list[0].intensity;
                yHeight = ceil(yHeight/pow(10, floor(log10(yHeight))))*pow(10, floor(log10(yHeight)));
                stringstream ss;
                ss << direct << "\\" << spec[i][j].spectrumNumber << ".html";
                string svgName = ss.str();
            
                ofstream svg(svgName.c_str());

                svg << "<!DOCTYPE html>" << endl << "<html>" << endl << "<body>" << endl << endl
                    //<< "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>" << endl
                    << "<svg width=\"" << WIDTH << "\" height=\"" << HEIGHT << "\">" << endl
                    << "<line x1=\"99\" y1=\"500\" x2=\"1000\" y2=\"500\" stroke=\"black\" stroke-width=\"2\" />" << endl
                    << "<line x1=\"99\" y1=\"600\" x2=\"1000\" y2=\"600\" stroke=\"black\" stroke-width=\"2\" />" << endl
                    << "<line x1=\"100\" y1=\"500\" x2=\"100\" y2=\"50\" stroke=\"black\" stroke-width=\"2\" />" << endl
                    << "<text font-family=\"sans-serif\" font-size=\"18\" transform=\"rotate(270 35,300)\" x=\"35\" y=\"300\" fill=\"black\">Intensity</text>" << endl
                    << "<text font-family=\"sans-serif\" font-size=\"18\" x=\"550\" y=\"650\" fill=\"black\">m/z</text>" << endl
                    << "<text font-family=\"sans-serif\" font-size=\"20\" font-weight=\"bold\" x=\"1050\" y=\"50\" fill=\"black\">Lipid: </text>" << endl
                    << "<text font-family=\"sans-serif\" font-size=\"20\" font-weight=\"bold\" x=\"1115\" y=\"50\" fill=\"black\">" << spec[i][j].lipidName << "</text>" << endl
                    << "<text font-family=\"sans-serif\" font-size=\"20\" font-weight=\"bold\" x=\"1050\" y=\"75\" fill=\"black\">Score: </text>" << endl
                    << "<text font-family=\"sans-serif\" font-size=\"20\" font-weight=\"bold\" x=\"1120\" y=\"75\" fill=\"black\">" << spec[i][j].totalScore << "</text>" << endl
                    << "<text font-family=\"sans-serif\" font-weight=\"bold\" x=\"1050\" y=\"110\" fill=\"black\">Precursor: </text>" << endl
                    << "<text font-family=\"sans-serif\" x=\"1205\" y=\"110\" fill=\"black\">" << "(" << spec[i][j].precursorDescription << ")" << "</text>" << endl
                    << "<text font-family=\"sans-serif\" x=\"1140\" y=\"110\" fill=\"darkorange\">" << spec[i][j].precursorMass << "</text>" << endl
                    << "<text font-family=\"sans-serif\" font-weight=\"bold\" x=\"1050\" y=\"145\" fill=\"black\">m/z</text>" << endl
                    << "<text font-family=\"sans-serif\" font-weight=\"bold\" x=\"1150\" y=\"145\" fill=\"black\">Intensity</text>" << endl
                    << "<text font-family=\"sans-serif\" font-weight=\"bold\" x=\"1250\" y=\"145\" fill=\"black\">Fragment</text>" << endl
                    << "<line x1=\"" << 150 + (spec[i][j].precursorMass-left)*800/range << "\" y1=\"600\" x2=\"" << 150 + (spec[i][j].precursorMass-left)*800/range << "\" y2=\"550\" stroke=\"darkorange\" stroke-width=\"1\" />" << endl;

                for (size_t k=left; k<right+1; k+=100)
                {

                    svg << "<line x1=\"" << 150 + (k-left)*800/range << "\" y1=\"507\" x2=\"" << 150 + (k-left)*800/range << "\" y2=\"493\" stroke=\"black\" stroke-width=\"1\" />" << endl
                        << "<line x1=\"" << 150 + (k-left)*800/range << "\" y1=\"607\" x2=\"" << 150 + (k-left)*800/range << "\" y2=\"593\" stroke=\"black\" stroke-width=\"1\" />" << endl
                        << "<text text-anchor=\"middle\" font-family=\"sans-serif\" x=\"" << 150 + (k-left)*800/range << "\" y=\"623\" fill=\"black\">" << k << "</text>" << endl;
                }
            
                double step = pow(10, floor(log10(yHeight)));
                for (size_t k=step; k<yHeight+1; k+=step)
                {
                    svg << "<line x1=\"" << 93 << "\" y1=\"" << 500 - k*400/yHeight << "\" x2=\"" << 107 << "\" y2=\"" << 500 - k*400/yHeight << "\" stroke=\"black\" stroke-width=\"1\" />" << endl
                        << "<text font-family=\"sans-serif\" x=\"60\" y=\"" << 506 - k*400/yHeight << "\" fill=\"black\">" << k << "</text>" << endl;
                }

                for (size_t k=0; k<spec[i][j].ms2list.size(); k++)
                {
                    if (spec[i][j].ms2list[k].mz < cutoff+10)
                    {
                        string color = "black";
                        if (spec[i][j].ms2list[k].match == 1)
                        {
                            color = "green";
                        }
                        svg << "<line x1=\"" << 150 + (spec[i][j].ms2list[k].mz-left)*800/range << "\" y1=\"500\" x2=\"" << 150 + (spec[i][j].ms2list[k].mz-left)*800/range << "\" y2=\"" 
                            << 500 - spec[i][j].ms2list[k].intensity*400/yHeight << "\" stroke=\"" << color << "\" stroke-width=\"1\" />" << endl;
                    }
                }

                for (size_t k=0; k<spec[i][j].match.size(); k++)
                {
                    string color = "black";
                    if (spec[i][j].match[k].match == 1)
                    {
                        color = "green";
                    }
                    svg << "<line x1=\"" << 150 + (spec[i][j].match[k].mz-left)*800/range << "\" y1=\"600\" x2=\"" << 150 + (spec[i][j].match[k].mz-left)*800/range << "\" y2=\"" 
                        << 550 << "\" stroke=\"" << color << "\" stroke-width=\"1\" />" << endl
                        //<< "<text font-size=\"8\" text-anchor=\"middle\" font-family=\"sans-serif\" x=\"" << 150 + spec[i][j].match[k].mz*800/range << "\" y=\"545\" fill=\"black\">" << k+1 << "</text>" << endl
                        //<< "<text font-family=\"sans-serif\" x=\"" << 1050 << "\" y=\"" << 50 + 20*k << "\" fill=\"black\">" << k+1 << ". " << "</text>" << endl
                        << "<text font-family=\"sans-serif\" x=\"" << 1050 << "\" y=\"" << 165 + 20*k << "\" fill=\"" << color << "\">" << spec[i][j].match[k].mz << "</text>" << endl
                        << "<text font-family=\"sans-serif\" x=\"" << 1150 << "\" y=\"" << 165 + 20*k << "\" fill=\"" << color << "\">" << spec[i][j].match[k].intensity << "</text>" << endl
                        << "<text font-family=\"sans-serif\" x=\"" << 1250 << "\" y=\"" << 165 + 20*k << "\" fill=\"" << color << "\">" << spec[i][j].match[k].fragmentD << "</text>" << endl;
                }

                svg    << "</svg>" << endl << endl
                    << "</body>" << endl << "</html>" << endl;

                svg.close();
            }
        }
    }
    cout << "done." << endl;

    /*
    now = time(0);
    current = localtime(&now);
    //cout << "hour:" << current->tm_hour << " min:" << current->tm_min << " sec:" << current->tm_sec << endl;
    */

    return 0;
}

// Processing of configuration/User input file
Map processConfig(const string & fname)
{
    string str;
    ifstream file(fname.c_str());
    if (!file)
    {
        cout << "Unable to open file.";
        getlinePortable(cin, str);
        cin.get();
        exit(1);
    }

    string line, tempLine, pStr;
    Map tempMap;
    while(getlinePortable(file, line))
    {
        if (line[line.length()-1] == 'Y')
        {    
            tempLine = line.substr(0, line.length() - 4);
            tempMap[tempLine] = 1;
        }
        if (line[line.length()-1] == 'N')
        {    
            tempLine = line.substr(0, line.length() - 4);
            tempMap[tempLine] = 0;
        }
        if (isdigit(line[line.length()-1]))
        {    
            int i = 0;
            while(line[i] != '=')
            {
                i++;                
            }
            tempLine = line.substr(0, i - 1);
            pStr = line.substr(i + 1, line.length() - 1);
            tempMap[tempLine] = atof(pStr.c_str());
        }
    }
    return tempMap;
}






// lipid count
void lipidCount(vector<Adduct> adducts, Map & paraMap, const HeadGroups & head, const BackBones & back, const HeadGroups & sphingoHead, const HeadGroups & cardioHead, double* count, int* pre)
{ 
    if (paraMap["GP"] == 1)
    {
        for  (int i = 0; i < 9; i++)
        {
            if (head[i].search == 1)
            {
                if (paraMap["GP.Lyso2"] == 1)
                {
                    if (paraMap["GP.AcylBond1"] == 1)
                    {
                        for (double m = paraMap["GP.LowerLengthLim1"]; m <= paraMap["GP.UpperLengthLim1"]; m++)
                        {
                            for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                            {
                                if ((m < 2) || (n > (m - 1)/2))
                                {
                                    break;
                                }
                                if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                {
                                    break;
                                }
                                
                                (*count)++;

                            }
                        }
                    }

                    if (paraMap["GP.EtherBond1"] == 1)
                    {
                        for (double m = paraMap["GP.LowerLengthLim1"]; m<= paraMap["GP.UpperLengthLim1"]; m++)
                        {
                            for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                            {
                                if ((m < 2) || (n > m/2))
                                {
                                    break;
                                }
                                if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                {
                                    break;
                                }

                                (*count)++;
                                
                            }
                        }
                    }
                }

                if (paraMap["GP.AcylBond2"] == 1)    // sn-2/acyl
                {
                    for (double j = paraMap["GP.LowerLengthLim2"]; j <= paraMap["GP.UpperLengthLim2"]; j++)
                    {
                        for (double k = paraMap["GP.LowerDoubleBondLim2"]; k <= paraMap["GP.UpperDoubleBondLim2"]; k++)
                        {
                            if ((j < 2) || (k > (j - 1)/2))
                            {
                                break;
                            }
                            if ((paraMap["GP.evenOnly"] == 1) && (int(j) % 2 != 0))
                            {
                                break;
                            }

                            if (paraMap["GP.Lyso1"] == 1)
                            {

                                (*count)++;
                                
                            }

                            if (paraMap["GP.AcylBond1"] == 1)
                            {
                                for (double m = paraMap["GP.LowerLengthLim1"]; m <= paraMap["GP.UpperLengthLim1"]; m++)
                                {
                                    for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                                    {

                                        if ((m < 2) || (n > (m - 1)/2))
                                        {
                                            break;
                                        }
                                        if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                        {
                                            break;
                                        }
                                
                                        (*count)++;                                    


                                    }
                                }
                            }
                            if (paraMap["GP.EtherBond1"] == 1)
                            {
                                for (double m = paraMap["GP.LowerLengthLim1"]; m<= paraMap["GP.UpperLengthLim1"]; m++)
                                {
                                    for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                                    {
                                        if ((m < 2) || (n > m/2))
                                        {
                                            break;
                                        }
                                        if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                        {
                                            break;
                                        }
                                
                                        (*count)++;
                                        
                                    }
                                }
                            }
                        }
                    }
                }
                if (paraMap["GP.EtherBond2"] == 1)    // sn-2/ether
                {
                    for (double j = paraMap["GP.LowerLengthLim2"]; j <= paraMap["GP.UpperLengthLim2"]; j++)
                    {
                        for (double k = paraMap["GP.LowerDoubleBondLim2"]; k <= paraMap["GP.UpperDoubleBondLim2"]; k++)
                        {
                            if ((j < 2) || (k > j/2))
                            {
                                break;
                            }
                            if ((paraMap["GP.evenOnly"] == 1) && (int(j) % 2 != 0))
                            {
                                break;
                            }


                            if (paraMap["GP.Lyso1"] == 1)
                            {
                                (*count)++;
                                
                            }

                            if (paraMap["GP.AcylBond1"] == 1)
                            {
                                for (double m = paraMap["GP.LowerLengthLim1"]; m <= paraMap["GP.UpperLengthLim1"]; m++)
                                {
                                    for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                                    {
                                        if ((m < 2) || (n > (m - 1)/2))
                                        {
                                            break;
                                        }
                                        if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                        {
                                            break;
                                        }
                                                                
                                        (*count)++;
                                        
                                    }
                                }
                            }
                            if (paraMap["GP.EtherBond1"] == 1)    // sn-2/ether sn-1/ether
                            {
                                for (double m = paraMap["GP.LowerLengthLim1"]; m<= paraMap["GP.UpperLengthLim1"]; m++)
                                {
                                    for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                                    {
                                        if ((m < 2) || (n > m/2))
                                        {
                                            break;
                                        }
                                        if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                        {
                                            break;
                                        }
                                
                                        (*count)++;
                                        
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
    if (paraMap["SP"] == 1)
    { 
        for (size_t i = 2; i < back.size(); i++)
        {
            for (int j = 0; j < 3; j++)
            {
                if (sphingoHead[j].search == 1)
                {
                    for (double m = paraMap["SP.LowerLength"]; m <= paraMap["SP.UpperLength"]; m++)
                    {
                        for (double n = paraMap["SP.LowerDoubleBond"]; n <= paraMap["SP.UpperDoubleBond"]; n++)
                        {
                            if ((m < 2) || (n > (m - 1)/2))
                            {
                                break;
                            }
                            /*if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                            {
                                break;
                            }*/

                            (*count)++;
                            
                        }
                    }                
                }
            }
        }
    }    
    int cardioCount = 0;
    int twice = 0;
    if (paraMap["CL"] == 1)
    { 
        for  (int l = 0; l < 1; l++)
        {
            if (cardioHead[l].search == 1)
            {
                if (paraMap["CL.Acyl"] == 1)
                {
                    size_t L1;
                    if ((paraMap["CL.Lyso1"] == 1) || (paraMap["CL.LowerLengthLim1a"] < 0))
                    {
                        L1 = 0;
                    }
                    else
                    {
                        L1 = paraMap["CL.LowerLengthLim1a"];
                    }
                    for (size_t j = L1; j <= paraMap["CL.UpperLengthLim1a"]; j++)
                    {
                        if ((j != 0) && (j < paraMap["CL.LowerLengthLim1a"]))
                        {
                            continue;
                        }
                        for (size_t k = paraMap["CL.LowerDoubleBondLim1a"]; k <= paraMap["CL.UpperDoubleBondLim1a"]; k++)
                        {
                            if (k > (j - 1)/2)
                            {
                                break;
                            }
                            if ((paraMap["CL.evenOnly"] == 1) && (int(j) % 2 != 0))
                            {
                                break;
                            }

                            double FA1mass;

                            if (j == 0)
                            {
                                FA1mass = H;
                            }

                            else
                            {
                                FA1mass = j*C + (2*(j-1)+1)*H - 2*k*H + O;
                            }

                            size_t L2;
                            if ((paraMap["CL.Lyso2"] == 1) || (paraMap["CL.LowerLengthLim2a"] < 0))
                            {
                                L2 = 0;
                            }
                            else
                            {
                                L2 = paraMap["CL.LowerLengthLim2a"];
                            }
                            
                            for (size_t m = L2; m <= paraMap["CL.UpperLengthLim2a"]; m++)
                            {
                                
                                if ((m != 0) && (m < paraMap["CL.LowerLengthLim2a"]))
                                {
                                    continue;
                                }
                                for (size_t n = paraMap["CL.LowerDoubleBondLim2a"]; n <= paraMap["CL.UpperDoubleBondLim2a"]; n++)
                                {
                                    if (n > (m - 1)/2)
                                    {
                                        break;
                                    }
                                    if ((paraMap["CL.evenOnly"] == 1) && (int(m) % 2 != 0))
                                    {
                                        break;
                                    }

                                    double FA2mass;

                                    if (m == 0)
                                    {
                                        FA2mass = H;
                                    }

                                    else
                                    {
                                        FA2mass = m*C + (2*(m-1)+1)*H - 2*n*H + O;
                                    }
                                    
                                    size_t L3;
                                    if ((paraMap["CL.Lyso3"] == 1) || (paraMap["CL.LowerLengthLim1b"] < 0))
                                    {
                                        L3 = 0;
                                    }
                                    else
                                    {
                                        L3 = paraMap["CL.LowerLengthLim1b"];
                                    }
                                    for (size_t s = L3; s<= paraMap["CL.UpperLengthLim1b"]; s++)
                                    {
                                        if ((s != 0) && (s < paraMap["CL.LowerLengthLim1b"]))
                                        {
                                            continue;
                                        }
                                        for (size_t t = paraMap["CL.LowerDoubleBondLim1b"]; t <= paraMap["CL.UpperDoubleBondLim1b"]; t++)
                                        {
                                            if (t > (s - 1)/2)
                                            {
                                                break;
                                            }
                                            if ((paraMap["CL.evenOnly"] == 1) && (int(s) % 2 != 0))
                                            {
                                                break;
                                            }
                                            double FA3mass;

                                            if (s == 0)
                                            {
                                                FA3mass = H;
                                            }

                                            else
                                            {
                                                FA3mass = s*C + (2*(s-1)+1)*H - 2*t*H + O;
                                            }

                                            size_t L4;
                                            if ((paraMap["CL.Lyso4"] == 1) || (paraMap["CL.LowerLengthLim2b"] < 0))
                                            {
                                                L4 = 0;
                                            }
                                            else
                                            {
                                                L4 = paraMap["CL.LowerLengthLim2b"];
                                            }
                                            for (size_t u = L4; u<= paraMap["CL.UpperLengthLim2b"]; u++)
                                            {
                                            if ((u != 0) && (u < paraMap["CL.LowerLengthLim2b"]))
                                            {
                                                continue;
                                            }
                                                for (size_t v = paraMap["CL.LowerDoubleBondLim2b"]; v <= paraMap["CL.UpperDoubleBondLim2b"]; v++)
                                                {
                                                    if (v > (u - 1)/2)
                                                    {
                                                        break;
                                                    }
                                                    if ((paraMap["CL.evenOnly"] == 1) && (int(u) % 2 != 0))
                                                    {
                                                        break;
                                                    }

                                                    double FA4mass;

                                                    if (u == 0)
                                                    {
                                                        FA4mass = H;
                                                    }

                                                    else
                                                    {
                                                        FA4mass = u*C + (2*(u-1)+1)*H - 2*v*H + O;
                                                    }

                                                    if (FA1mass != H || FA2mass != H || FA3mass != H || FA4mass != H)
                                                    {
                                                        //(*count)++;
                                                        cardioCount++;
                                                    }
                                                    int symmetric = 0;
                                                    if ((j == s) && (m == u) && (k == t) && (n == v))
                                                    {
                                                        symmetric = 1;
                                                    }
                                                    if (symmetric == 0)
                                                    {
                                                        if ((((j >= paraMap["CL.LowerLengthLim1b"]) && (j <= paraMap["CL.UpperLengthLim1b"]) && (k >= paraMap["CL.LowerDoubleBondLim1b"]) && (k <= paraMap["CL.UpperDoubleBondLim1b"])) || ((j == 0) && (paraMap["CL.Lyso3"] == 1)))
                                                            && (((m >= paraMap["CL.LowerLengthLim2b"]) && (m <= paraMap["CL.UpperLengthLim2b"]) && (n >= paraMap["CL.LowerDoubleBondLim2b"]) && (n <= paraMap["CL.UpperDoubleBondLim2b"])) || ((m == 0) && (paraMap["CL.Lyso4"] == 1)))
                                                            && (((s >= paraMap["CL.LowerLengthLim1a"]) && (s <= paraMap["CL.UpperLengthLim1a"]) && (t >= paraMap["CL.LowerDoubleBondLim1a"]) && (t <= paraMap["CL.UpperDoubleBondLim1a"])) || ((s == 0) && (paraMap["CL.Lyso1"] == 1)))
                                                            && (((u >= paraMap["CL.LowerLengthLim2a"]) && (u <= paraMap["CL.UpperLengthLim2a"]) && (v >= paraMap["CL.LowerDoubleBondLim2a"]) && (v <= paraMap["CL.UpperDoubleBondLim2a"])) || ((u == 0) && (paraMap["CL.Lyso2"] == 1))))
                                                        {
                                                            twice++;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }        
    }
    (*count) += (cardioCount - twice/2);
}

// Generation of lipid search space
void lipidConstructionAndScoring(vector<Adduct> adducts, Map & paraMap, const HeadGroups & head, const BackBones & back, const HeadGroups & sphingoHead, const HeadGroups & cardioHead, vector<SpectraHolder> spectraHolder, const SpectrumListPtr& SL, double* count, int* pre)
{ 
    if (paraMap["GP"] == 1)
    {
        int ca, ox, hy, ph, ni;
        for  (int i = 0; i < 9; i++)
        {
            ca = 3;
            hy = 5;
            ox = 3;
            ph = 0;
            ni = 0;

            if (head[i].search == 1)
            {
                if (head[i].name == "Choline")
                {
                    ca += 5;
                    hy += 13;
                    ox += 3;
                    ph += 1;
                    ni += 1;
                }
                if (head[i].name == "Ethanolamine")
                {
                    ca += 2;
                    hy += 7;
                    ox += 3;
                    ph += 1;
                    ni += 1;
                }
                if (head[i].name == "Serine")
                {
                    ca += 3;
                    hy += 7;
                    ox += 5;
                    ph += 1;
                    ni += 1;
                }
                if (head[i].name == "Glycerol")
                {
                    ca += 3;
                    hy += 8;
                    ox += 5;
                    ph += 1;
                    ni += 0;
                }
                if (head[i].name == "Inositol")
                {
                    ca += 6;
                    hy += 12;
                    ox += 8;
                    ph += 1;
                    ni += 0;
                }
                if (head[i].name == "Phosphate")
                {
                    ca += 0;
                    hy += 2;
                    ox += 3;
                    ph += 1;
                    ni += 0;
                }
                if (head[i].name == "PIP")
                {
                    ca += 6;
                    hy += 13;
                    ox += 11;
                    ph += 2;
                    ni += 0;
                }
                if (head[i].name == "PIP2")
                {
                    ca += 6;
                    hy += 14;
                    ox += 14;
                    ph += 3;
                    ni += 0;
                }
                if (head[i].name == "PIP3")
                {
                    ca += 6;
                    hy += 15;
                    ox += 17;
                    ph += 4;
                    ni += 0;
                }

                if (paraMap["GP.Lyso2"] == 1)    // sn-2/lyso
                {
                    stringstream designation;
                    designation /*<< "Lyso"*/ << head[i].common << "(";

                    stringstream FA2formula;
                    double FA2mass;
                    FA2formula << "H";
                    int ca1 = ca;
                    int hy1 = hy;
                    int ox1 = ox;
                    hy1 += 1;
                    FA2mass = H;
                    string bond2 = "lyso";

                    if (paraMap["GP.AcylBond1"] == 1)    // sn-2/lyso sn-1/acyl 
                    {
                        for (double m = paraMap["GP.LowerLengthLim1"]; m <= paraMap["GP.UpperLengthLim1"]; m++)
                        {
                            for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                            {
                                if ((m < 2) || (n > (m - 1)/2))
                                {
                                    break;
                                }
                                if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                {
                                    break;
                                }
                                stringstream FA1formula;
                                double FA1mass;
                                FA1formula << "C" << m << "H" << 2*(m-1)+1-2*n << "O";
                                int ca2 = ca1;
                                int hy2 = hy1;
                                int ox2 = ox1;
                                ca2 += m;
                                hy2 += 2*(m-1)+1-2*n;
                                ox2 += 1;
                                FA1mass = m*C + (2*(m-1)+1)*H - 2*n*H + O;
                                string bond1 = "acyl";
                                stringstream designationA;
                                designationA << designation.str();
                                designationA << m << ":" << n << "/" << 0 << ":" << 0 << ")";
                                
                                stringstream lipidFormula;
                                lipidFormula << "C" << ca2 << "H" << hy2;
                                if (ni != 0)
                                {
                                    if (ni == 1)
                                    {
                                        lipidFormula << "N";
                                    }
                                    if (ni > 1)
                                    {
                                        lipidFormula << "N" << ni;
                                    }
                                        
                                }
                                if (ox2 == 1)
                                {
                                    lipidFormula << "O";
                                }
                                if (ox2 > 1)
                                {
                                    lipidFormula << "O" << ox2;
                                }
                                lipidFormula << "P";

                                Lipid lipid;

                                lipid.name = designationA.str();
                                lipid.decoy = false;
                                lipid.totalMass = back[0].mass + head[i].mass + FA1mass + FA2mass;
                                lipid.lipidFormula = lipidFormula.str();
                                lipid.backbone = back[0].name;
                                lipid.backboneFormula = back[0].formula;
                                lipid.backboneMass = back[0].mass;
                                lipid.headGroup = head[i].name;
                                lipid.headGroupFormula = head[i].formula;
                                lipid.headGroupMass = head[i].mass;
                                lipid.FA1 = FA1formula.str();
                                lipid.FA1Mass = FA1mass;
                                lipid.FA1DoubleBonds = n;
                                lipid.SN1bond = bond1;
                                lipid.FA2 = FA2formula.str();
                                lipid.FA2Mass = FA2mass;
                                lipid.FA2DoubleBonds = 0;
                                lipid.SN2bond = bond2;
                                (*count)++;
                                if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                {
                                    cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                                }

                                precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                            }
                        }
                    }

                    if (paraMap["GP.EtherBond1"] == 1)    // sn-2/lyso sn-1/ether
                    {
                        for (double m = paraMap["GP.LowerLengthLim1"]; m<= paraMap["GP.UpperLengthLim1"]; m++)
                        {
                            for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                            {
                                if ((m < 2) || (n > m/2))
                                {
                                    break;
                                }
                                if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                {
                                    break;
                                }
                                stringstream FA1formula;
                                double FA1mass;
                                FA1formula << "C" << m << "H" << 2*m+1-2*n;
                                int ca2 = ca1;
                                int hy2 = hy1;
                                int ox2 = ox1;
                                ca2 += m;
                                hy2 += 2*m+1-2*n;
                                FA1mass = m*C + (2*m+1)*H - 2*n*H;
                                string bond1 = "ether";
                                string bondDes1 = "O-";
                                stringstream designationE;
                                designationE << designation.str();
                                designationE << bondDes1 << m << ":" << n << "/" << 0 << ":" << 0 << ")";
                                
                                stringstream lipidFormula;
                                lipidFormula << "C" << ca2 << "H" << hy2;
                                if (ni != 0)
                                {
                                    if (ni == 1)
                                    {
                                        lipidFormula << "N";
                                    }
                                    if (ni > 1)
                                    {
                                        lipidFormula << "N" << ni;
                                    }
                                        
                                }
                                if (ox2 == 1)
                                {
                                    lipidFormula << "O";    
                                }
                                if (ox2 > 1)
                                {
                                    lipidFormula << "O" << ox2;    
                                }
                                lipidFormula << "P";

                                Lipid lipid;

                                lipid.name = designationE.str();
                                lipid.decoy = false;
                                lipid.totalMass = back[0].mass + head[i].mass + FA1mass + FA2mass;
                                lipid.lipidFormula = lipidFormula.str();
                                lipid.backbone = back[0].name;
                                lipid.backboneFormula = back[0].formula;
                                lipid.backboneMass = back[0].mass;
                                lipid.headGroup = head[i].name;
                                lipid.headGroupFormula = head[i].formula;
                                lipid.headGroupMass = head[i].mass;
                                lipid.FA1 = FA1formula.str();
                                lipid.FA1Mass = FA1mass;
                                lipid.FA1DoubleBonds = n;
                                lipid.SN1bond = bond1;
                                lipid.FA2 = FA2formula.str();
                                lipid.FA2Mass = FA2mass;
                                lipid.FA2DoubleBonds = 0;
                                lipid.SN2bond = bond2;
                                (*count)++;
                                if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                {
                                    cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                                }
                                
                                precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                            }
                        }
                    }
                }

                if (paraMap["GP.AcylBond2"] == 1)    // sn-2/acyl
                {
                    
                    stringstream designation;
                    designation << head[i].common << "(";

                    for (double j = paraMap["GP.LowerLengthLim2"]; j <= paraMap["GP.UpperLengthLim2"]; j++)
                    {
                        for (double k = paraMap["GP.LowerDoubleBondLim2"]; k <= paraMap["GP.UpperDoubleBondLim2"]; k++)
                        {
                            if ((j < 2) || (k > (j - 1)/2))
                            {
                                break;
                            }
                            if ((paraMap["GP.evenOnly"] == 1) && (int(j) % 2 != 0))
                            {
                                break;
                            }
                            stringstream FA2formula;
                            double FA2mass;
                            FA2formula << "C" << j << "H" << 2*(j-1)+1-2*k << "O";
                            int ca1 = ca;
                            int hy1 = hy;
                            int ox1 = ox;
                            ca1 += j;
                            hy1 += 2*(j-1)+1-2*k;
                            ox1 += 1;
                            FA2mass = j*C + (2*(j-1)+1)*H - 2*k*H + O;
                            string bond2 = "acyl";

                            if (paraMap["GP.Lyso1"] == 1)
                            {
                                stringstream FA1formula;
                                double FA1mass;
                                FA1formula << "H";
                                int ca2 = ca1;
                                int hy2 = hy1;
                                int ox2 = ox1;
                                hy2 += 1;
                                FA1mass = H;
                                string bond1 = "lyso";
                                stringstream designationA;
                                designationA /*<< "Lyso"*/ << designation.str();
                                designationA << 0 << ":" << 0 << "/" << j << ":" << k << ")";
                                
                                stringstream lipidFormula;
                                lipidFormula << "C" << ca2 << "H" << hy2;
                                if (ni != 0)
                                {
                                    if (ni == 1)
                                    {
                                        lipidFormula << "N";
                                    }
                                    if (ni > 1)
                                    {
                                        lipidFormula << "N" << ni;
                                    }
                                        
                                }
                                if (ox2 == 1)
                                {
                                    lipidFormula << "O";    
                                }
                                if (ox2 > 1)
                                {
                                    lipidFormula << "O" << ox2;    
                                }
                                lipidFormula << "P";

                                Lipid lipid;
                                
                                lipid.name = designationA.str();
                                lipid.decoy = false;
                                lipid.totalMass = back[0].mass + head[i].mass + FA1mass + FA2mass;
                                lipid.lipidFormula = lipidFormula.str();
                                lipid.backbone = back[0].name;
                                lipid.backboneFormula = back[0].formula;
                                lipid.backboneMass = back[0].mass;
                                lipid.headGroup = head[i].name;
                                lipid.headGroupFormula = head[i].formula;
                                lipid.headGroupMass = head[i].mass;
                                lipid.FA1 = FA1formula.str();
                                lipid.FA1Mass = FA1mass;
                                lipid.FA1DoubleBonds = 0;
                                lipid.SN1bond = bond1;
                                lipid.FA2 = FA2formula.str();
                                lipid.FA2Mass = FA2mass;
                                lipid.FA2DoubleBonds = k;
                                lipid.SN2bond = bond2;
                                (*count)++;
                                if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                {
                                    cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                                }
                                
                                precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                            }

                            if (paraMap["GP.AcylBond1"] == 1)    // sn-2/acyl sn-1/acyl
                            {
                                for (double m = paraMap["GP.LowerLengthLim1"]; m <= paraMap["GP.UpperLengthLim1"]; m++)
                                {
                                    for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                                    {
                                        //if (m != j && k != n)
                                        //{




                                        if ((m < 2) || (n > (m - 1)/2))
                                        {
                                            break;
                                        }
                                        if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                        {
                                            break;
                                        }
                                        stringstream FA1formula;
                                        double FA1mass;
                                        FA1formula << "C" << m << "H" << 2*(m-1)+1-2*n << "O";
                                        int ca2 = ca1;
                                        int hy2 = hy1;
                                        int ox2 = ox1;
                                        ca2 += m;
                                        hy2 += 2*(m-1)+1-2*n;
                                        ox2 += 1;
                                        FA1mass = m*C + (2*(m-1)+1)*H - 2*n*H + O;
                                        string bond1 = "acyl";
                                        stringstream designationA;
                                        designationA << designation.str();
                                        designationA << m << ":" << n << "/" << j << ":" << k << ")";
                                
                                        stringstream lipidFormula;
                                        lipidFormula << "C" << ca2 << "H" << hy2;
                                        if (ni != 0)
                                        {
                                            if (ni == 1)
                                            {
                                                lipidFormula << "N";
                                            }
                                            if (ni > 1)
                                            {
                                                lipidFormula << "N" << ni;
                                            }
                                        
                                        }
                                        if (ox2 == 1)
                                        {
                                            lipidFormula << "O";    
                                        }
                                        if (ox2 > 1)
                                        {
                                            lipidFormula << "O" << ox2;    
                                        }
                                        lipidFormula << "P";

                                        Lipid lipid;
                                
                                        lipid.name = designationA.str();
                                        lipid.decoy = false;
                                        lipid.totalMass = back[0].mass + head[i].mass + FA1mass + FA2mass;
                                        lipid.lipidFormula = lipidFormula.str();
                                        lipid.backbone = back[0].name;
                                        lipid.backboneFormula = back[0].formula;
                                        lipid.backboneMass = back[0].mass;
                                        lipid.headGroup = head[i].name;
                                        lipid.headGroupFormula = head[i].formula;
                                        lipid.headGroupMass = head[i].mass;
                                        lipid.FA1 = FA1formula.str();
                                        lipid.FA1Mass = FA1mass;
                                        lipid.FA1DoubleBonds = n;
                                        lipid.SN1bond = bond1;
                                        lipid.FA2 = FA2formula.str();
                                        lipid.FA2Mass = FA2mass;
                                        lipid.FA2DoubleBonds = k;
                                        lipid.SN2bond = bond2;
                                        (*count)++;    
                                        if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                        {
                                            cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                                        }                                

                                        precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                                    //    }





                                    }
                                }
                            }
                            if (paraMap["GP.EtherBond1"] == 1)    // sn-2/acyl sn-1/ether
                            {
                                for (double m = paraMap["GP.LowerLengthLim1"]; m<= paraMap["GP.UpperLengthLim1"]; m++)
                                {
                                    for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                                    {
                                        if ((m < 2) || (n > m/2))
                                        {
                                            break;
                                        }
                                        if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                        {
                                            break;
                                        }
                                        stringstream FA1formula;
                                        double FA1mass;
                                        FA1formula << "C" << m << "H" << 2*m+1-2*n;
                                        int ca2 = ca1;
                                        int hy2 = hy1;
                                        int ox2 = ox1;
                                        ca2 += m;
                                        hy2 += 2*m+1-2*n;
                                        FA1mass = m*C + (2*m+1)*H - 2*n*H;
                                        string bond1 = "ether";
                                        string bondDes1 = "O-";
                                        stringstream designationE;
                                        designationE << designation.str();
                                        designationE << bondDes1 << m << ":" << n << "/" << j << ":" << k << ")";
                                
                                        stringstream lipidFormula;
                                        lipidFormula << "C" << ca2 << "H" << hy2;
                                        if (ni != 0)
                                        {
                                            if (ni == 1)
                                            {
                                                lipidFormula << "N";
                                            }
                                            if (ni > 1)
                                            {
                                                lipidFormula << "N" << ni;
                                            }
                                        
                                        }
                                        if (ox2 == 1)
                                        {
                                            lipidFormula << "O";    
                                        }
                                        if (ox2 > 1)
                                        {
                                            lipidFormula << "O" << ox2;    
                                        }
                                        lipidFormula << "P";

                                        Lipid lipid;
                                
                                        lipid.name = designationE.str();
                                        lipid.decoy = false;
                                        lipid.totalMass = back[0].mass + head[i].mass + FA1mass + FA2mass;
                                        lipid.lipidFormula = lipidFormula.str();
                                        lipid.backbone = back[0].name;
                                        lipid.backboneFormula = back[0].formula;
                                        lipid.backboneMass = back[0].mass;
                                        lipid.headGroup = head[i].name;
                                        lipid.headGroupFormula = head[i].formula;
                                        lipid.headGroupMass = head[i].mass;
                                        lipid.FA1 = FA1formula.str();
                                        lipid.FA1Mass = FA1mass;
                                        lipid.FA1DoubleBonds = n;
                                        lipid.SN1bond = bond1;
                                        lipid.FA2 = FA2formula.str();
                                        lipid.FA2Mass = FA2mass;
                                        lipid.FA2DoubleBonds = k;
                                        lipid.SN2bond = bond2;
                                        (*count)++;
                                        if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                        {
                                            cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                                        }
                                        
                                        precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                                    }
                                }
                            }
                        }
                    }
                }
                if (paraMap["GP.EtherBond2"] == 1)    // sn-2/ether
                {
                    stringstream designation;
                    designation << head[i].common << "(";

                    for (double j = paraMap["GP.LowerLengthLim2"]; j <= paraMap["GP.UpperLengthLim2"]; j++)
                    {
                        for (double k = paraMap["GP.LowerDoubleBondLim2"]; k <= paraMap["GP.UpperDoubleBondLim2"]; k++)
                        {
                            if ((j < 2) || (k > j/2))
                            {
                                break;
                            }
                            if ((paraMap["GP.evenOnly"] == 1) && (int(j) % 2 != 0))
                            {
                                break;
                            }
                            stringstream FA2formula;
                            double FA2mass;
                            FA2formula << "C" << j << "H" << 2*j+1-2*k;
                            int ca1 = ca;
                            int hy1 = hy;
                            int ox1 = ox;
                            ca1 += j;
                            hy1 += 2*j+1-2*k;
                            FA2mass = j*C + (2*j+1)*H - 2*k*H;
                            string bond2 = "ether";

                            if (paraMap["GP.Lyso1"] == 1)
                            {
                                stringstream FA1formula;
                                double FA1mass;
                                FA1formula << "H";
                                int ca2 = ca1;
                                int hy2 = hy1;
                                int ox2 = ox1;
                                hy2 += 1;
                                FA1mass = H;
                                string bond1 = "lyso";
                                stringstream designationA;
                                designationA /*<< "Lyso"*/ << designation.str();
                                designationA << 0 << ":" << 0 << "/" << "O-" << j << ":" << k << ")";
                                
                                stringstream lipidFormula;
                                lipidFormula << "C" << ca2 << "H" << hy2;
                                if (ni != 0)
                                {
                                    if (ni == 1)
                                    {
                                        lipidFormula << "N";
                                    }
                                    if (ni > 1)
                                    {
                                        lipidFormula << "N" << ni;
                                    }
                                        
                                }
                                if (ox2 == 1)
                                {
                                    lipidFormula << "O";    
                                }
                                if (ox2 > 1)
                                {
                                    lipidFormula << "O" << ox2;    
                                }
                                lipidFormula << "P";

                                Lipid lipid;
                                                                
                                lipid.name = designationA.str();
                                lipid.decoy = false;
                                lipid.totalMass = back[0].mass + head[i].mass + FA1mass + FA2mass;
                                lipid.lipidFormula = lipidFormula.str();
                                lipid.backbone = back[0].name;
                                lipid.backboneFormula = back[0].formula;
                                lipid.backboneMass = back[0].mass;
                                lipid.headGroup = head[i].name;
                                lipid.headGroupFormula = head[i].formula;
                                lipid.headGroupMass = head[i].mass;
                                lipid.FA1 = FA1formula.str();
                                lipid.FA1Mass = FA1mass;
                                lipid.FA1DoubleBonds = 0;
                                lipid.SN1bond = bond1;
                                lipid.FA2 = FA2formula.str();
                                lipid.FA2Mass = FA2mass;
                                lipid.FA2DoubleBonds = k;
                                lipid.SN2bond = bond2;
                                (*count)++;
                                if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                {
                                    cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "% done";
                                }
                                
                                precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                            }

                            if (paraMap["GP.AcylBond1"] == 1)    // sn-2/ether sn-1/acyl
                            {
                                for (double m = paraMap["GP.LowerLengthLim1"]; m <= paraMap["GP.UpperLengthLim1"]; m++)
                                {
                                    for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                                    {
                                        if ((m < 2) || (n > (m - 1)/2))
                                        {
                                            break;
                                        }
                                        if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                        {
                                            break;
                                        }
                                        stringstream FA1formula;
                                        double FA1mass;
                                        FA1formula << "C" << m << "H" << 2*(m-1)+1-2*n << "O";
                                        int ca2 = ca1;
                                        int hy2 = hy1;
                                        int ox2 = ox1;
                                        ca2 += m;
                                        hy2 += 2*(m-1)+1-2*n;
                                        ox2 += 1;
                                        FA1mass = m*C + (2*(m-1)+1)*H - 2*n*H + O;
                                        string bond1 = "acyl";
                                        stringstream designationA;
                                        designationA << designation.str();
                                        designationA << m << ":" << n << "/" << "O-" << j << ":" << k << ")";
                                
                                        stringstream lipidFormula;
                                        lipidFormula << "C" << ca2 << "H" << hy2;
                                        if (ni != 0)
                                        {
                                            if (ni == 1)
                                            {
                                                lipidFormula << "N";
                                            }
                                            if (ni > 1)
                                            {
                                                lipidFormula << "N" << ni;
                                            }
                                        
                                        }
                                        if (ox2 == 1)
                                        {
                                            lipidFormula << "O";    
                                        }
                                        if (ox2 > 1)
                                        {
                                            lipidFormula << "O" << ox2;    
                                        }
                                        lipidFormula << "P";

                                        Lipid lipid;
                                                                
                                        lipid.name = designationA.str();
                                        lipid.decoy = false;
                                        lipid.totalMass = back[0].mass + head[i].mass + FA1mass + FA2mass;
                                        lipid.lipidFormula = lipidFormula.str();
                                        lipid.backbone = back[0].name;
                                        lipid.backboneFormula = back[0].formula;
                                        lipid.backboneMass = back[0].mass;
                                        lipid.headGroup = head[i].name;
                                        lipid.headGroupFormula = head[i].formula;
                                        lipid.headGroupMass = head[i].mass;
                                        lipid.FA1 = FA1formula.str();
                                        lipid.FA1Mass = FA1mass;
                                        lipid.FA1DoubleBonds = n;
                                        lipid.SN1bond = bond1;
                                        lipid.FA2 = FA2formula.str();
                                        lipid.FA2Mass = FA2mass;
                                        lipid.FA2DoubleBonds = k;
                                        lipid.SN2bond = bond2;
                                        (*count)++;
                                        if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                        {
                                            cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                                        }
                                        
                                        precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                                    }
                                }
                            }
                            if (paraMap["GP.EtherBond1"] == 1)    // sn-2/ether sn-1/ether
                            {
                                for (double m = paraMap["GP.LowerLengthLim1"]; m<= paraMap["GP.UpperLengthLim1"]; m++)
                                {
                                    for (double n = paraMap["GP.LowerDoubleBondLim1"]; n <= paraMap["GP.UpperDoubleBondLim1"]; n++)
                                    {
                                        if ((m < 2) || (n > m/2))
                                        {
                                            break;
                                        }
                                        if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                                        {
                                            break;
                                        }
                                        stringstream FA1formula;
                                        double FA1mass;
                                        FA1formula << "C" << m << "H" << 2*m+1-2*n;
                                        int ca2 = ca1;
                                        int hy2 = hy1;
                                        int ox2 = ox1;
                                        ca2 += m;
                                        hy2 += 2*m+1-2*n;
                                        FA1mass = m*C + (2*m+1)*H - 2*n*H;
                                        string bond1 = "ether";
                                        string bondDes1 = "O-";
                                        stringstream designationE;
                                        designationE << designation.str();
                                        designationE << bondDes1 << m << ":" << n << "/" << "O-" << j << ":" << k << ")";
                                
                                        stringstream lipidFormula;
                                        lipidFormula << "C" << ca2 << "H" << hy2;
                                        if (ni != 0)
                                        {
                                            if (ni == 1)
                                            {
                                                lipidFormula << "N";
                                            }
                                            if (ni > 1)
                                            {
                                                lipidFormula << "N" << ni;
                                            }
                                        
                                        }
                                        if (ox2 == 1)
                                        {
                                            lipidFormula << "O";    
                                        }
                                        if (ox2 > 1)
                                        {
                                            lipidFormula << "O" << ox2;    
                                        }
                                        lipidFormula << "P";

                                        Lipid lipid;
                                
                                        lipid.name = designationE.str();
                                        lipid.decoy = false;
                                        lipid.totalMass = back[0].mass + head[i].mass + FA1mass + FA2mass;
                                        lipid.lipidFormula = lipidFormula.str();
                                        lipid.backbone = back[0].name;
                                        lipid.backboneFormula = back[0].formula;
                                        lipid.backboneMass = back[0].mass;
                                        lipid.headGroup = head[i].name;
                                        lipid.headGroupFormula = head[i].formula;
                                        lipid.headGroupMass = head[i].mass;
                                        lipid.FA1 = FA1formula.str();
                                        lipid.FA1Mass = FA1mass;
                                        lipid.FA1DoubleBonds = n;
                                        lipid.SN1bond = bond1;
                                        lipid.FA2 = FA2formula.str();
                                        lipid.FA2Mass = FA2mass;
                                        lipid.FA2DoubleBonds = k;
                                        lipid.SN2bond = bond2;
                                        (*count)++;
                                        if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                        {
                                            cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                                        }
                                        
                                        precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
    
    if (paraMap["SP"] == 1)
    { 
        int ca, hy, ni, ox, ph;
        for (size_t i = 2; i < back.size(); i++)
        {
            ca = back[i].ca;
            hy = back[i].hy;
            ni = back[i].ni;
            ox = back[i].ox;
            ph = 0;
            for (int j = 0; j < 3; j++)
            {
                if (sphingoHead[j].search == 1)
                {

                    if (sphingoHead[j].name == "Choline")
                    {
                        ca += 5;
                        hy += 13;
                        ox += 3;
                        ph += 1;
                        ni += 1;
                    }
                    if (sphingoHead[j].name == "Ethanolamine")
                    {
                        ca += 2;
                        hy += 7;
                        ox += 3;
                        ph += 1;
                        ni += 1;
                    }
                    if (sphingoHead[j].name == "Inositol")
                    {
                        ca += 6;
                        hy += 12;
                        ox += 8;
                        ph += 1;
                        ni += 0;
                    }

                    stringstream designation;
                    //designation << sphingoHead[j].common << "(d" << back[i].length << ":";

                    if (back[i].formula == "Sphingosine")
                    {
                        designation << sphingoHead[j].common << "(d" << back[i].length << ":" << "1/";
                    }
                    if (back[i].formula == "Sphinganine")
                    {
                        designation << sphingoHead[j].common << "(d" << back[i].length << ":" << "0/";
                    }
                    if (back[i].formula == "Phytosphingosine")
                    {
                        designation << sphingoHead[j].common << "(t" << back[i].length << ":" << "0/";
                    }
                    if (back[i].formula == "Sphingadienine")
                    {
                        designation << sphingoHead[j].common << "(d" << back[i].length << ":" << "2/";
                    }

                    for (double m = paraMap["SP.LowerLength"]; m <= paraMap["SP.UpperLength"]; m++)
                    {
                        for (double n = paraMap["SP.LowerDoubleBond"]; n <= paraMap["SP.UpperDoubleBond"]; n++)
                        {
                            stringstream FAdesig;

                            int ca1 = ca;
                            int hy1 = hy;
                            int ox1 = ox;

                            FAdesig << designation.str() << m << ":" << n << ")";
                            if ((m < 2) || (n > (m - 1)/2))
                            {
                                break;
                            }
                            /*if ((paraMap["GP.evenOnly"] == 1) && (int(m) % 2 != 0))
                            {
                                break;
                            }*/
                            stringstream FAformula;
                            double FAmass;
                            if (m == 0)
                            {
                                FAformula << "H";
                                FAmass = H;
                                hy1 += 1;

                            }
                            else if (m == 1)
                            {
                                FAformula << "C" << "O" << "H";
                                FAmass = C + H + O;
                                ca1 += 1;
                                hy1 += 1;
                                ox1 += 1;
                            }
                            else
                            {
                                FAformula << "C" << m << "H" << 2*(m-1)+1-2*n << "O";
                                FAmass = m*C + (2*(m-1)+1)*H - 2*n*H + O;
                                ca1 += m;
                                hy1 += 2*(m-1)+1-2*n;
                                ox1 += 1;
                            }

                            stringstream lipidFormula;
                            lipidFormula << "C" << ca1 << "H" << hy1;
                            if (ni != 0)
                            {
                                if (ni == 1)
                                {
                                    lipidFormula << "N";
                                }
                                if (ni > 1)
                                {
                                    lipidFormula << "N" << ni;
                                }
                                        
                            }
                            if (ox1 == 1)
                            {
                                lipidFormula << "O";    
                            }
                            if (ox1 > 1)
                            {
                                lipidFormula << "O" << ox1;    
                            }
                            lipidFormula << "P";

                            Lipid lipid;
                                
                            lipid.name = FAdesig.str();
                            lipid.decoy = false;
                            lipid.totalMass = back[i].mass + sphingoHead[j].mass + FAmass;
                            lipid.lipidFormula = lipidFormula.str();
                            lipid.backbone = back[i].name;
                            lipid.backboneFormula = back[i].formula;
                            lipid.backboneMass = back[i].mass;
                            lipid.headGroup = sphingoHead[j].name;
                            lipid.headGroupFormula = sphingoHead[j].formula;
                            lipid.headGroupMass = sphingoHead[j].mass;
                            lipid.FA1 = FAformula.str();
                            lipid.FA1Mass = FAmass;
                            lipid.FA1DoubleBonds = n;
                            lipid.FA2 = "-";
                            lipid.FA2Mass = 0;
                            lipid.FA2DoubleBonds = 0;
                            lipid.FA3 = "-";
                            lipid.FA3Mass = 0;
                            lipid.FA3DoubleBonds = 0;
                            lipid.FA4 = "-";
                            lipid.FA4Mass = 0;
                            lipid.FA4DoubleBonds = 0;
                            (*count)++;
                            if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                            {
                                cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                            }
                            
                            precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                        }
                    }                
                }
            }
        }
    }    
    
    if (paraMap["CL"] == 1)
    { 
        int ca, hy, ox; //ph;
        for  (int l = 0; l < 1; l++)
        {
            if (cardioHead[l].search == 1)
            {
                ca = 9;
                hy = 18;
                ox = 13;
                //ph = 2;

                if (paraMap["CL.Acyl"] == 1)
                {
                    size_t L1;
                    if ((paraMap["CL.Lyso1"] == 1) || (paraMap["CL.LowerLengthLim1a"] < 0))
                    {
                        L1 = 0;
                    }
                    else
                    {
                        L1 = paraMap["CL.LowerLengthLim1a"];
                    }
                    for (size_t j = L1; j <= paraMap["CL.UpperLengthLim1a"]; j++)
                    {
                        if ((j != 0) && (j < paraMap["CL.LowerLengthLim1a"]))
                        {
                            continue;
                        }
                        for (size_t k = paraMap["CL.LowerDoubleBondLim1a"]; k <= paraMap["CL.UpperDoubleBondLim1a"]; k++)
                        {
                            if (k > (j - 1)/2)
                            {
                                break;
                            }
                            if ((paraMap["CL.evenOnly"] == 1) && (int(j) % 2 != 0))
                            {
                                break;
                            }
                            stringstream FA1formula;
                            double FA1mass;
                            string bond1 = "acyl";
                            int ca1 = ca;
                            int hy1 = hy;
                            int ox1 = ox;
                            if (j == 0)
                            {
                                FA1formula << "H";
                                FA1mass = H;
                                hy1 += 1;
                            }
                            //else if (j == 1)
                            //{
                                //FA1formula << "C" << "O" << "H";
                                //FA1mass = C + H + O;
                            //}
                            else
                            {
                                FA1formula << "C" << j << "H" << 2*(j-1)+1-2*k << "O";
                                FA1mass = j*C + (2*(j-1)+1)*H - 2*k*H + O;
                                ca1 += j;
                                hy1 += 2*(j-1)+1-2*k;
                                ox1 += 1;

                            }

                            size_t L2;
                            if ((paraMap["CL.Lyso2"] == 1) || (paraMap["CL.LowerLengthLim2a"] < 0))
                            {
                                L2 = 0;
                            }
                            else
                            {
                                L2 = paraMap["CL.LowerLengthLim2a"];
                            }
                            
                            for (size_t m = L2; m <= paraMap["CL.UpperLengthLim2a"]; m++)
                            {
                                
                                if ((m != 0) && (m < paraMap["CL.LowerLengthLim2a"]))
                                {
                                    continue;
                                }
                                for (size_t n = paraMap["CL.LowerDoubleBondLim2a"]; n <= paraMap["CL.UpperDoubleBondLim2a"]; n++)
                                {
                                    if (n > (m - 1)/2)
                                    {
                                        break;
                                    }
                                    if ((paraMap["CL.evenOnly"] == 1) && (int(m) % 2 != 0))
                                    {
                                        break;
                                    }
                                    stringstream FA2formula;
                                    double FA2mass;
                                    string bond2 = "acyl";
                                    int ca2 = ca1;
                                    int hy2 = hy1;
                                    int ox2 = ox1;
                                    if (m == 0)
                                    {
                                        FA2formula << "H";
                                        FA2mass = H;
                                        hy2 += 1;
                                    }
                                    //else if (m == 1)
                                    //{
                                        //FA2formula << "C" << "O" << "H";
                                        //FA2mass = C + H + O;
                                    //}
                                    else
                                    {
                                        FA2formula << "C" << m << "H" << 2*(m-1)+1-2*n << "O";
                                        FA2mass = m*C + (2*(m-1)+1)*H - 2*n*H + O;
                                        ca2 += m;
                                        hy2 += 2*(m-1)+1-2*n;
                                        ox2 += 1;
                                    }
                                    
                                    size_t L3;
                                    if ((paraMap["CL.Lyso3"] == 1) || (paraMap["CL.LowerLengthLim1b"] < 0))
                                    {
                                        L3 = 0;
                                    }
                                    else
                                    {
                                        L3 = paraMap["CL.LowerLengthLim1b"];
                                    }
                                    for (size_t s = L3; s<= paraMap["CL.UpperLengthLim1b"]; s++)
                                    {
                                        if ((s != 0) && (s < paraMap["CL.LowerLengthLim1b"]))
                                        {
                                            continue;
                                        }
                                        for (size_t t = paraMap["CL.LowerDoubleBondLim1b"]; t <= paraMap["CL.UpperDoubleBondLim1b"]; t++)
                                        {
                                            if (t > (s - 1)/2)
                                            {
                                                break;
                                            }
                                            if ((paraMap["CL.evenOnly"] == 1) && (int(s) % 2 != 0))
                                            {
                                                break;
                                            }
                                            stringstream FA3formula;
                                            double FA3mass;
                                            string bond3 = "acyl";
                                            int ca3 = ca2;
                                            int hy3 = hy2;
                                            int ox3 = ox2;
                                            if (s == 0)
                                            {
                                                FA3formula << "H";
                                                FA3mass = H;
                                                hy3 += 1;
                                            }
                                            //else if (s == 1)
                                            //{
                                                //FA3formula << "C" << "O" << "H";
                                                //FA3mass = C + H + O;
                                            //}
                                            else
                                            {
                                                FA3formula << "C" << s << "H" << 2*(s-1)+1-2*t << "O";
                                                FA3mass = s*C + (2*(s-1)+1)*H - 2*t*H + O;
                                                ca3 += s;
                                                hy3 += 2*(s-1)+1-2*t;
                                                ox3 += 1;
                                            }

                                            size_t L4;
                                            if ((paraMap["CL.Lyso4"] == 1) || (paraMap["CL.LowerLengthLim2b"] < 0))
                                            {
                                                L4 = 0;
                                            }
                                            else
                                            {
                                                L4 = paraMap["CL.LowerLengthLim2b"];
                                            }
                                            for (size_t u = L4; u<= paraMap["CL.UpperLengthLim2b"]; u++)
                                            {
                                            if ((u != 0) && (u < paraMap["CL.LowerLengthLim2b"]))
                                            {
                                                continue;
                                            }
                                                for (size_t v = paraMap["CL.LowerDoubleBondLim2b"]; v <= paraMap["CL.UpperDoubleBondLim2b"]; v++)
                                                {
                                                    if (v > (u - 1)/2)
                                                    {
                                                        break;
                                                    }
                                                    if ((paraMap["CL.evenOnly"] == 1) && (int(u) % 2 != 0))
                                                    {
                                                        break;
                                                    }
                                                    stringstream FA4formula;
                                                    double FA4mass;
                                                    string bond4 = "acyl";
                                                    int ca4 = ca3;
                                                    int hy4 = hy3;
                                                    int ox4 = ox3;
                                                    if (u == 0)
                                                    {
                                                        FA4formula << "H";
                                                        FA4mass = H;
                                                        hy4 += 1;
                                                    }
                                                    //else if (u == 1)
                                                    //{
                                                        //FA4formula << "C" << "O" << "H";
                                                        //FA4mass = C + H + O;
                                                    //}
                                                    else
                                                    {
                                                        FA4formula << "C" << u << "H" << 2*(u-1)+1-2*v << "O";
                                                        FA4mass = u*C + (2*(u-1)+1)*H - 2*v*H + O;
                                                        ca4 += u;
                                                        hy4 += 2*(u-1)+1-2*v;
                                                        ox4 += 1;
                                                    }

                                                    stringstream designation;
                                                    designation << "CL" << "(" << j << ":" << k << "/" << m << ":" << n << "/" << s << ":" << t << "/" << u << ":" << v << ")";

                                                    if (FA1mass != H || FA2mass != H || FA3mass != H || FA4mass != H)
                                                    {
                                                        bool useLipid = true;
                                                        
                                                        vector<size_t> clvR(8);
                                                        clvR[0] = s;
                                                        clvR[1] = t;
                                                        clvR[2] = u;
                                                        clvR[3] = v;
                                                        clvR[4] = j;
                                                        clvR[5] = k;
                                                        clvR[6] = m;
                                                        clvR[7] = n;

                                                        size_t CLsize = CLspecies.size();
                                                        for (size_t c = 0; c<CLsize; c++)
                                                        {
                                                            if (CLspecies[c] == clvR)
                                                            {
                                                                useLipid = false;
                                                                break;
                                                            }
                                                        }                                                        
                                                                                                        
                                                        if (useLipid == true)
                                                        {
                                                            vector<size_t> clv(8);
                                                            clv[0] = j;
                                                            clv[1] = k;
                                                            clv[2] = m;
                                                            clv[3] = n;
                                                            clv[4] = s;
                                                            clv[5] = t;
                                                            clv[6] = u;
                                                            clv[7] = v;
                                                            CLspecies.push_back(clv);

                                                            stringstream lipidFormula;
                                                            lipidFormula << "C" << ca4 << "H" << hy4;

                                                            if (ox4 == 1)
                                                            {
                                                                lipidFormula << "O";    
                                                            }
                                                            if (ox4 > 1)
                                                            {
                                                                lipidFormula << "O" << ox4;    
                                                            }
                                                            lipidFormula << "P" << 2;

                                                            Lipid lipid;
                                
                                                            lipid.name = designation.str();
                                                            lipid.decoy = false;
                                                            lipid.totalMass = back[1].mass + cardioHead[l].mass + FA1mass + FA2mass + FA3mass + FA4mass;
                                                            lipid.lipidFormula = lipidFormula.str();
                                                            lipid.backbone = back[1].name;
                                                            lipid.backboneFormula = back[1].formula;
                                                            lipid.backboneMass = back[1].mass;
                                                            lipid.headGroup = cardioHead[l].name;
                                                            lipid.headGroupFormula = cardioHead[l].formula;
                                                            lipid.headGroupMass = cardioHead[l].mass;
                                                            lipid.FA1 = FA1formula.str();
                                                            lipid.FA1Mass = FA1mass;
                                                            lipid.FA1DoubleBonds = k;
                                                            lipid.SN1bond = bond1;
                                                            lipid.FA2 = FA2formula.str();
                                                            lipid.FA2Mass = FA2mass;
                                                            lipid.FA2DoubleBonds = n;
                                                            lipid.SN2bond = bond2;
                                                            lipid.FA3 = FA3formula.str();
                                                            lipid.FA3Mass = FA3mass;
                                                            lipid.FA3DoubleBonds = t;
                                                            lipid.SN3bond = bond3;
                                                            lipid.FA4 = FA4formula.str();
                                                            lipid.FA4Mass = FA4mass;
                                                            lipid.FA4DoubleBonds = v;
                                                            lipid.SN4bond = bond4;
                                                            (*count)++;
                                                            if (round(100.0*(*count)/lipCount) > round(100.0*((*count)-1)/lipCount))
                                                            {
                                                                cout << string(70, '\b') << "Comparing experimental and theoretical spectra. Progress...... " << round(100.0*(*count)/lipCount) << "%";
                                                            }
                                                        
                                                            precursorListConstruction(adducts, lipid, paraMap, spectraHolder, SL, pre);
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

// precursor list construction
void precursorListConstruction(vector<Adduct> adducts, Lipid lipid, Map & paraMap, vector<SpectraHolder> spectraHolder, const SpectrumListPtr& SL, int* pre)
{
    int index = 0; 
    string backsub = lipid.backbone.substr(5, 6);
    if (lipid.backbone == "Glycerol" || backsub == "Sphing" || backsub == "Phytos")
    {
        for (size_t i=0; i<adducts.size(); i++)
        {
            if ((paraMap["ESI.pos"] == 1) && (adducts[i].polarity == "positive"))
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = adducts[i].description;
                lipid.precursors[index].adduct = adducts[i].name;
                lipid.precursors[index].adductMass = adducts[i].mass;
                lipid.precursors[index].adductType = adducts[i].type;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + adducts[i].mass;
                index++;
                //(*pre)++; 
            }
        }
        /*
        if (paraMap["ESI.pos"] == 1) //positive mode
        {
            if (paraMap["Add.Pro"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "protonated";
                lipid.precursors[index].adduct = "None";
                lipid.precursors[index].adductMass = 0;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + H;
                index++;
                //(*pre)++;
            }

            if (paraMap["Add.Na"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "Sodium adduct";
                lipid.precursors[index].adduct = "Na";
                lipid.precursors[index].adductMass = Na;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + Na;
                index++;
                //(*pre)++;
            }

            if (paraMap["Add.NH4"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "Ammonium adduct";
                lipid.precursors[index].adduct = "NH4";
                lipid.precursors[index].adductMass = N + 4*H;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + N + 4*H;
                index++;
                //(*pre)++;
            }

            if (paraMap["Add.Li"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "Lithium adduct";
                lipid.precursors[index].adduct = "Li";
                lipid.precursors[index].adductMass = Li;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + Li;
                index++;
                //(*pre)++;
                
            }

            if (paraMap["Add.K"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "Potassium adduct";
                lipid.precursors[index].adduct = "K";
                lipid.precursors[index].adductMass = K;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + K;
                index++;
                //(*pre)++;
            }
        }
        */
        if (paraMap["ESI.neg"] == 1) //negative mode
        {
            
            if (paraMap["Add.Depro"] == 1 && lipid.headGroup != "Choline")
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "deprotonated";
                lipid.precursors[index].adduct = "-H";
                lipid.precursors[index].adductMass = 0;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - H;
                index++;
                //(*pre)++;
            }
            
            if (paraMap["Add.Depro"] == 1 && lipid.headGroup == "PIP")
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "deprotonatedX2";
                lipid.precursors[index].adduct = "-2H";
                lipid.precursors[index].adductMass = 0;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = (lipid.totalMass - 2*H)/2;
                index++;
                //(*pre)++;
            }
            
            if (paraMap["Add.Depro"] == 1 && lipid.headGroup == "PIP2")
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "deprotonatedX2";
                lipid.precursors[index].adduct = "-2H";
                lipid.precursors[index].adductMass = 0;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = (lipid.totalMass - 2*H)/2;
                index++;
                //(*pre)++;
            }
            /*
            if (paraMap["Add.Depro"] == 1 && lipid.headGroup == "PIP3")
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "deprotonatedX2";
                lipid.precursors[index].adduct = "-2H";
                lipid.precursors[index].adductMass = 0;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = (lipid.totalMass - 2*H)/2;
                index++;
                //(*pre)++;
            }
            */
            if (paraMap["Add.Cl"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "Cl adduct";
                lipid.precursors[index].adduct = "+Cl";
                lipid.precursors[index].adductMass = Cl;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass + Cl;
                index++;
                //(*pre)++;
            }

            if (paraMap["Add.HCOO"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "formate adduct";
                lipid.precursors[index].adduct = "+HCO2";
                lipid.precursors[index].adductMass = C + H + 2*O;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass + C + H + 2*O;
                index++;
                //(*pre)++;
            }

            if (paraMap["Add.CH3COO"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "acetate adduct";
                lipid.precursors[index].adduct = "+CH3CO2";
                lipid.precursors[index].adductMass = 2*C + 3*H + 2*O;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass + 2*C + 3*H + 2*O;
                index++;
                //(*pre)++;
            }

            if (lipid.headGroup == "Choline" && (paraMap["Add.Cl"] == 1 || paraMap["Add.CH3COO"] == 1 || paraMap["Add.HCOO"] == 1))
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "loss of adduct and methyl group";
                lipid.precursors[index].adduct = "-CH3";
                lipid.precursors[index].adductMass = 0;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - C - 3*H;
                index++;
                //(*pre)++;
            }
            /*
            if (lipid.headGroup == "Glycerol")
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "deprotonated; loss of H2O";
                lipid.precursors[index].adduct = "none";
                lipid.precursors[index].adductMass = 0;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - 3*H - O;
                index++;
                //(*pre)++;
            }*/
        }
    }
    
    if (lipid.backbone == "Cardiolipin")
    {
        if (paraMap["ESI.pos"] == 1) //positive mode
        {
            if (paraMap["Add.Pro"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "protonated";
                lipid.precursors[index].adduct = "+H";
                lipid.precursors[index].adductMass = H;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + H;
                index++;
                //(*pre)++;
            }

            if (paraMap["Add.Na"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "+ Na";
                lipid.precursors[index].adduct = "+Na";
                lipid.precursors[index].adductMass = Na;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + Na;
                index++;
                //(*pre)++;

                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "- H + 2Na";
                lipid.precursors[index].adduct = "+2Na-H";
                lipid.precursors[index].adductMass = Na;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + 2*Na - H;
                index++;
                //(*pre)++;

                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "- 2H + 3Na";
                lipid.precursors[index].adduct = "-2H + 3Na";
                lipid.precursors[index].adductMass = Na;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + 3*Na - 2*H;
                index++;
                //(*pre)++;
            }

            if (paraMap["Add.K"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "+ K";
                lipid.precursors[index].adduct = "+K";
                lipid.precursors[index].adductMass = K;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + K;
                index++;
                //(*pre)++;

                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "- H + 2K";
                lipid.precursors[index].adduct = "+2K-H";
                lipid.precursors[index].adductMass = K;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + 2*K - H;
                index++;
                //(*pre)++;

                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "- 2H + 3K";
                lipid.precursors[index].adduct = "-2H + 3K";
                lipid.precursors[index].adductMass = K;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + 3*K - 2*H;
                index++;
                //(*pre)++;
            }

            if (paraMap["Add.Li"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "+ Li";
                lipid.precursors[index].adduct = "+Li";
                lipid.precursors[index].adductMass = Li;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + Li;
                index++;
                //(*pre)++;

                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "- H + 2Li";
                lipid.precursors[index].adduct = "+2Li-H";
                lipid.precursors[index].adductMass = Li;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + 2*Li - H;
                index++;
                //(*pre)++;

                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "- 2H + 3Li";
                lipid.precursors[index].adduct = "-2H + 3Li";
                lipid.precursors[index].adductMass = Li;
                lipid.precursors[index].ESI = "pos";
                lipid.precursors[index].precursorMass = lipid.totalMass + 3*Li - 2*H;
                index++;
                //(*pre)++;
            }
        }

        if (paraMap["ESI.neg"] == 1) //negative mode
        {
            if (paraMap["Add.Depro"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "deprotonated";
                lipid.precursors[index].adduct = "-H";
                lipid.precursors[index].adductMass = 0;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - H;
                index++;
                (*pre)++;

                if (paraMap["CL.double"] == 1)
                {
                    lipid.precursors.push_back(PreCursor());
                    lipid.precursors[index].description = "deprotonatedX2";
                    lipid.precursors[index].adduct = "-2H";
                    lipid.precursors[index].adductMass = 0;
                    lipid.precursors[index].ESI = "neg";
                    lipid.precursors[index].precursorMass = (lipid.totalMass - 2*H)/2;
                    index++;                
                    (*pre)++;
                }
            }
            
            if (paraMap["Add.Na"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "-2H + Na";
                lipid.precursors[index].adduct = "-2H + Na";
                lipid.precursors[index].adductMass = Na;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - 2*H + Na;
                index++;
                (*pre)++;

                /*
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "-3H + 2Na";
                lipid.precursors[index].adduct = "2Na";
                lipid.precursors[index].adductMass = 2*Na;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - 3*H + 2*Na;
                index++;
                (*pre)++;
                */
            } 

            if (paraMap["Add.K"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "-2H + K";
                lipid.precursors[index].adduct = "-2H + K";
                lipid.precursors[index].adductMass = K;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - 2*H + K;
                index++;
                (*pre)++;

                /*
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "-3H + 2K";
                lipid.precursors[index].adduct = "2K";
                lipid.precursors[index].adductMass = 2*K;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - 3*H + 2*K;
                index++;
                (*pre)++;
                */
            } 

            if (paraMap["Add.Li"] == 1)
            {
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "-2H + Li";
                lipid.precursors[index].adduct = "-2H + Li";
                lipid.precursors[index].adductMass = Li;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - 2*H + Li;
                index++;
                (*pre)++;

                /*
                lipid.precursors.push_back(PreCursor());
                lipid.precursors[index].description = "-3H + 2Li";
                lipid.precursors[index].adduct = "2Li";
                lipid.precursors[index].adductMass = 2*Li;
                lipid.precursors[index].ESI = "neg";
                lipid.precursors[index].precursorMass = lipid.totalMass - 3*H + 2*Li;
                index++;
                (*pre)++;
                */
            } 
        }
    }

    fragmentListConstruction(lipid, paraMap, spectraHolder, SL);
}

// fragment list construction
void fragmentListConstruction(Lipid lipid, Map & paraMap, vector<SpectraHolder> spectraHolder, const SpectrumListPtr& SL)
{
    for (size_t j=0; j<lipid.precursors.size(); j++)
    {
        int index = 0;
        if (lipid.backbone == "Glycerol")
        {
            if (lipid.precursors[j].ESI == "pos")
            {
                if (lipid.precursors[j].adductType == "nonMetal")
                {
                    if (lipid.headGroup == "Choline")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "choline";
                        lipid.precursors[j].fragments[index].fragmentMass = N + 5*C + 12*H;
                        index++;
        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphocholine head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 5*C + N + 15*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }
                    }
                    if (lipid.headGroup == "Ethanolamine")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;
        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 2*C + N + 9*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }
                    }

                    if (lipid.headGroup == "Serine")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;
        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoserine head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 6*O + P + 3*C + N + 9*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }
                    }

                    if (lipid.headGroup == "Glycerol")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoglycerol head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 6*O + P + 3*C + 9*H;
                        index++;
                        */
                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }
                    }

                    if (lipid.headGroup == "Inositol")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 9*O + P + 6*C + 13*H;
                        index++;
                        */
                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }
                    }


                    if (lipid.headGroup == "Phosphate")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphate head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 3*H;
                        index++;
                        */
                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }
                    }

                    if (lipid.headGroup == "PIP")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 9*O + P + 6*C + 13*H;
                        index++;
                        */
                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }                        
                    }

                    if (lipid.headGroup == "PIP2")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 9*O + P + 6*C + 13*H;
                        index++;
                        */
                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }                            
                    }

                    if (lipid.headGroup == "PIP3")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 9*O + P + 6*C + 13*H;
                        index++;
                        */
                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                            index++;
                        }                            
                    }
                }

                if (lipid.precursors[j].adductType == "Metal")
                {
                    if (lipid.headGroup != "Choline")
                    {
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoric acid & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 3*H + lipid.precursors[j].adductMass;
                        index++;
                        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "glycerophosphoric acid & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 5*O + P + 7*H + 3*C + lipid.precursors[j].adductMass;
                        index++;
                        */
                    }

                    if (lipid.SN1bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                        index++;
                        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                        index++;
                        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                        index++;
                    }

                    if (lipid.SN2bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                        index++;
                        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                        index++;
                        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                        index++;
                    }    

                    if (lipid.headGroup == "Choline")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "choline";
                        lipid.precursors[j].fragments[index].fragmentMass = N + 5*C + 12*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "PO4C2H5 & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 2*C + 5*H + lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;
                        */
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphocholine & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphocholine & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass -H + lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of choline";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                        index++;
                        */
                        if (lipid.SN1bond == "acyl")
                        {

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid; loss of adduct";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H - lipid.precursors[j].adductMass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of choline; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of choline & adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H - lipid.precursors[j].adductMass + H;
                            index++;
                            /*
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of choline; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                            */
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H - lipid.FA1Mass - O - H;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            /*
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of choline; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of choline; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;
                            */
                            //if (lipid.FA2DoubleBonds > 2)
                            //{
                                lipid.precursors[j].fragments.push_back(Fragment());
                                lipid.precursors[j].fragments[index].fragType = "HF2";
                                lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine & adduct; loss of FA2 as ketene";
                                lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                                index++;
                            //}

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "F2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid; loss of adduct";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H - lipid.precursors[j].adductMass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H - lipid.FA2Mass - O - H;
                            index++;


                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphocholine; loss of FA2 as carboxylic acid (additional double bond)";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.FA2Mass - O;
                            index++;
                            
                        }    
                    }

                    if (lipid.headGroup == "Ethanolamine")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine and adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine and adduct; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass + H;
                            index++;
                            
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine and adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;
                            
                            //if (lipid.FA2DoubleBonds > 2)
                            //{
                                lipid.precursors[j].fragments.push_back(Fragment());
                                lipid.precursors[j].fragments[index].fragType = "HF2";
                                lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine and adduct; loss of FA2 as ketene";
                                lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                                index++;
                            //}
                        }    
                    }
                    
                    if (lipid.headGroup == "Serine")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoric acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - P - 4*O - 3*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoserine & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoserine & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass -H + lipid.precursors[j].adductMass;
                        index++;
                        */
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of serine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine and adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;
                            
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine and adduct; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                            
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass + H;
                            index++;
                            
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine and adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;
                            
                            //if (lipid.FA2DoubleBonds > 2)
                            //{
                                lipid.precursors[j].fragments.push_back(Fragment());
                                lipid.precursors[j].fragments[index].fragType = "HF2";
                                lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoserine and adduct; loss of FA2 as ketene";
                                lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                                index++;
                            //}
                        }    
                    }
                    
                    if (lipid.headGroup == "Glycerol")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoglycerol & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoglycerol & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass -H + lipid.precursors[j].adductMass;
                        index++;
                        */
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol and adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;
                            
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol and adduct; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                            
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass + H;
                            index++;
                            
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol and adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;
                            
                            //if (lipid.FA2DoubleBonds > 2)
                            //{
                                lipid.precursors[j].fragments.push_back(Fragment());
                                lipid.precursors[j].fragments[index].fragType = "HF2";
                                lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoglycerol and adduct; loss of FA2 as ketene";
                                lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                                index++;
                            //}
                        }    
                    }
                    
                    if (lipid.headGroup == "Inositol")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass -H + lipid.precursors[j].adductMass;
                        index++;
                        */
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of inositol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol and adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol and adduct; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol and adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol and adduct; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                            index++;
                        }    
                    }

                    if (lipid.headGroup == "Phosphate")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphate & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphate & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass -H + lipid.precursors[j].adductMass;
                        index++;
                        */

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate and adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate and adduct; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate and adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphate and adduct; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                            index++;
                        }    
                    }




                    if (lipid.headGroup == "PIP")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol phosphate & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol phosphate & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass -H + lipid.precursors[j].adductMass;
                        index++;
                        */
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of inositol phosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol phosphate; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol phosphate; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate and adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate and adduct; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol phosphate; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol phosphate; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate and adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol phosphate and adduct; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                            index++;
                        }    
                    }





                    if (lipid.headGroup == "PIP2")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol bisphosphate & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol bisphosphate & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass -H + lipid.precursors[j].adductMass;
                        index++;
                        */
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of inositol bisphosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol bisphosphate; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol bisphosphate; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate and adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate and adduct; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol bisphosphate; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol bisphosphate; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate and adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol bisphosphate and adduct; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                            index++;
                        }    
                    }



                    if (lipid.headGroup == "PIP3")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate; loss of adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol trisphosphate & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol trisphosphate & adduct - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass -H + lipid.precursors[j].adductMass;
                        index++;
                        */
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of inositol trisphosphate";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol trisphosphate; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol trisphosphate; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA1Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate and adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA1Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate and adduct; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA1Mass;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol trisphosphate; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol trisphosphate; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H - lipid.FA2Mass + H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate and adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass - O - H - lipid.FA2Mass;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoinositol trisphosphate and adduct; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.precursors[j].adductMass + H - lipid.FA2Mass;
                            index++;
                        }    
                    }
                }

                /*
                lipid.precursors[j].fragments.push_back(Fragment());
                lipid.precursors[j].fragments[index].fragType = "H";
                lipid.precursors[j].fragments[index].fragDescription = "ZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZZ";
                lipid.precursors[j].fragments[index].fragmentMass = 999.999;
                index++;

                lipid.precursors[j].fragments.push_back(Fragment());
                lipid.precursors[j].fragments[index].fragType = "H";
                lipid.precursors[j].fragments[index].fragDescription = "loss of head-group";
                lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                index++;

                if (lipid.SN1bond == "acyl")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "HF1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of FA1 as ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + 2*O + 3*C + 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "HF1";
                    lipid.precursors[j].fragments[index].fragDescription = "R1CO";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                    index++;
                }

                if (lipid.SN2bond == "acyl")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "HF2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of FA2 as ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + 2*O + 3*C + 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "HF2";
                    lipid.precursors[j].fragments[index].fragDescription = "R2CO";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                    index++;
                }

                if (lipid.headGroup == "Choline") 
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "choline";
                    lipid.precursors[j].fragments[index].fragmentMass = N + 5*C + 12*H;
                    index++;
        
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphocholine head-group";
                    lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 5*C + N + 15*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H;
                    index++;
                    
                    if (lipid.precursors[j].description == "Lithium adduct")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of Li adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - Li;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of Li adduct; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - Li - lipid.FA1Mass - O - H;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of Li adduct; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - Li - lipid.FA2Mass - O - H;
                            index++;
                        }
                    }
                    
                }

                if (lipid.headGroup == "Ethanolamine") 
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine head-group";
                    lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 2*C + N + 9*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head group; addition of water";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H;
                    index++;

                    if (lipid.SN1bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine; loss of FA1 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H
                                                                                        - lipid.FA1Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine; loss of FA1 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H
                                                                                        - lipid.FA1Mass + H;
                        index++;
                    }

                    if (lipid.SN2bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine; loss of FA2 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H
                                                                                        - lipid.FA2Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine; loss of FA2 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H
                                                                                        - lipid.FA2Mass + H;
                        index++;
                    }

                    if (lipid.precursors[j].description == "Lithium adduct")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of lithiated head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - Li;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "lithiated phosphoethanolamine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O + H + Li;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "lithiated phosphoric acid";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 3*H + Li;
                        index++;
                    }
                }

                if (lipid.headGroup == "Serine") 
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoserine head-group";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O + 2*H;
                    index++;
                    
                    if (lipid.precursors[j].description == "Lithium adduct")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of lithiated head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - Li;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of serine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - N - 5*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA1 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - N - 5*H - lipid.FA1Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA2 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - N - 5*H - lipid.FA2Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoric acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - P - 3*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "lithiated phosphoserine head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O + H + Li;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "lithiated phosphoric acid";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 3*H + Li;
                        index++;
                    }
                }

                if (lipid.headGroup == "Glycerol") 
                {                    
                    if (lipid.precursors[j].description == "Lithium adduct")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of lithiated head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - Li;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - 6*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA1 as a carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - 6*H - lipid.FA1Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA2 as a carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - 6*H - lipid.FA2Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "lithiated phosphoglycerol head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O + H + Li;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "lithiated phosphoric acid";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 3*H + Li;
                        index++;
                    }
                }

                if (lipid.headGroup == "Phosphate") 
                {                    
                    if (lipid.precursors[j].description == "Lithium adduct")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of lithiated head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - Li;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "lithiated phosphoric acid";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 3*H + Li;
                        index++;
                    }
                }
                */
            }

            if (lipid.precursors[j].ESI == "neg")
            {
                if ((lipid.headGroup == "PIP") && (lipid.precursors[j].description == "deprotonated"))
                {
                    if ((lipid.SN1bond != "lyso") && (lipid.SN2bond != "lyso"))
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "B";
                        lipid.precursors[j].fragments[index].fragDescription = "G3P-H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 3*C + 6*H + 5*O + P;
                        index++;
                    }



                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of inositol phosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + P + 3*O + 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "H2PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*H + 4*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = P + 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "HO6P2";
                    lipid.precursors[j].fragments[index].fragmentMass = 6*O + H + 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "H3O7P2";
                    lipid.precursors[j].fragments[index].fragmentMass = 7*O + 3*H + 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HPO3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*O - H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of inositol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of inositol & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 6*O - 12*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - O - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - O - 4*H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol - 3*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 7*H - P - 5*O;
                    index++;

                    if (lipid.SN1bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "HF1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid; loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - O - 2*H - lipid.FA1Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "FA1 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4; loss of FA1 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P - lipid.FA1Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4; loss of FA1 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P - lipid.FA1Mass + H;
                        index++;
                        */
                    }

                    if (lipid.SN2bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "HF2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid; loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - O - 2*H - lipid.FA2Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "FA2 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                        index++;
                        /*
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4; loss of FA2 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P - lipid.FA2Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4; loss of FA2 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P - lipid.FA2Mass + H;
                        index++;
                        */
                    }
                }

                if (lipid.headGroup == "PIP" && lipid.precursors[j].description == "deprotonatedX2")
                {
                    if ((lipid.SN1bond != "lyso") && (lipid.SN2bond != "lyso"))
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "B";
                        lipid.precursors[j].fragments[index].fragDescription = "G3P-H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 3*C + 6*H + 5*O + P;
                        index++;
                    }

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "H2PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*H + 4*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = P + 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 1*H - P - 2*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 3*H - P - 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 5*H - P - 4*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - 3*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 7*H - P - 5*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - P - 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "bisphosphate (2-)";
                    lipid.precursors[j].fragments[index].fragmentMass = (lipid.headGroupMass + O)/2;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "bisphosphate - H2O (2-)";
                    lipid.precursors[j].fragments[index].fragmentMass = (lipid.headGroupMass - 2*H)/2;
                    index++;



                    if (lipid.SN1bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "FA1 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - lipid.FA1Mass - O;
                        index++;

                        //lipid.precursors[j].fragments.push_back(Fragment());
                        //lipid.precursors[j].fragments[index].fragType = "F1";
                        //lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                        //lipid.precursors[j].fragments[index].fragmentMass = (2*(lipid.precursors[j].precursorMass) - lipid.FA1Mass + H)/2;
                        //index++;

                    }

                    if (lipid.SN2bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "FA2 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - lipid.FA2Mass - O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = (2*(lipid.precursors[j].precursorMass) - lipid.FA2Mass + H)/2;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene and PO3";
                        lipid.precursors[j].fragments[index].fragmentMass = 2*((2*(lipid.precursors[j].precursorMass) - lipid.FA2Mass + H)/2) - P - 3*O;
                        index++;
                    }

                    if (lipid.SN1bond == "acyl" && lipid.SN2bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene and FA1 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = 2*((2*(lipid.precursors[j].precursorMass) - lipid.FA2Mass + H)/2) - lipid.FA1Mass - O;
                        index++;
                    }

                }

                if ((lipid.headGroup == "PIP2") && (lipid.precursors[j].description == "deprotonated"))
                {
                    if ((lipid.SN1bond != "lyso") && (lipid.SN2bond != "lyso"))
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "B";
                        lipid.precursors[j].fragments[index].fragDescription = "G3P-H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 3*C + 6*H + 5*O + P;
                        index++;
                    }

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "H2PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*H + 4*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = P + 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "HO6P2";
                    lipid.precursors[j].fragments[index].fragmentMass = 6*O + H + 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "H3O7P2";
                    lipid.precursors[j].fragments[index].fragmentMass = 7*O + 3*H + 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HPO3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*O - H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HPO3 & inositol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 10*H - 9*O - 3*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of 2*HPO3 & inositol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 10*H - 12*O - 4*H - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HPO3 & inositol; addition of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 10*H - 8*O - H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4 & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 5*O - 5*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - P - 2*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - O - 4*H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol trisphosphate + H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + 2*(H + O);
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol trisphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol trisphosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol trisphosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 4*H - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H - P - 3*O - H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - O - 4*H - P - 3*O - H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - 3*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 8*O - 8*H - 2*P;
                    index++;

                    if (lipid.SN1bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "HF1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid; loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - O - 2*H - lipid.FA1Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4 & FA1 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P - lipid.FA1Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "FA1 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + O;
                        index++;
                    }

                    if (lipid.SN2bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "HF2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid; loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - O - 2*H - lipid.FA2Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4 & FA2 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P - lipid.FA2Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "FA2 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + O;
                        index++;
                    }
                }

                if ((lipid.headGroup == "PIP2") && (lipid.precursors[j].description == "deprotonatedX2"))
                {
                    if ((lipid.SN1bond != "lyso") && (lipid.SN2bond != "lyso"))
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "B";
                        lipid.precursors[j].fragments[index].fragDescription = "G3P-H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 3*C + 6*H + 5*O + P;
                        index++;
                    }

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "H2PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*H + 4*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = P + 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "HO6P2";
                    lipid.precursors[j].fragments[index].fragmentMass = 6*O + H + 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 3*H - P - 3*O - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 5*H - P - 4*O - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 3*H - P - 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 5*H - P - 4*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - P - 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of PO3; loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - P - 3*O - 2*H - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HO6P2";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - 2*P - 6*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HO6P2; loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - 2*P - 6*O - 3*H - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of PO3 & inositol; addition of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - 6*C - 10*H - 8*O - P;
                    index++;



                    if (lipid.SN1bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "FA1 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - lipid.FA1Mass - O;
                        index++;

                        //lipid.precursors[j].fragments.push_back(Fragment());
                        //lipid.precursors[j].fragments[index].fragType = "F1";
                        //lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                        //lipid.precursors[j].fragments[index].fragmentMass = (2*(lipid.precursors[j].precursorMass) - lipid.FA1Mass + H)/2;
                        //index++;

                    }

                    if (lipid.SN2bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "FA2 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - lipid.FA2Mass - O;
                        index++;
                    }
                }






                if (lipid.headGroup == "PIP3")
                {
                    if ((lipid.SN1bond != "lyso") && (lipid.SN2bond != "lyso"))
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "B";
                        lipid.precursors[j].fragments[index].fragDescription = "G3P-H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 3*C + 6*H + 5*O + P;
                        index++;
                    }

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "H2PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*H + 4*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = P + 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "HO6P2";
                    lipid.precursors[j].fragments[index].fragmentMass = 6*O + H + 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "H3O7P2";
                    lipid.precursors[j].fragments[index].fragmentMass = 7*O + 3*H + 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HPO3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*O - H - P;
                    index++;
                    /*
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HPO3 & inositol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 10*H - 9*O - 3*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of HPO3 & inositol; addition of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 10*H - 8*O - H - P;
                    index++;
                    */
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of 2*H3PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 2*(4*O + 3*H + P);
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4 & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 5*O - 5*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*(P + 3*O + H) + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*(P + 3*O + H) - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol bisphosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*(P + 3*O + H) - 4*H - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol trisphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - P - 2*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol trisphosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol trisphosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - O - 4*H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol tetraphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol tetraphosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol tetraphosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 4*H - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H - P - 3*O - H - P - 3*O - H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - O - 4*H - P - 3*O - H - P - 3*O - H - P - 3*O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "inositol phosphate - 3*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 8*O - 8*H - 2*P - P - 3*O - H;
                    index++;

                    if (lipid.SN1bond == "acyl")
                    {

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4 & FA1 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P - lipid.FA1Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "FA1 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + O;
                        index++;
                    }

                    if (lipid.SN2bond == "acyl")
                    {

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H3PO4 & FA2 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*O - 3*H - P - lipid.FA2Mass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "FA2 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + O;
                        index++;
                    }
                }














                if (lipid.headGroup != "PIP" && lipid.headGroup != "PIP2" &&  lipid.headGroup != "PIP3")
                {
                    if ((lipid.SN1bond != "lyso") && (lipid.SN2bond != "lyso"))
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "B";
                        lipid.precursors[j].fragments[index].fragDescription = "G3P-H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 3*C + 6*H + 5*O + P;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "B";
                        lipid.precursors[j].fragments[index].fragDescription = "G3P";
                        lipid.precursors[j].fragments[index].fragmentMass = 3*C + 8*H + 6*O + P;
                        index++;
                    }

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "H2PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*H + 4*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = P + 3*O;
                    index++;
            
                    if (lipid.SN1bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "FA1 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F1";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                        index++;
                    }

                    if (lipid.SN2bond == "acyl")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "FA2 carboxylate anion";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F2";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                        index++;
                    }

                    if ((lipid.headGroup == "Choline") && (lipid.precursors[j].adduct != "-CH3"))
                    {    
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "demethylation";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass
                                                                                    - lipid.precursors[j].adductMass - C - 3*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass 
                                                                                    - lipid.precursors[j].adductMass - 3*C - 10*H - N;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass
                                                                                    - lipid.precursors[j].adductMass - 5*C - 12*H - N;
                        index++;
        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "BH";
                        lipid.precursors[j].fragments[index].fragDescription = "GPC-CH3-H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 7*C + 15*H + 5*O + P + N;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "PC-CH3";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*C + 11*H + 4*O + P + N;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "demethylation; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H
                                                                                        - lipid.precursors[j].adductMass - C - 3*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H
                                                                                        - lipid.precursors[j].adductMass - 3*C - 10*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H
                                                                                        - lipid.precursors[j].adductMass - 5*C - 12*H - N;
                            index++;                    

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "demethylation; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - C - 3*H;
                            index++;
                
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - 3*C - 10*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - 5*C - 12*H - N;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "demethylation; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H
                                                                                        - lipid.precursors[j].adductMass - C - 3*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H
                                                                                        - lipid.precursors[j].adductMass - 3*C - 10*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H
                                                                                        - lipid.precursors[j].adductMass - 5*C - 12*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "demethylation; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - C - 3*H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - 3*C - 10*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - 5*C - 12*H - N;
                            index++;
                        }
                    }

                    if ((lipid.headGroup == "Choline") && (lipid.precursors[j].adduct == "-CH3"))
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass 
                                                                                    - lipid.precursors[j].adductMass - 2*C - 7*H - N;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass
                                                                                    - lipid.precursors[j].adductMass - 4*C - 9*H - N;
                        index++;
        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "BH";
                        lipid.precursors[j].fragments[index].fragDescription = "GPC-CH3-H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = 7*C + 15*H + 5*O + P + N;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "PC-CH3";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*C + 11*H + 4*O + P + N;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H
                                                                                        - lipid.precursors[j].adductMass - 2*C - 7*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H
                                                                                        - lipid.precursors[j].adductMass - 4*C - 9*H - N;
                            index++;
                
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - 2*C - 7*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - 4*C - 9*H - N;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H
                                                                                        - lipid.precursors[j].adductMass - 2*C - 7*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H
                                                                                        - lipid.precursors[j].adductMass - 4*C - 9*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - 2*C - 7*H - N;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H
                                                                                        - lipid.precursors[j].adductMass - 4*C - 9*H - N;
                            index++;
                        }
                    }

                    if (lipid.headGroup == "Ethanolamine") 
                    {        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of ethanolamine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of ethanolamine; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of ethanolamine; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H - lipid.FA1Mass + H;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of ethanolamine; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "H";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of ethanolamine; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H - lipid.FA2Mass + H;
                            index++;
                        }
                    }

                    if (lipid.headGroup == "Serine") 
                    {        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of serine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 2*O - N - 3*C - 5*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "serine head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 2*O - N - 3*C - 5*H 
                                                                                        - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 2*O - N - 3*C - 5*H 
                                                                                        - lipid.FA1Mass + H;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 2*O - N - 3*C - 5*H 
                                                                                        - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of serine; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 2*O - N - 3*C - 5*H 
                                                                                        - lipid.FA2Mass + H;
                            index++;
                        }
                    }

                    if (lipid.headGroup == "Inositol") 
                    {            
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of inositol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "glycerophosphoinositol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + lipid.backboneMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "glycerophosphoinositol - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + lipid.backboneMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "glycerophosphoinositol - 2*H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + lipid.backboneMass - 2*O - 3*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "glycerophosphoinositol - 3*H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + lipid.backboneMass - 3*O - 5*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol - 2*H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - O - 4*H;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H - lipid.FA1Mass + H;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H - lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H - lipid.FA2Mass + H;
                            index++;
                        }
                    }

                    if (lipid.headGroup == "Glycerol") 
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - 6*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "glycerophosphoglycerol";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + lipid.backboneMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "glycerophosphoglycerol - H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + lipid.backboneMass - H - O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "glycerophosphoglycerol - 2*H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + lipid.backboneMass -3*H - 2*O;
                        index++;

                        if (lipid.SN1bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA1 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - 6*H - lipid.FA1Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF1";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA1 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - 6*H- lipid.FA1Mass + H;
                            index++;
                        }

                        if (lipid.SN2bond == "acyl")
                        {
                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA2 as carboxylic acid";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - 6*H- lipid.FA2Mass - O - H;
                            index++;

                            lipid.precursors[j].fragments.push_back(Fragment());
                            lipid.precursors[j].fragments[index].fragType = "HF2";
                            lipid.precursors[j].fragments[index].fragDescription = "loss of glycerol; loss of FA2 as ketene";
                            lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 3*C - 2*O - 6*H- lipid.FA2Mass + H;
                            index++;
                        }
                    }
                }
            }
        }
        
        string backsub = lipid.backbone.substr(5, 6);
        if (backsub == "Sphing" || backsub == "Phytos")
        {
            if (lipid.precursors[j].ESI == "pos")
            {
                if (lipid.precursors[j].adductType == "nonMetal")
                {
                    if (lipid.headGroup == "Choline")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "HF";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of fatty acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*H - 2*O - lipid.FA1Mass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "HF";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of fatty acid; addition of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.FA1Mass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F";
                        lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NC2H3";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*C + 3*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F";
                        lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 3*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphocholine head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 5*C + N + 15*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "choline";
                        lipid.precursors[j].fragments[index].fragmentMass = N + 5*C + 12*H;
                        index++;
                    }

                    if (lipid.headGroup == "Ethanolamine")
                    {
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "FH";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; loss of fatty acid";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*H - 2*O - lipid.FA1Mass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "FH";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; loss of fatty acid; addition of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.FA1Mass;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F";
                        lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NC2H3";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*C + 3*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "F";
                        lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 3*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 2*C + N + 9*H;
                        index++;
                        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; addition of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H;
                        index++;
                    }

                    if (lipid.headGroup == "Inositol")
                    {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; addition of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of fatty acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*H - 2*O - lipid.FA1Mass;
                    index++;
                    /*
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of fatty acid; addition of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.FA1Mass;
                    index++;
                    */
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NC2H3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*C + 3*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 3*H;
                    index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 2*H - O;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of 2*H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 4*H - 2*O;
                        index++;

                        //lipid.precursors[j].fragments.push_back(Fragment());
                        //lipid.precursors[j].fragments[index].fragType = "H";
                        //lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol & adduct";
                        //lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        //index++;
                    }
                }

                if (lipid.precursors[j].adductType == "Metal")
                {
                    if (lipid.headGroup == "Choline")
                    {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group & adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H - lipid.precursors[j].adductMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group, loss of H2O; loss of adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*O - 3*H - lipid.precursors[j].adductMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of CH2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O - C;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of adduct; loss of fatty acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*H - 2*O - lipid.FA1Mass - lipid.precursors[j].adductMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of fatty acid; addition of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NC2H3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*C + 3*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 3*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3 + adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*H + lipid.precursors[j].adductMass;
                    index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphocholine head-group";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 5*C + N + 15*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of NC3H9";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 3*C - 9*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "choline";
                        lipid.precursors[j].fragments[index].fragmentMass = N + 5*C + 12*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "PO4C2H5 & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = P + 4*O + 2*C + 5*H + lipid.precursors[j].adductMass;
                        index++;
                    }

                    if (lipid.headGroup == "Ethanolamine")
                    {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group & adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H - lipid.precursors[j].adductMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group, loss of H2O; loss of adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*O - 3*H - lipid.precursors[j].adductMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of CH2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O - C;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of adduct; loss of fatty acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*H - 2*O - lipid.FA1Mass - lipid.precursors[j].adductMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of fatty acid; addition of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NC2H3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*C + 3*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 3*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3 + adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*H + lipid.precursors[j].adductMass;
                    index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine";
                        lipid.precursors[j].fragments[index].fragmentMass = 4*O + P + 2*C + N + 9*H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of phosphoethanolamine; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of aziridine";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H;
                        index++;
                    }
                    
                    if (lipid.headGroup == "Inositol")
                    {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of inositol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of inositol; loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H - 2*H - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group & adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - H - lipid.precursors[j].adductMass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group, loss of H2O; loss of adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*O - 3*H - lipid.precursors[j].adductMass + H;
                    index++;
                    /*
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O;
                    index++;
                    */
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of CH2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 3*H - 2*O - C;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of adduct; loss of fatty acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - 2*H - 2*O - lipid.FA1Mass - lipid.precursors[j].adductMass + H;
                    index++;
                    /*
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; loss of fatty acid; addition of H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass - O - lipid.FA1Mass;
                    index++;
                    */
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NC2H3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*C + 3*H;
                    index++;
                    /*
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NC2H3; loss of water";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*C + 3*H - 2*H - O;
                    index++;
                    */
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 3*H;
                    index++;
                    /*
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FH";
                    lipid.precursors[j].fragments[index].fragDescription = "fatty acid + NH3 + adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + N + 2*H + lipid.precursors[j].adductMass;
                    index++;
                    */    
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 2*H - O;
                        index++;
                        
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "loss of head-group; addition of water";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.headGroupMass + H;
                        index++;

                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol & adduct";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + H + O + lipid.precursors[j].adductMass;
                        index++;
    
                        lipid.precursors[j].fragments.push_back(Fragment());
                        lipid.precursors[j].fragments[index].fragType = "H";
                        lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol & adduct; loss of H2O";
                        lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - H + lipid.precursors[j].adductMass;
                        index++;
                    }
                    
                }
            }

            if (lipid.precursors[j].ESI == "neg")
            {
                if ((lipid.headGroup == "Choline") && (lipid.precursors[j].adduct != "-CH3"))
                {    
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of methyl group and adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass
                                                                                - lipid.precursors[j].adductMass - C - 3*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N and adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass 
                                                                                - lipid.precursors[j].adductMass - 3*C - 10*H - N;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N and adduct";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass
                                                                                - lipid.precursors[j].adductMass - 5*C - 12*H - N;
                    index++;
                }

                if ((lipid.headGroup == "Choline") && (lipid.precursors[j].adduct == "-CH3"))
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of C3H10N";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass 
                                                                                - lipid.precursors[j].adductMass - 2*C - 7*H - N;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of C5H12N";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass
                                                                                - lipid.precursors[j].adductMass - 4*C - 9*H - N;
                    index++;
                }

                if (lipid.headGroup == "Ethanolamine")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of ethanolamine";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - N - 2*C - 5*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoethanolamine - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H;
                    index++;
                }

                if (lipid.headGroup == "Inositol")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of inositol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - 6*C - 5*O - 10*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol - H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "H";
                    lipid.precursors[j].fragments[index].fragDescription = "phosphoinositol - 2*H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.headGroupMass - O - 4*H;
                    index++;
                }
            }
        }

        if (lipid.backbone == "Cardiolipin")
        {
            if (lipid.precursors[j].ESI == "pos")
            {
                if (lipid.precursors[j].description == "protonated")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 11*O - 6*C - 14*H - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3, glycerol, & glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 12*O - 6*C - 15*H - 2*P - lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 11*O - 6*C - 14*H - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1, glycerol, & glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 12*O - 6*C - 15*H - 2*P - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                    index++;
                }

                if (lipid.precursors[j].description == "+ Na")
                {

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & sodium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 11*O - 6*C - 13*H - Na - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3, glycerol, & sodium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 12*O - 6*C - 14*H - Na - 2*P - lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & sodium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 11*O - 6*C - 13*H - Na - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1, glycerol, & sodium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 12*O - 6*C - 14*H - Na - 2*P - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                    index++;

                }
            
                if (lipid.precursors[j].description == "- H + 2Na")
                {

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & di-sodium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 11*O - 6*C - 12*H - 2*Na - 2*P;
                    index++;                
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3, glycerol, & di-sodium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 12*O - 6*C - 13*H - 2*Na - 2*P - lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & di-sodium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 11*O - 6*C - 12*H - 2*Na - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1, glycerol, & di-sodium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 12*O - 6*C - 13*H - 2*Na - 2*P - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                    index++;
                }

                if (lipid.precursors[j].description == "- 2H + 3Na")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & sodiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 6*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & sodiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 6*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & phosphoglycerolphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & phosphoglycerolphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 & FA3 as carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA3Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 & FA2 as carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 & FA4 as carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA3Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA1Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA3Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA1Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, and FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA4Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, and FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA2Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA4Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA2Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, sodiated glycerophosphatidic acid, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 3*C - 7*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, sodiated glycerophosphatidic acid, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 3*C - 7*H - P - lipid.precursors[j].adductMass;
                    index++;
                } 

                if (lipid.precursors[j].description == "+ K")
                {

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & potassium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 11*O - 6*C - 13*H - K - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3, glycerol, & potassium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 12*O - 6*C - 14*H - K - 2*P - lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & potassium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 11*O - 6*C - 13*H - K - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1, glycerol, & potassium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 12*O - 6*C - 14*H - K - 2*P - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                    index++;
                }
            
                if (lipid.precursors[j].description == "- H + 2K")
                {

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & di-potassium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 11*O - 6*C - 12*H - 2*K - 2*P;
                    index++;                
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3, glycerol, & di-potassium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 12*O - 6*C - 13*H - 2*K - 2*P - lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & di-potassium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 11*O - 6*C - 12*H - 2*K - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1, glycerol, & di-potassium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 12*O - 6*C - 13*H - 2*K - 2*P - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                    index++;
                }

                if (lipid.precursors[j].description == "- 2H + 3K")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & potassiated glycerphosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 6*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & potassiated glycerphosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 6*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & phosphoglycerolphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & phosphoglycerolphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 & FA3 as a carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA3Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 & FA2 as a carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 & FA4 as a carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA3Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA1Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA3Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA1Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA4Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA2Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol and FA4 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA4Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, and FA2 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA2Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, potassiated glycerophosphatidic acid, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 3*C - 7*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, potassiated glycerophosphatidic acid, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 3*C - 7*H - P - lipid.precursors[j].adductMass;
                    index++;
                } 

                if (lipid.precursors[j].description == "+ Li")
                {

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol & lithium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 11*O - 6*C - 13*H - Li - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3, glycerol & lithium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 12*O - 6*C - 14*H - Li - 2*P - lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol & lithium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 11*O - 6*C - 13*H - Li - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1, glycerol & lithium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 12*O - 6*C - 14*H - Li - 2*P - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                    index++;
                }
            
                if (lipid.precursors[j].description == "- H + 2Li")
                {

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 3*C - 4*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol & di-lithium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 11*O - 6*C - 12*H - 2*Li - 2*P;
                    index++;                
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3, glycerol & di-lithium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 12*O - 6*C - 13*H - 2*Li - 2*P - lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol & di-lithium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 11*O - 6*C - 12*H - 2*Li - 2*P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1, glycerol & di-lithium glycerol-1,3-diphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 12*O - 6*C - 13*H - 2*Li - 2*P - lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 cation";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass;
                    index++;
                }

                if (lipid.precursors[j].description == "- 2H + 3Li")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & lithiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 6*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & lithiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 6*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, & phosphoglycerolphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, & phosphoglycerolphosphate";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 & FA3 as a carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA3Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 & FA2 as a carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 & FA4 as a carboxylic acids";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 2*O - 2*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA3Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA1Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA3Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA1Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA4Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA2Mass - 4*O - 3*C - 7*H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, and FA4 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - lipid.FA4Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, and FA2 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - lipid.FA2Mass - 4*O - 3*C - 6*H - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, lithiated glycerophosphatidic acid, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 3*C - 7*H - P - lipid.precursors[j].adductMass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "N";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, lithiated glycerophosphatidic acid, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 3*C - 7*H - P - lipid.precursors[j].adductMass;
                    index++;
                }
            }

            if (lipid.precursors[j].ESI == "neg")
            {
                if (lipid.precursors[j].description == "deprotonated")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "G3P-H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = 3*C + 6*H + 5*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "G3P";
                    lipid.precursors[j].fragments[index].fragmentMass = 3*C + 8*H + 6*O + P;
                    index++;
            
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "H2PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*H + 4*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = P + 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 carboxylate anion";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 carboxylate anion";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 carboxylate anion";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 carboxylate anion";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1 as carboxylic acid & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H
                                                                                - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA2 as carboxylic acid & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H
                                                                                - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1 as ketene & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H
                                                                                - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA2 as ketene & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H
                                                                                - lipid.FA2Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & phosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1 as carboxylic acid & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA2 as carboxylic acid & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1 as ketene & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA2 as ketene & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA2Mass + H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, phosphoglycerol & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, phosphoglycerol & FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, phosphoglycerol & FA1 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, phosphoglycerol & FA2 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA2Mass + H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3 as carboxylic acid & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H
                                                                                - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA4 as carboxylic acid & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H
                                                                                - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3 as ketene & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H
                                                                                - lipid.FA3Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA4 as ketene & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H
                                                                                - lipid.FA4Mass + H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & phosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3 as carboxylic acid & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA4 as carboxylic acid & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3 as ketene & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA3Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA4 as ketene & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA4Mass + H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, phosphoglycerol & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, phosphoglycerol & FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, phosphoglycerol & FA3 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA3Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, phosphoglycerol & FA4 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA4Mass + H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P;
                    index++;
                }
            
                if (lipid.precursors[j].description == "deprotonatedX2")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "G3P-H2O";
                    lipid.precursors[j].fragments[index].fragmentMass = 3*C + 6*H + 5*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "G3P";
                    lipid.precursors[j].fragments[index].fragmentMass = 3*C + 8*H + 6*O + P;
                    index++;
            
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "H2PO4";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*H + 4*O + P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "B";
                    lipid.precursors[j].fragments[index].fragDescription = "PO3";
                    lipid.precursors[j].fragments[index].fragmentMass = P + 3*O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "FA1 carboxylate anion";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA1Mass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 carboxylic anion";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - lipid.FA1Mass - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "FA2 carboxylate anion";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA2Mass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 carboxylic anion";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - lipid.FA2Mass - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = (2*(lipid.precursors[j].precursorMass) - lipid.FA2Mass + H)/2;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "FA3 carboxylate anion";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA3Mass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 carboxylic anion";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - lipid.FA3Mass - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "FA4 carboxylate anion";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.FA4Mass + O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 carboxylic anion";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) - lipid.FA4Mass - O;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = (2*(lipid.precursors[j].precursorMass) - lipid.FA4Mass + H)/2;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & phosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, phosphoglycerol & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, phosphoglycerol & FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, phosphoglycerol & FA3 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA3Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, phosphoglycerol & FA4 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA4Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3 as carboxylic acid & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA4 as carboxylic acid & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA3 as ketene & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA3Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, FA4 as ketene & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA4Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & phosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, phosphoglycerol & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, phosphoglycerol & FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, phosphoglycerol & FA1 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, phosphoglycerol & FA2 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 7*H - P
                                                                                - lipid.FA2Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1 as carboxylic acid & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA2 as carboxylic acid & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA1 as ketene & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, FA2 as ketene & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = 2*(lipid.precursors[j].precursorMass) + H - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P
                                                                                - lipid.FA2Mass + H;
                    index++;
                }
            
                if (lipid.precursors[j].description == "-2H + Na")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - Na;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - Na;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - Na;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - Na;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA1Mass - Na;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA2Mass - Na;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA1Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA2Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA1Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA2Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerophosphoglycerol & FA1 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA1Mass - Na;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerophosphoglycerol & FA2 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA2Mass - Na;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA3Mass - Na;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA4Mass - Na;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA3Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA4Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA3Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerophosphoglycerol & FA3 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA3Mass - Na;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerophosphoglycerol & FA4 as a sodium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA4Mass - Na;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & sodiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 6*H - P - Na;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & sodiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 6*H - P - Na;
                    index++;
                }

                if (lipid.precursors[j].description == "-2H + K")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - K;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - K;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - K;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - K;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA1Mass - K;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA2Mass - K;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA1Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA2Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA1Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA2Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerophosphoglycerol & FA1 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA1Mass - K;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerophosphoglycerol & FA2 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA2Mass - K;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA3Mass - K;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA4Mass - K;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA3Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA4Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA3Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerophosphoglycerol & FA3 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA3Mass - K;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerophosphoglycerol & FA4 as a potassium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA4Mass - K;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & sodiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 6*H - P - K;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & sodiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 6*H - P - K;
                    index++;
                }

                if (lipid.precursors[j].description == "-2H + Li")
                {
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F2";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA2 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA2Mass - O - Li;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F4";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA4 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA4Mass - O - Li;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass + H;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F1";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - O - Li;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "F3";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - O - Li;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 6*H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA1Mass - Li;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA2Mass - Li;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA1Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA2Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA1 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA1Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerol, & FA2 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA2Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerophosphoglycerol & FA1 as a lithiated salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA1Mass - Li;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4, glycerophosphoglycerol & FA2 as a lithiated salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA2Mass - Li;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 6*H;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA3Mass - Li;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 6*H
                                                                                - lipid.FA4Mass - Li;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA3Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a carboxylic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 4*O - 3*C - 7*H
                                                                                - lipid.FA4Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA3 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA3Mass;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerol, & FA4 as a ketene";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 3*O - 3*C - 5*H
                                                                                - lipid.FA4Mass;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & glycerophosphoglycerol";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 7*O - 6*C - 11*H - P;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerophosphoglycerol & FA3 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA3Mass - Li;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2, glycerophosphoglycerol & FA4 as a lithium salt";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 8*O - 6*C - 11*H - P
                                                                                - lipid.FA4Mass - Li;
                    index++;
                
                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA1, FA2 & lithiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA1Mass - lipid.FA2Mass - 6*O - 3*C - 6*H - P - Li;
                    index++;

                    lipid.precursors[j].fragments.push_back(Fragment());
                    lipid.precursors[j].fragments[index].fragType = "FA";
                    lipid.precursors[j].fragments[index].fragDescription = "loss of FA3, FA4 & lithiated glycerophosphatidic acid";
                    lipid.precursors[j].fragments[index].fragmentMass = lipid.precursors[j].precursorMass - lipid.FA3Mass - lipid.FA4Mass - 6*O - 3*C - 6*H - P - Li;
                    index++;
                }
            }
        }
    }

    
    Lipid decoy;
    decoy = lipid;
    decoy.decoy = true;
    decoy.name = lipid.name + "D";
    for (size_t i=0; i<decoy.precursors.size(); i++)
    {
        vector<double> masses(decoy.precursors[i].fragments.size());
        for (size_t j=0; j<decoy.precursors[i].fragments.size(); j++)
        {
            masses[j] = decoy.precursors[i].fragments[j].fragmentMass;
        }
        sort(masses.begin(), masses.end());
        for (size_t j=0; j<masses.size(); j++)
        {
            double fragmass = masses[j];
            masses[j] = 0 - (fragmass - decoy.precursors[i].precursorMass) + 100;
        }
        for (size_t j=0; j<decoy.precursors[i].fragments.size(); j++)
        {
            decoy.precursors[i].fragments[j].fragmentMass = masses[j];
        }
    }    
    scoring(lipid, paraMap, spectraHolder, SL);
    
}

// scoring algorithm
void scoring(Lipid lipid, Map & paraMap, vector<SpectraHolder> spectraHolder, const SpectrumListPtr& SL)
{    
    size_t peaks = paraMap["PeakNumber"];
    for (size_t i=0; i<lipid.precursors.size(); i++)
    {
        double test = lipid.precursors[i].precursorMass;
        int searchIndex=0;
        bool searchThis = false;
        for (size_t ii=0; ii<spectraHolder.size(); ii++)
        {
            if ((test >= spectraHolder[ii].preMassN) && (test <= spectraHolder[ii].preMassP))
            {
                searchThis = true;
                searchIndex = ii;
            }
        }
        if (searchThis == false)
        {
            continue;
        }
        int n = searchIndex;

        while ((test < spectraHolder[n].preMassP) && (test > spectraHolder[n].preMassN) && (n >= 0))
        {
            // get fragment data
            SpectrumPtr s = SL->spectrum(spectraHolder[n].spectraIndex);
            double lowLim, uppLim;
            if (s->scanList.scans[0].scanWindows.empty() == true)
            {
                lowLim = s->cvParam(MS_lowest_observed_m_z).valueAs<double>();
                uppLim = s->cvParam(MS_highest_observed_m_z).valueAs<double>();
            }
            if (s->scanList.scans[0].scanWindows.empty() == false)
            {
                lowLim = s->scanList.scans[0].scanWindows[0].cvParam(MS_scan_window_lower_limit).valueAs<double>();
                uppLim = s->scanList.scans[0].scanWindows[0].cvParam(MS_scan_window_upper_limit).valueAs<double>();
            }

            double mass = s->precursors[0].selectedIons[0].cvParam(MS_selected_ion_m_z).valueAs<double>();
            const bool getBinaryData = true;
            SpectrumPtr spectrum = SL->spectrum(spectraHolder[n].spectraIndex, getBinaryData);
            vector<MZIntensityPair> pairs;
            spectrum->getMZIntensityPairs(pairs);
            
            // Delete any peaks witin tolerance of the precursor mass
            for (vector<MZIntensityPair>::iterator it = pairs.begin(), end = pairs.end(); it!=end; ++it)
            {
                if ((it->mz > spectraHolder[n].preMassN) && (it->mz < spectraHolder[n].preMassP))
                {
                    pairs.erase (it);
                    --it;
                    pairs.push_back(MZIntensityPair());
                }
            }
            
            int mzNum = 0;
            for (vector<MZIntensityPair>::iterator it = pairs.begin(), end = pairs.end(); it!=end; ++it)
            {
                if (it->mz != 0)
                {
                    mzNum++;
                }
            }
            pairs.resize(mzNum);
            

            // Sort list by intensity and resize to the number of allowed peaks
            sort(pairs.begin(), pairs.end(), sortByIntensity);
            if (peaks < pairs.size())
            {
                pairs.resize(peaks);
            }

            // Normalize intensities
            double sum = 0;
            for (vector<MZIntensityPair>::const_iterator it = pairs.begin(), end = pairs.end(); it!=end; ++it)
            {
                sum = sum + it->intensity;
            }
            double factor = 1000/sum;
            for (vector<MZIntensityPair>::iterator it = pairs.begin(), end = pairs.end(); it!=end; ++it)
            {
                double intense = (it->intensity)*factor;
                it->intensity = intense;
            }

            // Creation of vector containing full list of m/z:intensity pairs 
            vector<Match> ms2list(pairs.size());
            int ms2index = 0;
            for (vector<MZIntensityPair>::const_iterator it = pairs.begin(), end = pairs.end(); it!=end; ++it)
            {
                ms2list[ms2index].mz = it->mz;
                ms2list[ms2index].intensity = it->intensity;
                ms2index++;
            }

            // Creation of vector containing full list of matched peaks
            int matchSize = 0;
            for (size_t m=0; m < lipid.precursors[i].fragments.size(); m++)
            {
                lipid.precursors[i].fragments[m].fragmentMass = floor(lipid.precursors[i].fragments[m].fragmentMass*1000000 + 0.5)/1000000;
            }
            for (size_t m=0; m < lipid.precursors[i].fragments.size(); m++)
            {
                if ((lipid.precursors[i].fragments[m].fragmentMass >= lowLim) && (lipid.precursors[i].fragments[m].fragmentMass <= uppLim))
                {
                    matchSize++;
                }
            }
            vector<Match> match(matchSize);
            int matvecIndex = 0;
            if (paraMap["fragTolmz"] == 1)
            {
                for (size_t m=0; m < lipid.precursors[i].fragments.size(); m++)
                {
                    if ((lipid.precursors[i].fragments[m].fragmentMass >= lowLim) && (lipid.precursors[i].fragments[m].fragmentMass <= uppLim))
                    {
                        match[matvecIndex].mz = lipid.precursors[i].fragments[m].fragmentMass;
                        match[matvecIndex].intensity = 0;
                        match[matvecIndex].match = false;
                        match[matvecIndex].peakIndex = 99999;
                        match[matvecIndex].fragmentD = lipid.precursors[i].fragments[m].fragDescription;
                        match[matvecIndex].fragmentT = lipid.precursors[i].fragments[m].fragType;
                        double variance = paraMap["mzFragTol"];
                                    
                        for (vector<MZIntensityPair>::const_iterator it = pairs.begin(), end = pairs.end(); it!=end; ++it)
                        {
                            if ((abs(lipid.precursors[i].fragments[m].fragmentMass - it->mz) < variance))
                            {
                                int isit = 0;
                                for (size_t nn=0; nn < lipid.precursors[i].fragments.size(); nn++)
                                {                                        
                                    if ((nn!=m) && (abs(lipid.precursors[i].fragments[nn].fragmentMass - it->mz) < abs(lipid.precursors[i].fragments[m].fragmentMass - it->mz)) 
                                        && (lipid.precursors[i].fragments[m].fragmentMass >= lowLim) && (lipid.precursors[i].fragments[m].fragmentMass <= uppLim))
                                    {
                                        isit = 1;
                                    }
                                }
                                if (isit == 0)
                                {
                                    match[matvecIndex].match = true;
                                    match[matvecIndex].intensity = it->intensity;
                                    match[matvecIndex].peakIndex = it - pairs.begin();
                                    variance = abs(lipid.precursors[i].fragments[m].fragmentMass - it->mz);
                                }
                            }
                        }
                        matvecIndex++;
                    }
                }
            }
            if (paraMap["fragTolmz"] == 0)
            { 
                for (size_t m=0; m < lipid.precursors[i].fragments.size(); m++)
                {
                    if ((lipid.precursors[i].fragments[m].fragmentMass >= lowLim) && (lipid.precursors[i].fragments[m].fragmentMass <= uppLim))
                    {
                        match[matvecIndex].mz = lipid.precursors[i].fragments[m].fragmentMass;
                        match[matvecIndex].intensity = 0;
                        match[matvecIndex].match = false;
                        match[matvecIndex].peakIndex = 99999;
                        match[matvecIndex].fragmentD = lipid.precursors[i].fragments[m].fragDescription;
                        match[matvecIndex].fragmentT = lipid.precursors[i].fragments[m].fragType;
                        double variance = 1000;
                        for (vector<MZIntensityPair>::const_iterator it = pairs.begin(), end = pairs.end(); it!=end; ++it)
                        {
                            double fragTol = paraMap["ppmFragTol"]*(it->mz)/1000000;
                            if ((abs(lipid.precursors[i].fragments[m].fragmentMass - it->mz) < fragTol) 
                                && (abs(lipid.precursors[i].fragments[m].fragmentMass - it->mz) < variance))
                            {
                                int isit = 0;
                                for (size_t nn=0; nn < lipid.precursors[i].fragments.size(); nn++)
                                {
                                    if ((nn!=m) && (abs(lipid.precursors[i].fragments[nn].fragmentMass - it->mz) < abs(lipid.precursors[i].fragments[m].fragmentMass - it->mz))
                                        && (lipid.precursors[i].fragments[m].fragmentMass >= lowLim) && (lipid.precursors[i].fragments[m].fragmentMass <= uppLim))
                                    {
                                        isit = 1;
                                    }
                                }
                                if (isit == 0)
                                {
                                    match[matvecIndex].match = true;
                                    match[matvecIndex].intensity = it->intensity;
                                    match[matvecIndex].peakIndex = it - pairs.begin();
                                    variance = abs(lipid.precursors[i].fragments[m].fragmentMass - it->mz);
                                }
                            }
                        }
                        matvecIndex++;
                    }
                }
            }    

            // This code eliminates entries for lipids with identical FA chains.
            vector<double> tempMZ(match.size());
            size_t tempIndex = 0;
            for (vector<Match>::const_iterator it = match.begin(), end = match.end(); it!=end; ++it)
            {
                tempMZ[tempIndex] = it->mz;
                tempIndex++;
            }
            for (size_t m=0; m<tempMZ.size(); m++)
            {
                int indexA = 0;
                for (vector<Match>::iterator it = match.begin(), end = match.end(); it!=end; ++it)
                {

                    if ((it->mz == tempMZ[m]) && (indexA != 0))
                    {
                        match.erase (it);
                        --it;
                        match.push_back(Match());
                        match.back().mz = 0;
                    }
                    if ((it->mz == tempMZ[m]) && (indexA == 0))
                    {
                        indexA++;
                    }
                }
            }
            int indexB = 0;
            for (vector<Match>::const_iterator it = match.begin(), end = match.end(); it!=end; ++it)
            {
                            
                if (it->mz != 0)
                {
                    indexB++;
                }
            }
            match.resize(indexB);

            // calculates the number of matched peaks and "continues" if zero
            int x = 0;
            for (size_t m=0; m<match.size(); m++)
            {
                if (match[m].match)
                {
                    x++;
                }                            
            }
            if (x == 0)
            {
                n--;
                continue;
            }    
            
            // set number of bins, filled bins, matching peaks, and support for the HGM method
            double N;
            if (paraMap["fragTolmz"] == 1)
            {
                N = round((uppLim - lowLim)/paraMap["mzFragTol"]);
            }
            else if (paraMap["fragTolmz"] == 0)
            {
                N = round((uppLim - lowLim)/(paraMap["ppmFragTol"]*((lowLim + uppLim)/2)/1000000));
            }
            int K = ms2list.size();
            int M = match.size();

            // calculate peakScore
            double c = factorials[N] - factorials[K] - factorials[N - K];
            double peakScore = 0;
            int Q = min(M, K);
            
            if (x != 0)
            {
                for (int m=x; m<Q+1; m++)
                {
                    double a = factorials[M] - factorials[m] - factorials[M - m];
                    double b = factorials[N - M] - factorials[(N - M) - (K - m)] - factorials[K - m];
                    peakScore = peakScore + pow(10, a + b - c);
                        
                }
            }
            else
            {
                peakScore = 1;
            }
            
            // calculate intensityScore
            double intensitySum = 0;
            for (size_t m=0; m<match.size(); m++)
            {
                intensitySum = intensitySum + match[m].intensity;
            }
                
            double iScore2;
            int numerator = 0;
            int* pNum = &numerator;


            if (x > 0)
            {
                combo(0, intensitySum, 0, x, ms2list, pNum);
                double num = numerator;
                double den =  factorials[K] - factorials[x] - factorials[K - x];
                iScore2 = (log10(num) - den);
            }
            else
            {
                iScore2 = 0;
            }

            // calculate chi-squared value
            long double totalScore;
            if ((peakScore == 1) && (iScore2 == 0))
            {
                totalScore = 0;
            }
            else
            {
                if (paraMap["intensityScore"] == 1)
                {
                    totalScore = -2*(log(peakScore) + log(pow(10,iScore2)));
                }
                if (paraMap["intensityScore"] == 0)
                {
                    totalScore = -2*log(peakScore);
                }
            }

            sort(match.begin(), match.end(), sortByMZr);

            spec[spectraHolder[n].spectraIndex].push_back(Score());
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].totalScore = totalScore;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].intensityScore = iScore2;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].peakScore = peakScore;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].spectrumNumber = spectraHolder[n].spectraIndex;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].spectrumPrecursorMass = mass;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].lipidName = lipid.name;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].lipidMass = lipid.totalMass;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].chemFormula = lipid.lipidFormula;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].precursorDescription = lipid.precursors[i].description;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].precursorMass = lipid.precursors[i].precursorMass;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].match = match;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].ms2list = ms2list;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].retTime = spectraHolder[n].retentionTime;
            spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].mod = lipid.precursors[i].adduct;

            if (lipid.precursors[i].ESI == "pos")
            {
                spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].charge = "+1";
            }
            if ((lipid.precursors[i].ESI == "neg") && (lipid.precursors[i].description != "deprotonatedX2"))
            {
                spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].charge = "-1";
            }
            if ((lipid.precursors[i].ESI == "neg") && (lipid.precursors[i].description == "deprotonatedX2"))
            {
                spec[spectraHolder[n].spectraIndex][spec[spectraHolder[n].spectraIndex].size()-1].charge = "-2";
            }

            sort(spec[spectraHolder[n].spectraIndex].begin(), spec[spectraHolder[n].spectraIndex].end(), sortByName);
            sort(spec[spectraHolder[n].spectraIndex].begin(), spec[spectraHolder[n].spectraIndex].end(), sortByTotalScore);

            int pc = 0;
            int pe = 0;
            int pi = 0;
            int pg = 0;
            int ps = 0;
            int pa = 0;
            int sm = 0;
            int pip = 0;
            int pip2 = 0;
            int cl = 0;
            int cl2 = 0;
            int mpc = 0;
            int mpe = 0;
            int mpi = 0;
            int mpg = 0;
            int mps = 0;
            int mpa = 0;
            int msm = 0;
            //int m3cl = 0;
            int mcl = 0;
            int mpip = 0;
            int pec = 0;
            int pic = 0;
            int mpec = 0;
            int mpic = 0;

            vector<Score> specTemp;
            for (size_t m=0; m<spec[spectraHolder[n].spectraIndex].size(); m++)
            {
                if (lipid.precursors[i].ESI == "pos")
                {
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PC" && pc == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pc = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PC" && mpc == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mpc = 1;
                    }

                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PE(" && pe == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pe = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PE(" && mpe == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mpe = 1;
                    }

                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PI(" && pi == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pi = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PI(" && mpi == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mpi = 1;
                    }

                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PG" && pg == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pg = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PG" && mpg == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mpg = 1;
                    }

                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PS" && ps == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        ps = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PS" && mps == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mps = 1;
                    }

                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PA" && pa == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pa = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PA" && mpa == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mpa = 1;
                    }

                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "SM" && sm == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        sm = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "SM" && msm == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        msm = 1;
                    }                
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 6) == "PE-Cer" && pec == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pec = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 6) == "PE-Cer" && mpec == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mpec = 1;
                    }

                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 6) == "PI-Cer" && pic == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pic = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 6) == "PI-Cer" && mpic == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mpic = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PIP" && pip == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "+H" || spec[spectraHolder[n].spectraIndex][m].mod == "+NH4"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pip = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PIP" && mpip == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "+H" && spec[spectraHolder[n].spectraIndex][m].mod != "+NH4")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mpip = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "CL" && cl == 0 && spec[spectraHolder[n].spectraIndex][m].mod != "-2H + 3Na" && spec[spectraHolder[n].spectraIndex][m].mod != "-2H + 3Li" && spec[spectraHolder[n].spectraIndex][m].mod != "-2H + 3K")
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        cl = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "CL" && mcl == 0 && (spec[spectraHolder[n].spectraIndex][m].mod == "-2H + 3Na" || spec[spectraHolder[n].spectraIndex][m].mod == "-2H + 3Li" || spec[spectraHolder[n].spectraIndex][m].mod == "-2H + 3K"))
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mcl = 1;
                    }
                }

                if (lipid.precursors[i].ESI == "neg")
                {
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PC" && pc == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pc = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PE(" && pe == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pe = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PI(" && pi == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pi = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PG" && pg == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pg = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PS" && ps == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        ps = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "PA" && pa == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pa = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "SM" && sm == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        sm = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 6) == "PE-Cer" && sm == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pec = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 6) == "PI-Cer" && sm == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pic = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "CL" && spec[spectraHolder[n].spectraIndex][m].mod == "-H" && cl == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        cl = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "CL" && (spec[spectraHolder[n].spectraIndex][m].mod == "-2H + Na" || spec[spectraHolder[n].spectraIndex][m].mod == "-2H + K" || spec[spectraHolder[n].spectraIndex][m].mod == "-2H + Li" ) && mcl == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        mcl = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 2) == "CL" && spec[spectraHolder[n].spectraIndex][m].mod == "-2H" && cl2 == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        cl2 = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PIP" && spec[spectraHolder[n].spectraIndex][m].mod != "-2H" && pip == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pip = 1;
                    }
                    if (spec[spectraHolder[n].spectraIndex][m].lipidName.substr(0, 3) == "PIP" && spec[spectraHolder[n].spectraIndex][m].mod == "-2H" && pip2 == 0)
                    {
                        specTemp.push_back(spec[spectraHolder[n].spectraIndex][m]);
                        pip2 = 1;
                    }
                }
            }
            spec[spectraHolder[n].spectraIndex] = specTemp;

            if (totalScore > firstSecondFull[spectraHolder[n].spectraIndex][0])
            {
                firstSecondFull[spectraHolder[n].spectraIndex][1] = firstSecondFull[spectraHolder[n].spectraIndex][0];
                firstSecondFull[spectraHolder[n].spectraIndex][0] = totalScore;
            }
            if (totalScore < firstSecondFull[spectraHolder[n].spectraIndex][0] && totalScore > firstSecondFull[spectraHolder[n].spectraIndex][1])
            {
                firstSecondFull[spectraHolder[n].spectraIndex][1] = totalScore;
            }

            if (lipid.precursors[i].ESI == "pos")
            {
                if (lipid.name.substr(0, 2) == "PC" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PC[1] = secondary[spectraHolder[n].spectraIndex].PC[0];
                        secondary[spectraHolder[n].spectraIndex].PC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PC[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "PC" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPC[1] = secondary[spectraHolder[n].spectraIndex].MPC[0];
                        secondary[spectraHolder[n].spectraIndex].MPC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPC[1] = totalScore;
                    }
                }

                if (lipid.name.substr(0, 3) == "PE(" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PE[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PE[1] = secondary[spectraHolder[n].spectraIndex].PE[0];
                        secondary[spectraHolder[n].spectraIndex].PE[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PE[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PE[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PE[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 3) == "PE(" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPE[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPE[1] = secondary[spectraHolder[n].spectraIndex].MPE[0];
                        secondary[spectraHolder[n].spectraIndex].MPE[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPE[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPE[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPE[1] = totalScore;
                    }
                }

                if (lipid.name.substr(0, 3) == "PI(" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PI[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PI[1] = secondary[spectraHolder[n].spectraIndex].PI[0];
                        secondary[spectraHolder[n].spectraIndex].PI[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PI[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PI[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PI[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 3) == "PI(" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPI[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPI[1] = secondary[spectraHolder[n].spectraIndex].MPI[0];
                        secondary[spectraHolder[n].spectraIndex].MPI[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPI[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPI[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPI[1] = totalScore;
                    }
                }

                if (lipid.name.substr(0, 2) == "PG" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PG[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PG[1] = secondary[spectraHolder[n].spectraIndex].PG[0];
                        secondary[spectraHolder[n].spectraIndex].PG[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PG[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PG[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PG[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "PG" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPG[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPG[1] = secondary[spectraHolder[n].spectraIndex].MPG[0];
                        secondary[spectraHolder[n].spectraIndex].MPG[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPG[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPG[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPG[1] = totalScore;
                    }
                }

                if (lipid.name.substr(0, 2) == "PS" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PS[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PS[1] = secondary[spectraHolder[n].spectraIndex].PS[0];
                        secondary[spectraHolder[n].spectraIndex].PS[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PS[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PS[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PS[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "PS" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPS[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPS[1] = secondary[spectraHolder[n].spectraIndex].MPS[0];
                        secondary[spectraHolder[n].spectraIndex].MPS[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPS[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPS[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPS[1] = totalScore;
                    }
                }

                if (lipid.name.substr(0, 2) == "PA" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PA[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PA[1] = secondary[spectraHolder[n].spectraIndex].PA[0];
                        secondary[spectraHolder[n].spectraIndex].PA[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PA[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PA[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PA[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "PA" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPA[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPA[1] = secondary[spectraHolder[n].spectraIndex].MPA[0];
                        secondary[spectraHolder[n].spectraIndex].MPA[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPA[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPA[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPA[1] = totalScore;
                    }
                }

                if (lipid.name.substr(0, 2) == "CL" && lipid.precursors[i].adduct != "-2H + 3Na" && lipid.precursors[i].adduct != "-2H + 3Li" && lipid.precursors[i].adduct != "-2H + 3K")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].CL[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].CL[1] = secondary[spectraHolder[n].spectraIndex].CL[0];
                        secondary[spectraHolder[n].spectraIndex].CL[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].CL[0] && totalScore > secondary[spectraHolder[n].spectraIndex].CL[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].CL[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "CL" && (lipid.precursors[i].adduct == "-2H + 3Na" || lipid.precursors[i].adduct == "-2H + 3Li" || lipid.precursors[i].adduct == "-2H + 3K"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MCL[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MCL[1] = secondary[spectraHolder[n].spectraIndex].MCL[0];
                        secondary[spectraHolder[n].spectraIndex].MCL[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MCL[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MCL[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MCL[1] = totalScore;
                    }
                }

                if (lipid.name.substr(0, 2) == "SM" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].SM[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].SM[1] = secondary[spectraHolder[n].spectraIndex].SM[0];
                        secondary[spectraHolder[n].spectraIndex].SM[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].SM[0] && totalScore > secondary[spectraHolder[n].spectraIndex].SM[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].SM[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "SM" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MSM[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MSM[1] = secondary[spectraHolder[n].spectraIndex].MSM[0];
                        secondary[spectraHolder[n].spectraIndex].MSM[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MSM[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MSM[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MSM[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 6) == "PE-Cer" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PEC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PEC[1] = secondary[spectraHolder[n].spectraIndex].PEC[0];
                        secondary[spectraHolder[n].spectraIndex].PEC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PEC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PEC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PEC[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 6) == "PE-Cer" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPEC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPEC[1] = secondary[spectraHolder[n].spectraIndex].MPEC[0];
                        secondary[spectraHolder[n].spectraIndex].MPEC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPEC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPEC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPEC[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 6) == "PI-Cer" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PIC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIC[1] = secondary[spectraHolder[n].spectraIndex].PIC[0];
                        secondary[spectraHolder[n].spectraIndex].PIC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PIC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PIC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIC[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 6) == "PI-Cer" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPIC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPIC[1] = secondary[spectraHolder[n].spectraIndex].MPIC[0];
                        secondary[spectraHolder[n].spectraIndex].MPIC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPIC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPIC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPIC[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 3) == "PIP" && (lipid.precursors[i].adduct == "+H" || lipid.precursors[i].adduct == "+NH4"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PIP[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIP[1] = secondary[spectraHolder[n].spectraIndex].PIP[0];
                        secondary[spectraHolder[n].spectraIndex].PIP[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PIP[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PIP[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIP[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 3) == "PIP" && lipid.precursors[i].adduct != "+H" && lipid.precursors[i].adduct != "+NH4")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MPIP[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPIP[1] = secondary[spectraHolder[n].spectraIndex].MPIP[0];
                        secondary[spectraHolder[n].spectraIndex].MPIP[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MPIP[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MPIP[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MPIP[1] = totalScore;
                    }
                }
            }
            if (lipid.precursors[i].ESI == "neg")
            {
                if (lipid.name.substr(0, 2) == "PC")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PC[1] = secondary[spectraHolder[n].spectraIndex].PC[0];
                        secondary[spectraHolder[n].spectraIndex].PC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PC[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 3) == "PE(")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PE[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PE[1] = secondary[spectraHolder[n].spectraIndex].PE[0];
                        secondary[spectraHolder[n].spectraIndex].PE[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PE[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PE[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PE[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 3) == "PI(")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PI[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PI[1] = secondary[spectraHolder[n].spectraIndex].PI[0];
                        secondary[spectraHolder[n].spectraIndex].PI[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PI[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PI[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PI[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "PG")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PG[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PG[1] = secondary[spectraHolder[n].spectraIndex].PG[0];
                        secondary[spectraHolder[n].spectraIndex].PG[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PG[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PG[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PG[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "PS")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PS[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PS[1] = secondary[spectraHolder[n].spectraIndex].PS[0];
                        secondary[spectraHolder[n].spectraIndex].PS[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PS[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PS[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PS[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "PA")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PA[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PA[1] = secondary[spectraHolder[n].spectraIndex].PA[0];
                        secondary[spectraHolder[n].spectraIndex].PA[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PA[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PA[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PA[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "CL" && lipid.precursors[i].adduct == "-H")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].CL[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].CL[1] = secondary[spectraHolder[n].spectraIndex].CL[0];
                        secondary[spectraHolder[n].spectraIndex].CL[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].CL[0] && totalScore > secondary[spectraHolder[n].spectraIndex].CL[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].CL[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "CL" && (lipid.precursors[i].adduct == "-2H + Na" || lipid.precursors[i].adduct == "-2H + K" ||lipid.precursors[i].adduct == "-2H + Li"))
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].MCL[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].MCL[1] = secondary[spectraHolder[n].spectraIndex].MCL[0];
                        secondary[spectraHolder[n].spectraIndex].MCL[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].MCL[0] && totalScore > secondary[spectraHolder[n].spectraIndex].MCL[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].MCL[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "CL" && lipid.precursors[i].adduct == "-2H")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].CL2[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].CL2[1] = secondary[spectraHolder[n].spectraIndex].CL2[0];
                        secondary[spectraHolder[n].spectraIndex].CL2[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].CL2[0] && totalScore > secondary[spectraHolder[n].spectraIndex].CL2[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].CL2[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 2) == "SM")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].SM[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].SM[1] = secondary[spectraHolder[n].spectraIndex].SM[0];
                        secondary[spectraHolder[n].spectraIndex].SM[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].SM[0] && totalScore > secondary[spectraHolder[n].spectraIndex].SM[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].SM[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 6) == "PE-Cer")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PEC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PEC[1] = secondary[spectraHolder[n].spectraIndex].PEC[0];
                        secondary[spectraHolder[n].spectraIndex].PEC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PEC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PEC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PEC[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 6) == "PI-Cer")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PIC[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIC[1] = secondary[spectraHolder[n].spectraIndex].PIC[0];
                        secondary[spectraHolder[n].spectraIndex].PIC[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PIC[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PIC[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIC[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 3) == "PIP" && lipid.precursors[i].adduct != "-2H")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PIP[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIP[1] = secondary[spectraHolder[n].spectraIndex].PIP[0];
                        secondary[spectraHolder[n].spectraIndex].PIP[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PIP[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PIP[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIP[1] = totalScore;
                    }
                }
                if (lipid.name.substr(0, 3) == "PIP" && lipid.precursors[i].adduct == "-2H")
                {
                    if (totalScore > secondary[spectraHolder[n].spectraIndex].PIP2[0])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIP2[1] = secondary[spectraHolder[n].spectraIndex].PIP2[0];
                        secondary[spectraHolder[n].spectraIndex].PIP2[0] = totalScore;
                    }
                    if (totalScore < secondary[spectraHolder[n].spectraIndex].PIP2[0] && totalScore > secondary[spectraHolder[n].spectraIndex].PIP2[1])
                    {
                        secondary[spectraHolder[n].spectraIndex].PIP2[1] = totalScore;
                    }
                }
            }
            n--;
        }
    }
}