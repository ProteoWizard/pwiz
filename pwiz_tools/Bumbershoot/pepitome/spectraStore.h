//
// $Id: spectraStore.h 2 2010-03-02 20:56:25Z dasari $
//

#ifndef _SPECTRASTORE_H
#define _SPECTRASTORE_H

#include "stdafx.h"
#include "freicore.h"
#include "PeakSpectrum.h"
#include "shared_types.h"
#include <boost/tokenizer.hpp>
#include <boost/assign.hpp>
#include <boost/algorithm/string.hpp>
#include <boost/algorithm/string/find.hpp>
#include <boost/algorithm/string/trim.hpp>
#include <boost/algorithm/string/split.hpp>
#include <boost/algorithm/string/predicate.hpp>
#include <iostream>
#include <ctype.h>
#include <fstream>

using namespace boost::assign;
using namespace boost::algorithm;

namespace freicore
{

namespace pepitome
{

    static const boost::char_separator<char> delim(" =\r\n");
    static const boost::char_separator<char> modsDelim("@");
    static const boost::char_separator<char> peakDelim("\t ");
    static const boost::char_separator<char> colon(":");
    static const boost::char_separator<char> backslash("/");
    static const boost::char_separator<char> dot(".");
    static const boost::char_separator<char> comma(",");
    static const boost::char_separator<char> equals("=");


    typedef boost::tokenizer<boost::char_separator<char> > tokenizer;
    typedef multimap<double,float> Peaks;
    typedef multimap<int,double> ModMap;
    typedef multimap<string, unsigned int> ProteinMap;

    typedef BasePeakData< PeakInfo > PeakData;

    // Modification values taken from http://chemdata.nist.gov/mass-spc/ftp/mass-spc/PepLib.pdf    
    static map<string,double> ModNamesToMasses = map_list_of ("Oxidation", 15.994915) ("Carbamidomethyl", 57.02146) \
        ("ICAT_light", 227.12) ("ICAT_heavy", 236.12) \
        ("AB_old_ICATd0", 442.20) ("AB_old_ICATd8", 450.20) \
        ("Acetyl", 42.0106) ("Deamidation", 0.9840) ("Pyro-cmC", 39.994915) \
        ("Pyro-glu", -17.026549) ("Pyro_glu", -18.010565) ("Amide", -0.984016) \
        ("Phospho", 79.9663) ("Methyl", 14.0157) ("Carbamyl", 43.00581) \
        ("Gln->pyro-Glu", -17.0265) ("Glu->pyro-Glu", -18.0106) ("Carboxymethyl", 58.005479) \
        ("Deamidated", 0.984016);

    struct BaseLibrarySpectrum
    {
        // Peptide seqeunce
        shared_ptr<DigestedPeptide> matchedPeptide;
        ProteinMap matchedProteins;

        // Spectrum info
        SpectrumId id;
        PeakPreData peakPreData;
        PeakData    peakData;
        int peakPreCount;
        // Neutral mass
        double libraryMass;
        double monoisotopicMass;
        double averageMass;
        // Data indices
        unsigned long peakPreDataIndex;
        unsigned long headerIndex;
        // Peptide data
        size_t numMissedCleavages;
        int NTT;

        BaseLibrarySpectrum() : peakPreCount(0), libraryMass(0.0), peakPreDataIndex(0), headerIndex(0), numMissedCleavages(0), NTT(-1) {}
        BaseLibrarySpectrum( const BaseLibrarySpectrum& old )
        {
            matchedPeptide = old.matchedPeptide;
            matchedProteins = old.matchedProteins;
            id = old.id;
            peakPreData = old.peakPreData;
            peakData = old.peakData;
            peakPreCount = old.peakPreCount;
            libraryMass = old.libraryMass;
            peakPreDataIndex = old.peakPreDataIndex;
            headerIndex = old.headerIndex;
            numMissedCleavages = old.numMissedCleavages;
            NTT = old.NTT;
        }

        virtual ~BaseLibrarySpectrum() 
        {
            matchedPeptide.reset(); 
            matchedProteins.clear(); 
            peakPreData.clear();
            peakData.clear();
        }

        virtual void readHeader(ifstream & library) {}
        virtual void readSpectrum(float TICCutoff = 1.0f, size_t maxPeakCount = 100, bool cleanLibSpectra = false) {}

        virtual void readPeaks(ifstream & library, bool cleanSpectrum = false)
        {
            library.seekg(0,ios::beg);
            library.seekg(peakPreDataIndex);
            string input;
            peakPreData.clear();
            while(getline(library, input))
            {
                if(input.size() < 5)
                    break;
                tokenizer parser(input, peakDelim);
                tokenizer::iterator itr = parser.begin();
                string attribute = *(itr);
                string value = *(++itr);
                //Skip the peak annotations
                string peakAnn = *(++itr);
                bool isotopeOrUnannotated = (icontains(peakAnn, "i") || icontains(peakAnn, "?"));
                if(cleanSpectrum && isotopeOrUnannotated)
                    continue;
                if(isdigit(attribute[0]))
                {
                    float peakMass = lexical_cast<float>(attribute);
                    float intensity = lexical_cast<float>(value);
                    peakPreData.insert(pair<float,float>(peakMass,intensity));
                }
            }
        }

