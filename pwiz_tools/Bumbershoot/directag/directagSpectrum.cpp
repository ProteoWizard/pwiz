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
// The Original Code is the DirecTag peptide sequence tagger.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasari, Zeqiang Ma
//

#include "stdafx.h"
#include "directagSpectrum.h"
#include "directagConfig.h"

using namespace freicore;

namespace std
{
    ostream& operator<< ( ostream& o, const freicore::directag::PeakInfo& rhs )
    {
        return o << "( " << rhs.intensityRank << " )";
    }

    ostream& operator<< ( ostream& o, const freicore::directag::GapInfo& rhs )
    {
        return o << "(GapInfo: " << rhs.fromPeakItr->first << " " << rhs.peakItr->first << " " << rhs.gapMass << " " << rhs.gapRes << " " << rhs.error << " )";
    }

    ostream& operator<< ( ostream& o, const freicore::directag::gapVector_t& rhs )
    {
        o << "(GapVector:";
        for( freicore::directag::gapVector_t::const_iterator itr = rhs.begin(); itr != rhs.end(); ++itr )
             o << " " << *itr;
        o << " )";

        return o;
    }

    ostream& operator<< ( ostream& o, const freicore::directag::gapMap_t& rhs )
    {
        o << "(GapMap:";
        for( freicore::directag::gapMap_t::const_iterator itr = rhs.begin(); itr != rhs.end(); ++itr )
            o << " " << itr->first << "->" << itr->second << "\n";
        o << " )";

        return o;
    }
}

namespace freicore
{
namespace directag
{

    Spectrum::Spectrum()
        :    BaseSpectrum(), PeakSpectrum< PeakInfo >(), TaggingSpectrum()
    {
    }

    Spectrum::Spectrum( const Spectrum& old )
        :    BaseSpectrum( old ), PeakSpectrum< PeakInfo >( old ), TaggingSpectrum( old )
    {
    }

    Spectrum::Spectrum(const string& nativeID, size_t charge, double precursorMZ, const flat_map<double, float>& peaks)
    {
        id.setId(nativeID);
        id.charge = charge;
        mzOfPrecursor = precursorMZ;
        mOfPrecursor = mzOfPrecursor * id.charge - ( id.charge * PROTON );
        peakPreData.clear();
        typedef pair<double, float> Peak;
        BOOST_FOREACH(const Peak& p, peaks)
            peakPreData.insert(p);
    }

    void Spectrum::setTagConfig(shared_ptr<DirecTagAPIConfig> config)
    {
        tagConfig = config;
        scoreWeights[ "intensity" ] = intensityScoreWeight = tagConfig->IntensityScoreWeight;
        scoreWeights[ "mzFidelity" ] = mzFidelityScoreWeight = tagConfig->MzFidelityScoreWeight;
        scoreWeights[ "complement" ] = complementScoreWeight = tagConfig->ComplementScoreWeight;
        //scoreWeights[ "random" ] = tagConfig->RandomScoreWeight;
        tagList.max_size( tagConfig->MaxTagCount );
    }

    void Spectrum::processAndTagSpectrum(bool clearPeakData = false)
    {
        // Check the tagging configuration before proceeding.
        if(tagConfig.use_count() == 0)
            throw runtime_error("Tagging configuration was not set.");
        try 
        {
            // Filter the peaks according to users config.
            FilterPeaks();
            Preprocess();
            // Ignore sparse spectra for tagging.
            if( (int) peakPreData.size() < tagConfig->minIntensityClassCount )
                return;
            // Generate tag graph, score the tags, and collect them.
            MakeTagGraph();
            MakeProbabilityTables();
            deallocate(nodeSet);
            Score();
            // Free memory
            deallocate(gapMaps);
            deallocate(tagGraphs);
            if( !tagConfig->MakeSpectrumGraphs || clearPeakData )
            {
                deallocate(peakPreData);
                deallocate(peakData);
            }
        } catch( std::exception& e )
        {
            cerr << " terminated with an error: " << e.what() << endl;
        } catch(...)
        {
            cerr << " terminated with an unknown error." << endl;
        }
    }

