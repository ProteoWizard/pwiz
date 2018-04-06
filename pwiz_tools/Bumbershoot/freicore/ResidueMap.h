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

#ifndef _RESIDUEMAP_H
#define _RESIDUEMAP_H

#include "stdafx.h"
#include "shared_types.h"

namespace freicore
{
    //typedef map< char, float > n2m_t;
    typedef map< double, AminoAcidResidue > m2n_t;
    //typedef vector< float > n2m_t;
    typedef CharIndexedVector<double> n2m_t;

    class ResidueMap
    {
    public:
        ResidueMap()
        {
            isValid = false;
            // Alanine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('A').residueFormula.monoisotopicMass() ] = 'A';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('A').residueFormula.molecularWeight() ] = 'A';    
            // Arginine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('R').residueFormula.monoisotopicMass() ] = 'R';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('R').residueFormula.molecularWeight() ] = 'R';    
            // Asparagine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('N').residueFormula.monoisotopicMass() ] = 'N';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('N').residueFormula.molecularWeight() ] = 'N';    
            // Aspartic acid
            m_monoMassesToNamesMap[ AminoAcid::Info::record('D').residueFormula.monoisotopicMass() ] = 'D';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('D').residueFormula.molecularWeight() ] = 'D';    
            // Cysteine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('C').residueFormula.monoisotopicMass() ] = 'C';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('C').residueFormula.molecularWeight() ] = 'C';
            // Glutamic acid
            m_monoMassesToNamesMap[ AminoAcid::Info::record('E').residueFormula.monoisotopicMass() ] = 'E'; 
            m_avgMassesToNamesMap[ AminoAcid::Info::record('E').residueFormula.molecularWeight() ] = 'E';    
            // Glutamine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('Q').residueFormula.monoisotopicMass() ] = 'Q';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('Q').residueFormula.molecularWeight() ] = 'Q';    
            // Glycine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('G').residueFormula.monoisotopicMass() ] = 'G';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('G').residueFormula.molecularWeight() ] = 'G';    
            // Histidine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('H').residueFormula.monoisotopicMass() ] = 'H';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('H').residueFormula.molecularWeight() ] = 'H';
            // Isoleucine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('I').residueFormula.monoisotopicMass() ] = 'I';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('I').residueFormula.molecularWeight() ] = 'I';    
            // Lysine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('K').residueFormula.monoisotopicMass() ] = 'K';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('K').residueFormula.molecularWeight() ] = 'K';    
            // Methionine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('M').residueFormula.monoisotopicMass() ] = 'M';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('M').residueFormula.molecularWeight() ] = 'M';    
            // Phenylalanine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('F').residueFormula.monoisotopicMass() ] = 'F';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('F').residueFormula.molecularWeight() ] = 'F';    
            // Proline
            m_monoMassesToNamesMap[ AminoAcid::Info::record('P').residueFormula.monoisotopicMass() ] = 'P';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('P').residueFormula.molecularWeight() ] = 'P';
            // Serine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('S').residueFormula.monoisotopicMass() ] = 'S';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('S').residueFormula.molecularWeight() ] = 'S';
            // Threonine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('T').residueFormula.monoisotopicMass() ] = 'T';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('T').residueFormula.molecularWeight() ] = 'T';    
            // Tryptophan
            m_monoMassesToNamesMap[ AminoAcid::Info::record('W').residueFormula.monoisotopicMass() ] = 'W';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('W').residueFormula.molecularWeight() ] = 'W';    
            // Tyrosine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('Y').residueFormula.monoisotopicMass() ] = 'Y';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('Y').residueFormula.molecularWeight() ] = 'Y';    
            // Valine
            m_monoMassesToNamesMap[ AminoAcid::Info::record('V').residueFormula.monoisotopicMass() ] = 'V';
            m_avgMassesToNamesMap[ AminoAcid::Info::record('V').residueFormula.molecularWeight() ] = 'V';    

            for( m2n_t::iterator itr = m_monoMassesToNamesMap.begin(); itr != m_monoMassesToNamesMap.end(); ++itr )
            {
                m_defaultNamesToMonoMassesMap[ itr->second ] = itr->first;
                m_defaultResidues.insert( itr->second );
            }

            for( m2n_t::iterator itr = m_avgMassesToNamesMap.begin(); itr != m_avgMassesToNamesMap.end(); ++itr )
                m_defaultNamesToAvgMassesMap[ itr->second ] = itr->first;

            Formula hydroxyl("O1H1");
            Formula hydrogen("H1");
            m_monoMassesToNamesMap[ hydroxyl.monoisotopicMass() ] = PEPTIDE_C_TERMINUS_SYMBOL;
            m_avgMassesToNamesMap[ hydroxyl.molecularWeight() ] = PEPTIDE_C_TERMINUS_SYMBOL;
            m_monoMassesToNamesMap[ hydrogen.monoisotopicMass() ] = PEPTIDE_N_TERMINUS_SYMBOL;
            m_avgMassesToNamesMap[ hydrogen.molecularWeight() ] = PEPTIDE_N_TERMINUS_SYMBOL;

            finalize();
        }

        bool initialized() { return isValid; }

        int initializeFromFile( const string& residuesCfgFilename = "residue_masses.cfg" )
        {
            ifstream residuesCfgFile( residuesCfgFilename.c_str() );
            if( residuesCfgFile.is_open() )
            {
                // clear old residues
                m_residues.clear();
                m_monoMassesToNamesMap.clear();
                m_avgMassesToNamesMap.clear();
                m_namesToMonoMassesMap.clear();
                m_namesToAvgMassesMap.clear();

                int cfgSize = (int) GetFileSize( residuesCfgFilename );
                cfgStr.resize( cfgSize );
                residuesCfgFile.read( &cfgStr[0], cfgSize );
                int rv = initializeFromBuffer( cfgStr );
                residuesCfgFile.close();
                return rv;
            } else
                return 1;
        }

        int initializeFromBuffer( const string& cfgStr )
        {
            isValid = false;
            stringstream cfgStream( cfgStr );

            char r;
            double mono, avg;
            while( cfgStream >> r >> mono >> avg )
            {
                if( !m_defaultResidues.count(r) )
                    cerr << "Warning: residue map has been initialized with non-standard residue '" << r << "'!" << endl;
                else
                {
                    if( m_defaultNamesToMonoMassesMap[r] != mono )
                        cerr << "Warning: residue map has initialized standard residue '" << r << "' with non-standard monoisotopic mass!" << endl;
                    if( m_defaultNamesToAvgMassesMap[r] != avg )
                        cerr << "Warning: residue map has initialized standard residue '" << r << "' with non-standard average mass!" << endl;
                }

                m_monoMassesToNamesMap[ mono ] = r;
                m_avgMassesToNamesMap[ avg ] = r;
            }
            
            if( m_monoMassesToNamesMap.empty() || m_avgMassesToNamesMap.empty() )
                return 1;

            finalize();

            return 0;
        }

        void finalize()
        {
            for( m2n_t::iterator itr = m_monoMassesToNamesMap.begin(); itr != m_monoMassesToNamesMap.end(); ++itr )
            {
                m_namesToMonoMassesMap[ itr->second ] = itr->first;
                m_residues.insert( itr->second );
            }

            for( m2n_t::iterator itr = m_avgMassesToNamesMap.begin(); itr != m_avgMassesToNamesMap.end(); ++itr )
                m_namesToAvgMassesMap[ itr->second ] = itr->first;

            if( m_namesToMonoMassesMap['I'] > 0 )
            {
                m_namesToMonoMassesMap['L'] = m_namesToMonoMassesMap['I'];
                m_namesToAvgMassesMap['L'] = m_namesToAvgMassesMap['I'];
                m_residues.insert('L');
            } else if( m_namesToMonoMassesMap['L'] > 0 )
            {
                m_namesToMonoMassesMap['I'] = m_namesToMonoMassesMap['L'];
                m_namesToAvgMassesMap['I'] = m_namesToAvgMassesMap['L'];
                m_residues.insert('L');
            }

            m_residues.erase(PEPTIDE_N_TERMINUS_SYMBOL);
            m_residues.erase(PEPTIDE_C_TERMINUS_SYMBOL);
            isValid = true;
        }

        void addDynamicMod( const DynamicMod& mod )
        {
            if( !dynamicMods.count( mod ) )
            {
                dynamicMods.insert( mod );
                m_monoMassesToNamesMap[ m_namesToMonoMassesMap[ mod.unmodChar ] + mod.modMass ] = mod.uniqueModChar;
                m_avgMassesToNamesMap[ m_namesToAvgMassesMap[ mod.unmodChar ] + mod.modMass ] = mod.uniqueModChar;
                m_namesToMonoMassesMap[ mod.uniqueModChar ] = m_namesToMonoMassesMap[ mod.unmodChar ] + mod.modMass;
                m_namesToAvgMassesMap[ mod.uniqueModChar ] = m_namesToAvgMassesMap[ mod.unmodChar ] + mod.modMass;
            }
        }

        void removeDynamicMod( const DynamicMod& mod )
        {
            if( dynamicMods.count( mod ) )
            {
                dynamicMods.erase( mod );
                forceAddDynamicMod( mod );
            }
        }

        void addStaticMod( const StaticMod& mod )
        {
            if( !staticMods.count( mod ) )
            {
                staticMods.insert( mod );
                forceAddStaticMod( mod );
            }
        }

        void clearDynamicMods()
        {
            for( DynamicModSet::iterator itr = dynamicMods.begin(); itr != dynamicMods.end(); ++itr )
            {
                m_monoMassesToNamesMap.erase( m_namesToMonoMassesMap[ itr->unmodChar ] + itr->modMass );
                m_avgMassesToNamesMap.erase( m_namesToAvgMassesMap[ itr->unmodChar ] + itr->modMass );
                m_namesToMonoMassesMap.erase( itr->uniqueModChar );
                m_namesToAvgMassesMap.erase( itr->uniqueModChar );
            }
            dynamicMods.clear();
        }

        void clearStaticMods()
        {
            for( StaticModSet::iterator itr = staticMods.begin(); itr != staticMods.end(); ++itr )
            {
                double monoMass = m_namesToMonoMassesMap[ itr->name ];
                double avgMass = m_namesToAvgMassesMap[ itr->name ];
                m_monoMassesToNamesMap[ monoMass - itr->mass ] = itr->name;
                m_avgMassesToNamesMap[ avgMass - itr->mass ] = itr->name;
                m_monoMassesToNamesMap.erase( monoMass );
                m_avgMassesToNamesMap.erase( avgMass );
                m_namesToMonoMassesMap[ itr->name ] = monoMass - itr->mass;
                m_namesToAvgMassesMap[ itr->name ] = avgMass - itr->mass;
            }
            staticMods.clear();
        }

        void setDynamicMods( const string& cfgStr )
        {
            clearDynamicMods();
            if( cfgStr.empty() ) return;
            DynamicModSet tmp( cfgStr );
            for( DynamicModSet::iterator itr = tmp.begin(); itr != tmp.end(); ++itr )
                addDynamicMod( *itr );
        }

        void setStaticMods( const string& cfgStr )
        {
            clearStaticMods();
            if( cfgStr.empty() ) return;
            StaticModSet tmp( cfgStr );
            for( StaticModSet::iterator itr = tmp.begin(); itr != tmp.end(); ++itr )
                addStaticMod( *itr );
        }

        const set<char>& getResidues() const    { return m_residues; }
        bool    hasResidue( char r ) const        { return m_namesToMonoMassesMap[r] > 0; }
        double    largestDynamicModMass() const    { return ( dynamicMods.empty() ? 0 : dynamicMods.rbegin()->modMass ); }
        double    smallestDynamicModMass() const    { return ( dynamicMods.empty() ? 0 : dynamicMods.begin()->modMass ); }

        inline double GetMassOfResidues( const string::const_iterator& seqBegin, const string::const_iterator& seqEnd, bool useAvgMass = false ) const
        {
            double mass = 0.0;
            if( useAvgMass )
            {
                for( string::const_iterator itr = seqBegin; itr != seqEnd; ++itr )
                    mass += getAvgMassByName( *itr );
            } else
            {
                for( string::const_iterator itr = seqBegin; itr != seqEnd; ++itr )
                    mass += getMonoMassByName( *itr );
            }
            return mass;
        }

        inline double GetMassOfResidues( const string& residues, bool useAvgMass = false ) const
        {
            return GetMassOfResidues( residues.begin(), residues.end(), useAvgMass );
        }

        void dump() const
        {
            //cout << "Residue map size: " << m_namesToMonoMassesMap.size() << endl;
            //for( n2m_t::iterator itr = m_namesToMonoMassesMap.begin(); itr != m_namesToMonoMassesMap.end(); ++itr )
            //    cout << itr->first << ": " << itr->second << " " << m_namesToAvgMassesMap[ itr->first ] << endl;
            for( size_t i=0; i < 128; ++i )
                if( m_namesToMonoMassesMap[i] > 0 )
                    cout << (char) i << ": " << m_namesToMonoMassesMap[i] << " " << m_namesToAvgMassesMap[i] << endl;
            cout << dynamicMods.userToUniqueMap << endl << dynamicMods.uniqueToUserMap << endl;
        }


        size_t size() const                                { return m_namesToMonoMassesMap.size(); }

        n2m_t::const_iterator beginMonoNames() const    { return m_namesToMonoMassesMap.begin(); }
        n2m_t::const_iterator endMonoNames() const        { return m_namesToMonoMassesMap.end(); }
        n2m_t::const_iterator beginAvgNames() const        { return m_namesToAvgMassesMap.begin(); }
        n2m_t::const_iterator endAvgNames() const        { return m_namesToAvgMassesMap.end(); }

        m2n_t::const_iterator beginMonoMasses() const    { return m_monoMassesToNamesMap.begin(); }
        m2n_t::const_iterator endMonoMasses() const        { return m_monoMassesToNamesMap.end(); }
        m2n_t::const_iterator beginAvgMasses() const    { return m_avgMassesToNamesMap.begin(); }
        m2n_t::const_iterator endAvgMasses() const        { return m_avgMassesToNamesMap.end(); }

        char getNameByMonoMass( double key ) const        { return m_monoMassesToNamesMap.find( key )->second; }
        char getNameByAvgMass( double key ) const        { return m_avgMassesToNamesMap.find( key )->second; }

        char getNameByMonoMass( double key, double epsilon ) const
        {
            if( epsilon > 0.0f )
            {
                m2n_t::const_iterator min, max, cur, best;
                min = m_monoMassesToNamesMap.lower_bound( key - epsilon );
                max = m_monoMassesToNamesMap.lower_bound( key + epsilon );
                if( min == max )
                    throw runtime_error( string( "No residue matching mass " ) + lexical_cast<string>( key ) );

                double minDelta = fabs( min->first - key );
                for( best = cur = min; cur != max; ++cur )
                {
                    double curDelta = fabs( cur->first - key );
                    if( curDelta < minDelta )
                    {
                        minDelta = curDelta;
                        best = cur;
                    }
                }
                return best->second;
            } else
                return getNameByMonoMass(key);
        }

        char getNameByAvgMass( double key, double epsilon ) const
        {
            if( epsilon > 0.0f )
            {
                m2n_t::const_iterator min, max, cur, best;
                min = m_avgMassesToNamesMap.lower_bound( key - epsilon );
                max = m_avgMassesToNamesMap.lower_bound( key + epsilon );
                if( min == max )
                    throw runtime_error( string( "No residue matching mass " ) + lexical_cast<string>( key ) );

                double minDelta = fabs( min->first - key );
                for( best = cur = min; cur != max; ++cur )
                {
                    double curDelta = fabs( cur->first - key );
                    if( curDelta < minDelta )
                    {
                        minDelta = curDelta;
                        best = cur;
                    }
                }
                return best->second;
            } else
                return getNameByAvgMass(key);
        }

        double getMonoMassByName( char key ) const        { return m_namesToMonoMassesMap[ key ]; }
        double getAvgMassByName( char key ) const        { return m_namesToAvgMassesMap[ key ]; }

        string            cfgStr;
        DynamicModSet    dynamicMods;
        StaticModSet    staticMods;
        bool            isValid;

    private:

        void forceAddDynamicMod( const DynamicMod& mod )
        {
            m_monoMassesToNamesMap[ m_namesToMonoMassesMap[ mod.unmodChar ] + mod.modMass ] = mod.uniqueModChar;
            m_avgMassesToNamesMap[ m_namesToAvgMassesMap[ mod.unmodChar ] + mod.modMass ] = mod.uniqueModChar;
            m_namesToMonoMassesMap[ mod.uniqueModChar ] = m_namesToMonoMassesMap[ mod.unmodChar ] + mod.modMass;
            m_namesToAvgMassesMap[ mod.uniqueModChar ] = m_namesToAvgMassesMap[ mod.unmodChar ] + mod.modMass;
        }

        void forceAddStaticMod( const StaticMod& mod )
        {
            double monoMass = m_namesToMonoMassesMap[ mod.name ];
            double avgMass = m_namesToAvgMassesMap[ mod.name ];
            m_monoMassesToNamesMap[ monoMass + mod.mass ] = mod.name;
            m_avgMassesToNamesMap[ avgMass + mod.mass ] = mod.name;
            m_monoMassesToNamesMap.erase( monoMass );
            m_avgMassesToNamesMap.erase( avgMass );
            m_namesToMonoMassesMap[ mod.name ] = monoMass + mod.mass;
            m_namesToAvgMassesMap[ mod.name ] = avgMass + mod.mass;
        }

        n2m_t m_defaultNamesToMonoMassesMap;
        n2m_t m_defaultNamesToAvgMassesMap;
        set<char> m_defaultResidues;

        n2m_t m_namesToMonoMassesMap;
        n2m_t m_namesToAvgMassesMap;
        m2n_t m_monoMassesToNamesMap;
        m2n_t m_avgMassesToNamesMap;
        set<char> m_residues;
    };
}

#endif

