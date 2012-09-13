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
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

#include "DeltaMasses.h"
#include "UniModXMLParser.h"
#include "shared_defs.h"
#include <boost/foreach_field.hpp>

namespace freicore {
namespace tagrecon {

		/**
			buildDeltaMassLookupTables() reads each single modification read by the
			UniModXMLParser and creates MassToAminoAcidMap, SubstitutionMap, and 
			InterpretationMap.See documentation of DeltaMasses class for full details
			of the above mentioned maps.
		*/
		void DeltaMasses::buildDeltaMassLookupTables() {

			// Reinitialize the maps with user-specified fragment ion mass tolerances.
			modificationMassToAminoAcidMap = MassToAminoAcidMap(MassTolerance(g_rtConfig->PrecursorMzTolerance));
			substitutionMassToAminoAcidMap = MassToAminoAcidMap(MassTolerance(g_rtConfig->PrecursorMzTolerance));
			interpretationMap = InterpretationMap(InterpretationMapComparator(g_rtConfig->FragmentMzTolerance));

			// Get the static mods configured by the user and create a <aminoAcid,mass> map
			multimap <string, float> staticModMap;
			BOOST_FOREACH(const StaticMod& mod, g_rtConfig->staticMods)
				staticModMap.insert(make_pair(string(1,mod.name), mod.mass));

			// For each Unimod modification
			BOOST_FOREACH(const UnimodModification& mod, modifications)
            {
				// Get the candidate mass depending up on whether we are using mono-isotopic or
				// average masses
				float candidateMass = mod.getMonoisotopicMass();
				if(g_rtConfig->UseAvgMassOfSequences)
					candidateMass = mod.getAverageMass();

				// For each amino acid specificity
                BOOST_FOREACH(const ModificationSpecificity& modSpecificity, mod.getAminoAcidSpecificities())
                {
					// Get the static mod mass for this amino acid. Static mod mass for an AA is 0.0 if 
					// it doesn't have a static modification. We subtract the mass of static modification
					// from the mass difference due to modification to get true mass of modification for
					// an amino acid with a static modification
					multimap<string,float>::iterator iter = staticModMap.find(modSpecificity.aminoAcid);
					float staticModMass = 0.0f;
					if(iter != staticModMap.end())
						staticModMass = iter->second;

					// If the delta mass corresponds to a modification then add it to the
					// substitution delta mass map
					if(modSpecificity.classification.find("substitution") == string::npos) {
                        // Add (mass, amino acid) pair into the map
						modificationMassToAminoAcidMap.insert(make_pair(candidateMass-staticModMass, modSpecificity.aminoAcid));
                        // Add (<mass,amino acid>, UniModObject) pair into the interpretation map
                        interpretationMap.insert(make_pair( make_pair(candidateMass-staticModMass, modSpecificity.aminoAcid), mod));
                    } 
                }
            }

            // Add the substitutions
            if(g_rtConfig->unknownMassShiftSearchMode == MUTATIONS)
            {
                // Iterate over all amino acids
                BOOST_FOREACH_FIELD((const string& from3LetterCode)(const string& from1LetterCode), aaLookup)
                {
                    // Find out if the from amino acid can have static modifications
                    multimap<string,float>::iterator iter = staticModMap.find(from1LetterCode);
                    float staticModMass = 0.0f;
                    if(iter != staticModMap.end())
                        staticModMass = iter->second;

                    // Iterate over all the rest of the amino acids
                    BOOST_FOREACH_FIELD((const string& to3LetterCode)(const string& to1LetterCode), aaLookup)
                    {
                        if (from1LetterCode == to1LetterCode)
                            continue;

                        // Get the candidate masses
						float candidateMonoMass = (float) (AminoAcid::Info::record(from1LetterCode[0]).residueFormula.monoisotopicMass() - AminoAcid::Info::record(to1LetterCode[0]).residueFormula.monoisotopicMass());
						float candidateAvgMass = (float) (AminoAcid::Info::record(from1LetterCode[0]).residueFormula.molecularWeight() - AminoAcid::Info::record(to1LetterCode[0]).residueFormula.molecularWeight());
                        float candidateMass = g_rtConfig->UseAvgMassOfSequences ? candidateAvgMass : candidateMonoMass;
                        // Skip the I/L and Q/K subs
                        if(fabs(candidateMass)<0.8) {
                            continue;
                        }

                        // Subtract the static mod mass
                        candidateMass-=staticModMass;
                        // Negate the mass so that we can add this mass to the 
                        // amino acid in the database peptide in order to match
                        // the amino acid in the spectrum.
                        candidateMass*=-1.0;

                         // Insert the modification in to the map
                        substitutionMassToAminoAcidMap.insert(make_pair(candidateMass, from1LetterCode));
                        // Get the log odds score and insert in to the map
                        int logOdds = blosumMatrix->getLogOdds(from1LetterCode,to1LetterCode);
                        SubstitutionLogOddsKey key(candidateMass, from1LetterCode);
                        substitutionLogOddsMap.insert(make_pair(key,logOdds));
                        // Make an unimod modification object out of this substitution
                        UnimodModification mod("Substitution from ["+from1LetterCode+"] to ["+to1LetterCode+"]",from1LetterCode+"->"+to1LetterCode);
                        mod.setModificationMasses(candidateMonoMass,candidateAvgMass);
                        mod.addASpecificity(from1LetterCode,"Any where","AA substitution");
                        // Add it to the whole map
                        interpretationMap.insert(make_pair(make_pair(candidateMass,from1LetterCode), mod));
                    }
                }
            }
		}

