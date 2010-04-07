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
// The Original Code is the Bumbershoot core library.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//
#include "stdafx.h"
#include "tagrecon.h"
#include "UniModXMLParser.h"
#include "BlosumMatrix.h"
#include "DeltaMasses.h"
#include "PTMVariantList.h"
#include "pwiz/data/msdata/Version.hpp"
#include "pwiz/data/proteome/Version.hpp"
#include "svnrev.hpp"


namespace freicore
{
    namespace tagrecon
    {
    WorkerThreadMap	            g_workerThreads;
    simplethread_mutex_t	    resourceMutex;

    proteinStore			    proteins;
    SpectraList			        spectra;
    SpectraTagMap		        spectraTagMapsByChargeState;

    RunTimeConfig*      g_rtConfig;

    tagIndex_t					tagIndex;
    tagMetaIndex_t				tagMetaIndex;
    tagMutexes_t				tagMutexes;
    string						tagIndexFilename;

    modMap_t					knownModifications;
    UniModXMLParser*			unimodXMLParser;
    DeltaMasses*				deltaMasses;
    BlosumMatrix*				blosumMatrix;

    int Version::Major()                {return 1;}
    int Version::Minor()                {return 2;}
    int Version::Revision()             {return SVN_REV;}
    string Version::LastModified()      {return SVN_REVDATE;}
    string Version::str()               
    {
    	std::ostringstream v;
    	v << Major() << "." << Minor() << "." << Revision();
    	return v.str();
    }

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
		for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
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
			s->resultSet.convertProteinIndexesToNames( proteins );

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
			for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
				meanScoreHistogramsByChargeState[ z ].writeToSvgFile( filenameAsScanName + g_rtConfig->OutputSuffix + "_+" + lexical_cast<string>(z) + "_histogram.svg", "MVH score", "Density", g_rtConfig->ScoreHistogramWidth, g_rtConfig->ScoreHistogramHeight );

		// Get some stats and program parameters
		RunTimeVariableMap vars = g_rtConfig->getVariables();
		RunTimeVariableMap fileParams;
		for( RunTimeVariableMap::iterator itr = vars.begin(); itr != vars.end(); ++itr )
			fileParams[ string("Config: ") + itr->first ] = itr->second;
		fileParams["SearchEngine: Name"] = "TagRecon";
		fileParams["SearchEngine: Version"] = Version::str();
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
			spectra.calculateFDRs( g_rtConfig->maxChargeStateFromSpectra, 1.0f, "rev_" );
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
			spectra.calculateFDRs( g_rtConfig->maxChargeStateFromSpectra, 1.0f, "rev_" );
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
          cout << "TagRecon " << Version::str() << " (" << Version::LastModified() << ")\n" <<
                  "FreiCore " << freicore::Version::str() << " (" << freicore::Version::LastModified() << ")\n" <<
                  "ProteoWizard MSData " << pwiz::msdata::Version::str() << " (" << pwiz::msdata::Version::LastModified() << ")\n" <<
                  "ProteoWizard Proteome " << pwiz::proteome::Version::str() << " (" << pwiz::proteome::Version::LastModified() << ")\n" <<
					TAGRECON_LICENSE << endl;
		}

		g_rtConfig = new RunTimeConfig;
        g_rtConfig->executableFilepath = args[0];
        
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

