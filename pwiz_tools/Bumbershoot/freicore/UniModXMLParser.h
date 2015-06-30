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
// The Initial Developer of the Original Code is Surendra Dasari.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s):
//

#ifndef _UNIMODXMLPARSER_H
#define _UNIMODXMLPARSER_H

#include "shared_defs.h"
#include "shared_funcs.h"
#include <limits>
#include "expat_xml.h"

/* Inline defs to get attribute names using expat libraries. */
#define HAS_ATTR(name) (paramIndex(name,atts,attsCount) > -1)
#define GET_ATTR_AS(name, type) getAttributeAs<type>(name,atts,attsCount)
#define GET_ATTR(name) GET_ATTR_AS(name,std::string)

namespace freicore {

    template< class T >
    T getAttributeAs( const string& name, const char** atts, int attsCount )
    {
        if( !HAS_ATTR(name) )
            throw out_of_range( "required attribute \"" + name + "\" not found" );
        try {
            return lexical_cast<T>( atts[paramIndex(name, atts, attsCount)+1] );
        } catch (std::exception&) {
            throw runtime_error( "Error parsing XML: attribute "+ name + " has non-standard value." );
        }
    }

    /**!
    ModificationSpecificity stores the site (amino acid), position,
    and classification of a modification (post-translational, co-
    translational etc.) from unimod schema.
    */
    struct ModificationSpecificity {

        string aminoAcid, position, classification;

        /**
        ModificationSpecificity constructor assigns the
        amino acid, position and classification of a modification.
        */
        ModificationSpecificity(string aa, string pos, string cls) {
            if(aa.compare("N-term")==0 || aa.compare("n-term")==0) {
                aminoAcid="(";
            } else if(aa.compare("C-term")==0 || aa.compare("c-term")==0) {
                aminoAcid=")";
            } else {
                aminoAcid = aa;
            }
            position = pos;
            classification = cls;
        }

        // Overloading the << operator for printing
        friend ostream& operator <<(ostream& os, const ModificationSpecificity &strt) {
            os << strt.aminoAcid << "," << strt.position << "," << strt.classification;
            return os;
        }
    };

    /**
    class UnimodModification holds the title, full name, mono-isotopic mas,
    average mass, elemental comosition and specificity of a modification
    present in the unimod database. 
    See http://www.unimod.org/xmlns/schema/unimod_2/unimod_2.xsd for details
    of the schema.
    */

    class UnimodModification {

        // Title and full name of the modification
        string title, fullName;
        // Mass of the modification
        float monoIsotopicMass, averageMass;
        // Elemental composition of the modification
        string composition;
        // A vector of residue specificities of the modification
        vector< ModificationSpecificity > specificities;

    public:
        //Constructor and desctructors
        UnimodModification(){};
        UnimodModification(string titl, string name) : title(titl), fullName(name) {}
        ~UnimodModification(){};

        // Add a residue specificity to the modification
        void addASpecificity(string aminoAcid, string pos, string cls);
        // Set the elemental composition
        void setComposition(string comp);            
        // Set the masses
        void setModificationMasses(float monoMass, float avgMass);
        // Get total number of modification sites
        int getTotalNumberOfModSites() const;
        // Get the average mass
        float getAverageMass() const { return averageMass;};
        // Get the monoisotopic mass
        float getMonoisotopicMass() const { return monoIsotopicMass;};
        // Get the modification specificities.
        const vector<ModificationSpecificity>& getAminoAcidSpecificities() const { return specificities;};
        string getTitle() const { return title;};

        // Overload the << operator for printing purposes
        friend ostream& operator <<(ostream &os, const UnimodModification &obj) {
            os << obj.title << "->" << obj.fullName << endl;
            os << "\t" << obj.monoIsotopicMass << "," << obj.averageMass << endl;
            os << "\t" << obj.composition << endl;
            for(vector<ModificationSpecificity>::const_iterator iter = obj.specificities.begin(); iter != obj.specificities.end(); iter++) {
                os << "\t" << (*iter) << endl;
            }
            return os;
        }
    };

    /**
    class UniModXMLParser uses the expat XML parsing libraries to parse
    out the unimod XML file. The file contains annotations of known 
    protein modifications. 
    See http://www.unimod.org/modifications_list.php for the full list.
    */
    class UniModXMLParser {

