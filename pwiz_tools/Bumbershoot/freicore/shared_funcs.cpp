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
#include "shared_funcs.h"
#include "MixedRadixEnumerator.h"
#include "SubsetEnumerator.h"
//#include "BitwiseSubsetGenerator.h"
#include "boost/foreach_field.hpp"
#include "boost/range/adaptor/reversed.hpp"

using namespace freicore;

namespace std
{
	ostream& operator<< ( ostream& o, const ProteinLocusByIndex& rhs )
	{
		return ( o << "(" << rhs.index << ", " << rhs.offset << ")" );
	}

	ostream& operator<< ( ostream& o, const ProteinLocusByName& rhs )
	{
		return ( o << "(" << rhs.name << ", " << rhs.offset << ")" );
	}

	ostream& operator<< ( ostream& o, const CleavageRule& rhs )
	{
		return ( o << rhs.first << " " << rhs.second );
	}

	istream& operator>> ( istream& i, CleavageRule& rhs )
	{
		static boost::char_separator<char> cleavageCandidatesDelimiter("|");

		rhs.first.clear();
		rhs.second.clear();

		string preCleavageCandidatesString, postCleavageCandidatesString;
		i >> preCleavageCandidatesString >> postCleavageCandidatesString;

		stokenizer preCleavageCandidatesParser(	preCleavageCandidatesString.begin(),
			preCleavageCandidatesString.end(),
			cleavageCandidatesDelimiter );
		for( stokenizer::iterator itr = preCleavageCandidatesParser.begin(); itr != preCleavageCandidatesParser.end(); ++itr )
			rhs.first.insert( *itr );

		stokenizer postCleavageCandidatesParser(postCleavageCandidatesString.begin(),
			postCleavageCandidatesString.end(),
			cleavageCandidatesDelimiter );
		for( stokenizer::iterator itr = postCleavageCandidatesParser.begin(); itr != postCleavageCandidatesParser.end(); ++itr )
			rhs.second.insert( *itr );

		return i;
	}

	istream& operator>> ( istream& i, CleavageRuleSet& rhs )
	{
		CleavageRule newRule;
		while( i >> newRule )
		{
			rhs.push_back( newRule );
			rhs.longestPreCleavageCandidate = max( newRule.first.longestCleavageCandidate, rhs.longestPreCleavageCandidate );
			rhs.longestPostCleavageCandidate = max( newRule.second.longestCleavageCandidate, rhs.longestPostCleavageCandidate );
		}

		return i;
	}

	ostream& operator<< ( ostream& o, MvIntKey& rhs )
	{
		return o << vector<int>( rhs );
	}

	ostream& operator<< ( ostream& o, MvhTable& rhs )
	{
		for( MvhTable::iterator itr = rhs.begin(); itr != rhs.end(); ++itr )
			o << itr->first << " -> " << itr->second << "\n";
		return o;
	}
}

namespace freicore
{
	void CleavageRuleSet::initialize( const string& cfgStr )
	{
		clear();
		stringstream CleavageRulesStream( cfgStr );
		CleavageRulesStream >> *this;
	}

    namespace {
        string cleavageHalfRuleToRegex(const CleavageHalfRule& halfRule)
        {
            if( halfRule.hasWildcard )
                return "";
            else if( halfRule.size() == 1 &&
                     halfRule.begin()->length() == 1 &&
                     *halfRule.begin() != "[" &&
                     *halfRule.begin() != "]" )
                return *halfRule.begin();

            else // i.e. "[M|K|R" -> "(^M)|([KR])
            {
                vector<string> regexPieces;
                string singleResidues;
                BOOST_FOREACH( const string& candidate, halfRule )
                {
                    if( candidate.length() == 1 )
                        singleResidues += (candidate == "[" || candidate == "]") ?
                                          "" : candidate; // skip protein termini
                    else
                    {
                        string copy = candidate;
                        // replace protein termini with regex start/end anchors
                        if( *copy.begin() == '[' ) *copy.begin() = '^';
                        if( *copy.rbegin() == ']' ) *copy.rbegin() = '$';
                        regexPieces.push_back(copy);
                    }
                }

                if( !singleResidues.empty() )
                    regexPieces.push_back("[" + singleResidues + "]");

                if( regexPieces.size() > 1 )
                    return "(" + join(regexPieces, ")|(") + ")";
                else if( regexPieces.size() == 1 )
                    return regexPieces[0];
                else
                    return "";
            }
        }
    }

    string CleavageRuleSet::asCleavageAgentRegex()
    {
        vector<string> regexPieces;
        BOOST_FOREACH( const CleavageRule& rule, *this )
        {
            string lookbehind = cleavageHalfRuleToRegex(rule.first);
            if( !lookbehind.empty() ) lookbehind = "(?<=" + lookbehind + ")";

            string lookahead = cleavageHalfRuleToRegex(rule.second);
            if( !lookahead.empty() ) lookahead = "(?=" + lookahead + ")";

            if( !lookbehind.empty() || !lookahead.empty() )
                regexPieces.push_back(lookbehind + lookahead);
            //else
            //    regexPieces.push_back("(" + lookbehind + lookahead + ")");
        }

        if (regexPieces.size() > 1)
            return "(" + join(regexPieces, ")|(") + ")";
        else
            return regexPieces[0];
    }

	bool	TestPtmSite( const string& seq, const DynamicMod& mod, size_t pos )
	{
		if( seq[pos] != mod.unmodChar )
			return false;

        if(pos > 0) {
		    for( size_t i=1; i <= mod.NTerminalFilters.size(); ++i )
		    {
			    if( pos-i < 0 )
				    return false;

			    const ResidueFilter& filter = *(mod.NTerminalFilters.rbegin()+(i-1));
			    if( !filter.testResidue( seq[pos-i] ) )
				    return false;
		    }
        }

        if(pos < seq.length()-1) {
		    for( size_t i=1; i <= mod.CTerminalFilters.size(); ++i )
		    {
			    if( pos+i >= seq.length() )
				    return false;

			    const ResidueFilter& filter = mod.CTerminalFilters[i-1];
			    if( !filter.testResidue( seq[pos+i] ) )
				    return false;
		    }
        }
		return true;
	}

    bool	TestPtmSiteOld( const string& seq, const DynamicMod& mod, size_t pos )
	{
		if( seq[pos] != mod.unmodChar )
			return false;

		for( size_t i=1; i <= mod.NTerminalFilters.size(); ++i )
		{
			if( pos-i < 0 )
				return false;

			const ResidueFilter& filter = *(mod.NTerminalFilters.rbegin()+(i-1));
			if( !filter.testResidue( seq[pos-i] ) )
				return false;
		}

		for( size_t i=1; i <= mod.CTerminalFilters.size(); ++i )
		{
			if( pos+i >= seq.length() )
				return false;

			const ResidueFilter& filter = mod.CTerminalFilters[i-1];
			if( !filter.testResidue( seq[pos+i] ) )
				return false;
		}
		return true;
	}

	/**
		getInterpretation function takes DigestedPeptide and converts it into a string.
		It uses AminoAcid+ModMass notation to represent the location of mods in the sequence. 
		An example output for a peptide with an oxidized methonine would look like PVSPLLLASGM+16AR.
		This string is used for display and sorting purposes.
	*/
	string getInterpretation(const DigestedPeptide& peptide)
    {
		// Get the peptide sequence and the mods
		string returnString = "(" + peptide.sequence() + ")";
		const ModificationMap& modMap = peptide.modifications();
        stringstream modMass;
        modMass << showpos;

        BOOST_FOREACH_FIELD((int position)(const ModificationList& modList), modMap | boost::adaptors::reversed)
        BOOST_FOREACH(const Modification& mod, modList)
        {
            int realPosition = max(-1, min((int) peptide.sequence().length(), position))+2;
            modMass.str(""); modMass << round(mod.monoisotopicDeltaMass());
            returnString.insert(realPosition, modMass.str());
        }

		return returnString;
	}


