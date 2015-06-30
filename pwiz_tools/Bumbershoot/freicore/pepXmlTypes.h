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

#ifndef _PEPXMLTYPES_H
#define _PEPXMLTYPES_H

#include "shared_defs.h"
#include "shared_funcs.h"
#include "searchResult.h"
//#include <limits>
//#include "expat_xml.h"

/* Inline defs to get attribute names using expat libraries. */
//#define HAS_ATTR(name) (paramIndex(name,atts,attsCount) > -1)
//#define GET_ATTR_AS(name, type) getAttributeAs<type>(name,atts,attsCount)
//#define GET_ATTR(name) GET_ATTR_AS(name,std::string)

namespace freicore
{
    /*template< class T >
    T getAttributeAs( const string& name, const char** atts, int attsCount )
    {
        if( !HAS_ATTR(name) )
            throw out_of_range( "required attribute \"" + name + "\" not found" );
        return lexical_cast<T>( atts[paramIndex(name, atts, attsCount)+1] );
    }*/

    struct GenericSearchResult : public BaseSearchResult
    {
        GenericSearchResult( const DigestedPeptide& c )
            :   BaseSearchResult( c )
        {}

        GenericSearchResult( const double s = 0.0f, const MvIntKey& k = MvIntKey(), const string& seq = "" )
            :    BaseSearchResult( k, seq )
        {}

        SearchScoreList scoreList;
        double fdr;

        double getTotalScore() const
        {
            return scoreList[0].second;
        }

        SearchScoreList getScoreList() const
        {
            return scoreList;
        }

        bool operator< ( const GenericSearchResult& rhs ) const
        {
            if( getTotalScore() == rhs.getTotalScore() )
                if( mod == rhs.mod )
                    return sequence() < rhs.sequence();
                else
                    return mod > rhs.mod;
            else
                return getTotalScore() < rhs.getTotalScore();
        }

