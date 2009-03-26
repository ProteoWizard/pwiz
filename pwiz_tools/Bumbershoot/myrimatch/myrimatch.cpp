// myrimatch.cpp : Defines the entry point for the console application.
//

#include "stdafx.h"
#include "myrimatch.h"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/utility/proteome/Version.hpp"

namespace freicore
{
namespace myrimatch
{
	WorkerThreadMap					g_workerThreads;
	simplethread_mutex_t			resourceMutex;

	vector< double>					relativePeakCount;
	vector< simplethread_mutex_t >	spectraMutexes;

	proteinStore					proteins;
	SpectraList						spectra;
	SpectraMassMapList				spectraMassMapsByChargeState;
	float							totalSequenceComparisons;

	RunTimeConfig*					g_rtConfig;

	int InitProcess( argList_t& args )
	{
		//cout << g_hostString << " is initializing." << endl;
		if( g_pid == 0 )
		{
			cout << "MyriMatch " << MYRIMATCH_VERSION_STRING << " (" << MYRIMATCH_BUILD_DATE << ")\n" <<
                    "ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
                    "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
					MYRIMATCH_LICENSE << endl;
		}

		g_rtConfig = new RunTimeConfig;
		g_rtSharedConfig = (BaseRunTimeConfig*) g_rtConfig;
		g_residueMap = new ResidueMap;
		g_endianType = GetHostEndianType();
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
				g_numWorkers = atoi( args[i+1].c_str() );
				args.erase( args.begin() + i );
			} else
				continue;

			args.erase( args.begin() + i );
			--i;
		}

