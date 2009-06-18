#include "stdafx.h"
#include "tagrecon.h"
#include "UniModXMLParser.h"
#include "BlosumMatrix.h"
#include "DeltaMasses.h"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/proteome/Version.hpp"


namespace freicore
{
namespace tagrecon
{
    WorkerThreadMap	            g_workerThreads;
	simplethread_mutex_t	    resourceMutex;

	proteinStore			    proteins;
	SpectraList			        spectra;
	SpectraTagMap		        spectraTagMapsByChargeState;

    TagreconRunTimeConfig*      g_rtConfig;

	tagIndex_t					tagIndex;
	tagMetaIndex_t				tagMetaIndex;
	tagMutexes_t				tagMutexes;
	string						tagIndexFilename;

	modMap_t					knownModifications;
	UniModXMLParser*			unimodXMLParser;
	DeltaMasses*				deltaMasses;
	BlosumMatrix*				blosumMatrix;

	/*void ProcessTagList( Spectrum* spectrum, SearchResultSet* scoreInfo, int i )
	{
		spectrum->numSequenceComparisons = 0;

		// Check every tag for this spectrum
		int tagLength;
		for( int t=0; t < min( 20, (int) spectrum->tagList.size() ); ++t )
		{
			tagLength = (int) spectrum->tagList[t].tag.length();

			// Look up the tag's instances vector in the tagIndex
			tagInstances_t* v = &tagIndex[ spectrum->tagList[t].tag ];

			//cout << spectrum->tagList[t].tag << ": " << v->size() << flush;
			simplethread_lock_mutex( &tagMutexes[ spectrum->tagList[t].tag ] );
			if( v->empty() )
				LoadTagInstancesFromIndexFile( tagIndexFilename, spectrum->tagList[t].tag, tagMetaIndex, tagIndex );
			simplethread_unlock_mutex( &tagMutexes[ spectrum->tagList[t].tag ] );
			//cout << " -> " << v->size() << endl;

			//cout << spectrum->tagList[i].tag << ":" << endl;

			// For each instance of the tag in the index, try to make a peptide with the appropriate termini masses
			// If a partial or complete peptide is successfully found, score the peptide to the spectrum using Freiquest's scorer
			// If the peptide gets a positive score:
			// - add the sequence
			// - insert its source protein into a list of protein loci for that sequence
			// If adding this peptide 
			for( int j=0; j < (int) v->size(); ++j )
			{
				//simplethread_lock_mutex( &resourceMutex );
				string* p = &proteins[ v->at(j).proteinIndex ].data;
				//simplethread_unlock_mutex( &resourceMutex );
				if( p->empty() )
					continue;

				float bestScore = 0.0f;
				string bestSequence;

				int tagOffset = v->at(j).dataOffset; // GASPGASP >TAG GASPGASP

				int minTerminusLength, maxTerminusLength;
				int nTerminusOffset = tagOffset;
				int cTerminusOffset = tagOffset + tagLength;
				float nTerminusMass, cTerminusMass;
				bool nTerminusMatched = false;
				bool cTerminusMatched = false;
				bool nTerminusTryptic = false;
				bool cTerminusTryptic = false;

				vector< pair< string, dataOffset_t > > sequenceCandidates;

				// Try every pre-substring as a potential N-terminus
				if( spectrum->tagList[t].nTerminusMass < ( g_smallestResidue - g_rtConfig.nTerminusMassTolerance ) )
				{
					//cout << "Not matching the N-terminus." << endl;
					nTerminusMass = 0;
					nTerminusMatched = true;

					if( tagOffset == 0 )
						nTerminusTryptic = true;
				} else
				{
					minTerminusLength = (int) floor( spectrum->tagList[t].nTerminusMass / g_largestResidue );
					maxTerminusLength = (int) ceil( spectrum->tagList[t].nTerminusMass / g_smallestResidue );
					for( int n = minTerminusLength; n > 0 && n < maxTerminusLength; ++n )
					{
						int curTerminusOffset = tagOffset - n;
						int cleavageOffset = curTerminusOffset - 1;
						if( curTerminusOffset < 0 )
							continue;

						// In Tryptic-only mode: if the current N-terminus substring starts after a K or a R, it is a valid terminus
						// In Semi-tryptic mode: if the current N-terminus substring mass matches the tag's N-terminus mass, it is a valid terminus
						// In all modes: if the current N-terminus substring starts at the beginning of the protein, it is a valid terminus
						if( cleavageOffset < 0 ||
							!g_rtConfig.trypticCandidatesOnly ||
							( g_rtConfig.trypticCandidatesOnly && ( p->at( cleavageOffset ) == 'K' || p->at( cleavageOffset ) == 'R' ) ) )
						{
							if( cleavageOffset >= 0 )
							{
								if( p->at( cleavageOffset ) == 'K' || p->at( cleavageOffset ) == 'R' )
									nTerminusTryptic = true;
								else if( !g_residueMap->hasResidue( p->at( cleavageOffset ) ) )
									break; // residue was not in the map
							}

							string curTerminus = p->substr( curTerminusOffset, n );
							//bool test = curTerminus == "I";
							float curTerminusMass = g_residueMap->GetMassOfResidues( curTerminus );

							//cout << "N-terminus: " << curTerminus << " (" << curTerminusMass << " Da)" << endl;

							// Determine if the current terminus mass matches the tag's desired terminus mass
							if( fabs( curTerminusMass - spectrum->tagList[t].nTerminusMass ) < g_rtConfig.nTerminusMassTolerance )
							{
								nTerminusOffset = curTerminusOffset;
								nTerminusMass = curTerminusMass;
								nTerminusMatched = true;
								break;
							}

							if( curTerminusMass > spectrum->tagList[t].nTerminusMass )
								break;
						}
					}
				}

				if( g_rtConfig.partialSequencesAllowed && nTerminusMatched )
					if( !g_rtConfig.trypticCandidatesOnly || ( g_rtConfig.trypticCandidatesOnly && nTerminusTryptic ) )
					{
						pair< string, dataOffset_t > aCandidate( p->substr( nTerminusOffset, tagOffset + tagLength - nTerminusOffset ) + string( "-" ), nTerminusOffset );
						sequenceCandidates.push_back( aCandidate );
					}

				// Try every post-substring as a potential C-terminus
				// If searching for partial sequences AND the N terminus was NOT matched, search for a C terminus match
				// If not searching for partial sequences AND the N terminus WAS matched, search for a C terminus match
				if( spectrum->tagList[t].cTerminusMass < ( g_smallestResidue - g_rtConfig.cTerminusMassTolerance ) )
				{
					//cout << "Not matching the C-terminus." << spectrum->tagList[i].cTerminusMass << endl;
					cTerminusMass = 0;
					cTerminusMatched = true;

					if( tagOffset + tagLength >= (int) p->length() )
						cTerminusTryptic = true;
				} else if( g_rtConfig.partialSequencesAllowed || nTerminusMatched )
				{
					minTerminusLength = (int) floor( spectrum->tagList[t].cTerminusMass / g_largestResidue );
					maxTerminusLength = (int) ceil( spectrum->tagList[t].cTerminusMass / g_smallestResidue );
					for( int c = minTerminusLength; c > 0 && c < maxTerminusLength; ++c )
					{
						int curTerminusOffset = tagOffset + tagLength + c;
						int cleavageOffset = curTerminusOffset - 1;
						if( curTerminusOffset >= (int) p->length() )
							break;

						// In Tryptic-only mode: if the current C-terminus substring ends with a K or a R, it is a valid terminus
						// In Semi-tryptic mode: if the current C-terminus substring mass matches the tag's C-terminus mass, it is a valid terminus
						// In all modes: if the current C-terminus substring is at the end of the protein, it is a valid terminus
						if( curTerminusOffset == (int) p->length()-1 ||
							( !g_rtConfig.trypticCandidatesOnly && nTerminusTryptic ) ||
							( g_rtConfig.trypticCandidatesOnly && ( p->at( cleavageOffset ) == 'K' || p->at( cleavageOffset ) == 'R' ) ) )
						{
							if( p->at( cleavageOffset ) == 'K' || p->at( cleavageOffset ) == 'R' )
								cTerminusTryptic = true;
							else if( !g_residueMap->hasResidue( p->at( cleavageOffset ) ) )
									break; // residue was not in the map

							string curTerminus = p->substr( cTerminusOffset, c );
							//bool test = curTerminus == "IEK";
							float curTerminusMass = g_residueMap->GetMassOfResidues( curTerminus );

							//cout << "C-terminus: " << curTerminus << " (" << curTerminusMass << " Da)" << endl;

							// Determine if the current terminus mass matches the tag's desired terminus mass
							if( fabs( curTerminusMass - spectrum->tagList[t].cTerminusMass ) < g_rtConfig.cTerminusMassTolerance )
							{
								cTerminusOffset = curTerminusOffset;
								cTerminusMass = curTerminusMass;
								cTerminusMatched = true;
								break;
							}

							if( curTerminusMass > spectrum->tagList[t].cTerminusMass )
								break;
						}
					}
				}

				if( g_rtConfig.partialSequencesAllowed && cTerminusMatched )
					if( !g_rtConfig.trypticCandidatesOnly || ( g_rtConfig.trypticCandidatesOnly && cTerminusTryptic ) )
					{
						pair< string, dataOffset_t > aCandidate( string( "-" ) + p->substr( tagOffset, cTerminusOffset - tagOffset ), tagOffset );
						sequenceCandidates.push_back( aCandidate );
					}

				if( nTerminusMatched && cTerminusMatched )
					if( !g_rtConfig.trypticCandidatesOnly ||
						( g_rtConfig.trypticCandidatesOnly && nTerminusTryptic && cTerminusTryptic ) )
					{
						pair< string, dataOffset_t > aCandidate( p->substr( nTerminusOffset, cTerminusOffset - nTerminusOffset ), nTerminusOffset );
						sequenceCandidates.push_back( aCandidate );
					}

				// If partial sequence matching is enabled:
				for( int c=0; c < (int) sequenceCandidates.size(); ++c )
				{
					//if( spectrum->id.index == 897 )
					//	cout << proteins[ v->at(j).proteinIndex ].name << ": " << sequenceCandidates[c] << endl;

					++ spectrum->numSequenceComparisons;

					//if( (spectrum->id.index == 1977 || spectrum->id.index == 1909) && (v->at(j).proteinIndex == 1011 || v->at(j).proteinIndex == 5340) )
					//	cout << nTerminusMatched << " " << nTerminusTryptic << " " << cTerminusMatched << " " << cTerminusTryptic << endl;

					//if( nTerminusMatched && (v->at(j).proteinIndex == 7267 || v->at(j).proteinIndex == 70) )
					//	cout << "Scoring sequence: " << aSequence << endl << endl;

					//cout << spectrum->id.index << ": " << spectrum->tagList[i].tag << " occurs in sequence " << aSequence << endl;

					string aSequence = sequenceCandidates[c].first;
					////////////////////////////////////////
					///// SCORE THE CANDIDATE SEQUENCE /////
					////////////////////////////////////////

					//float mass = g_residueMap->GetMassOfResidues( aSequence, g_residueMap, true ) + 19.0f;
					vector< float > seqIons;

					//CalculateSequenceIons( aSequence, &seqIons, &seqIons, g_residueMap, false, spectrum->mOfPrecursor );

					scoreInstance_t scoreResult = spectrum->ScoreSequenceVsSpectrum(aSequence,
																					seqIons,
																					g_rtConfig.numIntensityClasses,
																					g_rtConfig.fragmentMzTolerance );

					float deltCN = 1.0f;

					if( scoreResult.score > 0 )
						deltCN = 0.0f;

					if( scoreResult.score > 0 && !scoreInfo->empty() )
						deltCN = ( scoreInfo->rbegin()->score - scoreResult.score ) / scoreInfo->rbegin()->score;

					if( g_singleScanMode )
					{
						cout << "Round 1 candidate: " << spectrum->tagList[t].tag << " -> " << aSequence << " " <<
								scoreResult.score << " " << deltCN << " " << scoreResult.key << endl;
					}

					if( deltCN < 0.5f )
					{
						if( scoreResult.score > bestScore )
						{
							bestScore = scoreResult.score;
							bestSequence = aSequence;

							if( !scoreInfo->empty() )
							{
								SearchResultSet::iterator trimItr = scoreInfo->begin();
								while( ( scoreInfo->rbegin()->score - trimItr->score ) / scoreInfo->rbegin()->score >= 0.5f )
									++trimItr;
								scoreInfo->erase( scoreInfo->begin(), trimItr );
							}
						}

						//simplethread_lock_mutex( &resourceMutex );
						SearchResultSet::iterator itr = scoreInfo->insert( scoreResult ).first;
						proteinLoci_t* loci = const_cast< proteinLoci_t* >( &itr->loci );
						loci->insert( proteinLocus( v->at(j).proteinIndex, sequenceCandidates[c].second ) );

						//if( (int) scoreInfo->size() > 100 )
						//	scoreInfo->erase( scoreInfo->begin() );
						//simplethread_unlock_mutex( &resourceMutex );
					}
				}
			}
		}

		//cout << g_hostString << " " << spectrum->id.index << ": " << (int) sequenceProteinLoci.size() << " " << (int) sequenceToSpectraMap.size() << endl;
	#if 1
		if( g_singleScanMode && !scoreInfo->empty() )
		{
			simplethread_lock_mutex( &resourceMutex );
			cout << "Round 1 results (" << (int) scoreInfo->size() << "):" << endl;
			for( SearchResultSet::reverse_iterator rItr = scoreInfo->rbegin(); rItr != scoreInfo->rend(); ++rItr )
			{
				cout.width(40);
				cout.fill('_');
				cout << left << rItr->sequence + " ";
				cout << " ";
				cout.width(5);
				cout.fill('0');
				cout << left << showpoint << round( rItr->score, 2 ) << " " <<
						( scoreInfo->rbegin()->score - rItr->score ) / scoreInfo->rbegin()->score << noshowpoint;
				cout << "\t" << rItr->key << endl;
			}
			cout << endl;
			simplethread_unlock_mutex( &resourceMutex );
		}
	#endif
	}

	void ReconcilePartialResults( Spectrum* spectrum, SearchResult* scoreInfo, int i )
	{
		SearchResultSet::reverse_iterator resultItr;
		int minTerminusLength, maxTerminusLength;
		int nTerminusOffset, cTerminusOffset;
		float sequenceMass, massDifference;
		string* p;

		for( resultItr = scoreInfo->rbegin(); resultItr != scoreInfo->rend(); ++resultItr )
		{
			proteinLoci_t::iterator lociItr;

			if( *resultItr->sequence.begin() == '-' ) // N terminus is unmatched
			{
				string partialSequence = resultItr->sequence.substr( 1 );
				sequenceMass = g_residueMap->GetMassOfResidues( partialSequence );
				massDifference = spectrum->mOfPrecursor - sequenceMass;
				minTerminusLength = 1;//(int) floor( massDifference / g_largestResidue );
				

				for( lociItr = resultItr->loci.begin(); lociItr != resultItr->loci.end(); ++lociItr )
				{
					p = &proteins[ lociItr->index ].data;
					maxTerminusLength = lociItr->offset;//(int) ceil( massDifference / g_smallestResidue );
					for(	nTerminusOffset = lociItr->offset - minTerminusLength;
							nTerminusOffset >= 0 && nTerminusOffset >= lociItr->offset - maxTerminusLength;
							-- nTerminusOffset )
					{
						if( nTerminusOffset > 0 && p->at( nTerminusOffset-1 ) != 'K' && p->at( nTerminusOffset-1 != 'R' ) )
							continue;

						string nTerminus = p->substr( nTerminusOffset, lociItr->offset - nTerminusOffset );
						string unmodifiedSequence = nTerminus + partialSequence;
						float unmodifiedMass = g_residueMap->GetMassOfResidues( unmodifiedSequence, g_rtConfig.useAvgMassOfSequences ) + 19.0f;
						float modificationMass = spectrum->mOfPrecursor - unmodifiedMass;

						if( fabs( modificationMass ) > 600.0f )
							break;

						vector< float > candidateModifications( 1, modificationMass );

						for(	modMap_t::iterator mItr = knownModifications.lower_bound( modificationMass - g_rtConfig.precursorMzTolerance );
								mItr != knownModifications.upper_bound( modificationMass + g_rtConfig.precursorMzTolerance );
								++ mItr )
						{
							candidateModifications.push_back( mItr->first );
						}

						for( int m=0; m < (int) candidateModifications.size(); ++m )
						{
							for( int n=0; n < (int) nTerminus.length(); ++n )
							{
								ResidueMap residueMap = *g_residueMap;
								residueMap.addResidueMod( nTerminus[n], '*', (int) round( candidateModifications[m], 0 ) );
								char r = nTerminus[n];
								nTerminus[n] = '*';
								ptmMap_t ptmMap( 1, ptmInfo_t( r, '*', 0 ) );

								string aSequence = nTerminus + partialSequence;
								float candidateMass = residueMap.GetMassOfResidues( aSequence, g_rtConfig.useAvgMassOfSequences ) + 19.0f;

								if( fabs( spectrum->mOfPrecursor - candidateMass ) > g_rtConfig.precursorMzTolerance )
									break;

								++ spectrum->numSequenceComparisons;

								vector< float > seqIons;
								//CalculateSequenceIons( aSequence, &seqIons, &seqIons, residueMap );
								scoreInstance_t modResult = spectrum->ScoreSequenceVsSpectrum( aSequence, seqIons, g_rtConfig.numIntensityClasses, g_rtConfig.fragmentMzTolerance );

								modResult.sequence = ConvertFreiPtmToSqtPtm( modResult.sequence, ptmMap );
								modResult.mass = candidateMass;
								modResult.mod = candidateModifications[m];
								SearchResultSet::iterator itr = spectrum->resultSet.insert( modResult ).first;
								proteinLocus_t completeLocus( lociItr->index, nTerminusOffset );
								proteinLoci_t* loci = const_cast< proteinLoci_t* >( &itr->loci );
								loci->insert( completeLocus );

								if( g_singleScanMode )
								{
									cout << "Round 2 N-partial candidate: -" << partialSequence << " -> " << modResult.sequence << " " << candidateModifications[m] << " " <<
											modResult.score << " " << modResult.key << endl;
								}

								if( (int) spectrum->resultSet.size() > g_rtConfig.maxResults )
									spectrum->resultSet.erase( spectrum->resultSet.begin() );

								nTerminus[n] = r;
							}
						}
					}
				}

			} else if( *resultItr->sequence.rbegin() == '-' ) // C terminus is unmatched
			{
				string partialSequence = resultItr->sequence.substr( 0, resultItr->sequence.length()-1 );
				sequenceMass = g_residueMap->GetMassOfResidues( partialSequence );
				massDifference = spectrum->mOfPrecursor - sequenceMass;
				minTerminusLength = 1;//(int) floor( massDifference / g_largestResidue );

				for( lociItr = resultItr->loci.begin(); lociItr != resultItr->loci.end(); ++lociItr )
				{
					p = &proteins[ lociItr->index ].data;
					int endOfSequence = lociItr->offset + (int) resultItr->sequence.length()-1;
					maxTerminusLength = (int) p->length() - endOfSequence;//(int) ceil( massDifference / g_smallestResidue );
					for(	cTerminusOffset = endOfSequence + minTerminusLength;
							cTerminusOffset < (int) p->length() && cTerminusOffset <= endOfSequence + maxTerminusLength;
							++ cTerminusOffset )
					{
						if( cTerminusOffset < (int) p->length() && p->at( cTerminusOffset-1 ) != 'K' && p->at( cTerminusOffset-1 ) != 'R' )
							continue;

						string cTerminus = p->substr( endOfSequence, cTerminusOffset - endOfSequence );
						string unmodifiedSequence = partialSequence + cTerminus;
						float unmodifiedMass = g_residueMap->GetMassOfResidues( unmodifiedSequence, g_rtConfig.useAvgMassOfSequences ) + 19.0f;
						float modificationMass = spectrum->mOfPrecursor - unmodifiedMass;

						if( fabs( modificationMass ) > 600.0f )
							break;

						vector< float > candidateModifications( 1, modificationMass );

						for(	modMap_t::iterator mItr = knownModifications.lower_bound( modificationMass - g_rtConfig.precursorMzTolerance );
								mItr != knownModifications.upper_bound( modificationMass + g_rtConfig.precursorMzTolerance );
								++ mItr )
						{
							candidateModifications.push_back( mItr->first );
						}

						for( int m=0; m < (int) candidateModifications.size(); ++m )
						{
							for( int c=0; c < (int) cTerminus.length(); ++c )
							{
								ResidueMap residueMap = *g_residueMap;
								residueMap.addResidueMod( cTerminus[c], '*', (int) round( candidateModifications[m], 0 ) );
								char r = cTerminus[c];
								cTerminus[c] = '*';
								ptmMap_t ptmMap( 1, ptmInfo_t( r, '*', 0 ) );

								string aSequence = partialSequence + cTerminus;
								float candidateMass = g_residueMap->GetMassOfResidues( aSequence, g_rtConfig.useAvgMassOfSequences ) + 19.0f;

								if( fabs( spectrum->mOfPrecursor - candidateMass ) > g_rtConfig.precursorMzTolerance )
									break;

								++ spectrum->numSequenceComparisons;

								vector< float > seqIons;
								//CalculateSequenceIons( aSequence, &seqIons, &seqIons, residueMap );
								scoreInstance_t modResult = spectrum->ScoreSequenceVsSpectrum( aSequence, seqIons, g_rtConfig.numIntensityClasses, g_rtConfig.fragmentMzTolerance );

								modResult.sequence = ConvertFreiPtmToSqtPtm( modResult.sequence, ptmMap );
								modResult.mass = candidateMass;
								modResult.mod = candidateModifications[m];
								SearchResultSet::iterator itr = spectrum->resultSet.insert( modResult ).first;
								proteinLoci_t* loci = const_cast< proteinLoci_t* >( &itr->loci );
								loci->insert( *lociItr );

								if( g_singleScanMode )
								{
									cout << "Round 2 C-partial candidate: " << partialSequence << "- -> " << modResult.sequence << " " << candidateModifications[m] << " " <<
											modResult.score << " " << modResult.key << endl;
								}

								if( (int) spectrum->resultSet.size() > g_rtConfig.maxResults )
									spectrum->resultSet.erase( spectrum->resultSet.begin() );

								//if( modResult.score > resultItr->score )
								//	cout << spectrum->id.index << " " << proteins[ lociItr->index ].name << " @ " <<
								//			lociItr->offset << ": " << resultItr->sequence << " -> " << modResult.sequence <<
								//			" = " << modResult.score - resultItr->score << " " << endl;

								cTerminus[c] = r;
							}
						}
					}
				}
			} else
			{
				scoreInstance_t completeResult = *resultItr;
				completeResult.mass = g_residueMap->GetMassOfResidues( resultItr->sequence, g_rtConfig.useAvgMassOfSequences ) + 19.0f;
				completeResult.mod = 0.0f;
				SearchResultSet::iterator itr = spectrum->resultSet.insert( completeResult ).first;
				proteinLoci_t* loci = const_cast< proteinLoci_t* >( &itr->loci );
				*loci = resultItr->loci;

				if( (int) spectrum->resultSet.size() > g_rtConfig.maxResults )
					spectrum->resultSet.erase( spectrum->resultSet.begin() );
			}

			//for( lociItr = resultItr->loci.begin(); lociItr != resultItr->loci.end(); ++lociItr )
				//cout << proteins[ lociItr->index ].data.substr( lociItr->offset, resultItr->sequence.length() ) << endl;
		}

		if( g_singleScanMode )
		{
			simplethread_lock_mutex( &resourceMutex );
			cout << "Round 2 results (" << (int) spectrum->resultSet.size() << "):" << endl;
			for( SearchResultSet::reverse_iterator rItr = spectrum->resultSet.rbegin(); rItr != spectrum->resultSet.rend(); ++rItr )
			{
				cout.width(40);
				cout.fill('_');
				cout << left << rItr->sequence + " ";
				cout << " ";
				cout.width(5);
				cout.fill('0');
				cout << left << showpoint << round( rItr->score, 2 ) << noshowpoint;
				cout << "\t" << rItr->key << "\t" << rItr->mod << endl;
			}
			cout << endl;
			simplethread_unlock_mutex( &resourceMutex );
		}
	}*/