        virtual void clearSpectrum()
        {
            matchedPeptide.reset();
            matchedProteins.clear();
            peakPreData.clear();
            peakData.clear();
        }

        virtual void clearHeader()
        {
            matchedPeptide.reset();
            matchedProteins.clear();
        }

        void clearPeaks()
        {
            peakPreData.clear();
            peakData.clear();
        }

        // Filters out the peaks with the lowest intensities until only <ticCutoffPercentage> of the total ion current remains
        void FilterByTIC( double ticCutoffPercentage )
        {
            //cout << "TicCutoffPercentage:" << ticCutoffPercentage << endl;
            //exit(1);
            if( !peakPreData.empty() )
            {
                // Sort peak list in descending order of intensity while calculating the total ion current in the spectrum.
                // Use a multimap because multiple peaks can have the same intensity.
                float totalIonCurrent = 0.0f;
                typedef multimap< double, double > IntenSortedPeakPreData;
                IntenSortedPeakPreData intenSortedPeakPreData;
                for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
                {
                    totalIonCurrent += itr->second;
                    IntenSortedPeakPreData::iterator iItr = intenSortedPeakPreData.insert( make_pair( itr->second, itr->second ) );
                    iItr->second = itr->first;
                }

                double relativeIntensity = 0.0f;
                IntenSortedPeakPreData::reverse_iterator r_iItr;
                for(	r_iItr = intenSortedPeakPreData.rbegin();
                    relativeIntensity < ticCutoffPercentage && r_iItr != intenSortedPeakPreData.rend();
                    ++r_iItr )
                {
                    //cout << relativeIntensity << " / " << totalIonCurrent << endl;
                    relativeIntensity += r_iItr->first / totalIonCurrent; // add current peak's relative intensity to the sum
                }

                if( r_iItr == intenSortedPeakPreData.rend() )
                    --r_iItr;

                peakPreData.clear();

                for(	IntenSortedPeakPreData::iterator iItr = intenSortedPeakPreData.lower_bound( r_iItr->first );
                    iItr != intenSortedPeakPreData.end();
                    ++iItr )
                {
                    PeakPreData::iterator itr = peakPreData.insert( make_pair( iItr->second, iItr->second ) ).first;
                    itr->second = iItr->first;
                }
            }
        }

        void FilterByPeakCount( size_t maxPeakCount )
        {
            if( !peakPreData.empty() )
            {
                // Sort peak list in descending order of intensity
                // Use a multimap because multiple peaks can have the same intensity.
                typedef multimap< double, double > IntenSortedPeakPreData;
                IntenSortedPeakPreData intenSortedPeakPreData;
                for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
                {
                    IntenSortedPeakPreData::iterator iItr = intenSortedPeakPreData.insert( make_pair( itr->second, itr->second ) );
                    iItr->second = itr->first;
                }

                peakPreData.clear();

                size_t peakCount = 0;
                for(	IntenSortedPeakPreData::reverse_iterator r_iItr = intenSortedPeakPreData.rbegin();
                    r_iItr != intenSortedPeakPreData.rend() && peakCount < maxPeakCount;
                    ++r_iItr, ++peakCount )
                {
                    PeakPreData::iterator itr = peakPreData.insert( PeakPreData::value_type( r_iItr->second, r_iItr->second ) ).first;
                    itr->second = r_iItr->first;
                }
            }
        }

        void preprocessSpectrum(float TICCutoff = 1.0f, size_t maxPeakCount = 150)
        {
            double parentIonEraseWindow = 3.0;
            if( !peakPreData.empty() )
            {
                double maxPeakMass = libraryMass + PROTON + parentIonEraseWindow;
                PeakPreData::iterator itr = peakPreData.upper_bound( maxPeakMass );
                peakPreData.erase( itr, peakPreData.end() );
            }

            BOOST_FOREACH(double parentIon, getPrecursorIons())
            {
                PeakPreData::iterator begin = peakPreData.lower_bound(parentIon - parentIonEraseWindow);
                PeakPreData::iterator end = peakPreData.upper_bound(parentIon + parentIonEraseWindow);
                while(begin != end)
                    peakPreData.erase(begin++);
            }    

            FilterByTIC(TICCutoff);
            FilterByPeakCount(maxPeakCount);

            // Sort the peaks by intensity
            double TIC = 0.0;
            typedef multimap< double, double > IntenSortedPeakPreData;
            IntenSortedPeakPreData intenSortedPeakPreData;
            for( PeakPreData::iterator itr = peakPreData.begin(); itr != peakPreData.end(); ++itr )
            {
                IntenSortedPeakPreData::iterator iItr = intenSortedPeakPreData.insert( make_pair( itr->second, itr->second ) );
                iItr->second = itr->first;
                TIC += itr->second;
            }

            peakPreData.clear();
            peakData.clear();
            
            IntenSortedPeakPreData::reverse_iterator iItr = intenSortedPeakPreData.rbegin();
            double prevPeakInten = iItr->first;
            int prevPeakRank = 1;
            for( ; iItr != intenSortedPeakPreData.rend(); ++iItr )
            {
                double mz = iItr->second;
                double inten = iItr->first;
                peakData[ mz ].rawIntensity = inten;
                peakData[ mz ].normIntensity = inten/TIC;
                
                if(inten != prevPeakInten)
                {
                    ++prevPeakRank; 
                    prevPeakInten = inten;
                }
                peakData[ mz ].intensityRank = prevPeakRank;
            }
        }

