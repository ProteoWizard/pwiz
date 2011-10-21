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
#include "tagreconVersion.hpp"
#include "boost/lockfree/fifo.hpp"

namespace freicore
{
namespace tagrecon
{
    proteinStore			        proteins;
    boost::lockfree::fifo<size_t>   proteinTasks;
    SearchStatistics                searchStatistics;

    SpectraList			            spectra;
    SpectraTagTrie                  spectraTagTrie;

    RunTimeConfig*                  g_rtConfig;

    modMap_t					    knownModifications;
    UniModXMLParser*			    unimodXMLParser;
    DeltaMasses*				    deltaMasses;
    BlosumMatrix*				    blosumMatrix;


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
							SearchStatistics& overallStats )
	{
		int numSpectra = 0;

		string filenameAsScanName = basename( MAKE_PATH_FOR_BOOST(dataFilename) );

		// Make histograms of scores by charge state
		map< int, Histogram<float> > meanScoreHistogramsByChargeState;
		for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
			meanScoreHistogramsByChargeState[ z ] = Histogram<float>( g_rtConfig->NumScoreHistogramBins, g_rtConfig->MaxScoreHistogramValues );

		BOOST_FOREACH(Spectrum* s, spectra)
		{
			++ numSpectra;

			// Set the spectrum id as the scan number
			spectra.setId( s->id, SpectrumId( filenameAsScanName, s->id.nativeID, s->id.charge ) );

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

        // SearchSpectraList::write() expects MyriMatch 2.0 tolerances (with a unit suffix)
        string precursorMassType = g_rtConfig->UseAvgMassOfSequences ? "Avg" : "Mono";
        fileParams["Config: PrecursorMzToleranceRule"] = bal::to_lower_copy(precursorMassType);
        fileParams["Config: " + precursorMassType + "PrecursorMzTolerance"] = lexical_cast<string>(g_rtConfig->PrecursorMzTolerance) + "mz";
        fileParams["Config: FragmentMzTolerance"] += "mz";

        string extension = g_rtConfig->outputFormat == pwiz::identdata::IdentDataFile::Format_pepXML ? ".pepXML" : ".mzid";
		string outputFilename = filenameAsScanName + g_rtConfig->OutputSuffix + extension;
		cout << "Writing search results to file \"" << outputFilename << "\"." << endl;

		spectra.write(dataFilename,
                      g_rtConfig->outputFormat,
                      g_rtConfig->OutputSuffix,
                      "TagRecon",
                      Version::str(),
                      "http://forge.fenchurch.mc.vanderbilt.edu/projects/tagrecon/",
                      g_dbPath + g_dbFilename,
                      g_rtConfig->cleavageAgentRegex,
                      g_rtConfig->decoyPrefix,
                      fileParams);
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
						cerr << "Could not find runtime configuration at \"" << args[i+1] << "\"." << endl;
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
					cerr << "Could not find the default configuration file (hard-coded defaults in use)." << endl;
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

		// Determine the maximum seen charge state
        g_rtConfig->maxChargeStateFromSpectra = 1;
		BOOST_FOREACH(Spectrum* s, spectra)
		    g_rtConfig->maxChargeStateFromSpectra = max(s->id.charge, g_rtConfig->maxChargeStateFromSpectra);

        //Set the mass tolerances according to the charge state.
        g_rtConfig->PrecursorMassTolerance.clear();
        g_rtConfig->NTerminalMassTolerance.clear();
        g_rtConfig->CTerminalMassTolerance.clear();
        for( int z=1; z <= g_rtConfig->maxChargeStateFromSpectra; ++z )
        {
            g_rtConfig->PrecursorMassTolerance.push_back(g_rtConfig->PrecursorMzTolerance * z);
            g_rtConfig->NTerminalMassTolerance.push_back(g_rtConfig->NTerminusMzTolerance * z);
            g_rtConfig->CTerminalMassTolerance.push_back(g_rtConfig->CTerminusMzTolerance * z);
        }

   		// Locate all tags with same amino acid sequence to a single location.        
        typedef set< shared_ptr<AATagToSpectraMap>, AATagToSpectraMapCompare > UniqueTags;
        UniqueTags  uniqueTags;
        for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr ) 
        {
			for( TagList::iterator tItr = (*sItr)->tagList.begin(); tItr != (*sItr)->tagList.end(); ++tItr ) 
            {
                // Generate the tag-spectrum structure
                TagSpectrumInfo  tagInfo(sItr, tItr->tag, tItr->nTerminusMass, tItr->cTerminusMass);
                tagInfo.tagChargeState = tItr->chargeState;
                // Insert it into an empty map
                shared_ptr<AATagToSpectraMap> tagMap(new AATagToSpectraMap);
                tagMap->aminoAcidTag = tItr->tag;
                pair< UniqueTags::const_iterator, bool > ret;
                ret = uniqueTags.insert(tagMap);
                // Update the map
                const_cast< shared_ptr<AATagToSpectraMap>&>((*ret.first))->addTag(tagInfo);
			}
		}
        // Initialize a tag trie for rapid tag location of tags in the protein sequence.
        spectraTagTrie.clear();
        spectraTagTrie.insert(uniqueTags.begin(),uniqueTags.end());
        
		// Get minimum and maximum peptide masses observed in the dataset
		// and determine the number of peak bins required. This 
		double minPrecursorMass = spectra.front()->mOfPrecursor;
		double maxPrecursorMass = 0;

		size_t maxPeakBins = (size_t) spectra.front()->totalPeakSpace;
		BOOST_FOREACH(Spectrum* s, spectra)
		{
			if( s->mOfPrecursor < minPrecursorMass )
				minPrecursorMass = s->mOfPrecursor;

			if( s->mOfPrecursor > maxPrecursorMass )
				maxPrecursorMass = s->mOfPrecursor;

			size_t totalPeakBins = (size_t) round( s->totalPeakSpace / ( g_rtConfig->FragmentMzTolerance * 2.0 ) );
			if( totalPeakBins > maxPeakBins )
				maxPeakBins = totalPeakBins;
		}

		g_rtConfig->curMinPeptideMass = minPrecursorMass - g_rtConfig->PrecursorMassTolerance.back() - g_rtConfig->MaxModificationMassPlus;
		g_rtConfig->curMaxPeptideMass = maxPrecursorMass + g_rtConfig->PrecursorMassTolerance.back() + g_rtConfig->MaxModificationMassMinus;

        // set the effective minimum and maximum sequence masses based on config and precursors
        g_rtConfig->curMinPeptideMass = max( g_rtConfig->curMinPeptideMass, g_rtConfig->MinPeptideMass );
        g_rtConfig->curMaxPeptideMass = min( g_rtConfig->curMaxPeptideMass, g_rtConfig->MaxPeptideMass );

        double minResidueMass = AminoAcid::Info::record('G').residueFormula.monoisotopicMass();
        double maxResidueMass = AminoAcid::Info::record('W').residueFormula.monoisotopicMass();

        // calculate minimum length of a peptide made entirely of tryptophan over the minimum mass
        int curMinPeptideLength = max( g_rtConfig->MinPeptideLength,
                                       (int) floor( g_rtConfig->curMinPeptideMass / maxResidueMass ) );

        // calculate maximum length of a peptide made entirely of glycine under the maximum mass
        int curMaxPeptideLength = min( (int) ceil( g_rtConfig->curMaxPeptideMass / minResidueMass ), 
                                       g_rtConfig->MaxPeptideLength);

        // set digestion parameters
        Digestion::Specificity specificity = (Digestion::Specificity) g_rtConfig->MinTerminiCleavages;
        g_rtConfig->MaxMissedCleavages = g_rtConfig->MaxMissedCleavages < 0 ? 10000 : g_rtConfig->MaxMissedCleavages;
        g_rtConfig->digestionConfig = Digestion::Config( g_rtConfig->MaxMissedCleavages,
                                                         curMinPeptideLength,
                                                         curMaxPeptideLength,
                                                         specificity );

		//cout << g_hostString << " is precaching factorials up to " << (int) maxPeakSpace << "." << endl;
		
		// Calculate the ln(x!) table where x= number of m/z spaces.
		// This table is used in MVH scoring.
		g_lnFactorialTable.resize( maxPeakBins );
		//cout << g_hostString << " finished precaching factorials." << endl;

		if( !g_numChildren )
		{
			cout << "Smallest observed precursor is " << minPrecursorMass << " Da." << endl;
			cout << "Largest observed precursor is " << maxPrecursorMass << " Da." << endl;
            cout << "Min. effective sequence mass is " << g_rtConfig->curMinPeptideMass << endl;
            cout << "Max. effective sequence mass is " << g_rtConfig->curMaxPeptideMass << endl;
            cout << "Min. effective sequence length is " << curMinPeptideLength << endl;
            cout << "Max. effective sequence length is " << curMaxPeptideLength << endl;
		}

        //cout << "tagMapSize:" << spectraTagMapsByChargeState.size() << endl;

		return 0;
	}

