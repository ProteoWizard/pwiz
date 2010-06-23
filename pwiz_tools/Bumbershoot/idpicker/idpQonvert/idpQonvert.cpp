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
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

#define USE_BOOST_REGEX

#include "stdafx.h"
#include "freicore.h"

#include "idpQonvert.h"
#include <iomanip>
#include "boost/format.hpp"
#include "ScoreDiscriminant.h"
#include "svnrev.hpp"

using namespace freicore;
using namespace freicore::idpicker;
using std::setw;
using std::setfill;
using boost::format;

namespace freicore
{
namespace idpicker
{
    int Version::Major()                {return 2;}
    int Version::Minor()                {return 6;}
    int Version::Revision()             {return SVN_REV;}
    string Version::LastModified()      {return SVN_REVDATE;}
    string Version::str()
    {
	    std::ostringstream v;
	    v << Major() << "." << Minor() << "." << Revision();
	    return v.str();
    }

	RunTimeConfig* g_rtConfig;
	proteinStore proteins;
	simplethread_mutex_t ioMutex;
	simplethread_mutex_t resourceMutex;

	size_t QonvertSpectra( SpectraList& fileSpectra, RunTimeVariableMap& varsFromFile, const map< string, double >& scoreWeights, ostream* pQonversionDetailsStream = NULL, bool keepOnlyPassingSpectra = true )
	{
		BOOST_FOREACH(Spectrum* s, fileSpectra)
            BOOST_FOREACH(const SearchResult& result, s->resultSet)
				const_cast< SearchResult& >( result ).setScoreWeights( scoreWeights );

		int fileNumChargeStates = g_rtConfig->NumChargeStates;
        if( varsFromFile.count( "SearchStats: MaxChargeState" ) )
			fileNumChargeStates = lexical_cast<int>( varsFromFile["SearchStats: MaxChargeState"] );

		double decoyRatio = 1.0;
		if( g_rtConfig->HasDecoyDatabase )
			decoyRatio = (double) proteins.numReals / (double) proteins.numDecoys;

		START_PROFILER(3);
		fileSpectra.calculateFDRs( fileNumChargeStates, decoyRatio, proteins.decoyPrefix, pQonversionDetailsStream );
		STOP_PROFILER(3);

		size_t passingSpectraCount;
		if( keepOnlyPassingSpectra )
		{
			SpectraList passingSpectra, failingSpectra;
			fileSpectra.filterByFDR( g_rtConfig->MaxFDR, &passingSpectra, &failingSpectra );
			fileSpectra = passingSpectra;
			passingSpectraCount = passingSpectra.size();
			passingSpectra.clear(false);
			failingSpectra.clear();
		} else
			passingSpectraCount = fileSpectra.getPassingCountByFDR( g_rtConfig->MaxFDR );
		return passingSpectraCount;
	}

    bool CalculatePercolatorDiscriminantScore(const SpectraList& spectra)
    {
        map< string, double > scoreWeights;
        vector< string > tokens;
		split( tokens, g_rtConfig->SearchScoreWeights, boost::is_space() );
		for( size_t i=0; i < tokens.size(); i += 2 )
			scoreWeights[to_lower_copy( tokens[i] )] = lexical_cast<double>( tokens[i+1] );

        BOOST_FOREACH(Spectrum* s, spectra)
        {
            BOOST_FOREACH(const SearchResult& result, s->resultSet)
				const_cast< SearchResult& >( result ).setScoreWeights( scoreWeights );
            
            BOOST_FOREACH(const SearchResult& result, s->topTargetHits)
				const_cast< SearchResult& >( result ).setScoreWeights( scoreWeights );
            
            BOOST_FOREACH(const SearchResult& result, s->topDecoyHits)
				const_cast< SearchResult& >( result ).setScoreWeights( scoreWeights );
        }

        set<ScoreDiscriminant::ScoreInfo> scoreInfo;
        typedef pair<string, double> ScoreWeightInfo;
        BOOST_FOREACH(const ScoreWeightInfo& itr, scoreWeights)
        {
            // ignore scores with weight of 0
            if (itr.second == 0)
                continue;

            bool ascending = itr.second > 0;
            bool normalized = g_rtConfig->normalizedSearchScores.count(itr.first);
            scoreInfo.insert(ScoreDiscriminant::ScoreInfo(itr.first, ascending, normalized));
        }

        map<string, double> featureWeights;

        // Determine the optimal score discriminant to separate targets and decoys
        // NOTE: inside mutex because Percolator is not thread-safe
        simplethread_lock_mutex( &resourceMutex );
        ScoreDiscriminant scoreDiscriminant(spectra, scoreInfo);
        scoreDiscriminant.computeFeatureWeights();
        if(!scoreDiscriminant.isSuccessful)
        {
            cout << g_hostString << " failed to compute feature weights; preserving original ranks!" << endl;
            simplethread_unlock_mutex( &resourceMutex );
            return false;
        }
        simplethread_unlock_mutex( &resourceMutex );

        BOOST_FOREACH(Spectrum* s, spectra)
            BOOST_FOREACH(const SearchResult& result, s->resultSet)
            {
                SearchResult& mutableResult = (const_cast<SearchResult&>(result));
                mutableResult.computeDiscriminantScore( scoreDiscriminant.featureWeights, g_rtConfig->normalizedSearchScores );
            }
        return true;
    }

    void RerankResults(const SpectraList& spectra, const map<string, double>& scoreWeights)
    {
        // Use the discriminant to change the ranks of each PSM.
        BOOST_FOREACH(Spectrum* s, spectra)
        {
            Spectrum::SearchResultSetType newResultSet;
            BOOST_FOREACH(const SearchResult& result, s->resultSet)
            {
                SearchResult& mutableResult = (const_cast<SearchResult&>(result));
                mutableResult.setScoreWeights(scoreWeights);
                newResultSet.insert(mutableResult);
            }
            s->resultSet = newResultSet;
            s->resultSet.calculateRanks();
        }
    }

	const char* specificityStrings[] = { "non-specific", "semi-specific", "fully specific" };

