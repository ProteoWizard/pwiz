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

// Globals defined for common usage

#include "stdafx.h"
#include "shared_defs.h"
#include "shared_funcs.h"

using namespace freicore;

namespace freicore
{
	fileList_t			g_inputFilenames;
	string				g_dbFilename;	// name of FASTA file, e.g. "file.fasta"
	string				g_dbPath;		// path to FASTA database, e.g. "/dir"
    string              g_spectralLibName;

	ResidueMap*			g_residueMap;
	BaseRunTimeConfig*	g_rtSharedConfig;
	lnFactorialTable	g_lnFactorialTable;

	bool				g_normalizeOnMode;

	int					g_pid;
	int					g_endianType;
	int					g_numProcesses;
	int					g_numChildren;
	int					g_numWorkers;
	string				g_hostString;

	vector< Profiler >	g_profilers;

	#ifdef USE_MPI
		MPI_Status		st;
		void*			g_mpiBuffer;
		MPI_Datatype	mpi_flatSpectrum;
		MPI_Datatype	mpi_flatPeakData;
		MPI_Datatype	mpi_flatTagInfo;
	#endif

	ResidueFilter::operator string () const
	{
		stringstream ostr;
		for( size_t i=20; i < 128; ++i )
			if( m_filter[i] )
				ostr << (char) i;
		return ostr.str();
	}

	const char UniqueModCharList[] = "0123456789`~!@#$%^&*-_=|:;,./?abcdefghjiklmnopqrstuvwxyz";

	void DynamicModSet::clear()
	{
		set<DynamicMod>::clear();
		uniqueToUserMap.clear();
		userToUniqueMap.clear();
	}

	void DynamicModSet::erase( const DynamicMod& mod )
	{
	}

	SetInsertPair(set<DynamicMod>) DynamicModSet::insert( const DynamicMod& mod )
	{
		// Is the mod's userModChar already used as either a user mod char or a unique mod char?
		// - if true and it's for the same unmodChar and modMass, it's a duplicate mod: return existing itr and false
		// - else: assign a new uniqueModChar and insert as usual
		if( userToUniqueMap[mod.userModChar].size() > 0 )
		{
			vector< DynamicMod >& modsAtUserChar = userToUniqueMap[mod.userModChar];
			for( size_t i=0; i < modsAtUserChar.size(); ++i )
				if( modsAtUserChar[i].unmodChar == mod.unmodChar && modsAtUserChar[i].modMass == mod.modMass )
					return SetInsertPair(set<DynamicMod>)( set<DynamicMod>::find( modsAtUserChar[i] ), false );
		}
		const_cast< DynamicMod& >( mod ).uniqueModChar = UniqueModCharList[ uniqueToUserMap.size() ];

		// Store the mapping between the user and unique mod chars, and insert the mod as usual
		uniqueToUserMap[mod.uniqueModChar] = mod;
		userToUniqueMap[mod.userModChar].push_back( mod );
		return set<DynamicMod>::insert( mod );
	}

	void DynamicModSet::initialize( const string& cfgStr, bool noUserChar )
	{
		boost::char_separator<char> delim(" ");
		stokenizer parser( cfgStr.begin(), cfgStr.begin() + cfgStr.length(), delim );
		stokenizer::iterator itr = parser.begin();
		while( itr != parser.end() )
		{
			string motif = *itr;
			char userModChar = '@';
            if(!noUserChar)
                userModChar = (*(++itr))[0];
			double modMass = lexical_cast<double>( *(++itr) );
			parseMotif( motif, userModChar, modMass );
			++itr;
		}
	}

	DynamicModSet::operator string () const
	{
		stringstream modStr;
		for( const_iterator itr = begin(); itr != end(); ++itr )
		{
			modStr << ( itr == begin() ? "" : " " );

			for( size_t i=0; i < itr->NTerminalFilters.size(); ++i )
			{
				size_t filterSize = itr->NTerminalFilters[i].m_filter.size();
				if( filterSize == 0 )
					continue;
				else if( filterSize == 1 )
					modStr << (string) itr->NTerminalFilters[i];
				else
					modStr << '[' << (string) itr->NTerminalFilters[i] << ']';
			}

			modStr << itr->unmodChar;
			if( !itr->CTerminalFilters.empty() )
				modStr << '!';

			for( size_t i=0; i < itr->CTerminalFilters.size(); ++i )
			{
				size_t filterSize = itr->CTerminalFilters[i].m_filter.size();
				if( filterSize == 0 )
					continue;
				else if( filterSize == 1 )
					modStr << (string) itr->CTerminalFilters[i];
				else
					modStr << '[' << (string) itr->CTerminalFilters[i] << ']';
			}

			modStr << " " << itr->userModChar << " " << itr->modMass;
		}
		return modStr.str();
	}