	void DestroyWorkerGlobals()
	{
	}

    void ComputeXCorrs()
    {        
        Timer timer;
        timer.Begin();

		if( g_numChildren == 0 )
            cout << "Computing cross-correlations." << endl;

        // For each spectrum, iterate through its result set and compute the XCorr.
        BOOST_FOREACH(Spectrum* s, spectra)
            s->ComputeXCorrs();

        if( g_numChildren == 0 )
            cout << "Finished computing cross-correlations; " << timer.End() << " seconds elapsed." << endl;
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

		if( g_numChildren == 0 )
			cout << "Trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;

		int preTrimCount = spectra.filterByPeakCount(g_rtConfig->minIntensityClassCount);
		numSpectra = (int) spectra.size();

		if( g_numChildren == 0 )
		{
			cout << "Trimmed " << preTrimCount << " spectra for being too sparse." << endl;
			cout << "Determining charge states for " << numSpectra << " spectra." << endl;
		}

		timer.Begin();
		SpectraList duplicates;
		// Try to determine the charge state for each spectrum
		// If you can't determine the charge state (i.e if z
		// state is not +1) then duplicate the spectrum to create
		// multiple charge states.
		BOOST_FOREACH(Spectrum* s, spectra)
		{
			try
			{
				if( !g_rtConfig->UseChargeStateFromMS )
					spectra.setId( s->id, SpectrumId( s->id.source, s->id.nativeID, 0 ) );

				if( s->id.charge == 0 )
				{
					SpectrumId preChargeId( s->id );
					// Determine the charge state
					s->DetermineSpectrumChargeState();
					SpectrumId postChargeId( s->id );

					// If the charge state is not +1
					if( postChargeId.charge == 0 )
					{
						// Duplicate the spectrum and create
						// spectrum with multiple charge states
						postChargeId.setCharge(2);
                        s->possibleChargeStates = vector<int>(1, 2);

						if( g_rtConfig->DuplicateSpectra )
						{
							for( int z = 3; z <= g_rtConfig->NumChargeStates; ++z )
							{
								Spectrum* s2 = new Spectrum( *s );
								s2->id.setCharge(z);
                                s2->mutex.reset(new boost::mutex); // create a separate mutex
                                s2->possibleChargeStates = vector<int>(1, z);
								duplicates.push_back(s2);
							}
						}
					}
                    else
                        s->possibleChargeStates = vector<int>(1, 1);

					spectra.setId( preChargeId, postChargeId );
				}

			} catch( exception& e )
			{
				throw runtime_error( string( "duplicating scan " ) + string( s->id ) + ": " + e.what() );
			} catch( ... )
			{
				throw runtime_error( string( "duplicating scan " ) + string( s->id ) );
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
			cout << "Finished determining charge states for its spectra; " << timer.End() << " seconds elapsed." << endl;
			cout << "Preprocessing " << numSpectra << " spectra." << endl;
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
			if( (*sItr)->mOfPrecursor < g_rtConfig->MinPeptideMass ||
				(*sItr)->mOfPrecursor > g_rtConfig->MaxPeptideMass )
			{
				(*sItr)->peakPreData.clear();
				(*sItr)->peakData.clear();
			}
		}

		if( g_numChildren == 0 )
		{
			// Throw some bones to the user to keep him occupied or disinterested.....
			cout << "Finished preprocessing its spectra; " << timer.End() << " seconds elapsed." << endl;
			cout << "Trimming spectra with less than " << g_rtConfig->minIntensityClassCount << " peaks." << endl;
			cout << "Trimming spectra with precursors too small or large: " <<
				g_rtConfig->MinPeptideMass << " - " << g_rtConfig->MaxPeptideMass << endl;
		}

		// Filter the spectra by peak count. If a spectrum doesn't have enough peaks to fill
		// out minimum number of intensity classes (user configurable) then the spectrum is
		// most likely a noisy spectrum. So, clip it off.
		int postTrimCount = spectra.filterByPeakCount( g_rtConfig->minIntensityClassCount );

		if( g_numChildren == 0 )
		{
			cout << "Trimmed " << postTrimCount << " spectra." << endl;
		}
	}


	int numSearched;
	vector< int > workerNumbers;
	
    /**!
		GetTagsFromSequence takes a peptide sequence, its mass, and generates tags of specified length
		(user configurable) from the sequence. 
	*/
	void GetTagsFromSequence(const DigestedPeptide& peptide, int tagLength, double seqMass, vector< TagInfo >& tags )
	{
        // Get the modification map and the sequence without mods.
        const ModificationMap& mods = peptide.modifications();
        string seq = peptide.sequence();
        vector<double> residueMasses(seq.length(), 0);

        ModificationMap::const_iterator nTermItr = mods.find(ModificationMap::NTerminus());
        if (nTermItr != mods.end())
            residueMasses[0] += g_rtConfig->UseAvgMassOfSequences ? nTermItr->second.averageDeltaMass()
                                                                 : nTermItr->second.monoisotopicDeltaMass(); 
        
        ModificationMap::const_iterator cTermItr = mods.find(ModificationMap::CTerminus());
        if (cTermItr != mods.end())
            residueMasses[seq.length()-1] += g_rtConfig->UseAvgMassOfSequences ? nTermItr->second.averageDeltaMass()
                                                                 : nTermItr->second.monoisotopicDeltaMass();
        for(size_t i = 0; i < residueMasses.size(); i++)
        {
            ModificationMap::const_iterator modItr = mods.find(i);
            if (modItr != mods.end())
                residueMasses[i] += g_rtConfig->UseAvgMassOfSequences ? modItr->second.averageDeltaMass()
                                                                     : modItr->second.monoisotopicDeltaMass();
            residueMasses[i] += g_rtConfig->UseAvgMassOfSequences ? AminoAcid::Info::record(seq[i]).residueFormula.molecularWeight()
                                                                  : AminoAcid::Info::record(seq[i]).residueFormula.monoisotopicMass();
        }

        size_t seqLength = seq.length();
		size_t maxTagStartIndex = seqLength - tagLength;
        float waterMass = WATER(g_rtConfig->UseAvgMassOfSequences);
        double runningNTerminalMass = residueMasses[0];
        tags.resize(peptide.sequence().length());
        for(size_t i = 1; i < maxTagStartIndex; ++i)
        {
            double tagMass = 0.0;
            for(size_t j = i; j < i+tagLength; j++)
                tagMass += residueMasses[j];

            double cTerminalMass = seqMass - runningNTerminalMass - tagMass - waterMass;
            TagInfo tag(seq.substr( i, tagLength ), runningNTerminalMass, cTerminalMass);
            tag.lowPeakMz = double(i);
            tags[i] = tag;
            runningNTerminalMass += residueMasses[i]; 
        }
	}

	/**
		ScoreKnownVariants takes a candidate peptide and delta mass. The procedure finds
		all the substitutions or preferred mass shifts that can fit the delta mass, generates 
        the variants, scores each of the generated variant against an experimental spectrum 
        and stores the results.
	*/
	inline boost::int64_t ScoreKnownModification(const DigestedPeptide& candidate, float mass, float modMass, 
												size_t locStart, size_t locEnd, Spectrum* spectrum, 
												const string& proteinId, vector<double>& sequenceIons,
                                                float massTol, int NTT, bool isDecoy)
    {
        typedef boost::shared_ptr<SearchResult> SearchResultPtr;

		boost::int64_t numComparisonsDone = 0;
		//cout << "\t\t\t\t\t" << candidate.sequence() << "," << modMass << "," << locStart << "," << locEnd << endl;
		// Get all possible amino acid substitutions that fit the modification mass with in the mass tolerance
		DynamicModSet possibleModifications;
        size_t numDynamicMods = candidate.modifications().size();
        // Get all possible amino acid substitutions or preferred mass shifts 
        // that fit the mod mass within the tolerance
        size_t maxCombin = 1 , minCombin = 1;
        if(g_rtConfig->unknownMassShiftSearchMode == MUTATIONS)
            possibleModifications = deltaMasses->getPossibleSubstitutions(modMass, massTol);
        else if(g_rtConfig->unknownMassShiftSearchMode == PREFERRED_DELTA_MASSES)
            possibleModifications = g_rtConfig->preferredDeltaMasses.getMatchingMassShifts(modMass, massTol, maxCombin, minCombin);
            
		if(possibleModifications.size() == 0)
            return numComparisonsDone;

        // Generate variants of the current peptide using the possible substitutions
        vector <DigestedPeptide> possibleVariants;
        MakePeptideVariants(candidate, possibleVariants, minCombin, maxCombin, possibleModifications, locStart, locEnd);
        // For each variant
        for(size_t aVariantIndex = 0; aVariantIndex < possibleVariants.size(); aVariantIndex++)
        {
            const DigestedPeptide& variant = possibleVariants[aVariantIndex];
            // Check to make sure that the insertion of sub or preferred PTM doesn't put the mass 
            // of the peptide over the precursor mass tolerance.
            float neutralMass = g_rtConfig->UseAvgMassOfSequences ? ((float) variant.molecularWeight(0,true))
                : (float) variant.monoisotopicMass(0,true);
            float massDiff = fabs(neutralMass - spectrum->mOfPrecursor);
            if(massDiff > g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1])
                continue;

            string variantSequence = PEPTIDE_N_TERMINUS_SYMBOL + variant.sequence() + PEPTIDE_C_TERMINUS_SYMBOL;
            //cout << "\t\t\t\t\t" << tagrecon::getInterpretation(const_cast <DigestedPeptide&>(variant)) << endl;
            // Initialize the result
            SearchResultPtr resultPtr(new SearchResult(variant));
            SearchResult& result = *resultPtr;
            result.numberOfBlindMods = 0;
            result.numberOfOtherMods = numDynamicMods;
            result.precursorMassHypothesis.mass = spectrum->mOfPrecursor;
            result.precursorMassHypothesis.massType = g_rtConfig->UseAvgMassOfSequences ? MassType_Average : MassType_Monoisotopic;
            result.precursorMassHypothesis.charge = spectrum->id.charge;

            // Compute the predicted spectrum and score it against the experimental spectrum
            CalculateSequenceIons( variant, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
            spectrum->ScoreSequenceVsSpectrum( result, sequenceIons, NTT );

            // Compute the true modification mass. The modMass of in the arguments is used to look up
            // the canidate mods with a certain tolerance. It's not the true modification mass of the
            // peptide.
            float trueModificationMass = g_rtConfig->UseAvgMassOfSequences ? variant.modifications().averageDeltaMass()-candidate.modifications().averageDeltaMass() : variant.modifications().monoisotopicDeltaMass() - candidate.modifications().monoisotopicDeltaMass();

            result.massError = spectrum->mOfPrecursor-(mass+trueModificationMass);
            // Assign the peptide identification to the protein by loci
            result.proteins.insert(proteinId);
            result._isDecoy = isDecoy;
            // cout << "\t\t\t\t\t" << result.mvh << "," << result.mzFidelity << endl;

            ++ numComparisonsDone;

            // Update some search stats and add the result to the spectrum
            boost::mutex::scoped_lock lock(*spectrum->mutex);
            if( isDecoy ) 
            {
                ++ spectrum->numDecoyComparisons;
                ++ spectrum->detailedCompStats.numDecoyModComparisons;
            }
            else
            {
                ++ spectrum->numTargetComparisons;
                ++ spectrum->detailedCompStats.numTargetModComparisons;
            }
            spectrum->resultsByCharge[spectrum->id.charge-1].add( resultPtr );
        }
		return numComparisonsDone;
	}


	/**
		ScoreUnknownModification takes a peptide sequence and localizes an unknown modification mass
		to a particular residue in the sequence. The number of tested resiudes is defined by locStart
		and locEnd variables of the procedure.
	*/
	inline boost::int64_t ScoreUnknownModification(const DigestedPeptide& candidate, float mass, float modMass, 
												size_t locStart, size_t locEnd, Spectrum* spectrum, 
												const string& proteinId, vector<double>& sequenceIons,
                                                int NTT, bool isDecoy)
    {
        typedef boost::shared_ptr<SearchResult> SearchResultPtr;

		boost::int64_t numComparisonsDone = 0;
		DynamicModSet possibleInterpretations;
		string peptideSeq = PEPTIDE_N_TERMINUS_STRING + candidate.sequence() + PEPTIDE_C_TERMINUS_STRING;
		multimap<double, SearchResultPtr> localizationPossibilities;
        double topMVHScore = 0.0;
        size_t numDynamicMods = candidate.modifications().size();
        
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
		for(size_t variantIndex = 0; variantIndex < modificationVariants.size(); variantIndex++)
        {
			const DigestedPeptide& variant = modificationVariants[variantIndex];

			// Initialize search result
            SearchResultPtr resultPtr(new SearchResult(variant));
            SearchResult& result = *resultPtr;
            result.numberOfBlindMods = 1;
            result.numberOfOtherMods = numDynamicMods;
            result.precursorMassHypothesis.mass = spectrum->mOfPrecursor;
            result.precursorMassHypothesis.massType = g_rtConfig->UseAvgMassOfSequences ? MassType_Average : MassType_Monoisotopic;
            result.precursorMassHypothesis.charge = spectrum->id.charge;

			// Compute the predicted spectru;m and score it against the experimental spectrum
            CalculateSequenceIons( variant, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
			spectrum->ScoreSequenceVsSpectrum( result, sequenceIons, NTT );
			// Assign the modification mass and the mass error
			// Compute the true modification mass. The modMass in the arguments is used to look up
			// the canidate mods with a certain tolerance. It's not the true modification mass of the
			// peptide.
			float trueModificationMass = g_rtConfig->UseAvgMassOfSequences ? variant.modifications().averageDeltaMass()-candidate.modifications().averageDeltaMass() : variant.modifications().monoisotopicDeltaMass() - candidate.modifications().monoisotopicDeltaMass();

			result.massError = spectrum->mOfPrecursor-(mass+trueModificationMass);
			// Assign the peptide identification to the protein by loci
			result.proteins.insert(proteinId);
            result._isDecoy = isDecoy;
            // Score to beat
            if(topMVHScore<result.mvh)
                topMVHScore = result.mvh;
            //if(debug)
            //    cout << tagrecon::getInterpretation(const_cast <DigestedPeptide&>(variant)) << "->" << result.mvh << endl;
            // Save the localization result
            localizationPossibilities.insert(make_pair(result.mvh, resultPtr));

            ++ numComparisonsDone;
		}

        boost::mutex::scoped_lock lock(*spectrum->mutex);
        if( isDecoy ) 
        {
            spectrum->numDecoyComparisons += numComparisonsDone;
            spectrum->detailedCompStats.numDecoyModComparisons += numComparisonsDone;
        }
        else
        {
            spectrum->numTargetComparisons += numComparisonsDone;
            spectrum->detailedCompStats.numTargetModComparisons += numComparisonsDone;
        }

        // Update some search stats and add the best 
        // localization result(s) to the spectrum
        if(topMVHScore>0)
        {
            multimap<double,SearchResultPtr>::const_iterator begin = localizationPossibilities.lower_bound(topMVHScore);
            multimap<double,SearchResultPtr>::const_iterator end = localizationPossibilities.upper_bound(topMVHScore);
            
            // By default we only keep track of top 2 results for each ambiguous peptide
            int maxAmbResults = g_rtConfig->MaxAmbResultsForBlindMods;
            while(begin != end && maxAmbResults > 0)
            {
                spectrum->resultsByCharge[spectrum->id.charge-1].add(begin->second);
                ++ numComparisonsDone;
                ++ begin;
                -- maxAmbResults;
            }
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

    inline boost::int64_t QuerySequenceMassReconMode( const Peptide& protein, const DigestedPeptide& candidate, const string& proteinId, bool isDecoy, bool estimateComparisonsOnly = false )
    {
        typedef boost::shared_ptr<SearchResult> SearchResultPtr;

        bool debug = false;
        //if(candidate.sequence() == "SSQELEGSCR")
        //    debug = true;
		// Search stats
		boost::int64_t numComparisonsDone = 0;
		// Candidate peptide sequence and its mass
        const string& aSequence  = candidate.sequence();
        if(debug)
		    cout << aSequence << "," << aSequence.length() << "," << candidate.sequence() << "," << candidate.sequence().length() << endl;
        double neutralMass = g_rtConfig->UseAvgMassOfSequences ? candidate.molecularWeight()
                                                               : candidate.monoisotopicMass();

		vector< double > fragmentIonsByChargeState;
        vector< double >& sequenceIons = fragmentIonsByChargeState;

        // Number of enzymatic termini
        int NTT = candidate.specificTermini();

        BOOST_FOREACH(Spectrum* spectrum, spectra)
        {
            float modMass = ((float)spectrum->mOfPrecursor) - neutralMass;

            // Don't bother interpreting the mod mass if it's outside the user-set
            // limits.
            if( modMass < -1.0*g_rtConfig->MaxModificationMassMinus || modMass > g_rtConfig->MaxModificationMassPlus ) 
                continue;

            double tolerance = max(g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1]);

            if(g_rtConfig->unknownMassShiftSearchMode != INACTIVE)
            {
                // Check to remove unfeasible modification decorations
                bool legitimateModMass = true;
                if(g_rtConfig->unknownMassShiftSearchMode == BLIND_PTMS ||
                    g_rtConfig->unknownMassShiftSearchMode == MUTATIONS)
                    legitimateModMass = checkForModificationSanity(candidate, modMass, tolerance);

                if( legitimateModMass && fabs(modMass) > tolerance )
                {
                    // If the user configured the searches for substitutions
                    if(g_rtConfig->unknownMassShiftSearchMode == MUTATIONS ||
                        g_rtConfig->unknownMassShiftSearchMode == PREFERRED_DELTA_MASSES)
                    {
                        numComparisonsDone +=
                            ScoreKnownModification(candidate, neutralMass, modMass,
                            0, aSequence.length(), spectrum,
                            proteinId, sequenceIons,  
                            g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1],
                            NTT, isDecoy);
                    }

                    // If the user wants us to find unknown modifications.
                    if(g_rtConfig->unknownMassShiftSearchMode == BLIND_PTMS)
                    {
                        numComparisonsDone +=
                            ScoreUnknownModification(candidate, neutralMass, modMass,
                            0, aSequence.length(), spectrum, 
                            proteinId, sequenceIons, 
                            NTT, isDecoy);
                    }
                }
            }

            if( fabs(modMass) <= g_rtConfig->PrecursorMassTolerance[spectrum->id.charge-1] )
            {
                // If there are no n-terminal and c-terminal delta mass differences then
                // score the match as an unmodified sequence.

                SearchResultPtr resultPtr(new SearchResult(candidate));
                SearchResult& result = *resultPtr;
                result.numberOfBlindMods = 0;
                result.numberOfOtherMods = candidate.modifications().size();
                result.precursorMassHypothesis.mass = spectrum->mOfPrecursor;
                result.precursorMassHypothesis.massType = g_rtConfig->UseAvgMassOfSequences ? MassType_Average : MassType_Monoisotopic;
                result.precursorMassHypothesis.charge = spectrum->id.charge;

                CalculateSequenceIons( candidate, spectrum->id.charge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
                spectrum->ScoreSequenceVsSpectrum( result, sequenceIons, NTT );

                result.massError = spectrum->mOfPrecursor-neutralMass;
                result.proteins.insert(proteinId);
                result._isDecoy = isDecoy;

                ++numComparisonsDone;

                boost::mutex::scoped_lock lock(*spectrum->mutex);

                if(candidate.modifications().size()>0)
                    ++ spectrum->detailedCompStats.numDecoyModComparisons;
                else
                    ++ spectrum->detailedCompStats.numDecoyUnmodComparisons;

                if( isDecoy )
                    ++ spectrum->numDecoyComparisons;
                else
                    ++ spectrum->numTargetComparisons;

                spectrum->resultsByCharge[spectrum->id.charge-1].add( resultPtr );
            }
        }

        return numComparisonsDone;
    }

    void QueryProteinMassReconMode(const proteinData& protein)
    {
        bool isDecoy = protein.isDecoy();

        Digestion digestion( protein, g_rtConfig->cleavageAgentRegex, g_rtConfig->digestionConfig );
        for(Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr)
        {
            const DigestedPeptide& peptide = (*itr);
            if (peptide.sequence().find_first_of("BXZ") != string::npos)
                continue;
            // a selenopeptide's molecular weight can be lower than its monoisotopic mass!
            double minMass = min(peptide.monoisotopicMass(), peptide.molecularWeight());
            double maxMass = max(peptide.monoisotopicMass(), peptide.molecularWeight());

            if( minMass > g_rtConfig->curMaxPeptideMass || maxMass < g_rtConfig->curMinPeptideMass )
                continue;

            vector<DigestedPeptide> digestedPeptides;
            PTMVariantList variantIterator( peptide, g_rtConfig->MaxDynamicMods, g_rtConfig->dynamicMods, g_rtConfig->staticMods, g_rtConfig->MaxPeptideVariants);
            if(variantIterator.isSkipped)
            {
                ++ searchStatistics.numCandidatesSkipped;
                continue;
            }

            variantIterator.getVariantsAsList(digestedPeptides);
            searchStatistics.numCandidatesGenerated += digestedPeptides.size();

            for( size_t j=0; j < digestedPeptides.size(); ++j )
            {
                boost::int64_t queryComparisonCount = QuerySequenceMassReconMode( protein,
                    digestedPeptides[j],
                    protein.getName(),
                    isDecoy,
                    g_rtConfig->EstimateSearchTimeOnly );
                if( queryComparisonCount > 0 )
                {
                    searchStatistics.numComparisonsDone += queryComparisonCount;
                    ++searchStatistics.numCandidatesQueried;
                }
            }
        }
    }

    typedef flat_map<size_t, shared_ptr<AATagToSpectraMap> > TagMatches;

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
    inline boost::int64_t QueryPeptideTagRecon(const Peptide& protein, const DigestedPeptide& candidate, 
                                               const string& proteinId, bool isDecoy, 
                                               TagMatches::const_iterator tagMatchesBegin, 
                                               const TagMatches::const_iterator tagMatchesEnd,
                                               bool estimateComparisonsOnly = false)
    {
        typedef boost::shared_ptr<SearchResult> SearchResultPtr;

        //bool debug = false;
        //if(candidate.sequence() == "NDKSEEEQSSSSVK")
        //    debug = true;
		// Search stats
		boost::int64_t numComparisonsDone = 0;
		// Candidate peptide sequence and its mass
        const string& aSequence  = candidate.sequence();
        double neutralMass = g_rtConfig->UseAvgMassOfSequences ? candidate.molecularWeight()
                                                               : candidate.monoisotopicMass();

		vector< double > fragmentIonsByChargeState;
        vector< double >& sequenceIons = fragmentIonsByChargeState;

        // Number of enzymatic termini
        int NTT = candidate.specificTermini();

		// Get tags of length 3 from the candidate peptide sequence
		vector< TagInfo > candidateTags;
        GetTagsFromSequence( candidate, 3, neutralMass, candidateTags );
        
        // Store all tag matches and their modification specific attributes
        flat_set<TagMatchInfo> tagMatches;
        flat_map<TagMatchInfo,size_t> lowIndex;
        flat_map<TagMatchInfo,size_t> highIndex;
        flat_map<TagMatchInfo,float> substitutionLookupTolerance;
        
        // For each of the tag matched index
        for(;tagMatchesBegin != tagMatchesEnd; ++tagMatchesBegin)
        {
            size_t tagMatchStart = (*tagMatchesBegin).first-candidate.offset();
            if(tagMatchStart < 1 || tagMatchStart > aSequence.length()-4)
                continue;
            const shared_ptr<AATagToSpectraMap>& matchedTags = (*tagMatchesBegin).second;
            double peptidePrefixMass = candidateTags[tagMatchStart].nTerminusMass;
            double peptideSuffixMass = candidateTags[tagMatchStart].cTerminusMass;
            
            BOOST_FOREACH(const TagSpectrumInfo& tag, matchedTags->tags)
            {
                float nTerminusDeviation = fabs( tag.nTerminusMass - peptidePrefixMass );
				float cTerminusDeviation = fabs( tag.cTerminusMass - peptideSuffixMass );
                
				if(nTerminusDeviation+cTerminusDeviation >= g_rtConfig->MaxModificationMassPlus) 
					continue;
               
                // Get the charge state of the fragment ions that gave rise to the tag
                int tagCharge = tag.tagChargeState;
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
				Spectrum* spectrum = *tag.sItr;
				float modMass = spectrum->mOfPrecursor - neutralMass;

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
                    tagMisMatchHighIndex = (size_t) candidateTags[tagMatchStart].lowPeakMz-1;
                    subMassTol = g_rtConfig->NTerminalMassTolerance[tagCharge-1];
                } else if(cTermMatch == MASS_MISMATCH)
                {
                    // These indexes are used to move the mod around in the "blind PTM" mode.
                    tagMisMatchLowIndex = (size_t) candidateTags[tagMatchStart].lowPeakMz + 3;
                    tagMisMatchHighIndex = aSequence.length();
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
        for(flat_set<TagMatchInfo>::const_iterator mItr = tagMatches.begin(); mItr != tagMatches.end(); ++mItr)
        {
            TagMatchInfo tagMatch = (*mItr);

            Spectrum* spectrum = tagMatch.spectrum;
            int spectrumCharge = spectrum->id.charge;

            if(tagMatch.nTermMatch == MASS_MATCH && tagMatch.cTermMatch == MASS_MATCH && 
                fabs(tagMatch.modificationMass) <=g_rtConfig->PrecursorMassTolerance[spectrumCharge-1])
            {
                // If there are no n-terminal and c-terminal delta mass differences then
                // score the match as an unmodified sequence.
                
                SearchResultPtr resultPtr(new SearchResult(candidate));
                SearchResult& result = *resultPtr;
                result.numberOfBlindMods = 0;
                result.numberOfOtherMods = candidate.modifications().size();
                result.precursorMassHypothesis.mass = spectrum->mOfPrecursor;
                result.precursorMassHypothesis.massType = g_rtConfig->UseAvgMassOfSequences ? MassType_Average : MassType_Monoisotopic;
                result.precursorMassHypothesis.charge = spectrum->id.charge;

                CalculateSequenceIons( candidate, spectrumCharge, &sequenceIons, spectrum->fragmentTypes, g_rtConfig->UseSmartPlusThreeModel, 0, 0);
                spectrum->ScoreSequenceVsSpectrum( result, sequenceIons, NTT );

                result.massError = spectrum->mOfPrecursor - neutralMass;
				result.proteins.insert(proteinId);
                result._isDecoy = isDecoy;

                ++numComparisonsDone;

                boost::mutex::scoped_lock lock(*spectrum->mutex);

                if(candidate.modifications().size()>0)
                    ++ spectrum->detailedCompStats.numDecoyModComparisons;
                else
                    ++ spectrum->detailedCompStats.numDecoyUnmodComparisons;

                if( isDecoy )
                    ++ spectrum->numDecoyComparisons;
                else
                    ++ spectrum->numTargetComparisons;

                spectrum->resultsByCharge[spectrum->id.charge-1].add( resultPtr );
            } 
            else if(g_rtConfig->unknownMassShiftSearchMode !=  INACTIVE)
            {
                // Make sure we are looking at a real mod
                double modMassTolerance = max(g_rtConfig->MinModificationMass, g_rtConfig->PrecursorMassTolerance[spectrumCharge-1]);
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
                if(g_rtConfig->unknownMassShiftSearchMode == PREFERRED_DELTA_MASSES) 
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
                        g_rtConfig->unknownMassShiftSearchMode == PREFERRED_DELTA_MASSES) 
                    {
                        // Find the substitutions or PDMs that fit the mass, generate variants and score them.
                        numComparisonsDone += 
                            ScoreKnownModification(candidate, neutralMass, tagMatch.modificationMass, modLowIndex, 
                                                   modHighIndex,spectrum, proteinId, sequenceIons, 
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
                                                     modHighIndex, spectrum, proteinId, sequenceIons,	
                                                     NTT, isDecoy);
                    }
                } 
            }
        }

		return numComparisonsDone;

    }

    void QueryProteinTagReconMode(const proteinData& protein)
    {
        
        bool isDecoy = protein.isDecoy();
        Peptide pwizProtein(protein.getSequence());
        // Find all tag matches in the protein sequence
        TagMatches tagMatches;
        BOOST_FOREACH(const SpectraTagTrie::SearchResult& tagMatch, spectraTagTrie.find_all(protein.getSequence()))
            tagMatches[tagMatch.offset()] = static_cast<const shared_ptr<AATagToSpectraMap>&>(tagMatch.keyword());
        // Digest the protein
        Digestion digestion( protein, g_rtConfig->cleavageAgentRegex, g_rtConfig->digestionConfig );
        for( Digestion::const_iterator dItr = digestion.begin(); dItr != digestion.end(); ++dItr)
        {
            if (dItr->sequence().find_first_of("BXZ") != string::npos)
                continue;

            // a selenopeptide's molecular weight can be lower than its monoisotopic mass!
            double minMass = min(dItr->monoisotopicMass(), dItr->molecularWeight());
            double maxMass = max(dItr->monoisotopicMass(), dItr->molecularWeight());

            if( minMass > g_rtConfig->curMaxPeptideMass || maxMass < g_rtConfig->curMinPeptideMass )
                continue;
            
            PTMVariantList variantIterator( (*dItr), g_rtConfig->MaxDynamicMods, g_rtConfig->dynamicMods, g_rtConfig->staticMods, g_rtConfig->MaxPeptideVariants);
            if(variantIterator.isSkipped)
            {
                ++ searchStatistics.numCandidatesSkipped;
                continue;
            }
            
            vector<DigestedPeptide> peptideVariants;
            variantIterator.getVariantsAsList(peptideVariants);
            searchStatistics.numCandidatesGenerated += peptideVariants.size();
            
            size_t nterminalOffset = dItr->offset() ;
            size_t cTerminalOffset = dItr->offset() + dItr->sequence().length();
            TagMatches::const_iterator tIterBegin = tagMatches.lower_bound(nterminalOffset + 1);
            TagMatches::const_iterator tIterEnd = tagMatches.upper_bound(cTerminalOffset - 1);
            BOOST_FOREACH(const DigestedPeptide& variant, peptideVariants)
            {
                boost::int64_t queryComparisonCount = QueryPeptideTagRecon( protein, variant, protein.getName(),
                                                                            isDecoy, tIterBegin, tIterEnd,
                                                                            g_rtConfig->EstimateSearchTimeOnly );
                if( queryComparisonCount > 0 )
                {
                    searchStatistics.numComparisonsDone += queryComparisonCount;
                    ++searchStatistics.numCandidatesQueried;
                }
            }
        }
    }

	/**!
		ExecuteSearchThread function takes a thread, figures out which part of the protein database
		needs to be searched with the thread, generates the peptide sequences for the candidate 
		protein sequences, and searches them with the tags and the spectra that generated the tags.
	*/
	void ExecuteSearchThread()
	{
        try
        {
            size_t proteinTask;
		    while( true )
		    {
                if (!proteinTasks.dequeue(&proteinTask))
				    break;

			    ++ searchStatistics.numProteinsDigested;
                proteinData protein = proteins[proteinTask];
                if (!g_rtConfig->ProteinListFilters.empty() && g_rtConfig->ProteinListFilters.find(protein.getName()) == string::npos)
                    continue;

                if(g_rtConfig->MassReconMode)
                {
                    QueryProteinMassReconMode(protein);
                    continue;
                }
                QueryProteinTagReconMode(protein);
            }
        } catch( std::exception& e )
        {
            cerr << " terminated with an error: " << e.what() << endl;
        } catch(...)
        {
            cerr << " terminated with an unknown error." << endl;
        }
	}

	/**!
		ExecuteSearch function determines the number of workers depending
		on the number of CPUs present in a box and create threads for each
		of the CPU. This function treats each multi-core CPU as a multi-processor
		machine.
	*/
	void ExecuteSearch()
	{
		size_t numProcessors = (size_t) g_numWorkers;
        boost::uint32_t numProteins = (boost::uint32_t) proteins.size();

		for (size_t i=0; i < numProteins; ++i)
			proteinTasks.enqueue(i);

        bpt::ptime start = bpt::microsec_clock::local_time();

        boost::thread_group workerThreadGroup;
        vector<boost::thread*> workerThreads;

		for (size_t i = 0; i < numProcessors; ++i)
            workerThreads.push_back(workerThreadGroup.create_thread(&ExecuteSearchThread));

        if (g_numChildren > 0)
        {
            // MPI jobs do a simple join_all
            workerThreadGroup.join_all();

            // xcorrs are calculated just before sending back results
        }
        else
        {
            bpt::ptime lastUpdate = start;

            for (size_t i=0; i < numProcessors; ++i)
            {
                // returns true if the thread finished before the timeout;
                // (each thread index is joined until it finishes)
                if (!workerThreads[i]->timed_join(bpt::seconds(round(g_rtConfig->StatusUpdateFrequency))))
                    --i;

                bpt::ptime current = bpt::microsec_clock::local_time();

                // only make one update per StatusUpdateFrequency seconds
                if ((current - lastUpdate).total_microseconds() / 1e6 < g_rtConfig->StatusUpdateFrequency)
                    continue;

                lastUpdate = current;
                bpt::time_duration elapsed = current - start;

			    float proteinsPerSec = static_cast<float>(searchStatistics.numProteinsDigested) / elapsed.total_microseconds() * 1e6;
                bpt::time_duration estimatedTimeRemaining(0, 0, round((numProteins - searchStatistics.numProteinsDigested) / proteinsPerSec));

		        cout << "Searched " << searchStatistics.numProteinsDigested << " of " << numProteins << " proteins; "
                     << round(proteinsPerSec) << " per second, "
                     << format_date_time("%H:%M:%S", bpt::time_duration(0, 0, elapsed.total_seconds())) << " elapsed, "
                     << format_date_time("%H:%M:%S", estimatedTimeRemaining) << " remaining." << endl;

		        //float candidatesPerSec = threadInfo->stats.numComparisonsDone / totalSearchTime;
		        //float estimatedTimeRemaining = float( numCandidates - threadInfo->stats.numComparisonsDone ) / candidatesPerSec / numThreads;
		        //cout << threadInfo->workerHostString << " has made " << threadInfo->stats.numComparisonsDone << " of about " << numCandidates << " comparisons; " <<
		        //		candidatesPerSec << " per second, " << estimatedTimeRemaining << " seconds remaining." << endl;
		    }

            // compute xcorr for top ranked results
            if( g_rtConfig->ComputeXCorr )
                ComputeXCorrs();
        }
	}

    void ExecuteNETThread(size_t start, size_t end, vector<double>& NETStats)
    {
        // Digest the proteins given the thread and accumulate the total numbers 
        // of peptides seen in each NET class
        for(size_t index=start; index <= end; ++index)
        {
            Peptide protein(proteins[index].getSequence());
            Digestion digestion( protein, g_rtConfig->cleavageAgentRegex, g_rtConfig->digestionConfig );
            for( Digestion::const_iterator itr = digestion.begin(); itr != digestion.end(); ++itr ) 
                ++NETStats[itr->specificTermini()];
        }
    }
    
    /* This function randomly samples 
    */
    void ComputeNETProbabilities()
    {
        g_rtConfig->NETRewardVector.resize(3);
        fill(g_rtConfig->NETRewardVector.begin(), g_rtConfig->NETRewardVector.end(), 0);
        if(!g_rtConfig->UseNETAdjustment)
            return;
        
        cout << "Computing NET probabilities." << endl;
        Timer timer;
        timer.Begin();
        // Shuffle the proteins, select 5% of the total, and distribute them between workers
        proteins.random_shuffle();
        size_t numNETWorkers = g_numWorkers;
        size_t proteinsPerWorker = (size_t) ((proteins.size() * 0.05)/numNETWorkers);

        // one NET stats vector per thread
        vector<vector<double> > NETStatsByThread(numNETWorkers, vector<double>(3, 0));

        boost::thread_group workerThreadGroup;
        vector<boost::thread*> workerThreads;

        size_t proteinStartIndex = 0;
		for (size_t i = 0; i < numNETWorkers; ++i)
        {
            workerThreads.push_back(new boost::thread(&ExecuteNETThread,
                                                      proteinStartIndex,
                                                      proteinStartIndex+proteinsPerWorker,
                                                      NETStatsByThread[i]));
            workerThreadGroup.add_thread(workerThreads.back());
            proteinStartIndex += proteinsPerWorker;
        }

        workerThreadGroup.join_all();
        
        // Accumulate the total numbers of peptides in each NET class.
        for (size_t i=0; i < numNETWorkers; ++i)
            for(size_t net=0; net < 3; ++net)
                g_rtConfig->NETRewardVector[net] += NETStatsByThread[i][net];

        // Normalize and compute the log probability of findind a peptide in each class by random chance
        double sum = accumulate(g_rtConfig->NETRewardVector.begin(), g_rtConfig->NETRewardVector.end(), 0.0);
        for(size_t index=0; index < g_rtConfig->NETRewardVector.size(); ++index) 
            if(g_rtConfig->NETRewardVector[index] > 0)
                g_rtConfig->NETRewardVector[index]=log(g_rtConfig->NETRewardVector[index]/sum);
        
        cout << "Finished computing NET probabilities; " << timer.End() << " seconds elapsed." << endl;

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
            estimatedResultsSize += g_rtConfig->MaxResultRank;
            if(estimatedResultsSize>g_rtConfig->ResultsPerBatch) 
            {
                batches.push_back(current);
                current.reset(new SpectraList());
                estimatedResultsSize = g_rtConfig->MaxResultRank;
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
				cout << "No spectra files found matching given filemasks." << endl;
				return 1;
			}

			// Test for the protein database format
			if( !TestFileType( g_dbFilename, "fasta" ) )
				return 1;

			cout << "Reading \"" << g_dbFilename << "\"" << endl;
			Timer readTime(true);
			// Read the protein database 
			try
			{
                proteins = proteinStore( g_rtConfig->decoyPrefix );
                proteins.readFASTA( g_dbFilename );
			} catch( exception& e )
			{
				cout << "Error: " << e.what() << endl;
				return 1;
			}
			cout << "Read " << proteins.size() << " proteins; " << readTime.End() << " seconds elapsed." << endl;

			proteins.random_shuffle(); // randomize order to optimize work distribution
            ComputeNETProbabilities(); // Compute the penalties for a peptide's enzymatic status
			// Split the database into multiple parts to distrubute it over the cluster
			#ifdef USE_MPI
				if( g_numChildren > 0 )
				{
					g_rtConfig->ProteinBatchSize = (int) ceil( (float) proteins.size() / (float) g_numChildren / (float) g_rtConfig->NumBatches );
                    cout << "Dynamic protein batch size is " << g_rtConfig->ProteinBatchSize << endl;
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

                searchStatistics = SearchStatistics();

				cout << "Reading spectra from file \"" << *fItr << "\"" << endl;
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
                    sourceFilepath = spectra.readTags( *fItr, true );

                // Try to find the source next to the tags
                if( !bfs::exists(sourceFilepath) )
                {
                    string sourceFilename = bfs::path(sourceFilepath).filename();
                    bfs::path tagsFilepath = bfs::path(*fItr);
                    bfs::path adjacentSourceFilepath = tagsFilepath.parent_path() / sourceFilename;
                    if( bfs::exists(adjacentSourceFilepath) )
                        sourceFilepath = adjacentSourceFilepath.string();
                    else
                    {
                        cerr << "Error: could not find source \"" + sourceFilename + "\" for tags file \"" + tagsFilepath.filename() + "\"" << endl;
                        continue;
                    }
                }


				// Set the parameters for the search
				g_rtConfig->setVariables( varsFromFile );

				// Read peaks from the source data of the tags file
				spectra.readPeaks( sourceFilepath, 0, -1, 2, g_rtConfig->SpectrumListFilters );

				// Count total number of peaks in all the spectra in the current file
				int totalPeakCount = 0;
				for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
					totalPeakCount += (*sItr)->peakPreCount;

				cout << "Read " << spectra.size() << " spectra with " << totalPeakCount << " peaks; " << readTime.End() << " seconds elapsed." << endl;

				// Skip empty spectra
				int skip = 0;
				if( spectra.empty() )
				{
					cout << "Skipping a file with no spectra." << endl;
					skip = 1;
				}

				// If the program is running on a cluster then determine
				// the optimal batch size for sending the spectra over
				// to the other processors
				#ifdef USE_MPI
					if( g_numChildren > 0 )
					{
						g_rtConfig->SpectraBatchSize = (int) ceil( (float) spectra.size() / (float) g_numChildren / (float) g_rtConfig->NumBatches );
                        cout << "Dynamic spectra batch size is " << g_rtConfig->SpectraBatchSize << endl;
					}

					// Send the skip variable to all child processes
					for( int p=0; p < g_numChildren; ++p )
						MPI_Ssend( &skip,		1,		MPI_INT,	p+1, 0x00, MPI_COMM_WORLD );
                    
                    TransmitNETRewardsToChildProcess();
				#endif

				Timer searchTime;
				string startTime;
				string startDate;

				vector< size_t > opcs; // original peak count statistics
				vector< size_t > fpcs; // filtered peak count statistics

				if( !skip )
				{
					// If the current process is a parent process
					if( g_numProcesses > 1 && !g_rtConfig->EstimateSearchTimeOnly )
					{
						#ifdef USE_MPI
							//Use the child processes to prepare the spectra
							cout << "Sending spectra to worker nodes to prepare them for search." << endl;
							Timer prepareTime(true);
							TransmitUnpreparedSpectraToChildProcesses();

							spectra.clear();
                            spectraTagTrie.clear();

							ReceivePreparedSpectraFromChildProcesses();

							numSpectra = (int) spectra.size();

							skip = 0;
							if( numSpectra == 0 )
							{
								cout << "Skipping a file with no suitable spectra." << endl;
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
								cout << "Mean original (filtered) peak count: " << opcs[5] << " (" << fpcs[5] << ")" << endl;
								cout << "Min/max original (filtered) peak count: " << opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
								cout << "Original (filtered) peak count at 1st/2nd/3rd quartiles: " <<
										opcs[2] << " (" << fpcs[2] << "), " <<
										opcs[3] << " (" << fpcs[3] << "), " <<
										opcs[4] << " (" << fpcs[4] << ")" << endl;

								float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
								cout << "Filtered out " << filter * 100.0f << "% of peaks." << endl;

								cout << "Prepared " << spectra.size() << " spectra; " << prepareTime.End() << " seconds elapsed." << endl;
                                if(!g_rtConfig->MassReconMode) 
                                {
								    cout << "Reading tags for " << spectra.size() << " prepared spectra." << endl;
								    size_t totalTags = 0;
								    // Read the tags in the input file
								    spectra.readTags( *fItr, true );
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
								    cout << "Finished reading " << totalTags << " tags." << endl;

								    cout << "Trimming spectra with no tags." << endl;
								    int noTagsCount = spectra.trimByTagCount();
								    cout << "Trimmed " << noTagsCount << " spectra." << endl;
                                }
								// Initialize few global data structures. See function documentation
								// for details
								InitWorkerGlobals();

                                // List to store finished spectra
                                SpectraList finishedSpectra;
                                // Split the spectra into batches if needed
                                vector<SpectraListPtr> batches = estimateSpectralBatches();
                                if(batches.size()>1)
                                    cout << "Splitting spectra into " << batches.size() << " batches for search." << endl;
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
                                    cout << "Sending some prepared spectra to all worker nodes from a pool of " << spectra.size() << " spectra" << batchString.str() << "." << endl;
                                    try
                                    {
                                        Timer sendTime(true);
                                        numSpectra = TransmitSpectraToChildProcesses(lastBatch);
                                        cout << "Finished sending " << numSpectra << " prepared spectra to all worker nodes; " <<
                                            sendTime.End() << " seconds elapsed." << endl;
                                    } catch( std::exception& e )
                                    {
                                        cout << g_hostString << " had an error transmitting prepared spectra: " << e.what() << endl;
                                        MPI_Abort( MPI_COMM_WORLD, 1 );
                                    }
                                    // Transmit the proteins and start the search.
                                    cout << "Commencing database search on " << numSpectra << " spectra" << batchString.str() << "." << endl;
                                    try
                                    {
                                        Timer batchTimer(true); batchTimer.Begin();
                                        TransmitProteinsToChildProcesses();
                                        cout << "Finished database search; " << batchTimer.End() << " seconds elapsed" << batchString.str() << "." << endl;
                                    } catch( std::exception& e )
                                    {
                                        cout << g_hostString << " had an error transmitting protein batches: " << e.what() << endl;
                                        MPI_Abort( MPI_COMM_WORLD, 1 );
                                    }
                                    // Get the results
                                    cout << "Receiving search results for " << numSpectra << " spectra" << batchString.str() << "." << endl;
                                    try
                                    {
                                        Timer receiveTime(true);
                                        ReceiveResultsFromChildProcesses((*bItr) == batches.front());
                                        cout << "Finished receiving search results; " << receiveTime.End() << " seconds elapsed." << endl;
                                    } catch( std::exception& e )
                                    {
                                        cout << g_hostString << " had an error receiving results: " << e.what() << endl;
                                        MPI_Abort( MPI_COMM_WORLD, 1 );
                                    }
                                    cout << "Overall stats: " << (string) searchStatistics << endl;

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
						cout << "Preparing " << spectra.size() << " spectra." << endl;
						Timer prepareTime(true);
						// Preprocess the spectra
						PrepareSpectra();
						cout << "Finished preparing spectra; " << prepareTime.End() << " seconds elapsed." << endl;

						// Get number of spectra that survived the preprocessing ;-)
						numSpectra = (int) spectra.size();

						skip = 0;
						if( spectra.empty() )
						{
							cout << "Skipping a file with no suitable spectra." << endl;
							skip = 1;
						}

						if( !skip )
						{
							// If the data file has some spectra and if the process is being
							// run on a single node then perform the search

							// Some stats for the user!!
							opcs = spectra.getOriginalPeakCountStatistics();
							fpcs = spectra.getFilteredPeakCountStatistics();
							cout << "Mean original (filtered) peak count: " << opcs[5] << " (" << fpcs[5] << ")" << endl;
							cout << "Min/max original (filtered) peak count: " << opcs[0] << " (" << fpcs[0] << ") / " << opcs[1] << " (" << fpcs[1] << ")" << endl;
							cout << "Original (filtered) peak count at 1st/2nd/3rd quartiles: " <<
									opcs[2] << " (" << fpcs[2] << "), " <<
									opcs[3] << " (" << fpcs[3] << "), " <<
									opcs[4] << " (" << fpcs[4] << ")" << endl;

							float filter = 1.0f - ( (float) fpcs[5] / (float) opcs[5] );
							cout << "Filtered out " << filter * 100.0f << "% of peaks." << endl;

                            if( !g_rtConfig->MassReconMode )
                            {
							    cout << "Reading tags for " << spectra.size() << " prepared spectra." << endl;
							    size_t totalTags = 0;
                            
							    // Read the tags from the input file
                                spectra.readTags( *fItr, true );
							    // For each spectrum
							    for( SpectraList::iterator sItr = spectra.begin(); sItr != spectra.end(); ++sItr )
							    {
								    // Get the number of tags and generate new tags for tags containing I/L
								    (*sItr)->tagList.max_size( g_rtConfig->MaxTagCount );
								    for( TagList::iterator tItr = (*sItr)->tagList.begin(); tItr != (*sItr)->tagList.end(); ++tItr )
									    (*sItr)->tagList.tagExploder( *tItr );
								    totalTags += (*sItr)->tagList.size();
							    }
							    cout << "Finished reading " << totalTags << " tags." << endl;

							    cout << "Trimming spectra with no tags." << endl;
							    // Delete spectra that has no tags
							    int noTagsCount = spectra.trimByTagCount();
							    cout << "Trimmed " << noTagsCount << " spectra." << endl;
                            }

						    // Initialize global data structures.
                            // Must be done after spectra charge states are determined and tags are read
						    InitWorkerGlobals();

							cout << "Commencing database search on " << spectra.size() << " spectra." << endl;
							startTime = GetTimeString(); startDate = GetDateString(); searchTime.Begin();
							// Start the threads
							ExecuteSearch();
							cout << "Finished database search; " << searchTime.End() << " seconds elapsed." << endl;
							cout << "Overall stats: " << (string) searchStatistics << endl;

							// Free global variables
							DestroyWorkerGlobals();
						}
					}

					if( !skip )
					{
						// Generate an output file for each input file
						WriteOutputToFile( sourceFilepath, startTime, startDate, searchTime.End(), opcs, fpcs, searchStatistics );
						cout << "Finished file \"" << *fItr << "\"; " << fileTime.End() << " seconds elapsed." << endl;
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
						//	cout << preparedSpectra[i]->id.nativeID << " " << preparedSpectra[i]->peakData.size() << endl;

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
                                try
                                {
                                    // Get a batch of protein sequences from root process
                                    while( ReceiveProteinBatchFromRootProcess() )
                                    {
                                        ++ numBatches;

                                        ExecuteSearch();
                                    }
                                } catch(std::exception& e )
                                {
                                    cout << g_hostString << " had an error receiving protein batch: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }

								cout << g_hostString << " stats: " << numBatches << " batches; " << (string) searchStatistics << endl;
                                try
                                {
                                    // compute xcorr for top ranked results
                                    if( g_rtConfig->ComputeXCorr )
                                        ComputeXCorrs();

                                    // Send results back to the parent process
                                    TransmitResultsToRootProcess();
                                } catch( std::exception& e )
                                {
                                    cout << g_hostString << " had an error transmitting results: " << e.what() << endl;
                                    MPI_Abort( MPI_COMM_WORLD, 1 );
                                }

								// Clean up the variables.
								DestroyWorkerGlobals();
								spectra.clear();
                                spectraTagTrie.clear();
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
