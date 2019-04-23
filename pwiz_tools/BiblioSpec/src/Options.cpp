//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

//class definition for Options

#include "Options.h"
#include "pwiz/utility/misc/Std.hpp"


/**
 * Create an options object and set all options to default values.
 */
Options::Options()
: isClearPrecursor(true),
    isReport(true),
    topPeaksForSearch(100),
    sqliteTopMatches(5),
    mzWindow(3),
    chargeLow(1),
    chargeHigh(5),
    verbosity(3),
    reportTopMatches(5)
{
    /*
    paramFile.open("blib.params", ios::in);
    if(!paramFile.is_open()) {
        cout<<"Can't open blib.params, make sure you have blib.params in the current directory!"<<endl;
        exit(1);
    }
    */
}

Options& Options::operator=(const Options& right)
{
    isClearPrecursor = right.isClearPrecursor;
    isReport = right.isReport;
    topPeaksForSearch = right.topPeaksForSearch;
    sqliteTopMatches = right.sqliteTopMatches;
    mzWindow = right.mzWindow;
    chargeLow = right.chargeLow;
    chargeHigh = right.chargeHigh;
    verbosity = right.verbosity;
    reportTopMatches = right.reportTopMatches;

    return *this;
}

    Options::~Options() {
        if(paramFile.is_open())
            paramFile.close();
    }

void Options::parseOptions()
{
    string paramLine;
  
    while(paramFile.good()) {
        getline(paramFile,paramLine);
        int pos = paramLine.find_first_of('\t');
        paramLine = paramLine.substr(0,pos);
        
        if(paramLine.find("clear_precursor") != string::npos) {
            string clearPre=paramLine.substr(paramLine.find('=')+1);
            if(atoi(clearPre.c_str()) == 1)
                isClearPrecursor = true;
            else
                isClearPrecursor = false;
        }
        
        if(paramLine.find("num_top_peaks_for_search") != string::npos) {
            string tmp = paramLine.substr(paramLine.find('=')+1);
            
            topPeaksForSearch = atoi(tmp.c_str());
        }
        
        if(paramLine.find("precursor_mass_tolerance") != string::npos) {
            string tmp = paramLine.substr(paramLine.find('=')+1);
            mzWindow = (float)atof(tmp.c_str());
        }
        
        if(paramLine.find("charge_low") != string::npos) {
            string tmp = paramLine.substr(paramLine.find('=')+1);
            chargeLow = atoi(tmp.c_str());
        }
        if(paramLine.find("charge_high") != string::npos) {
            string tmp = paramLine.substr(paramLine.find('=')+1);
            chargeHigh = atoi(tmp.c_str());
        }
        
        if(paramLine.find("num_top_matches_sqlite") != string::npos) {
            string tmp = paramLine.substr(paramLine.find('=')+1);
            sqliteTopMatches= atoi(tmp.c_str());
        }
        if(paramLine.find("generate_report_file") != string::npos) {
            string tmp = paramLine.substr(paramLine.find('=')+1);
            if(atoi(tmp.c_str()) == 1)
                isReport = true;
            else
                isReport = false;
        }
        if(paramLine.find("num_top_matches_report") != string::npos) {
            string tmp = paramLine.substr(paramLine.find('=')+1);
            reportTopMatches = atoi(tmp.c_str());
        }
        
        if(paramLine.find("verbosity") != string::npos) {
            string tmp = paramLine.substr(paramLine.find('=')+1);
            verbosity = atoi(tmp.c_str());
        }
    }
    
}
string Options::toString()
{
    string returnString="command line options are:\n";
  
    stringstream os;
    os<<"clear precursor = "<<isClearPrecursor<<"\n"
      <<"print report file = "<<isReport<<"\n"
      <<"top n peaks for search = "<<topPeaksForSearch<<"\n"
      <<"number of top matches reported in .sqlite = "<<sqliteTopMatches<<"\n"
      <<"mzWindow in Da= "<<mzWindow<<"\n"
      <<"low precursor charge = "<<chargeLow<<"\n"
      <<"high precursor charge = "<<chargeHigh<<"\n"
      <<"verbose level = "<<verbosity<<"\n"
      <<"number of top matches reported in .report = "<<reportTopMatches<<"\n";

    returnString.append(os.str());
  
    return returnString;
}


map<string,int> Options::getOptionsMap()
{
    map<string, int> returnMap;

    returnMap["clearPrecursor"] = (int)isClearPrecursor;
    returnMap["outReport"] = (int)isReport;
    returnMap["topPeaksForSearch"] = topPeaksForSearch;
    returnMap["sqliteTopMatches"] = sqliteTopMatches;
    returnMap["chargeLow"] = chargeLow;
    returnMap["chargeHigh"] = chargeHigh;
    returnMap["verbosityLevel"] = verbosity;
    returnMap["reportTopMatches"] = reportTopMatches;

    return returnMap;
}

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