		vector<UnimodModification> DeltaMasses::getPossibleModifications(string aminoAcid, float deltaMass) 
        {

			vector <UnimodModification> modificationInterpretation;

			pair<float,string> lookUpKey(deltaMass, aminoAcid);
			pair<InterpretationMap::const_iterator,InterpretationMap::const_iterator> range = interpretationMap.equal_range(lookUpKey);
			InterpretationMap::const_iterator lowEnd = range.first;
			InterpretationMap::const_iterator highEnd = range.second;

			while(lowEnd != highEnd && lowEnd != interpretationMap.end()) 
            {
				modificationInterpretation.push_back((*lowEnd).second);
				lowEnd++;
			}
			if(modificationInterpretation.size()==0 && g_rtConfig->unknownMassShiftSearchMode == BLIND_PTMS) 
            {
				UnimodModification unknown("unknown "+lexical_cast<string>(deltaMass)+ " on " + aminoAcid,"unknown");
				unknown.setModificationMasses(deltaMass, deltaMass);
				unknown.addASpecificity(aminoAcid,"Any where","Post-translational");
			}
			return modificationInterpretation;
		}

		/**
			getPossibleSubstitutions takes a delta mass and looks up all possible substitutions
			for the mass. The function creates a dynamic mod set out of the the candidate
			substitutions. The mod set is then used to generate all possible peptide candidates
			that contain the each of the substitution.
		*/
		DynamicModSet DeltaMasses::getPossibleSubstitutions(float deltaMass) 
        {

			// Create a new dynamic mod set
			DynamicModSet candidateSubs;
			// Look up substitutions with the delta mass.
			pair<MassToAminoAcidMap::iterator, MassToAminoAcidMap::iterator> range = substitutionMassToAminoAcidMap.equal_range(deltaMass);
			MassToAminoAcidMap::iterator cur = range.first;
			// For each of the substitution create a new dynamic mod and add it to the
			// mod set. We only add substitutions that have log odds above user defined
			// threshold.
			while(cur != range.second) 
            {
				SubstitutionLogOddsKey key((*cur).first,(*cur).second);
				SubstitutionLogOddsMap::iterator iter = substitutionLogOddsMap.find(key);
				//cout << (*iter).first.first << "," << (*iter).first.second << "->" << (*iter).second << endl;
				if((*iter).second >= g_rtConfig->BlosumThreshold) 
                {
					DynamicMod sub((*cur).second[0],(*cur).second[0], (float) (*cur).first);
					candidateSubs.insert(sub);
				}
				cur++;
			}

			// Return the new dynamic mod set.
			return candidateSubs;
		}

		/**
			getPossibleSubstitutions takes a delta mass and looks up all possible substitutions
			for the mass. The function creates a dynamic mod set out of the the candidate
			substitutions. The mod set is then used to generate all possible peptide candidates
			that contain the each of the substitution. The function uses a defined massTolerance
			while it is looking up the candidate subs.
		*/
		DynamicModSet DeltaMasses::getPossibleSubstitutions(float deltaMass, float massTol) 
        {

			// Create a new dynamic mod set
			DynamicModSet candidateSubs;
			// Look up substitutions with the delta mass and the mass tolerance.
			MassToAminoAcidMap::iterator low = substitutionMassToAminoAcidMap.lower_bound(deltaMass - massTol);
			MassToAminoAcidMap::iterator high = substitutionMassToAminoAcidMap.upper_bound(deltaMass + massTol);
			// For each of the substitution create a new dynamic mod and add it to the
			// mod set. We only add substitutions that have log odds above user defined
			// threshold.
			while(low != high) 
            {
				SubstitutionLogOddsKey key((*low).first,(*low).second);
				SubstitutionLogOddsMap::iterator iter = substitutionLogOddsMap.find(key);
				//cout << (*iter).first.first << "," << (*iter).first.second << "->" << (*iter).second << endl;
				if((*iter).second >= g_rtConfig->BlosumThreshold) 
                {
					DynamicMod sub((*low).second[0],(*low).second[0], (float) (*low).first);
					candidateSubs.insert(sub);
				}
				++low;
			}

			// Return the new dynamic mod set.
			return candidateSubs;
		}


		/// Print function to print out the contents of the delta masses to amino acid map.
		void DeltaMasses::printMassToAminoAcidMap() 
        {
			// Print out the modification mass map
			for(MassToAminoAcidMap::iterator itr = modificationMassToAminoAcidMap.begin(); itr != modificationMassToAminoAcidMap.end(); ++itr) 
            {
				cout << (*itr).first << "->" << (*itr).second << endl;
			}
			// Print out the substitution mass map
			for(MassToAminoAcidMap::iterator itr = substitutionMassToAminoAcidMap.begin(); itr != substitutionMassToAminoAcidMap.end(); ++itr) 
            {
				cout << (*itr).first << "->" << (*itr).second << endl;
			}
		}

		/// Print function to print out the contents of the interpretation map
		void DeltaMasses::printInterpretationMap() 
        {
			for(InterpretationMap::iterator itr = interpretationMap.begin(); itr != interpretationMap.end(); ++itr) 
				cout << "<" << (*itr).first.first << "," << (*itr).first.second << "> ->" << (*itr).second << endl;
		}
	}
}