        set<double> getPrecursorIons()
        {
            set<double> precursorIons;
            double precursorMZ = (libraryMass+id.charge*PROTON)/(double) id.charge;
            // Water, double water, and ammonia loss
            precursorIons.insert(precursorMZ - WATER_MONO/id.charge);
            precursorIons.insert(precursorMZ - 2.0*WATER_MONO/id.charge);
            precursorIons.insert(precursorMZ - AMMONIA_MONO/id.charge);
            precursorIons.insert(precursorMZ);
            return precursorIons;
        }
    };

    struct XHunterSpectrum : public virtual BaseLibrarySpectrum 
    {

        XHunterSpectrum() : BaseLibrarySpectrum(){}

        XHunterSpectrum( const XHunterSpectrum& old ) : BaseLibrarySpectrum( old ) {}

        ~XHunterSpectrum()
        {
            BaseLibrarySpectrum::clearSpectrum();
        }

        void readPeaks(ifstream & library, bool ignoreIsotopesAndUnannotated = false)
        {
            library.seekg(0,ios::beg);
            library.seekg(peakPreDataIndex);
            string input;
            peakPreData.clear();
            while(getline(library, input))
            {
                if(input.length() > 2 && isdigit(input[0]))
                {
                    tokenizer parser(input, delim);
                    tokenizer::iterator itr = parser.begin();
                    string attribute = *(itr);
                    string value = *(++itr);
                    float peakMass = lexical_cast<float>(attribute);
                    float intensity = lexical_cast<float>(value);
                    peakPreData.insert(pair<float,float>(peakMass,intensity));
                } else if(boost::starts_with(input,"END"))
                {
                    break;
                }
            }
        }

        void clearHeader()
        {
            BaseLibrarySpectrum::clearHeader();
        }

        void clearSpectrum()
        {
            BaseLibrarySpectrum::clearSpectrum();
        }

        void readHeader(ifstream & library)
        {
            library.seekg(0,ios::beg);
            library.seekg(headerIndex);
            string input;
            matchedProteins.clear();
            string peptide;
            ModMap mods;
            while(getline(library, input))
            {
                if(boost::starts_with(input,"PEPSEQ"))
                {
                    tokenizer parser(input, delim);
                    tokenizer::iterator itr = parser.begin();
                    string attribute = *(itr);
                    string value = *(++itr);
                    peptide = value;
                } else if(boost::starts_with(input,"PEPMOD"))
                {
                    tokenizer parser(input, delim);
                    tokenizer::iterator itr = parser.begin();
                    string attribute = *(itr);
                    string value = *(++itr);
                    tokenizer modsParser(value, modsDelim);
                    tokenizer::iterator modsIter = modsParser.begin();
                    double mass = lexical_cast<double>(*(modsIter));
                    int location = lexical_cast<int>(*(++modsIter));
                    mods.insert(pair<int,double>(location,mass));
                } else if(boost::starts_with(input,"PEPACC"))
                {
                    tokenizer parser(input, delim);
                    tokenizer::iterator itr = parser.begin();
                    string attribute = *(itr);
                    string value = *(++itr);
                    tokenizer protParser(value, modsDelim);
                    tokenizer::iterator protIter = protParser.begin();
                    string protAcc = *(protIter);
                    size_t location = lexical_cast<size_t>(*(++protIter));
                    matchedProteins.insert(pair<string,size_t>(protAcc,location));
                } else if(input.length()>1 && isdigit(input[0]))
                {
                    matchedPeptide.reset(new DigestedPeptide(peptide));
                    ModificationMap& modMap = matchedPeptide->modifications();
                    for(ModMap::const_iterator modIter = mods.begin(); modIter != mods.end(); ++modIter)
                    {
                        char aa = matchedPeptide->sequence()[(*modIter).first-1];
                        DynamicMod mod(aa,aa,(*modIter).second);
                        modMap.insert(make_pair<int,DynamicMod>((*modIter).first-1,mod));
                    }
                    break;
                }
            }
        }

        void readSpectrum(float TICCutoff = 1.0f, size_t maxPeakCount = 150, bool cleanSpectrum = false)
        {
            ifstream library(id.source.c_str(), ios::in);
            readPeaks(library, cleanSpectrum);
            readHeader(library);
            preprocessSpectrum(TICCutoff, maxPeakCount);
        }
    };

    struct SpectraSTSpectrum : public virtual BaseLibrarySpectrum {

        size_t numPeaks;

        SpectraSTSpectrum() : BaseLibrarySpectrum() { numPeaks = 0;}

        SpectraSTSpectrum( const SpectraSTSpectrum& old ) : BaseLibrarySpectrum( old ) { numPeaks = old.numPeaks; }

        ~SpectraSTSpectrum()
        {
            BaseLibrarySpectrum::clearSpectrum();
        }

        void readPeaks(ifstream & library, bool cleanSpectrum = false)
        {
            BaseLibrarySpectrum::readPeaks(library, cleanSpectrum);
        }

        void clearHeader()
        {
            BaseLibrarySpectrum::clearHeader();
        }

        void clearSpectrum()
        {
            BaseLibrarySpectrum::clearSpectrum();
        }