			// Make sure the user gave at least one input file (tags or spectra)
			if( args.size() < 2 )
			{
				cerr << "Not enough arguments.\nUsage: " << args[0] << " [-ProteinDatabase <FASTA protein database filepath>] <input filemask 1> [input filemask 2] ..." << endl;
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

        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
            g_rtConfig->maxChargeStateFromSpectra = max((*sItr)->id.charge, g_rtConfig->maxChargeStateFromSpectra);

        //Set the mass tolerances according to the charge state.
        g_rtConfig->PrecursorMassTolerance.clear();
        g_rtConfig->NTerminalMassTolerance.clear();
        g_rtConfig->CTerminalMassTolerance.clear();
        for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
        {
            g_rtConfig->PrecursorMassTolerance.push_back( g_rtConfig->PrecursorMzTolerance * z );
            g_rtConfig->NTerminalMassTolerance.push_back( g_rtConfig->NTerminusMzTolerance * z );
            g_rtConfig->CTerminalMassTolerance.push_back( g_rtConfig->CTerminusMzTolerance * z );
        }

		// Get the number of spectra
		//size_t numSpectra = spectra.size();
		spectraTagMapsByChargeState = SpectraTagMap(TagSetCompare(max(g_rtConfig->MaxModificationMassPlus,g_rtConfig->MaxModificationMassMinus)));
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

    void ComputeXCorrs(string sourceFilepath)
    {
        // Get total spectra size
        int numSpectra = (int) spectra.size();

        cout << g_hostString << " is reading " << numSpectra << " spectra for cross-correlation analysis." << endl;

        Timer timer;
        timer.Begin();

        spectra.backFillPeaks(sourceFilepath);
        // Parse each spectrum
        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
        {
            try
            {
                if((*sItr)->resultSet.size()==0)
                    continue;
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
        cout << g_hostString << " finished reading its spectra; " << timer.End() << " seconds elapsed." << endl;
        cout << g_hostString << " is preparing spectra for cross-correlation." << endl;

        timer.Begin();
        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
        {
            try
            {
                // Preprocess the spectrum for XCorr
                if((*sItr)->resultSet.size()==0)
                    continue;
                (*sItr)->PreprocessForXCorr();
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
        cout << g_hostString << " finished preparing spectra; " << timer.End() << " seconds elapsed." << endl;
        cout << g_hostString << " is computing cross-correlations." << endl;

        timer.Begin();
        // For each spectrum, iterate through its result set and compute the XCorr.
        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
        {
            Spectrum* s = (*sItr);
            size_t charge = min(s->id.charge-1,1);
            BOOST_FOREACH(const SearchResult& result, (*sItr)->resultSet)
                s->ComputeXCorr(result,charge);
            s->peakDataForXCorr.clear();
        }
        cout << g_hostString << " finished computing cross-correlations; " << timer.End() << " seconds elapsed." << endl;

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
            runningNTerminalMass += residueMasses[i]; 
        }
	}

	/**
		ScoreKnownVariants takes a candidate peptide and delta mass. The procedure finds
		all the substitutions or preferred mass shifts that can fit the delta mass, generates 
        the variants, scores each of the generated variant against an experimental spectrum 
        and stores the results.
	*/
	inline boost::int64_t ScoreKnownModification(DigestedPeptide candidate, float mass, float modMass, 
												size_t locStart, size_t locEnd, Spectrum* spectrum, 
												int idx, vector<double>& sequenceIons, 
												const bool * ionTypesToSearchFor, float massTol, 
                                                int NTT, bool isDecoy) {

		
		boost::int64_t numComparisonsDone = 0;
		//cout << "\t\t\t\t\t" << candidate.sequence() << "," << modMass << "," << locStart << "," << locEnd << endl;
		// Get all possible amino acid substitutions that fit the modification mass with in the mass tolerance
		DynamicModSet possibleModifications;
        // Get all possible amino acid substitutions or preferred mass shifts 
        // that fit the mod mass within the tolerance
        size_t maxCombin = 1 , minCombin = 1;
        if(g_rtConfig->unknownMassShiftSearchMode == MUTATIONS)
            possibleModifications = deltaMasses->getPossibleSubstitutions(modMass, massTol);
        else if(g_rtConfig->unknownMassShiftSearchMode == PREFERRED_MASS_SHITS)
            possibleModifications =  g_rtConfig->preferredDeltaMasses.getMatchingMassShifts(modMass, massTol, maxCombin, minCombin);
            
		if(possibleModifications.size() == 0)
            return numComparisonsDone;

        // Generate variants of the current peptide using the possible substitutions
        vector <DigestedPeptide> possibleVariants;
        MakePeptideVariants(candidate, possibleVariants, minCombin, maxCombin, possibleModifications, locStart, locEnd);
        // For each variant
        for(size_t aVariantIndex = 0; aVariantIndex < possibleVariants.size(); aVariantIndex++) {
            const DigestedPeptide& variant = possibleVariants[aVariantIndex];
            // Check to make sure that the insertion of sub or preferred PTM doesn't put the mass 
            // of the peptide over the precursor mass tolerance.
            float neutralMass = g_rtConfig->UseAvgMassOfSequences ? ((float) variant.molecularWeight(0,true))
                : (float) variant.monoisotopicMass(0,true);
            float massDiff = fabs(neutralMass - spectrum->mOfPrecursor);
            if(massDiff > g_rtConfig->PrecursorMassTolerance[spectrum->id.charge])
                continue;

            string variantSequence = PEPTIDE_N_TERMINUS_SYMBOL + variant.sequence() + PEPTIDE_C_TERMINUS_SYMBOL;
            //cout << "\t\t\t\t\t" << tagrecon::getInterpretation(const_cast <DigestedPeptide&>(variant)) << endl;
            // Initialize the result
            SearchResult result(variant);
            // Compute the predicted spectrum and score it against the experimental spectrum
            CalculateSequenceIons( variant, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
            spectrum->ScoreSequenceVsSpectrum( result, variantSequence, sequenceIons, NTT );
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
            if( isDecoy )
                ++ spectrum->numDecoyComparisons;
            else
                ++ spectrum->numTargetComparisons;
            spectrum->resultSet.add( result );
            simplethread_unlock_mutex( &spectrum->mutex );
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
												const bool * ionTypesToSearchFor, int NTT,
                                                bool isDecoy) {

		
		boost::int64_t numComparisonsDone = 0;
		DynamicModSet possibleInterpretations;
		string peptideSeq = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
		multimap<double,SearchResult> localizationPossibilities;
        double topMVHScore = 0.0;
        //bool debug = false;
        //if(candidate.sequence() == "AMGIMNSFVNDIFER")
        //    debug = true;
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
        MakePeptideVariants(candidate, modificationVariants, 1, 1, possibleInterpretations, locStart, locEnd);
		// For each variant
		for(size_t variantIndex = 0; variantIndex < modificationVariants.size(); variantIndex++) {
			const DigestedPeptide& variant = modificationVariants[variantIndex];
            
            //cout << "\t" << variant.sequence() << endl;
			string variantSequence = PEPTIDE_N_TERMINUS_SYMBOL + variant.sequence() + PEPTIDE_C_TERMINUS_SYMBOL;
			// Initialize search result
			SearchResult result(variant);
			// Compute the predicted spectrum and score it against the experimental spectrum
            CalculateSequenceIons( variant, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
			spectrum->ScoreSequenceVsSpectrum( result, variantSequence, sequenceIons, NTT );
			// Assign the modification mass and the mass error
			// Compute the true modification mass. The modMass in the arguments is used to look up
			// the canidate mods with a certain tolerance. It's not the true modification mass of the
			// peptide.
			float trueModificationMass = g_rtConfig->UseAvgMassOfSequences? variant.modifications().averageDeltaMass()-candidate.modifications().averageDeltaMass() : variant.modifications().monoisotopicDeltaMass() - candidate.modifications().monoisotopicDeltaMass();
			result.mod = trueModificationMass;
			result.massError = spectrum->mOfPrecursor-(mass+trueModificationMass);
			// Assign the peptide identification to the protein by loci
			result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );
            // Score to beat
            if(topMVHScore<result.mvh)
                topMVHScore = result.mvh;
            //if(debug)
            //    cout << tagrecon::getInterpretation(const_cast <DigestedPeptide&>(variant)) << "->" << result.mvh << endl;
            // Save the localization result
            localizationPossibilities.insert(make_pair<double,SearchResult>(result.mvh,result));
		}

        // Update some search stats and add the best 
        // localization result(s) to the spectrum
        if(topMVHScore>0)
        {
            multimap<double,SearchResult>::const_iterator begin = localizationPossibilities.lower_bound(topMVHScore);
            multimap<double,SearchResult>::const_iterator end = localizationPossibilities.upper_bound(topMVHScore);
            
			simplethread_lock_mutex( &spectrum->mutex );
            if( isDecoy )
				++ spectrum->numDecoyComparisons;
			else
				++ spectrum->numTargetComparisons;
            // By default we only keep track of top 2 results for each ambiguous peptide
            int maxAmbResults = g_rtConfig->MaxAmbResultsForBlindMods;
            while(begin != end && maxAmbResults > 0)
            {
                spectrum->resultSet.add((*begin).second);
                ++ numComparisonsDone;
                ++ begin;
                -- maxAmbResults;
            }
            simplethread_unlock_mutex( &spectrum->mutex );
        }
        
		// Return the number of comparisons performed
		return numComparisonsDone;
	}

    inline bool checkForModificationSanity(const DigestedPeptide& candidate, float modMass,float tolerance)
    {
        // Figure out if the modification mass does nullify
        // any dynamic mods. For example, M+16ILFEGHFK peptide
        // can not have a modification of -16 on M. 
        const ModificationMap& dynamicMods = candidate.modifications();
        
        // Step through each of the dynamic mods
        for( ModificationMap::const_iterator itr = dynamicMods.begin(); itr != dynamicMods.end() ; ++itr )
        {
            // Check to make sure that the unknown mod mass doesn't nullify the dynamic mod mass
            float residueModMass = g_rtConfig->UseAvgMassOfSequences ? itr->second.averageDeltaMass() : itr->second.monoisotopicDeltaMass();
            if(fabs(residueModMass+modMass) <= tolerance)
                return false;
        }

        // Make sure that the modification mass doesn't negate all the mods already present in the peptide
        float candidateModificationsMass = g_rtConfig->UseAvgMassOfSequences ? candidate.modifications().averageDeltaMass() : candidate.modifications().monoisotopicDeltaMass();
        if(fabs(candidateModificationsMass+modMass) <= tolerance)
            return false;
        return true;
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
	boost::int64_t QuerySequence( const Peptide& protein, const DigestedPeptide& candidate, int idx, bool isDecoy, bool estimateComparisonsOnly = false )
	{
        bool debug = false;
        //if(candidate.sequence() == "SSQELEGSCR")
        //    debug = true;
		// Search stats
		boost::int64_t numComparisonsDone = 0;
		// Candidate peptide sequence and its mass
        string aSequence  = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
		string seq = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
        if(debug)
		    cout << aSequence << "," << aSequence.length() << "," << candidate.sequence() << "," << candidate.sequence().length() << endl;
        float neutralMass = g_rtConfig->UseAvgMassOfSequences ? ((float) candidate.molecularWeight(0,true))
                                                       : (float) candidate.monoisotopicMass(0,true);

		vector< double > fragmentIonsByChargeState;
        vector< double >& sequenceIons = fragmentIonsByChargeState;

		Spectrum* spectrum;
        SearchResult result(candidate);

        // Number of enzymatic termini
        int NTT = candidate.specificTermini();

		// Ion types to search for {y, b, [y-H2O,b-H2O], [y-NH3,b-NH3]}
		static const bool ionTypesToSearchFor[4] = { true, true, false, false };

        if( g_rtConfig->MassReconMode )
        {
            for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr)
            {
                spectrum = *sItr;
                float modMass = ((float)spectrum->mOfPrecursor) - neutralMass;
               
			    // Don't bother interpreting the mod mass if it's outside the user-set
                // limits.
                if( modMass < -1.0*g_rtConfig->MaxModificationMassMinus || modMass > g_rtConfig->MaxModificationMassPlus ) {
                    continue;
                }

                if(g_rtConfig->unknownMassShiftSearchMode != INACTIVE)
                {
                    // Check to remove unfeasible modification decorations
                    bool legitimateModMass = true;
                    float tolerance = max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1]);
                    if(g_rtConfig->unknownMassShiftSearchMode == BLIND_PTMS ||
                       g_rtConfig->unknownMassShiftSearchMode == MUTATIONS)
                        legitimateModMass = checkForModificationSanity(candidate, modMass, tolerance);

                    if( legitimateModMass &&
                        fabs(modMass) > max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1]) )
                    {
                        // If the user configured the searches for substitutions
                        if(g_rtConfig->unknownMassShiftSearchMode ==  MUTATIONS ||
                           g_rtConfig->unknownMassShiftSearchMode == PREFERRED_MASS_SHITS)
                        {
                            numComparisonsDone +=
                                ScoreKnownModification(candidate, neutralMass, modMass,
                                                          0, seq.length(), spectrum,
                                                          idx, sequenceIons, ionTypesToSearchFor, 
                                                          g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1],
                                                          NTT, isDecoy);
                        }

                        // If the user wants us to find unknown modifications.
                        if(g_rtConfig->unknownMassShiftSearchMode ==  BLIND_PTMS)
                        {
                            numComparisonsDone +=
                                ScoreUnknownModification(candidate, neutralMass, modMass,
                                                         0, seq.length(), spectrum, 
                                                         idx, sequenceIons, ionTypesToSearchFor,
                                                         NTT, isDecoy);
                        }
                    }
                }

                if( fabs(modMass) <= g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1] )
                {
				    // If there are no n-terminal and c-terminal delta mass differences then
				    // score the match as an unmodified sequence.

                    CalculateSequenceIons( candidate, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
				    spectrum->ScoreSequenceVsSpectrum( result, aSequence, sequenceIons, NTT );

				    result.massError = spectrum->mOfPrecursor-neutralMass;
				    result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );
				    ++numComparisonsDone;

				    if( g_rtConfig->UseMultipleProcessors )
					    simplethread_lock_mutex( &spectrum->mutex );
				    if( isDecoy )
					    ++ spectrum->numDecoyComparisons;
				    else
					    ++ spectrum->numTargetComparisons;
				    spectrum->resultSet.add( result );

				    if( g_rtConfig->UseMultipleProcessors )
					    simplethread_unlock_mutex( &spectrum->mutex );
			    }
            }

            // avoid tag-based querying
            //cout << numComparisonsDone << endl;
            return numComparisonsDone;
        }

		// Get tags of length 3 from the candidate peptide sequence
		vector< TagInfo > candidateTags;
        GetTagsFromSequence( candidate, 3, neutralMass, candidateTags );
        
        // Store all tag matches and their modification specific attributes
        set<TagMatchInfo> tagMatches;
        map<TagMatchInfo,size_t> lowIndex;
        map<TagMatchInfo,size_t> highIndex;
        map<TagMatchInfo,float> substitutionLookupTolerance;
        
        // For each of the generated tags
		for( size_t i=0; i < candidateTags.size(); ++i )
		{
			const TagInfo& tag = candidateTags[i];

			// Get the range of spectral tags that have the same sequence as the peptide tag
			// and total mass deviation between the n-terminal and c-terminal masses <= +/-
			// MaxTagMassDeviation.
			TagSetInfo tagKey(tag.tag, tag.nTerminusMass, tag.cTerminusMass);
            if(debug)
                cout << "\t" << tagKey.candidateTag << "," << tagKey.nTerminusMass << "," << tagKey.cTerminusMass << endl;
			pair< SpectraTagMap::const_iterator, SpectraTagMap::const_iterator > range = spectraTagMapsByChargeState.equal_range( tagKey );

			SpectraTagMap::const_iterator cur, end = range.second;
			
			// Iterate over the range
			for( cur = range.first; cur != end; ++cur )
			{
                if(debug)
				    cout << "\t\t" << (*cur->sItr)->id.source << " " << (*cur->sItr)->nativeID << " " << (*cur->sItr)->mOfPrecursor << endl;

				// Compute the n-terminal and c-terminal mass deviation between the peptide
				// sequence and the spectral tag-based sequence ([200.45]NST[400.65]) 
				// outside the tag match. For example:
				//    XXXXXXXXNSTXXXXXXXX (peptide sequence)
				//            |||		  (Tag match)
				//	  [200.45]NST[400.65] (spectrum sequence tag)
				float nTerminusDeviation = fabs( tag.nTerminusMass - cur->nTerminusMass );
				float cTerminusDeviation = fabs( tag.cTerminusMass - cur->cTerminusMass );
				if(nTerminusDeviation+cTerminusDeviation >= g_rtConfig->MaxModificationMassPlus) 
					continue;

                // Get the charge state of the fragment ions that gave rise to the tag
                int tagCharge = cur->tagChargeState;

                // Figure out if the termini matched
                TermMassMatch nTermMatch = nTerminusDeviation > g_rtConfig->NTerminalMassTolerance[tagCharge-1] ? MASS_MISMATCH : MASS_MATCH;
                TermMassMatch cTermMatch = cTerminusDeviation > g_rtConfig->CTerminalMassTolerance[tagCharge-1] ? MASS_MISMATCH : MASS_MATCH;
				// At least one termini has to match to proceed
				if( nTermMatch == MASS_MISMATCH && cTermMatch == MASS_MISMATCH )
					continue;
                // If the all the delta mass modes are tuned off then both termini has to match
                if( g_rtConfig->unknownMassShiftSearchMode == INACTIVE && nTermMatch != MASS_MATCH && cTermMatch != MASS_MATCH)
                    continue;

				// Get the mass spectrum, charge, and the total 
                // mass difference b/w the spectrum and the candidate
				spectrum = *cur->sItr;
				float modMass = ((float)spectrum->mOfPrecursor) - neutralMass;

                // Don't bother interpreting the mod mass if it's outside the user-set limits.
				if( modMass < -1.0*g_rtConfig->MaxModificationMassMinus || modMass > g_rtConfig->MaxModificationMassPlus )
					continue;
                
                // Make a tag match and remember some mod related attributes
                TagMatchInfo tagMatch(spectrum, modMass, nTermMatch, cTermMatch);
                size_t tagMisMatchLowIndex = 0;
                size_t tagMisMatchHighIndex = 0;
                float subMassTol = 0.0;
                if(nTermMatch == MASS_MISMATCH)
                {
                    // These indexes are used to move the mod around in the "blind PTM" mode.
                    tagMisMatchLowIndex = 0;
                    tagMisMatchHighIndex = (size_t) tag.lowPeakMz-1;
                    subMassTol = g_rtConfig->NTerminalMassTolerance[tagCharge-1];
                } else if(cTermMatch == MASS_MISMATCH)
                {
                    // These indexes are used to move the mod around in the "blind PTM" mode.
                    tagMisMatchLowIndex = (size_t) tag.lowPeakMz + tag.tag.length();
                    tagMisMatchHighIndex = aSequence.length()-1;
                    subMassTol = g_rtConfig->CTerminalMassTolerance[tagCharge-1];
                }

                // Store the mod movement indices and also the tag match
                lowIndex[tagMatch] = min(tagMisMatchLowIndex, lowIndex[tagMatch]);
                highIndex[tagMatch] = max(tagMisMatchHighIndex, highIndex[tagMatch]);
                substitutionLookupTolerance[tagMatch] = max(subMassTol, substitutionLookupTolerance[tagMatch]);
                tagMatches.insert(tagMatch);

                // Score the peptide without the PTM if the modificaiton mass is less than 5.0 daltons.
                // This would catch all parent mass errors.
                if(g_rtConfig->unknownMassShiftSearchMode != INACTIVE && fabs(modMass) <= 5.0)
                {
                    TagMatchInfo unmodTagMatch(spectrum, 0.0f, MASS_MATCH, MASS_MATCH);
                    tagMatches.insert(unmodTagMatch);
                    lowIndex[unmodTagMatch] = 0;
                    highIndex[unmodTagMatch] = 0;
                }
            }
        }

        // For each unique tag match
        for(set<TagMatchInfo>::const_iterator mItr = tagMatches.begin(); mItr != tagMatches.end(); ++mItr)
        {
            TagMatchInfo tagMatch = (*mItr);

            Spectrum* spectrum = tagMatch.spectrum;
            int spectrumCharge = spectrum->id.charge;

            if(tagMatch.nTermMatch == MASS_MATCH && tagMatch.cTermMatch == MASS_MATCH && 
                fabs(tagMatch.modificationMass) <=g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])
            {
                // If there are no n-terminal and c-terminal delta mass differences then
                // score the match as an unmodified sequence.
                CalculateSequenceIons( candidate, spectrumCharge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
                spectrum->ScoreSequenceVsSpectrum( result, aSequence, sequenceIons, NTT );

                result.massError = spectrum->mOfPrecursor - neutralMass;
                result.lociByIndex.insert( ProteinLocusByIndex( idx + g_rtConfig->ProteinIndexOffset, candidate.offset() ) );	
                ++numComparisonsDone;

                if( g_rtConfig->UseMultipleProcessors )
                    simplethread_lock_mutex( &spectrum->mutex );
                if( isDecoy )
                    ++ spectrum->numDecoyComparisons;
                else
                    ++ spectrum->numTargetComparisons;
                spectrum->resultSet.add( result );

                if( g_rtConfig->UseMultipleProcessors )
                    simplethread_unlock_mutex( &spectrum->mutex );
            } 
            else if(g_rtConfig->unknownMassShiftSearchMode !=  INACTIVE)
            {
                // Make sure we are looking at a real mod
                float modMassTolerance = max((float)g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1]);
                if(fabs(tagMatch.modificationMass) <= modMassTolerance)
                    continue;
                
                // Which terminal is a mass mismatch?
                TermType misMatchTerm = NONE;
                misMatchTerm = (tagMatch.nTermMatch == MASS_MISMATCH && tagMatch.cTermMatch == MASS_MATCH) ? NTERM : misMatchTerm;
                misMatchTerm = (tagMatch.nTermMatch == MASS_MATCH && tagMatch.cTermMatch == MASS_MISMATCH) ? CTERM : misMatchTerm;
                
                // Check to remove unfeasible modification decorations
                // If we are configured to reconcile the modificaitons 
                // against a small list, check to make sure that the 
                // current mod mass is in that list.
                bool legitimateModMass = true;
                if(g_rtConfig->unknownMassShiftSearchMode == PREFERRED_MASS_SHITS) 
                   legitimateModMass &= g_rtConfig->preferredDeltaMasses.containsMassShift(tagMatch.modificationMass, modMassTolerance);
                
                // If we are in the blind or mutation mode, make sure we are not picking any
                // irregular mods like -16 on M+16 or terminal.
                if(g_rtConfig->unknownMassShiftSearchMode == BLIND_PTMS ||
                    g_rtConfig->unknownMassShiftSearchMode == MUTATIONS)
                    legitimateModMass = checkForModificationSanity(candidate, tagMatch.modificationMass, modMassTolerance);
                
                int modLowIndex = lowIndex[tagMatch];
                int modHighIndex = highIndex[tagMatch];
                float lookupTol = substitutionLookupTolerance[tagMatch];
                // Try to explain away this modification
                if( legitimateModMass && misMatchTerm != NONE) 
                {

                    // If the user configured the search for either substitutions or preferred mass shifts
                    if(g_rtConfig->unknownMassShiftSearchMode ==  MUTATIONS||
                        g_rtConfig->unknownMassShiftSearchMode == PREFERRED_MASS_SHITS) 
                    {
                        // Find the substitutions or PDMs that fit the mass, generate variants and score them.
                        numComparisonsDone += 
                            ScoreKnownModification(candidate, neutralMass, tagMatch.modificationMass, modLowIndex, 
                                                   modHighIndex,spectrum, idx, sequenceIons, ionTypesToSearchFor,
                                                   lookupTol, NTT, isDecoy );
                    }

                    // If the user wants us to find unknown modifications perform some sanity checks before scoring.
                    // First, check to see if the mod can be explained away by either growing or shrinking the peptide terminal.
                    if(g_rtConfig->unknownMassShiftSearchMode ==  BLIND_PTMS && 
                        checkForTerminalErrors(protein, candidate, tagMatch.modificationMass, 
                                                        max(lookupTol,(float) NEUTRON), misMatchTerm) ) 
                    {

                        // Adjust the location bounds in which the mod can be moved. 
                        // We ignore the termini when moving an unknown mod because
                        // we can never find an ion to differentiate b/w termini mod
                        // and first or last resiude mod.
                        modLowIndex = misMatchTerm == NTERM ? 1 : modLowIndex;
                        modHighIndex = misMatchTerm == CTERM ? modHighIndex-1 : modHighIndex;
                        numComparisonsDone += 
                            ScoreUnknownModification(candidate,neutralMass, tagMatch.modificationMass, modLowIndex, 
                                                     modHighIndex, spectrum, idx, sequenceIons,	ionTypesToSearchFor,
                                                     NTT, isDecoy);
                    }
                } 
            }
        }

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
			
