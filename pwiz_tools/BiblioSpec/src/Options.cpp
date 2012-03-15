/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
//class definition for Options

#include "Options.h"



using namespace std;

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
