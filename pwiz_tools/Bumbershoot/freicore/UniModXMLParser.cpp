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

#include "UniModXMLParser.h"

namespace freicore {

	/**
	Function addASpecificity adds a residue specificity to a 
	modification. For example, an Oxidation modification can
	have a specificity of M at any-where. The following types
	of modification specificities are allowed:
	1) Post-translational
	2) Artefactual
	3) Pre-translational
	4) Co-translational
	5) Amino acid substitution
	6) Multiple causes
	*/
	void UnimodModification::addASpecificity(string aminoAcid, string pos, string cls) {
		if(cls.compare("Post-translational")==0 || cls.compare("Artefact")==0 \
			|| cls.compare("Pre-translational")==0 || cls.compare("Co-translational")==0 \
			|| cls.compare("Multiple")==0 || cls.compare("AA substitution")==0) {
				specificities.push_back(ModificationSpecificity(aminoAcid, pos, cls));
		}
	}

	/// Sets the elemental composition
	void UnimodModification::setComposition(string comp) {
		composition = comp;
	}

	/// Sets the mono isotopic and average masses for modification
	void UnimodModification::setModificationMasses(float monoMass, float avgMass) {
		monoIsotopicMass = monoMass;
		averageMass = avgMass;
	}

	/// Get the total number of modification sites for a particular
	/// modification
	int UnimodModification::getTotalNumberOfModSites() const {
		return specificities.size();
	}

	/// Adds the modification to the list of the modifications parsed out
	/// by the XML parser.
	void UniModXMLParser::addModification(UnimodModification mod) {
		modifications.push_back(mod);
	}

	// Constructor
	UniModXMLParser::UniModXMLParser(string filename) {
		// Open a file stream
		inputStream.open( filename.c_str(), std::ios::binary );
		if( !inputStream.is_open() ) {
			throw invalid_argument( string( "unable to open pepXML file \"" ) + filename + "\"" );
		}

		inputStream.clear();
		// Create a parser, set the user data for the parser, and the
		// start and end tag parsing functions (Element Handlers).
		documentParser = XML_ParserCreate( NULL );
		XML_SetUserData( documentParser, this );
		XML_SetElementHandler( documentParser, startElement, endElement );
	}

	/// Obligatory desctructor
	UniModXMLParser::~UniModXMLParser() {
		inputStream.close();

		if( documentParser )
		{
			XML_ParserFree( documentParser );
			documentParser = NULL;
		}
	}

	/**
	Function parseDocument reads the XML document a chunk at a time
	and uses the start and end element handlers to parse out the
	modifications present in the unimod XML document
	*/
	void UniModXMLParser::parseDocument() {

		// Get a buffer
		size_t done, bytesRead;
		char* buffer = new char[READ_BUFFER_SIZE];

		do {
			// Fill the buffer using the XML document input
			// stream
			inputStream.read(buffer, READ_BUFFER_SIZE);
			bytesRead = inputStream.gcount();
			done = bytesRead < sizeof(buffer);

			// Parse out the data chunk
			try	{
				if( !XML_Parse( documentParser, buffer, bytesRead, done ) )	{
					throw runtime_error( XML_ErrorString( XML_GetErrorCode( documentParser ) ) );
				}
			} catch( exception& e )	{
				throw runtime_error( string( e.what() ) + " at line " + lexical_cast<string>( XML_GetCurrentLineNumber( documentParser ) ) );
			}
		} while( !done );

		// Clean up!!
		delete buffer;
	}
}
