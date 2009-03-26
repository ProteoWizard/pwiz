#include "DeltaMasses.h"
#include "UniModXMLParser.h"
#include "shared_defs.h"

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
			for(StaticModSet::iterator staticModIter = g_residueMap->staticMods.begin(); staticModIter != g_residueMap->staticMods.end(); staticModIter++) {
				staticModMap.insert(multimap<string,float>::value_type(string(1,staticModIter->name),staticModIter->mass));
			}

			// For each Unimod modification
			for(vector <UnimodModification>::iterator itr = modifications.begin(); itr != modifications.end(); ++itr) {
				// Get the candidate mass depending up on whether we are using mono-isotopic or
				// average masses
				float candidateMass = (*itr).getMonoisotopicMass();
				if(g_rtConfig->UseAvgMassOfSequences) {
					candidateMass = (*itr).getAverageMass();
				}

				// Get the amino acid specificities of the modification
				vector <ModificationSpecificity> aminoAcidSpecificities = (*itr).getAminoAcidSpecificities();
				// For each amino acid specificity
				for(vector <ModificationSpecificity>::iterator aaSpecIter = aminoAcidSpecificities.begin(); aaSpecIter != aminoAcidSpecificities.end(); ++aaSpecIter) {
					// Get the static mod mass for this amino acid. Static mod mass for an AA is 0.0 if 
					// it doesn't have a static modification. We subtract the mass of static modification
					// from the mass difference due to modification to get true mass of modification for
					// an amino acid with a static modification
					multimap<string,float>::iterator iter = staticModMap.find((*aaSpecIter).aminoAcid);
					float staticModMass = 0.0f;
					if(iter != staticModMap.end()) {
						staticModMass = iter->second;
					}

					// If the delta mass corresponds to a modification then add it to the
					// substitution delta mass map
					if((*aaSpecIter).classification.find("substitution") == string::npos) {
                        // Add (mass, amino acid) pair into the map
						modificationMassToAminoAcidMap.insert(MassToAminoAcidMap::value_type(candidateMass-staticModMass, (*aaSpecIter).aminoAcid));
                        // Add (<mass,amino acid>, UniModObject) pair into the interpretation map
                        interpretationMap.insert(InterpretationMap::value_type( pair<float,string>(candidateMass-staticModMass,(*aaSpecIter).aminoAcid), (*itr)));
                    } 
                }
            }

            // Add the substitutions
            if(g_rtConfig->FindSequenceVariations) {
                // Iterate over all amino acids
                for(AminoAcidNameLookup::const_iterator from = aaLookup.begin(); from != aaLookup.end(); ++from) {
                    // Find out if the from amino acid can have static modifications
                    multimap<string,float>::iterator iter = staticModMap.find((*from).second);
                    float staticModMass = 0.0f;
                    if(iter != staticModMap.end()) {
                        staticModMass = iter->second;
                    }

                    // Iterate over all the rest of the amino acids
                    for(AminoAcidNameLookup::const_iterator to = aaLookup.begin(); to != aaLookup.end(); ++to) {
                        if((*from).second != (*to).second) {
                            // Get the candidate masses
							float candidateMonoMass = (float) (AminoAcid::Info::record((*from).second[0]).residueFormula.monoisotopicMass() - AminoAcid::Info::record((*to).second[0]).residueFormula.monoisotopicMass());
							float candidateAvgMass = (float) (AminoAcid::Info::record((*from).second[0]).residueFormula.molecularWeight() - AminoAcid::Info::record((*to).second[0]).residueFormula.molecularWeight());
                            //float candidateMonoMass = (float) (g_residueMap->getMonoMassByName((*from).second[0]) - g_residueMap->getMonoMassByName((*to).second[0]));
                            //float candidateAvgMass = (float) (g_residueMap->getAvgMassByName((*from).second[0]) - g_residueMap->getAvgMassByName((*to).second[0]));
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
                            substitutionMassToAminoAcidMap.insert(MassToAminoAcidMap::value_type(candidateMass, (*from).second));
                            // Get the log odds score and insert in to the map
                            int logOdds = blosumMatrix->getLogOdds((*from).second,(*to).second);
                            SubstitutionLogOddsKey key(candidateMass, (*from).second);
                            substitutionLogOddsMap.insert(SubstitutionLogOddsMap::value_type(key,logOdds));
                            // Make an unimod modification object out of this substitution
                            UnimodModification mod("Substitution from ["+(*from).second+"] to ["+(*to).second+"]",(*from).second+"->"+(*to).second);
                            mod.setModificationMasses(candidateMonoMass,candidateAvgMass);
                            mod.addASpecificity((*from).second,"Any where","AA substitution");
                            // Add it to the whole map
                            interpretationMap.insert(InterpretationMap::value_type( pair<float,string>(candidateMass,(*from).second), mod));
                        }
                    }
                }
            }
		}

		vector<UnimodModification> DeltaMasses::getPossibleModifications(string aminoAcid, float deltaMass) {

			vector <UnimodModification> modificationInterpretation;

			pair<float,string> lookUpKey(deltaMass, aminoAcid);
			pair<InterpretationMap::const_iterator,InterpretationMap::const_iterator> range = interpretationMap.equal_range(lookUpKey);
			InterpretationMap::const_iterator lowEnd = range.first;
			InterpretationMap::const_iterator highEnd = range.second;

			while(lowEnd != highEnd && lowEnd != interpretationMap.end()) {
				modificationInterpretation.push_back((*lowEnd).second);
				lowEnd++;
			}
			if(modificationInterpretation.size()==0 && g_rtConfig->FindUnknownMods) {
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
		DynamicModSet DeltaMasses::getPossibleSubstitutions(float deltaMass) {

			// Create a new dynamic mod set
			DynamicModSet candidateSubs;
			// Look up substitutions with the delta mass.
			pair<MassToAminoAcidMap::iterator, MassToAminoAcidMap::iterator> range = substitutionMassToAminoAcidMap.equal_range(deltaMass);
			MassToAminoAcidMap::iterator cur = range.first;
			// For each of the substitution create a new dynamic mod and add it to the
			// mod set. We only add substitutions that have log odds above user defined
			// threshold.
			while(cur != range.second) {
				SubstitutionLogOddsKey key((*cur).first,(*cur).second);
				SubstitutionLogOddsMap::iterator iter = substitutionLogOddsMap.find(key);
				//cout << (*iter).first.first << "," << (*iter).first.second << "->" << (*iter).second << endl;
				if((*iter).second >= g_rtConfig->BlosumThreshold) {
					DynamicMod sub((*cur).second[0],(*cur).second[0], (float) (*cur).first);
					candidateSubs.insert(sub);
				}
				cur++;
			}

			// Return the new dynamic mod set.
			return candidateSubs;
		}

		/// Print function to print out the contents of the delta masses to amino acid map.
		void DeltaMasses::printMassToAminoAcidMap() {
			// Print out the modification mass map
			for(MassToAminoAcidMap::iterator itr = modificationMassToAminoAcidMap.begin(); itr != modificationMassToAminoAcidMap.end(); ++itr) {
				cout << (*itr).first << "->" << (*itr).second << endl;
			}
			// Print out the substitution mass map
			for(MassToAminoAcidMap::iterator itr = substitutionMassToAminoAcidMap.begin(); itr != substitutionMassToAminoAcidMap.end(); ++itr) {
				cout << (*itr).first << "->" << (*itr).second << endl;
			}
		}

		/// Print function to print out the contents of the interpretation map
		void DeltaMasses::printInterpretationMap() {
			for(InterpretationMap::iterator itr = interpretationMap.begin(); itr != interpretationMap.end(); ++itr) {
				cout << "<" << (*itr).first.first << "," << (*itr).first.second << "> ->" << (*itr).second << endl;
			}
		}
	}
}