        void readHeader(ifstream & library)
        {
            //cout << headerIndex << "," << peakPreDataIndex << endl;
            library.seekg(0,ios::beg);
            library.seekg(headerIndex);
            string input;
            matchedProteins.clear();
            ModMap mods;

            // First parse out the peptide FullName: -.n[43]AASC[160]VLLHTGQK.M/2
            getline(library,input);
            //cout << input << endl;
            string::size_type pepStart = input.find(".");
            string::size_type pepEnd = input.find(".",pepStart+1);
            string peptide = input.substr(pepStart+1, pepEnd-pepStart-1);
            if(peptide[0]=='n')
                peptide.erase(0,1);

            while(peptide.find("[") != string::npos)
            {
                string::size_type startPos = peptide.find("[");
                string::size_type endPos = peptide.find("]");
                peptide.erase(startPos, endPos-startPos+1);
            }

            //cout << peptide << endl;
            if(peptide.length() == 0)
                throw "Failed to parse header entry for the spectrum";

            // Get the "Comment:" line. See below for an example string (all in one line). Yuck!
            /* Comment: Spec=Consensus Pep=Tryptic/miss_bad_unconfirmed Fullname=-.AAAAAAGAGPEM(O)VRGQVFDVGPR.Y/3 \
            Mods=2/0,A,Acetyl/11,M,Oxidation Parent=752.712 Inst=qtof Mz_diff=0.002 Mz_exact=752.7117
            Mz_av=753.182 Protein="tr|Q1HBJ4|Q1HBJ4_HUMAN Mitogen-activated protein kinase 1 
            [Homo sapiens]" Pseq=38 Organism="human" Se=3^X2:ex=0.0001057/0.0001043,td=0/0,sd=0/0,
            hs=54.2/3.6,bs=1.4e-006,b2=0.00021^O2:ex=2.67192e-008/2.668e-008,td=4.435e+010/4.435e+010,
            pr=9.0156e-012/8.984e-012,bs=3.83e-011,b2=5.34e-008,bd=898^P2:sc=25.7/2.2,dc=15.6/2.2,
            ps=2.98/0.33,bs=0 Sample=1/mpi_a459_cam,2,2 Nreps=2/2 Missing=0.3298/0.0646 
            Parent_med=752.7136/0.00 Max2med_orig=53.6/12.4 Dotfull=0.796/0.012 Dot_cons=0.842/0.005
            Unassign_all=0.096 Unassigned=0.026 Dotbest=0.85 Flags=0,1,0 Naa=23 DUScorr=3.4/2.1/4.2 
            Dottheory=0.82 Pfin=2.4e+022 Probcorr=0.001 Tfratio=2.2e+012 Pfract=0 */
            getline(library, input);
            if(istarts_with(input,"Comment: "))
                erase_head(input,9);

            string modStr;
            string nmcStr;
            string nttStr;
            typedef vector<string> SplitVec;
            SplitVec splitVec;
            split(splitVec, input, is_any_of("= "));
            for(size_t index=0; index < splitVec.size(); ++index)
            {
                if(iequals(splitVec[index],"mods") && modStr.length()==0 )
                    modStr = splitVec[++index];
                else if(iequals(splitVec[index],"nmc") && nmcStr.length() ==0 )
                    nmcStr = splitVec[++index];
                else if(iequals(splitVec[index],"ntt") && nttStr.length() == 0 )
                    nttStr = splitVec[++index];
            }

            if(nmcStr.length()>0)
                numMissedCleavages = lexical_cast<size_t>(nmcStr);
            if(nttStr.length()>0)
                NTT = lexical_cast<int>(nttStr);

            if(NTT == 1)
                matchedPeptide.reset(new DigestedPeptide(peptide.begin(), peptide.end(), 0, numMissedCleavages, false, true));
            else if(NTT == 0)
                matchedPeptide.reset(new DigestedPeptide(peptide.begin(), peptide.end(), 0, numMissedCleavages, false, false));
            else
                matchedPeptide.reset(new DigestedPeptide(peptide.begin(), peptide.end(), 0, numMissedCleavages, true, true));
            //cout << input << endl;
            //cout << modStr << "," << nmcStr << "," << nttStr << endl;
            //cout << getInterpretation(*matchedPeptide) << "," << peptide << endl;

            // Parse out the modifications [2/0,A,Acetyl/11,M,Oxidation]
            if(modStr != "0")
            {
                ModificationMap& modMap = matchedPeptide->modifications();

                tokenizer modsParser(modStr, backslash);
                tokenizer::iterator modsItr = modsParser.begin();
                ++modsItr;
                while(modsItr != modsParser.end())
                {
                    string mod = *(modsItr);
                    tokenizer modParser(mod, comma);
                    tokenizer::iterator modItr = modParser.begin();
                    int position = lexical_cast<int>(*(modItr));
                    ++modItr;
                    string modName = *(++modItr);
                    double modMass = ModNamesToMasses[modName];
                    if(fabs(modMass) > 0)
                    {
                        if(position < 0)
                        {
                            DynamicMod mod('(','(',modMass);
                            modMap.insert(make_pair<int,DynamicMod>(modMap.NTerminus(),mod));
                        } 
                        else
                        {
                            char aa = matchedPeptide->sequence().at(position);
                            DynamicMod mod(aa,aa,modMass);
                            modMap.insert(make_pair<int,DynamicMod>(position,mod));
                        }
                        //cout << position << "," << modName << "," << aa << "," << modMass << endl;
                    }
                    ++modsItr;
                }
            }

            // Parse out the protein
            string::size_type proteinStart = input.find(" Protein=");
            string::size_type proteinEnd = input.find(" ",proteinStart+1);
            string proteinAnn = input.substr(proteinStart + 9, proteinEnd - proteinStart - 9);
            //cout << proteinStart << "," << proteinEnd << "," << proteinAnn << endl;
            if(proteinAnn.find("/") == string::npos)
            {
                matchedProteins.insert(pair<string,size_t>(proteinAnn,0));
                return;
            }
            
            tokenizer protParser(proteinAnn, backslash);
            tokenizer::iterator protsItr = protParser.begin();
            ++protsItr;
            while(protsItr != protParser.end())
            {
                string proteinStr = *(protsItr);
                if(proteinStr.find(",") != string::npos)
                {
                    tokenizer splitProtStr(proteinStr,comma);
                    tokenizer::iterator splitItr = splitProtStr.begin();
                    string proteinAcc = *(splitItr);
                    size_t pos = lexical_cast<size_t>(*(++splitItr));
                    matchedProteins.insert(make_pair(proteinAcc,pos));
                }
                else
                    matchedProteins.insert(pair<string,size_t>(proteinStr,0));
                ++protsItr;
            }
        }