			// The database search schema works in the following fashion
			// A protein database is split into equal parts using number
			// of processors available for search. If each processor is a
			// multi-core CPU then multiple threads (equal to number of cores
			// in a processor) are created per processor. Each processor takes
			// its chunk of protein database and searches it with all the
			// spectra in the dataset using threads. The thread works in a
			// interweaving fashion.
			// For each worker thread on the processor
			for( i = threadInfo->workerNum; i < numProteins; i += g_numWorkers )
			{
				++ threadInfo->stats.numProteinsDigested;

				// Get a protein sequence
				Peptide protein(proteins[i].getSequence());
                bool isDecoy = proteins[i].isDecoy();
				// Digest the protein sequence using pwiz library. The sequence is
				// digested using cleavage rules specified in the user configuration
				// file.
                Digestion digestion( protein, g_rtConfig->cleavageAgentRegex, g_rtConfig->digestionConfig );
				// For each peptide
				for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr ) {
					// Get the mass
					double mass = g_rtConfig->UseAvgMassOfSequences ? itr->molecularWeight()
						: itr->monoisotopicMass();
                    if( mass+g_rtConfig->MaxModificationMassPlus < g_rtConfig->curMinSequenceMass ||
                        mass-g_rtConfig->MaxModificationMassMinus > g_rtConfig->curMaxSequenceMass ||
                        mass == 0.0 )
						continue;
                    
                    vector<DigestedPeptide> digestedPeptides;
					//START_PROFILER(0);
					// Make any PTM variants if user has specified dynamic mods. This function
					// decorates the candidate peptide with both static and dynamic mods.
                    PTMVariantList variantIterator( (*itr), g_rtConfig->MaxDynamicMods, g_residueMap->dynamicMods, g_residueMap->staticMods, g_rtConfig->MaxNumPeptideVariants);
                    if(variantIterator.isSkipped) {
                        ++ threadInfo->stats.numCandidatesSkipped;
                        continue;
                    }
                    variantIterator.getVariantsAsList(digestedPeptides);
                    threadInfo->stats.numCandidatesGenerated += digestedPeptides.size();
                    
					//STOP_PROFILER(0);
                    // For each candidate peptide sequence
				    for( size_t j=0; j < digestedPeptides.size(); ++j )
				    {
					    //START_PROFILER(1);
					    // Search the sequence against the tags and the spectra that generated the tags
                        boost::int64_t queryComparisonCount = QuerySequence( protein, digestedPeptides[j], i, isDecoy );
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
					float proteinsPerSec = float( ( threadInfo->stats.numProteinsDigested / totalSearchTime ) );
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

    // Number of enzymatic termini (NET) probability computation variables
    struct NETWorkerInfo
    {
        // Worker ID, protein index start and protein index stop
        size_t number;
        size_t start;
        size_t end;
        // Peptide NET class distribution
        vector<double> NETStats;

        NETWorkerInfo(int num, int strt, int e)
        {
            number = num;
            start = strt;
            end = e;
            NETStats.resize(3);
            fill(NETStats.begin(), NETStats.end(),0);
        };

        NETWorkerInfo() {};
    };
    size_t numNETWorkers;
    simplethread_handle_array_t NETWorkerHandles;
    typedef map<simplethread_id_t, NETWorkerInfo*> NETWorkerThreadMap;
    NETWorkerThreadMap NETWorkerThreads;

    simplethread_return_t ExecuteNETThread(simplethread_arg_t threadArg)
    {

        // Get a sempahore on this function
        simplethread_lock_mutex( &resourceMutex );
        // Get the thread ID
        simplethread_id_t threadId = simplethread_get_id();
        NETWorkerThreadMap* threadMap = (NETWorkerThreadMap*) threadArg;
        // Find the data structure that is supposed to store the thread information.
        NETWorkerInfo* threadInfo = reinterpret_cast< NETWorkerInfo* >( threadMap->find( threadId )->second );
        // Realease the semaphore
        simplethread_unlock_mutex( &resourceMutex );

        // Digest the proteins given the thread and accumulate the total numbers 
        // of peptides seen in each NET class
        for(size_t index=threadInfo->start; index <= threadInfo->end; ++index)
        {
            Peptide protein(proteins[index].getSequence());
            Digestion digestion( protein, g_rtConfig->cleavageAgentRegex, g_rtConfig->digestionConfig );
            for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr ) 
                ++threadInfo->NETStats[(*itr).specificTermini()];
        }
        return 0;
    }
    
    /* This function randomly samples 
    */
    void ComputeNETProbabilities()
    {
        g_rtConfig->NETRewardVector.resize(3);
        fill(g_rtConfig->NETRewardVector.begin(), g_rtConfig->NETRewardVector.end(), 0);
        if(!g_rtConfig->UseNETAdjustment)
            return;
        
        cout << g_hostString << " computing NET probabilities." << endl;
        Timer timer;
        timer.Begin();
        // Shuffle the proteins, select 5% of the total, and distribute them between workers
        proteins.random_shuffle();
        numNETWorkers = g_numWorkers;
        size_t eachWorkerProteinCount = (size_t) ((proteins.size() * 0.05)/numNETWorkers);
        // Get a semaphore
        simplethread_lock_mutex( &resourceMutex );
        // Create a thread for each of the processor and
        // attach the procedure that needs to be executed
        // for each thread [i.e. the start() function].
        size_t proteinStartIndex = 0;
        for( size_t t = 0; t < numNETWorkers; ++t )
        {
            simplethread_id_t threadId;
            simplethread_handle_t threadHandle = simplethread_create_thread( &threadId, &ExecuteNETThread, &NETWorkerThreads );
            NETWorkerThreads[ threadId ] = new NETWorkerInfo( t, proteinStartIndex, (proteinStartIndex+eachWorkerProteinCount) );
            NETWorkerHandles.array.push_back( threadHandle );
            proteinStartIndex += eachWorkerProteinCount;
        }
        simplethread_unlock_mutex( &resourceMutex );

        simplethread_join_all( &NETWorkerHandles );
        
        // Accumulate the total numbers of peptides in each NET class.
        for(NETWorkerThreadMap::const_iterator itr = NETWorkerThreads.begin(); itr != NETWorkerThreads.end(); ++itr)
            for(size_t index=0; index < (*itr).second->NETStats.size(); ++index)
                g_rtConfig->NETRewardVector[index] += (*itr).second->NETStats[index];

        // Normalize and compute the log probability of findind a peptide in each class by random chance
        double sum = accumulate(g_rtConfig->NETRewardVector.begin(), g_rtConfig->NETRewardVector.end(), 0.0);
        for(size_t index=0; index < g_rtConfig->NETRewardVector.size(); ++index) 
            if(g_rtConfig->NETRewardVector[index] > 0)
                g_rtConfig->NETRewardVector[index]=log(g_rtConfig->NETRewardVector[index]/sum);
        
        cout << g_hostString << " Finished computing NET probabilities; " << timer.End() << " seconds elapsed." << endl;

    }

    // Shared pointer to SpectraList.
    typedef boost::shared_ptr<SpectraList> SpectraListPtr;
    /**
    This function takes a spectra list and splits them into small batches as dictated by
    ResultsPerBatch variable. This function also checks to make sure that the last batch
    is not smaller than 1000 spectra.
    */
    inline vector<SpectraListPtr> estimateSpectralBatches()
    {
        int estimatedResultsSize = 0;

        // Shuffle the spectra so that there is a 
        // proper load balancing between batches.
        spectra.random_shuffle();

        vector<SpectraListPtr> batches;
        SpectraListPtr current(new SpectraList());
        // For each spectrum
        for( SpectraList::const_iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr ) 
        {
            // Check the result size, if it exceeds the limit, then push back the
            // current list into the vector and get a fresh list
            estimatedResultsSize += g_rtConfig->MaxResults;
            if(estimatedResultsSize>g_rtConfig->ResultsPerBatch) 
            {
                batches.push_back(current);
                current.reset(new SpectraList());
                estimatedResultsSize = g_rtConfig->MaxResults;
            }
            current->push_back((*sItr));
        }
        // Make sure you push back the last batch
        if(current->size()>0)
            batches.push_back(current);
        // Check to see if the last batch is not a tiny batch
        if(batches.back()->size()<1000 && batches.size()>1) 
        {
            SpectraListPtr last = batches.back(); batches.pop_back();
            SpectraListPtr penultimate = batches.back(); batches.pop_back();
            penultimate->insert(last->begin(),last->end(),penultimate->end());
            batches.push_back(penultimate);
            last->clear( false );
        }
        //for(vector<SpectraListPtr>::const_iterator bItr = batches.begin(); bItr != batches.end(); ++bItr)
        //    cout << (*bItr)->size() << endl;
        return batches;
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
                proteins.readFASTA( g_dbFilename );
			} catch( exception& e )
			{
				cout << g_hostString << " had an error: " << e.what() << endl;
				return 1;
			}
			cout << g_hostString << " read " << proteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl;

			proteins.random_shuffle(); // randomize order to optimize work distribution
            ComputeNETProbabilities(); // Compute the penalties for a peptide's enzymatic status
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
                // Clears the tag map
                spectraTagMapsByChargeState.clear();

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
				string sourceFilepath;
                if( g_rtConfig->MassReconMode )
                    // Treat input files as peak files
                    sourceFilepath = *fItr;
                else
				    // Treat input files as tag files:
                    // Read the metadata without the tags (returns the path to peaks file, i.e. mzML)
                    sourceFilepath = spectra.readTags( *fItr, g_rtConfig->StartSpectraScanNum, g_rtConfig->EndSpectraScanNum, true);

				// Set the parameters for the search
				g_rtConfig->setVariables( varsFromFile );
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
                    
                    TransmitNETRewardsToChildProcess();
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
					if( g_numProcesses > 1 && !g_rtConfig->EstimateSearchTimeOnly )
					{
						#ifdef USE_MPI
							//Use the child processes to prepare the spectra
							cout << g_hostString << " is sending spectra to worker nodes to prepare them for search." << endl;
							Timer prepareTime(true);
							TransmitUnpreparedSpectraToChildProcesses();

							spectra.clear();
                            spectraTagMapsByChargeState.clear();

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
                                if(!g_rtConfig->MassReconMode) 
                                {
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
                                }
								// Initialize few global data structures. See function documentation
								// for details
								InitWorkerGlobals();

                                // List to store finished spectra
                                SpectraList finishedSpectra;
                                // Split the spectra into batches if needed
                                vector<SpectraListPtr> batches = estimateSpectralBatches();
                                if(batches.size()>1)
                                    cout << g_hostString << " is splitting spectra into " << batches.size() << " batches for search." << endl;
                                startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
                                // For each spectral batch
                                size_t batchIndex = 0;
                                for(vector<SpectraListPtr>::iterator bItr = batches.begin(); bItr != batches.end(); ++bItr) 
                                {
                                    // Variables to report batch progess to the user
                                    ++batchIndex;
                                    stringstream batchString;
                                    batchString << "";
                                    if(batches.size()>1)
                                        batchString << " (" << batchIndex << " of " << batches.size() << " batches)";
                                    // Clear the master list and populate it with a small batch
                                    spectra.clear( false );
                                    spectra.insert((*bItr)->begin(), (*bItr)->end(), spectra.end());
                                    // Check to see if we are processing the last batch.
                                    int lastBatch = 0;
                                    if((*bItr) == batches.back())
                                        lastBatch = 1;
                                    // Transmit spectra to all children. Also tell them if this is
                                    // the last batch of the spectra they would be getting from the parent.
                                    cout << g_hostString << " is sending some prepared spectra to all worker nodes from a pool of " << spectra.size() << " spectra" << batchString.str() << "." << endl;
                                    try
                                    {
                                        Timer sendTime(true);
                                        numSpectra = TransmitSpectraToChildProcesses(lastBatch);
                                        cout << g_hostString << " is finished sending " << numSpectra << " prepared spectra to all worker nodes; " <<
                                            sendTime.End() << " seconds elapsed." << endl;
                                    } catch( std::exception& e )
                                    {
                                        cout << g_hostString << " had an error transmitting prepared spectra: " << e.what() << endl;
                                        MPI_Abort( MPI_COMM_WORLD, 1 );
                                    }
                                    // Transmit the proteins and start the search.
                                    cout << g_hostString << " is commencing database search on " << numSpectra << " spectra" << batchString.str() << "." << endl;
                                    try
                                    {
                                        Timer batchTimer(true); batchTimer.Begin();
                                        TransmitProteinsToChildProcesses();
                                        cout << g_hostString << " has finished database search; " << batchTimer.End() << " seconds elapsed" << batchString.str() << "." << endl;
                                    } catch( std::exception& e )
                                    {
                                        cout << g_hostString << " had an error transmitting protein batches: " << e.what() << endl;
                                        MPI_Abort( MPI_COMM_WORLD, 1 );
                                    }
                                    // Get the results
                                    cout << g_hostString << " is receiving search results for " << numSpectra << " spectra" << batchString.str() << "." << endl;
                                    try
                                    {
                                        Timer receiveTime(true);
                                        ReceiveResultsFromChildProcesses(sumSearchStats, ((*bItr) == batches.front()));
                                        cout << g_hostString << " is finished receiving search results; " << receiveTime.End() << " seconds elapsed." << endl;
                                    } catch( std::exception& e )
                                    {
                                        cout << g_hostString << " had an error receiving results: " << e.what() << endl;
                                        MPI_Abort( MPI_COMM_WORLD, 1 );
                                    }
                                    cout << g_hostString << " overall stats: " << (string) sumSearchStats << endl;

                                    // Store the searched spectra in a list and clear the 
                                    // master list for next batch
                                    finishedSpectra.insert( spectra.begin(), spectra.end(), finishedSpectra.end() );
                                    spectra.clear( false );
                                    (*bItr)->clear( false );
                                }
                                searchTime.End();
                                // Move the searched spectra from temporary list to the master list
                                spectra.clear( false );
                                spectra.insert(finishedSpectra.begin(), finishedSpectra.end(), spectra.end() );
                                finishedSpectra.clear(false);
                                // Spectra are randomly shuffled for load distribution during batching process
                                // Sort them back by ID.
                                spectra.sort( spectraSortByID() );

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

                            if( !g_rtConfig->MassReconMode )
                            {
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
                            }

						    // Initialize global data structures.
                            // Must be done after spectra charge states are determined and tags are read
						    InitWorkerGlobals();

							cout << g_hostString << " is commencing database search on " << spectra.size() << " spectra." << endl;
							startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
							// Start the threads
							sumSearchStats = ExecuteSearch();
							cout << g_hostString << " has finished database search; " << searchTime.End() << " seconds elapsed." << endl;
							cout << g_hostString << (string) sumSearchStats << endl;

							// Free global variables
							DestroyWorkerGlobals();
						}
					}