	void DynamicModSet::parseMotif( const string& motif, char modChar, double modMass )
	{
		vector<DynamicMod> mods;
		vector<AminoAcidResidue> multiResidueBlock;
		int multiResidueBlockMode = 0; // 0=off 1=contained residues are included 2=contained residues are excluded
		bool hasModifiedSite = false;
		vector<ResidueFilter> NTerminalFilters;
		vector<ResidueFilter> CTerminalFilters;
		ResidueMap defaultResidueMap;

		for( size_t i=0; i < motif.size(); ++i )
		{
			switch( motif[i] )
			{
				case '{':
				case '[':
					// start multi residue block
					if( multiResidueBlockMode > 0 )
					{
						cerr << "Warning: invalid nested multi-residue block opening bracket in motif \"" << motif << "\"" << endl;
						continue;
					}
					multiResidueBlockMode = ( motif[i] == '[' ? 1 : 2 );
					break;

				case '}':
				case ']':
					// close multi residue block
					if( multiResidueBlockMode == 0 ||
						( multiResidueBlockMode == 1 && motif[i] == '}' ) ||
						( multiResidueBlockMode == 2 && motif[i] == ']' ) )
					{
						cerr << "Warning: mismatched multi-residue block closing bracket in motif \"" << motif << "\"" << endl;
						continue;
					}

					if( motif.size()-2 == multiResidueBlock.size() ||
						( i+1 < motif.size() && motif[i+1] == '!' ) ||
						( i+1 == motif.size() && !hasModifiedSite ) )
					{
						if( multiResidueBlockMode == 2 )
						{
							for( set<char>::const_iterator itr = defaultResidueMap.getResidues().begin(); itr != defaultResidueMap.getResidues().end(); ++itr )
								if( std::find( multiResidueBlock.begin(), multiResidueBlock.end(), *itr ) == multiResidueBlock.end() )
									mods.push_back( DynamicMod( *itr, modChar, modMass ) );
						} else
						{
							for( size_t j=0; j < multiResidueBlock.size(); ++j )
								mods.push_back( DynamicMod( multiResidueBlock[j], modChar, modMass ) );
						}
					} else
					{
						if( hasModifiedSite )
						{
							CTerminalFilters.push_back( ResidueFilter() );
							bool inclusionMode = true;
							if( multiResidueBlockMode == 2 )
							{
								for( set<char>::const_iterator itr = defaultResidueMap.getResidues().begin(); itr != defaultResidueMap.getResidues().end(); ++itr )
									CTerminalFilters.back().m_filter[ *itr ] = true;
								inclusionMode = false;
							}
							for( size_t j=0; j < multiResidueBlock.size(); ++j )
								CTerminalFilters.back().m_filter[ multiResidueBlock[j] ] = inclusionMode;
						} else
						{
							NTerminalFilters.push_back( ResidueFilter() );
							bool inclusionMode = true;
							if( multiResidueBlockMode == 2 )
							{
								for( set<char>::const_iterator itr = defaultResidueMap.getResidues().begin(); itr != defaultResidueMap.getResidues().end(); ++itr )
									NTerminalFilters.back().m_filter[ *itr ] = true;
								inclusionMode = false;
							}
							for( size_t j=0; j < multiResidueBlock.size(); ++j )
								NTerminalFilters.back().m_filter[ multiResidueBlock[j] ] = inclusionMode;
						}
					}
					multiResidueBlockMode = 0;
					multiResidueBlock.clear();
					break;
				case '!':
					// set last block as the modification site
					if( i == 0 )
						cerr << "Warning: mod site specifier (!) does not occur after a residue or multi-residue block in motif \"" << motif << "\"" << endl;
					else if( multiResidueBlockMode > 0 )
						cerr << "Warning: mod site specifier (!) is invalid inside a multi-residue block in motif \"" << motif << "\"" << endl;
					else
						hasModifiedSite = true;
					break;
				//case '<':
					// N terminus (must be in first block)
					break;
				//case '>':
					// C terminus (must be in last block)
					break;
				default:
					// all other characters are assumed to be residues (A to V bounds checking?)
					if( multiResidueBlockMode > 0 )
						multiResidueBlock.push_back( motif[i] );
					else if( hasModifiedSite )
					{
						CTerminalFilters.push_back( ResidueFilter() );
						CTerminalFilters.back().m_filter[ motif[i] ] = true;
					} else if( i+1 == motif.size() || ( i+1 < motif.size() && motif[i+1] == '!' ) )
					{
							mods.push_back( DynamicMod( motif[i], modChar, modMass ) );
					} else
					{
						NTerminalFilters.push_back( ResidueFilter() );
						NTerminalFilters.back().m_filter[ motif[i] ] = true;
					}
			}
		}
		if( multiResidueBlockMode > 0 )
			cerr << "Warning: mismatched multi-residue block opening bracket in motif \"" << motif << "\"" << endl;

		for( size_t i=0; i < mods.size(); ++i )
		{
			mods[i].NTerminalFilters = NTerminalFilters;
			mods[i].CTerminalFilters = CTerminalFilters;
			insert( mods[i] );
		}
	}