	simplethread_return_t idpQonvertThread( simplethread_arg_t threadArg )
	{
		simplethread_lock_mutex( &resourceMutex );
		simplethread_id_t threadId = simplethread_get_id();
		WorkerThreadMap* threadMap = (WorkerThreadMap*) threadArg;
		BaseWorkerInfo* threadInfo = threadMap->find( threadId )->second;
		//cout << threadInfo->workerHostString << " is initialized." << endl;
		simplethread_unlock_mutex( &resourceMutex );

		SpectraList fileSpectra;
		RunTimeVariableMap varsFromFile( "DynamicMods StaticMods NumChargeStates" );
		//varsFromFile["ProteinDatabase"] = fastaFile;

		while( true )
		{
			string inputFilename;

			simplethread_lock_mutex( &resourceMutex );
			if( g_rtConfig->inputFilepaths.empty() )
			{
				simplethread_unlock_mutex( &resourceMutex );
				break;
			} else
			{
				inputFilename = *g_rtConfig->inputFilepaths.begin();
				g_rtConfig->inputFilepaths.erase( g_rtConfig->inputFilepaths.begin() );
			}
			simplethread_unlock_mutex( &resourceMutex );

			try
			{
				if( !TestFileType( inputFilename, "msms_pipeline_analysis" ) )
					continue;
				SpectraList validationList; // for validating search parameters
				validationList.readPepXml( inputFilename, 0, varsFromFile );
				if( GetFilenameFromFilepath( g_rtConfig->ProteinDatabase ) != GetFilenameFromFilepath( varsFromFile["ProteinDatabase"] ) )
				{
					cerr << "Warning qonverting \"" << inputFilename << "\": protein database for qonversion (" << g_rtConfig->ProteinDatabase <<
						") does not match database from file (" << varsFromFile["ProteinDatabase"] << ")" << endl;
				}

				string sqtHeader, startTime, finishTime;

				//varsFromFile["NumChargeStates"] = g_rtConfig->NumChargeStates;

				simplethread_lock_mutex( &ioMutex );
			    try
			    {
				    if( !TestFileType( inputFilename, "msms_pipeline_analysis" ) )
				    {
					    simplethread_unlock_mutex( &ioMutex );
					    continue;
				    }
				    START_PROFILER(1);
				    cout << "Reading PepXML file from filepath: \"" << inputFilename << "\"" << endl;

                    // read all results if reranking is enabled
                    if( g_rtConfig->PercolatorReranking )
				        fileSpectra.readPepXml( inputFilename, -1, varsFromFile );
                    else
                        fileSpectra.readPepXml( inputFilename, g_rtConfig->MaxResultRank, varsFromFile );

				    STOP_PROFILER(1);
    				simplethread_unlock_mutex( &ioMutex );
                } catch( std::exception& e )
			    {
				    cerr << "Caught exception reading pepXML \"" << inputFilename + "\": " << e.what() << endl;
	    			simplethread_unlock_mutex( &ioMutex );
                    continue;
			    }

				ResidueMap fileResidueMap( *g_residueMap );
				if( varsFromFile.count( "DynamicMods" ) )
					fileResidueMap.setDynamicMods( varsFromFile["DynamicMods"] );
				// no longer include static mods in the output
				//if( varsFromFile.count( "StaticMods" ) )
				//		fileResidueMap.setStaticMods( varsFromFile["StaticMods"] );

		        int fileNumChargeStates = g_rtConfig->NumChargeStates;
                if( varsFromFile.count( "SearchStats: MaxChargeState" ) )
			        fileNumChargeStates = lexical_cast<int>( varsFromFile["SearchStats: MaxChargeState"] );

				int minSpecificity = 2;
				for( SpectraList::ListIterator sItr = fileSpectra.begin();
					sItr != fileSpectra.end();
					++sItr )
				{
					minSpecificity = min( (int) (*sItr)->numTerminiCleavages, minSpecificity );
				}

				if( !g_rtConfig->normalizedSearchScores.empty() )
				{
                    fileSpectra.normalizeSearchScores( g_rtConfig->normalizedSearchScores, fileNumChargeStates );
				}

                // set top target and decoy hits
                BOOST_FOREACH(Spectrum* s, fileSpectra)
                {
                    BOOST_FOREACH(const SearchResult& r, s->resultSet)
                    {
                        SearchResult::DecoyState decoyState = r.getDecoyState(g_rtConfig->DecoyPrefix);
                        if( decoyState == SearchResult::DecoyState_Decoy )
                            s->topDecoyHits.insert(r);
                        else if( decoyState == SearchResult::DecoyState_Target )
                            s->topTargetHits.insert(r);
                    }
                }

				ostream* pSummaryTextStream = NULL;
				ostream* pDetailsTextStream = NULL;
				if( g_rtConfig->WriteQonversionDetails )
				{
					pSummaryTextStream = new ostringstream;
					pDetailsTextStream = new ostringstream;
					(*pSummaryTextStream) << setfill(' ') << setw(20) << "";
					(*pSummaryTextStream) << setw(15) << left << "Overall Total";
					for( int t=2; minSpecificity < 2 && t >= minSpecificity; --t )
						(*pSummaryTextStream) << setw(25) << left << (string(specificityStrings[t]) + " Total");
					for( int z=1; minSpecificity < 2 && z <= fileNumChargeStates; ++z )
						(*pSummaryTextStream) << setw(11) << left << (format("+%d Total") % z).str();
					for( int t=2; t >= minSpecificity; --t )
						for( int z=1; z <= fileNumChargeStates; ++z )
							(*pSummaryTextStream) << setw(20) << left << (format("+%d %s") % z % specificityStrings[t]).str();
					(*pSummaryTextStream) << "\n";

					(*pDetailsTextStream) << setw(9) << left << "Ordinal"
						<< setw(14) << left << "NativeID"
						<< setw(7) << left << "Index"
						<< setw(8) << left << "Charge"
						<< setw(12) << left << "DecoyState"
						<< setw(11) << left << "NumReals"
						<< setw(12) << left << "NumDecoys"
						<< setw(14) << left << "NumAmbiguous"
						<< setw(12) << left << "TotalScore"
						<< setw(8) << left << "FDR"
						<< "  " << "ScoreList"
						<< "\n";
				}

				size_t totalPassingSpectraCount = 0;
				size_t totalPossibleSpectraCount = 0;
				map< int, size_t > totalPassingSpectraCountByCharge;
				map< int, size_t > totalPassingSpectraCountByTerminiCleavages;
				map< int, map< int, size_t > > totalPassingSpectraCountByChargeByTerminiCleavages;
				map< int, size_t > fileSpectraCountByCharge;
				map< int, size_t > fileSpectraCountByTerminiCleavages;
				map< int, map< int, size_t > > fileSpectraCountByChargeByTerminiCleavages;

				map< int, map< int, double > > scoreThresholdByChargeByTerminiCleavages;


				START_PROFILER(2);
				startTime = GetDateString() + '@' + GetTimeString();
				map< string, double > scoreWeights;

				if( g_rtConfig->PercolatorScore )
                {
                    if( CalculatePercolatorDiscriminantScore(fileSpectra) )
                    {
                        scoreWeights["f-score"] = 1;

                        if( g_rtConfig->PercolatorReranking )
                            RerankResults(fileSpectra, scoreWeights);
                    }
                }

                if( g_rtConfig->OptimizeScoreWeights )
				{
					const size_t MAX_PERMUTATIONS = g_rtConfig->OptimizeScorePermutations;

					vector< permutation >* pScorePermutations;
					if( g_rtConfig->permutations.size() <= MAX_PERMUTATIONS )
						pScorePermutations = &g_rtConfig->permutations;
					else
					{
						pScorePermutations = new vector< permutation >( g_rtConfig->permutations.begin(), g_rtConfig->permutations.begin()+MAX_PERMUTATIONS );
						std::random_shuffle( pScorePermutations->begin(), pScorePermutations->end() );
					}
					vector< permutation >& scorePermutations = *pScorePermutations;

					map< int, map< int, size_t > > bestPermutationByChargeStateByTerminiCleavage;
					map< int, map< int, SpectraList > > fileSpectraByChargeStateByTerminiCleavages;

					for( int t=2; t >= minSpecificity; --t )
					{
						for( int z=1; z <= fileNumChargeStates; ++z )
						{
							fileSpectra.filterByChargeStateAndTerminiCleavages( z, t, &fileSpectraByChargeStateByTerminiCleavages[z][t] );

							map< size_t, vector< size_t > > permutationInfo;
							for( size_t i=0; i < scorePermutations.size(); ++i )
							{
								permutation& p = scorePermutations[i];
								//cout << "+" << z << " validating with score weights: " << p << endl;
								for( permutation::iterator itr = p.begin(); itr != p.end(); ++itr )
									scoreWeights[itr->first] = lexical_cast<double>( itr->second );
								size_t passingSpectraCount = QonvertSpectra( fileSpectraByChargeStateByTerminiCleavages[z][t], varsFromFile, scoreWeights, NULL, false );
								permutationInfo[ passingSpectraCount ].push_back(i);
								//cout << "+" << z << " results within " << g_rtConfig->MaxFDR << " FDR: " << passingSpectraCount << endl;
							}

							size_t passingSpectraCount = permutationInfo.rbegin()->first;
							size_t bestPermutationIndex = permutationInfo.rbegin()->second.front();
							bestPermutationByChargeStateByTerminiCleavage[z][t] = bestPermutationIndex;
							totalPassingSpectraCount += passingSpectraCount;
							totalPassingSpectraCountByCharge[z] += passingSpectraCount;
							totalPassingSpectraCountByTerminiCleavages[t] += passingSpectraCount;
							totalPassingSpectraCountByChargeByTerminiCleavages[z][t] += passingSpectraCount;
							fileSpectraCountByCharge[z] += fileSpectraByChargeStateByTerminiCleavages[z][t].size();
							fileSpectraCountByTerminiCleavages[t] += fileSpectraByChargeStateByTerminiCleavages[z][t].size();
							fileSpectraCountByChargeByTerminiCleavages[z][t] += fileSpectraByChargeStateByTerminiCleavages[z][t].size();
							cout << "Optimized score weights for +" << z << ": " << scorePermutations[ bestPermutationByChargeStateByTerminiCleavage[z][t] ] << endl;
							cout << "At max. FDR of " << g_rtConfig->MaxFDR << ": " << passingSpectraCount << " of " << fileSpectraByChargeStateByTerminiCleavages[z][t].size() << " +" << z << " " << specificityStrings[t] << " results pass." << endl;
						}
					}

					for( int t=2; t >= minSpecificity; --t )
						cout << "At max. FDR of " << g_rtConfig->MaxFDR << ": " << totalPassingSpectraCountByTerminiCleavages[t] << " of " << fileSpectraCountByTerminiCleavages[t] << " total " << specificityStrings[t] << " results pass." << endl;

					for( int z=1; z <= fileNumChargeStates; ++z )
						cout << "At max. FDR of " << g_rtConfig->MaxFDR << ": " << totalPassingSpectraCountByCharge[z] << " of " << fileSpectraCountByCharge[z] << " total +" << z << " results pass." << endl;

					totalPossibleSpectraCount = fileSpectra.size();
					cout << "At max. FDR of " << g_rtConfig->MaxFDR << ": " << totalPassingSpectraCount << " of " << fileSpectra.size() << " total results pass." << endl;

					fileSpectra.clear(false);
					for( int t=2; t >= minSpecificity; --t )
					{
						for( int z=1; z <= fileNumChargeStates; ++z )
						{
							permutation& bestPermutation = scorePermutations[ bestPermutationByChargeStateByTerminiCleavage[z][t] ];
							for( permutation::iterator itr = bestPermutation.begin(); itr != bestPermutation.end(); ++itr )
								scoreWeights[itr->first] = lexical_cast<double>( itr->second );
							QonvertSpectra( fileSpectraByChargeStateByTerminiCleavages[z][t], varsFromFile, scoreWeights, pDetailsTextStream );
							fileSpectra.insert( fileSpectraByChargeStateByTerminiCleavages[z][t].begin(), fileSpectraByChargeStateByTerminiCleavages[z][t].end(), fileSpectra.end() );
							scoreThresholdByChargeByTerminiCleavages[z][t] = fileSpectraByChargeStateByTerminiCleavages[t][z].getScoreThresholdByFDR( g_rtConfig->MaxFDR );
							fileSpectraByChargeStateByTerminiCleavages[z][t].clear(false);
						}
					}

					if( g_rtConfig->permutations.size() > MAX_PERMUTATIONS )
						delete pScorePermutations;
				} else
				{
					if (scoreWeights.empty())
                    {
                        vector< string > tokens;
					    split( tokens, g_rtConfig->SearchScoreWeights, boost::is_space() );
					    for( size_t i=0; i < tokens.size(); i += 2 )
                        {
                            string scoreName = to_lower_copy( tokens[i] );
						    if( g_rtConfig->normalizedSearchScores.count(scoreName) > 0 )
                                scoreName += "_norm";
							scoreWeights[scoreName] = lexical_cast<double>( tokens[i+1] );
                        }
                    }

					cout << "Static score weights for results: " << scoreWeights << endl;
					map< int, map< int, SpectraList > > fileSpectraByChargeStateByTerminiCleavages;
					for( int t=2; t >= minSpecificity; --t )
					{
						for( int z=1; z <= fileNumChargeStates; ++z )
						{
							fileSpectra.filterByChargeStateAndTerminiCleavages( z, t, &fileSpectraByChargeStateByTerminiCleavages[z][t] );
							size_t passingSpectraCount = QonvertSpectra( fileSpectraByChargeStateByTerminiCleavages[z][t], varsFromFile, scoreWeights, NULL, false );
							totalPassingSpectraCount += passingSpectraCount;
							totalPassingSpectraCountByCharge[z] += passingSpectraCount;
							totalPassingSpectraCountByTerminiCleavages[t] += passingSpectraCount;
							totalPassingSpectraCountByChargeByTerminiCleavages[z][t] += passingSpectraCount;
							fileSpectraCountByCharge[z] += fileSpectraByChargeStateByTerminiCleavages[z][t].size();
							fileSpectraCountByTerminiCleavages[t] += fileSpectraByChargeStateByTerminiCleavages[z][t].size();
							fileSpectraCountByChargeByTerminiCleavages[z][t] += fileSpectraByChargeStateByTerminiCleavages[z][t].size();
							cout << "At max. FDR of " << g_rtConfig->MaxFDR << ": " << passingSpectraCount << " of " << fileSpectraByChargeStateByTerminiCleavages[z][t].size() << " +" << z << " " << specificityStrings[t] << " results pass." << endl;
						}
					}

					for( int t=2; t >= minSpecificity; --t )
						cout << "At max. FDR of " << g_rtConfig->MaxFDR << ": " << totalPassingSpectraCountByTerminiCleavages[t] << " of " << fileSpectraCountByTerminiCleavages[t] << " total " << specificityStrings[t] << " results pass." << endl;

					for( int z=1; z <= fileNumChargeStates; ++z )
						cout << "At max. FDR of " << g_rtConfig->MaxFDR << ": " << totalPassingSpectraCountByCharge[z] << " of " << fileSpectraCountByCharge[z] << " total +" << z << " results pass." << endl;

					totalPossibleSpectraCount = fileSpectra.size();
					cout << "At max. FDR of " << g_rtConfig->MaxFDR << ": " << totalPassingSpectraCount << " of " << fileSpectra.size() << " total results pass." << endl;

					fileSpectra.clear(false);
					for( int z=1; z <= fileNumChargeStates; ++z )
					{
						for( int t=minSpecificity; t <= 2; ++t )
						{
							QonvertSpectra( fileSpectraByChargeStateByTerminiCleavages[z][t], varsFromFile, scoreWeights, pDetailsTextStream );
							fileSpectra.insert( fileSpectraByChargeStateByTerminiCleavages[z][t].begin(), fileSpectraByChargeStateByTerminiCleavages[z][t].end(), fileSpectra.end() );
							scoreThresholdByChargeByTerminiCleavages[z][t] = fileSpectraByChargeStateByTerminiCleavages[z][t].getScoreThresholdByFDR( g_rtConfig->MaxFDR );
							fileSpectraByChargeStateByTerminiCleavages[z][t].clear(false);
						}
					}
				}
				STOP_PROFILER(2);

				if( pSummaryTextStream )
				{
					ostream& summary = *pSummaryTextStream;
					summary << setw(20) << left << "Valid IDs" << setw(15) << left << totalPassingSpectraCount;
					for( int t=2; minSpecificity < 2 && t >= minSpecificity; --t )
						summary << setw(25) << left << totalPassingSpectraCountByTerminiCleavages[t];
					for( int z=1; minSpecificity < 2 && z <= fileNumChargeStates; ++z )
						summary << setw(11) << left << totalPassingSpectraCountByCharge[z];
					for( int t=2; t >= minSpecificity; --t )
						for( int z=1; z <= fileNumChargeStates; ++z )
							summary << setw(20) << left << totalPassingSpectraCountByChargeByTerminiCleavages[z][t];
					summary << "\n";

					summary << setw(20) << left << "Possible IDs" << setw(15) << left << totalPossibleSpectraCount;
					for( int t=2; minSpecificity < 2 && t >= minSpecificity; --t )
						summary << setw(25) << left << fileSpectraCountByTerminiCleavages[t];
					for( int z=1; minSpecificity < 2 && z <= fileNumChargeStates; ++z )
						summary << setw(11) << left << fileSpectraCountByCharge[z];
					for( int t=2; t >= minSpecificity; --t )
						for( int z=1; z <= fileNumChargeStates; ++z )
							summary << setw(20) << left << fileSpectraCountByChargeByTerminiCleavages[z][t];
					summary << "\n";

					summary << setw(20) << left << "Score Threshold" << setw(15) << left << "n/a";
					for( int t=2; minSpecificity < 2 && t >= minSpecificity; --t )
						summary << setw(25) << left << "n/a";
					for( int z=1; minSpecificity < 2 && z <= fileNumChargeStates; ++z )
						summary << setw(11) << left << "n/a";
					for( int t=2; t >= minSpecificity; --t )
						for( int z=1; z <= fileNumChargeStates; ++z )
							summary << setw(20) << left << scoreThresholdByChargeByTerminiCleavages[z][t];
					summary << "\n";
				}

				fileSpectra.maxFDR = g_rtConfig->MaxFDR;
				fileSpectra.sort( spectraSortByID() );
				for( SpectraList::iterator sItr = fileSpectra.begin(); sItr != fileSpectra.end(); ++sItr )
				{
					Spectrum* s = *sItr;

					s->resultSet.calculateRanks();

					for( Spectrum::SearchResultSetType::reverse_iterator mItr = s->resultSet.rbegin(); mItr != s->resultSet.rend(); ++mItr )
					{
						if( mItr->rank > (size_t) g_rtConfig->MaxResultRank )
							break;
						
						pair<map<string, PeptideInfo>::iterator, bool> insertResult =
                            fileSpectra.peptides.insert(make_pair(mItr->sequence(), PeptideInfo()));

						PeptideInfo& pep = insertResult.first->second;

						if( insertResult.second ||
                            mItr->specificTermini() > pep.specificTermini())
						{
							pep = PeptideInfo( DigestedPeptide(mItr->sequence().begin(),
                                                               mItr->sequence().end(),
                                                               string::npos,
                                                               mItr->missedCleavages(),
                                                               mItr->NTerminusIsSpecific(),
                                                               mItr->CTerminusIsSpecific()) );
						}
						
						pep.spectra.insert(s);

						for( ProteinLociByName::iterator lItr = mItr->lociByName.begin(); lItr != mItr->lociByName.end(); ++lItr )
						{
							bool lookupProtein = true;
							if( !g_rtConfig->HasDecoyDatabase )
							{
								// determine if current hit is a decoy
								lookupProtein = lItr->name.find( g_rtConfig->DecoyPrefix ) != 0;
							}

							if( lookupProtein )
							{
                                size_t index = proteins.find(lItr->name);
								if( index == proteins.size() )
									cerr << "Warning while validating source \"" << s->id.source << "\": protein \"" << lItr->name << "\" not found in database" << endl;
								else
								{
									ProteinInfo& pro = fileSpectra.proteins[ lItr->name ] = ProteinInfo( proteins[index] );

									pro.spectra.insert(s);
									pro.peptides.insert( &pep );
									pep.proteins.insert( &pro );
								}
							} else
							{
								pair< map<string, ProteinInfo>::iterator, bool > insertResult = fileSpectra.proteins.insert( pair< string, ProteinInfo >( lItr->name, ProteinInfo( lItr->name ) ) );
								ProteinInfo& pro = insertResult.first->second;
								pro.spectra.insert(s);
								pro.peptides.insert( &pep );
								pep.proteins.insert( &pro );
							}
						}
					}
				}
				finishTime = GetDateString() + '@' + GetTimeString();

				string outputFilepathString = inputFilename;
				if( g_rtConfig->PreserveInputHierarchy )
				{
					boost::filesystem::path outputPath( outputFilepathString );
					if( outputPath.has_root_path() )
						outputFilepathString = outputFilepathString.substr( outputPath.root_path().string().length() );
					outputPath = outputFilepathString;
					boost::filesystem::create_directories( outputPath.branch_path() );
					outputPath = boost::filesystem::change_extension( outputPath, g_rtConfig->OutputSuffix + ".idpXML" );
					outputFilepathString = outputPath.string();
				} else
					outputFilepathString = GetFilenameWithoutExtension( inputFilename ) + g_rtConfig->OutputSuffix + ".idpXML";

				if( g_rtConfig->WriteQonversionDetails && pDetailsTextStream && pSummaryTextStream )
				{
					string qonversionDetailsFilepath = boost::filesystem::change_extension( boost::filesystem::path(outputFilepathString), "-qonversion.txt" ).string();
					ofstream qonversionDetailsFile( qonversionDetailsFilepath.c_str(), ios::trunc );
					qonversionDetailsFile << reinterpret_cast<ostringstream*>(pSummaryTextStream)->str();
					qonversionDetailsFile << endl;
					qonversionDetailsFile << reinterpret_cast<ostringstream*>(pDetailsTextStream)->str();
				}

				START_PROFILER(4);
				string outputString = fileSpectra.writePeptides( "", varsFromFile, sqtHeader, startTime, finishTime, g_rtConfig->MaxResultRank );
				STOP_PROFILER(4);
				simplethread_lock_mutex( &ioMutex );
				START_PROFILER(5);
				cout << "Writing validated identifications to filepath: \"" << outputFilepathString << "\"" << endl;
				ofstream outputFile( outputFilepathString.c_str(), ios::binary );
				outputFile << outputString;
				STOP_PROFILER(5);
				simplethread_unlock_mutex( &ioMutex );

				PRINT_PROFILERS(cout, threadInfo->workerHostString);
			} catch( std::exception& e )
			{
				cerr << "Caught exception while validating \"" << inputFilename + "\": " << e.what() << endl;
			}
			fileSpectra.clear();
			fileSpectra.peptides.clear();
			fileSpectra.proteins.clear();
		}

		return 0;
	}