	CustomBaseNumber::CustomBaseNumber(unsigned int valueAsBase10, size_t base)
        : base_(base), nonZeroDigits_(0)
    {
        const static string digits("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ");
        if( valueAsBase10 > 0 )
        {
            double value = (double) valueAsBase10;
            double base = (double) base_;
            while( value > 0 )
            {
                double x = floor(((value / base) - floor(value / base)) * base_ + 0.5);
                digits_.insert(digits_.begin(), digits[(size_t) x]);
                if( (size_t) x > 0 )
                    ++nonZeroDigits_;
                value = floor(value / base_);
            }
        } else
            digits_.push_back('0');
    }

    string CustomBaseNumber::str() const
    {
        return string(digits_.begin(), digits_.end());
    }

    size_t CustomBaseNumber::size() const { return digits_.size(); }

    CustomBaseNumber& CustomBaseNumber::operator<< (size_t shift)
    {
        while( nonZeroDigits_-- && shift-- )
        {
            digits_.pop_front();
            digits_.push_back('0');
        }
        return *this;
    }

    CustomBaseNumber& CustomBaseNumber::operator>> (size_t shift)
    {
        while( nonZeroDigits_-- && shift-- )
        {
            digits_.pop_back();
            digits_.push_front('0');
        }
        return *this;
    }

    void CustomBaseNumber::incrementChar( int i )
	{
        size_t digitValue = *(digits_.begin()+i) - '0';
		if( digitValue+1 == base_ )
		{
			if( i == 0 )
            {
                nonZeroDigits_ = 1;
                digits_[0] = '1';
				digits_.push_back('0');
            } else
            {
                --nonZeroDigits_;
			    digits_[i] = '0';
				incrementChar(i - 1);
            }
		} else
        {
            if( digitValue == 0 )
                ++nonZeroDigits_;
			++digits_[i];
        }
	}

    CustomBaseNumber& CustomBaseNumber::operator++()
    {
		incrementChar(digits_.size()-1);
		return *this;
    }

    int CustomBaseNumber::digit(size_t n) const
    {
        return n >= digits_.size() ? 0 : *(digits_.rbegin()+n) - '0';
    }

    int CustomBaseNumber::nonZeroDigits() const
    {
        return nonZeroDigits_;
    }

	void MakePtmVariantsOld( const DigestedPeptide& unmodifiedPeptide,
                          vector<DigestedPeptide>& ptmPeptides,
                          int maxPtmCount,
                          const DynamicModSet& dynamicMods,
                          const StaticModSet& staticMods )
	{
		/*cout << "PTM map: ";
		for( int i=0; i < (int) dynamicMods.size(); ++i )
			cout << dynamicMods[i].unmodChar << " -> " << dynamicMods[i].modChar << "; ";
		cout << endl;*/
        const string& sequence = unmodifiedPeptide.sequence();
        string sequenceWithTermini = PEPTIDE_N_TERMINUS_STRING + sequence + PEPTIDE_C_TERMINUS_STRING;
        size_t seqLength = sequence.length();

		// Apply static mods to the peptide
		DigestedPeptide staticPeptide( unmodifiedPeptide );
		ModificationMap& staticModMap = staticPeptide.modifications();
		for( size_t i=0; i < seqLength; ++i )
			for( StaticModSet::const_iterator itr = staticMods.begin(); itr != staticMods.end(); ++itr )
				if( itr->name == sequence[i] )
                {
					staticModMap.insert( make_pair( i, *itr ) );
					break;
				}

		// For each amino acid residue test if it can be modified
		// Store pointers to moddable residues
		size_t moddableResidues = 0;
        size_t maxModsPerResidue = 0;
		vector< vector<DynamicModSet::const_iterator> > ptmMask(seqLength);
		for( size_t i=0; i < seqLength; ++i )
        {
			for( DynamicModSet::const_iterator itr = dynamicMods.begin(); itr != dynamicMods.end(); ++itr )
            {
				if( TestPtmSiteOld( sequenceWithTermini, *itr, i+1 ) )
                    ptmMask[i].push_back(itr);
            }

            if( !ptmMask[i].empty() )
            {
                ++moddableResidues;
                maxModsPerResidue = max( maxModsPerResidue, ptmMask[i].size() );
            }
        }

		// Maximum number of possible PTM interpretations = <max. mods per residue+1> ^ <# moddable residues>
		// Note: assumes that a dynamic mod may occur either 0 or 1 time
        //int ptmPermutations = 1 << possiblePtmCount;
        int ptmPermutations = (int) pow( (double) maxModsPerResidue+1, (double) moddableResidues );

		// For each possible PTM permutation
        CustomBaseNumber permutation(0, maxModsPerResidue+1);
		for( int p=0; p < ptmPermutations; ++p, ++permutation )
		{
            // p is treated as a mask where each non-zero bit is a modified residue;
            // for example, for a peptide SAMIAM with oxidations on M:
            // possiblePtmCount: 2 (equal to length of mask)
            // ptmPermutations: 4 (equal to maximum value of mask plus one)
            // 00: no modified residues
            // 01: second M modified
            // 10: first M modified
            // 11: both Ms modified

			// An awesome function to very quickly count non-zero bits in a 32-bit integer:
			// http://graphics.stanford.edu/~seander/bithacks.html#CountBitsSetParallel
			//unsigned int const w = p - ((p >> 1) & 0x55555555);                    // temp
			//unsigned int const x = (w & 0x33333333) + ((w >> 2) & 0x33333333);     // temp
			//int ptmCount = ((x + (x >> 4) & 0xF0F0F0F) * 0x1010101) >> 24; // count

            // if too many mods are on this peptide, skip this permutation
			if( permutation.nonZeroDigits() > maxPtmCount )
				continue;

			DigestedPeptide ptmPeptide(staticPeptide);
			ModificationMap& ptmMap = ptmPeptide.modifications();
            int ptmDigit = 0;
			for( size_t i=0; i < ptmMask.size(); ++i )
			{
                // if the current modifiable residue is a non-zero digit in the current permutation
                if( !ptmMask[i].empty() )
                {
                    size_t digit = permutation.digit(ptmDigit);
                    if( digit > 0 && digit-1 < ptmMask[i].size() )
                        // use the digit value - 1 to get the mod on this residue
                        ptmMap[i].push_back( *ptmMask[i][digit-1] );
                    ++ptmDigit;
                }
			}

			// Store the new sequence
			ptmPeptides.push_back( ptmPeptide );
            //cout << getInterpretation(ptmPeptide) << endl;
		}
	}

    /**
        This function is part of the combinatorial math that computes the total number of peptide 
        variants possible if atmost maxPTMCount number of modifications are allowed. This math
        is fully tested with an independent Bit-wise subset generator that computes all possible
        subsets of a set and sums the elements in the subsets that have <= maxPTMCount number
        of elements. 
    */
    inline size_t getMultiplier(const vector <size_t>& positionalRadix, size_t start, size_t wordSize) {
        size_t multipler = 0;
        for(size_t i = start; i < positionalRadix.size()-wordSize; ++i) {
            size_t local = positionalRadix[i];
            for(size_t j = i+1; j <= i+wordSize-1; ++j) {
                local *= positionalRadix[j];
            }
            multipler+=local;
        }
        return multipler;
    }