					if( !skip )
					{
                        if(g_rtConfig->ComputeXCorr)
                            ComputeXCorrs(sourceFilepath);
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

                    ReceiveNETRewardsFromRootProcess();

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
                                try
                                {
                                    // Get a batch of protein sequences from root process
                                    while( ReceiveProteinBatchFromRootProcess( lastSearchStats.numComparisonsDone ) )
                                    {
                                        ++ numBatches;

                                        // Execute the search
                                        lastSearchStats = ExecuteSearch();
                                        sumSearchStats = sumSearchStats + lastSearchStats;
                                    }
                                } catch(std::exception& e )
                                {
                                    cout << g_hostString << " had an error receiving protein batch: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }

								cout << g_hostString << " stats: " << numBatches << " batches; " << (string) sumSearchStats << endl;
                                try
                                {
                                    // Send results back to the parent process
                                    TransmitResultsToRootProcess(sumSearchStats);
                                } catch( std::exception& e )
                                {
                                    cout << g_hostString << " had an error transmitting results: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }

								// Clean up the variables.
								DestroyWorkerGlobals();
								spectra.clear();
                                spectraTagMapsByChargeState.clear();
							} while( !done );
						}
					}

					// See if we are all done. Master process sends this signal when there are
					// no more spectra to search
					MPI_Recv( &allDone,	1,		MPI_INT,	0,	0x00, MPI_COMM_WORLD, &st );
				} // end of while
			} // end of if
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