    void Spectrum::processAndTagSpectrum(PeakFilteringStatistics& peakStats, TaggingStatistics& stats, bool clearPeakData = false)
    {
        // Check the tagging configuration before proceeding.
        if(tagConfig.use_count() == 0)
            throw runtime_error("Tagging configuration was not set.");
        try
        {
            // Filter the peaks according to users config.
            // Update the peak counts for user stats.
            peakStats.before(peakPreCount);
            FilterPeaks();
            Preprocess();
            peakStats.after(peakCount);
            // Ignore sparse spectra for tagging.
            if( (int) peakPreData.size() < tagConfig->minIntensityClassCount )
                return;
            // Generate tag graph, score the tags, and collect them.
            // Update the tagging statistics variable.
            ++stats.numSpectraTagged;
            stats.numResidueMassGaps += MakeTagGraph();
            MakeProbabilityTables();
            deallocate(nodeSet);
            stats.numTagsGenerated += Score();
            stats.numTagsRetained += tagList.size();
            // Free memory
            deallocate(gapMaps);
            deallocate(tagGraphs);
            if( !tagConfig->MakeSpectrumGraphs || clearPeakData)
            {
                deallocate(peakPreData);
                deallocate(peakData);
            }
        } catch( std::exception& e )
        {
            cerr << " terminated with an error: " << e.what() << endl;
        } catch(...)
        {
            cerr << " terminated with an unknown error." << endl;
        }
    }

    void Spectrum::ClassifyPeakIntensities()
    {
        // Sort peaks by intensity.
        // Use multimap because multiple peaks can have the same intensity.
        typedef multimap< double, double > IntenSortedPeakPreData;
        IntenSortedPeakPreData intenSortedPeakPreData;
        for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
        {
            IntenSortedPeakPreData::iterator iItr = intenSortedPeakPreData.insert( make_pair( itr->second, itr->second ) );
            iItr->second = itr->first;
        }

        // Restore the sorting order to be based on MZ
        IntenSortedPeakPreData::reverse_iterator r_iItr = intenSortedPeakPreData.rbegin();
        peakPreData.clear();
        peakData.clear();

        for( int i=0; i < tagConfig->NumIntensityClasses; ++i )
        {
            int numFragments = (int) round( (double) ( pow( (double) tagConfig->ClassSizeMultiplier, i ) * intenSortedPeakPreData.size() ) / (double) tagConfig->minIntensityClassCount, 0 );
            for( int j=0; r_iItr != intenSortedPeakPreData.rend() && j < numFragments; ++j, ++r_iItr )
            {
                double mz = r_iItr->second;
                float inten = r_iItr->first;
                peakPreData.insert( peakPreData.end(), make_pair( mz, inten ) );
                peakData[ mz ].intensityRank = peakPreData.size();
                peakData[ mz ].intensity = inten;
            }
        }
        intenSortedPeakPreData.clear();
    }

    // Attempts to find a complement for each peak in the spectrum
    // Returns the sum of products of the found complements' intensities
    double Spectrum::FindComplements( double complementMzTolerance )
    {
        double sumOfProducts = 0;
        complementaryTIC = 0;
        for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
        {
            PeakInfo& peak = peakData[ itr->first ];// = PeakInfo();

            for( int z=0; z < numFragmentChargeStates; ++z )
            {
                double complementMz = CalculateComplementMz( itr->first, z+1 );
                PeakPreData::iterator complementItr = peakPreData.findNear( complementMz, complementMzTolerance, true );
                if( complementItr != peakPreData.end() )
                {
                    sumOfProducts += itr->second * complementItr->second;
                    complementaryTIC += itr->second;
                    peak.hasComplementAsCharge[z] = true;
                } else
                    peak.hasComplementAsCharge[z] = false;
            }
        }

        return sumOfProducts;
    }