        void readSpectrum(float TICCutoff = 1.0f, size_t maxPeakCount = 150, bool cleanSpectrum = false)
        {
            ifstream library(id.source.c_str(), ios::in);
            readPeaks(library, cleanSpectrum);
            readHeader(library);
            preprocessSpectrum(TICCutoff, maxPeakCount);
        }
    };

    struct NISTSpectrum : public virtual BaseLibrarySpectrum {

        size_t numPeaks;

        NISTSpectrum() : BaseLibrarySpectrum() { numPeaks = 0;}

        NISTSpectrum( const NISTSpectrum& old ) : BaseLibrarySpectrum( old ) { numPeaks = old.numPeaks; }

        ~NISTSpectrum()
        {
            BaseLibrarySpectrum::clearSpectrum();
        }

        void readPeaks(ifstream & library, bool cleanSpectrum = false)
        {
            BaseLibrarySpectrum::readPeaks(library, cleanSpectrum);
        }

        void clearHeader()
        {
            BaseLibrarySpectrum::clearHeader();
        }

        void clearSpectrum()
        {
            BaseLibrarySpectrum::clearSpectrum();
        }

        void readHeader(ifstream & library)
        {
            library.seekg(0,ios::beg);
            library.seekg(headerIndex);
            string input;
            matchedProteins.clear();
            ModMap mods;

            // Get the "Comment:" line. See below for an example string (all in one line). Yuck!
            /* Comment: Spec=Consensus Pep=Tryptic/miss_bad_unconfirmed Fullname=-.AAAAAAGAGPEM(O)VRGQVFDVGPR.Y/3 \
            Mods=2/0,A,Acetyl/11,M,Oxidation Parent=752.712 Inst=qtof Mz_diff=0.002 Mz_exact=752.7117
            Mz_av=753.182 Protein="tr|Q1HBJ4|Q1HBJ4_HUMAN Mitogen-activated protein kinase 1 
            [Homo sapiens]" Pseq=38 Organism="human" Se=3^X2:ex=0.0001057/0.0001043,td=0/0,sd=0/0,
            hs=54.2/3.6,bs=1.4e-006,b2=0.00021^O2:ex=2.67192e-008/2.668e-008,td=4.435e+010/4.435e+010,
            pr=9.0156e-012/8.984e-012,bs=3.83e-011,b2=5.34e-008,bd=898^P2:sc=25.7/2.2,dc=15.6/2.2,
            ps=2.98/0.33,bs=0 Sample=1/mpi_a459_cam,2,2 Nreps=2/2 Missing=0.3298/0.0646 
            Parent_med=752.7136/0.00 Max2med_orig=53.6/12.4 Dotfull=0.796/0.012 Dot_cons=0.842/0.005
            Unassign_all=0.096 Unassigned=0.026 Dotbest=0.85 Flags=0,1,0 Naa=23 DUScorr=3.4/2.1/4.2 
            Dottheory=0.82 Pfin=2.4e+022 Probcorr=0.001 Tfratio=2.2e+012 Pfract=0 */

            getline(library, input);
            // Get the spectrum type, interact style peptide sequence, and the modification string
            //cout << input << endl;
            size_t specStart = input.find("Spec=");
            size_t specEnd = input.find("Pep=");
            size_t pepStart = input.find("Fullname=");
            size_t pepEnd = input.find("Mods=");
            size_t modsEnd = input.find("Parent=");

            string specType;
            if(specStart != input.npos && specEnd != input.npos)
                specType = input.substr(specStart + 5, specEnd - specStart - 6);
            string interactSeq;
            if(pepStart != input.npos && pepEnd != input.npos)
                interactSeq = input.substr(pepStart + 9, pepEnd - pepStart - 10);
            string modStr;
            if(pepEnd != input.npos && modsEnd != input.npos)
                modStr = input.substr(pepEnd + 5, modsEnd - pepEnd - 6);

            //cout << specType << "," << interactSeq << "," << modStr << endl;
            if(interactSeq.length() == 0)
                throw "Failed to parse header entry for the spectrum";

            // Parse out the peptide string from the interact style sequence [-.AAAAAAGAGPEM(O)VRGQVFDVGPR.Y/3]
            tokenizer peptideParser(interactSeq, dot);
            tokenizer::iterator pepItr = peptideParser.begin();
            ++pepItr;
            string peptide = *(pepItr);
            while(peptide.find("(") != string::npos)
            {
                string::size_type startPos = peptide.find("(");
                string::size_type endPos = peptide.find(")");
                peptide.erase(startPos, endPos-startPos+1);
            }
            matchedPeptide.reset(new DigestedPeptide(peptide));
            //cout << getInterpretation(*matchedPeptide) << "," << peptide << endl;

            // Parse out the modifications [2/0,A,Acetyl/11,M,Oxidation]
            if(modStr != "0")
            {
                ModificationMap& modMap = matchedPeptide->modifications();

                tokenizer modsParser(modStr, backslash);
                tokenizer::iterator modsItr = modsParser.begin();
                ++modsItr;
                while(modsItr != modsParser.end())
                {
                    string mod = *(modsItr);
                    tokenizer modParser(mod, comma);
                    tokenizer::iterator modItr = modParser.begin();
                    int position = lexical_cast<int>(*(modItr));
                    ++modItr;
                    string modName = *(++modItr);
                    double modMass = ModNamesToMasses[modName];
                    if(fabs(modMass) > 0)
                    {
                        if(position < 0)
                        {
                            DynamicMod mod('(','(',modMass);
                            modMap.insert(make_pair<int,DynamicMod>(modMap.NTerminus(),mod));
                        } 
                        else
                        {
                            char aa = matchedPeptide->sequence().at(position);
                            DynamicMod mod(aa,aa,modMass);
                            modMap.insert(make_pair<int,DynamicMod>(position,mod));
                        }
                        //cout << position << "," << modName << "," << aa << "," << modMass << endl;
                    }
                    ++modsItr;
                }
            }

            // Parse out the protein
            size_t proteinStart = input.find("Protein=");
            size_t proteinEnd = input.find("Pseq=");
            //cout << proteinStart << "," << proteinEnd << endl;
            string proteinAnn = input.substr(proteinStart + 8, proteinEnd - proteinStart - 9);
            proteinAnn = proteinAnn.substr(1,proteinAnn.length()-2);
            size_t pos = proteinAnn.find(" ");
            string proteinAcc = proteinAnn;
            if(pos != proteinAnn.npos)
                proteinAcc = proteinAnn.substr(0,proteinAnn.find(" "));
            matchedProteins.insert(pair<string,size_t>(proteinAcc,0));
        }

