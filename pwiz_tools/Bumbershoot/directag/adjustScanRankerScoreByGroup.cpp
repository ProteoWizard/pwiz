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
// The Initial Developer of the Original Code is Zeqiang Ma.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

// adjust ScanRanker scores by groups
// use mean and IQR of all files in a group for score normalization
// input: ScanRanker metrics files separated by comma, one line for one group

#include <stdio.h>
#include <vector>
#include <string>
#include <iostream>
#include <fstream>
#include <sstream>
#include <stdexcept>
#include <boost/lexical_cast.hpp>
#include <boost/filesystem.hpp>

using boost::lexical_cast;
using namespace std;

struct ScoreInfo
{
    float    bestTagScoreMean;
    float    bestTagTICMean;
    float    tagMzRangeMean;
    float    bestTagScoreIQR;
    float    bestTagTICIQR;
    float    tagMzRangeIQR;
    int        numTaggedSpectra;
};

///    <summary>
///    extract ScanRanker subscore information from a metrics file
///    </summary>
void getHeader(const string& filename, ScoreInfo *scoreInfo )     
{
    // Open the input file
    ifstream fileStream( filename.c_str() );
    if( !fileStream.is_open() )
        throw invalid_argument( string( "unable to open file \"" ) + filename + "\"" );

    string headerLine;
    getline(fileStream, headerLine); //discard the first header line: H    BestTagScoreMean    BestTagTICMean    TagMzRangeMean    BestTagScoreIQR    BestTagTICIQR    TagMzRangeIQR    numTaggedSpectra
    getline(fileStream, headerLine); // get second line, e.g.: H    25.4616    7.06014    701.679    12.0654    4.52176    305.127    18587
    stringstream lineStream(headerLine);
    string segments;
    vector<string> v;
    while(getline(lineStream,segments,'\t'))
    {
        v.push_back(segments);
    }
//    copy(v.begin(), v.end(), ostream_iterator<string>(cout, "\n"));

    scoreInfo->bestTagScoreMean = lexical_cast<float>( v[1] );
    scoreInfo->bestTagTICMean = lexical_cast<float>( v[2] );
    scoreInfo->tagMzRangeMean = lexical_cast<float>( v[3] );
    scoreInfo->bestTagScoreIQR = lexical_cast<float>( v[4] );
    scoreInfo->bestTagTICIQR = lexical_cast<float>( v[5] );
    scoreInfo->tagMzRangeIQR = lexical_cast<float>( v[6] );
    scoreInfo->numTaggedSpectra = lexical_cast<int>( v[7] );
}


///    <summary>
///    adjust scores with global mean and IQR of a group, write new metrics file
///    </summary>
void adjustScore(    const string& filename,     
                    float    gbBestTagScoreMean,
                    float    gbBestTagTICMean,
                    float    gbTagMzRangeMean,
                    float    gbBestTagScoreIQRMean,
                    float    gbBestTagTICIQRMean,
                    float    gbTagMzRangeIQRMean)   
{
    cout << '\t' << filename << '\n';
    namespace bfs = boost::filesystem;
    bfs::path newFilename = bfs::basename(filename) + "-adjusted.txt";
     string outputFilename = newFilename.string();
    ofstream ofileStream( outputFilename.c_str() );

    // Open the input file
    ifstream fileStream( filename.c_str() );
    if( !fileStream.is_open() )
        throw invalid_argument( string( "unable to open file \"" ) + filename + "\"" );

    string line;
    while(getline(fileStream, line))
    {
        if (line.find("H\tBestTagScoreMean") != string::npos)
        {
            ofileStream << "H\tBestTagScoreMean\tBestTagTICMean\tTagMzRangeMean\tBestTagScoreIQR\tBestTagTICIQR\tTagMzRangeIQR\tnumTaggedSpectra\tgbBestTagScoreMean\tgbBestTagTICMean\tgbTagMzRangeMean\tgbBestTagScoreIQRMean\tgbBestTagTICIQRMean\tgbTagMzRangeIQRMean\n";
        }
        else if (line.find("H\tIndex") != string::npos)    // if header line, write out line, else extract subscores and compute the new score
        {
            ofileStream << line << "\tAdjustedScore\n"; 
        }
        else if (line.find("H\t") != string::npos)
        {
            ofileStream << line << '\t'
                        << gbBestTagScoreMean << '\t'
                        << gbBestTagTICMean << '\t'
                        << gbTagMzRangeMean << '\t'
                        << gbBestTagScoreIQRMean << '\t'
                        << gbBestTagTICIQRMean << '\t'
                        << gbTagMzRangeIQRMean << '\n';


        }
        else
        {
            stringstream lineStream(line);
            string segments;
            vector<string> v;
            while(getline(lineStream,segments,'\t'))
            {
                v.push_back(segments);
            }
            // metrics file format:
            //H    NativeID    PrecursorMZ    Charge    PrecursorMass    BestTagScore    BestTagTIC    TagMzRange    ScanRankerScore
            float bestTagScore = lexical_cast<float>( v[5] );
            float bestTagTIC = lexical_cast<float>( v[6] );
            float tagMzRange = lexical_cast<float>( v[7] );
            float originalScore = lexical_cast<float>( v[8] );
            float bestTagScoreNorm = ( bestTagScore - gbBestTagScoreMean ) / gbBestTagScoreIQRMean;
            float bestTagTICNorm = ( bestTagTIC - gbBestTagTICMean ) / gbBestTagTICIQRMean;
            float tagMzRangeNorm = ( tagMzRange - gbTagMzRangeMean ) / gbTagMzRangeIQRMean;
            float adjustedScore = ( bestTagScoreNorm + bestTagTICNorm + tagMzRangeNorm) / 3;

            ofileStream << line << '\t' << adjustedScore << '\n';
        }
    }
}