    size_t Spectrum::MakeTagGraph()
    {
        PeakData::iterator left;    // main iterator pointing to the first peak in a comparison
        PeakData::iterator cur;        // main iterator pointing to the peak currently being looked at
        m2n_t::const_iterator resItr;
        size_t numResidueMassGaps = 0;

        gapMaps.clear();
        tagGraphs.clear();
        nodeSet.clear();

        gapMaps.resize( numFragmentChargeStates );
        tagGraphs.resize( numFragmentChargeStates );

        for( int z=0; z < numFragmentChargeStates; ++z )
        {
            gapMap_t& gapMap = gapMaps[z];
            spectrumGraph& tagGraph = tagGraphs[z];

            for( left = peakData.begin(); left != peakData.end(); ++left )
            {
                for( resItr = g_residueMap->beginMonoMasses(); resItr != g_residueMap->endMonoMasses(); ++resItr )
                {
                    if( resItr->second == PEPTIDE_N_TERMINUS_SYMBOL || resItr->second == PEPTIDE_C_TERMINUS_SYMBOL )
                        continue;

                    double mzGap = resItr->first / (float) (z+1);
                    double expectedMZ = left->first + mzGap;
                    cur = peakData.findNear( expectedMZ, rtConfig->FragmentMzTolerance );

                    if( cur != peakData.end() )
                    {
                        // Calculate the error between the m/z of the actual peak and the m/z that was expected for it
                        double error = (cur->first - left->first) - mzGap;
                        if( fabs( error ) > tagConfig->FragmentMzTolerance )
                            continue;

                        gapMap_t::iterator nextGapInfo = gapMap.insert( gapMap_t::value_type( cur->first, gapVector_t() ) ).first;
                        gapMap[ left->first ].push_back( GapInfo( left, cur, nextGapInfo, mzGap, resItr->second, error ) );
                        ++ numResidueMassGaps;

                        GapInfo newEdge( left, cur, nextGapInfo, mzGap, resItr->second, error, left->first, cur->first );
                        tagGraph[ left->first ].cEdges.push_back( newEdge );
                        tagGraph[ cur->first ].nEdges.push_back( newEdge );
                        tagGraph[ cur->first ].nPathSize = max(    tagGraph[ cur->first ].nPathSize,
                                                                tagGraph[ left->first ].nPathSize + 1 );
                        nodeSet.insert( left->first );
                        nodeSet.insert( cur->first );
                    }
                }
            }

            for( spectrumGraph::reverse_iterator itr = tagGraph.rbegin(); itr != tagGraph.rend(); ++itr )
            {
                for( size_t i=0; i < itr->second.nEdges.size(); ++i )
                {
                    tagGraph[ itr->second.nEdges[i].nTermMz ].cPathSize = max(    tagGraph[ itr->second.nEdges[i].nTermMz ].cPathSize,
                                                                                itr->second.cPathSize + 1 );
                }

                itr->second.longestPath = itr->second.cPathSize + itr->second.nPathSize;
            }
        }

        tagGraphPeakCount = nodeSet.size();
        tagGraphTIC = 0;
        for( nodeSet_t::iterator itr = nodeSet.begin(); itr != nodeSet.end(); ++itr )
            tagGraphTIC += peakPreData[*itr];

        return numResidueMassGaps;
    }