	/**!
		WriteOutputToFile writes the results of TagRecon to an XML file. The XML file is formatted as
		PepXML. The function first computes the e-values of the peptide hits, computes the ranks of
		peptide hits for each spectrum, and converts the protein indices of each petpide hit to its
		corresponding protein annotation. The function also write out an SVG file for each top hit of 
		the spectrum. The function also write out auxillary files that are used to asses the performance
		of the deisotoping and precursor m/z adjustment. 
	*/
	void WriteOutputToFile(	const string& dataFilename,
							string startTime,
							string startDate,
							float totalSearchTime,
							vector< size_t > opcs,
							vector< size_t > fpcs,
							searchStats& overallStats )
	{
		int numSpectra = 0;
		int numMatches = 0;
		int numLoci = 0;

		string filenameAsScanName = basename( MAKE_PATH_FOR_BOOST(dataFilename) );

		// Make histograms of scores by charge state
		map< int, Histogram<float> > meanScoreHistogramsByChargeState;
		for( int z=1; z <= g_rtConfig->NumChargeStates; ++z )
			meanScoreHistogramsByChargeState[ z ] = Histogram<float>( g_rtConfig->NumScoreHistogramBins, g_rtConfig->MaxScoreHistogramValues );

		// Compute the e-value if required by the user 
		if( g_rtConfig->CalculateRelativeScores )
		{
			Timer calculationTime(true);
			cout << g_hostString << " is calculating relative scores for " << spectra.size() << " spectra." << endl;
			float lastUpdateTime = 0;
			size_t n = 0;
			// For each spectrum
			for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr, ++n )
			{
				Spectrum* s = (*sItr);

				try
				{
					// Compute the e-value
					s->CalculateRelativeScores();
				} catch( exception& e )
				{
					//throw runtime_error( "calculating relative scores for scan " + string( s->id ) + ": " + e.what() );
					cerr << "Error: calculating relative scores for scan " << string( s->id ) << ": " << e.what() << endl;
					continue;
				} catch( ... )
				{
					cerr << "Error: calculating relative scores for scan " << string( s->id ) << endl;
					continue;
				}

				if( calculationTime.TimeElapsed() - lastUpdateTime > g_rtConfig->StatusUpdateFrequency )
				{
					cout << g_hostString << " has calculated relative scores for " << n << " of " << spectra.size() << " spectra." << endl;
					lastUpdateTime = calculationTime.TimeElapsed();
				}
				PRINT_PROFILERS(cout, s->id.id + " done");
			}
			cout << g_hostString << " finished calculating relative scores; " << calculationTime.End() << " seconds elapsed." << endl;
		}

		// For each spectrum
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			++ numSpectra;
			Spectrum* s = (*sItr);

			// Set the spectrum id as the scan number
			spectra.setId( s->id, SpectrumId( filenameAsScanName, s->id.index, s->id.charge ) );

			// Compute the relative ranks of the results in the result set
			s->resultSet.calculateRanks();
			// Convert all the protein indices in the result set to their corresponding names
			s->resultSet.convertProteinIndexesToNames( proteins.indexToName );

			// Compute score histograms
			if( g_rtConfig->MakeScoreHistograms )
			{
				s->scoreHistogram.smooth();
				for( map<float,int>::iterator itr = s->scores.begin(); itr != s->scores.end(); ++itr )
					cout << itr->first << "\t" << itr->second << "\n";
				//cout << std::keys( s->scores ) << endl;
				//cout << std::values( s->scores ) << endl;
				//cout << std::keys( s->scoreHistogram.m_bins ) << endl;
				//cout << std::values( s->scoreHistogram.m_bins ) << endl;
				//s->scoreHistogram.writeToSvgFile( string( s->id ) + "-histogram.svg", "MVH score", "Density", 800, 600 );
				meanScoreHistogramsByChargeState[ s->id.charge ] += s->scoreHistogram;
			}

