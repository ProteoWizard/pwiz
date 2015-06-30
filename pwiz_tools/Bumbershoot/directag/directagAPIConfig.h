//
// $Id: directagConfig.h 56 2012-03-08 19:28:34Z dasari $
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
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Zeqiang Ma
//

#ifndef _DIRECTAGAPICONFIG_H
#define _DIRECTAGAPICONFIG_H

#include "stdafx.h"
#include "freicore.h"
#include "BaseRunTimeConfig.h"
#include <boost/atomic.hpp>
#include <boost/accumulators/accumulators.hpp>
#include <boost/accumulators/statistics/stats.hpp>
#include <boost/accumulators/statistics/mean.hpp>
#include <boost/accumulators/statistics/min.hpp>
#include <boost/accumulators/statistics/max.hpp>


using namespace freicore;
namespace accs = boost::accumulators;

#define DIRECTAG_API_CONFIG \
    COMMON_RTCONFIG \
    RTCONFIG_VARIABLE( int,                MaxTagCount,                50                    ) \
    RTCONFIG_VARIABLE( double,            MaxTagScore,                20.0                ) \
    RTCONFIG_VARIABLE( int,                NumIntensityClasses,        3                    ) \
    RTCONFIG_VARIABLE( int,                NumMzFidelityClasses,        3                    ) \
    RTCONFIG_VARIABLE( int,                TagLength,                    3                    ) \
    RTCONFIG_VARIABLE( double,            TicCutoffPercentage,        1.0                    ) \
    RTCONFIG_VARIABLE( size_t,            MaxPeakCount,                100                    ) \
    RTCONFIG_VARIABLE( double,            ClassSizeMultiplier,        2.0                    ) \
    RTCONFIG_VARIABLE( double,            MinPrecursorAdjustment,        -2.5                ) \
    RTCONFIG_VARIABLE( double,            MaxPrecursorAdjustment,        2.5                    ) \
    RTCONFIG_VARIABLE( double,            PrecursorAdjustmentStep,    0.1                    ) \
    RTCONFIG_VARIABLE( bool,            NormalizeOnMode,            true                ) \
    RTCONFIG_VARIABLE( bool,            AdjustPrecursorMass,        false                ) \
    RTCONFIG_VARIABLE( int,                DeisotopingMode,            0                    ) \
    RTCONFIG_VARIABLE( bool,            MakeSpectrumGraphs,            false                ) \
    RTCONFIG_VARIABLE( int,                MzFidelityErrorBinsSize,    20                    ) \
    RTCONFIG_VARIABLE( int,                MzFidelityErrorBinsSamples,    10000                ) \
    RTCONFIG_VARIABLE( double,            MzFidelityErrorBinsLogMin,    -5.0                ) \
    RTCONFIG_VARIABLE( double,            MzFidelityErrorBinsLogMax,    1.0                    ) \
    RTCONFIG_VARIABLE( double,            PrecursorMzTolerance,        1.5                    ) \
    RTCONFIG_VARIABLE( double,            FragmentMzTolerance,        0.5                    ) \
    RTCONFIG_VARIABLE( double,            ComplementMzTolerance,        0.5                    ) \
    RTCONFIG_VARIABLE( double,            IsotopeMzTolerance,            0.25                ) \
    RTCONFIG_VARIABLE( string,            DynamicMods,                ""                    ) \
    RTCONFIG_VARIABLE( int,                MaxDynamicMods,                2                    ) \
    RTCONFIG_VARIABLE( string,            StaticMods,                    ""                    ) \
    RTCONFIG_VARIABLE( double,            IntensityScoreWeight,        1.0                    ) \
    RTCONFIG_VARIABLE( double,            MzFidelityScoreWeight,        1.0                    ) \
    RTCONFIG_VARIABLE( double,            ComplementScoreWeight,        1.0                    ) 
    //RTCONFIG_VARIABLE( double,            RandomScoreWeight,            0.0                    ) 

namespace freicore
{
namespace directag
{
    