    void Spectrum::FilterPeaks()
    {
        // Secondly, determine the neutral mass of the precursor (m/z * z - z)
        mOfPrecursor = mzOfPrecursor * id.charge - ( id.charge * PROTON );

        numFragmentChargeStates = max( 1, id.charge - 1 );

        if( peakPreData.empty() )
            return;

        // Eliminate peaks above the precursor's mass with a given tolerance
        double maxPeakMass = mOfPrecursor + PROTON + tagConfig->PrecursorMzTolerance;
        PeakPreData::iterator itr = peakPreData.upper_bound( maxPeakMass );
        peakPreData.erase( itr, peakPreData.end() );

        if( peakPreData.empty() )
            return;

        // Thirdly, store the bounds of the spectrum before eliminating any peaks
        mzLowerBound = peakPreData.begin()->first;
        mzUpperBound = peakPreData.rbegin()->first;
        totalPeakSpace = mzUpperBound - mzLowerBound;

        //if( tagConfig->MakeSpectrumGraphs )
        //    writeToSvgFile( "-unprocessed" + tagConfig->OutputSuffix );

        if( tagConfig->DeisotopingMode > 0 || tagConfig->AdjustPrecursorMass )
        {
            Deisotope( tagConfig->IsotopeMzTolerance );

            //if( tagConfig->MakeSpectrumGraphs )
            //    writeToSvgFile( "-deisotoped" + tagConfig->OutputSuffix );
        }

        FilterByTIC( tagConfig->TicCutoffPercentage );
        FilterByPeakCount( tagConfig->MaxPeakCount );

        //if( tagConfig->MakeSpectrumGraphs )
        //    writeToSvgFile( "-filtered" + tagConfig->OutputSuffix );

        if( peakPreData.size() < (size_t) tagConfig->minIntensityClassCount )
        {
            peakPreData.clear();
            return;
        }

        // Create peak data from pre peak data
        /*ClassifyPeakIntensities();

        MakeTagGraph();
        //map< int, int > pathSizeHistogram;
        int maxLongestPath = 0;
        map< float, int > longestPathMap;

        for( int z=0; z < numFragmentChargeStates; ++z )
        {
            spectrumGraph& tagGraph = tagGraphs[z];
            for( spectrumGraph::reverse_iterator itr = tagGraph.rbegin(); itr != tagGraph.rend(); ++itr )
            {
                if( longestPathMap[ itr->first ] < itr->second.longestPath )
                    longestPathMap[ itr->first ] = itr->second.longestPath;

                if( itr->second.longestPath > maxLongestPath )
                    maxLongestPath = itr->second.longestPath;

                //++ pathSizeHistogram[ itr->second.longestPath ];
            }
            deallocate(tagGraph);
        }
        //cout << id << " peak path histogram:\n" << pathSizeHistogram << endl;

        vector< PeakData::iterator > junkPeaks;
        for( PeakData::iterator cur = peakData.begin(); cur != peakData.end(); ++cur )
        {
            if( longestPathMap[ cur->first ] < tagConfig->tagPeakCount )
                junkPeaks.push_back( cur );
        }

        for( size_t i=0; i < junkPeaks.size(); ++i )
        {
            peakData.erase( junkPeaks[i] );
            peakPreData.erase( junkPeaks[i]->first );
        }

        if( peakData.size() < (size_t) tagConfig->minIntensityClassCount )
        {
            deallocate(peakPreData);
            deallocate(peakData);
            return;
        }*/

        // Create peak data from pre peak data
        ClassifyPeakIntensities();

        for( PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr ) 
        {
            itr->second.hasComplementAsCharge.resize(numFragmentChargeStates, false);
        }

        totalPeakSpace = peakData.rbegin()->first - peakData.begin()->first;
        peakCount = (int) peakData.size();
    }

    void Spectrum::Preprocess()
    {
        PeakPreData::iterator itr;
        PeakPreData::reverse_iterator r_itr;
        PeakPreData::iterator findItr;

        if( mzOfPrecursor < 1 )
        {
            peakPreData.clear();
            return;
        }

        if( tagConfig->AdjustPrecursorMass )
        {
            double originalPrecursorMass = mOfPrecursor;
            double originalPrecursorMz = mzOfPrecursor;
            double bestPrecursorAdjustment = 0.0;
            double maxSumOfProducts = 0.0;
            map<double, double> AdjustmentResults;

            for( mOfPrecursor += tagConfig->MinPrecursorAdjustment;
                 mOfPrecursor <= originalPrecursorMass + tagConfig->MaxPrecursorAdjustment;
                 mOfPrecursor += tagConfig->PrecursorAdjustmentStep )
            {
                mzOfPrecursor = ( mOfPrecursor + ( id.charge * PROTON ) ) / id.charge;

                double sumOfProducts = FindComplements( tagConfig->ComplementMzTolerance );

                if( sumOfProducts > maxSumOfProducts )
                {
                    maxSumOfProducts = sumOfProducts;
                    bestPrecursorAdjustment = mOfPrecursor - originalPrecursorMass;
                }

                AdjustmentResults[ mOfPrecursor ] = sumOfProducts;
            }

            if( maxSumOfProducts > 0.0 )
            {
                mOfPrecursor = originalPrecursorMass + bestPrecursorAdjustment;
                mzOfPrecursor = ( mOfPrecursor + ( id.charge * PROTON ) ) / id.charge;
            } else
            {
                mOfPrecursor = originalPrecursorMass;
                mzOfPrecursor = originalPrecursorMz;
            }

            //if( tagConfig->MakeSpectrumGraphs )
            //{
            //    writeToSvgFile( "-adjusted" + tagConfig->OutputSuffix );
            //    cout << "Original precursor m/z: " << originalPrecursorMz << endl;
            //    cout << "Corrected precursor m/z: " << mzOfPrecursor << endl;
            //    cout << "Sum of complement products: " << maxSumOfProducts << endl;

                /*cout << "Best complement total: " << BestComplementTotal << endl;
                cout << oldPrecursor << " (" << spectrum->mOfPrecursorFixed << ") corrected by " << spectrum->mzOfPrecursor - oldPrecursor <<
                        " to " << spectrum->mzOfPrecursor << " (" << spectrum->mOfPrecursor << ") " << endl;*/

            //    cout << AdjustmentResults << endl;
            //}
        }

        // Initialize the spectrum info tables
        initialize( tagConfig->NumIntensityClasses, tagConfig->NumMzFidelityClasses );

        if( peakData.size() < (size_t) tagConfig->minIntensityClassCount )
        {
            peakPreData.clear();
            peakData.clear();
            return;
        }

        // Reclassify intensities and find complements based on fully processed spectrum
        ClassifyPeakIntensities();
                
        for( PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr )
        {
            itr->second.hasComplementAsCharge.resize(numFragmentChargeStates, false);
        }

        FindComplements( tagConfig->ComplementMzTolerance );

        totalPeakSpace = peakData.rbegin()->first - peakData.begin()->first;
        peakCount = (int) peakData.size();
    }