    /*
        This function takes a possible delta mass and tries to snap it to user-supplied "preferred delta masses".
        The function tries to snap the mods in two stages. In the first state, the whole delta mass is snapped
        to the PDMs. In the second stage, the delta mass is split into two or more masses. The split masses are
        in turn get snapped to the user-supplied PDMs.
    */
    vector<DynamicModSet> PreferredDeltaMassesList::getMatchingMassShifts(float modMass, float tolerance)
    {
        vector<DynamicModSet> candidateModSets;
        // Get all possible PDMs for this delta mass
        PreferredDeltaMassesList::iterator begin = lower_bound(modMass - tolerance);
        PreferredDeltaMassesList::iterator end = lower_bound(modMass + tolerance);
        for(PreferredDeltaMassesList::iterator cur = begin; cur != end; ++cur)
        {
            DynamicModSet candidateMods;
            for(DynamicModSet::iterator modIter = cur->second.begin(); modIter != cur->second.end(); ++modIter)
                candidateMods.insert((*modIter));
            candidateModSets.push_back(candidateMods);
        }
        return candidateModSets;
    }

    bool PreferredDeltaMassesList::containsMassShift(float modMass, float tolerance)
    {
        // Get all possible PDMs for this delta mass
        PreferredDeltaMassesList::iterator begin = lower_bound(modMass - tolerance);
        PreferredDeltaMassesList::iterator end = lower_bound(modMass + tolerance);
        if(begin == end)
            return false;
        return true;
    }

	void MvhTable::ConvertToPValues()
	{
		//START_PROFILER(11);
		typedef multimap< double, iterator > KeyByProbability;
		KeyByProbability keyByProbability;
		for( iterator itr = begin(); itr != end(); ++itr )
			keyByProbability.insert( KeyByProbability::value_type( itr->second, itr ) );
		//STOP_PROFILER(11);
		//START_PROFILER(12);
		double pSum = 0;
		for( KeyByProbability::iterator itr = keyByProbability.begin(); itr != keyByProbability.end(); )
		{
			pSum += exp( itr->first );
			itr->second->second = pSum;
			double curProbability = itr->first;
			do { ++itr; } while( itr != keyByProbability.end() && curProbability == itr->first );
		}
		//STOP_PROFILER(12);
	}
}

namespace std
{
	ostream& operator<< ( ostream& o, const SpectrumId& s )
	{
		return ( o << s.id );
	}

	istream& operator>> ( istream& i, DynamicMod& rhs )
	{
		return ( i >> rhs.unmodChar >> rhs.userModChar >> rhs.modMass );
	}

	istream& operator>> ( istream& i, StaticMod& rhs )
	{
		return ( i >> rhs.name >> rhs.mass );
	}

	ostream& operator<< ( ostream& o, const DynamicMod& rhs )
	{
		return ( o << rhs.unmodChar << " " << rhs.userModChar << " " << rhs.modMass );
	}

	ostream& operator<< ( ostream& o, const StaticMod& rhs )
	{
		return ( o << rhs.name << " " << rhs.mass );
	}

}