    /**
        This function is part of the combinatorial math that computes the total number of peptide 
        variants possible if atmost maxPTMCount number of modifications are allowed. This math
        is fully tested with an independent Bit-wise subset generator that computes all possible
        subsets of a set and sums the elements in the subsets that have <= maxPTMCount number
        of elements. 
    */
    inline size_t countVariants(const vector<size_t>& positionalRadix, size_t maxPTMCount) {

        if(maxPTMCount == 0) {
            return 0;
        }

        size_t alternativeCount = 0;
        for(size_t i = 0; i < positionalRadix.size(); ++i) {
            alternativeCount+=positionalRadix[i];
        }

        // The general mathematics is: Given that a peptid ABCD can be modified 
        // with A in N1 ways and B in N2 ways and D in N3 ways. Assume that
        // we can not have more than 2 modifications per peptide then the total
        // number of peptide variants is N1+N2+N3+(N1*N2)+(N1*N3)+(N2*N3).
        size_t ptmCount = 2;
        while(ptmCount <= maxPTMCount) {
            for(size_t i = 0; i <= positionalRadix.size()-ptmCount; ++i) {
                size_t multiplier = getMultiplier(positionalRadix, i+1,ptmCount-1);
                alternativeCount+=multiplier*positionalRadix[i];
            }
            ++ptmCount;
        }
        return alternativeCount;
    }

    /**
        This function takes a peptide, possible modifications, the maxPTMCount, and computes the total
        number of variants possible for that peptide. This number is different from the total number of 
        permutations and is generally much smaller and grows as a function of total number of mods and
        maxPTMCount. This function is either linear if maxPTMCount is 1, quadratic if it is 2. It won't
        be exponential.
    */
    void getDynamicPTMVariables(const DigestedPeptide& unmodifiedPeptide, const DynamicModSet& mods, 
                                size_t maxPTMCount, size_t& totalPermutations, size_t& totalModifiedPeptides) {
        
        // Get the sequence.
        string sequenceWithTermini = PEPTIDE_N_TERMINUS_STRING + unmodifiedPeptide.sequence() + PEPTIDE_C_TERMINUS_STRING;
        size_t seqLength = sequenceWithTermini.length();

		// An array of ints to store the index of the amino acid
        // in the sequence to which a modification corresponds.
        vector <size_t> positions(seqLength);
                
        // For each amino acid
        for( size_t i=0; i < seqLength; ++i ) {
            positions[i]=0;
            // Check to see if any ptms can go on it
            for( DynamicModSet::const_iterator itr = mods.begin(); itr != mods.end(); ++itr ) {
                // If they do then increment the number of PTMS possible at that resiude
                if( TestPtmSite( sequenceWithTermini, *itr, i ) ) {
                    ++positions[i];
                }
            }
        }

        // Compute total permutations.
        totalPermutations=1;
        for(size_t i = 0; i < seqLength; ++i) {
            if(positions[i]>0) {
                totalPermutations *= positions[i];
            }
        }

        // Compute total variants using the maxPTMCount as a cap
        totalModifiedPeptides = countVariants(positions,maxPTMCount);
    }

    bool MakePtmVariantsWithMixedRadixEnumerator( const DigestedPeptide& unmodifiedPeptide,
                          vector<DigestedPeptide>& ptmPeptides,
                          int maxPtmCount,
                          const DynamicModSet& dynamicMods,
                          const StaticModSet& staticMods, size_t maxVarIters )
	{
		//cout << "PTM map: ";
		//for( int i=0; i < (int) dynamicMods.size(); ++i )
		//	cout << dynamicMods[i].unmodChar << " -> " << dynamicMods[i].modChar << "; ";
		//cout << endl;
        string sequenceWithTermini = PEPTIDE_N_TERMINUS_STRING + unmodifiedPeptide.sequence() + PEPTIDE_C_TERMINUS_STRING;
        size_t seqLength = sequenceWithTermini.length();


		// Apply static mods to the peptide
		DigestedPeptide staticPeptide( unmodifiedPeptide );
		ModificationMap& staticModMap = staticPeptide.modifications();
        for( size_t i=0; i < seqLength; ++i ) {
            size_t ptmIndex = (i==0) ? staticModMap.NTerminus() : (i < seqLength-1) ? i-1 : staticModMap.CTerminus();
            for( StaticModSet::const_iterator itr = staticMods.begin(); itr != staticMods.end(); ++itr ) {
				if( itr->name == sequenceWithTermini[i] )
                {
					staticModMap.insert( make_pair( ptmIndex, *itr ) );
					break;
				}
            }
        }

        // An array of unsigned long to store the number of ways a 
        // residue can be modified
        vector <size_t> ptmRadix(seqLength);
        // An array of ints to store the index of the amino acid
        // in the sequence to which the ptmRadix corresponds.
        vector <int> positions(seqLength);
        // An array of arrays to store the ptms that can be on
        // a residue;
        vector < vector<DynamicModSet::const_iterator> > ptms(seqLength);
        size_t index = 0;
        
        // For each amino acid
        for( size_t i=0; i < seqLength; ++i ) {
            bool hitAMod = false;
            // Check to see if any ptms can go on it
            for( DynamicModSet::const_iterator itr = dynamicMods.begin(); itr != dynamicMods.end(); ++itr ) {
                // If they do then increment the radix, remember the amino acid
                // position and the modification
                if( TestPtmSite( sequenceWithTermini, *itr, i ) ) {
                    ptms[index].push_back(itr);
                    ++ptmRadix[index];
                    positions[index]=(i==0) ? staticModMap.NTerminus() : (i < seqLength-1) ? i-1 : staticModMap.CTerminus();
                    hitAMod = true;
                }
            }
            // Increment the index to store the mods of
            // next candidate amino acid
            if(hitAMod) {
                ++ptmRadix[index];
                ++index;
            }
        }
        
        // Get a mixed radix enumerator with a number of modifiable amino acids 
        MixedRadixEnumerator enumerator(index,(size_t)4,maxPtmCount,ptmRadix, maxVarIters);
        if(enumerator.maxPossiblePermutations>(int) maxVarIters) {
            // Don't forget to add the unmodified peptide;
            ptmPeptides.push_back(staticPeptide);
            return true;
        }
        
        //cout << enumerator.maxPossiblePermutations << endl;
        //cout << index << "," << maxPtmCount;
        //enumerator.print("\tEnum", false);
        //cout << endl;
        

       // Enumerate through the candidate numbers
       while(enumerator.smartNext()) {
           //enumerator.print("Enum:",true);
           //cout << endl;
           // Get a peptide
           DigestedPeptide ptmPeptide(staticPeptide);
		   ModificationMap& ptmMap = ptmPeptide.modifications();
           // Figure out which mods to add to which residue
           for(size_t i = 0; i < enumerator.n_; ++i) {
               if(enumerator.a_[i]>0) {
                   ptmMap[positions[i]].push_back(*ptms[i][enumerator.a_[i]-1]);
               }
           }
           // Store the new sequence
			ptmPeptides.push_back( ptmPeptide );
            //cout << getInterpretation(ptmPeptide) << endl;
        }
       
       // Don't forget to add the unmodified peptide;
       ptmPeptides.push_back(staticPeptide);
       //cout << "\t" << getInterpretation(staticPeptide) << endl;
       return false;
	}