			// Iterate through the result set from backwards (i.e highest scoring peptide)
			for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
			{
				++ numMatches;
				numLoci += itr->lociByName.size();

				string theSequence = itr->sequence();

				// Make a spectrum graph for the top scoring interpretation. The spectrum
				// graph is created as an SVG formatted file.
				if( itr->rank == 1 && g_rtConfig->MakeSpectrumGraphs )
				{
					vector< double > ionMasses;
					vector< string > ionNames;
					// Compute the predicted ions for the interpretation.
					//const bool allIonTypes[4] = { true, true, false, false };
					CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, &ionNames, 0 );
					map< double, string > ionLabels;
					map< double, string > ionColors;
					map< double, int > ionWidths;

					// Set the width of the ion strokes
					for( PeakPreData::iterator itr = s->peakPreData.begin(); itr != s->peakPreData.end(); ++itr )
						ionWidths[ itr->first ] = 1;
                    cout << ionMasses << endl << ionNames << endl;
					// For each ion in the predicted spectrum find a peak in the experimental spectrum
					// that is close to it.
					for( size_t i=0; i < ionMasses.size(); ++i )
					{
						PeakPreData::iterator itr = s->peakPreData.findNear( ionMasses[i], g_rtConfig->FragmentMzTolerance );
						// Assign the color depending on which ion type we matched.
						if( itr != s->peakPreData.end() )
						{
							ionLabels[ itr->first ] = ionNames[i];
							ionColors[ itr->first ] = ( ionNames[i].find( "b" ) == 0 ? "red" : "blue" );
							ionWidths[ itr->first ] = 2;
						}
					}

					cout << theSequence << " fragment ions: " << ionLabels << endl;

					// Write the spectrum to a SVG formatted file
					s->writeToSvgFile( string( "-" ) + theSequence + g_rtConfig->OutputSuffix, &ionLabels, &ionColors, &ionWidths );
				}
			}
		}

		// Write the score histograms to the SVG file
		if( g_rtConfig->MakeScoreHistograms )
			for( int z=1; z <= g_rtConfig->NumChargeStates; ++z )
				meanScoreHistogramsByChargeState[ z ].writeToSvgFile( filenameAsScanName + g_rtConfig->OutputSuffix + "_+" + lexical_cast<string>(z) + "_histogram.svg", "MVH score", "Density", g_rtConfig->ScoreHistogramWidth, g_rtConfig->ScoreHistogramHeight );

		// Get some stats and program parameters
		RunTimeVariableMap vars = g_rtConfig->getVariables();
		RunTimeVariableMap fileParams;
		for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
			fileParams[ string("Config: ") + itr->first ] = itr->second;
		fileParams["SearchEngine: Name"] = "TagRecon";
		fileParams["SearchEngine: Version"] = TAGRECON_VERSION_STRING;
		fileParams["SearchTime: Started"] = startTime + " on " + startDate;
		fileParams["SearchTime: Stopped"] = GetTimeString() + " on " + GetDateString();
		fileParams["SearchTime: Duration"] = lexical_cast<string>( totalSearchTime ) + " seconds";
		fileParams["SearchStats: Nodes"] = lexical_cast<string>( g_numProcesses );
		fileParams["SearchStats: Overall"] = (string) overallStats;
		fileParams["PeakCounts: Mean: Original"] = lexical_cast<string>( opcs[5] );
		fileParams["PeakCounts: Mean: Filtered"] = lexical_cast<string>( fpcs[5] );
		fileParams["PeakCounts: Min/Max: Original"] = lexical_cast<string>( opcs[0] ) + " / " + lexical_cast<string>( opcs[1] );
		fileParams["PeakCounts: Min/Max: Filtered"] = lexical_cast<string>( fpcs[0] ) + " / " + lexical_cast<string>( fpcs[1] );
		fileParams["PeakCounts: 1stQuartile: Original"] = lexical_cast<string>( opcs[2] );
		fileParams["PeakCounts: 1stQuartile: Filtered"] = lexical_cast<string>( fpcs[2] );
		fileParams["PeakCounts: 2ndQuartile: Original"] = lexical_cast<string>( opcs[3] );
		fileParams["PeakCounts: 2ndQuartile: Filtered"] = lexical_cast<string>( fpcs[3] );
		fileParams["PeakCounts: 3rdQuartile: Original"] = lexical_cast<string>( opcs[4] );
		fileParams["PeakCounts: 3rdQuartile: Filtered"] = lexical_cast<string>( fpcs[4] );

		// Output pepXML format
		string outputFilename = filenameAsScanName + g_rtConfig->OutputSuffix + ".pepXML";
		cout << g_hostString << " is writing search results to file \"" << outputFilename << "\"." << endl;
		spectra.writePepXml( dataFilename, g_rtConfig->OutputSuffix, "TagRecon", g_dbPath + g_dbFilename, &proteins, fileParams );

		// Auxillary file to test the performance of deisotoping
		if( g_rtConfig->DeisotopingMode == 3 /*&& g_rtConfig->DeisotopingTestMode != 0*/ )
		{
			// Compute the FDR for all charge states and get spectra that passes the
			// score threshold at 0.05 level.
			spectra.calculateFDRs( g_rtConfig->NumChargeStates, 1.0f, "rev_" );
			SpectraList passingSpectra;
			spectra.filterByFDR( 0.05f, &passingSpectra );
			//g_rtConfig->DeisotopingMode = g_rtConfig->DeisotopingTestMode;

			// For each of the filtered spectrum
			ofstream deisotopingDetails( (filenameAsScanName+g_rtConfig->OutputSuffix+"-deisotope-test.tsv").c_str() );
			deisotopingDetails << "Scan\tCharge\tSequence\tPredicted\tMatchesBefore\tMatchesAfter\n";
			for( SpectraList::iterator sItr = passingSpectra.begin(); sItr != passingSpectra.end(); ++sItr )
			{
				// Deisotope the spectrum
				Spectrum* s = (*sItr);
				s->Deisotope( g_rtConfig->IsotopeMzTolerance );

				s->resultSet.calculateRanks();
				// Iterate through the results
				for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
				{
					string theSequence = itr->sequence();

					// Get the top hit
					if( itr->rank == 1 )
					{
						// Compute the number of predicted fragments, matched fragments before deisotoping
						// and matched fragments after deisotoping.
						vector< double > ionMasses;
						//const bool allIonTypes[4] = { true, true, true, true };
						//CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0 );
                        CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0 );
						int fragmentsPredicted = accumulate( itr->key.begin(), itr->key.end(), 0 );
						int fragmentsFound = fragmentsPredicted - itr->key.back();
						int fragmentsFoundAfterDeisotoping = 0;
						for( size_t i=0; i < ionMasses.size(); ++i )
							if( s->peakPreData.findNear( ionMasses[i], g_rtConfig->FragmentMzTolerance ) != s->peakPreData.end() )
								++ fragmentsFoundAfterDeisotoping;
						deisotopingDetails << s->id.index << "\t" << s->id.charge << "\t" << theSequence << "\t" << fragmentsPredicted << "\t" << fragmentsFound << "\t" << fragmentsFoundAfterDeisotoping << "\n";
					}
				}
			}
			passingSpectra.clear(false);
		}

		// Auxillary file to test the performance of precursor mass adjustment
		if( g_rtConfig->AdjustPrecursorMass == 1 )
		{
			// Compute the FDR and select spectra that passes the score thresold at 
			// an FDR of 0.05
			spectra.calculateFDRs( g_rtConfig->NumChargeStates, 1.0f, "rev_" );
			SpectraList passingSpectra;
			spectra.filterByFDR( 0.05f, &passingSpectra );

			// For each of the spectrum that passes the FDR test
			ofstream adjustmentDetails( (filenameAsScanName+g_rtConfig->OutputSuffix+"-adjustment-test.tsv").c_str() );
			adjustmentDetails << "Scan\tCharge\tUnadjustedSequenceMass\tAdjustedSequenceMass\tUnadjustedPrecursorMass\tAdjustedPrecursorMass\tUnadjustedError\tAdjustedError\tSequence\n";
			for( SpectraList::iterator sItr = passingSpectra.begin(); sItr != passingSpectra.end(); ++sItr )
			{
				Spectrum* s = (*sItr);

				// Compute the ranks of the results
				s->resultSet.calculateRanks();
				for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
				{
					// Get the top hit
					if( itr->rank == 1 )
					{
						// Print out the mass of the sequence before and after the precursor m/z adjustment
						double setSeqMass = g_rtConfig->UseAvgMassOfSequences ? itr->molecularWeight() : itr->monoisotopicMass();
						double monoSeqMass = itr->monoisotopicMass();
						adjustmentDetails <<	s->id.index << "\t" << s->id.charge << "\t" <<
												setSeqMass << "\t" << monoSeqMass << "\t" << s->mOfUnadjustedPrecursor << "\t" << s->mOfPrecursor << "\t" <<
												fabs( setSeqMass - s->mOfUnadjustedPrecursor ) << "\t" <<
												fabs( monoSeqMass - s->mOfPrecursor ) << "\t" << itr->sequence() << "\n";
					}
				}
			}
			passingSpectra.clear(false);
		}
	}

	/**
			getInterpretation function takes DigestedPeptide and converts it into a string.
			It uses AminoAcid+ModMass notation to represent the location of mods in the sequence. 
			An example output for a peptide with an oxidized methonine would look like PVSPLLLASGM+16AR.
			This string is used for display and sorting purposes.
		*/
		string getInterpretation(const DigestedPeptide& peptide) {

			string returnString;
			// Get the peptide sequence and the mods
			string baseString = peptide.sequence();
			ModificationMap& mods = const_cast <ModificationMap&> (peptide.modifications());
			// For each amino acid
			for(size_t aa = 0; aa < baseString.length(); aa++) {
				// Append the amino acid to the sequence
				returnString += baseString[aa];
				std::ostringstream os; 
				// Get the mods at the location of the amino acid
				for(ModificationList::iterator modIter = mods[aa].begin(); modIter != mods[aa].end(); modIter++) {
					// Add the mass of the mod (rounded) after the amino acid
                    os << ((int) ((*modIter).monoisotopicDeltaMass()+((*modIter).monoisotopicDeltaMass()>0?0.5:-0.5)));
				}
				// Append the mod to the amino acid
				returnString += os.str();
			}

			// Get the modifications on n-terminus and add them 
			// to the list.
			string nTerminus = "(";
			// For each n-terminal mod
			if(mods.begin()!= mods.end() && mods.begin()->first==ModificationMap::NTerminus() ) {
				std::ostringstream os;
				os << (int) (mods.begin()->second.monoisotopicDeltaMass()+0.5);
				nTerminus += os.str();
			}
			
			// Get the modifications on c-termimus
			string cTerminus = ")";
			if( mods.rbegin() != mods.rend() && mods.rbegin()->first == ModificationMap::CTerminus() ) {
				std::ostringstream os;
				os << (int) (mods.rbegin()->second.monoisotopicDeltaMass()+0.5);
				cTerminus += os.str();
			}
            
			// Return the formed interpretation
			return (nTerminus+returnString+cTerminus);
		}

	/**! 
	  InitProcess (argsList) loacates the working directory if present, detects the number of cpus
	  available for the process, loads the default or user given parameters and amino acid residue
	  masses for the search. The default values are overridden by command line arguments. The
	  function also transmits or receives the configs depending upon whether it is a root process
	  or a child process.
	*/
	int InitProcess( vector <std::string> & args ) {

		//cout << g_hostString << " is initializing." << endl;
		if( g_pid == 0 )
		{
			cout << "TagRecon " << TAGRECON_VERSION_STRING << " (" << TAGRECON_BUILD_DATE << ")\n" <<
					"ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
                    "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
					TAGRECON_LICENSE << endl;
		}

		g_rtConfig = new TagreconRunTimeConfig;
		g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
		g_residueMap = new ResidueMap;
		g_numWorkers = GetNumProcessors();

		// First set the working directory, if provided
		for( size_t i=1; i < args.size(); ++i )
		{
			if( args[i] == "-workdir" && i+1 <= args.size() )
			{
				chdir( args[i+1].c_str() );
				args.erase( args.begin() + i );
			} else if( args[i] == "-cpus" && i+1 <= args.size() )
			{
				//Get the number of cpus
				g_numWorkers = atoi( args[i+1].c_str() );
				args.erase( args.begin() + i );
			} else
				continue;

			args.erase( args.begin() + i );
			--i;
		}

		//Read the parameters and residue masses if this process is a master process.
		if( g_pid == 0 )
		{
			for( size_t i=1; i < args.size(); ++i )
			{
				if( args[i] == "-cfg" && i+1 <= args.size() )
				{
					//Initialize the parameters from .cfg file.
					if( g_rtConfig->initializeFromFile( args[i+1] ) )
					{
						cerr << g_hostString << " could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
						return 1;
					}
					args.erase( args.begin() + i );

				} else if( args[i] == "-rescfg" && i+1 <= args.size() )
				{
					//Initialize the residue masses from "residue_masses.cfg" file.
					if( g_residueMap->initializeFromFile( args[i+1] ) )
					{
						cerr << g_hostString << " could not find residue masses at \"" << args[i+1] << "\"." << endl;
						return 1;
					}
					args.erase( args.begin() + i );
				} else
					continue;

				args.erase( args.begin() + i );
				--i;
			}

			//Check to make sure the user has given a DB and a set of spectra.
			if( args.size() < 4 )
			{
				cerr << "Not enough arguments.\nUsage: " << args[0] << " [-ProteinDatabase <FASTA protein database filepath>] [-UnimodXML <Unimod XML filepath>] [-Blosum <Blosum Matrix filepath>] <input tags filemask 1> [input tags filemask 2] ..." << endl;
				return 1;
			}
		
			//Check to see if the search parameters have been initialized
			if( !g_rtConfig->initialized() )
			{
				if( g_rtConfig->initializeFromFile() )
				{
					cerr << g_hostString << " could not find the default configuration file (hard-coded defaults in use)." << endl;
				}
			}

			//Check to see if the residue masses have been initialized
			if( !g_residueMap->initialized() )
			{
				if( g_residueMap->initializeFromFile() )
				{
					cerr << g_hostString << " could not find the default residue masses file (hard-coded defaults in use)." << endl;
				}
			}

			//If running on a cluster as a parent process then transmit the 
			//search parameters and residue masses to the child processes.
			#ifdef USE_MPI
				if( g_numChildren > 0 )
					TransmitConfigsToChildProcesses();
			#endif

		} else // child process
		{
			#ifdef USE_MPI
				ReceiveConfigsFromRootProcess();
			#endif
		}

		// Command line overrides happen after config file has been distributed but before PTM parsing
		RunTimeVariableMap vars = g_rtConfig->getVariables();
		// Run through each of the variable and check if a new value has been specified for it 
		// using a command line option
		for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
		{
			string varName;
			varName += "-" + itr->first;

			for( size_t i=1; i < args.size(); ++i )
			{
				if( args[i].find( varName ) == 0 && i+1 <= args.size() )
				{
					//cout << varName << " " << itr->second << " " << args[i+1] << endl;
					itr->second = args[i+1];
					args.erase( args.begin() + i );
					args.erase( args.begin() + i );
					--i;
				}
			}
		}
		// Set the variables
		g_rtConfig->setVariables( vars );

		#ifdef DEBUG
			for( size_t i = 0; i < args.size(); i++) {
				cout << "args[" << i << "]:" << args[i] << "\n";
			}	
		#endif	
		
		// Dump the paramters if the user opts for it
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

			// Skip unintelligible arguments on the command line
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

	

		// Parse out the unimod xml document for modifications
		unimodXMLParser = new UniModXMLParser(g_rtConfig->UnimodXML);
		unimodXMLParser->parseDocument();
		deltaMasses = new DeltaMasses(unimodXMLParser->getModifications());
		deltaMasses->buildDeltaMassLookupTables();
		//deltaMasses->printMassToAminoAcidMap();
		//deltaMasses->printInterpretationMap();
		//exit(1);
		return 0;
	}

	
	/**! 
		InitWorkerGlobals() sorts the spectra by their ID, create a map of tags and spectra
		containing the tags, pre-computes the ln(x!) table where x is number of m/z bins.
		The precomputed ln factorial table is used in MVH scoring.
	*/
	int InitWorkerGlobals()
	{
		// Sort the spectra by their ID
		spectra.sort( spectraSortByID() );

		if( spectra.empty() )
			return 0;

		// Get the number of spectra
		//size_t numSpectra = spectra.size();
		spectraTagMapsByChargeState = SpectraTagMap(TagSetCompare(g_rtConfig->MaxTagMassDeviation));
		/* Create a map of precursor masses to the spectrum indices and map of tags and spectra with the
		   tag. while doing that we also create a map of precursor masses and spectra with the precursor 
		   mass. Precursor mass map is used to rapidly find candidates peptide sequences that are with in
		   a wide mass tolerance (+/- 300 Da). After	the initial filtering step, tag map is used to 
		   further filter out spectra that doesn't have any matching tags with the candidate peptide 
		   sequences. 
		*/
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr ) {
			// Get the tags for the spectrum and put them in the tag map.
			for( TagList::iterator tItr = (*sItr)->tagList.begin(); tItr != (*sItr)->tagList.end(); ++tItr ) {
                TagSetInfo  tagInfo(sItr, tItr->tag, tItr->nTerminusMass, tItr->cTerminusMass);
                tagInfo.tagChargeState = tItr->chargeState;
				//spectraTagMapsByChargeState.insert( SpectraTagMap::value_type(TagSetInfo( sItr, tItr->tag, tItr->nTerminusMass, tItr->cTerminusMass ) ) );
                spectraTagMapsByChargeState.insert( SpectraTagMap::value_type( tagInfo ) );
			}
		}
		
        /*if(false) {
			for(SpectraTagMap::const_iterator itr = spectraTagMapsByChargeState.begin(); itr != spectraTagMapsByChargeState.end(); ++itr) {
				cout << (*itr).candidateTag << "," << (*itr).nTerminusMass << "," << (*itr).cTerminusMass << (*(*itr).sItr)->id.source << " " << (*(*itr).sItr)->id.index << endl;
			}
		}*/

		// Get minimum and maximum peptide masses observed in the dataset
		// and determine the number of peak bins required. This 
		g_rtConfig->curMinSequenceMass = spectra.front()->mOfPrecursor;
		g_rtConfig->curMaxSequenceMass = 0;

		size_t maxPeakBins = (size_t) spectra.front()->totalPeakSpace;
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			if( (*sItr)->mOfPrecursor < g_rtConfig->curMinSequenceMass )
				g_rtConfig->curMinSequenceMass = (*sItr)->mOfPrecursor;

			if( (*sItr)->mOfPrecursor > g_rtConfig->curMaxSequenceMass )
				g_rtConfig->curMaxSequenceMass = (*sItr)->mOfPrecursor;

			size_t totalPeakBins = (size_t) round( (*sItr)->totalPeakSpace / ( g_rtConfig->FragmentMzTolerance * 2.0f ) );
			if( totalPeakBins > maxPeakBins )
				maxPeakBins = totalPeakBins;
		}

		g_rtConfig->curMinSequenceMass -= g_rtConfig->PrecursorMassTolerance.back();
		g_rtConfig->curMaxSequenceMass += g_rtConfig->PrecursorMassTolerance.back();

        // set the effective minimum and maximum sequence masses based on config and precursors
        g_rtConfig->curMinSequenceMass = max( g_rtConfig->curMinSequenceMass, g_rtConfig->MinSequenceMass );
        g_rtConfig->curMaxSequenceMass = min( g_rtConfig->curMaxSequenceMass, g_rtConfig->MaxSequenceMass );

        double minResidueMass = AminoAcid::Info::record('G').residueFormula.monoisotopicMass();
        double maxResidueMass = AminoAcid::Info::record('W').residueFormula.monoisotopicMass();

        // calculate minimum length of a peptide made entirely of tryptophan over the minimum mass
        int curMinCandidateLength = max( g_rtConfig->MinCandidateLength,
                                         (int) floor( g_rtConfig->curMinSequenceMass /
                                                      maxResidueMass ) );

        // calculate maximum length of a peptide made entirely of glycine under the maximum mass
        int curMaxCandidateLength = min((int) ceil( g_rtConfig->curMaxSequenceMass / minResidueMass ), 
                                           g_rtConfig->MaxSequenceLength);

        // set digestion parameters
        Digestion::Specificity specificity = (Digestion::Specificity) g_rtConfig->NumMinTerminiCleavages;
        g_rtConfig->NumMaxMissedCleavages = g_rtConfig->NumMaxMissedCleavages < 0 ? 10000 : g_rtConfig->NumMaxMissedCleavages;
        g_rtConfig->digestionConfig = Digestion::Config( g_rtConfig->NumMaxMissedCleavages,
                                                         curMinCandidateLength,
                                                         curMaxCandidateLength,
                                                         specificity );

		//cout << g_hostString << " is precaching factorials up to " << (int) maxPeakSpace << "." << endl;
		
		// Calculate the ln(x!) table where x= number of m/z spaces.
		// This table is used in MVH scoring.
		g_lnFactorialTable.resize( maxPeakBins );
		//cout << g_hostString << " finished precaching factorials." << endl;

		if( !g_numChildren )
		{
			cout << "Smallest observed precursor is " << g_rtConfig->curMinSequenceMass << " Da." << endl;
			cout << "Largest observed precursor is " << g_rtConfig->curMaxSequenceMass << " Da." << endl;
            cout << "Min. effective sequence mass is " << g_rtConfig->curMinSequenceMass << endl;
            cout << "Max. effective sequence mass is " << g_rtConfig->curMaxSequenceMass << endl;
            cout << "Min. effective sequence length is " << curMinCandidateLength << endl;
            cout << "Max. effective sequence length is " << curMaxCandidateLength << endl;
		}

        //cout << "tagMapSize:" << spectraTagMapsByChargeState.size() << endl;

		return 0;
	}

	void DestroyWorkerGlobals()
	{
	}

	
	/**!
	PrepareSpectra parses out all the spectra in an input file, deterimes the
	charge states (user configurable), preprocesses the spectra, and trims out
	the spectra with too few peaks.
	*/
	void PrepareSpectra()
	{
		// Get total spectra size
		int numSpectra = (int) spectra.size();

		Timer timer;
		//Running in a single processor mode
		if( g_numChildren == 0 )
			cout << g_hostString << " is parsing " << numSpectra << " spectra." << endl;

		timer.Begin();
		// Parse each spectrum
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			try
			{
				(*sItr)->parse();
			} catch( exception& e )
			{
				stringstream msg;
				msg << "parsing spectrum " << (*sItr)->id << ": " << e.what();
				throw runtime_error( msg.str() );
			} catch( ... )
			{
				stringstream msg;
				msg << "parsing spectrum " << (*sItr)->id;
				throw runtime_error( msg.str() );
			}
		}

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " finished parsing its spectra; " << timer.End() << " seconds elapsed." << endl;
			cout << g_hostString << " is trimming spectra with less than " << 10 << " peaks." << endl;
		}

		// Take out spectra that have less then 10 peaks
		int preTrimCount = spectra.filterByPeakCount( 10 );
		numSpectra = (int) spectra.size();

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " trimmed " << preTrimCount << " spectra for being too sparse." << endl;
			cout << g_hostString << " is determining charge states for " << numSpectra << " spectra." << endl;
		}

		timer.Begin();
		SpectraList duplicates;
		// Try to determine the charge state for each spectrum
		// If you can't determine the charge state (i.e if z
		// state is not +1) then duplicate the spectrum to create
		// multiple charge states.
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			try
			{
				if( !g_rtConfig->UseChargeStateFromMS )
					spectra.setId( (*sItr)->id, SpectrumId( (*sItr)->id.source,(*sItr)->id.index, 0 ) );

				if( (*sItr)->id.charge == 0 )
				{
					SpectrumId preChargeId( (*sItr)->id );
					// Determine the charge state
					(*sItr)->DetermineSpectrumChargeState();
					SpectrumId postChargeId( (*sItr)->id );

					// If the charge state is not +1
					if( postChargeId.charge == 0 )
					{
						// Duplicate the spectrum and create
						// spectrum with multiple charge states
						postChargeId.setCharge(2);

						if( g_rtConfig->DuplicateSpectra )
						{
							for( int z = 3; z <= g_rtConfig->NumChargeStates; ++z )
							{
								Spectrum* s = new Spectrum( *(*sItr) );
								s->id.setCharge(z);
								duplicates.push_back(s);
							}
						}
					}

					spectra.setId( preChargeId, postChargeId );
				}

			} catch( exception& e )
			{
				throw runtime_error( string( "duplicating scan " ) + string( (*sItr)->id ) + ": " + e.what() );
			} catch( ... )
			{
				throw runtime_error( string( "duplicating scan " ) + string( (*sItr)->id ) );
			}
		}

		try
		{
			// Add the created duplicates to the existing spectra list
			spectra.insert( duplicates.begin(), duplicates.end(), spectra.end() );
			duplicates.clear(false);
		} catch( exception& e )
		{
			throw runtime_error( string( "adding duplicated spectra: " ) + e.what() );
		} catch( ... )
		{
			throw runtime_error( "adding duplicated spectra" );
		}

		//int replicateCount = (int) spectra.size() - numSpectra;
		numSpectra = (int) spectra.size();

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " finished determining charge states for its spectra; " << timer.End() << " seconds elapsed." << endl;
			cout << g_hostString << " is preprocessing " << numSpectra << " spectra." << endl;
		}

		timer.Begin();
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			try
			{
				// Preprocess the spectrum (see the function documentation for further details)
				(*sItr)->Preprocess();
			} catch( exception& e )
			{
				stringstream msg;
				msg << "preprocessing spectrum " << (*sItr)->id << ": " << e.what();
				throw runtime_error( msg.str() );
			} catch( ... )
			{
				stringstream msg;
				msg << "preprocessing spectrum " << (*sItr)->id;
				throw runtime_error( msg.str() );
			}
		}

		// Trim spectra that have observed precursor masses outside the user-configured range
		// (erase the peak list and the trim 0 peaks out)
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			if( (*sItr)->mOfPrecursor < g_rtConfig->MinSequenceMass ||
				(*sItr)->mOfPrecursor > g_rtConfig->MaxSequenceMass )
			{
				(*sItr)->peakPreData.clear();
				(*sItr)->peakData.clear();
			}
		}

		if( g_numChildren == 0 )
		{
			// Throw some bones to the user to keep him occupied or disinterested.....
			cout << g_hostString << " finished preprocessing its spectra; " << timer.End() << " seconds elapsed." << endl;
			cout << g_hostString << " is trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
			cout << g_hostString << " is trimming spectra with precursors too small or large: " <<
				g_rtConfig->MinSequenceMass << " - " << g_rtConfig->MaxSequenceMass << endl;
		}

		// Filter the spectra by peak count. If a spectrum doesn't have enough peaks to fill
		// out minimum number of intensity classes (user configurable) then the spectrum is
		// most likely a noisy spectrum. So, clip it off.
		int postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " trimmed " << postTrimCount << " spectra." << endl;
		}
	}


	int numSearched;
	vector< int > workerNumbers;

	/**!
		DigestProteinSequence function takes a protein sequence, digests it into peptide sequences
		based on cleavage rules (user specified). The generated candidate peptide sequences  and 
		a variable modification list are used to generate all possible sequence variants for each
		candidate petpide sequence that needs to be searched.
	*/
	/*void DigestProteinSequence( const proteinData& protein, CandidateSequenceList& candidates )
	{
		// Get the minimum and maximum dynamic modification mass
		double minDynamicModMass = min( g_residueMap->smallestDynamicModMass(), 0.0 ) * g_rtConfig->MaxDynamicMods;
		double maxDynamicModMass = max( g_residueMap->largestDynamicModMass(), 0.0 ) * g_rtConfig->MaxDynamicMods;

		// Get the sequence
		const string& p = protein.getSequence();

		// For each amino acid residue
		for( int nIndex = 0; nIndex < (int) p.length(); ++nIndex )
		{
			int nTerminusCanCleave = 0;
			const char& n_r = p[ nIndex ];
			// If the residue is an unknown then continue
			if( !g_residueMap->hasResidue( n_r ) )
				continue;

			int numTotalCleavages = 0;

			// Get the n-terminal cleavage
			CleavageRuleSet::const_iterator nTermCleavageItr = protein.testCleavage( g_rtConfig->_CleavageRules, nIndex );
			if( nTermCleavageItr != g_rtConfig->_CleavageRules.end() )
			{
				++numTotalCleavages;
				nTerminusCanCleave = 1;
			}

			if( numTotalCleavages < g_rtConfig->NumMinTerminiCleavages-1 )
				continue;

			// Get the c-terminal cleavage site
			for( int cIndex = nIndex; cIndex < (int) p.length(); ++cIndex )
			{
				int cTerminusCanCleave = 0;
				const char& c_r = p[ cIndex ];

				if( !g_residueMap->hasResidue( c_r ) )
					break;

				CleavageRuleSet::const_iterator cTermCleavageItr = protein.testCleavage( g_rtConfig->_CleavageRules, cIndex+1 );
				if( cTermCleavageItr != g_rtConfig->_CleavageRules.end() )
				{
					++numTotalCleavages;
					cTerminusCanCleave = 1;
				}

				if( cIndex - nIndex + 1 < g_rtConfig->MinCandidateLength )
					continue;
				
				// Get number of termini cleavages
				int numTerminiCleavages = nTerminusCanCleave + cTerminusCanCleave;

				if( numTerminiCleavages < g_rtConfig->NumMinTerminiCleavages )
					continue;

				// Get number of missed cleavages
				int numMissedCleavages = numTotalCleavages - numTerminiCleavages;

				if( g_rtConfig->NumMaxMissedCleavages != -1 && numMissedCleavages > g_rtConfig->NumMaxMissedCleavages )
					break;

				// Get the peptide sequence and its mass
				string aSequence = PEPTIDE_N_TERMINUS_SYMBOL + p.substr( nIndex, cIndex - nIndex + 1 ) + PEPTIDE_C_TERMINUS_SYMBOL;
				float aSequenceMass = g_residueMap->GetMassOfResidues( aSequence, g_rtConfig->UseAvgMassOfSequences );

				if( aSequenceMass + maxDynamicModMass < g_rtConfig->curMinSequenceMass )
					continue;

				if( aSequenceMass + minDynamicModMass > g_rtConfig->curMaxSequenceMass )
					break;

				// All tests passed, we have a genuine bonafide sequence candidate!
				CandidateSequenceInfo candidate( aSequence, aSequenceMass, nIndex, numTerminiCleavages, numMissedCleavages );
				//cout << p[max(0,nIndex-1)] << '.' << aSequence << '.' << p[min(p.length()-1,cIndex+1)] << " " << numTerminiCleavages << " " << numMissedCleavages << endl;
				
				// Make all the variants of the candidate sequence using the potential 
				// variable modification list.
				//MakePtmVariants( candidate, candidates, g_rtConfig->MaxDynamicMods );
			}
		}
	}*/

	
	/**!
		GetTagsFromSequence takes a peptide sequence, its mass, and generates tags of specified length
		(user configurable) from the sequence. 
	*/
	void GetTagsFromSequenceOld( const string& seq, int tagLength, float seqMass, vector< TagInfo >& tags )
	{
		// n-terminal and c-terminal mass
		float nTerminusMass = 0.0f;
		float cTerminusMass = seqMass - WATER_MONO;
		for( int i=0; i < tagLength; ++i ) {
			cTerminusMass -= g_rtConfig->UseAvgMassOfSequences?g_residueMap->getAvgMassByName(seq[i]):g_residueMap->getMonoMassByName( seq[i] );
		}

		size_t seqLength = seq.length();
		size_t maxTagStartIndex = seqLength - tagLength;

		// March through the sequence 
		for( size_t i=0; i <= maxTagStartIndex; ++i )
		{
			// Generate tags of length equal to tagLength. Store the n-terminal and 
			// c-terminal mass for the corresponding tag
			tags.push_back( TagInfo( seq.substr( i, tagLength ), nTerminusMass, cTerminusMass ) );
			tags.back().lowPeakMz = (float) i; // index into sequence where tag starts

			// Set the n-terminal and c-terminal tag masses for next iteration.
			if( i <= maxTagStartIndex )
			{
				nTerminusMass += g_rtConfig->UseAvgMassOfSequences?g_residueMap->getAvgMassByName(seq[i]):g_residueMap->getMonoMassByName( seq[i] );
				cTerminusMass -= g_rtConfig->UseAvgMassOfSequences?g_residueMap->getAvgMassByName(seq[i+tagLength]):g_residueMap->getMonoMassByName( seq[i+tagLength] );
			}
		}
	}

    /**!
		GetTagsFromSequence takes a peptide sequence, its mass, and generates tags of specified length
		(user configurable) from the sequence. 
	*/
	void GetTagsFromSequence( DigestedPeptide peptide, int tagLength, float seqMass, vector< TagInfo >& tags )
	{
        // Get the modification map and the sequence without mods.
        ModificationMap& mods = peptide.modifications();
        string seq = PEPTIDE_N_TERMINUS_STRING + peptide.sequence() + PEPTIDE_C_TERMINUS_STRING;
        vector<float> residueMasses(seq.length());

        residueMasses[0] = g_rtConfig->UseAvgMassOfSequences ? mods[mods.NTerminus()].averageDeltaMass() : mods[mods.NTerminus()].monoisotopicDeltaMass(); 
        for(size_t i = 1; i < residueMasses.size()-1; i++) {
            float modMass = g_rtConfig->UseAvgMassOfSequences ? mods[i-1].averageDeltaMass(): mods[i-1].monoisotopicDeltaMass();
            residueMasses[i] = (g_rtConfig->UseAvgMassOfSequences ? (AminoAcid::Info::record(seq[i])).residueFormula.molecularWeight():(AminoAcid::Info::record(seq[i])).residueFormula.monoisotopicMass()) + modMass;
        }

        //for(size_t i = 0; i < residueMasses.size(); ++i) {
        //    cout << residueMasses[i] << "," ;
        //}
        //cout << endl;
        size_t seqLength = seq.length();
		size_t maxTagStartIndex = seqLength - tagLength;

        float waterMass = WATER(g_rtConfig->UseAvgMassOfSequences);

        float runningNTerminalMass = 0.0f;
        for(size_t i = 0; i <= maxTagStartIndex; ++i) {
            float tagMass = 0.0f;
            for(size_t j = i; j < i+tagLength; j++) {
                tagMass += residueMasses[j];
            }
            float cTerminalMass = seqMass - runningNTerminalMass - tagMass - waterMass;
            tags.push_back( TagInfo( seq.substr( i, tagLength ), runningNTerminalMass, cTerminalMass ) );
            tags.back().lowPeakMz = (float) i;
            //cout << seq.substr( i, tagLength ) << "," << runningNTerminalMass << "," << cTerminalMass << "," << tagMass << endl;
            runningNTerminalMass += residueMasses[i]; 
            //cout << runningNTerminalMass << endl;
        }
	}

	/**
		ScoreSubstitutionVariants takes a candidate peptide and delta mass. The procedure finds
		all the substitutions that can fit the delta mass, generates the variants, scores each
		of the generated variant against an experimental spectrum and stores the results.
	*/
	inline boost::int64_t ScoreSubstitutionVariants(DigestedPeptide candidate, float mass, float modMass, 
												size_t locStart, size_t locEnd, Spectrum* spectrum, 
												int idx, vector<double>& sequenceIons, 
												const bool * ionTypesToSearchFor, float massTol) {

		
		boost::int64_t numComparisonsDone = 0;
		//cout << "\t\t\t\t\t" << candidate.sequence() << "," << modMass << "," << locStart << "," << locEnd << endl;
		// Get all possible amino acid substitutions that fit the modification mass with in the mass tolerance
		DynamicModSet possibleSubstitutions = deltaMasses->getPossibleSubstitutions(modMass, massTol);
		//cout << "\t\t\t\t\t" << possibleSubstitutions << endl;
		if(possibleSubstitutions.size() > 0) {
			// Generate variants of the current peptide using the possible substitutions
			vector <DigestedPeptide> substitutionVariants;
			MakePeptideVariants(candidate, substitutionVariants, 1, possibleSubstitutions, locStart, locEnd, g_rtConfig->MaxNumPeptideVariants);
			// For each variant
			for(size_t aVariantIndex = 0; aVariantIndex < substitutionVariants.size(); aVariantIndex++) {
				const DigestedPeptide& variant = substitutionVariants[aVariantIndex];
				// Check to make sure that the insertion of sub doesn't put the mass of the peptide
				// over the precursor mass tolerance.
				float neutralMass = g_rtConfig->UseAvgMassOfSequences ? ((float) variant.molecularWeight(0,true))
                                                       : (float) variant.monoisotopicMass(0,true);
				float massDiff = fabs(neutralMass - spectrum->mOfPrecursor);
				if(massDiff > g_rtConfig->PrecursorMassTolerance[spectrum->id.charge]) {
					continue;
				}
				string variantSequence = PEPTIDE_N_TERMINUS_SYMBOL + variant.sequence() + PEPTIDE_C_TERMINUS_SYMBOL;
                //cout << "\t\t\t\t\t" << tagrecon::getInterpretation(const_cast <DigestedPeptide&>(variant)) << endl;
				// Initialize the result
				SearchResult result(variant);
				// Compute the predicted spectrum and score it against the experimental spectrum
                //CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, &ionNames, 0 );
                CalculateSequenceIons( variant, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
				//CalculateSequenceIons( variant, variantSequence, spectrum->id.charge, &sequenceIons, g_rtConfig->UseSmartPlusThreeModel, 0, 0, ionTypesToSearchFor );
				spectrum->ScoreSequenceVsSpectrum( result, variantSequence, sequenceIons );
				// Create the result
				// Compute the true modification mass. The modMass of in the arguments is used to look up
				// the canidate mods with a certain tolerance. It's not the true modification mass of the
				// peptide.
				float trueModificationMass = g_rtConfig->UseAvgMassOfSequences? variant.modifications().averageDeltaMass()-candidate.modifications().averageDeltaMass() : variant.modifications().monoisotopicDeltaMass() - candidate.modifications().monoisotopicDeltaMass();
				result.mod = trueModificationMass;
				result.massError = spectrum->mOfPrecursor-(mass+trueModificationMass);
				// Assign the peptide identification to the protein by loci
				result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );
               // cout << "\t\t\t\t\t" << result.mvh << "," << result.mzFidelity << endl;

				++ numComparisonsDone;

				// Update some search stats and add the result to the
				// spectrum
				simplethread_lock_mutex( &spectrum->mutex );
				if( proteins[idx].isDecoy() )
					++ spectrum->numDecoyComparisons;
				else
					++ spectrum->numTargetComparisons;
				spectrum->resultSet.add( result );
				simplethread_unlock_mutex( &spectrum->mutex );
			}
		}
		return numComparisonsDone;
	}


	/**
		ScoreUnknownModification takes a peptide sequence and localizes an unknown modification mass
		to a particular residue in the sequence. The number of tested resiudes is defined by locStart
		and locEnd variables of the procedure.
	*/
	
	inline boost::int64_t ScoreUnknownModification(DigestedPeptide candidate, float mass, float modMass, 
												size_t locStart, size_t locEnd, Spectrum* spectrum, 
												int idx, vector<double>& sequenceIons, 
												const bool * ionTypesToSearchFor) {

		
		boost::int64_t numComparisonsDone = 0;
		DynamicModSet possibleInterpretations;
		string peptideSeq = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
		
		//cout << candidate.sequence() << "," << modMass << "," << locStart << "," << locEnd << endl;
		// For each amino acid between the location bounds
		for(size_t aaIndex = locStart; aaIndex <= locEnd; aaIndex++) {
			// Add the modification to the amino acid
			DynamicMod mod(peptideSeq[aaIndex],peptideSeq[aaIndex],modMass);
			possibleInterpretations.insert(mod);
		}

		// Generate peptide variants of the current petpide using possible modifications
		vector<DigestedPeptide> modificationVariants;
        //cout << "Making variants" << endl;
        MakePeptideVariants(candidate, modificationVariants, 1, possibleInterpretations, locStart, locEnd, g_rtConfig->MaxNumPeptideVariants);
		// For each variant
		for(size_t variantIndex = 0; variantIndex < modificationVariants.size(); variantIndex++) {
			const DigestedPeptide& variant = modificationVariants[variantIndex];
            //cout << "\t" << variant.sequence() << endl;
			string variantSequence = PEPTIDE_N_TERMINUS_SYMBOL + variant.sequence() + PEPTIDE_C_TERMINUS_SYMBOL;
			// Initialize search result
			SearchResult result(variant);
			// Compute the predicted spectrum and score it against the experimental spectrum
			//CalculateSequenceIons( variant, variantSequence, spectrum->id.charge, &sequenceIons, g_rtConfig->UseSmartPlusThreeModel, 0, 0, ionTypesToSearchFor );
            CalculateSequenceIons( variant, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
			spectrum->ScoreSequenceVsSpectrum( result, variantSequence, sequenceIons );
			// Assign the modification mass and the mass error
			// Compute the true modification mass. The modMass of in the arguments is used to look up
			// the canidate mods with a certain tolerance. It's not the true modification mass of the
			// peptide.
			float trueModificationMass = g_rtConfig->UseAvgMassOfSequences? variant.modifications().averageDeltaMass()-candidate.modifications().averageDeltaMass() : variant.modifications().monoisotopicDeltaMass() - candidate.modifications().monoisotopicDeltaMass();
			result.mod = trueModificationMass;
			result.massError = spectrum->mOfPrecursor-(mass+trueModificationMass);
			// Assign the peptide identification to the protein by loci
			result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );

			++ numComparisonsDone;

			// Update some search stats and add the result to the
			// spectrum
			simplethread_lock_mutex( &spectrum->mutex );
			if( proteins[idx].isDecoy() )
				++ spectrum->numDecoyComparisons;
			else
				++ spectrum->numTargetComparisons;
			spectrum->resultSet.add( result );
			simplethread_unlock_mutex( &spectrum->mutex );
		}

		// Return the number of comparisons performed
		return numComparisonsDone;
	}

	/**!
		QuerySequence function takes a candidate peptide sequence as input. It generates all n length tags from 
		the peptide sequence. For each of the generated tags, it locates the spectra that contain the tag. It 
		computes the n-terminal and c-terminal delta masses between the candidate peptide sequence and spectral
		tag-based sequence sourrounding the tag match. 
		For example:
					//    XXXXXXXXNSTXXXXXXXX (peptide sequence)
					//            |||		  (Tag match)
					//	  [200.45]NST[400.65] (spectral tag-based sequence)
		If either n-terminal or c-terminal delta mass is greater than set mass tolerance then the function 
		interprets either of them using a variable modification list supplied by the user. The function can 
		also explain the delta mass without the help of a user-supplied	variable modification list. If there
		are no non-zero delta masses then the function interprets the peptide match as an unmodified peptide.
	*/
	boost::int64_t QuerySequence( const DigestedPeptide& candidate, int idx, bool estimateComparisonsOnly = false )
	{
		// Search stats
		boost::int64_t numComparisonsDone = 0;
		// Candidate peptide sequence and its mass
        string aSequence  = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
		string seq = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
		//cout << aSequence << "," << aSequence.length() << "," << candidate.sequence() << "," << candidate.sequence().length() << endl;
        float neutralMass = g_rtConfig->UseAvgMassOfSequences ? ((float) candidate.molecularWeight(0,true))
                                                       : (float) candidate.monoisotopicMass(0,true);

		// A amino acid residue map
        ResidueMap* residueMap = new ResidueMap();
		// A vector to store fragment ions by charge state
		vector< double > fragmentIonsByChargeState;
		// A spectrum pointer
		Spectrum* spectrum;
		// A data structure to store the results
        SearchResult result(candidate);
		// A variable to hold the number of common peaks between hypothetical
		// and experimental spectrum
		size_t peaksFound;
		// Ion types to search for {y, b, [y-H2O,b-H2O], [y-NH3,b-NH3]}
		static const bool ionTypesToSearchFor[4] = { true, true, false, false };

        //set<SpectrumId> matchedSpectra;
		// Get tags of length 3 from the candidate peptide sequence
		vector< TagInfo > candidateTags;
		//GetTagsFromSequenceOld( seq, 3, neutralMass, candidateTags );
        GetTagsFromSequence( candidate, 3, neutralMass, candidateTags );
        //cout << "Query:TagMapSize:" <<spectraTagMapsByChargeState.size() << endl;
		//string comparisonDone;

        //cout << "peptide:" << seq << "->" << neutralMass << "->" << getInterpretation(candidate) << endl;
		// For each of the generated tags
		for( size_t i=0; i < candidateTags.size(); ++i )
		{
			const TagInfo& tag = candidateTags[i];

			vector< double >& sequenceIons = fragmentIonsByChargeState;
			// Get the range of spectral tags that have the same sequence as the peptide tag
			// and total mass deviation between the n-terminal and c-terminal masses <= +/-
			// MaxTagMassDeviation.
			TagSetInfo tagKey(tag.tag, tag.nTerminusMass, tag.cTerminusMass);
            //cout << "\t" << tagKey.candidateTag << "," << tagKey.nTerminusMass << "," << tagKey.cTerminusMass << endl;
			pair< SpectraTagMap::const_iterator, SpectraTagMap::const_iterator > range = spectraTagMapsByChargeState.equal_range( tagKey);

			SpectraTagMap::const_iterator cur, end = range.second;
			
			// Iterate over the range
			for( cur = range.first; cur != end; ++cur )
			{
				//cout << "\t\t" << (*(*cur).sItr)->id.source << " " << (*(*cur).sItr)->id.index << " " << (*(*cur).sItr)->mOfPrecursor << endl;

				// Compute the n-terminal and c-terminal mass deviation between the peptide
				// sequence and the spectral tag-based sequence ([200.45]NST[400.65]) 
				// outside the tag match. For example:
				//    XXXXXXXXNSTXXXXXXXX (peptide sequence)
				//            |||		  (Tag match)
				//	  [200.45]NST[400.65] (spectral tag-based sequence)
				float nTerminusDeviation = fabs( tag.nTerminusMass - (*cur).nTerminusMass );
				float cTerminusDeviation = fabs( tag.cTerminusMass - (*cur).cTerminusMass );
				//cout << "\t\tBef:" << (nTerminusDeviation+cTerminusDeviation) << endl;
				if(nTerminusDeviation+cTerminusDeviation>= 300.0f) {
					continue;
				}

                // Get the charge state of the fragment ions that gave rise to the
                // tag
                int tagCharge = (*cur).tagChargeState;
				//cout << "\t\tAft:" << (nTerminusDeviation+cTerminusDeviation) << endl;
				// If both mass deviations are too big then forestall the search.
				// This is essentially searching for candidate alignments where 
				// the tags and either of the n-terminal or c-terminal mass have to 
				// match between the candidate petpide sequence and the tag-based
				// sequence. This strategy only accounts for peptides where mods happen
				// on only one side of the tag match. What happens if the residues 
				// on both sides of the tag match are modified?
                //cout << "\t\ttagCharge:" << tagCharge << "\n" << endl;
				if( nTerminusDeviation > g_rtConfig->NTerminalMassTolerance[tagCharge-1] &&
					cTerminusDeviation > g_rtConfig->CTerminalMassTolerance[tagCharge-1] )
					continue;

				// Get the mass spectrum
				spectrum = (*(*cur).sItr);
                // Get the spectrum charge
                int spectrumCharge = spectrum->id.charge;
				//cout << "\t\tComparing " << spectrum->id.source << " " << spectrum->id.index << endl;

				//if( spectrum->id.charge != z+1 )
				//	continue;

				// If Tag and c-terminal masses match then try to put the mod
				// on the residues that are n-terminus to the tag match. Use the
				// total mass difference between the candidate as the mod mass.
				float modMass = ((float)spectrum->mOfPrecursor) - neutralMass;
           
				// Don't bother interpreting it if the mass is less than -130.0 Da
				// -130 is close to Trp -> Gly substitution mass.
				if( modMass < -130.0 ) {
					continue;
				}

                if(g_rtConfig->FindUnknownMods || g_rtConfig->FindSequenceVariations) {
                    // Figure out if the modification mass does nullify
                    // any dynamic mods. For example, M+16ILFEGHFK peptide
                    // can not have a modification of -16 on M. 
                    bool legitimateModMass = true;
                    // Get the dynamic mods
                    ModificationMap& dynamicMods = (const_cast<DigestedPeptide&>(candidate)).modifications();
                    // Step through each of the dynamic mods
                    for(ModificationMap::iterator itr = dynamicMods.begin(); itr != dynamicMods.end() && legitimateModMass ; ++itr) {
                        // Compute the mod mass
                        float residueModMass = g_rtConfig->UseAvgMassOfSequences? (*itr).second.averageDeltaMass() : (*itr).second.monoisotopicDeltaMass();
                        // Check to make sure that the unknown mod mass doesn't nullify the dynamic mod mass
                        if(fabs(residueModMass+modMass) <= g_rtConfig->PrecursorMassTolerance[spectrumCharge-1]) {
                            legitimateModMass = false;
                        }
                    }
					// Make sure that the modification mass doesn't negate the mods already present in the peptide
					float candidateModificationsMass = g_rtConfig->UseAvgMassOfSequences ? candidate.modifications().averageDeltaMass() : candidate.modifications().monoisotopicDeltaMass();
					if(fabs(candidateModificationsMass-modMass)< max((float)NEUTRON, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])) {
						legitimateModMass = false;
					}
                    //cout << aSequence << "," << tag.tag << endl;
                    if( legitimateModMass && fabs(modMass) > max((float)NEUTRON, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])
                        && nTerminusDeviation > g_rtConfig->NTerminalMassTolerance[tagCharge-1] 
                        && cTerminusDeviation <= g_rtConfig->CTerminalMassTolerance[tagCharge-1]) {
                        // Get the peptide sequence on the n-terminus of the tag match
                        string nTerminus = aSequence.substr( 0, (size_t) tag.lowPeakMz );


                        // If the user configured the searches for substitutions
                        if(g_rtConfig->FindSequenceVariations) {
                            //cout << "\t\t\t\t" << candidate.sequence() << "," << tag.tag << ","  << fabs(tag.nTerminusMass-cur->nTerminusMass) << "," << fabs(tag.cTerminusMass-cur->cTerminusMass) << "," << neutralMass << "," << modMass << "," << spectrum->mOfPrecursor 
                            //    << "," << (neutralMass+modMass-spectrum->mOfPrecursor) << endl;
                            // Find the substitutions that fit the mass, generate variants and score them.
                            //comparisonDone = "Seq:Nterm";
                            numComparisonsDone += ScoreSubstitutionVariants(candidate, neutralMass, modMass, 0, 
                                (size_t) tag.lowPeakMz-1,spectrum, 
                                idx, sequenceIons, ionTypesToSearchFor, g_rtConfig->NTerminalMassTolerance[tagCharge-1]);
                        }

                        // If the user wants us to find unknown modifications.
                        if(g_rtConfig->FindUnknownMods) {
                            //comparisonDone = "PTM:Nterm";
                            //cout << candidate.sequence() << "," << tag.tag  << "," << fabs(tag.nTerminusMass-cur->nTerminusMass) << "," << fabs(tag.cTerminusMass-cur->cTerminusMass) << "," << neutralMass << "," << modMass << "," << spectrum->mOfPrecursor 
                            //	 << "," << (neutralMass+modMass-spectrum->mOfPrecursor) << endl;
                            numComparisonsDone += ScoreUnknownModification(candidate,neutralMass, modMass, 0, 
                                (size_t) tag.lowPeakMz-1, spectrum, 
                                idx, sequenceIons,	ionTypesToSearchFor);
                        }
                    }

                    if( legitimateModMass && fabs(modMass) > max((float)NEUTRON, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])
                        && cTerminusDeviation > g_rtConfig->CTerminalMassTolerance[tagCharge-1] 
                        && nTerminusDeviation <= g_rtConfig->NTerminalMassTolerance[tagCharge-1]) {
                        // Do the same thing we did for reconciling n-terminal mass difference
                        // This time we are reconciling the c-terminal mass difference
                        string cTerminus = aSequence.substr( (size_t) tag.lowPeakMz + tag.tag.length() );

                        if(g_rtConfig->FindSequenceVariations) {
                            //comparisonDone = "Seq:Cterm";
                            //cout << "\t\t\t\t" << candidate.sequence() << "," << tag.tag  << "," << fabs(tag.nTerminusMass-cur->nTerminusMass) << "," << fabs(tag.cTerminusMass-cur->cTerminusMass) << "," << neutralMass << "," << modMass << "," << spectrum->mOfPrecursor 
                            //	<< "," << (neutralMass+modMass-spectrum->mOfPrecursor) << endl;
                            // Find the substitutions that fit the mass, generate variants and score them.
                            numComparisonsDone += ScoreSubstitutionVariants(candidate, neutralMass, modMass, (size_t) tag.lowPeakMz + tag.tag.length(), 
                                aSequence.length()-1,	spectrum, idx, 
                                sequenceIons, ionTypesToSearchFor, g_rtConfig->CTerminalMassTolerance[tagCharge-1]);
                        }

                        if(g_rtConfig->FindUnknownMods) {
                            //comparisonDone = "PTM:Cterm";
                            //cout << candidate.sequence() << "," << tag.tag  << "," << fabs(tag.nTerminusMass-cur->nTerminusMass) << "," << fabs(tag.cTerminusMass-cur->cTerminusMass) << "," << neutralMass << "," << modMass << "," << spectrum->mOfPrecursor 
                            //	<< "," << (neutralMass+modMass-spectrum->mOfPrecursor) << endl;
                            numComparisonsDone += ScoreUnknownModification(candidate,neutralMass, modMass, (size_t) tag.lowPeakMz + tag.tag.length(), 
                                aSequence.length()-1, spectrum, idx, 
                                sequenceIons, ionTypesToSearchFor);
                        } 
                    } 
                }

                //cout << "\t\t\t" <<modMass << "," << nTerminusDeviation << "," << cTerminusDeviation << endl;
                if(fabs(modMass) <= g_rtConfig->PrecursorMassTolerance[spectrumCharge-1] 
                    && nTerminusDeviation <= g_rtConfig->NTerminalMassTolerance[tagCharge-1]
                    && cTerminusDeviation <= g_rtConfig->CTerminalMassTolerance[tagCharge-1]) {
					// If there are no n-terminal and c-terminal delta mass differences then
					// score the match as an unmodified sequence.
					//comparisonDone = "DIRECT";
					//comparisonDone = comparisonDone + "->" + aSequence;
                    //cout << "Direct" << endl;
                    //if(matchedSpectra.find(spectrum->id)!=matchedSpectra.end()) {
                    //    continue;
                    //}
                    CalculateSequenceIons( candidate, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
					//CalculateSequenceIons( candidate, aSequence, spectrum->id.charge, &sequenceIons, g_rtConfig->UseSmartPlusThreeModel, 0, 0, ionTypesToSearchFor );
					peaksFound = spectrum->ScoreSequenceVsSpectrum( result, aSequence, sequenceIons );
					//result.mass = neutralMass;
					//result.mod = modMass;
					//result.massError = spectrum->mOfPrecursor-result.mass;
					result.massError = spectrum->mOfPrecursor-neutralMass;
					result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );	
					++numComparisonsDone;
                    //cout << numComparisonsDone << endl;
					//comparisonDone = comparisonDone + "->" + result.sequence;
					if( g_rtConfig->UseMultipleProcessors )
						simplethread_lock_mutex( &spectrum->mutex );
					if( proteins[idx].isDecoy() )
						++ spectrum->numDecoyComparisons;
					else
						++ spectrum->numTargetComparisons;
					spectrum->resultSet.add( result );

                    //matchedSpectra.insert(spectrum->id);
					//cout << " " << aSequence << " " << spectrum->id.id << " " << result.getTotalScore() << " " \
					<< result.sequence << " " << result.mass << endl;
                    //cout << "\t\t\t" << aSequence << result.getTotalScore() << endl;
					if( g_rtConfig->UseMultipleProcessors )
						simplethread_unlock_mutex( &spectrum->mutex );
				}
			}
		}

		//if(comparisonDone.length()>0) {
		//	cout << comparisonDone << "->" << peaksFound <<  endl;
		//}
        //cout << numComparisonsDone;
        //cout << comparisonDone << endl;
        delete residueMap;
		return numComparisonsDone;
	}
	
	/**!
		ExecuteSearchThread function takes a thread, figures out which part of the protein database
		needs to be searched with the thread, generates the peptide sequences for the candidate 
		protein sequences, and searches them with the tags and the spectra that generated the tags.
	*/
	simplethread_return_t ExecuteSearchThread( simplethread_arg_t threadArg )
	{
		// Get a sempahore on this function
		simplethread_lock_mutex( &resourceMutex );
		// Get the thread ID
		simplethread_id_t threadId = simplethread_get_id();
		WorkerThreadMap* threadMap = (WorkerThreadMap*) threadArg;
		// Find the data structure that is supposed to store the thread information.
		WorkerInfo* threadInfo = reinterpret_cast< WorkerInfo* >( threadMap->find( threadId )->second );
		int numThreads = (int) threadMap->size();
		// Get the residue map
		ResidueMap threadResidueMap( *g_residueMap );
		// Realease the semaphore
		simplethread_unlock_mutex( &resourceMutex );

		bool done;
		//threadInfo->spectraResults.resize( (int) spectra.size() );
		
		//double largestDynamicModMass = g_residueMap->dynamicMods.empty() ? 0 : g_residueMap->dynamicMods.rbegin()->modMass * g_rtConfig->MaxDynamicMods;
		//double smallestDynamicModMass = g_residueMap->dynamicMods.empty() ? 0 : g_residueMap->dynamicMods.begin()->modMass * g_rtConfig->MaxDynamicMods;
		
		Timer searchTime;
		float totalSearchTime = 0;
		float lastUpdate = 0;
		searchTime.Begin();

		while( true )
		{
			// Get the semaphore
			simplethread_lock_mutex( &resourceMutex );
			done = workerNumbers.empty();
			// If we are not done then get a worker ID
			if( !done )
			{
				threadInfo->workerNum = workerNumbers.back();
				workerNumbers.pop_back();
			}
			simplethread_unlock_mutex( &resourceMutex );

			if( done )
				break;

			// Figure out which section of the protein database
			// needs to be searched with this thread
			int numProteins = (int) proteins.size();
			threadInfo->endIndex = ( numProteins / g_numWorkers )-1;

			int i;
			vector<DigestedPeptide> digestedPeptides;
			// The database search schema works in the following fashion
			// A protein database is split into equal parts using number
			// of processors available for search. If each processor is a
			// multi-core CPU then multiple threads (equal to number of cores
			// in a processor are created per processor. Each processor takes
			// its chunk of protein database and searches it with all the
			// spectra in the dataset using threads. The thread works in a
			// interweaving fashion.
			// For each worker thread on the processor
			for( i = threadInfo->workerNum; i < numProteins; i += g_numWorkers )
			{
				++ threadInfo->stats.numProteinsDigested;

				// Get a protein sequence
				Peptide protein(proteins[i].getSequence());
				// Digest the protein sequence using pwiz library. The sequence is
				// digested using cleavage rules specified in the user configuration
				// file.
                Digestion digestion( protein, g_rtConfig->digestionMotifs, g_rtConfig->digestionConfig );
				// For each peptide
				for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr ) {
					// Get the mass
					double mass = g_rtConfig->UseAvgMassOfSequences ? itr->molecularWeight()
						: itr->monoisotopicMass();
                    if( mass < g_rtConfig->curMinSequenceMass-g_rtConfig->MaxTagMassDeviation ||
						mass > g_rtConfig->curMaxSequenceMass+g_rtConfig->MaxTagMassDeviation )
						continue;

                    digestedPeptides.clear();
					//START_PROFILER(0);
					// Make any PTM variants if user has specified dynamic mods. This function
					// decorates the candidate peptide with both static and dynamic mods.
                    if(MakePtmVariants( *itr, digestedPeptides, g_rtConfig->MaxDynamicMods, g_residueMap->dynamicMods, g_residueMap->staticMods, g_rtConfig->MaxNumPeptideVariants)) {
                        ++ threadInfo->stats.numCandidatesSkipped;
                    }
                   
                    threadInfo->stats.numCandidatesGenerated += digestedPeptides.size();
                    
                    
					//STOP_PROFILER(0);
                    // For each candidate peptide sequence
				    for( size_t j=0; j < digestedPeptides.size(); ++j )
				    {
					    //START_PROFILER(1);
					    // Search the sequence against the tags and the spectra that generated the tags
                        boost::int64_t queryComparisonCount = QuerySequence( digestedPeptides[j], i );
					    //cout << digestedPeptides[j].sequence() << " qCC:" << queryComparisonCount <<" test:" << (boost::int64_t(1) << 40) << endl;
					    //STOP_PROFILER(1);
					    // Update some thread statistics
					    if( queryComparisonCount > 0 )
					    {
						    threadInfo->stats.numComparisonsDone += queryComparisonCount;
						    ++ threadInfo->stats.numCandidatesQueried;
					    }
				    }
				}
				
				if( g_numChildren == 0 )
					totalSearchTime = searchTime.TimeElapsed();
				//if( g_pid == 1 && !(i%50) )
				//	cout << i << ": " << (int) sequenceToSpectraMap.size() << " " << (int) sequenceProteinLoci.size() << endl;

				// Print out some numbers to keep the user interested or disinterested.....
				if( g_numChildren == 0 && ( ( totalSearchTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i == numProteins ) )
				{
					//int curProtein = ( i + 1 ) / g_numWorkers;
					int proteinsPerSec = (int) ((float( threadInfo->stats.numProteinsDigested ) / totalSearchTime)+0.5);
					float estimatedTimeRemaining = float( ( numProteins / numThreads ) - threadInfo->stats.numProteinsDigested ) / proteinsPerSec;

					simplethread_lock_mutex( &resourceMutex );
					cout << threadInfo->workerHostString << " has searched " << threadInfo->stats.numProteinsDigested << " of " <<	numProteins <<
							" proteins; " << proteinsPerSec << " per second, " << totalSearchTime << " elapsed, " << estimatedTimeRemaining << " remaining." << endl;

					PRINT_PROFILERS(cout, threadInfo->workerHostString + " profiling")

					//float candidatesPerSec = threadInfo->stats.numComparisonsDone / totalSearchTime;
					//float estimatedTimeRemaining = float( numCandidates - threadInfo->stats.numComparisonsDone ) / candidatesPerSec / numThreads;
					//cout << threadInfo->workerHostString << " has made " << threadInfo->stats.numComparisonsDone << " of about " << numCandidates << " comparisons; " <<
					//		candidatesPerSec << " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
					simplethread_unlock_mutex( &resourceMutex );

					lastUpdate = totalSearchTime;
					//cout << g_hostString << " has scored " << resultSpectra << " spectra and " << resultSequences << " sequences
				}
			}
		}
		//i -= g_numWorkers;
		//cout << threadInfo->workerHostString << " last searched protein " << i-1 << " (" << proteins[i].name << ")." << endl;

		return 0;
	}

	
	/**!
		ExecuteSearch function determines the number of workers depending
		on the number of CPUs present in a box and create threads for each
		of the CPU. This function treats each multi-core CPU as a multi-processor
		machine.
	*/
	searchStats ExecuteSearch()
	{
		// A map to store worker threads
		WorkerThreadMap workerThreads;

		int numProcessors = g_numWorkers;
		workerNumbers.clear();

		// If we are using a multi-core CPU or multiple processor box
		if( /*!g_singleScanMode &&*/ g_rtConfig->UseMultipleProcessors && g_numWorkers > 1 )
		{
			g_numWorkers *= g_rtConfig->ThreadCountMultiplier;

			simplethread_handle_array_t workerHandles;

			// Generate handles for each worker
			for( int i=0; i < g_numWorkers; ++i ) {
				workerNumbers.push_back(i);
			}

			// Get a semaphore
			simplethread_lock_mutex( &resourceMutex );
			// Create a thread for each of the processor and
			// attach the procedure that needs to be executed
			// for each thread [i.e. the start() function].
			for( int t = 0; t < numProcessors; ++t )
			{
				simplethread_id_t threadId;
				simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &ExecuteSearchThread, &workerThreads );
				workerThreads[ threadId ] = new WorkerInfo( t, 0, 0 );
				workerHandles.array.push_back( threadHandle );
			}
			simplethread_unlock_mutex( &resourceMutex );

			simplethread_join_all( &workerHandles );

			// Number of worker threads is equal to number of processors
			g_numWorkers = numProcessors;
			//cout << g_hostString << " searched " << numSearched << " proteins." << endl;
		} else
		{
			// Obvious otherwise
			g_numWorkers = 1;
			workerNumbers.push_back(0);
			simplethread_id_t threadId = simplethread_get_id();
			workerThreads[ threadId ] = new WorkerInfo( 0, 0, 0 );
			ExecuteSearchThread( &workerThreads );
			//cout << g_hostString << " searched " << numSearched << " proteins." << endl;
		}

		searchStats stats;

		// Accumulate the statistics for each of the worker thread
		for( WorkerThreadMap::iterator itr = workerThreads.begin(); itr != workerThreads.end(); ++itr )
			stats = stats + reinterpret_cast< WorkerInfo* >( itr->second )->stats;

		return stats;
	}

	
	simplethread_return_t ExecuteCountThread( simplethread_arg_t threadArg )
	{
		simplethread_lock_mutex( &resourceMutex );
		simplethread_id_t threadId = simplethread_get_id();
		WorkerThreadMap* threadMap = (WorkerThreadMap*) threadArg;
		WorkerInfo* threadInfo = reinterpret_cast< WorkerInfo* >( threadMap->find( threadId )->second );
		simplethread_unlock_mutex( &resourceMutex );

		bool done;
		//double largestDynamicModMass = g_residueMap->dynamicMods.empty() ? 0 : g_residueMap->dynamicMods.rbegin()->modMass * g_rtConfig->MaxDynamicMods;
		//double smallestDynamicModMass = g_residueMap->dynamicMods.empty() ? 0 : g_residueMap->dynamicMods.begin()->modMass * g_rtConfig->MaxDynamicMods;
		
		Timer samplingTime(true);

		while( samplingTime.TimeElapsed() < g_rtConfig->ProteinSamplingTime )
		{
			simplethread_lock_mutex( &resourceMutex );
			done = workerNumbers.empty();
			if( !done )
			{
				threadInfo->workerNum = workerNumbers.back();
				workerNumbers.pop_back();
			}
			simplethread_unlock_mutex( &resourceMutex );

			if( done )
				break;

			int i = threadInfo->workerNum;
			// Get a protein sequence
			vector <DigestedPeptide> digestedPeptides;
			Peptide protein(proteins[i].getSequence());
			// Digest the protein sequence using pwiz library. The sequence is
			// digested using cleavage rules specified in the user configuration
			// file.
			Digestion digestion( protein, g_rtConfig->digestionMotifs, g_rtConfig->digestionConfig );
			// For each peptide
			for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr ) {
				// Get the mass
				double mass = g_rtConfig->UseAvgMassOfSequences ? itr->molecularWeight()
					: itr->monoisotopicMass();
                 if( mass < g_rtConfig->curMinSequenceMass-g_rtConfig->MaxTagMassDeviation ||
						mass > g_rtConfig->curMaxSequenceMass+g_rtConfig->MaxTagMassDeviation )
						continue;
                 
			    digestedPeptides.clear();
				// Make any PTM variants if user has specified dynamic mods. This function
				// decorates the candidate peptide with both static and dynamic mods.
				MakePtmVariants( *itr, digestedPeptides, g_rtConfig->MaxDynamicMods, g_residueMap->dynamicMods, g_residueMap->staticMods, g_rtConfig->MaxNumPeptideVariants );
                threadInfo->stats.numCandidatesGenerated += digestedPeptides.size();
                
			    // For each candidate peptide sequence
			    for( size_t j=0; j < digestedPeptides.size(); ++j ) {
                     // Get the number of comparisons that would be performed if the search is performed.
                     boost::int64_t queryComparisonCount = QuerySequence( digestedPeptides[j], i, true );
                     if(queryComparisonCount > 0) {
				        // Update the number of queries statistics
				        ++threadInfo->stats.numCandidatesQueried;
                        threadInfo->stats.numComparisonsDone += queryComparisonCount;
                     }
			    }
			}
		}

		return 0;
	}

	boost::int64_t ExecuteCount()
	{
		WorkerThreadMap workerThreads;

		// protein list is already randomly shuffled
		for( size_t p=0; p < proteins.size(); ++p )
			workerNumbers.push_back( (int) p );

		int numProcessors = g_numWorkers;
		if( g_rtConfig->UseMultipleProcessors && g_numWorkers > 1 )
		{
			g_numWorkers *= g_rtConfig->ThreadCountMultiplier;

			simplethread_handle_array_t workerHandles;

			simplethread_lock_mutex( &resourceMutex );
			for( int t = 0; t < numProcessors; ++t )
			{
				simplethread_id_t threadId;
				simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &ExecuteCountThread, &workerThreads );
				workerThreads[ threadId ] = new WorkerInfo( t, 0, 0 );
				workerHandles.array.push_back( threadHandle );
			}
			simplethread_unlock_mutex( &resourceMutex );

			simplethread_join_all( &workerHandles );
			workerHandles.array.clear();

			g_numWorkers = numProcessors;

		} else
		{
			g_numWorkers = 1;
			simplethread_id_t threadId = simplethread_get_id();
			workerThreads[ threadId ] = new WorkerInfo( 0, 0, 0 );
			ExecuteCountThread( &workerThreads );
		}

		searchStats stats;

		for( WorkerThreadMap::iterator itr = workerThreads.begin(); itr != workerThreads.end(); ++itr )
		{
			stats = stats + reinterpret_cast< WorkerInfo* >( itr->second )->stats;
			delete itr->second;
		}

		return (boost::int64_t) ( stats.numComparisonsDone * ( (float) proteins.size() / stats.numProteinsDigested ) );
	}

	/**!
		ProcessHandler function reads the input files and protein database to perform the search.
		The function preprocess the spectra, splits the protein database according to the number
		of processors available (including clusters), and executes the search on each of the
		processor simultaneously. The results are put together if the search has been executed on
		a multi-core CPU or multi-node cluster.
	*/
	int ProcessHandler( int argc, char* argv[] )
	{
		simplethread_create_mutex( &resourceMutex );

		//Process the command line arguments
		vector< string > args;
		for(int i=0; i < argc; ++i )
			args.push_back( argv[i] );

		if( InitProcess( args ) )
			return 1;

		//Get the database file name
		g_dbFilename = g_rtConfig->ProteinDatabase;
		//cout << g_dbFilename << "\n";
		int numSpectra = 0;

		INIT_PROFILERS(13)

		//If the process is a master process
		if( g_pid == 0 )
		{
			// Read the the regular exp and locate the input files.
			for( size_t i=1; i < args.size(); ++i )
			{
				//cout << g_hostString << " is reading spectra from files matching mask \"" << args[i] << "\"" << endl;
				FindFilesByMask( args[i], g_inputFilenames );
			}

			if( g_inputFilenames.empty() )
			{
				cout << g_hostString << " did not find any spectra matching given filemasks." << endl;
				return 1;
			}

			// Test for the protein database format
			if( !TestFileType( g_dbFilename, "fasta" ) )
				return 1;

			cout << g_hostString << " is reading \"" << g_dbFilename << "\"" << endl;
			Timer readTime(true);
			// Read the protein database 
			try
			{
				proteins.readFASTA( g_dbFilename, g_rtConfig->StartProteinIndex, g_rtConfig->EndProteinIndex );
			} catch( exception& e )
			{
				cout << g_hostString << " had an error: " << e.what() << endl;
				return 1;
			}
			cout << g_hostString << " read " << proteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl;

			proteins.random_shuffle(); // randomize order to optimize work distribution

			// Split the database into multiple parts to distrubute it over the cluster
			#ifdef USE_MPI
				if( g_numChildren > 0 )
				{
					g_rtConfig->ProteinBatchSize = (int) ceil( (float) proteins.size() / (float) g_numChildren / (float) g_rtConfig->NumBatches );
					cout << g_hostString << " calculates dynamic protein batch size is " << g_rtConfig->ProteinBatchSize << endl;
				}
			#endif

			// Read the input spectra
			set<std::string> finishedFiles;
			set<std::string>::iterator fItr;
			// For each input file name
			for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
			{
				Timer fileTime(true);

				// Clear the spectra object
				spectra.clear();

				cout << g_hostString << " is reading spectra from file \"" << *fItr << "\"" << endl;
				finishedFiles.insert( *fItr );

				Timer readTime(true);

				//long long memoryUsageCap = (int) GetAvailablePhysicalMemory() / 4;
				//int peakCountCap = (float) memoryUsageCap / ( sizeof( peakPreInfo ) + sizeof( peakInfo ) );
				//cout << g_hostString << " sets memory usage ceiling at " << memoryUsageCap << ", or about " << peakCountCap << " total peaks." << endl;

				// Read runtime variables and source data filepath from tags file (but not the tags)
				RunTimeVariableMap varsFromFile(	"NumChargeStates DynamicMods StaticMods UseAvgMassOfSequences "
													"DuplicateSpectra UseChargeStateFromMS PrecursorMzTolerance "
											 		"FragmentMzTolerance TicCutoffPercentage" );
				// Read the tags and return the source file path
				string sourceFilepath = spectra.readTags( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum, true);
				// Set the parameters for the search
				g_rtConfig->setVariables( varsFromFile );
				// Set static and variable mods
				g_residueMap->dynamicMods = DynamicModSet( g_rtConfig->DynamicMods );
				g_residueMap->staticMods = StaticModSet( g_rtConfig->StaticMods );

				// Read peaks from the source data of the tags file
				spectra.readPeaks( sourceFilepath, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum );

				// Count total number of peaks in all the spectra in the current file
				int totalPeakCount = 0;
				for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
					totalPeakCount += (*sItr)->peakCount;

				cout << g_hostString << " read " << spectra.size() << " spectra with " << totalPeakCount << " peaks; " << readTime.End() << " seconds elapsed." << endl;

				// Skip empty spectra
				int skip = 0;
				if( spectra.empty() )
				{
					cout << g_hostString << " is skipping a file with no spectra." << endl;
					skip = 1;
				}

				// If the program is running on a cluster then determine
				// the optimal batch size for sending the spectra over
				// to the other processors
				#ifdef USE_MPI
					if( g_numChildren > 0 )
					{
						g_rtConfig->SpectraBatchSize = (int) ceil( (float) spectra.size() / (float) g_numChildren / (float) g_rtConfig->NumBatches );
						cout << g_hostString << " calculates dynamic spectra batch size is " << g_rtConfig->SpectraBatchSize << endl;
					}

					// Send the skip variable to all child processes
					for( int p=0; p < g_numChildren; ++p )
						MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
				#endif

				Timer searchTime;
				string startTime;
				string startDate;
				// A data structure to store search statistics
                searchStats sumSearchStats;
				vector< size_t > opcs; // original peak count statistics
				vector< size_t > fpcs; // filtered peak count statistics

				if( !skip )
				{
					// If the current process is a parent process
					if( g_numProcesses > 1 )
					{
						#ifdef USE_MPI
							//Use the child processes to prepare the spectra
							cout << g_hostString << " is sending spectra to worker nodes to prepare them for search." << endl;
							Timer prepareTime(true);
							TransmitUnpreparedSpectraToChildProcesses();

							spectra.clear();

							ReceivePreparedSpectraFromChildProcesses();

							numSpectra = (int) spectra.size();

							skip = 0;
							if( numSpectra == 0 )
							{
								cout << g_hostString << " is skipping a file with no suitable spectra." << endl;
								skip = 1;
							}

							// Send the message to skip the file to all sub-processes
							for( int p=0; p < g_numChildren; ++p )
								MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );

							if( !skip )
							{
								// Print out some spectra stats
								opcs = spectra.getOriginalPeakCountStatistics();
								fpcs = spectra.getFilteredPeakCountStatistics();
								cout << g_hostString << ": mean original (filtered) peak count: " <<
										opcs[5] << " (" << fpcs[5] << ")" << endl;
								cout << g_hostString << ": min/max original (filtered) peak count: " <<
										opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
								cout << g_hostString << ": original (filtered) peak count at 1st/2nd/3rd quartiles: " <<
										opcs[2] << " (" << fpcs[2] << "), " <<
										opcs[3] << " (" << fpcs[3] << "), " <<
										opcs[4] << " (" << fpcs[4] << ")" << endl;

								float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
								cout << g_hostString << " filtered out " << filter * 100.0f << "% of peaks." << endl;

								cout << g_hostString << " has " << spectra.size() << " spectra prepared now; " << prepareTime.End() << " seconds elapsed." << endl;
								cout << g_hostString << " is reading tags for " << spectra.size() << " prepared spectra." << endl;
								size_t totalTags = 0;
								// Read the tags in the input file
								spectra.readTags( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum, false);
								// For each spectrum in the input file
								for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
								{
									// Get number of tags in the spectrum
									(*sItr)->tagList.max_size( g_rtConfig->MaxTagCount );
									// For each tag
									for( TagList::iterator tItr = (*sItr)->tagList.begin(); tItr != (*sItr)->tagList.end(); ++tItr ) {
										// Replace the I/L and generate new tags
										(*sItr)->tagList.tagExploder( *tItr );
									}
									totalTags += (*sItr)->tagList.size();
								}
								cout << g_hostString << " finished reading " << totalTags << " tags." << endl;

								cout << g_hostString << " is trimming spectra with no tags." << endl;
								int noTagsCount = spectra.trimByTagCount();
								cout << g_hostString << " trimmed " << noTagsCount << " spectra." << endl;
								// Initialize few global data structures. See function documentation
								// for details
								InitWorkerGlobals();
								if(g_rtConfig->EstimateSearchTimeOnly )
								{
									cout << g_hostString << " is estimating the count of sequence comparisons to be done." << endl;
									int estimatedComparisonCount = ExecuteCount();
									cout << g_hostString << " will make an estimated " << estimatedComparisonCount << " sequence comparisons." << endl;
								}

								// If it is worth while to send the spectra for a search
								if( !g_rtConfig->EstimateSearchTimeOnly )
								{
									SpectraList finishedSpectra;
									//do
									{
										cout << g_hostString << " is sending some prepared spectra to all worker nodes from a pool of " << spectra.size() << " spectra." << endl;
										Timer sendTime(true);
										// Send spectra to the child processes
										numSpectra = TransmitSpectraToChildProcesses();
										cout << g_hostString << " is finished sending " << numSpectra << " prepared spectra to all worker nodes; " <<
												sendTime.End() << " seconds elapsed." << endl;

										cout << g_hostString << " is commencing database search on " << numSpectra << " spectra." << endl;
										startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
										// Send proteins to the child processes
										TransmitProteinsToChildProcesses();
										cout << g_hostString << " has finished database search; " << searchTime.End() << " seconds elapsed." << endl;

										cout << g_hostString << " is receiving search results for " << numSpectra << " spectra." << endl;
										Timer receiveTime(true);
										// Get the results
										ReceiveResultsFromChildProcesses(sumSearchStats);
										cout << g_hostString << " is finished receiving search results; " << receiveTime.End() << " seconds elapsed." << endl;
										
										cout << g_hostString << " overall stats: " << (string) sumSearchStats << endl;

										//SpectraList::iterator lastSpectrumItr = spectra.begin();
										//advance_to_bound( lastSpectrumItr, spectra.end(), numSpectra );
										//finishedSpectra.insert( spectra.begin(), spectra.end(), finishedSpectra.end() );
										//spectra.erase( spectra.begin(), spectra.end(), false );
									}// while( spectra.size() > 0 );

									//finishedSpectra.clear();
								} else
									skip = 1;

								DestroyWorkerGlobals();
							}

						#endif
					} else
					{
						// If total number of process is <=1 then we are executing in non-cluster mode
						cout << g_hostString << " is preparing " << spectra.size() << " spectra." << endl;
						Timer prepareTime(true);
						// Preprocess the spectra
						PrepareSpectra();
						cout << g_hostString << " is finished preparing spectra; " << prepareTime.End() << " seconds elapsed." << endl;

						// Get number of spectra that survived the preprocessing ;-)
						numSpectra = (int) spectra.size();

						skip = 0;
						if( spectra.empty() )
						{
							cout << g_hostString << " is skipping a file with no suitable spectra." << endl;
							skip = 1;
						}

						if( !skip )
						{
							// If the data file has some spectra and if the process is being
							// run on a single node then perform the search

							// Some stats for the user!!
							opcs = spectra.getOriginalPeakCountStatistics();
							fpcs = spectra.getFilteredPeakCountStatistics();
							cout << g_hostString << ": mean original (filtered) peak count: " <<
									opcs[5] << " (" << fpcs[5] << ")" << endl;
							cout << g_hostString << ": min/max original (filtered) peak count: " <<
									opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
							cout << g_hostString << ": original (filtered) peak count at 1st/2nd/3rd quartiles: " <<
									opcs[2] << " (" << fpcs[2] << "), " <<
									opcs[3] << " (" << fpcs[3] << "), " <<
									opcs[4] << " (" << fpcs[4] << ")" << endl;

							float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
							cout << g_hostString << " filtered out " << filter * 100.0f << "% of peaks." << endl;

							cout << g_hostString << " is reading tags for " << spectra.size() << " prepared spectra." << endl;
							size_t totalTags = 0;
							// Read the tags from the input file
                            spectra.readTags( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum, false );
							// For each spectrum
							for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
							{
								// Get the number of tags and generate new tags for tags containing I/L
								(*sItr)->tagList.max_size( g_rtConfig->MaxTagCount );
								for( TagList::iterator tItr = (*sItr)->tagList.begin(); tItr != (*sItr)->tagList.end(); ++tItr )
									(*sItr)->tagList.tagExploder( *tItr );
								totalTags += (*sItr)->tagList.size();
							}
							cout << g_hostString << " finished reading " << totalTags << " tags." << endl;

							cout << g_hostString << " is trimming spectra with no tags." << endl;
							// Delete spectra that has no tags
							int noTagsCount = spectra.trimByTagCount();
							cout << g_hostString << " trimmed " << noTagsCount << " spectra." << endl;
							
							/*for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr ) {
								Spectrum * tempSpectrum = (*sItr);
								float mzOfPrec = (tempSpectrum->mOfPrecursor+tempSpectrum->id.charge*PROTON)/(float)tempSpectrum->id.charge;
								cout << tempSpectrum->id << "\t" << tempSpectrum->nativeID << "\t" << tempSpectrum->mOfPrecursor \
									 << "\t" << tempSpectrum->tagList.size() << "\t" << tempSpectrum->peakCount \
									 << "\t" << tempSpectrum->peakPreCount << "\t" << mzOfPrec << endl;
								for(TagList::iterator tagsIterator = tempSpectrum->tagList.begin(); \
									tagsIterator != tempSpectrum->tagList.end(); ++tagsIterator) {
										cout << (*tagsIterator).tag << " ";
								}
								cout << endl;
							}
							exit(1);*/

							// Initialize few global data structures. See function documentation
							// for details
							InitWorkerGlobals();

							cout << g_hostString << " is commencing database search on " << spectra.size() << " spectra." << endl;
							startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
							// Start the threads
							sumSearchStats = ExecuteSearch();
							cout << g_hostString << " has finished database search; " << searchTime.End() << " seconds elapsed." << endl;
							cout << g_hostString << (string) sumSearchStats << endl;

							// Get rid of the global variables
							DestroyWorkerGlobals();
						}
					}

					if( !skip )
					{
						// Generate an output file for each input file
						WriteOutputToFile( *fItr, startTime, startDate, searchTime.End(), opcs, fpcs, sumSearchStats );
						cout << g_hostString << " finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
					}
				}

				#ifdef USE_MPI
					// Send a message to all processes about the number of files that
					// have been processed
					int done = ( ( g_inputFilenames.size() - finishedFiles.size() ) == 0 ? 1 : 0 );
					for( int p=0; p < g_numChildren; ++p )
						MPI_Ssend( &done,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
				#endif
			}
		}
		#ifdef USE_MPI
			// If the process is a not a root process and number of child processes is
			// greater 0 then this a child process.
			else
			{
				// When executing in cluster mode
				int allDone = 0;

				// Check to make sure all the child processes are done.
				while( !allDone )
				{
					int skip;
					MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

					// If we have spectra in the input file of the root process
					if( !skip )
					{
						// Get unprepared spectra from root process
						SpectraList preparedSpectra;

						while( ReceiveUnpreparedSpectraBatchFromRootProcess() )
						{
							// Preprocess them
							PrepareSpectra();
							preparedSpectra.insert( spectra.begin(), spectra.end(), preparedSpectra.end() );
							spectra.clear( false );
						}
						//cout << "totalSpectraReceived:" << preparedSpectra.size() << endl;

						//for( int i=0; i < (int) preparedSpectra.size(); ++i )
						//	cout << preparedSpectra[i]->id.index << " " << preparedSpectra[i]->peakData.size() << endl;

						// Send them back
						TransmitPreparedSpectraToRootProcess( preparedSpectra );

						preparedSpectra.clear();

						// See if we have to do the search
						MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

						// If the root process tell you to perform the search
						if( !skip )
						{

							int done = 0;
							do
							{
								// Get the processed spectra from the parent.
								done = ReceiveSpectraFromRootProcess();

								// Initialize the global variables
								InitWorkerGlobals();

								int numBatches = 0;
								searchStats sumSearchStats;
								searchStats lastSearchStats;
								// Get a batch of protein sequences from root process
								while( ReceiveProteinBatchFromRootProcess( lastSearchStats.numComparisonsDone ) )
								{
									++ numBatches;

									// Execute the search
									lastSearchStats = ExecuteSearch();
									sumSearchStats = sumSearchStats + lastSearchStats;
									proteins.clear();
								}

								cout << g_hostString << " stats: " << numBatches << " batches; " << (string) sumSearchStats << endl;

								// Send results back to the parent process
								TransmitResultsToRootProcess(sumSearchStats);

								// Clean up the variables.
								DestroyWorkerGlobals();
								spectra.clear();
							} while( !done );
						}
					}

					// See if we are all done. Master process sends this signal when there are
					// no more spectra to search
					MPI_Recv( &allDone,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
				}
			}
		#endif

		return 0;
	}
}
}