        bool operator== ( const GenericSearchResult& rhs ) const
        {
            return ( getTotalScore() == rhs.getTotalScore() && sequence() == rhs.sequence() );
        }

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & boost::serialization::base_object< BaseSearchResult >( *this );
            ar & scoreList & fdr;
        }
    };

    //typedef GenericSearchResult SearchResult;

    static const double MOD_MASS_EPSILON = 0.01f;

    /*struct EnzymeSpecificityInfo
    {
        char sense;
        int min_spacing;
        string cut;
        string no_cut;
    };

    struct EnzymeInfo
    {
        EnzymeInfo( const string& CleavageRules, int NumMinTerminiCleavages )
        {
            stringstream ruleStream(CleavageRules);
            CleavageRuleSet rules;
            ruleStream >> rules;

            name = "unknown";
            desc = "";
            switch( NumMinTerminiCleavages )
            {
                case 0:
                    fidelity = "nonspecific";
                    break;
                case 1:
                    fidelity = "semispecific";
                    break;
                default:
                case 2:
                    fidelity = "specific";
                    break;
            }
            independent = true;
        }

        string name;
        string desc;
        string fidelity;
        bool independent;
        vector< EnzymeSpecificityInfo > specifityList;
    };*/

    struct ScoreInfo
    {
        ScoreInfo( bool higherIsBetter = true, const string& range = "[0,infinity)" ) : higherIsBetter( higherIsBetter )
        {
            string varRange = TrimWhitespace(range);
            minValueInclusive = ( *varRange.begin() == '[' );
            maxValueInclusive = ( *varRange.rbegin() == ']' );
            varRange = varRange.substr( 1, varRange.length()-2 );

            vector<string> values;
            split( values, varRange, boost::is_any_of(",") );

            if( TrimWhitespace( values[0] ) == "-infinity" )
                minValue = -std::numeric_limits<double>::infinity();
            else
                minValue = lexical_cast<double>( values[0] );

            if( TrimWhitespace( values[1] ) == "infinity" )
                maxValue = std::numeric_limits<double>::infinity();
            else
                maxValue = lexical_cast<double>( values[1] );
        }

        int testScore( double score )
        {
            if( ( minValueInclusive && score < minValue ) || ( !minValueInclusive && score <= minValue ) )
                return -1;
            if( ( maxValueInclusive && score > maxValue ) || ( !maxValueInclusive && score >= maxValue ) )
                return 1;
            return 0;
        }

        bool    higherIsBetter;
        double    minValue;
        bool    minValueInclusive;
        double    maxValue;
        bool    maxValueInclusive;
    };

    template< class SpectrumType, class SpectraListType >
    class PepXmlReader
    {
    public:

        typedef typename SpectraListType::iterator                SpectraListIterator;
        typedef PepXmlReader< SpectrumType, SpectraListType >    ReaderType;

        PepXmlReader( SpectraListType* pSpectra, const string& filename = "" )
            : m_pSpectra( pSpectra ), m_pParser( NULL ), m_maxResultRank(0),
            m_maxChargeState(0)
        {
            /*m_absoluteScores["xcorr"]        = ScoreInfo( true, "[0,infinity)" );
            m_absoluteScores["mvh"]            = ScoreInfo( true, "[0,infinity)" );
            m_absoluteScores["hyperscore"]    = ScoreInfo( true, "[0,infinity)" );
            m_relativeScores["deltacn"]        = ScoreInfo( true, "[0,1]" );
            m_relativeScores["expect"]        = ScoreInfo( false, "(0,infinity)" );*/

            if( !filename.empty() )
                open( filename );
        }

        ~PepXmlReader()
        {
            close();
        }

        void open( const string& filename )
        {
            close();

            m_vars["NumChargeStates"] = "3";
            m_vars["StaticMods"] = "";
            m_vars["DynamicMods"] = "";
            m_vars["ProteinDatabase"] = "";
            m_vars["CleavageRules"] = "K|R|[ . . ]";
            m_vars["UseAvgMassOfSequences"] = "1";
            m_vars["NumMaxMissedCleavages"] = "10";
            m_vars["NumMinTerminiCleavages"] = "2";

            m_curResultRank = 0;

            m_InputFileName = filename;
            m_InputFile.open( m_InputFileName.c_str(), std::ios::binary );
            if( !m_InputFile.is_open() )
                throw invalid_argument( string( "unable to open pepXML file \"" ) + filename + "\"" );

            m_InputFile.clear();
            m_pParser = XML_ParserCreate( NULL );
            XML_SetUserData( m_pParser, this );

            XML_SetElementHandler( m_pParser, StartElement, EndElement );
        }

        void close()
        {
            m_InputFile.close();

            if( m_pParser )
            {
                XML_ParserFree( m_pParser );
                m_pParser = NULL;
            }
        }

        int ReadSpectra( int nCount, int maxTotalPeakCount )
        {
                unsigned int done, bytesRead;
                char* buf = new char[READ_BUFFER_SIZE];

                do
                {
                    m_InputFile.read( buf, READ_BUFFER_SIZE );
                    bytesRead = m_InputFile.gcount();
                    done = bytesRead < sizeof(buf);

                    try
                    {
                        if( !XML_Parse( m_pParser, buf, bytesRead, done ) )
                        {
                            if( m_maxResultRank == 0 && XML_GetErrorCode( m_pParser ) == XML_ERROR_ABORTED )
                                break;
                            throw runtime_error( XML_ErrorString( XML_GetErrorCode( m_pParser ) ) );
                        }
                    } catch( std::exception& e )
                    {
                        throw runtime_error( string( e.what() ) + " at line " + lexical_cast<string>( XML_GetCurrentLineNumber( m_pParser ) ) );
                    }

                } while( !done );

                delete buf;

                return m_nCount;
        }

        RunTimeVariableMap ReadSpectra( int maxResultRank = 1 )
        {
            if( maxResultRank < 0 )
                m_maxResultRank = std::numeric_limits<int>::max();
            else
                m_maxResultRank = maxResultRank;
            ReadSpectra( 0, 0 );
            return m_vars;
        }

    private:
        ifstream                        m_InputFile;
        SpectraListType                    m_overflowList;

        SpectraListType*                m_pSpectra;
        SpectrumType*                    m_pSpectrum;
        SearchResultSet<GenericSearchResult>    m_resultSet;
        GenericSearchResult                m_result;

        XML_Parser                        m_pParser;

        int                                m_nCount;
        string                            m_InputFileName;
        string                            m_scanName;
        int                                m_maxResultRank;
        int                                m_curResultRank;
        int                             m_maxChargeState;
        bool                            m_validSequence;
        bool                            m_precursorsHaveAverageMass;

        map<string,ScoreInfo>            m_absoluteScores;
        map<string,ScoreInfo>            m_relativeScores;

        RunTimeVariableMap                m_vars;
        ResidueMap                        m_residueMap;

        static void StartElement( void *userData, const char *name, const char **atts )
        {
            ReaderType* pInstance = static_cast< ReaderType* >( userData );
            SpectrumType*& s = pInstance->m_pSpectrum;
            GenericSearchResult& result = pInstance->m_result;
            RunTimeVariableMap& vars = pInstance->m_vars;

            string tag(name);
            int attsCount = XML_GetSpecifiedAttributeCount( pInstance->m_pParser );

            try
            {
                if( tag == "search_hit" )
                {

                    pInstance->m_curResultRank = GET_ATTR_AS("hit_rank", int);
                    if( pInstance->m_maxResultRank != -1 && pInstance->m_curResultRank > pInstance->m_maxResultRank ) {
                        pInstance->m_validSequence = false;
                        return;
                    }

                    string sequence = GET_ATTR("peptide");
                    // Make sure the peptide sequence doesn't have any unknown or ambiguous amino acids.
                    // We ignore those peptide sequences because PWIZ can't handle them.
                    size_t unknownAminoAcid = sequence.find_first_not_of("ACDEFGHIKLMNPQRSTUVWY");
                    if(unknownAminoAcid != string::npos)
                    {
                        pInstance->m_validSequence = false;
                        cerr << "Warning (PepXML Parser): Ignoring peptide with non-standard amino acids: " << sequence << endl;
                        return;
                    }
                    
                    pInstance->m_validSequence = true;
                    int specificTermini = HAS_ATTR("num_tol_term") ? GET_ATTR_AS("num_tol_term", int) : 2;
                    result = GenericSearchResult( DigestedPeptide(sequence.begin(),
                                                               sequence.end(),
                                                               0,
                                                               HAS_ATTR("num_missed_cleavages") ? GET_ATTR_AS("num_missed_cleavages", int) : 0,
                                                               specificTermini == 0 ? false: true,
                                                               specificTermini == 2 ? true : false) );
                    result.lociByName.clear();
                    result.scoreList.clear();
                    result.rank = pInstance->m_curResultRank;
                    result.lociByName.insert( ProteinLocusByName( GET_ATTR("protein") ) );
                    result.key.resize(2);
                    result.key[0] = HAS_ATTR("num_matched_ions") ? GET_ATTR_AS("num_matched_ions", int) : 0;
                    result.key[1] = HAS_ATTR("tot_num_ions") ? GET_ATTR_AS("tot_num_ions", int) : result.key[0];

                } else if( tag == "search_score" )
                {
                    if( !pInstance->m_validSequence || (pInstance->m_maxResultRank != -1 && pInstance->m_curResultRank > pInstance->m_maxResultRank) )
                        return;

                    string name = to_lower_copy( GET_ATTR("name") );
                    try
                    {
                        result.scoreList.push_back( SearchScoreInfo( name, GET_ATTR_AS("value", double) ) );
                    } catch( bad_lexical_cast& )
                    {
                        // ignore non-numeric scores
                    } catch( std::exception& )
                    {
                        // ignore scores without values
                    }

                } else if( tag == "alternative_protein" )
                {
                    if( !pInstance->m_validSequence || ( pInstance->m_maxResultRank != -1 && pInstance->m_curResultRank > pInstance->m_maxResultRank ))
                        return;

                    result.lociByName.insert( ProteinLocusByName( GET_ATTR("protein") ) );

                } else if( tag == "mod_aminoacid_mass" )
                {
                    if( !pInstance->m_validSequence || ( pInstance->m_maxResultRank != -1 && pInstance->m_curResultRank > pInstance->m_maxResultRank ))
                        return;

                    double mass = GET_ATTR_AS("mass", double);
                    size_t position = GET_ATTR_AS("position", size_t)-1;
                    //cout << result.sequence() << endl;
                    //cout << result.sequence()[position] << endl;
                    //cout << position << endl;
                    const Formula& aaFormula = AminoAcid::Info::record((result.sequence())[position]).residueFormula;
                    double deltaMass = mass - (pInstance->m_precursorsHaveAverageMass ? aaFormula.molecularWeight() : aaFormula.monoisotopicMass());
                    (result.modifications())[position].push_back(Modification(deltaMass, deltaMass));

                } else if( tag == "modification_info" )
                {
                    if( !pInstance->m_validSequence || ( pInstance->m_maxResultRank != -1 && pInstance->m_curResultRank > pInstance->m_maxResultRank ))
                        return;

                    int attrPos;

                    if( ( attrPos = paramIndex("mod_nterm_mass", atts, attsCount)+1 ) > 0 )
                    {
                        static Formula nTerminus("H1");
                        double nMass = lexical_cast<double>( atts[attrPos] );
                        double deltaMass = nMass - nTerminus.monoisotopicMass();
                        (result.modifications())[ModificationMap::NTerminus()].push_back(Modification(deltaMass, deltaMass));
                    }

                    if( ( attrPos = paramIndex("mod_cterm_mass", atts, attsCount)+1 ) > 0 )
                    {
                        static Formula cTerminus("H1O1");
                        double cMass = lexical_cast<double>( atts[attrPos] );
                        double deltaMass = cMass - cTerminus.monoisotopicMass();
                        (result.modifications())[ModificationMap::CTerminus()].push_back(Modification(deltaMass, deltaMass));
                    }

                } else if( tag == "spectrum_query" )
                {
                    
                    // Read the spectrum scanNum
                    int index;
                    if( HAS_ATTR("spectrumIndex") )
                        index = GET_ATTR_AS("spectrumIndex", int);
                    else
                        index = GET_ATTR_AS("start_scan", int)-1;
                    int charge = GET_ATTR_AS("assumed_charge", int);

                    pInstance->m_maxChargeState = max( charge, pInstance->m_maxChargeState );

                    // Create a new spectrum
                    s = new SpectrumType;
                    s->id.set( pInstance->m_scanName, index, charge );
                    //cout << pInstance->m_scanName << endl;
                    //s->stringID = HAS_ATTR("spectrumID") ? GET_ATTR("spectrumID") : lexical_cast<string>( index+1 );
                    s->nativeID = HAS_ATTR("spectrumNativeID") ? GET_ATTR("spectrumNativeID") : lexical_cast<string>( index+1 );

                    s->mOfPrecursor = GET_ATTR_AS("precursor_neutral_mass", double);
                    s->retentionTime = HAS_ATTR("retention_time_sec") ? ( GET_ATTR_AS("retention_time_sec", double) / 60.0 ) : 0;

                    // Set the spectrum filename and scanName
                    s->fileName = pInstance->m_InputFileName;

                } else if( tag == "search_result" )
                {
                    s->numTargetComparisons = HAS_ATTR("num_target_comparisons") ? GET_ATTR_AS("num_target_comparisons", int) : 0;
                    s->numDecoyComparisons = HAS_ATTR("num_decoy_comparisons") ? GET_ATTR_AS("num_decoy_comparisons", int) : 0;
                    s->detailedCompStats.numTargetUnmodComparisons = HAS_ATTR("target_unmod_comps") ? GET_ATTR_AS("target_unmod_comps", int) : 0;
                    s->detailedCompStats.numDecoyUnmodComparisons = HAS_ATTR("decoy_unmod_comps") ? GET_ATTR_AS("decoy_unmod_comps", int) : 0;
                    s->detailedCompStats.numTargetModComparisons = HAS_ATTR("target_mod_comps") ? GET_ATTR_AS("target_mod_comps", int) : 0;
                    s->detailedCompStats.numDecoyModComparisons = HAS_ATTR("decoy_mod_comps") ? GET_ATTR_AS("decoy_mod_comps", int) : 0;
                } else if( tag == "msms_run_summary" )
                {
                    pInstance->m_scanName = path( GET_ATTR("base_name") ).leaf();

                } else if( tag == "sample_enzyme" )
                {
                    // not supported yet

                } else if( tag == "search_summary" )
                {
                    bool& useAvgMass = pInstance->m_precursorsHaveAverageMass;
                    useAvgMass = GET_ATTR("precursor_mass_type") == "average";
                    vars["UseAvgMassOfSequences"] = useAvgMass ? "1" : "0";

                    vars["SearchEngine: Name"] = GET_ATTR("search_engine");
                    vars["SearchEngine: Version"] = "unknown";

                } else if( tag == "search_database" )
                {
                    vars["ProteinDatabase"] = GET_ATTR("local_path");

                } else if( tag == "enzymatic_search_constraint" )
                {
                    vars["NumMaxMissedCleavages"] = GET_ATTR("max_num_internal_cleavages");
                    vars["NumMinTerminiCleavages"] = GET_ATTR("min_number_termini");

                } else if( tag == "aminoacid_modification" )
                {
                    bool isDynamic = ( GET_ATTR("variable")[0] == 'Y' );
                    if( isDynamic )
                    {
                        string unmodChar = GET_ATTR("aminoacid");
                        string modMass = GET_ATTR("massdiff");
                        string symbol = HAS_ATTR("symbol") ? GET_ATTR("symbol") : "*";
                        vars["DynamicMods"] += unmodChar + " " + symbol + " " + modMass + " ";
                    } else
                    {
                        string unmodChar = GET_ATTR("aminoacid");
                        string modMass = GET_ATTR("massdiff");
                        vars["StaticMods"] += unmodChar + " " + modMass + " ";
                    }

                } else if( tag == "terminal_modification" )
                {
                    bool isDynamic = ( GET_ATTR("variable")[0] == 'Y' );
                    if( isDynamic )
                    {
                        string terminus = GET_ATTR("terminus");
                        const char* unmodChar = STR_EQUAL( terminus, "n" ) ? PEPTIDE_N_TERMINUS_STRING : PEPTIDE_C_TERMINUS_STRING;
                        string modMass = GET_ATTR("massdiff");
                        string symbol = HAS_ATTR("symbol") ? GET_ATTR("symbol") : "*";
                        vars["DynamicMods"] += string( unmodChar ) + " " + symbol + " " + modMass + " ";
                    } else
                    {
                        string terminus = GET_ATTR("terminus");
                        const char* unmodChar = STR_EQUAL( terminus, "n" ) ? PEPTIDE_N_TERMINUS_STRING : PEPTIDE_C_TERMINUS_STRING;
                        string modMass = GET_ATTR("massdiff");
                        vars["StaticMods"] += string( unmodChar ) + " " + modMass + " ";
                    }

                } else if( tag == "parameter" )
                {
                    string varName = GET_ATTR("name");
                    if( varName.find("Config: ") == 0 )
                        varName = varName.substr(8);
                    vars[varName] = GET_ATTR("value");
                }
            } catch( std::exception& e )
            {
                throw runtime_error( string( "error parsing element \"" ) + tag + "\": " + e.what() );
            }
        }

        static void EndElement( void *userData, const char *name )
        {
            ReaderType* pInstance = static_cast< ReaderType* >( userData );
            SpectrumType*& s = pInstance->m_pSpectrum;
            GenericSearchResult& result = pInstance->m_result;
            ResidueMap& residueMap = pInstance->m_residueMap;

            string tag(name);

            if( tag == "search_hit" )
            {
                if( !pInstance->m_validSequence || ( pInstance->m_maxResultRank != -1 && pInstance->m_curResultRank > pInstance->m_maxResultRank ))
                    return;
                //cout << result.sequence << '\n';

                //s->resultSet.insert( result );
                //cout << s->id.charge << "," << s->id.index << "," << result.sequence() << "," << result.modifications().monoisotopicDeltaMass() << endl;
                //Add the result.
                s->resultSet.update( result );

            } else if( tag == "modification_info" )
            {
                if( !pInstance->m_validSequence || ( pInstance->m_maxResultRank != -1 && pInstance->m_curResultRank > pInstance->m_maxResultRank ))
                    return;

            } else if( tag == "search_result" )
            {

                
                //s->resultSet.calculateRelativeScores();

            } else if( tag == "spectrum_query" )
            {
                if( s->resultSet.empty() )
                    s->numTerminiCleavages = 2;
                else
                    s->numTerminiCleavages = s->resultSet.rbegin()->specificTermini();

                // Push current spectrum onto the list
                pInstance->m_pSpectra->push_back( s );
                ++pInstance->m_nCount;

            } else if( tag == "search_summary" )
            {
                residueMap.setDynamicMods( pInstance->m_vars["DynamicMods"] );
                residueMap.setStaticMods( pInstance->m_vars["StaticMods"] );
                //residueMap.dump();

                if( pInstance->m_maxResultRank == 0 )
                    XML_StopParser( pInstance->m_pParser, XML_FALSE );
            } else if( tag == "msms_run_summary" )
            {
                // return the highest charge state in the file:
                // - the NumChargeStates taken from the header
                // - the highest assumed_charge attribute
                if( pInstance->m_vars.count("NumChargeStates") )
                    pInstance->m_maxChargeState = max( lexical_cast<int>(pInstance->m_vars["NumChargeStates"]),
                                                       pInstance->m_maxChargeState );
                pInstance->m_vars["SearchStats: MaxChargeState"] = lexical_cast<string>(pInstance->m_maxChargeState);
            }
        }

    };
}

#endif