    /**
        This function generates variants of a peptide using subset based enumerator
    */
    bool MakePtmVariants( const DigestedPeptide& unmodifiedPeptide,
                          vector<DigestedPeptide>& ptmPeptides,
                          int maxPtmCount,
                          const DynamicModSet& dynamicMods,
                          const StaticModSet& staticMods, size_t maxVarIters )
	{
		/*cout << "PTM map: ";
		for( DynamicModSet::const_iterator itr = dynamicMods.begin(); itr != dynamicMods.end(); ++itr ) {
			cout << (*itr).unmodChar << (*itr).modMass << ";";
        }
		cout << endl;
        cout << unmodifiedPeptide.sequence() << endl;*/

        string sequenceWithTermini = PEPTIDE_N_TERMINUS_STRING + unmodifiedPeptide.sequence() + PEPTIDE_C_TERMINUS_STRING;
        size_t seqLength = sequenceWithTermini.length();

		// Apply static mods to the peptide
		DigestedPeptide staticPeptide( unmodifiedPeptide );
		ModificationMap& staticModMap = staticPeptide.modifications();
        for( size_t i=0; i < seqLength; ++i ) {
            size_t ptmIndex = (i==0) ? staticModMap.NTerminus() : (i < seqLength-1) ? i-1 : staticModMap.CTerminus();
            for( StaticModSet::const_iterator itr = staticMods.begin(); itr != staticMods.end(); ++itr ) {
				if( itr->name == sequenceWithTermini[i] )
                {
					staticModMap.insert( make_pair( ptmIndex, *itr ) );
					break;
				}
            }
        }

        // An array of ints to store the index of the amino acid
        // in the sequence to which a modification corresponds.
        vector <size_t> positions;
        // An array to store the ptms that can be present on a residue
        vector < DynamicModSet::const_iterator > ptms;
        // Array to store total number of modifications possible at
        // a single site
        vector <size_t> ptmRadix(seqLength);
        
        // For each amino acid
        for( size_t i=0; i < seqLength; ++i ) {
            ptmRadix[i] = 0;
            // Check to see if any ptms can go on it
            for( DynamicModSet::const_iterator itr = dynamicMods.begin(); itr != dynamicMods.end(); ++itr ) {
                // If they do then remember the amino acid position and the modification
                if( TestPtmSite( sequenceWithTermini, *itr, i ) ) {
                    ptms.push_back(itr);
                    size_t pos = (i==0) ? staticModMap.NTerminus() : (i < seqLength-1) ? i-1 : staticModMap.CTerminus();
                    positions.push_back(pos);
                    ++ptmRadix[i];
                }
            }
        }

        // Don't forget to add the unmodified peptide;
        ptmPeptides.push_back(staticPeptide);
        //cout << "\t" << getInterpretation(staticPeptide) << endl;
        // If there are no mods to add to this peptide then return promptly
        if(positions.size()==0) {
            return false;
        }

        // If the total number of peptide variants are above the user set
        // threshold then just return without generating them.
        if(countVariants(ptmRadix,maxPtmCount)>maxVarIters) {
            return true;
        }

        // Get a subset enumerator
        SubsetEnumerator enumerator(positions.size(),1,min((size_t)maxPtmCount,positions.size()),positions);
       // Enumerate through the candidate numbers
       do {
           // Check to make sure that there are no two modifications of same residue in the same set
           if(enumerator.setFitness) {
               // Get the digested peptide and the mod map
               DigestedPeptide ptmPeptide(staticPeptide);
		       ModificationMap& ptmMap = ptmPeptide.modifications();
               // Add the mods
               for(size_t k = 1; k <= enumerator.k_; ++k) {
                   //cout << positions[enumerator.S_[k]-1] <<"-"<< enumerator.S_[k]-1 << ",";
                   ptmMap[positions[enumerator.S_[k]-1]].push_back(*ptms[enumerator.S_[k]-1]);
               }
               // Store the new sequence
			   ptmPeptides.push_back( ptmPeptide );
               /*--maxVarIters;
               if(maxVarIters<0) {
                   ptmPeptides.clear();
                   ptmPeptides.push_back(staticPeptide);
                   return true;
               }*/
           }
        }while(enumerator.next());
       return false;
	}

	void MakePeptideVariants( const DigestedPeptide& peptide, vector<DigestedPeptide>& subPeptides,
									size_t minModCount, size_t maxModCount, const DynamicModSet& dynamicSubs, 
                                    size_t locStart, size_t locEnd)
	{
		string sequenceWithTermini = PEPTIDE_N_TERMINUS_STRING + peptide.sequence() + PEPTIDE_C_TERMINUS_STRING;
        size_t seqLength = sequenceWithTermini.length();

        // An array of ints to store the index of the amino acid
        // in the sequence to which the ptmRadix corresponds.
        vector <size_t> positions;
        // An array of arrays to store the ptms that can be present on a residue;
        vector < DynamicModSet::const_iterator > ptms;

        const ModificationMap& baseModMap = peptide.modifications();
        // For each amino acid
        for( size_t i=locStart; i <= locEnd; ++i ) {
            // Check to see if any ptms can go on it
            for( DynamicModSet::const_iterator itr = dynamicSubs.begin(); itr != dynamicSubs.end(); ++itr ) {
                // If they do then increment the radix, remember the amino acid
                // position and the modification
                if( TestPtmSite( sequenceWithTermini, *itr, i ) ) {
                    ptms.push_back(itr);
                    size_t pos = (i==0) ? baseModMap.NTerminus() : (i < seqLength-1) ? i-1 : baseModMap.CTerminus();
                    positions.push_back(pos);
                }
            }
        }
        
        if(positions.size()==0) {
            return;
        }
       
        // Get a subset enumerator
        SubsetEnumerator enumerator(positions.size(),minModCount,maxModCount,positions);
       //enumerator.print_set();
       // Enumerate through the candidate numbers
       do {
           // Check to make sure that there are no two modifications of same residue in the same set
           if(enumerator.setFitness) {
               // Get the digested peptide and the mod map
               DigestedPeptide ptmPeptide(peptide);
		       ModificationMap& ptmMap = ptmPeptide.modifications();
               // Add the mods
               for(size_t k = 1; k <= enumerator.k_; ++k) {
                   //cout << positions[enumerator.S_[k]-1] <<"-"<< enumerator.S_[k]-1 << ",";
                   ptmMap[positions[enumerator.S_[k]-1]].push_back(*ptms[enumerator.S_[k]-1]);
               }
               //cout << endl;
               // Store the new sequence
			   subPeptides.push_back( ptmPeptide );
               //cout << getInterpretation(ptmPeptide) << endl;
           }
        }while(enumerator.next());
	}

    

	/**
		AddStaticMods adds the static modifications for a peptide. The static mods are read from
		the configuration variable "StaticMods" and added to each candidate reside in the peptide.
	*/
	DigestedPeptide AddStaticMods( const DigestedPeptide& unmodifiedPeptide, const StaticModSet& staticMods ) {
		const string& sequence = unmodifiedPeptide.sequence();
		size_t seqLength = sequence.size();

		// Apply static mods to the peptide
		DigestedPeptide staticPeptide( unmodifiedPeptide );
		ModificationMap& staticModMap = staticPeptide.modifications();
		for( size_t i=0; i < seqLength; ++i ) {
			for( StaticModSet::const_iterator itr = staticMods.begin(); itr != staticMods.end(); ++itr ) {
				if( itr->name == sequence[i] ) {
					staticModMap.insert( make_pair( i, *itr ) );
					break;
				}
			}
		}
		return staticPeptide;
	}