	int InitProcess( argList_t& args )
	{
		g_rtConfig = new RunTimeConfig;
		g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
		g_residueMap = new ResidueMap;
		g_endianType = GetHostEndianType();
		g_numWorkers = GetNumProcessors();
		g_hostString = "Process #" + lexical_cast<string>( g_pid ) + " (" + GetHostname() + ")";

		// First set the working directory, if provided
		for( size_t i=1; i < args.size(); ++i )
		{
			if( args[i] == "-workdir" && i+1 <= args.size() )
			{
				chdir( args[i+1].c_str() );
				args.erase( args.begin() + i );
			} else if( args[i] == "-cpus" && i+1 <= args.size() )
			{
				g_numWorkers = atoi( args[i+1].c_str() );
				args.erase( args.begin() + i );
			} else
				continue;

			args.erase( args.begin() + i );
			--i;
		}

		for( size_t i=1; i < args.size(); ++i )
		{
			if( args[i] == "-cfg" && i+1 <= args.size() )
			{
				if( g_rtConfig->initializeFromFile( args[i+1] ) )
				{
					cerr << g_hostString << " could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
					return QONVERT_ERROR_RUNTIME_CONFIG_FILE_FAILURE;
				}
				args.erase( args.begin() + i );

			} else if( args[i] == "-rescfg" && i+1 <= args.size() )
			{
				if( g_residueMap->initializeFromFile( args[i+1] ) )
				{
					cerr << g_hostString << " could not find residue masses at \"" << args[i+1] << "\"." << endl;
					return QONVERT_ERROR_RESIDUE_CONFIG_FILE_FAILURE;
				}
				args.erase( args.begin() + i );
			} else if( args[i] == "-b" && i+1 <= args.size() )
			{
				string batchFilename;
				ifstream batchFile( args[i+1].c_str() );
				while( !batchFile.eof() )
				{
					std::getline( batchFile, batchFilename );
					FindFilesByMask( batchFilename, g_rtConfig->inputFilepaths );
				}
				args.erase( args.begin() + i );
			} else
				continue;

			args.erase( args.begin() + i );
			--i;
		}

		if( g_rtConfig->inputFilepaths.empty() && args.size() < 2 )
		{
			cerr <<		"Not enough arguments.\nUsage: idpQonvert [<pepXML filemask> [<another pepXML filemask> ...]]" <<
				" [-b <filepath to list of input pepXML filemasks, one per line>]" << endl;
			return QONVERT_ERROR_NOT_ENOUGH_ARGUMENTS;
		}

		if( !g_rtConfig->initialized() )
		{
			if( g_rtConfig->initializeFromFile() )
			{
				cerr << g_hostString << " could not find the default configuration file (hard-coded defaults in use)." << endl;
			}
		}

		if( !g_residueMap->initialized() )
		{
			if( g_residueMap->initializeFromFile() )
			{
				cerr << g_hostString << " could not find the default residue masses file (hard-coded defaults in use)." << endl;
			}
		}

		// Command line overrides happen after config file has been distributed but before PTM parsing
		RunTimeVariableMap vars = g_rtConfig->getVariables();
		for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
		{
			string varName;
			varName += "-" + itr->first;

			for( size_t i=1; i < args.size(); ++i )
			{
				if( args[i] == varName && i+1 < args.size() )
				{
					//cout << varName << " " << itr->second << " " << args[i+1] << endl;
					itr->second = args[i+1];
					args.erase( args.begin() + i );
					args.erase( args.begin() + i );
					--i;
				}
			}
		}

		try
		{
			g_rtConfig->setVariables( vars );
		} catch( std::exception& e )
		{
			if( g_pid == 0 ) cerr << g_hostString << " had an error while overriding runtime variables: " << e.what() << endl;
			return QONVERT_ERROR_RUNTIME_CONFIG_OVERRIDE_FAILURE;
		}

		if( g_pid == 0 )
		{
			for( size_t i=1; i < args.size(); ++i )
			{
				if( args[i] == "-dump" )
				{
					g_rtConfig->dump();
					g_residueMap->dump();
					args.erase( args.begin() + i );
					--i;
				}
			}

			for( size_t i=1; i < args.size(); ++i )
			{
				if( args[i][0] == '-' )
				{
					cerr << "Warning: ignoring unrecognized parameter \"" << args[i] << "\"" << endl;
					args.erase( args.begin() + i );
					--i;
				}
			}
		}

		return 0;
	}
}
}