    void Spectrum::MakeProbabilityTables()
    {
        for( PeakData::iterator itr = peakData.begin(); itr != peakData.end(); ++itr )
        {
            itr->second.hasSomeComplement = accumulate( itr->second.hasComplementAsCharge.begin(),
                                                        itr->second.hasComplementAsCharge.begin() + numFragmentChargeStates,
                                                        0 );
            ++ complementClassCounts[ ( itr->second.hasSomeComplement == 0 ? 1 : 0 ) ];
        }

        if( complementClassCounts[0] == 0 )
        {
            //scoreWeights["complement"] = 0;
            complementScoreWeight = 0;
        } else
            CreateScoringTableMVH( 0, tagConfig->tagPeakCount, 2, complementClassCounts, bgComplements, g_lnFactorialTable, false, false, false );
    }

    size_t Spectrum::Score()
    {
        START_PROFILER(3)
        size_t numTagsGenerated = findTags();
        STOP_PROFILER(3)

        // compute approximate tagMzRange for ScanRanker
        float tagMzRangeLowerBound = peakData.rbegin()->first;
        float tagMzRangeUpperBound = peakData.begin()->first;

        for( TagList::iterator itr = interimTagList.begin(); itr != interimTagList.end(); ++itr )
        {
            TagInfo& tag = const_cast< TagInfo& >( *itr );
            //tag.CalculateTotal( scoreWeights );
            START_PROFILER(4)
            tag.CalculateTotal( complementScoreWeight, intensityScoreWeight, mzFidelityScoreWeight );
            STOP_PROFILER(4)
            tag.totalScore *= numTagsGenerated;
            //for( map< string, double >::iterator scoreItr = tag.scores.begin(); scoreItr != tag.scores.end(); ++scoreItr )
            //    scoreItr->second *= numTagsGenerated;

            tagMzRangeLowerBound = min( tagMzRangeLowerBound, tag.lowPeakMz);
            tagMzRangeUpperBound = max( tagMzRangeUpperBound, tag.highPeakMz);

            START_PROFILER(5)
            if( tagConfig->MaxTagScore == 0 || tag.totalScore <= tagConfig->MaxTagScore )
                tagList.insert( tag );
            STOP_PROFILER(5)
        }

        tagMzRange = ((tagMzRangeUpperBound - tagMzRangeLowerBound) < 0) ? 0 : (tagMzRangeUpperBound - tagMzRangeLowerBound);

        // Code for ScanRanker
        bestTagScore = (tagList.empty()) ? 0 : tagList.rbegin()->chisquared;
        bestTagTIC = (tagList.empty()) ? 0 : tagList.rbegin()->tagTIC;

        START_PROFILER(6)
        deallocate( interimTagList );

        deallocate( bgComplements );
        STOP_PROFILER(6)

        return numTagsGenerated;
    }

    // Takes a tag and recursively fills a list of strings of variations of that tag based on I/L substitutions
    void TagExploder_R( const string& tag, int idx, vector< string >& tagList )
    {
        if( idx == (int) tag.length() )
        {
            tagList.push_back( tag );
            return;
        }

        if( tag[idx] == 'I' )
        {
            string newTag( tag );
            newTag[idx] = 'L';
            TagExploder_R( newTag, idx+1, tagList );
        }

        TagExploder_R( tag, idx+1, tagList );
    }

    void TagExploder( const string& tag, vector< string >& tagList )
    {
        tagList.push_back( tag );
        TagExploder_R( tag, 0, tagList );
    }