    /*
        This function is a kludge. It takes a protein, one of its peptide, and a possible delta mass.
        It then tries to see if the terminals of the peptide could be expanded to "explain away" the
        delta mass. This function comes in very handy during the semi-specific blind PTM searches. This
        should be handled in the next iteration in a much smarter way.
    */
    bool checkForTerminalErrors(const Peptide& protein, const DigestedPeptide& candidate, float modMass, float tolerance, TermType term)
    {
        const ModificationMap& mods = candidate.modifications();
        if(term == NTERM)
        {
            // First get the n-term mod mass and add it to the "candidate mod mass"
            float nTermModMass = 0.0;
            ModificationMap::const_iterator modIter = mods.find(mods.NTerminus());
            if(modIter != mods.end())
                nTermModMass = (*modIter).second.monoisotopicDeltaMass();
            modMass += nTermModMass;
            if(modMass>0.0)
            {
                // If the postive mod mass is on the n-terminus then 
                // grow the candidate back towards the protein n-term
                // and try to explain away the delta mass.
                size_t depth = 3;
                int begin = candidate.offset()-1;
                float cumulativeMass = 0.0;
                string proteinSequence = protein.sequence();
                while(begin >=0 && depth>=1)
                {
                    try {
                        cumulativeMass += AminoAcid::Info::record(proteinSequence[begin]).residueFormula.monoisotopicMass();
                    } catch(exception&) {}
                    if(fabs(modMass-cumulativeMass) <= tolerance)
                        return false;
                    --begin; --depth;
                }
            } else if(modMass < 0.0)
            {
                // If the negative mod mass is on the n-terminus then 
                // cut-off the residues from the candidate n-term and 
                // try to explain away the delta mass.
                size_t depth = 3;
                int begin = 0;
                float cumulativeMass = 0.0;
                string peptideSequence = candidate.sequence();
                while(begin <= peptideSequence.length() && depth >=1)
                {
                    try {
                        cumulativeMass += AminoAcid::Info::record(peptideSequence[begin]).residueFormula.monoisotopicMass();
                    } catch(exception&) {}
                    if(fabs(modMass+cumulativeMass)<= tolerance)
                        return false;
                    ++begin; --depth;
                }
            }
        } else if(term == CTERM)
        {
            // First get the c-term mod mass and add it to the "candidate mod mass"
            float cTermModMass = 0.0;
            ModificationMap::const_iterator modIter = mods.find(mods.CTerminus());
            if(modIter != mods.end())
                cTermModMass = (*modIter).second.monoisotopicDeltaMass();
            modMass += cTermModMass;
            if(modMass>0.0)
            {
                // If the positive mod mass is on the c-terminus then 
                // grow the candidate towards the protein c-term and 
                // try to explain away the delta mass.
                size_t depth = 3;
                int begin = candidate.offset() + candidate.sequence().length();
                float cumulativeMass = 0.0;
                string proteinSequence = protein.sequence();
                while(begin < proteinSequence.length() && depth>=1)
                {
                    try {
                        cumulativeMass += AminoAcid::Info::record(proteinSequence[begin]).residueFormula.monoisotopicMass();
                    } catch(exception&) {}
                    if(fabs(modMass-cumulativeMass)<= tolerance)
                        return false;
                    ++begin; --depth;
                }
            } else if(modMass < 0.0)
            {
                // If the negative mod mass is on the c-terminus then 
                // cut-off the residues from the candidate c-term and 
                // try to explain away the delta mass.
                size_t depth = 3;
                float cumulativeMass = 0.0;
                string peptideSequence = candidate.sequence();
                int begin = peptideSequence.length()-1;
                while(begin >= 0 && depth>=1)
                {
                    try {
                        cumulativeMass += AminoAcid::Info::record(peptideSequence[begin]).residueFormula.monoisotopicMass();
                    } catch(exception&) {}
                    if(fabs(modMass+cumulativeMass)<= tolerance)
                        return false;
                    --begin; --depth;
                }
            }
        }
        return true;
    }

	double poz( double z )
	{
		double y, x, w;
		double Z_MAX = 6.0; 

		if (z == 0.0) {
			x = 0.0;
		} else {
			y = 0.5 * fabs(z);
			if (y >= (Z_MAX * 0.5)) {
				x = 1.0;
			} else if (y < 1.0) {
				w = y * y;
				x = ((((((((0.000124818987 * w
					- 0.001075204047) * w + 0.005198775019) * w
					- 0.019198292004) * w + 0.059054035642) * w
					- 0.151968751364) * w + 0.319152932694) * w
					- 0.531923007300) * w + 0.797884560593) * y * 2.0;
			} else {
				y -= 2.0;
				x = (((((((((((((-0.000045255659 * y
					+ 0.000152529290) * y - 0.000019538132) * y
					- 0.000676904986) * y + 0.001390604284) * y
					- 0.000794620820) * y - 0.002034254874) * y
					+ 0.006549791214) * y - 0.010557625006) * y
					+ 0.011630447319) * y - 0.009279453341) * y
					+ 0.005353579108) * y - 0.002141268741) * y
					+ 0.000535310849) * y + 0.999936657524;
			}
		}
		return z > 0.0 ? ((x + 1.0) * 0.5) : ((1.0 - x) * 0.5);
	}

	static int BIGX = 20;
	static double LOG_SQRT_PI = 0.5723649429247000870717135; /* log(sqrt(pi)) */
	static double I_SQRT_PI = 0.5641895835477562869480795;   /* 1 / sqrt(pi) */

	double ex( double x )
	{
		return (x < -BIGX) ? 0.0 : exp(x);
	}

	double ChiSquaredToPValue( double x, int df )
	{
		double a, y=0.0, s;
		double e, c, z;
		bool even;


		if (x <= 0.0 || df < 1) {
			return 1.0;
		}

		a = 0.5 * x;
		even = !(df & 1);
		if (df > 1) {
			y = ex(-a);
		}
		s = (even ? y : (2.0 * poz(-sqrt(x))));
		if (df > 2) {
			x = 0.5f * double(df - 1.0);
			z = (even ? 1.0 : 0.5);
			if (a > BIGX) {
				e = (even ? 0.0 : LOG_SQRT_PI);
				c = log(a);
				while (z <= x) {
					e = log(z) + e;
					s += ex(c * z - a - e);
					z += 1.0;
				}
				return s;
			} else {
				e = (even ? 1.0 : (I_SQRT_PI / sqrt(a)));
				c = 0.0;
				while (z <= x) {
					e = e * (a / z);
					c = c + e;
					z += 1.0;
				}
				return double(c * y + s);
			}
		} else {
			return s;
		}
	}

	void LoadTagInstancesFromIndexFile( const string& tagIndexFilename, const string& tag, tagMetaIndex_t& tagMetaIndex, tagIndex_t& tagIndex )
	{
		if( tagMetaIndex.count( tag ) > 0 )
		{
			ifstream tagIndexFile( tagIndexFilename.c_str(), ios::binary );
			tagIndexFile.seekg( (ios::off_type) tagMetaIndex[tag].offset );

			tagIndexFile.read( (char*) &tagMetaIndex[tag].size, sizeof( tagMetaIndex[tag].size ) );

			ProteinIndex idx;
			ProteinOffset off;
			for( int i=0; i < tagMetaIndex[tag].size; ++i )
			{
				tagIndexFile.read( (char*) &idx, sizeof( idx ) );
				tagIndexFile.read( (char*) &off, sizeof( off ) );
				//cout << string(tag) << " " << (int) instanceCount << " " << idx << " " << off << endl;
				tagIndex[ tag ].push_back( tagInstance_t( idx, off ) );
			}
			tagIndexFile.close();
		} else
		{
			cerr << "Tag \"" << tag << "\" not in index!" << endl;
		}
	}