        void readSpectrum(float TICCutoff = 1.0f, size_t maxPeakCount = 150, bool cleanSpectrum = false)
        {
            ifstream library(id.source.c_str(), ios::in);
            readPeaks(library, cleanSpectrum);
            readHeader(library);
            preprocessSpectrum(TICCutoff, maxPeakCount);
        }
    };

    struct SpectraStore : public vector< shared_ptr<BaseLibrarySpectrum> >
    {
        string      libraryName;

        SpectraStore() { }

        ~SpectraStore()
        {
            clear();
        }

        void random_shuffle()
        {
            std::random_shuffle( begin(), end() );
        }

        void loadLibrary(const string& libName)
        {
            libraryName = libName;
            if(hasEnding(libraryName,".mgf"))
                loadXHunterLibraryFromMGF();
            else if (hasEnding(libraryName,".hlf"))
                loadXHunterLibraryFromBin();
            else if(hasEnding(libraryName,".msp"))
                loadNISTLibraryFromMSP();
            else if(hasEnding(libraryName,".sptxt"))
                loadSpectraSTLibraryFromSptxt();
        }

        void loadSpectraSTLibraryFromSptxt()
        {
            cout << "Reading \"" << libraryName << "\"" << endl;
            Timer libReadTime(true);
            ifstream library(libraryName.c_str(), ios::in);
            size_t spectrumIndex = 0;
            if(library)
            {
                string input;
                double parentMass;
                int charge;
                unsigned long headerIndex = 0;
                unsigned long peakIndex = 0;
                size_t readBytes = 0;
                size_t numPeaks;
                while(getline(library,input)) 
                {
                    readBytes += input.length();
                    // Skip the comments
                    if( boost::starts_with(input, "#") )
                        continue;

                    if(input.length() < 2 && headerIndex != 0 && peakIndex != 0)
                    {
                        shared_ptr<SpectraSTSpectrum> spectrum(new SpectraSTSpectrum);
                        spectrum->id.charge = charge; 
                        spectrum->id.source = libraryName;
                        spectrum->id.index = spectrumIndex;
                        spectrum->libraryMass = (parentMass * charge) - (charge * 1.00727);
                        spectrum->peakPreDataIndex = peakIndex;
                        spectrum->headerIndex = headerIndex;
                        push_back(spectrum);
                        continue;
                    }

                    if(boost::starts_with(input, "Name:"))
                    {
                        tokenizer parser(input, colon);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        ++spectrumIndex;
                        if(!(spectrumIndex % 10000)) 
                            cout << spectrumIndex << ": " << readBytes << '\r' << flush;
                        tokenizer splitter(value, backslash);
                        tokenizer::iterator pItr = splitter.begin();
                        ++pItr;
                        charge = lexical_cast<int>(*pItr);
                    } else if(boost::starts_with(input, "NumPeaks"))
                    {
                        tokenizer parser(input, colon);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        numPeaks = lexical_cast<size_t>(value);
                    } else if(boost::starts_with(input, "PrecursorMZ"))
                    {
                        tokenizer parser(input, colon);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        parentMass = lexical_cast<double>(value);
                    } else if(boost::starts_with(input, "FullName"))
                    {
                        headerIndex = ((unsigned long) library.tellg())-((unsigned long)input.length()+1) ;
                        peakIndex = 0;
                    } else if(input.length() > 2 && isdigit(input[0]) && peakIndex == 0)
                    {
                        peakIndex = ((unsigned long) library.tellg())-((unsigned long)input.length()+1);
                    }
                }
            }
            cout << "Read " << (spectrumIndex+1) << " spectra from library; " << libReadTime.End() << " seconds elapsed." << endl;
        }