    typedef map< double, double >                    MzFidelityErrorBins, MzFEBins;
    typedef map< double, double >                    ComplementErrorBins, CEBins;
    typedef vector< CEBins >                        CEBinsList;
    typedef vector< double >                        IntensityRanksumBins, IRBins;
    typedef vector< vector< IRBins > >                IntensityRanksumBinsByPeakCountAndTagLength, IRBinsTable;

    struct TaggingStatistics
    {
        TaggingStatistics() :
            numSpectraTagged(0), numResidueMassGaps(0), 
            numTagsGenerated(0), numTagsRetained(0) {}

        boost::atomic_uint32_t numSpectraTagged;
        boost::atomic_uint32_t numResidueMassGaps;
        boost::atomic_uint32_t numTagsGenerated;
        boost::atomic_uint32_t numTagsRetained;

        TaggingStatistics(const TaggingStatistics& other)
        {
            operator=(other);
        }

        TaggingStatistics& operator=(const TaggingStatistics& other)
        {
            numSpectraTagged.store(other.numSpectraTagged);
            numResidueMassGaps.store(other.numResidueMassGaps);
            numTagsGenerated.store(other.numTagsGenerated);
            numTagsRetained.store(other.numTagsRetained);
            return *this;
        }

        TaggingStatistics operator+ ( const TaggingStatistics& rhs )
        {
            TaggingStatistics tmp(*this);
            tmp.numSpectraTagged = numSpectraTagged + rhs.numSpectraTagged;
            tmp.numResidueMassGaps = numResidueMassGaps + rhs.numResidueMassGaps;
            tmp.numTagsGenerated = numTagsGenerated + rhs.numTagsGenerated;
            tmp.numTagsRetained = numTagsRetained + rhs.numTagsRetained;
            return tmp;
        }

        void reset()
        {
            numSpectraTagged = 0;
            numResidueMassGaps = 0;
            numTagsGenerated = 0;
            numTagsRetained = 0;
        }

        operator string() const
        {
            stringstream s;
            s << numSpectraTagged << " spectra; " << numResidueMassGaps << " tag graph edges; "
              << numTagsGenerated << " tags generated; " << numTagsRetained << " tags retained";
            return s.str();
        }

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & numSpectraTagged & numResidueMassGaps & numTagsGenerated & numTagsRetained;
        }
    };

    struct PeakFilteringStatistics
    {
        typedef accs::accumulator_set<int, accs::stats< accs::tag::sum, accs::tag::mean, accs::tag::max, accs::tag::min > > PeakCountAccumulator;
        
        PeakCountAccumulator before;
        PeakCountAccumulator after;

        PeakFilteringStatistics() {}
        
        void reset()
        {
            before = PeakCountAccumulator();
            after = PeakCountAccumulator();
        }

        operator string() const
        {
            stringstream s;
            s << "Mean original (filtered) peak count: " << accs::mean(before) << " (" << accs::mean(after) << ")" << endl;
            s << "Min/Max original (filtered) peak count: " << accs::min(before) << "/" << accs::max(before) << " (" << accs::min(after) << "/" << accs::max(after) << ")" << endl;
            float filter = 1.0f - ( (float) accs::sum(after) / (float) accs::sum(before) );
            s << "Filtered out " << filter * 100.0f << "% of peaks before tagging.";
            return s.str();
        }
    };

    struct DirecTagAPIConfig : public BaseRunTimeConfig
    {
    public:
        RTCONFIG_DEFINE_MEMBERS( DirecTagAPIConfig, DIRECTAG_API_CONFIG, "directag.cfg" )

        vector< float >            scoreThresholds;
        tagMetaIndex_t            tagMetaIndex;
        map< string, float >    compositionScoreMap;
        
        //boost::mt19937 rng;
        //boost::uniform_real<> RandomScoreRange;
        //boost::variate_generator< boost::mt19937&, boost::uniform_real<> > GetRandomScore;

        int        minIntensityClassCount;
        int        minMzFidelityClassCount;
        int        tagPeakCount;
        double    MzFidelityErrorBinsScaling;
        double    MzFidelityErrorBinsOffset;

        CEBinsList            complementErrorBinsList;
        IRBinsTable            intensityRanksumBinsTable;
        MzFEBins            mzFidelityErrorBins;