int main( int argc, char* argv[] )
{
    cout << "IDPickerQonvert " << freicore::idpicker::Version::str() << " (" << freicore::idpicker::Version::LastModified() << ")\n" <<
            "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
            IDPQONVERT_LICENSE << endl;

	argList_t args( argv, argv+argc );

	int rv;
	if( ( rv = InitProcess(args) ) > 0 )
		return rv;

	INIT_PROFILERS(10);

	for( size_t i = 1; i < args.size(); ++i )
		freicore::FindFilesByMask( args[i], g_rtConfig->inputFilepaths );

	if( g_rtConfig->inputFilepaths.empty() )
	{
		cout << "Error: no files found matching input filemasks." << endl;
		return QONVERT_ERROR_NO_INPUT_FILES_FOUND;
	}


	for( fileList_t::iterator fItr = g_rtConfig->inputFilepaths.begin();
		fItr != g_rtConfig->inputFilepaths.end() && g_rtConfig->ProteinDatabase.empty();
		++fItr )
	{
		if( !TestFileType( *fItr, "msms_pipeline_analysis" ) )
			continue;
		RunTimeVariableMap varsFromFile;
		SpectraList validationList; // for reading protein database from the first pepXML file
		try
		{
			validationList.readPepXml( *fItr, 0, varsFromFile );
		} catch( std::exception& e )
		{
			cerr << "Error: parsing file \"" << *fItr << "\": " << e.what() << endl;
		}
		g_rtConfig->ProteinDatabase = varsFromFile["ProteinDatabase"];
	}

	if( g_rtConfig->ProteinDatabase.empty() )
	{
		cerr << "Error: unable to determine protein database to use from input files" << endl;
		return QONVERT_ERROR_FASTA_FILE_FAILURE;
	}

	if( !exists(g_rtConfig->ProteinDatabase) )
	{
		string missingPath = path(g_rtConfig->ProteinDatabase).leaf();
		if( !g_rtConfig->ProteinDatabaseSearchPath.empty() )
		{
			vector<string> searchPathList;
			boost::split(searchPathList, g_rtConfig->ProteinDatabaseSearchPath, boost::is_any_of(";"));
			g_rtConfig->ProteinDatabase = FindFileInSearchPath( missingPath, searchPathList );
		}

		if( g_rtConfig->ProteinDatabaseSearchPath.empty() )
		{
			cerr << "Error: unable to find protein database \"" << missingPath << "\"; have you set the search path?" << endl;
			return QONVERT_ERROR_FASTA_FILE_FAILURE;
		} else if( g_rtConfig->ProteinDatabase.empty() )
		{
			cerr << "Error: unable to find protein database \"" << missingPath << "\" in the search path" << endl;
			return QONVERT_ERROR_FASTA_FILE_FAILURE;
		}
	}

	START_PROFILER(0);
	try
	{
		Timer readTime(true);
		cout << "Reading protein database from filepath: \"" << g_rtConfig->ProteinDatabase << "\"" << endl;
		proteins.decoyPrefix = g_rtConfig->DecoyPrefix;
		proteins.readFASTA( g_rtConfig->ProteinDatabase, " " );
		cout << "Finished reading database with " << proteins.numReals << " real sequences and " << proteins.numDecoys << " decoys; " << readTime.End() << " seconds elapsed." << endl;
        if( proteins.numReals == 0 )
            return QONVERT_ERROR_NO_TARGET_PROTEINS;
	} catch( std::exception& e )
	{
		cerr << "Error: " << e.what() << endl;
		return QONVERT_ERROR_UNHANDLED_EXCEPTION;
	}
	STOP_PROFILER(0);

    // make sure at least one decoy protein is found in every file
    BOOST_FOREACH(const string& filepath, g_rtConfig->inputFilepaths)
    {
        string line;
        ifstream pepXml(filepath.c_str());
        string attributePrefix = "protein=\"" + g_rtConfig->DecoyPrefix;
        bool foundDecoy = false;
        while( !foundDecoy && getline(pepXml, line) )
            if( line.find(attributePrefix) != string::npos )
                foundDecoy = true;

        if( !foundDecoy )
        {
            cerr << "Error: file \"" << filepath << "\" has no proteins with the decoy prefix" << endl;
		    return QONVERT_ERROR_NO_DECOY_PROTEINS;
        }
    }

	PRINT_PROFILERS(cout, g_hostString);

	WorkerThreadMap workerThreads;
	simplethread_handle_array_t workerHandles;

	simplethread_create_mutex( &resourceMutex );
	simplethread_create_mutex( &ioMutex );
	simplethread_lock_mutex( &resourceMutex );
	for( int t = 0; t < g_numWorkers; ++t )
	{
		simplethread_id_t threadId;
		simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &idpQonvertThread, &workerThreads );
		workerThreads[ threadId ] = new BaseWorkerInfo( t, 0, 0 );
		workerHandles.array.push_back( threadHandle );
	}
	simplethread_unlock_mutex( &resourceMutex );
	simplethread_join_all( &workerHandles );
	simplethread_destroy_mutex( &resourceMutex );
	simplethread_destroy_mutex( &ioMutex );

	return 0;
}