        void loadNISTLibraryFromMSP()
        {
            cout << "Reading \"" << libraryName << "\"" << endl;
            Timer libReadTime(true);
            ifstream library(libraryName.c_str(), ios::in);
            size_t spectrumIndex = 0;
            if(library)
            {
                string input;
                double parentMass;
                int charge;
                unsigned long headerIndex = 0;
                unsigned long peakIndex = 0;
                size_t readBytes = 0;
                size_t numPeaks;
                while(getline(library,input)) 
                {
                    readBytes += input.length();
                    // Skip the comments
                    if( boost::starts_with(input, "#") )
                        continue;

                    if(input.length() < 2 && headerIndex != 0 && peakIndex != 0)
                    {
                        shared_ptr<NISTSpectrum> spectrum(new NISTSpectrum);
                        spectrum->id.charge = charge; 
                        spectrum->id.source = libraryName;
                        spectrum->id.index = spectrumIndex;
                        spectrum->libraryMass = (parentMass * charge) - (charge * 1.00727);
                        spectrum->peakPreDataIndex = peakIndex;
                        spectrum->headerIndex = headerIndex;
                        push_back(spectrum);
                        continue;
                    }

                    if(boost::starts_with(input, "Name:"))
                    {
                        tokenizer parser(input, colon);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        ++spectrumIndex;
                        if(!(spectrumIndex % 10000)) 
                            cout << spectrumIndex << ": " << readBytes << '\r' << flush;
                        tokenizer splitter(value, backslash);
                        tokenizer::iterator pItr = splitter.begin();
                        ++pItr;
                        charge = lexical_cast<int>(*pItr);
                    } else if(boost::starts_with(input, "Num peaks"))
                    {
                        tokenizer parser(input, colon);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        numPeaks = lexical_cast<size_t>(value);
                    } else if(boost::starts_with(input, "Comment"))
                    {
                        headerIndex = ((unsigned long) library.tellg())-((unsigned long) input.length()+1) ;
                        peakIndex = 0;
                        // Parse out the parent mass
                        size_t pStart = input.find("Parent=");
                        size_t pEnd = input.find("Inst=");
                        parentMass = lexical_cast<double>(input.substr(pStart + 7, pEnd-pStart-8));
                    } else if (input.length() > 2 && isdigit(input[0]) && peakIndex == 0)
                    {
                        peakIndex = ((unsigned long) library.tellg())-((unsigned long)input.length()+1);
                    }
                }
            }
            cout << "Read " << (spectrumIndex+1) << " spectra from library; " << libReadTime.End() << " seconds elapsed." << endl;
        }

        void loadXHunterLibraryFromBin() 
        {

            cout << "Reading \"" << libraryName << "\"" << endl;
            Timer libReadTime(true);
            ifstream library(libraryName.c_str(), ios::in);
            int spectrumIndex = 0;
            if(library)
            {
                string input;
                double MHPlusMass;
                int charge;
                unsigned long headerIndex = 0;
                unsigned long peakIndex = 0;
                size_t readBytes = 0;

                while(getline(library,input)) 
                {
                    readBytes += input.length();
                    if(boost::starts_with(input, "PEPMASS"))
                    {
                        tokenizer parser(input, delim);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        MHPlusMass = lexical_cast<double>(value);
                    } else if(boost::starts_with(input, "CHARGE"))
                    {
                        tokenizer parser(input, delim);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        charge = lexical_cast<int>(value);
                    } else if(boost::starts_with(input, "END"))
                    {
                        shared_ptr<XHunterSpectrum> spectrum(new XHunterSpectrum);
                        spectrum->id.charge = charge; 
                        spectrum->id.source = libraryName;
                        spectrum->id.index = spectrumIndex;
                        spectrum->libraryMass = MHPlusMass - 1.00727;
                        spectrum->peakPreDataIndex = peakIndex;
                        spectrum->headerIndex = headerIndex;
                        push_back(spectrum);
                    } else if(boost::starts_with(input,"BEGIN"))
                    {
                        ++spectrumIndex;
                        headerIndex = library.tellg();
                        peakIndex = 0;
                        if(!(spectrumIndex % 10000)) 
                            cout << spectrumIndex << ": " << readBytes << '\r' << flush;
                    } else if(input.length() > 2 && isdigit(input[0]) && peakIndex == 0)
                    {
                        peakIndex = ((unsigned long) library.tellg())-((unsigned long)input.length()+1);
                    }
                }
            }
            cout << "Read " << (spectrumIndex+1) << " spectra from library; " << libReadTime.End() << " seconds elapsed." << endl;
        }