        void CalculateIRBins_R( IRBins& theseRanksumBins, int tagLength, int numPeaks, int curRanksum, int curRank, int loopDepth )
        {
            if( loopDepth > tagLength )
                ++ theseRanksumBins[ curRanksum ];
            else
                for( int rank = curRank + 1; rank <= numPeaks; ++rank )
                    CalculateIRBins_R( theseRanksumBins, tagLength, numPeaks, curRanksum + rank, rank, loopDepth+1 );
        }

        void CalculateIRBins( int tagLength, int numPeaks )
        {
            if( intensityRanksumBinsTable.size() <= (size_t) tagLength )
                intensityRanksumBinsTable.resize( tagLength+1, vector< IRBins >() );
            if( intensityRanksumBinsTable[ tagLength ].size() <= (size_t) numPeaks )
                intensityRanksumBinsTable[ tagLength ].resize( numPeaks+1, IRBins() );
            IRBins& theseRanksumBins = intensityRanksumBinsTable[ tagLength ][ numPeaks ];
            theseRanksumBins.resize( (tagLength+1) * numPeaks, 0 );
            CalculateIRBins_R( theseRanksumBins, tagLength, numPeaks, 0, 0, 0 );

            double totalRanksum = 0;
            for( IRBins::iterator itr = theseRanksumBins.begin(); itr != theseRanksumBins.end(); ++itr )
                totalRanksum += *itr;

            double tmpRanksum = 0;
            for( IRBins::iterator itr = theseRanksumBins.begin(); itr != theseRanksumBins.end(); ++itr )
            {
                tmpRanksum += *itr;
                *itr = tmpRanksum / totalRanksum;
            }
        }

        void PrecacheIRBins( )
        {
            intensityRanksumBinsTable.clear();
            try {
                cout << "Reading intensity ranksum bins cache file." << endl;
                ifstream cacheInputFile( "directag_intensity_ranksum_bins.cache" );
                if( cacheInputFile.is_open() )
                {
                    text_iarchive cacheInputArchive( cacheInputFile );
                    cacheInputArchive & intensityRanksumBinsTable;
                }
                cacheInputFile.close();
            } catch( ... ) {}

            cout << "Calculating uncached ranksum bins (this could take a while)." << endl;
            for( size_t peakCount = 4; peakCount <= MaxPeakCount; ++peakCount )
            {
                if( intensityRanksumBinsTable.size() <= (size_t) TagLength ||
                    intensityRanksumBinsTable[ TagLength ].size() <= peakCount ||
                    intensityRanksumBinsTable[ TagLength ][ peakCount ].empty() )
                {
                    CalculateIRBins( TagLength, peakCount );
                }
            }

            cout << "Writing intensity ranksum bins cache file." << endl;
            ofstream cacheOutputFile( "directag_intensity_ranksum_bins.cache" );
            text_oarchive cacheOutputArchive( cacheOutputFile );
            cacheOutputArchive & intensityRanksumBinsTable;
            cacheOutputFile.close();
        }