        // Expat XML document parser
        XML_Parser documentParser;
        // Input stream for the parser
        ifstream inputStream;
        // A list of modifications that are parsed out from the unimod XML
        vector <UnimodModification> modifications;

        /**
        Function endElement is called when the parser sees an
        end tag. In this implementation, we listen for the end
        tag "umod:mod". This event signifies that we have just
        parsed out a modification and the mod needs to be added
        to the list.
        */
        static void endElement( void *userData, const char *name ) {

            // Get the parser being used
            UniModXMLParser* parserInstance = static_cast<UniModXMLParser*> (userData);
            // Get the current modification that was being parsed out
            UnimodModification* currentMod = &parserInstance->uniModModification;

            string tag(name);
            // If we have seen the end of the modification parsing then 
            // store the mod.
            if(tag == "umod:mod" && currentMod->getTotalNumberOfModSites()>0) {
                parserInstance->addModification(*currentMod);
            }
        }

        /**
        Function startElement is called when the parser sees a start tag.
        In this implementation, we listen to tags that are particular to
        a modification (like its name, mass, specificity etc) and update
        the current modification object that is being parsed.
        */
        static void startElement( void *userData, const char *name, const char **atts ) {

            // Get the parser instance.
            UniModXMLParser* parserInstance = static_cast<UniModXMLParser*> (userData);
            // Get a pointer to the current modification being parsed.
            UnimodModification* currentMod = &parserInstance->uniModModification;

            string tag(name);
            int attsCount = XML_GetSpecifiedAttributeCount( parserInstance->getDocumentParser() );

            try {
                // If we are at the start of a new mod.
                if(tag == "umod:mod") {
                    // Get the title and name
                    string title = GET_ATTR("title");
                    string fullName = GET_ATTR("full_name");
                    // Create a new mod and remember it as the
                    // current mod being parsed.
                    currentMod = new UnimodModification(title, fullName);
                    parserInstance->uniModModification = *currentMod;
                } else if(tag == "umod:specificity") {
                    // Parse out the specificity parameters of the mod
                    // and add it the list of the specificities
                    string aminoAcid = GET_ATTR("site");
                    string position = GET_ATTR("position");
                    string classification = GET_ATTR("classification");

                    if(currentMod != NULL) {
                        currentMod->addASpecificity(aminoAcid, position, classification);
                    }
                } else if(tag == "umod:delta") {
                    // Parse out the mass of the mod and update the
                    // mass parameters along with its composition
                    float monoMass = GET_ATTR_AS("mono_mass", float);
                    float avgMass = GET_ATTR_AS("avge_mass", float);
                    string comp = GET_ATTR("composition");

                    if(currentMod != NULL) {
                        currentMod->setModificationMasses(monoMass, avgMass);
                        currentMod->setComposition(comp);
                    }
                }
            } catch(exception& e) {
                throw runtime_error( string("[UniModParsing Error] Error parsing element ") + tag  + ":" + e.what());
            }
        }

    public:
        // Global variable to keep track of the current modification
        // being parsed
        UnimodModification uniModModification;
        // Constructors and desctructors
        UniModXMLParser(string filename);
        ~UniModXMLParser();

        // This function parses the document.
        void parseDocument();
        // Getter functions for member variables.
        vector<UnimodModification> getModifications() { return modifications;}
        XML_Parser getDocumentParser() { return documentParser;}
        // A function to add a modification to the list of the 
        // parsed out modifications
        void addModification(UnimodModification mod);

        // Overloading << for printing.
        friend ostream& operator <<(ostream &os, const UniModXMLParser &obj) {
            cout << "total Parsed Out Mods:" << obj.modifications.size() << endl;
            for(vector <UnimodModification>::const_iterator iter = obj.modifications.begin(); iter != obj.modifications.end(); iter++) {
                os << (*iter) << endl;
            }
            return os;
        }

        // Test case
        static void testUnimodParser(string filename) {
            UniModXMLParser* parser = new UniModXMLParser(filename);
            parser->parseDocument();
            cout << (*parser) << endl;
        }
    };
}

#endif