        void loadXHunterLibraryFromMGF()
        {

            cout << "Reading \"" << libraryName << "\"" << endl;
            Timer libReadTime(true);
            ifstream library(libraryName.c_str(), ios::in);
            int spectrumIndex = 0;
            if(library)
            {
                string input;
                double MHPlusMass;
                int charge;
                unsigned long headerIndex = 0;
                unsigned long peakIndex = 0;
                size_t readBytes = 0;

                while(getline(library,input)) 
                {
                    readBytes += input.length();
                    if(boost::starts_with(input, "PEPMASS"))
                    {
                        tokenizer parser(input, delim);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        MHPlusMass = lexical_cast<double>(value);
                    } else if(boost::starts_with(input, "CHARGE"))
                    {
                        tokenizer parser(input, delim);
                        tokenizer::iterator itr = parser.begin();
                        string attribute = *(itr);
                        string value = *(++itr);
                        charge = lexical_cast<int>(value);
                    } else if(boost::starts_with(input, "END"))
                    {
                        shared_ptr<XHunterSpectrum> spectrum(new XHunterSpectrum);
                        spectrum->id.charge = charge; 
                        spectrum->id.source = libraryName;
                        spectrum->id.index = spectrumIndex;
                        spectrum->libraryMass = MHPlusMass - 1.00727;
                        spectrum->peakPreDataIndex = peakIndex;
                        spectrum->headerIndex = headerIndex;
                        push_back(spectrum);
                    } else if(boost::starts_with(input,"BEGIN"))
                    {
                        ++spectrumIndex;
                        headerIndex = library.tellg();
                        peakIndex = 0;
                        if(!(spectrumIndex % 10000)) 
                            cout << spectrumIndex << ": " << readBytes << '\r' << flush;
                    } else if(input.length() > 2 && isdigit(input[0]) && peakIndex == 0)
                    {
                        peakIndex = ((unsigned long) library.tellg())-((unsigned long)input.length()+1);
                    }
                }
            }
            cout << "Read " << (spectrumIndex+1) << " spectra from library; " << libReadTime.End() << " seconds elapsed." << endl;
        }

        void readSpectra(float TICCutoff = 1.0f, size_t maxPeakCount = 100, bool cleanSpectrum = false)
        {
            ifstream library(libraryName.c_str(), ios::in);
            for(vector<shared_ptr<BaseLibrarySpectrum> >::iterator sItr = begin(); sItr != end(); ++sItr )
                (*sItr)->readSpectrum(TICCutoff,maxPeakCount,cleanSpectrum);
        }

        void recalculatePrecursorMasses()
        {
            ifstream library(libraryName.c_str(), ios::in);
            size_t spectraIndex = 0;
            size_t totalSpectra = size();
            for(vector<shared_ptr<BaseLibrarySpectrum> >::iterator sItr = begin(); sItr != end(); ++sItr )
            {
                ++spectraIndex;
                (*sItr)->readHeader(library);
                (*sItr)->averageMass = (*sItr)->matchedPeptide->molecularWeight();
                (*sItr)->monoisotopicMass = (*sItr)->matchedPeptide->monoisotopicMass();
                (*sItr)->matchedPeptide.reset();
                if(!(spectraIndex % 10000))
                    cout << totalSpectra << ": " << spectraIndex << '\r' << flush;
            }
        }

        void printSpectra()
        {
            for(vector<shared_ptr<BaseLibrarySpectrum> >::const_iterator sItr = begin(); sItr != end(); ++sItr )
            {
                cout << (*sItr)->id.source << "," << (*sItr)->id.charge << "," << (*sItr)->id.index << endl;
                cout << (*sItr)->libraryMass << endl;
                cout << (*sItr)->matchedPeptide->sequence() << "->" << getInterpretation(*((*sItr)->matchedPeptide)) << endl;
                cout << (*sItr)->numMissedCleavages << "," << (*sItr)->NTT << endl;
                for(PeakPreData::const_iterator pItr = (*sItr)->peakPreData.begin(); pItr != (*sItr)->peakPreData.end(); ++pItr)
                    cout << (*pItr).first << "," << (*pItr).second << endl;
                for(ProteinMap::const_iterator pItr = (*sItr)->matchedProteins.begin(); pItr != (*sItr)->matchedProteins.end(); ++pItr)
                    cout << (*pItr).first << "," << (*pItr).second << endl;
            }
        }

        static bool hasEnding (std::string const &fullString, std::string const &ending)
        {
            size_t lastMatchPos = fullString.rfind(ending); // Find the last occurrence of ending
            bool isEnding = lastMatchPos != std::string::npos; // Make sure it's found at least once

            // If the string was found, make sure that any characters that follow it are the ones we're trying to ignore
            for( size_t i = lastMatchPos + ending.length(); (i < fullString.length()) && isEnding; i++)
                if( (fullString[i] != '\n') && (fullString[i] != '\r') )
                    isEnding = false;
            return isEnding;
        }

    };
}
}
#endif