        void InitMzFEBins()
        {
            int numPeaks = tagPeakCount;
            vector< double > peakErrors( numPeaks );
            double peakErrorSum = 0.0;
            for( int i=0; i < numPeaks; ++i )
            {
                peakErrors[i] = FragmentMzTolerance * i;
                peakErrorSum += peakErrors[i];
            }

            double peakErrorAvg = peakErrorSum / numPeaks;
            for( int i=0; i < numPeaks; ++i )
                peakErrors[i] -= peakErrorAvg;
            //cout << peakErrors << endl;

            double maxError = 0.0;
            for( int i=0; i < numPeaks; ++i )
                maxError += pow( peakErrors[i], 2 );
            //cout << maxError << endl;

            mzFidelityErrorBins[ 0.0 ] = 0.0;

            peakErrors.clear();
            peakErrors.resize( numPeaks, 0.0 );
            vector< double > sumErrors( numPeaks, 0.0 );
            vector< double > adjustedSumErrors( numPeaks, 0.0 );

            // Random sampling permits longer tag lengths
            boost::mt19937 rng(0);
            boost::uniform_real<double> MzErrorRange( -FragmentMzTolerance, FragmentMzTolerance );
            boost::variate_generator< boost::mt19937&, boost::uniform_real<double> > RandomMzError( rng, MzErrorRange );
            for( int i=0; i < MzFidelityErrorBinsSamples; ++i )
            {
                for( int p=1; p < numPeaks; ++p )
                {
                    double e = RandomMzError();
                    peakErrors[p] = e;
                    sumErrors[p] = accumulate( peakErrors.begin(), peakErrors.begin()+p, e );
                }
                //cout << sumErrors << endl;
                //double sum = accumulate( peakErrors.begin(), peakErrors.end(), 0.0 );
                //double avg = sum / (int) peakErrors.size();
                double sum = accumulate( sumErrors.begin(), sumErrors.end(), 0.0 );
                double avg = sum / (int) sumErrors.size();

                sum = 0.0;
                for( size_t i=0; i < sumErrors.size(); ++i )
                {
                    adjustedSumErrors[i] = sumErrors[i] - avg;
                    sum += pow( adjustedSumErrors[i], 2 );
                }

                mzFidelityErrorBins[ sum ] = 0;
            }

            double n = 0.0;
            double totalSize = (double) mzFidelityErrorBins.size();
            for( MzFEBins::iterator itr = mzFidelityErrorBins.begin(); itr != mzFidelityErrorBins.end(); ++itr )
            {
                n += 1.0;
                itr->second = n / totalSize;
            }
            //cout << mzFidelityErrorBins << endl;
        }

        void InitCEBins()
        {
            boost::mt19937 rng(0);
            boost::uniform_real<double> ComplementErrorRange( -ComplementMzTolerance, ComplementMzTolerance );
            boost::variate_generator< boost::mt19937&, boost::uniform_real<double> > RandomComplementError( rng, ComplementErrorRange );
            complementErrorBinsList.resize( tagPeakCount+1, CEBins() );
            for( int numComplements = 2; numComplements <= tagPeakCount; ++numComplements )
            {
                CEBins& errorBins = complementErrorBinsList[numComplements];
                errorBins[0.0] = 0.0;
                for( int i=0; i < MzFidelityErrorBinsSamples; ++i )
                {
                    vector< double > errors;
                    for( int j=0; j < numComplements; ++j )
                        errors.push_back( RandomComplementError() );
                    double mean = arithmetic_mean<float>(errors);
                    for( int j=0; j < numComplements; ++j )
                        errors[j] = pow( errors[j] - mean, 2.0 );
                    double sse = accumulate( errors.begin(), errors.end(), 0.0 );
                    errorBins[sse] = 0;
                }
                double count = 0;
                for( map< double, double >::iterator itr = errorBins.begin(); itr != errorBins.end(); ++itr, ++count )
                    itr->second = count / (double) errorBins.size();
                //cout << errorBins << endl << endl;
            }
        }

        void PreComputeScoreDistributions()
        {
            InitMzFEBins();
            InitCEBins();
            PrecacheIRBins();
        }

    protected:
        void finalize()
        {
            if( TicCutoffPercentage > 1.0f )
            {
                TicCutoffPercentage /= 100.0f;
                cerr << "TicCutoffPercentage > 1.0 (100%) corrected, now at: " << TicCutoffPercentage << endl;
            }

            if( !DynamicMods.empty() )
            {
                DynamicMods = TrimWhitespace( DynamicMods );
                g_residueMap->setDynamicMods( DynamicMods );
            }

            if( !StaticMods.empty() )
            {
                StaticMods = TrimWhitespace( StaticMods );
                g_residueMap->setStaticMods( StaticMods );
            }

            tagPeakCount = TagLength + 1;

            double m = ClassSizeMultiplier;
            if( m > 1 )
            {
                minIntensityClassCount = int( ( pow( m, NumIntensityClasses ) - 1 ) / ( m - 1 ) );
                minMzFidelityClassCount = int( ( pow( m, NumMzFidelityClasses ) - 1 ) / ( m - 1 ) );
            } else
            {
                minIntensityClassCount = NumIntensityClasses;
                minMzFidelityClassCount = NumMzFidelityClasses;
            }

            BaseRunTimeConfig::finalize();
        }
    };
}
}

#endif