int main( int argc, char* argv[] )
{
    if (argc < 2)
    {
        cerr << "Not enough arguments.\n" << "Usage: " << argv[0] << "   groupFile" << endl;
        return 1;
    }

    ifstream  groupFile( argv[1] );
    string line;
    while(getline(groupFile,line))  //work on each group; one line one group, separated with ","
    {
        stringstream  lineStream(line);
        string        file;
        vector<ScoreInfo> scoreInfoVector;
        //cout << "Extracting subscore infomation from metrics files ... \n" << endl;
        while(getline(lineStream,file,','))  //work on a sigle file in a group, extract mean and IQR from each file 
        {
            ScoreInfo scoreInfo;
            getHeader( file, &scoreInfo );
            scoreInfoVector.push_back( scoreInfo );
        }

        // iterate scoreInforVector, compute global mean and IQR for normalization
        float    gbBestTagScoreSum = 0.0;
        float    gbBestTagTICSum = 0.0;
        float    gbTagMzRangeSum = 0.0;
        float    gbBestTagScoreIQRSum = 0.0;
        float    gbBestTagTICIQRSum = 0.0;
        float    gbTagMzRangeIQRSum = 0.0;
        int        totalSpectra = 0;        

        for( vector<ScoreInfo>::iterator itr = scoreInfoVector.begin(); itr != scoreInfoVector.end(); ++itr )
        {
            gbBestTagScoreSum += itr->bestTagScoreMean * itr->numTaggedSpectra ;
            gbBestTagTICSum += itr->bestTagTICMean * itr->numTaggedSpectra ;
            gbTagMzRangeSum += itr->tagMzRangeMean * itr->numTaggedSpectra ;
            gbBestTagScoreIQRSum += itr->bestTagScoreIQR ;
            gbBestTagTICIQRSum += itr->bestTagTICIQR ;
            gbTagMzRangeIQRSum += itr->tagMzRangeIQR ;
            totalSpectra +=  itr->numTaggedSpectra ;
        }

        int numFiles = (int) scoreInfoVector.size();
        float gbBestTagScoreMean = gbBestTagScoreSum / (float) totalSpectra;
        float gbBestTagTICMean = gbBestTagTICSum / (float) totalSpectra;
        float gbTagMzRangeMean = gbTagMzRangeSum / (float) totalSpectra;
        float gbBestTagScoreIQRMean = gbBestTagScoreIQRSum / (float) numFiles;
        float gbBestTagTICIQRMean = gbBestTagTICIQRSum / (float) numFiles;
        float gbTagMzRangeIQRMean = gbTagMzRangeIQRSum / (float) numFiles;
        
        lineStream.clear(); // reset lineStream for getline()
        lineStream.seekg(0,std::ios::beg);
        cout << "Adjusting ScanRanker scores ...\n" << endl;
        while(getline(lineStream,file,','))  //work on a sigle file in a group, add adjusted scores 
        {
            adjustScore(    file, 
                            gbBestTagScoreMean,
                            gbBestTagTICMean,
                            gbTagMzRangeMean,
                            gbBestTagScoreIQRMean,
                            gbBestTagTICIQRMean,
                            gbTagMzRangeIQRMean);
        }

    }
}
