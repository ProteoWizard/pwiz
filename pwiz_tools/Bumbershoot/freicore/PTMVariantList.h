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
// The Original Code is the MyriMatch search engine.
//
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

#ifndef _PTMVARIANTITERATOR_H
#define _PTMVARIANTITERATOR_H

#include "SubsetEnumerator.h"
#include "shared_funcs.h"

class PTMVariantList {

private:

    // Maximum number of variants allowed for this peptide.
    size_t maximumVariants;

    // An array of ints to store the index of the amino acid
    // in the sequence to which a modification corresponds.
    vector <size_t> positions;

    // An array to store the ptms that can be present on a residue
    vector < DynamicModSet::const_iterator > ptms;

    // Maximum number of modified variants allowed per peptide
    size_t maxPtmCount;

    // Enumerates subsets between two numbers.
    SubsetEnumerator enumerator;

    // Holds the statically modified variant
    DigestedPeptide staticVariant;

    // Holds the index of the current variant
    size_t variantIndex;

    /*
        This function takes a peptide sequence, modification, and a amino acid position.
        It checkes to see if the modification can go at that site.
    */
    bool TestPtmSite( const string& seq, const DynamicMod& mod, size_t pos )
    {
        // Check the amino acid at the site.
        if( seq[pos] != mod.unmodChar )
            return false;

        // If the position matches then we have to check 
        // if we are handling a n-terminal mod
        if(pos > 0) {
            // Check if any n-terminal filters match the peptide n-terminal
            for( size_t i=1; i <= mod.NTerminalFilters.size(); ++i )
            {
                if( pos-i < 0 )
                    return false;

                const ResidueFilter& filter = *(mod.NTerminalFilters.rbegin()+(i-1));
                if( !filter.testResidue( seq[pos-i] ) )
                    return false;
            }
        }

        // If we are checking the c-terminal mod
        if(pos < seq.length()-1) {
            // Check if any c-terminal filters match the peptide c-terminal
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

    /*
        This function computes the number of variants possible for the current
        peptide in the worst case scenario. It takes the number of PTMs, and the 
        maximum number of PTMs allowed per peptide.
    */
    inline size_t countMaxVariants(size_t numOfPossibleMods, size_t maxPTMCount) {

        // if the number of sites is <= maxPTMCount then return (2^numOfPossiblePTMs). For example, 
        // if we have 6 possible sites and 6 mods are allowed per peptide, then the total number 
        // of possibe variants is 6c0 + 6c1 + 6c2 + 6c3 + 6c4 + 6c5 + 6c6 = 2^6.
        if(numOfPossibleMods <= maxPTMCount)
            return (size_t) pow(2.0,(int) numOfPossibleMods) ;

        // Other wise we have to compute the combinatorial math. For example, if we have 6 
        // possible sites and only 4 mods are allowed per peptide, then the total number of 
        // possibe variants is 6c0 + 6c1 + 6c2 + 6c3 + 6c4. The combinatorial math is computed
        // in log domain for efficency purposes.
        size_t n_ = numOfPossibleMods;
        // Init the total mods to nc0 (6c0 in the above example)
        double totalMods = 1.0;
        // Compute the rest of the series
        for(size_t k_ = 1; k_ <= maxPTMCount; ++k_)
            totalMods += exp(lnCombin(n_, k_));
        return (size_t) round(totalMods);
    }

public:

    // Holds the generated PTM variant
    DigestedPeptide ptmVariant;

    // Holds the number of possible variants for this peptide
    size_t numVariants;

    // Is the peptide skipped
    bool isSkipped;

    // Constructor
    PTMVariantList(const DigestedPeptide& peptide, int maxPTMs, const freicore::DynamicModSet& dynamicMods,
        const freicore::StaticModSet& staticMods, int maxIters) :
    maxPtmCount(maxPTMs), staticVariant(peptide), ptmVariant(peptide) {

        // Get the sequence with termini and measure its length
        string sequenceWithTermini = PEPTIDE_N_TERMINUS_STRING + peptide.sequence() + PEPTIDE_C_TERMINUS_STRING;
        size_t seqLength = sequenceWithTermini.length();

        // Apply static mods to the peptide
        ModificationMap& staticModMap = staticVariant.modifications();
        // For each amino acid
        for( size_t i=0; i < seqLength; ++i ) {
            // Iterate over the mods and see if you can add any of them
            size_t ptmIndex = (i==0) ? staticModMap.NTerminus() : (i < seqLength-1) ? i-1 : staticModMap.CTerminus();
            for( StaticModSet::const_iterator itr = staticMods.begin(); itr != staticMods.end(); ++itr ) {
                if( itr->name == sequenceWithTermini[i] )
                {
                    staticModMap.insert( make_pair( ptmIndex, *itr ) );
                    break;
                }
            }
        }

        // For each amino acid
        for( size_t i=0; i < seqLength && maxPtmCount > 0; ++i ) {
            // Check to see if any ptms can go on it
            for( DynamicModSet::const_iterator itr = dynamicMods.begin(); itr != dynamicMods.end(); ++itr ) {
                // If they do then remember the amino acid position and the modification
                if( TestPtmSite( sequenceWithTermini, *itr, i ) ) {
                    ptms.push_back(itr);
                    size_t pos = (i==0) ? staticModMap.NTerminus() : (i < seqLength-1) ? i-1 : staticModMap.CTerminus();
                    positions.push_back(pos);
                }
            }
        }

        // If the maximum number of possible peptide variants are 
        // above the user set threshold then just return without 
        // generating them.
        numVariants = countMaxVariants(positions.size(), (size_t) maxPtmCount);
        maximumVariants = (size_t) maxIters;
        isSkipped = false;
        if(numVariants > maximumVariants)
            isSkipped = true;
    
        // Get a subset enumerator
        enumerator = SubsetEnumerator(positions.size(),1,min((size_t)maxPtmCount,positions.size()),positions);
        ptmVariant = staticVariant;
        variantIndex = 0;
    }

    ~PTMVariantList() {
    }

    /*
        This function iterates over the number of possible variants and generates
        them one-by-one, which ignoring the unmodified peptide. DO NOT mix this
        function call with next() or getVariantsAsList(vector,bool).
    */
    bool nextWithoutStaticPeptide() {
        if(positions.size() == 0 || isSkipped) 
            return false;

        enumerator.next();
        if(enumerator.setFitness) {
            // Get the digested peptide and the mod map
            ptmVariant = staticVariant;
            ModificationMap& ptmMap = ptmVariant.modifications();
            // Add the mods
            for(size_t k = 1; k <= enumerator.k_; ++k) {
                //cout << positions[enumerator.S_[k]-1] <<"-"<< enumerator.S_[k]-1 << ",";
                ptmMap[positions[enumerator.S_[k]-1]].push_back(*ptms[enumerator.S_[k]-1]);
            }
        }
        ++variantIndex;
        return (variantIndex < numVariants);
    }

    /*
        This function iterates over the number of possible variants and generates them
        one-by-one. DO NOT mix this function call with nextWithoutStaticPeptide() or
        getVariantsAsList(vector,bool).
    */
    bool next() {

        // Skip if there are no mods or too many mods
        if(positions.size() == 0 || isSkipped) 
            return false;

        if(enumerator.setFitness) {
            // Get the digested peptide and the mod map
            ptmVariant = staticVariant;
            ModificationMap& ptmMap = ptmVariant.modifications();
            // Add the mods
            for(size_t k = 1; k <= enumerator.k_; ++k) {
                //cout << positions[enumerator.S_[k]-1] <<"-"<< enumerator.S_[k]-1 << ",";
                ptmMap[positions[enumerator.S_[k]-1]].push_back(*ptms[enumerator.S_[k]-1]);
            }
        }
        // Get the next variant
        ++variantIndex;
        enumerator.next();
        return (variantIndex < numVariants);
    }

    /*
        This function makes the variants of the peptide and adds them to a list.
        DO NOT mix this function call with next() or nextWithoutStaticPeptide()
        function calls.
    */
    void getVariantsAsList(vector<DigestedPeptide>& list, bool withStaticForm = true) {

        if(withStaticForm)
            list.push_back(staticVariant);        
        
        if(positions.size()==0 || isSkipped)
            return;
        
        do {
            if(enumerator.setFitness) {
                // Get the digested peptide and the mod map
                ptmVariant = staticVariant;
                ModificationMap& ptmMap = ptmVariant.modifications();
                // Add the mods
                for(size_t k = 1; k <= enumerator.k_; ++k) {
                    //cout << positions[enumerator.S_[k]-1] <<"-"<< enumerator.S_[k]-1 << ",";
                    ptmMap[positions[enumerator.S_[k]-1]].push_back(*ptms[enumerator.S_[k]-1]);
                }
                list.push_back(ptmVariant);
            }
        } while(enumerator.next());
    }
};


#endif
