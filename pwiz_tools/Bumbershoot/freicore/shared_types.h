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

#ifndef _SHARED_TYPES_H
#define _SHARED_TYPES_H

#include "stdafx.h"
#include "SubsetEnumerator.h"
#include "float.h"
#include "boost/variant.hpp"

namespace freicore
{
    class ResidueMap;
    class lnFactorialTable;
    //class mzDataReader;
    struct BaseRunTimeConfig;
    struct SqtList;
    
    // Units of the mass measurements
    enum MassUnits { THOMSON, PPM, UNKNOWN, DALTONS };
    // Terminal types
    enum TermType {NTERM, CTERM, NONE};
    // Terminal mass match types
    enum TermMassMatch {MASS_MATCH, MASS_MISMATCH};

    enum MassType
    {
        MassType_Monoisotopic,
        MassType_Average
    };

    struct PrecursorMassHypothesis
    {
        double mass;
        MassType massType;
        int charge;

        bool operator< (const PrecursorMassHypothesis& rhs) const {return mass < rhs.mass;}

        template<class Archive>
        void serialize(Archive& ar, const unsigned int version)
        {
            ar & mass & massType & charge;
        }
    };

    typedef enum { SYS_BIG_ENDIAN, SYS_LITTLE_ENDIAN, SYS_UNKNOWN_ENDIAN } endianType_t;

    typedef char                                            AminoAcidResidue;
    const size_t                                            AminoAcidResidueSize = sizeof(AminoAcidResidue)*CHAR_BIT;
    //const double                                            DBL_EPSILON = std::numeric_limits<double>::min();

    typedef unsigned long                                    ProteinIndex;
    typedef unsigned short                                    ProteinOffset;
    typedef string                                            ProteinName;

    struct ProteinLocusByIndex
    {
        ProteinLocusByIndex( ProteinIndex pIndex = 0, ProteinOffset pOffset = 0 ) : index( pIndex ), offset( pOffset ) {}
        ProteinIndex    index;
        ProteinOffset    offset;

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & index & offset;
        }