namespace freicore {
namespace idpicker {

void SpectraList::normalizeSearchScores( const set<string>& scoreNames, int numChargeStates )
{
	for( int z=1; z <= numChargeStates; ++z )
	{
		map< string, vector< double > > scoreListByScoreName;
		BOOST_FOREACH(string scoreName, scoreNames)
			scoreListByScoreName[ scoreName ].resize(0);

		BOOST_FOREACH( Spectrum* s, *this )
        {
			if( s->id.charge != z )
				continue;

			BOOST_FOREACH( const SearchResult& result, s->resultSet )
            {
				SearchScoreList& scoreList = const_cast< SearchScoreList& >( result.scoreList );

				for( size_t i=0; i < scoreList.size(); ++i )
				{
					map< string, vector< double > >::iterator itr = scoreListByScoreName.find( scoreList[i].first );
					if( itr != scoreListByScoreName.end() )
						itr->second.push_back( scoreList[i].second );
				}
			}
		}

		BOOST_FOREACH(string scoreName, scoreNames)
		{
			vector< double >& scores = scoreListByScoreName[ scoreName ];
			std::sort( scores.begin(), scores.end() );
			//cout << scoreNames[i] << ": ";// << scores << endl;
			//for( int percentile = 0; percentile <= 100; percentile += 10 )
			//		cout << scores[ max(1, (percentile * scores.size() / 100))-1 ] << " ";
			//cout << endl;
		}

        if( g_rtConfig->normalizationMethod == RunTimeConfig::NormalizationMethod_Quantile )
        {
		    BOOST_FOREACH( Spectrum* s, *this )
            {
			    if( s->id.charge != z )
				    continue;

			    BOOST_FOREACH( const SearchResult& result, s->resultSet )
                {
				    SearchScoreList normalizedScoreList;
				    SearchScoreList& scoreList = const_cast< SearchScoreList& >( result.scoreList );
				    for( size_t i=0; i < scoreList.size(); ++i )
				    {
					    map< string, vector< double > >::iterator itr = scoreListByScoreName.find( scoreList[i].first );
					    if( itr != scoreListByScoreName.end() )
					    {
						    vector< double >& scores = itr->second;
						    vector< double >::iterator lowerItr = std::lower_bound( scores.begin(), scores.end(), scoreList[i].second );
						    vector< double >::iterator upperItr = std::upper_bound( scores.begin(), scores.end(), scoreList[i].second );
						    --upperItr;
						    size_t lowerIndex = lowerItr - scores.begin();
						    size_t upperIndex = upperItr - scores.begin();
						    normalizedScoreList.push_back( make_pair( scoreList[i].first + "_norm", double(lowerIndex + upperIndex) / ( scores.size() * 2.0 ) ) );
					    }
				    }
				    scoreList.insert( scoreList.end(), normalizedScoreList.begin(), normalizedScoreList.end() );
			    }
		    }
        }
        else // NormalizationMethod_Linear
        {
            BOOST_FOREACH( Spectrum* s, *this )
            {
			    if( s->id.charge != z )
				    continue;

			    BOOST_FOREACH( const SearchResult& result, s->resultSet )
                {
				    SearchScoreList normalizedScoreList;
				    SearchScoreList& scoreList = const_cast< SearchScoreList& >( result.scoreList );
				    for( size_t i=0; i < scoreList.size(); ++i )
				    {
					    map< string, vector< double > >::iterator itr = scoreListByScoreName.find( scoreList[i].first );
					    if( itr != scoreListByScoreName.end() )
					    {
						    vector< double >& scores = itr->second;
                            double min = scores.front();
                            double max = scores.back();
                            double div = max - min > 0 ? max - min : 1;
						    normalizedScoreList.push_back( make_pair( scoreList[i].first + "_norm", (scoreList[i].second - min) / div ) );
					    }
				    }
				    scoreList.insert( scoreList.end(), normalizedScoreList.begin(), normalizedScoreList.end() );
			    }
		    }
        }
	}
}