int main( int argc, char* argv[] )
{
	//freicore::UniModXMLParser::testUnimodParser(argv[1]);
	//exit(1);
	//tagrecon::BlosumMatrix::testBlosumMatrix(argv[1]);
	//exit(1);
	char buf[256];
	GetHostname( buf, sizeof(buf) );

	// Initialize the message passing interface for the parallel processing system
	#ifdef MPI_DEBUG
		cout << buf << " is initializing MPI... " << endl;
	#endif

	#ifdef USE_MPI
		int threadLevel;
		MPI_Init_thread( &argc, &argv, MPI_THREAD_MULTIPLE, &threadLevel );
		if( threadLevel < MPI_THREAD_SINGLE )
		{
			cerr << "MPI library is not thread compliant: " << threadLevel << " should be " << MPI_THREAD_MULTIPLE << endl;
			return 1;
		}
		MPI_Buffer_attach( malloc( MPI_BUFFER_SIZE ), MPI_BUFFER_SIZE );
		//CommitCommonDatatypes();
	#endif

	#ifdef MPI_DEBUG
		cout << buf << " has initialized MPI... " << endl;
	#endif

	// Get information on the MPI environment
	#ifdef MPI_DEBUG
		cout << buf << " is gathering MPI information... " << endl;
	#endif

	// Get the number of total process and the rank the parent process
	#ifdef USE_MPI
		MPI_Comm_size( MPI_COMM_WORLD, &g_numProcesses );
		MPI_Comm_rank( MPI_COMM_WORLD, &g_pid );
	#else
		g_numProcesses = 1;
		g_pid = 0;
	#endif

	g_numChildren = g_numProcesses - 1;

	ostringstream str;
	str << "Process #" << g_pid << " (" << buf << ")";
	g_hostString = str.str();

	#ifdef MPI_DEBUG
		cout << g_hostString << " has gathered its MPI information." << endl;
	#endif


	// Process the data
	#ifndef MPI_DEBUG
		cout << g_hostString << " is starting." << endl;
	#endif

	int result = 0;
	try
	{
		result = tagrecon::ProcessHandler( argc, argv );
	} catch( exception& e )
	{
		cerr << e.what() << endl;
		result = 1;
	} catch( ... )
	{
		cerr << "Caught unspecified fatal exception." << endl;
		result = 1;
	}

	#ifdef USE_MPI
		if( g_pid == 0 && g_numChildren > 0 && result > 0 )
			MPI_Abort( MPI_COMM_WORLD, 1 );
	#endif

	#ifdef MPI_DEBUG
		cout << g_hostString << " has finished." << endl;	
	#endif

	// Destroy the message passing interface
	#ifdef MPI_DEBUG
		cout << g_hostString << " is finalizing MPI... " << endl;
	#endif

	#ifdef USE_MPI
		int size;
		MPI_Buffer_detach( &g_mpiBuffer, &size );
		free( g_mpiBuffer );
		MPI_Finalize();
	#endif

	#ifdef MPI_DEBUG
		cout << g_hostString << " is terminating." << endl;
	#endif

	return result;
}