		if( g_pid == 0 )
		{
			for( size_t i=1; i < args.size(); ++i )
			{
				if( args[i] == "-cfg" && i+1 <= args.size() )
				{
					if( g_rtConfig->initializeFromFile( args[i+1] ) )
					{
						cerr << g_hostString << " could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
						return 1;
					}
					args.erase( args.begin() + i );

				} else if( args[i] == "-rescfg" && i+1 <= args.size() )
				{
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

			if( args.size() < 2 )
			{
				cerr << "Not enough arguments.\nUsage: " << args[0] << " [-ProteinDatabase <FASTA protein database filepath>] <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
				return 1;
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

		try
		{
			g_rtConfig->setVariables( vars );
        } catch( std::exception& e )
		{
			if( g_pid == 0 ) cerr << g_hostString << " had an error while overriding runtime variables: " << e.what() << endl;
			return 1;
		}

        if( g_rtConfig->ProteinDatabase.empty() )
		{
			cerr << "No FASTA protein database specified.\nUsage: " << args[0] << " [-ProteinDatabase <FASTA protein database filepath>] <input spectra filemask 1> [input spectra filemask 2] ..." << endl;
			return 1;
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

	int InitWorkerGlobals()
	{
		spectra.sort( spectraSortByID() );

		if( spectra.empty() )
			return 0;

		//size_t numSpectra = spectra.size();

		// Create a map of precursor masses to the spectrum indices
		spectraMassMapsByChargeState.resize( g_rtConfig->NumChargeStates );
		for( int z=0; z < g_rtConfig->NumChargeStates; ++z )
		{
			for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
			{
				Spectrum* s = *sItr;

				if( s->id.charge-1 != z )
					continue;

				for( size_t i=0; i < s->mOfPrecursorList.size(); ++i )
					spectraMassMapsByChargeState[z].insert( make_pair( s->mOfPrecursorList[i], sItr ) );
					//spectraMassMapsByChargeState[z].insert( pair< float, SpectraList::iterator >( s->mOfPrecursor, sItr ) );
				//cout << i << " " << (*sItr)->mOfPrecursor << endl;
			}
		}

		g_rtConfig->curMinSequenceMass = spectra.front()->mOfPrecursor;
		g_rtConfig->curMaxSequenceMass = 0;

		// find the smallest and largest precursor masses
		size_t maxPeakBins = (size_t) spectra.front()->totalPeakSpace;
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			if( (*sItr)->mOfPrecursor < g_rtConfig->curMinSequenceMass )
				g_rtConfig->curMinSequenceMass = (*sItr)->mOfPrecursor;

			if( (*sItr)->mOfPrecursor > g_rtConfig->curMaxSequenceMass )
				g_rtConfig->curMaxSequenceMass = (*sItr)->mOfPrecursor;

			size_t totalPeakBins = (size_t) round( (*sItr)->totalPeakSpace / ( g_rtConfig->FragmentMzTolerance * 2.0 ) );
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
        g_rtConfig->digestionConfig = Digestion::Config( g_rtConfig->NumMaxMissedCleavages,
                                                         curMinCandidateLength,
                                                         curMaxCandidateLength,
                                                         specificity );

		//cout << g_hostString << " is precaching factorials up to " << (int) maxPeakSpace << "." << endl;
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

		return 0;
	}

	void DestroyWorkerGlobals()
	{
	}

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

		map< int, Histogram<double> > meanScoreHistogramsByChargeState;
		for( int z=1; z <= g_rtConfig->NumChargeStates; ++z )
			meanScoreHistogramsByChargeState[ z ] = Histogram<double>( g_rtConfig->NumScoreHistogramBins, g_rtConfig->MaxScoreHistogramValues );

		if( g_rtConfig->CalculateRelativeScores )
		{
			Timer calculationTime(true);
			cout << g_hostString << " is calculating relative scores for " << spectra.size() << " spectra." << endl;
			float lastUpdateTime = 0;
			size_t n = 0;
			for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr, ++n )
			{
				Spectrum* s = (*sItr);

				try
				{
					s->CalculateRelativeScores();
				} catch( std::exception& e )
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

		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
		{
			++ numSpectra;
			Spectrum* s = (*sItr);

			spectra.setId( s->id, SpectrumId( filenameAsScanName, s->id.index, s->id.charge ) );

		//#ifdef DELTA_SCORES
			//s->computeSecondaryScores();
		//#endif
			s->resultSet.calculateRanks();
			s->resultSet.convertProteinIndexesToNames( proteins.indexToName );

			if( g_rtConfig->MakeScoreHistograms )
			{
				s->scoreHistogram.smooth();
				for( map<double,int>::iterator itr = s->scores.begin(); itr != s->scores.end(); ++itr )
					cout << itr->first << "\t" << itr->second << "\n";
				//cout << std::keys( s->scores ) << endl;
				//cout << std::values( s->scores ) << endl;
				//cout << std::keys( s->scoreHistogram.m_bins ) << endl;
				//cout << std::values( s->scoreHistogram.m_bins ) << endl;
				//s->scoreHistogram.writeToSvgFile( string( s->id ) + "-histogram.svg", "MVH score", "Density", 800, 600 );
				meanScoreHistogramsByChargeState[ s->id.charge ] += s->scoreHistogram;
			}

			for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
			{
				++ numMatches;
				numLoci += itr->lociByName.size();

				string theSequence = itr->sequence();

				if( itr->rank == 1 && g_rtConfig->MakeSpectrumGraphs )
				{
					vector< double > ionMasses;
					vector< string > ionNames;
					CalculateSequenceIons( Peptide(theSequence), s->id.charge, &ionMasses, s->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, &ionNames, 0 );
					map< double, string > ionLabels;
					map< double, string > ionColors;
					map< double, int > ionWidths;

					for( PeakPreData::iterator itr = s->peakPreData.begin(); itr != s->peakPreData.end(); ++itr )
						ionWidths[ itr->first ] = 1;
                    cout << ionMasses << endl << ionNames << endl;
					for( size_t i=0; i < ionMasses.size(); ++i )
					{
						PeakPreData::iterator itr = s->peakPreData.findNear( ionMasses[i], g_rtConfig->FragmentMzTolerance );
						if( itr != s->peakPreData.end() )
						{
							ionLabels[ itr->first ] = ionNames[i];
							ionColors[ itr->first ] = ( ionNames[i].find( "b" ) == 0 ? "red" : "blue" );
							ionWidths[ itr->first ] = 2;
						}
					}

					cout << theSequence << " fragment ions: " << ionLabels << endl;

					s->writeToSvgFile( string( "-" ) + theSequence + g_rtConfig->OutputSuffix, &ionLabels, &ionColors, &ionWidths );
				}


			}
		}

		if( g_rtConfig->MakeScoreHistograms )
			for( int z=1; z <= g_rtConfig->NumChargeStates; ++z )
				meanScoreHistogramsByChargeState[ z ].writeToSvgFile( filenameAsScanName + g_rtConfig->OutputSuffix + "_+" + lexical_cast<string>(z) + "_histogram.svg", "MVH score", "Density", g_rtConfig->ScoreHistogramWidth, g_rtConfig->ScoreHistogramHeight );

		RunTimeVariableMap vars = g_rtConfig->getVariables();
		RunTimeVariableMap fileParams;
		for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
			fileParams[ string("Config: ") + itr->first ] = itr->second;
		fileParams["SearchEngine: Name"] = "MyriMatch";
		fileParams["SearchEngine: Version"] = MYRIMATCH_VERSION_STRING;
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

		string outputFilename = filenameAsScanName + g_rtConfig->OutputSuffix + ".pepXML";
		cout << g_hostString << " is writing search results to file \"" << outputFilename << "\"." << endl;

		spectra.writePepXml( dataFilename, g_rtConfig->OutputSuffix, "MyriMatch", g_dbPath + g_dbFilename, &proteins, fileParams );

		if( g_rtConfig->DeisotopingMode == 3 /*&& g_rtConfig->DeisotopingTestMode != 0*/ )
		{
			spectra.calculateFDRs( g_rtConfig->NumChargeStates, 1.0, "rev_" );
			SpectraList passingSpectra;
			spectra.filterByFDR( 0.05, &passingSpectra );
			//g_rtConfig->DeisotopingMode = g_rtConfig->DeisotopingTestMode;

			ofstream deisotopingDetails( (filenameAsScanName+g_rtConfig->OutputSuffix+"-deisotope-test.tsv").c_str() );
			deisotopingDetails << "Scan\tCharge\tSequence\tPredicted\tMatchesBefore\tMatchesAfter\n";
			for( SpectraList::iterator sItr = passingSpectra.begin(); sItr != passingSpectra.end(); ++sItr )
			{
				Spectrum* s = (*sItr);
				s->Deisotope( g_rtConfig->IsotopeMzTolerance );

				s->resultSet.calculateRanks();
				for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
				{
					string theSequence = itr->sequence();

					if( itr->rank == 1 )
					{
						vector< double > ionMasses;
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

		if( g_rtConfig->AdjustPrecursorMass == 1 )
		{
			spectra.calculateFDRs( g_rtConfig->NumChargeStates, 1.0, "rev_" );
			SpectraList passingSpectra;
			spectra.filterByFDR( 0.05, &passingSpectra );

			ofstream adjustmentDetails( (filenameAsScanName+g_rtConfig->OutputSuffix+"-adjustment-test.tsv").c_str() );
			adjustmentDetails << "Scan\tCharge\tUnadjustedSequenceMass\tAdjustedSequenceMass\tUnadjustedPrecursorMass\tAdjustedPrecursorMass\tUnadjustedError\tAdjustedError\tSequence\n";
			for( SpectraList::iterator sItr = passingSpectra.begin(); sItr != passingSpectra.end(); ++sItr )
			{
				Spectrum* s = (*sItr);

				s->resultSet.calculateRanks();
				for( Spectrum::SearchResultSetType::reverse_iterator itr = s->resultSet.rbegin(); itr != s->resultSet.rend(); ++itr )
				{
					if( itr->rank == 1 )
					{
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

	void PrepareSpectra()
    {
		int numSpectra = (int) spectra.size();

		Timer timer;

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " is trimming spectra with less than " << 10 << " peaks." << endl;
		}

		int preTrimCount = spectra.filterByPeakCount( 10 );
		numSpectra = (int) spectra.size();

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " trimmed " << preTrimCount << " spectra for being too sparse." << endl;
			cout << g_hostString << " is determining charge states for " << numSpectra << " spectra." << endl;
		}

		timer.Begin();
		SpectraList duplicates;
		for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
        {
			try
			{
				if( !g_rtConfig->UseChargeStateFromMS )
						spectra.setId( (*sItr)->id, SpectrumId( (*sItr)->id.index, 0 ) );

				if( (*sItr)->id.charge == 0 )
				{
					SpectrumId preChargeId( (*sItr)->id );
					(*sItr)->DetermineSpectrumChargeState();
					SpectrumId postChargeId( (*sItr)->id );

					if( postChargeId.charge == 0 )
					{
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

			} catch( std::exception& e )
			{
				throw runtime_error( string( "duplicating scan " ) + string( (*sItr)->id ) + ": " + e.what() );
			} catch( ... )
			{
				throw runtime_error( string( "duplicating scan " ) + string( (*sItr)->id ) );
			}
		}

		try
		{
			spectra.insert( duplicates.begin(), duplicates.end(), spectra.end() );
			duplicates.clear(false);
		} catch( std::exception& e )
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
				(*sItr)->Preprocess();
			} catch( std::exception& e )
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
			cout << g_hostString << " finished preprocessing its spectra; " << timer.End() << " seconds elapsed." << endl;
			cout << g_hostString << " is trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
			cout << g_hostString << " is trimming spectra with precursors too small or large: " <<
					g_rtConfig->MinSequenceMass << " - " << g_rtConfig->MaxSequenceMass << endl;
		}

		int postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );

		if( g_numChildren == 0 )
		{
			cout << g_hostString << " trimmed " << postTrimCount << " spectra." << endl;
		}

	}

	vector< int > workerNumbers;

	/*void DigestProteinSequence( const proteinData& protein, CandidateSequenceList& candidates )
	{
		float minDynamicModMass = min( g_residueMap->smallestDynamicModMass(), 0.0f ) * g_rtConfig->MaxDynamicMods;
		float maxDynamicModMass = max( g_residueMap->largestDynamicModMass(), 0.0f ) * g_rtConfig->MaxDynamicMods;

		const string& p = protein.getSequence();

		for( int nIndex = 0; nIndex < (int) p.length(); ++nIndex )
		{
			int nTerminusCanCleave = 0;
			const char& n_r = p[ nIndex ];
			if( !g_residueMap->hasResidue( n_r ) )
				continue;

			int numTotalCleavages = 0;

			CleavageRuleSet::const_iterator nTermCleavageItr = protein.testCleavage( g_rtConfig->_CleavageRules, nIndex );
			if( nTermCleavageItr != g_rtConfig->_CleavageRules.end() )
			{
				++numTotalCleavages;
				nTerminusCanCleave = 1;
			}

			if( numTotalCleavages < g_rtConfig->NumMinTerminiCleavages-1 )
				continue;

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

				int numTerminiCleavages = nTerminusCanCleave + cTerminusCanCleave;

				if( numTerminiCleavages < g_rtConfig->NumMinTerminiCleavages )
					continue;

				int numMissedCleavages = numTotalCleavages - numTerminiCleavages;

				if( g_rtConfig->NumMaxMissedCleavages != -1 && numMissedCleavages > g_rtConfig->NumMaxMissedCleavages )
					break;

				string aSequence = PEPTIDE_N_TERMINUS_SYMBOL + p.substr( nIndex, cIndex - nIndex + 1 ) + PEPTIDE_C_TERMINUS_SYMBOL;
				float aSequenceMass = g_residueMap->GetMassOfResidues( aSequence, g_rtConfig->UseAvgMassOfSequences );

				if( aSequenceMass + maxDynamicModMass < g_rtConfig->curMinSequenceMass )
					continue;

				if( aSequenceMass + minDynamicModMass > g_rtConfig->curMaxSequenceMass )
					break;

				// All tests passed, we have a genuine bonafide sequence candidate!
				CandidateSequenceInfo candidate( aSequence, aSequenceMass, nIndex, numTerminiCleavages, numMissedCleavages );
				//cout << p[max(0,nIndex-1)] << '.' << aSequence << '.' << p[min(p.length()-1,cIndex+1)] << " " << numTerminiCleavages << " " << numMissedCleavages << endl;
				//MakePtmVariants( candidate, candidates, g_rtConfig->MaxDynamicMods );
			}
		}
	}*/

	boost::int64_t QuerySequence( const DigestedPeptide& candidate, int idx, bool estimateComparisonsOnly = false )
	{
		boost::int64_t numComparisonsDone = 0;
		vector< vector< double > > fragmentIonsByChargeState( g_rtConfig->NumChargeStates );
		Spectrum* spectrum;
		SearchResult result(candidate);
        string sequence = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
        double mass = g_rtConfig->UseAvgMassOfSequences ? candidate.molecularWeight()
                                                        : candidate.monoisotopicMass();

		int maxFragmentChargeState = ( g_rtConfig->MaxFragmentChargeState > 0 ? g_rtConfig->MaxFragmentChargeState+1 : g_rtConfig->NumChargeStates );
		for( int z = 0; z < g_rtConfig->NumChargeStates; ++z )
		{
			int fragmentChargeState = min( z, maxFragmentChargeState-1 );
			vector<double>& sequenceIons = fragmentIonsByChargeState[fragmentChargeState];

			SpectraMassMap::iterator cur, end = spectraMassMapsByChargeState[z].upper_bound( mass + g_rtConfig->PrecursorMassTolerance[z] );
			for( cur = spectraMassMapsByChargeState[z].lower_bound( mass - g_rtConfig->PrecursorMassTolerance[z] ); cur != end; ++cur )
			{
				spectrum = *cur->second;
				//if( spectrum->mOfPrecursor != cur->first )
				//	cout << spectrum->id.index << " searching adjusted mass: " << spectrum->mOfPrecursor << " " << cur->first << endl;
				//if( seq == "AGLLGLLEEMR" ) cout << id.index << " " << i << " -> " << cur->first << endl;

				if( !estimateComparisonsOnly )
				{
					START_PROFILER(2);
					if( sequenceIons.empty() )
                    {
						CalculateSequenceIons( candidate,
                                               fragmentChargeState+1,
                                               &sequenceIons,
                                               spectrum->fragmentTypes,
                                               g_rtConfig->UseSmartPlusThreeModel,
                                               0,
                                               0 );
                    }
					STOP_PROFILER(2);
					START_PROFILER(3);
					spectrum->ScoreSequenceVsSpectrum( result, sequence, sequenceIons );
					STOP_PROFILER(3);

                    //cout << (spectrum->id.index+1) << "." << spectrum->id.charge << ":" << result.sequence() << ":" << result << endl;
					if( result.mvh >= g_rtConfig->MinResultScore )
					{
						START_PROFILER(5);
                        result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );	
						STOP_PROFILER(5);
					}
				}

				++ numComparisonsDone;

				if( estimateComparisonsOnly )
					continue;

				//if( spectrum->id.index == 4 && spectrum->id.charge == 2 ) cout << spectrum->id << " -> " << result << endl;

				START_PROFILER(4);
				if( g_rtConfig->UseMultipleProcessors )
					simplethread_lock_mutex( &spectrum->mutex );

                if( proteins[idx].isDecoy() )
				    ++ spectrum->numDecoyComparisons;
                else
                    ++ spectrum->numTargetComparisons;

				if( result.mvh >= g_rtConfig->MinResultScore && accumulate( result.key.begin(), result.key.end(), 0 ) > 0 )
				{
					result.massError = mass - cur->first;

					if( g_rtConfig->MakeScoreHistograms )
					{
						++ spectrum->scores[ result.mvh ];
						spectrum->scoreHistogram.add( result.mvh );
					}
					//#ifdef DELTA_SCORES
					// Accumulate score distributions for the spectrum
					//++ spectrum->mvhScoreDistribution[ (int) (result.mvh+0.5) ];
					//++ spectrum->mzFidelityDistribution[ (int) (result.mzFidelity+0.5)];
					//#endif
					spectrum->resultSet.add( result );
				}
				if( g_rtConfig->UseMultipleProcessors )
					simplethread_unlock_mutex( &spectrum->mutex );
				STOP_PROFILER(4);
			}
		}

		return numComparisonsDone;
	}

	simplethread_return_t ExecuteSearchThread( simplethread_arg_t threadArg )
	{
		simplethread_lock_mutex( &resourceMutex );
		simplethread_id_t threadId = simplethread_get_id();
		WorkerThreadMap* threadMap = (WorkerThreadMap*) threadArg;
		WorkerInfo* threadInfo = reinterpret_cast< WorkerInfo* >( threadMap->find( threadId )->second );
		int numThreads = (int) threadMap->size();
		simplethread_unlock_mutex( &resourceMutex );

		bool done;
		//threadInfo->spectraResults.resize( (int) spectra.size() );

        double largestDynamicModMass = g_rtConfig->dynamicMods.empty() ? 0 : g_rtConfig->dynamicMods.rbegin()->modMass * g_rtConfig->MaxDynamicMods;
		double smallestDynamicModMass = g_rtConfig->dynamicMods.empty() ? 0 : g_rtConfig->dynamicMods.begin()->modMass * g_rtConfig->MaxDynamicMods;

        try
        {
		    Timer searchTime;
		    float totalSearchTime = 0;
		    float lastUpdate = 0;
		    searchTime.Begin();
		    while( true )
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

			    int numProteins = (int) proteins.size();
			    threadInfo->endIndex = ( numProteins / g_numWorkers )-1;

			    //simplethread_lock_mutex( &resourceMutex );
			    //cout << threadInfo->workerHostString << " " << numProteins << " " << g_numWorkers << " " <<  threadInfo->workerNum << endl;
			    //simplethread_unlock_mutex( &resourceMutex );

			    /*ifstream inFile( "candidates0.txt", ios::binary );
			    char* buf = new char[1024*1024];
			    while( !inFile.eof() )
			    {
				    inFile.getline( buf, 1024*1024 );
				    string sbuf( buf );
				    stringstream instances( sbuf.substr( sbuf.find_first_of(' ')+1 ) );
				    int start, i;
				    string& aSequence = sbuf.substr( 0, sbuf.find_first_of(' ') );
				    instances >> i >> start;
				    cout << "Querying sequence " << n+1 << ": \"" << aSequence << "\"\t\t\t\t\r" << flush;
				    QuerySequence( aSequence, i, start, threadInfo->spectraResults );
				    ++n;
			    }
			    delete [] buf;
			    continue;*/

			    int i;
                vector<DigestedPeptide> digestedPeptides;
			    for( i = threadInfo->workerNum; i < numProteins; i += g_numWorkers )
			    {
				    //cout << threadInfo->workerHostString <<	" is generating candidates from protein " << i << endl;
				    ++ threadInfo->stats.numProteinsDigested;

                    //digestedPeptides.clear();

				    START_PROFILER(0);
                    START_PROFILER(9);
                    Peptide protein(proteins[i].getSequence());
                    STOP_PROFILER(9);
                    START_PROFILER(10);
                    Digestion digestion( protein, g_rtConfig->digestionMotifs, g_rtConfig->digestionConfig );
                    STOP_PROFILER(10);
                    for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); )
                    {
                        
						double mass = g_rtConfig->UseAvgMassOfSequences ? itr->molecularWeight()
                                                                        : itr->monoisotopicMass();
                        if( mass /* + smallestDynamicModMass*/ > g_rtConfig->curMaxSequenceMass ||
                            mass /*+ largestDynamicModMass*/ < g_rtConfig->curMinSequenceMass ) {
                            START_PROFILER(11);
                            ++itr;
                            STOP_PROFILER(11);
                            continue;
                        }
                        digestedPeptides.clear();
                        START_PROFILER(12);
						
                        //MakePtmVariantsOld( *itr, digestedPeptides, g_rtConfig->MaxDynamicMods, g_rtConfig->dynamicMods, g_rtConfig->staticMods);
                        if(MakePtmVariants( *itr, digestedPeptides, g_rtConfig->MaxDynamicMods, g_rtConfig->dynamicMods, g_rtConfig->staticMods, g_rtConfig->MaxNumPeptideVariants )) {
                            ++ threadInfo->stats.numCandidatesSkipped;
                        }
                        STOP_PROFILER(12);
                        threadInfo->stats.numCandidatesGenerated += digestedPeptides.size();
                        
                        for( size_t j=0; j < digestedPeptides.size(); ++j )
                        {
                            //++ threadInfo->stats.numCandidatesGenerated;
                            START_PROFILER(1);
                            boost::int64_t queryComparisonCount = QuerySequence( digestedPeptides[j], i );
                            STOP_PROFILER(1);
                            if( queryComparisonCount > 0 )
                            {
                                threadInfo->stats.numComparisonsDone += queryComparisonCount;
                                ++threadInfo->stats.numCandidatesQueried;
                                //cout << "QC>0" << queryComparisonCount << endl;
                            }
                        }
                        START_PROFILER(11);
                        ++itr;
                        STOP_PROFILER(11);

                    }
				    STOP_PROFILER(0);

				   
				    if( g_numChildren == 0 )
					    totalSearchTime = searchTime.TimeElapsed();
				    //if( g_pid == 1 && !(i%50) )
				    //	cout << i << ": " << (int) sequenceToSpectraMap.size() << " " << (int) sequenceProteinLoci.size() << endl;

				    if( g_numChildren == 0 && ( ( totalSearchTime - lastUpdate > g_rtConfig->StatusUpdateFrequency ) || i == numProteins ) )
				    {
					    float proteinsPerSec = float( threadInfo->stats.numProteinsDigested ) / totalSearchTime;
					    float estimatedTimeRemaining = float( ( numProteins / numThreads ) - threadInfo->stats.numProteinsDigested ) / proteinsPerSec;

					    simplethread_lock_mutex( &resourceMutex );
					    cout << threadInfo->workerHostString << " has searched " << threadInfo->stats.numProteinsDigested << " of " << numProteins <<
							    " proteins; " << round(proteinsPerSec, 1) << " per second, " << round(totalSearchTime, 0) << " elapsed, " << round(estimatedTimeRemaining, 0) << " remaining." << endl;

					    PRINT_PROFILERS(cout, threadInfo->workerHostString + " profiling")

					    //float candidatesPerSec = threadInfo->stats.numComparisonsDone / totalSearchTime;
					    //float estimatedTimeRemaining = float( numCandidates - threadInfo->stats.numComparisonsDone ) / candidatesPerSec / numThreads;
					    //cout << threadInfo->workerHostString << " has made " << threadInfo->stats.numComparisonsDone << " of about " << numCandidates << " comparisons; " <<
					    //		candidatesPerSec << " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
					    simplethread_unlock_mutex( &resourceMutex );

					    lastUpdate = totalSearchTime;
				    }
			    }
		    }
        } catch( std::exception& e )
        {
            cerr << threadInfo->workerHostString << " terminated with an error: " << e.what() << endl;
        } catch(...)
        {
            cerr << threadInfo->workerHostString << " terminated with an unknown error." << endl;
        }
		//i -= g_numWorkers;
		//cout << threadInfo->workerHostString << " last searched protein " << i-1 << " (" << proteins[i].name << ")." << endl;

		return 0;
	}

	searchStats ExecuteSearch()
	{
		WorkerThreadMap workerThreads;

		int numProcessors = g_numWorkers;
		workerNumbers.clear();

		if( /*!g_singleScanMode &&*/ g_rtConfig->UseMultipleProcessors && g_numWorkers > 1 )
		{
			g_numWorkers *= g_rtConfig->ThreadCountMultiplier;

			simplethread_handle_array_t workerHandles;

			for( int i=0; i < g_numWorkers; ++i )
				workerNumbers.push_back(i);

			simplethread_lock_mutex( &resourceMutex );
			for( int t = 0; t < numProcessors; ++t )
			{
				simplethread_id_t threadId;
				simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &ExecuteSearchThread, &workerThreads );
				workerThreads[ threadId ] = new WorkerInfo( t, 0, 0 );
				workerHandles.array.push_back( threadHandle );
			}
			simplethread_unlock_mutex( &resourceMutex );

			simplethread_join_all( &workerHandles );

			g_numWorkers = numProcessors;
			//cout << g_hostString << " searched " << numSearched << " proteins." << endl;
		} else
		{
			g_numWorkers = 1;
			workerNumbers.push_back(0);
			simplethread_id_t threadId = simplethread_get_id();
			workerThreads[ threadId ] = new WorkerInfo( 0, 0, 0 );
			ExecuteSearchThread( &workerThreads );
			//cout << g_hostString << " searched " << numSearched << " proteins." << endl;
		}

		searchStats stats;

		for( WorkerThreadMap::iterator itr = workerThreads.begin(); itr != workerThreads.end(); ++itr )
		{
			stats = stats + reinterpret_cast< WorkerInfo* >( itr->second )->stats;
			delete itr->second;
		}

		return stats;
	}

	int ProcessHandler( int argc, char* argv[] )
	{
		simplethread_create_mutex( &resourceMutex );

		vector< string > args;
		for( int i=0; i < argc; ++i )
			args.push_back( argv[i] );

		if( InitProcess( args ) )
			return 1;

		g_dbFilename = g_rtConfig->ProteinDatabase;
		int numSpectra = 0;

		INIT_PROFILERS(13)

		if( g_pid == 0 )
		{
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

			if( !TestFileType( g_dbFilename, "fasta" ) )
				return 1;

			cout << g_hostString << " is reading \"" << g_dbFilename << "\"" << endl;
			Timer readTime(true);
			try
			{
				proteins.readFASTA( g_dbFilename, g_rtConfig->StartProteinIndex, g_rtConfig->EndProteinIndex );
			} catch( std::exception& e )
			{
				cout << g_hostString << " had an error: " << e.what() << endl;
				return 1;
			}
			cout << g_hostString << " read " << proteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl;

			proteins.random_shuffle(); // randomize order to optimize work distribution

			#ifdef USE_MPI
				if( g_numChildren > 0 )
				{
					g_rtConfig->ProteinBatchSize = (int) ceil( (float) proteins.size() / (float) g_numChildren / (float) g_rtConfig->NumBatches );
					cout << g_hostString << " calculates dynamic protein batch size is " << g_rtConfig->ProteinBatchSize << endl;
				}
			#endif

			fileList_t finishedFiles;
			fileList_t::iterator fItr;
			for( fItr = g_inputFilenames.begin(); fItr != g_inputFilenames.end(); ++fItr )
			{
				Timer fileTime(true);

				spectra.clear();
				spectraMassMapsByChargeState.clear();

				//if( !TestFileType( *fItr, "mzdata" ) )
				//	continue;

				cout << g_hostString << " is reading spectra from file \"" << *fItr << "\"" << endl;
				finishedFiles.insert( *fItr );

				Timer readTime(true);

				//long long memoryUsageCap = (int) GetAvailablePhysicalMemory() / 4;
				//int peakCountCap = (float) memoryUsageCap / ( sizeof( peakPreInfo ) + sizeof( peakInfo ) );
				//cout << g_hostString << " sets memory usage ceiling at " << memoryUsageCap << ", or about " << peakCountCap << " total peaks." << endl;
				try
				{
					spectra.readPeaks( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum );
				} catch( std::exception& e )
				{
					cerr << g_hostString << " had an error: " << e.what() << endl;
					return 1;
				}

				int totalPeakCount = 0;
				/*int i = 0;	
				while( GetAvailablePhysicalMemory() - 2*totalPeakCount*(sizeof(PeakInfo)+sizeof(float)) > 100000000 )
				{
					if( DataReader.ReadSpectra( g_rtConfig[ "SpectraBatchSize" ) ) < 1 )
						break;
					for( ; i < (int) baseList.size(); ++i )
						totalPeakCount += baseList[i]->peakCount;
				}*/
				//if( g_rtConfig->EndSpectraIndex > 0 )
					//DataReader.ReadSpectraRange( 1000, 1025 );
				//else
				//	DataReader.ReadSpectra( 0, peakCountCap );

				numSpectra = (int) spectra.size();
				for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
					totalPeakCount += (*sItr)->peakPreCount;

				cout << g_hostString << " read " << numSpectra << " spectra with " << totalPeakCount << " peaks; " << readTime.End() << " seconds elapsed." << endl;

				int skip = 0;
				if( numSpectra == 0 )
				{
					cout << g_hostString << " is skipping a file with no spectra." << endl;
					skip = 1;
				}

				#ifdef USE_MPI
					if( g_numChildren > 0 )
					{
						g_rtConfig->SpectraBatchSize = (int) ceil( (float) numSpectra / (float) g_numChildren / (float) g_rtConfig->NumBatches );
						cout << g_hostString << " calculates dynamic spectra batch size is " << g_rtConfig->SpectraBatchSize << endl;
					}

					for( int p=0; p < g_numChildren; ++p )
						MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
				#endif

				Timer searchTime;
				string startTime;
				string startDate;
				searchStats sumSearchStats;
				vector< size_t > opcs; // original peak count statistics
				vector< size_t > fpcs; // filtered peak count statistics

				if( !skip )
				{
					if( g_numProcesses > 1 )
					{
						#ifdef USE_MPI
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

							for( int p=0; p < g_numChildren; ++p )
								MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );

							if( !skip )
							{
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

								cout << g_hostString << " has " << numSpectra << " spectra prepared now; " << prepareTime.End() << " seconds elapsed." << endl;

								InitWorkerGlobals();

								/*if( g_rtConfig->ProteinSamplingTime > 0 || g_rtConfig->EstimateSearchTimeOnly )
								{
							        cout << g_hostString << " is estimating the count of sequence comparisons to be done." << endl;
									int estimatedComparisonCount = ExecuteCount();
									cout << g_hostString << " will make an estimated " << estimatedComparisonCount << " sequence comparisons." << endl;
								}*/

								if( !g_rtConfig->EstimateSearchTimeOnly )
								{
									SpectraList finishedSpectra;
									//do
									{
										cout << g_hostString << " is sending some prepared spectra to all worker nodes from a pool of " << spectra.size() << " spectra." << endl;
                                        try
                                        {
										    Timer sendTime(true);
										    numSpectra = TransmitSpectraToChildProcesses();
										    cout << g_hostString << " is finished sending " << numSpectra << " prepared spectra to all worker nodes; " <<
												    sendTime.End() << " seconds elapsed." << endl;
                                        } catch( std::exception& e )
                                        {
                                            cout << g_hostString << " had an error transmitting prepared spectra: " << e.what() << endl;
                                            MPI_Abort( MPI_COMM_WORLD, 1 );
                                        }

										cout << g_hostString << " is commencing database search on " << numSpectra << " spectra." << endl;
                                        try
                                        {
										    startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
										    TransmitProteinsToChildProcesses();
										    cout << g_hostString << " has finished database search; " << searchTime.End() << " seconds elapsed." << endl;
                                        } catch( std::exception& e )
                                        {
                                            cout << g_hostString << " had an error transmitting protein batches: " << e.what() << endl;
                                            MPI_Abort( MPI_COMM_WORLD, 1 );
                                        }

										cout << g_hostString << " is receiving search results for " << numSpectra << " spectra." << endl;
                                        try
                                        {
										    Timer receiveTime(true);
										    ReceiveResultsFromChildProcesses(sumSearchStats);
										    cout << g_hostString << " is finished receiving search results; " << receiveTime.End() << " seconds elapsed." << endl;
                                        } catch( std::exception& e )
                                        {
                                            cout << g_hostString << " had an error receiving results: " << e.what() << endl;
                                            MPI_Abort( MPI_COMM_WORLD, 1 );
                                        }
										
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
						cout << g_hostString << " is preparing " << numSpectra << " spectra." << endl;
						Timer prepareTime(true);
						PrepareSpectra();
						cout << g_hostString << " is finished preparing spectra; " << prepareTime.End() << " seconds elapsed." << endl;
						//spectra.dump();

						numSpectra = (int) spectra.size();

						skip = 0;
						if( numSpectra == 0 )
						{
							cout << g_hostString << " is skipping a file with no suitable spectra." << endl;
							skip = 1;
						}

						if( !skip )
						{
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

							InitWorkerGlobals();

							/*if( g_rtConfig->ProteinSamplingTime > 0 || g_rtConfig->EstimateSearchTimeOnly )
							{
								cout << g_hostString << " is estimating the count of sequence comparisons to be done." << endl;
								boost::int64_t estimatedComparisonCount = ExecuteCount();
								cout << g_hostString << " will make an estimated " << estimatedComparisonCount << " sequence comparisons." << endl;
							}*/

							if( !g_rtConfig->EstimateSearchTimeOnly )
							{
								cout << g_hostString << " is commencing database search on " << numSpectra << " spectra." << endl;
								startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
								sumSearchStats = ExecuteSearch();
								cout << g_hostString << " has finished database search; " << searchTime.End() << " seconds elapsed." << endl;
				
								cout << g_hostString << " overall stats: " << (string) sumSearchStats << endl;
							} else
								skip = 1;

							DestroyWorkerGlobals();
						}
					}

                    if( !skip ) {
						WriteOutputToFile( *fItr, startTime, startDate, searchTime.End(), opcs, fpcs, sumSearchStats );
					cout << g_hostString << " finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
                    PRINT_PROFILERS(cout,"old");
                    }

				}

				#ifdef USE_MPI
					int done = ( ( g_inputFilenames.size() - finishedFiles.size() ) == 0 ? 1 : 0 );
					for( int p=0; p < g_numChildren; ++p )
						MPI_Ssend( &done,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
				#endif
			}
		}
		#ifdef USE_MPI
			else
			{
				int allDone = 0;

				while( !allDone )
				{
					int skip;
					MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

					if( !skip )
					{
						SpectraList preparedSpectra;

						while( ReceiveUnpreparedSpectraBatchFromRootProcess() )
						{
							PrepareSpectra();
							preparedSpectra.insert( spectra.begin(), spectra.end(), preparedSpectra.end() );
							spectra.clear( false );
						}

						//for( int i=0; i < (int) preparedSpectra.size(); ++i )
						//	cout << preparedSpectra[i]->id.index << " " << preparedSpectra[i]->peakData.size() << endl;

						TransmitPreparedSpectraToRootProcess( preparedSpectra );

						preparedSpectra.clear();

						MPI_Recv( &skip,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );

						if( !skip )
						{

							int done = 0;
							do
							{
                                try
                                {
								    done = ReceiveSpectraFromRootProcess();
                                } catch( std::exception& e )
                                {
                                    cout << g_hostString << " had an error receiving prepared spectra: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }

								InitWorkerGlobals();

								int numBatches = 0;
								searchStats sumSearchStats;
								searchStats lastSearchStats;
                                try
                                {
								    while( ReceiveProteinBatchFromRootProcess( lastSearchStats.numComparisonsDone ) )
								    {
									    ++ numBatches;

									    lastSearchStats = ExecuteSearch();
									    sumSearchStats = sumSearchStats + lastSearchStats;
									    proteins.clear();
								    }
                                } catch( std::exception& e )
                                {
                                    cout << g_hostString << " had an error receiving protein batch: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }

								cout << g_hostString << " stats: " << numBatches << " batches; " << (string) sumSearchStats << endl;

                                try
                                {
								    TransmitResultsToRootProcess( sumSearchStats );
                                } catch( std::exception& e )
                                {
                                    cout << g_hostString << " had an error transmitting results: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }

								DestroyWorkerGlobals();
								spectra.clear();
								spectraMassMapsByChargeState.clear();
							} while( !done );
						}
					}

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
		result = myrimatch::ProcessHandler( argc, argv );
	} catch( std::exception& e )
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
