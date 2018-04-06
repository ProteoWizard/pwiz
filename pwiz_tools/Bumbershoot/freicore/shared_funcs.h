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

#ifndef _SHARED_FUNCS_H
#define _SHARED_FUNCS_H

#include "stdafx.h"
#include "shared_types.h"
#include "shared_defs.h"
#include "lnFactorialTable.h"
#include "ResidueMap.h"
#include "BaseRunTimeConfig.h"
#include "BaseSpectrum.h"
#include <boost/math/distributions/normal.hpp> // for normal_distribution
using boost::math::normal; // typedef provides default type is double.

using namespace freicore;

namespace std
{
    ostream&        operator<< ( ostream& o, const ProteinLocusByIndex& rhs );
    ostream&        operator<< ( ostream& o, const ProteinLocusByName& rhs );

    
    ostream&        operator<< ( ostream& o, const CleavageRule& rhs );
    istream&        operator>> ( istream& i, CleavageRule& rhs );
    istream&        operator>> ( istream& i, CleavageRuleSet& rhs );

    template< class T >
    ostream&        operator<< ( ostream& o, const topset<T>& rhs )
    {
        return o << reinterpret_cast< const set<T>& >( rhs );
    }

    ostream& operator<< ( ostream& o, MvIntKey& rhs );
    ostream& operator<< ( ostream& o, MvhTable& rhs );
}

namespace freicore
{
    double            lnCombin( int n, int k, lnFactorialTable& lnTable = g_lnFactorialTable );
    float            lnOdds( float p );
    
    class CustomBaseNumber
    {
    public:
        /// creates a number which is manipulated in the specified base (radix)
        CustomBaseNumber(unsigned int valueAsBase10, size_t base);
        
        /// returns a string representation of the number in its base
        string str() const;

        /// returns the number of digits needed to represent the number in its base
        size_t size() const;

        /// shift digits left
        CustomBaseNumber& operator<< (size_t shift);

        /// shift digits right
        CustomBaseNumber& operator>> (size_t shift);

        /// pre-increments the number by one
        CustomBaseNumber& operator++();
        
        /// returns the value of the Nth digit of the number, from [0, base)
        int digit(size_t n) const;

        /// returns the number of digits that are non-zero
        int nonZeroDigits() const;

    private:
        void incrementChar(int i);
        deque<char> digits_;
        size_t base_;
        size_t nonZeroDigits_;
    };

    bool            MakePtmVariants( const DigestedPeptide& unmodifiedPeptide,
                                     vector<DigestedPeptide>& ptmPeptides,
                                     int maxPtmCount,
                                     const DynamicModSet& dynamicMods,
                                     const StaticModSet& staticMods, size_t maxNumIters );

    void            MakePtmVariantsOld( const DigestedPeptide& unmodifiedPeptide,
                                     vector<DigestedPeptide>& ptmPeptides,
                                     int maxPtmCount,
                                     const DynamicModSet& dynamicMods,
                                     const StaticModSet& staticMods);
    
    bool            MakePtmVariantsWithMixedRadixEnumerator( const DigestedPeptide& unmodifiedPeptide,
                                     vector<DigestedPeptide>& ptmPeptides,
                                     int maxPtmCount,
                                     const DynamicModSet& dynamicMods,
                                     const StaticModSet& staticMods, size_t maxNumIters );

    void            getDynamicPTMVariables(const DigestedPeptide& unmodifiedPeptide, const DynamicModSet& mods, 
                                            size_t maxPTMCount, size_t& totalPermutations, size_t& totalModifiedPeptides);
   
    DigestedPeptide    AddStaticMods(const DigestedPeptide & unmodifiedPeptide, const StaticModSet& staticMods);

    void            MakePeptideVariants( const DigestedPeptide& peptide, vector<DigestedPeptide>& subPeptides,
                                    size_t minModCount, size_t maxModCount, const DynamicModSet& dynamicSubs, 
                                    size_t locStart, size_t locEnd);
    
    double            ChiSquaredToPValue( double x, int df );

    int                ClassifyError( double error, const vector< double >& mzFidelityThresholds );

    void            LoadTagInstancesFromIndexFile( const string& tagIndexFilename, const string& tag, tagMetaIndex_t& tagMetaIndex, tagIndex_t& tagIndex );
    void            LoadIndexFromFile( const string& tagIndexFilename, tagMetaIndex_t& tagMetaIndex );

    void            CalculateSequenceIons(    const Peptide& peptide,
                                            int maxIonCharge,
                                            vector< double >* sequenceIonMasses,
                                            const FragmentTypesBitset& fragmentTypes,
                                            bool useSmartPlusThreeModel = true,
                                            vector< string >* sequenceIonLabels = NULL,
                                            float precursorMass = 0.0f );

    void            CreateScoringTableMVH(    const int minValue,
                                            const int totalValue,
                                            const int numClasses,
                                            vector< int > classCounts,
                                            MvhTable& mvTable,
                                            lnFactorialTable& lnFT,
                                            bool NormalizeOnMode = false,
                                            bool adjustRareOutcomes = true,
                                            bool convertToPValues = false,
                                            const MvIntKey* minKey = NULL );

    void            CreateScoringTableMVB(    const int minValue,
                                            const int totalValue,
                                            const int numClasses,
                                            const vector< float >& classProbabilities,
                                            MvhTable& mvTable,
                                            lnFactorialTable& lnFT,
                                            bool NormalizeOnMode = false,
                                            bool adjustRareOutcomes = true );

    int                paramIndex( const string& param, const char** atts, int attsCount );

    void            FindFilesByMask( const string& mask, fileList_t& filenames );

    string            getInterpretation(const DigestedPeptide& peptide);

    bool            TestPtmSite( const string& seq, const DynamicMod& mod, size_t pos );

    /// looks in each directory in searchPathList for filename
    /// returns the absolute filepath of the filename if found, else empty string
    string FindFileInSearchPath( const string& filename, vector<string> searchPathList );
    // An utility function to check if a mod mass on a terminal is illegal
    bool checkForTerminalErrors(const Peptide& protein, const DigestedPeptide& candidate, float modMass, float tolerance, TermType term);
    // An inline function that takes two masses and computes the mass difference between them
    // based on the mass units
    inline double    ComputeMassError(double observedMass, double expectedMass, MassUnits units) 
    {
        // Compute the delta mass
        double delta = fabs(observedMass - expectedMass);
        // If the mass units are in PPM
        if(units == PPM) 
        {
            //       |obs-exp|     6
            //PPM = ---------- * 10
            //            exp
            delta = (delta/expectedMass) * 1e6;
        }
        return delta;
    }

    inline double getLogPvalForRankSum(int observedRankSum, int populationSize, int sampleSize, int numTies = 0)
    {
        double mean = ((populationSize+1)*sampleSize)/2.0;
        double stdev = sqrt(mean*(populationSize-sampleSize)/6.0);
        stdev = max(1.0,stdev);
        normal distribution(mean, stdev);
        return log(cdf(complement(distribution, observedRankSum)) + DBL_EPSILON);
    }
}

#endif