	/**
	This function takes a ModificationMap (ProteoWizard) and converts them
	into a string format for representation. The format is
	mod1Pos:mod1Mass mod2Pos:mod2Mass
	N- and C-terminals are represented as 'n' and 'c' respectively.
	*/
	inline string getModString(const ModificationMap& modMap) {
		stringstream modStr;
		if( !modMap.empty() )
		{
			bool firstModOut = false;
			for( ModificationMap::const_iterator itr4 = modMap.begin();
				itr4 != modMap.end();
				++itr4 )
			{
				if( itr4->first == ModificationMap::NTerminus() )
				{
					modStr << "n:" << itr4->second.monoisotopicDeltaMass();
					firstModOut = true;
				} else if( itr4->first == ModificationMap::CTerminus() )
				{
					if(firstModOut)
						modStr << ' ';
					modStr << "c:" << itr4->second.monoisotopicDeltaMass();
					firstModOut = true;
				} else
				{
					for( ModificationList::const_iterator itr5 = itr4->second.begin();
						itr5 != itr4->second.end();
						++itr5 )
					{
						if(firstModOut)
							modStr << ' ';
						modStr << itr4->first+1 << ':' << itr5->monoisotopicDeltaMass();
						firstModOut = true;
					}
				}
			}
		}
		return modStr.str();
	}

string SpectraList::writePeptides(		const string& header,
	const RunTimeVariableMap& idVars,
	const string& sourceHeader,
	const string& validationStartTime,
	const string& validationEndTime,
	size_t maxRank )
{
	SourceReverseIndex		sourceReverseIndex;
	PeptideReverseIndex		peptideReverseIndex;
	ProteinReverseIndex		proteinReverseIndex;
	generateReverseIndex( sourceReverseIndex, peptideReverseIndex, proteinReverseIndex );

	ResidueMap residueMap( *g_residueMap );
	if( idVars.count( "DynamicMods" ) )
		residueMap.setDynamicMods( idVars.find("DynamicMods")->second );
	if( idVars.count( "StaticMods" ) )
		residueMap.setStaticMods( idVars.find("StaticMods")->second );

	stringstream xmlStream;
	SimpleXMLWriter xmlWriter;
	xmlWriter.condenseAttr_ = true;
	xmlWriter.setOutputStream( xmlStream );
	xmlWriter.startDocument();
	xmlWriter.open( "idPickerPeptides" ); // root element

	/*xmlWriter.open( "analysisParameters" );
	xmlWriter.attr( "count", vars.size() );
	for( RunTimeVariableMap::const_iterator itr = vars.begin(); itr != vars.end(); ++itr )
	{
	xmlWriter.open( "analysisParameter" );
	xmlWriter.attr( "name", itr->first );
	xmlWriter.attr( "value", itr->second );
	xmlWriter.close();
	}
	xmlWriter.close();*/ // analysisParameters

	xmlWriter.open( "proteinIndex" );
	xmlWriter.attr( "count", proteinReverseIndex.size() );
    if( idVars.count( "ProteinDatabase" ) )
        xmlWriter.attr( "database", idVars.find("ProteinDatabase")->second );
	for( ProteinReverseIndex::iterator itr = proteinReverseIndex.begin(); itr != proteinReverseIndex.end(); ++itr )
	{
		ProteinInfo& pro = *itr->second.second;
		if( pro.spectra.empty() )
			continue;

		xmlWriter.open( "protein" );
		xmlWriter.attr( "id", itr->second.first );
		xmlWriter.attr( "locus", itr->first );
		xmlWriter.attr( "decoy", pro.isDecoy() );
		xmlWriter.attr( "length", pro.getSequence().size() );
		//if( !pro.isDecoy() && !pro.desc.empty() )
		if( !pro.isDecoy() && !pro.getDescription().empty() )
			xmlWriter.attr( "description", pro.getDescription() );
		xmlWriter.close();
	}
	xmlWriter.close(); // proteinIndex

	xmlWriter.open( "peptideIndex" );
	xmlWriter.attr( "count", peptideReverseIndex.size() );
	for( PeptideReverseIndex::iterator itr = peptideReverseIndex.begin(); itr != peptideReverseIndex.end(); ++itr )
	{
		PeptideInfo& pep = *itr->second.second;
		if( pep.spectra.empty() )
			continue;

		xmlWriter.open( "peptide" );
		xmlWriter.attr( "id", itr->second.first );
		xmlWriter.attr( "sequence", itr->first );
		//Fix this to use the appropriate masses
		xmlWriter.attr( "mass", pep.monoisotopicMass() );
		xmlWriter.attr( "unique", ( pep.proteins.size() > 1 ? 0 : 1 ) );
		//Add the termini specificities of the peptide.
		xmlWriter.attr( "NTerminusIsSpecific", pep.NTerminusIsSpecific() );
        xmlWriter.attr( "CTerminusIsSpecific", pep.CTerminusIsSpecific() );
		for( set< ProteinInfo* >::iterator itr2 = pep.proteins.begin(); itr2 != pep.proteins.end(); ++itr2 )
			if( !(*itr2)->spectra.empty() )
			{
				xmlWriter.open( "locus" );
				//xmlWriter.attr( "id", proteinReverseIndex[ (*itr2)->name ].first );
				xmlWriter.attr( "id", proteinReverseIndex[ (*itr2)->getName() ].first );

				// lookup the real offset of the peptide in this instance
				simplethread_lock_mutex( &resourceMutex );
				//const string& proteinSequence = proteins[ (*itr2)->name ].getSequence();
				const string& proteinSequence = proteins[ (*itr2)->getName() ].getSequence();
				simplethread_unlock_mutex( &resourceMutex );

				size_t offset = proteinSequence.find( itr->first );
				if( offset == string::npos )
					offset = 0;
				xmlWriter.attr( "offset", offset );
				xmlWriter.close();
			}
			xmlWriter.close(); // peptide
	}
	xmlWriter.close(); // peptideIndex

	RunTimeVariableMap vars(idVars);
	RunTimeVariableMap::iterator searchEngineNameItr = find_first_of( vars, "SearchEngine\tSearchEngine: Name", "\t" );
	string searchEngineName = searchEngineNameItr == vars.end() ? "unknown" : searchEngineNameItr->second;
	if( searchEngineNameItr != vars.end() ) vars.erase( searchEngineNameItr );

	RunTimeVariableMap::iterator searchEngineVersionItr = vars.find( "SearchEngine: Version" );
	string searchEngineVersion = searchEngineVersionItr == vars.end() ? "unknown" : searchEngineVersionItr->second;
	if( searchEngineVersionItr != vars.end() ) vars.erase( searchEngineVersionItr );

	RunTimeVariableMap::iterator searchStartTimeItr = find_first_of( vars, "SearchStarted\tSearchTime: Started", "\t" );
	string searchStartTime;
	if( searchStartTimeItr != vars.end() )
	{
		searchStartTime = searchStartTimeItr->second.substr( 12, 10 ) + '@' + searchStartTimeItr->second.substr( 0, 8 );
		vars.erase( searchStartTimeItr );
	}

	RunTimeVariableMap::iterator searchStopTimeItr = find_first_of( vars, "SearchEnded\tSearchTime: Stopped", "\t" );
	string searchStopTime;
	if( searchStopTimeItr != vars.end() )
	{
		searchStopTime = searchStopTimeItr->second.substr( 12, 10 ) + '@' + searchStopTimeItr->second.substr( 0, 8 );
		vars.erase( searchStopTimeItr );
	}

	xmlWriter.open( "spectraSources" );
	xmlWriter.attr( "count", sourceReverseIndex.size() );
	for( SourceReverseIndex::iterator itr = sourceReverseIndex.begin(); itr != sourceReverseIndex.end(); ++itr )
	{
		xmlWriter.open( "spectraSource" );
		if( !(*itr->second.begin())->group.empty() )
			xmlWriter.attr( "group", (*itr->second.begin())->group );
		xmlWriter.attr( "name", itr->first );
		xmlWriter.attr( "count", itr->second.size() );

		xmlWriter.open( "processingEventList" );
		xmlWriter.attr( "count", 2 );
		{
			xmlWriter.open( "processingEvent" );
			xmlWriter.attr( "type", "identification" );
			if( !searchStartTime.empty() )
				xmlWriter.attr( "start", searchStartTime );
			if( !searchStopTime.empty() )
				xmlWriter.attr( "end", searchStopTime );
			xmlWriter.attr( "params", ( vars.size() + 2 ) );
			{
				xmlWriter.open( "processingParam" );
				xmlWriter.attr( "name", "software name" );
				xmlWriter.attr( "value", searchEngineName );
				xmlWriter.close();
				xmlWriter.open( "processingParam" );
				xmlWriter.attr( "name", "software version" );
				xmlWriter.attr( "value", searchEngineVersion );
				xmlWriter.close();
				for( RunTimeVariableMap::const_iterator itr = vars.begin(); itr != vars.end(); ++itr )
				{
					xmlWriter.open( "processingParam" );
					xmlWriter.attr( "name", itr->first );
					xmlWriter.attr( "value", itr->second );
					xmlWriter.close();
				}
			}
			xmlWriter.close(); // processingEvent

			xmlWriter.open( "processingEvent" );
			xmlWriter.attr( "type", "validation" );
			xmlWriter.attr( "start", validationStartTime );
			xmlWriter.attr( "end", validationEndTime );
			xmlWriter.attr( "params", 4 );
			{
				xmlWriter.open( "processingParam" );
				xmlWriter.attr( "name", "software name" );
				xmlWriter.attr( "value", "idpQonvert" );
				xmlWriter.close();
				xmlWriter.open( "processingParam" );
				xmlWriter.attr( "name", "software version" );
                xmlWriter.attr( "value", Version::str() );
				xmlWriter.close();
				const RunTimeVariableMap& rtVars = g_rtConfig->getVariables();
				for( RunTimeVariableMap::const_iterator itr = rtVars.begin(); itr != rtVars.end(); ++itr )
				{
					xmlWriter.open( "processingParam" );
					xmlWriter.attr( "name", itr->first );
					xmlWriter.attr( "value", itr->second );
					xmlWriter.close();
				}
			}
			xmlWriter.close(); // processingEvent
		}
		xmlWriter.close(); // processingEventList

		for( set< Spectrum*, spectraSortByID >::iterator itr2 = itr->second.begin(); itr2 != itr->second.end(); ++itr2 )
		{
			Spectrum* s = *itr2;

			map< size_t, Spectrum::SearchResultSetType > resultsByRank;

			for( Spectrum::SearchResultSetType::reverse_iterator itr3 = s->resultSet.rbegin();
				 itr3 != s->resultSet.rend() && itr3->rank <= maxRank;
				 ++itr3 )
			{
				resultsByRank[itr3->rank].insert( *itr3 );
			}

			if( !resultsByRank.empty() )
			{
				xmlWriter.open( "spectrum" );
				//xmlWriter.attr( "id", s->stringID );
				//xmlWriter.attr( "nativeID", s->nativeID );
				xmlWriter.attr( "id", s->nativeID );
				xmlWriter.attr( "index", s->id.index );
				xmlWriter.attr( "z", s->id.charge );
				xmlWriter.attr( "mass", s->mOfPrecursor );
				xmlWriter.attr( "time", s->retentionTime );
				xmlWriter.attr( "targets", s->numTargetComparisons );
				xmlWriter.attr( "decoys", s->numDecoyComparisons );
				xmlWriter.attr( "results", resultsByRank.size() );

				for( size_t rank=1; rank <= maxRank; ++rank )
				{
					if( !resultsByRank.count(rank) )
						continue;

					Spectrum::SearchResultSetType& results = resultsByRank[rank];

					if( results.empty() )
						continue;

					xmlWriter.open( "result" );
					xmlWriter.attr( "rank", rank );
					xmlWriter.attr( "FDR", results.rbegin()->fdr );
					if(results.rbegin()->hasFScore())
						xmlWriter.attr( "scores", results.rbegin()->getSearchScores(g_rtConfig->searchScoreNames));
					xmlWriter.attr( "ids", results.size() );

					for( Spectrum::SearchResultSetType::reverse_iterator itr3 = results.rbegin();
						 itr3 != results.rend();
						 ++itr3 )
					{
						string rawSequence = itr3->sequence();
						xmlWriter.open( "id" );
						xmlWriter.attr( "peptide", peptideReverseIndex[ rawSequence ].first );

						// Get the modification map and format it for XML.
						// ModStr: 8:15.996;9:15.996
						// Modification(s) before each semi-colon correspond to
						// an interpretation. This facilitates the representation
						// of ambiguous modifications in the same <id> tag.
						const ModificationMap& modMap = itr3->modifications();
						stringstream modStr;
						string str=getModString(modMap);
						if(!str.empty()) {
							modStr  << str;
						}

						// Get any alternative interpretations for the same modification and
						// format them, and append them to the modification string.
						for(vector<Peptide>::const_iterator alts = itr3->alternatives.begin(); alts != itr3->alternatives.end(); alts++) {
							const ModificationMap& alternativeModificationMap = alts->modifications();
							str = getModString(alternativeModificationMap);
							if(!str.empty()) {
								modStr << ';' << str;
							}
						}
						if( !modStr.str().empty() )
							xmlWriter.attr( "mods", modStr.str() );

						xmlWriter.close();
					}
					xmlWriter.close(); // result
				}
				xmlWriter.close(); // spectrum
			}
		}
		xmlWriter.close(); // spectraSource
	}
	xmlWriter.close(); // spectraSources

	xmlWriter.close(); // root element

	return xmlStream.str();
}
} // namespace idpicker
} // namespace freicore