        bool operator< ( const ProteinLocusByIndex& rhs ) const
        {
            if( index == rhs.index )
                return offset < rhs.offset;
            else
                return index < rhs.index;
        }
    };

    struct ProteinLocusByName
    {
        ProteinLocusByName( ProteinName pName = "", ProteinOffset pOffset = 0 ) : name( pName ), offset( pOffset ) {}
        ProteinName        name;
        ProteinOffset    offset;
        string            desc;

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & name & offset;
        }

        bool operator< ( const ProteinLocusByName& rhs ) const
        {
            if( name == rhs.name )
                return offset < rhs.offset;
            else
                return name < rhs.name;
        }
    };

    typedef set< ProteinLocusByIndex >                        ProteinLociByIndex;
    typedef set< ProteinLocusByName >                        ProteinLociByName;
    typedef map< ProteinIndex, vector< ProteinOffset > >    ProteinLociMap;

    struct CleavageHalfRule : public set< string >
    {
        CleavageHalfRule() : hasWildcard( false ), longestCleavageCandidate( 0 ) {}
        void clear()
        {
            set< string >::clear();
            hasWildcard = false;
            longestCleavageCandidate = 0;
        }

        SetInsertPair( set< string > ) insert( const string& seq )
        {
            if( seq == "." )
            {
                hasWildcard = true;
                return SetInsertPair( set< string > )( end(), false );
            } else
            {
                longestCleavageCandidate = max( seq.length(), longestCleavageCandidate );
                return set< string >::insert( seq );
            }
        }

        bool hasWildcard;
        size_t longestCleavageCandidate;
    };

    struct CleavageRule : public pair< CleavageHalfRule, CleavageHalfRule >
    {
        CleavageRule() {}
    };

    struct CleavageRuleSet : public vector< CleavageRule >
    {
        CleavageRuleSet( const string& cfgStr = "" ) : longestPreCleavageCandidate( 0 ), longestPostCleavageCandidate( 0 )
        {
            initialize( cfgStr );
        }

        size_t longestPreCleavageCandidate;
        size_t longestPostCleavageCandidate;

        void initialize( const string& cfgStr );

        /// returns a string in the PSI-PI zero-width regex syntax
        string asCleavageAgentRegex();
    };

    struct ResidueFilter
    {
        ResidueFilter() {};

        bool testResidue( const AminoAcidResidue& r ) const
        {
            return m_filter[r];
        }

        operator string () const;
        CharIndexedVector<bool> m_filter;
    };

    /**!
        DynamicMod represents the meta data about a variable post-translational modification
    */
    struct DynamicMod : pwiz::proteome::Modification
    {
        DynamicMod( char unmodChar = 0, char userModChar = 0, double modMass = 0 )
            : Modification( modMass, modMass ),
              unmodChar( unmodChar ), userModChar( userModChar ), modMass( modMass ) {}

        vector< ResidueFilter > NTerminalFilters;
        vector< ResidueFilter > CTerminalFilters;

        char unmodChar;
        char userModChar;
        char uniqueModChar;
        double modMass;

        bool operator< ( const DynamicMod& rhs ) const
        {
            return uniqueModChar < rhs.uniqueModChar;
        }
    };
    
    /**!
        StaticMod represents the meta data about a static post-translational modification
    */
    struct StaticMod : pwiz::proteome::Modification
    {
        StaticMod() {}
        StaticMod( char r, double m )
            : Modification( m, m ),
              name(r), mass(m) {}

        char name;
        double mass;

        bool operator< ( const StaticMod& rhs ) const
        {
            if( name == rhs.name )
                return mass < rhs.mass;
            return name < rhs.name;
        }
    };

    /**!
        DynamicModSet maps the DynamicMod structures to a character
        used to represent the respective mods
    */
    struct DynamicModSet : public set< DynamicMod >
    {
        typedef map< char, vector< DynamicMod > >    UserToUniqueMap;
        typedef map< char, DynamicMod >                UniqueToUserMap;

        DynamicModSet( const string& cfgStr = "" )
        {
            initialize( cfgStr, false );
        }

        UserToUniqueMap    userToUniqueMap;
        UniqueToUserMap    uniqueToUserMap;

        void clear();
        void erase( const DynamicMod& mod );
        SetInsertPair(set< DynamicMod >) insert( const DynamicMod& mod );

        void initialize( const string& cfgStr, bool noUserChar = false);
        //int size() { return userToUniqueMap.size(); };
        void parseMotif( const string& motif, char modChar, double modMass );
        operator string () const;
    };

    struct StaticModSet : public set< StaticMod >
    {
        StaticModSet( const string& cfgStr = "" )
        {
            boost::char_separator<char> delim(" ");
            stokenizer parser( cfgStr.begin(), cfgStr.begin() + cfgStr.length(), delim );
            stokenizer::iterator itr = parser.begin();
            while( itr != parser.end() )
            {
                char r = (*itr)[0];
                double m = lexical_cast<double>( *(++itr) );
                insert( StaticMod( r, m ) );
                ++itr;
            }
        }

        operator string ()
        {
            stringstream modStr;
            for( iterator itr = begin(); itr != end(); ++itr )
                modStr << ( itr == begin() ? "" : " " ) << itr->name << " " << itr->mass;
            return modStr.str();
        }
    };

    /**!
    DeltaMassList maps a list of DeltaMassEntry (modification meta-data entries)
    to a particular delta mass. This class can also all possible combinations
    of a given mass list and use them to split the "delta masses" found during
    the tag reconciliation stage.
    */
    struct PreferredDeltaMassesList : public multimap< float, DynamicModSet >
    {
        PreferredDeltaMassesList(  const string& cfgStr = "" , int numCombinations = 0) 
        {
            try 
            {
                DynamicModSet mods;
                mods.initialize(cfgStr, true);
                if(mods.size()==0)
                    return;
                // An array of ints to store the index of the amino acid
                // in the sequence to which the ptmRadix corresponds.
                vector <size_t> positions;
                // An array of arrays to store the ptms that can be present on a residue;
                vector < DynamicModSet::const_iterator > ptms;
                size_t index = 0;
                for(DynamicModSet::const_iterator iter = mods.begin(); iter != mods.end(); ++iter)
                {
                    ptms.push_back(iter);
                    positions.push_back(index++);
                }
                // Generate all possible combinations of the given mods and store them
                SubsetEnumerator enumerator(positions.size(),1,numCombinations,positions);
                do
                {
                    DynamicModSet mods;
                    float totalMass = 0.0;
                    // Add the mods
                    for(size_t k = 1; k <= enumerator.k_; ++k) 
                    {
                        mods.insert(*ptms[enumerator.S_[k]-1]);
                        totalMass += (*ptms[enumerator.S_[k]-1]).modMass;
                    }
                    // Check for multiple mods cancelling each other's masses.
                    if(mods.size() == 1 || ( mods.size()> 1 && fabs(totalMass) > NEUTRON) )
                        insert(pair<float,DynamicModSet>(totalMass,mods));
                } while(enumerator.next());
            }catch(exception&)
            {
                throw runtime_error("error while parsing the \"PreferredDeltaMasses\" variable.");
            }
            if(false)
            {
                for(multimap< float, DynamicModSet >::iterator iter = begin(); iter != end(); ++iter)
                {
                    cout << (*iter).first;
                    for(DynamicModSet::const_iterator modIter = (*iter).second.begin(); modIter != (*iter).second.end(); ++modIter)
                        cout << "," << (*modIter).modMass << ":" << (*modIter).unmodChar;
                    cout << endl;
                }
            }
        }

        /* This function figures out which "Preferred Mass Shifts" are possible for  given mass. */
        vector<DynamicModSet> getMatchingMassShifts(float modMass, float tolerance);
        bool    containsMassShift(float modMass, float tolerance);
        
    };

    enum FragmentTypes
    {
        FragmentType_A,
        FragmentType_B,
        FragmentType_C,
        FragmentType_X,
        FragmentType_Y,
        FragmentType_Z,
        FragmentType_Z_Radical,
        FragmentTypes_Size
    };

    struct FragmentTypesBitset : public bitset<FragmentTypes_Size>
    {
        FragmentTypesBitset() {}

// HACK: MSVC 10's non-standard int constructor requires this
#if _HAS_CPP0X
        FragmentTypesBitset(int val)
#else
        FragmentTypesBitset(unsigned long val)
#endif
             : bitset<FragmentTypes_Size>(val) {}

        template<class Archive>
        void save(Archive& ar, const unsigned int version) const
        {
            unsigned long val = to_ulong();
            ar << val;
        }

        template<class Archive>
        void load(Archive& ar, const unsigned int version)
        {
            unsigned long val;
            ar >> val;
            *this = FragmentTypesBitset(val);
        }

        BOOST_SERIALIZATION_SPLIT_MEMBER()
    };

    /// represents a PSI nativeID in a way that will sort properly (i.e. scan=9 < scan=10)
    struct NativeID
    {
        NativeID() {}

        NativeID( const string& id ) : _id(id)
        {
            size_t indexEquals, indexSpace = 0;
            do
            {
                indexEquals = id.find('=', indexSpace);
                if (indexEquals == string::npos)
                    throw runtime_error("[NativeID::ctor] Bad format: " + id);

                string value;

                indexSpace = id.find(' ', indexEquals+1);
                if (indexSpace == string::npos)
                    value = id.substr(indexEquals+1);
                else
                    value = id.substr(indexEquals+1, indexSpace-indexEquals-1);

                try
                {
                    int value2 = lexical_cast<int>(value);
                    _pieces.push_back(IdVariantType(value2));
                }
                catch (bad_lexical_cast&)
                {
                    _pieces.push_back(IdVariantType(value));
                }
            } while (indexSpace != string::npos);
        }

        bool operator== (const NativeID& rhs) const
        {
            if (_pieces.size() != rhs._pieces.size())
                return false;
            for (size_t i=0; i < _pieces.size(); ++i)
                if (!(_pieces[i] == rhs._pieces[i]))
                    return false;
            return true;
        }

        bool operator< (const NativeID& rhs) const
        {
            if (_pieces.size() != rhs._pieces.size())
                return _pieces.size() < rhs._pieces.size();
            for (size_t i=0; i < _pieces.size(); ++i)
                if (!(_pieces[i] == rhs._pieces[i]))
                    return _pieces[i] < rhs._pieces[i];
            return false;
        }

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & _id & _pieces;
        }

        operator string () { return _id; }

    private:
        string _id;
        typedef boost::variant<int, string> IdVariantType;
        vector<IdVariantType> _pieces;
    };

    struct SpectrumId
    {
        SpectrumId( const string& s = "" )
            : id(s)
        {
            updateFromString();
        }

        SpectrumId( const string& source, const string& nativeID, int charge = 0 )
            : source(source), nativeID(nativeID), charge(charge)
        {
            updateFromVars();
        }

        SpectrumId( const string& nativeID, int charge )
            : source(""), nativeID(nativeID), charge(charge)
        {
            updateFromVars();
        }

        void set( const string& source, const string& nativeID, int charge = 0 )
        {
            this->source = source; this->nativeID = NativeID(nativeID); this->charge = charge;
            updateFromVars();
        }

        string toString() {
            std::ostringstream outString;
            outString << source << "," << (string) nativeID << "," << charge;
            return outString.str();
        }

        void setId( const SpectrumId& id )        { *this = id; }
        void setId( const string& id )            { this->id = id; updateFromString(); }
        void setSource( const string& source )    { this->source = source; updateFromVars(); }
        void setNativeID( const string& id )    { this->nativeID = NativeID(id); updateFromVars(); }
        void setCharge( int charge )            { this->charge = charge; updateFromVars(); }

        bool operator< ( const SpectrumId& rhs ) const
        {
            if( source == rhs.source )
                if( nativeID == rhs.nativeID )
                    return charge < rhs.charge;
                else
                    return nativeID < rhs.nativeID;
            else
                return source < rhs.source;
        }

        bool operator== ( const SpectrumId& rhs ) const
        {
            return source == rhs.source && charge == rhs.charge && nativeID == rhs.nativeID;
        }

        operator string () { return id; }

        template< class Archive >
        void serialize( Archive& ar, const unsigned int version )
        {
            ar & id & source & nativeID & charge;
        }

        string      id;
        string      source;
        NativeID    nativeID;
        int         charge;

    private:
        void updateFromVars()
        {
            stringstream idStream;
            if( !source.empty() )
                idStream << source << '.';

            idStream << (string) nativeID;

            if( charge > 0 )
                idStream << '.' << charge;

            id = idStream.str();
        }

        void updateFromString()
        {
            size_t firstDot = id.find_first_of( '.' );
            size_t lastDot = id.find_last_of( '.' );

            source = id.substr( 0, firstDot );

            if( firstDot < lastDot )
            {
                nativeID = NativeID(id.substr( firstDot+1, lastDot-firstDot-1 ));
                charge = lexical_cast<int>( id.substr( lastDot+1 ) );
            } else if( firstDot != string::npos )
                nativeID = NativeID(id.substr( firstDot+1 ));
        }
    };
    
    typedef set< string >            fileList_t;
    typedef vector< string >        argList_t;
}
#endif