	void LoadIndexFromFile( const string& tagIndexFilename, tagMetaIndex_t& tagMetaIndex )
	{
		ifstream tagIndexFile;

		tagIndexFile.open( tagIndexFilename.c_str(), ios::in | ios::binary );

		tagIndexFile.seekg( 40 ); // skip the checksum

		int tagCount;
		tagIndexFile.read( (char*) &tagCount, sizeof( tagCount ) );
		tagIndexFile.read( (char*) &tagMetaIndex.totalTagInstances, sizeof( tagMetaIndex.totalTagInstances ) );

		for( int i=0; i < tagCount; ++i )
		{
			/* ASCII-style index
			string tag;
			tagIndexFile >> tag;

			int instanceCount;
			tagIndexFile >> instanceCount;

			for( int i=0; i < instanceCount; ++i )
			{
			int idx, off;
			tagIndexFile >> idx >> off;
			tagIndex[ tag ].push_back( tagInstance_t( idx, off ) );
			}
			*/

			/* Binary-style index */
			char* tag = new char[ 4 ];
			tagIndexFile.read( tag, 3 );
			tag[3] = 0;

			tagIndexFile.read( (char*) &tagMetaIndex[tag].offset, sizeof( tagMetaIndex[tag].offset ) );
			ios::pos_type cur = tagIndexFile.tellg();
			tagIndexFile.seekg( (ios::off_type) tagMetaIndex[tag].offset );
			tagIndexFile.read( (char*) &tagMetaIndex[tag].size, sizeof( tagMetaIndex[tag].size ) );
			tagIndexFile.seekg( cur );
			tagMetaIndex[tag].proportion = (float) tagMetaIndex[tag].size / (float) tagMetaIndex.totalTagInstances;
			delete tag;
		}
		tagIndexFile.close();
	}

	void	CalculateSequenceIons(	const Peptide& peptide,
									int maxIonCharge,
									vector< double >* sequenceIonMasses,
									const FragmentTypesBitset& fragmentTypes,
									bool useSmartPlusThreeModel,
									vector< string >* sequenceIonLabels,
									float precursorMass )
	{
		if( !sequenceIonMasses )
			return;

        const string& seq = peptide.sequence();
        size_t seqLength = seq.length();

		sequenceIonMasses->clear();

		if( sequenceIonLabels )
			sequenceIonLabels->clear();

		//bool nTerminusIsPartial = ( *seq.begin() == '-' );
		//bool cTerminusIsPartial = ( *seq.rbegin() == '-' );

        Fragmentation fragmentation = peptide.fragmentation(true, true);

		// calculate y ion MZs
		if( maxIonCharge > 2 )
		{
			if( useSmartPlusThreeModel )
			{
				size_t totalStrongBasicCount = 0, totalWeakBasicCount = 0;
				for( size_t i=0; i < seqLength; ++i )
					if( seq[i] == 'R' || seq[i] == 'K' || seq[i] == 'H' )
						++totalStrongBasicCount;
					else if( seq[i] == 'Q' || seq[i] == 'N' )
						++totalWeakBasicCount;
				size_t totalBasicity = totalStrongBasicCount * 4 + totalWeakBasicCount * 2 + seq.length()-2;

				map< double, int > basicityThresholds;
				basicityThresholds[ 0.0 ] = 1;
				for( int z = 1; z < maxIonCharge-1; ++z )
					basicityThresholds[ (double) z / (double) (maxIonCharge-1) ] = z+1;

				for( size_t c = 0; c <= seqLength; ++c )
				{
					size_t bStrongBasicCount = 0, bWeakBasicCount = 0;
					for( size_t i=0; i < c; ++i )
						if( seq[i] == 'R' || seq[i] == 'K' || seq[i] == 'H' )
							++bStrongBasicCount;
						else if( seq[i] == 'Q' || seq[i] == 'N' )
							++bWeakBasicCount;

					size_t bScore = bStrongBasicCount * 4 + bWeakBasicCount * 2 + c;

					double basicityRatio = (double) bScore / (double) totalBasicity;
					map< double, int >::iterator itr = basicityThresholds.upper_bound( basicityRatio );
					--itr;
					int bZ = itr->second;
					int yZ = maxIonCharge - bZ;

					//cout << "b" << c+1 << "(+" << bZ << ") <-> y" << seq.length()-(c+3) << "(+" << yZ << ")" << endl;
					//cout << bSeq << " <-> " << ySeq << endl;
					//cout << bMass << " <-> " << yMass << endl << endl;

                    size_t nLength = c;
                    size_t cLength = seqLength - c;

                    if( nLength > 0 )
                    {
                        if( fragmentTypes[FragmentType_A] )
					    {
						    if( sequenceIonLabels )
							    sequenceIonLabels->push_back( string("a") + lexical_cast<string>(nLength) + "(+" + lexical_cast<string>(bZ) + ")" );
						    sequenceIonMasses->push_back( fragmentation.a(nLength, bZ) );
					    }

                        if( fragmentTypes[FragmentType_B] )
					    {
						    if( sequenceIonLabels )
							    sequenceIonLabels->push_back( string("b") + lexical_cast<string>(nLength) + "(+" + lexical_cast<string>(bZ) + ")" );
						    sequenceIonMasses->push_back( fragmentation.b(nLength, bZ) );
					    }

					    if( fragmentTypes[FragmentType_C] && nLength < seqLength )
					    {
						    if( sequenceIonLabels )
							    sequenceIonLabels->push_back( string("c") + lexical_cast<string>(nLength) + "(+" + lexical_cast<string>(bZ) + ")" );
						    sequenceIonMasses->push_back( fragmentation.c(nLength, bZ) );
					    }
                    }

                    if( cLength > 0 )
                    {
					    if( fragmentTypes[FragmentType_X] && cLength < seqLength )
					    {
						    if( sequenceIonLabels )
							    sequenceIonLabels->push_back( string("x") + lexical_cast<string>(cLength) + "(+" + lexical_cast<string>(yZ) + ")" );
						    sequenceIonMasses->push_back( fragmentation.x(cLength, yZ) );
					    }

                        if( fragmentTypes[FragmentType_Y] )
					    {
						    if( sequenceIonLabels )
							    sequenceIonLabels->push_back( string("y") + lexical_cast<string>(cLength) + "(+" + lexical_cast<string>(yZ) + ")" );
						    sequenceIonMasses->push_back( fragmentation.y(cLength, yZ) );
					    }

                        if( fragmentTypes[FragmentType_Z] )
					    {
						    if( sequenceIonLabels )
							    sequenceIonLabels->push_back( string("z") + lexical_cast<string>(cLength) + "(+" + lexical_cast<string>(yZ) + ")" );
						    sequenceIonMasses->push_back( fragmentation.z(cLength, yZ) + 2*Proton/yZ  );
					    }

                        if( fragmentTypes[FragmentType_Z_Radical] )
					    {
						    if( sequenceIonLabels )
							    sequenceIonLabels->push_back( string("z") + lexical_cast<string>(cLength) + "*(+" + lexical_cast<string>(yZ) + ")" );
						    sequenceIonMasses->push_back( fragmentation.zRadical(cLength, yZ) );
					    }
                    }
				}
			} else
			{
				for( int z = 1; z < maxIonCharge; ++z )
				{
					for( size_t c = 0; c < seqLength; ++c )
					{
						size_t nLength = c;
                        size_t cLength = seqLength - c;

                        if( nLength > 0 )
                        {
                            if( fragmentTypes[FragmentType_A] )
					        {
						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("a") + lexical_cast<string>(nLength) + "(+" + lexical_cast<string>(z) + ")" );
						        sequenceIonMasses->push_back( fragmentation.a(nLength, z) );
					        }

                            if( fragmentTypes[FragmentType_B] )
					        {
						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("b") + lexical_cast<string>(nLength) + "(+" + lexical_cast<string>(z) + ")" );
						        sequenceIonMasses->push_back( fragmentation.b(nLength, z) );
					        }

					        if( fragmentTypes[FragmentType_C] && nLength < seqLength )
					        {
						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("c") + lexical_cast<string>(nLength) + "(+" + lexical_cast<string>(z) + ")" );
						        sequenceIonMasses->push_back( fragmentation.c(nLength, z) );

						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("c-1") + lexical_cast<string>(nLength) + "(+" + lexical_cast<string>(z) + ")" );
                                sequenceIonMasses->push_back( fragmentation.c(nLength, z) - pwiz::chemistry::Proton / z );
					        }
                        }

                        if( cLength > 0 )
                        {
					        if( fragmentTypes[FragmentType_X] && cLength < seqLength )
					        {
						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("x") + lexical_cast<string>(cLength) + "(+" + lexical_cast<string>(z) + ")" );
						        sequenceIonMasses->push_back( fragmentation.x(cLength, z) );
					        }

                            if( fragmentTypes[FragmentType_Y] )
					        {
						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("y") + lexical_cast<string>(cLength) + "(+" + lexical_cast<string>(z) + ")" );
						        sequenceIonMasses->push_back( fragmentation.y(cLength, z) );
					        }