    void Spectrum::findTags_R(    gapMap_t::iterator gapInfoItr,
                                int tagIndex,
                                string& tag,
                                vector< double >& peakErrors,
                                vector< PeakData::iterator >& peakList,
                                int peakChargeState,
                                size_t& numTagsGenerated,
                                IRBins& irBins )
    {
        if( tagIndex == 0 )
        {
            ++ numTagsGenerated;

            TagInfo newTag;

            MvIntKey intensityClassKey, complementClassKey, mzFidelityKey;
            intensityClassKey.resize( tagConfig->NumIntensityClasses, 0 );
            complementClassKey.resize( 2, 0 );

            vector<double> modelPeaks;
            vector<double> modelErrors;
            vector<double> modelSquaredErrors;

            double gapMass = 0.0;
            modelPeaks.push_back( peakList[0]->first );
            for( int i=0; i < tagConfig->TagLength; ++i )
            {
                gapMass += g_residueMap->getMonoMassByName( tag[i] ) / (double) peakChargeState;
                modelPeaks.push_back( peakList[i+1]->first - gapMass );
            }
            double sum = accumulate( modelPeaks.begin(), modelPeaks.end(), 0.0 );
            double avg = sum / tagConfig->tagPeakCount;

            sum = 0.0;
            for( int i=0; i < tagConfig->tagPeakCount; ++i )
            {
                modelErrors.push_back( fabs( modelPeaks[i] - avg ) );
                sum += pow( modelErrors[i], 2 );
                //cout << e1 << " " << e2 << " " << e3 << ": " << errors << endl;
            }

            MzFEBins::iterator binItr = tagConfig->mzFidelityErrorBins.upper_bound( sum );
            -- binItr;
            //newTag.scores[ "mzFidelity" ] = binItr->second;
            newTag.mzFidelityScore = binItr->second;

            gapMass = 0.0;
            modelPeaks[0] = avg;
            for( int i=0; i < tagConfig->TagLength; ++i )
            {
                gapMass += g_residueMap->getMonoMassByName( tag[i] ) / (double) peakChargeState;
                modelPeaks[i+1] = avg + gapMass;
            }
            //cout << peakList << " " << modelPeaks << " " << modelErrors << endl;

            //int totalPathLength = 0;
            int totalIntensityRanks = 1;
            double totalIntensity = 0;
            //int totalContextRanks = 1;
            vector< double > complementPairMasses;
            //spectrumGraph& tagGraph = tagGraphs[peakChargeState-1];
            for( int i=0; i < tagConfig->tagPeakCount; ++i )
            {
                PeakInfo& peak = peakList[i]->second;

                newTag.worstPeakRank = max( peak.intensityRank, newTag.worstPeakRank );
                totalIntensityRanks += peak.intensityRank;
                totalIntensity += peak.intensity;

                bool hasComplement = peak.hasComplementAsCharge[ peakChargeState-1 ];
                ++ complementClassKey[ hasComplement ? 0 : 1 ];
                if( hasComplement )
                {
                    double complementMz = CalculateComplementMz( peakList[i]->first, peakChargeState );
                    PeakData::iterator complementItr = peakData.findNear( complementMz, tagConfig->ComplementMzTolerance );
                    complementPairMasses.push_back( peakList[i]->first + complementItr->first );
                }
            }

            //newTag.scores[ "intensity" ] = irBins[ totalIntensityRanks ];
            newTag.intensityScore = irBins[ totalIntensityRanks ];
            newTag.ranksum = totalIntensityRanks;
            newTag.tagTIC = (float) totalIntensity;

            double complementClassScore = 0;
            if( complementClassCounts[0] > 0 )
            {
                CEBins::iterator binItr = tagConfig->complementErrorBinsList[2].begin();
                if( complementClassKey[0] > 1 )
                {
                    double complementPairMean = arithmetic_mean<double>( complementPairMasses );
                    for( size_t i=0; i < complementPairMasses.size(); ++i )
                        complementPairMasses[i] = pow( complementPairMasses[i] - complementPairMean, 2.0 );
                    double sse = accumulate( complementPairMasses.begin(), complementPairMasses.end(), 0.0 );

                    binItr = tagConfig->complementErrorBinsList[complementClassKey[0]].upper_bound( sse );
                    -- binItr;
                    while(binItr->second == 0)
                        ++ binItr;
                }
                MvhTable::reverse_iterator itr;
                int i = tagConfig->tagPeakCount;
                for( itr = bgComplements.rbegin(); itr != bgComplements.rend() && i >= (int) complementPairMasses.size(); ++itr, --i )
                    complementClassScore += (double) exp(itr->second);
                --itr;
                if( i >= 1 )
                    complementClassScore -= (double) exp(itr->second) * ( 1.0 - binItr->second );
                else
                    complementClassScore -= (double) exp(itr->second) / 2.0;
                //newTag.scores[ "complement" ] = complementClassScore;
                newTag.complementScore = complementClassScore;
                //cout << id.index << ": " << complementClassKey << " " << complementClassCounts << " " << complementClassScore << " " << complementPairMasses << " " << binItr->second << " " << i << " " << itr->second << endl;
            } else
                //newTag.scores[ "complement" ] = 1.0;
                newTag.complementScore = 1.0;

            //newNode.peakList = peakList;

            //if( tagConfig->RandomScoreWeight != 0 )
            //    newTag.scores[ "random" ] = (double) tagConfig->GetRandomScore();

            newTag.lowPeakMz = peakList.front()->first;
            newTag.highPeakMz = peakList.back()->first;

            //----------------------------------------- lower y - water+proton 
            //newNode->cTerminusMass = max( 0.0, *peakList.begin() - WATER + PROTON );
            newTag.cTerminusMass = modelPeaks.front() * peakChargeState - ( PROTON * peakChargeState );
            newTag.cTerminusMass = max( 0.0, newTag.cTerminusMass - WATER_MONO );

            //---------------------------- neutral precursor - proton ----- upper y
            //newNode->nTerminusMass = max( 0.0, mOfPrecursor + 1 - *peakList.rbegin() );
            newTag.nTerminusMass = modelPeaks.back() * peakChargeState - ( PROTON * peakChargeState );
            newTag.nTerminusMass = max( 0.0, mOfPrecursor - newTag.nTerminusMass );

            string properTag = tag;
            std::reverse( properTag.begin(), properTag.end() );

            newTag.tag = properTag;
            newTag.totalScore = (double) tagCount;
            newTag.chargeState = peakChargeState;

            ++ tagCount;
            interimTagList.insert( newTag );
        } else
        {
            if( gapInfoItr == gapMaps[peakChargeState-1].end() )
                return;

            gapVector_t& gapVector = gapInfoItr->second;

            if( gapVector.empty() )
                return;

            peakList.push_back( gapVector.front().fromPeakItr );

            size_t gapCount = gapVector.size();
            for( size_t i=0; i < gapCount; ++i )
            {
                if( tagIndex-1 == 0 )
                    peakList.push_back( gapVector[i].peakItr );

                tag.push_back( gapVector[i].gapRes );
                peakErrors.push_back( gapVector[i].error );
                findTags_R( gapVector[i].nextGapInfo,
                            tagIndex-1,
                            tag,
                            peakErrors,
                            peakList,
                            peakChargeState,
                            numTagsGenerated,
                            irBins );
                peakErrors.pop_back();
                tag.erase( tag.length()-1 );

                if( tagIndex-1 == 0 )
                    peakList.pop_back();
            }

            peakList.pop_back();
        }
    }

    size_t Spectrum::findTags()
    {
        size_t numTagsGenerated = 0;
        gapMap_t::iterator gapInfoItr;
        string tag;
        vector< double > peakErrors;
        vector< PeakData::iterator > peakList;
        IRBins& irBins = tagConfig->intensityRanksumBinsTable[ tagConfig->TagLength ][ peakData.size() ];
//cout << peakData.size() << irBins << endl;
        peakErrors.reserve( tagConfig->tagPeakCount );
        peakList.reserve( tagConfig->tagPeakCount );

        tagCount = 0;

        for( int z=0; z < numFragmentChargeStates; ++z )
        {
            gapMap_t& gapMap = gapMaps[z];
            for( gapInfoItr = gapMap.begin(); gapInfoItr != gapMap.end(); ++gapInfoItr )
                findTags_R( gapInfoItr, tagConfig->TagLength, tag, peakErrors, peakList, z+1, numTagsGenerated, irBins );
        }

        return numTagsGenerated;
    }
}
}
