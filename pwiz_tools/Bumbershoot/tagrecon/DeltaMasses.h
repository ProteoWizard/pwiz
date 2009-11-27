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
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

#ifndef _DELTAMASSES_H
#define _DELTAMASSES_H

#include "UniModXMLParser.h"
#include "tagreconConfig.h"
#include "shared_types.h"
#include "../freicore/BlosumMatrix.h"

namespace freicore {
	namespace tagrecon {
		
		/**
			Class MassTolerance compares two masses using specified mass tolerance.
		*/
		class MassTolerance {
			// Mass tolerance used in the comparison. Two masses with in this
			// mass tolerance are treated as equal
			float deltaMassTolerance;
		public:
			MassTolerance(float dMTolerance = 0.5f) {
				deltaMassTolerance = dMTolerance;
			}

			bool operator ()(const float lhs, const float rhs) const {
				float deltaMass = lhs-rhs;
				if(fabs(deltaMass) > deltaMassTolerance) {
					return lhs < rhs;
				} else {
					return false;
				}
			}
		};

		/**
			Class InterpretationMapComparator sorts the elements (modifications) in the InterpretationMap
			based on amino acids and masses of the modification. Two modifications that can go on same
			amino acid with a different masses (with in the mass tolerance) are treated as same.
		*/
		class InterpretationMapComparator {
			// Mass tolerance used for the comparator.
			float deltaMassTolerance;
		public:
			InterpretationMapComparator(float dMTolerance = 0.5f) {
				deltaMassTolerance = dMTolerance;
			}

			/**
				bool operator() takes pairs of <mass, amino acid> from two modification objects.
				The operator first orders them based on the amino acid and then based on their
				mass. Two modifications that can go on the same amino acid having masses with in
				a specified mass tolerance are treated as same.
			*/
			bool operator() (const pair<float, string>& lhs, const pair<float, string>& rhs) const
            {
				float deltaMass = lhs.first - rhs.first;
				if(fabs(deltaMass) > deltaMassTolerance)
					return lhs.first < rhs.first;
				else
					return lhs.second < rhs.second;
			}
		};

        typedef multimap <float, string, MassTolerance> MassToAminoAcidMap;
		typedef multimap < pair<float, string>, UnimodModification, InterpretationMapComparator> InterpretationMap;
		typedef multimap <string,string> AminoAcidNameLookup;
		typedef pair <float,string> SubstitutionLogOddsKey;
		typedef multimap <pair<float,string>,int> SubstitutionLogOddsMap;
		
		/**
			class DeltaMasses stores the modifcations read by UniModXMLParser. It also
			creates two modification maps.
			1: modificationMassToAminoAcidMap = This maps mass of a modification to amino acids that can
												have the particular modification.
			2: substitutionMassToAminoAcidMap = This maps mass of a substitution to amino acid that can
												have the particular substitution. 
			3: InterpretationMap = This maps a pair <mass, amino acid> of a modification to the
									unimod modification object that contains full details about
									that modification. This map is used to annotate a particular
									delta mass on a particular amino acid.
		*/
		class DeltaMasses {

			vector <UnimodModification> modifications;
            MassToAminoAcidMap modificationMassToAminoAcidMap;
			MassToAminoAcidMap substitutionMassToAminoAcidMap;
			InterpretationMap interpretationMap;
			
			BlosumMatrix* blosumMatrix;
			AminoAcidNameLookup aaLookup;
			SubstitutionLogOddsMap substitutionLogOddsMap;

		public:
			DeltaMasses(vector <UnimodModification> mods) {
				modifications = mods;
				if(g_rtConfig->FindSequenceVariations) {
					// Parse out the blosum matrix
					blosumMatrix = new BlosumMatrix(g_rtConfig->Blosum);
					blosumMatrix->parseBlosumMatrix();
					//cout << (*blosumMatrix) << endl;
				
					// The lookup table maps the three letter amino acid code to its single letter
					// representation. This map is used to map the substitutions found in the
					// unimod xml file to the blosum matrix file. The hack is necessary due to 
					// the lack of target amino acid information in the substitution description
					// of unimod xml.
					aaLookup.insert(AminoAcidNameLookup::value_type("Gly", "G"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Ala", "A"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Ser", "S"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Pro", "P"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Val", "V"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Thr", "T"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Cys", "C"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Leu", "L"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Ile", "I"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Asn", "N"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Asp", "D"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Gln", "Q"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Lys", "K"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Glu", "E"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Met", "M"));
					aaLookup.insert(AminoAcidNameLookup::value_type("His", "H"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Phe", "F"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Arg", "R"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Tyr", "Y"));
					aaLookup.insert(AminoAcidNameLookup::value_type("Trp", "W"));
				}
			}
			// Builds MassToAminoAcidMap and InterpretationMap
			void buildDeltaMassLookupTables();
			// Returns possible modification annotations for an 
			// amino acid and a mass.
			vector<UnimodModification> getPossibleModifications(string aminoAcid, float deltaMass);
			// Returns DynamicModSet object that contains substitutions
			// which matches a particular mass
			DynamicModSet getPossibleSubstitutions(float deltaMass);
			// Returns DynamicModSet object that contains substitutions
			// which matches a particular mass
			DynamicModSet getPossibleSubstitutions(float deltaMass, float massTol);
			// Getter functions
			MassToAminoAcidMap getMassToAminoAcidMap() { return modificationMassToAminoAcidMap;};
			InterpretationMap getInterpretationMap() { return interpretationMap;};
			// Print functions
			void printMassToAminoAcidMap();
			void printInterpretationMap();
		};

	}
}
#endif