                            if( fragmentTypes[FragmentType_Z] )
					        {
						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("z") + lexical_cast<string>(cLength) + "(+" + lexical_cast<string>(z) + ")" );
						        sequenceIonMasses->push_back( fragmentation.z(cLength, z) );
					        }

                            if( fragmentTypes[FragmentType_Z_Radical] )
					        {
						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("z") + lexical_cast<string>(cLength) + "*(+" + lexical_cast<string>(z) + ")" );
						        sequenceIonMasses->push_back( fragmentation.zRadical(cLength, z) );

						        if( sequenceIonLabels )
							        sequenceIonLabels->push_back( string("z+1") + lexical_cast<string>(cLength) + "(+" + lexical_cast<string>(z) + ")" );
                                sequenceIonMasses->push_back( fragmentation.zRadical(cLength, z) + pwiz::chemistry::Proton / z );
					        }
                        }
                    }
				}
			}
		} else
		{
			for( size_t c = 0; c < seqLength; ++c )
			{
                size_t nLength = c;
                size_t cLength = seqLength - c;

                if( nLength > 0 )
                {
                    if( fragmentTypes[FragmentType_A] )
				    {
					    if( sequenceIonLabels )
						    sequenceIonLabels->push_back( string("a") + lexical_cast<string>(nLength) );
					    sequenceIonMasses->push_back( fragmentation.a(nLength, 1) );
				    }

                    if( fragmentTypes[FragmentType_B] )
				    {
					    if( sequenceIonLabels )
						    sequenceIonLabels->push_back( string("b") + lexical_cast<string>(nLength) );
					    sequenceIonMasses->push_back( fragmentation.b(nLength, 1) );
				    }

				    if( fragmentTypes[FragmentType_C] && nLength < seqLength )
				    {
					    if( sequenceIonLabels )
						    sequenceIonLabels->push_back( string("c") + lexical_cast<string>(nLength) );
					    sequenceIonMasses->push_back( fragmentation.c(nLength, 1) );
				    }
                }

                if( cLength > 0 )
                {
				    if( fragmentTypes[FragmentType_X] && cLength < seqLength )
				    {
					    if( sequenceIonLabels )
						    sequenceIonLabels->push_back( string("x") + lexical_cast<string>(cLength) );
					    sequenceIonMasses->push_back( fragmentation.x(cLength, 1) );
				    }

                    if( fragmentTypes[FragmentType_Y] )
				    {
					    if( sequenceIonLabels )
						    sequenceIonLabels->push_back( string("y") + lexical_cast<string>(cLength) );
					    sequenceIonMasses->push_back( fragmentation.y(cLength, 1) );
				    }

                    if( fragmentTypes[FragmentType_Z] )
				    {
					    if( sequenceIonLabels )
						    sequenceIonLabels->push_back( string("z") + lexical_cast<string>(cLength) );
					    sequenceIonMasses->push_back( fragmentation.z(cLength, 1) );
				    }

                    if( fragmentTypes[FragmentType_Z_Radical] )
				    {
					    if( sequenceIonLabels )
						    sequenceIonLabels->push_back( string("z") + lexical_cast<string>(cLength) + "*(+" + lexical_cast<string>(1) + ")" );
					    sequenceIonMasses->push_back( fragmentation.zRadical(cLength, 1) );
				    }
                }
			}
		}
	}

	void	CreateScoringTableMVH_R(	const int minValue,
		const int totalValue,
		const int numClasses,
		const vector< int >& classCounts,
		MvhTable& mvTable,
		MvIntKey& key,
		lnFactorialTable& lnFT,
		const MvIntKey* minKey = NULL )
	{
		// At the highest degree of variability the key is fully set
		// Calculate the MVH score and add it to the mvTable
		if( numClasses == 1 )
		{
			key.front() = totalValue;
			//if( minKey == NULL || !( mvTable.comp( *minKey, key ) ) )
			//	return;

			int totalClasses = (int) key.size();
			double lnP = 0.0f;
			for( int i=0; i < totalClasses; ++i )
				lnP += lnCombin( classCounts[i], key[i], lnFT );
			//float p = 0.0f;
			//for( int i=0; i < totalClasses; ++i )
			//	p += lnCombin( classCounts[i], key[i], lnFT );
			int totalClassCount = accumulate( classCounts.begin(), classCounts.end(), 0 );
			int totalValueCount = accumulate( key.begin(), key.end(), 0 );
			lnP -= lnCombin( totalClassCount, totalValueCount, lnFT );
			//START_PROFILER(9);
			mvTable[ key ] = lnP;
			//STOP_PROFILER(9);

			// Create another level of variability
		} else
		{
			for( int curValue = minValue; (totalValue - curValue) >= minValue ; ++curValue )
			{
				key[numClasses-1] = curValue;
				CreateScoringTableMVH_R( minValue, totalValue - curValue, numClasses-1, classCounts, mvTable, key, lnFT, minKey );
			}
		}
	}

	void	CreateScoringTableMVH(	const int minValue,
		const int totalValue,
		const int numClasses,
		vector< int > classCounts,
		MvhTable& mvTable,
		lnFactorialTable& lnFT,
		bool normalizeOnMode,
		bool adjustRareOutcomes,
		bool convertToPValues,
		const MvIntKey* minKey )
	{
		// Check to see if all classes have a count of at least 1
		bool allClassesUsed = true;
		for( int i=0; i < numClasses; ++i )
		{
			if( classCounts[i] == 0 )
			{
				allClassesUsed = false;
				break;
			}
		}

		// If any class is not populated, increment each class by one
		if( !allClassesUsed )
			for( int i=0; i < numClasses; ++i )
				++ classCounts[i];

		MvIntKey key;
		key.resize( numClasses, 0 );
		//START_PROFILER(10);
		CreateScoringTableMVH_R( minValue, totalValue, numClasses, classCounts, mvTable, key, lnFT, minKey );
		//STOP_PROFILER(10);

		if( convertToPValues )
		{
			mvTable.ConvertToPValues();
		} else
			if( normalizeOnMode )
			{
				// Normalize on the mode value if desired
				MvhTable::iterator itr;
				MvIntKey modeKey = mvTable.begin()->first;
				double modeValue = mvTable.begin()->second;

				for( itr = mvTable.begin(); itr != mvTable.end(); ++itr )
				{
					if( modeValue < itr->second )
					{
						modeKey = itr->first;
						modeValue = itr->second;
					}
				}

				for( itr = mvTable.begin(); itr != mvTable.end(); ++itr )
					itr->second -= modeValue;

				if( adjustRareOutcomes )
				{
					// Prevent rare and undesirable outcomes from having good scores
					for( itr = mvTable.begin(); itr != mvTable.end(); ++itr )
					{
						key = itr->first;
						bool IsRareAndUndesirable = true;
						int numVarsToAdjust = (int) key.size() - 1;
						for( int i=0; i < numVarsToAdjust ; ++i )
						{
							if( key[i] > modeKey[i] )
							{
								IsRareAndUndesirable = false;
								break;
							}
						}

						if( IsRareAndUndesirable )
							itr->second = - itr->second;
					}
				}
			}
	}

	void	CreateScoringTableMVB_R(	const int minValue,
		const int totalValue,
		const int numClasses,
		const vector< double >& classProbabilities,
		MvhTable& mvTable,
		MvIntKey& key,
		lnFactorialTable& lnFT )
	{
		// At the highest degree of variability the key is fully set
		// Calculate the MVH score and add it to the mvTable
		if( numClasses == 1 )
		{
			key.front() = totalValue;
			int totalClasses = (int) key.size();
			int N = accumulate( key.begin(), key.end(), 0 );
			double sum1 = 0, sum2 = 0;
			for( int i=0; i < totalClasses; ++i )
			{
				sum1 += log( pow( classProbabilities[i], key[i] ) );
				sum2 += lnFT[ key[i] ];
			}
			mvTable[ key ] = ( lnFT[N] - sum2 ) + sum1;

			// Create another level of variability
		} else
		{
			for( int curValue = minValue; (totalValue - curValue) >= minValue ; ++curValue )
			{
				key[numClasses-1] = curValue;
				CreateScoringTableMVB_R( minValue, totalValue - curValue, numClasses-1, classProbabilities, mvTable, key, lnFT );
			}
		}
	}

	void	CreateScoringTableMVB(	const int minValue,
		const int totalValue,
		const int numClasses,
		const vector< double >& classProbabilities,
		MvhTable& mvTable,
		lnFactorialTable& lnFT,
		bool normalizeOnMode,
		bool adjustRareOutcomes )
	{
		MvIntKey key;
		key.resize( numClasses, 0 );
		CreateScoringTableMVB_R( minValue, totalValue, numClasses, classProbabilities, mvTable, key, lnFT );

		if( normalizeOnMode )
		{
			// Normalize on the mode value if desired
			MvhTable::iterator itr;
			MvIntKey modeKey = mvTable.begin()->first;
			double modeValue = mvTable.begin()->second;

			for( itr = mvTable.begin(); itr != mvTable.end(); ++itr )
			{
				if( modeValue < itr->second )
				{
					modeKey = itr->first;
					modeValue = itr->second;
				}
			}

			for( itr = mvTable.begin(); itr != mvTable.end(); ++itr )
				itr->second -= modeValue;

			if( adjustRareOutcomes )
			{
				// Prevent rare and undesirable outcomes from having good scores
				for( itr = mvTable.begin(); itr != mvTable.end(); ++itr )
				{
					key = itr->first;
					bool IsRareAndUndesirable = true;
					int numVarsToAdjust = (int) key.size() - 1;
					for( int i=0; i < numVarsToAdjust ; ++i )
					{
						if( key[i] > modeKey[i] )
						{
							IsRareAndUndesirable = false;
							break;
						}
					}

					if( IsRareAndUndesirable )
						itr->second = 0;
				}
			}
		}
	}


	int	ClassifyError( double error, const vector< double >& mzFidelityThresholds )
	{
		for( int i=0; i < (int) mzFidelityThresholds.size(); ++i )
		{
			if( error <= mzFidelityThresholds[i] )
				return i;
		}
		return (int) mzFidelityThresholds.size()-1;

		cout.precision(8);
		cout << "ClassifyError: could not classify error " << error << " in thresholds:\n";
		for( int i=0; i < (int) mzFidelityThresholds.size(); ++i )
			cout << mzFidelityThresholds[i] << " ";
		cout << endl;
		return 0;
	}

	double lnCombin( int n, int k, lnFactorialTable& lnTable )
	{
		if( n < 0 || k < 0 || n < k )
			return -1;

		try
		{
			return lnTable[n] - lnTable[n-k] - lnTable[k];
		} catch( std::exception& e )
		{
			cerr << "lnCombin(): caught exception with n=" << n << " and k=" << k << endl;
			throw e;
		}
	}

	float lnOdds( float p )
	{
		return log( p / (1 - p) );
	}

	int paramIndex( const string& param, const char** atts, int attsCount )
	{
		for( int i=0; i < attsCount; ++ i )
		{
			if( !strcmp( atts[i], param.c_str() ) )
				return i;
		}
		//cerr << "Attribute \"" << param << "\" required but not specified." << endl;
		return -1;
	}

	void FindFilesByMask( const string& mask, fileList_t& filenames )
	{
#ifdef WIN32
		string maskPathname = GetPathnameFromFilepath( mask );
		WIN32_FIND_DATA fdata;
		HANDLE srcFile = FindFirstFileEx( mask.c_str(), FindExInfoStandard, &fdata, FindExSearchNameMatch, NULL, 0 );
		if( srcFile == INVALID_HANDLE_VALUE )
			return;

		do
		{
			filenames.insert( maskPathname + fdata.cFileName );
		} while( FindNextFile( srcFile, &fdata ) );

		FindClose( srcFile );

#else

		glob_t globbuf;
		int rv = glob( mask.c_str(), 0, NULL, &globbuf );
		if( rv > 0 && rv != GLOB_NOMATCH )
			throw runtime_error( "FindFilesByMask(): glob() error" );

		DIR* curDir = opendir( "." );
		struct stat curEntryData;

		for( size_t i=0; i < globbuf.gl_pathc; ++i )
		{
			stat( globbuf.gl_pathv[i], &curEntryData );
			if( S_ISREG( curEntryData.st_mode ) )
				filenames.insert( globbuf.gl_pathv[i] );
		}
		closedir( curDir );

		globfree( &globbuf );

#endif
	}

	string FindFileInSearchPath( const string& filename, vector<string> searchPathList )
	{
		BOOST_FOREACH( string searchPath, searchPathList )
		{
			path filepath = path(searchPath) / path(filename).leaf();
			if( exists(filepath) )
				return filepath.string();
		}
		return "";
	}

	endianType_t GetMachineEndianType()
	{
		int testInt = 127;
		char* testIntP = (char*) &testInt;

		if( testIntP[0] == 127 )
			return SYS_LITTLE_ENDIAN;
		else if( testIntP[ sizeof(int)-1 ] == 127 )
			return SYS_BIG_ENDIAN;
		else
			return SYS_UNKNOWN_ENDIAN;
	}
}
