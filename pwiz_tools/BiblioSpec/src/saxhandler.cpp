//
// $Id$
//
//
// Original author: Barbara Frewen <frewen@u.washington.edu>
//
// Copyright 2012 University of Washington - Seattle, WA 98195
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

#include "stdafx.h"

#include "saxhandler.h"

namespace BiblioSpec {

// Static callback handlers
static void startElementCallback(void *data, const XML_Char *el, const XML_Char **attr)
{
    ((SAXHandler*) data)->startElement(el, attr);
}

static void endElementCallback(void *data, const XML_Char *el)
{
    ((SAXHandler*) data)->endElement(el);
}

static void charactersCallback(void *data, const XML_Char *s, int len)
{
    ((SAXHandler*) data)->characters(s, len);
}


SAXHandler::SAXHandler()
{
    m_parser_ = XML_ParserCreate(NULL);
    XML_SetUserData(m_parser_, this);
    XML_SetElementHandler(m_parser_, startElementCallback, endElementCallback);
    XML_SetCharacterDataHandler(m_parser_, charactersCallback);
}


SAXHandler::~SAXHandler()
{
    XML_ParserFree(m_parser_);
}


void SAXHandler::startElement(const XML_Char *el, const XML_Char **attr)
{
}

void SAXHandler::endElement(const XML_Char *el)
{
}


void SAXHandler::characters(const XML_Char *s, int len)
{
}

bool SAXHandler::parse()
{
    FILE* pfIn = fopen(m_strFileName_.data(), "r");
    if (pfIn == NULL) {
        throw BlibException(true, "Failed to open input file '%s'.", 
                            m_strFileName_.c_str());
    }
    
    bool success = true;
    string message;
    
    try {
        // HACK!! I have no idea why this is, but without this string
        // declaration, the MSVC optimizer removes the catch block below.
        // Very mysterious.
        string temp;
        
        char buffer[8192];
        int readBytes = 0;
        
        while (success && (readBytes = (int) fread(buffer, 1, sizeof(buffer), pfIn)) != 0) {
            success = (XML_Parse(m_parser_, buffer, readBytes, false) != 0);
        }
        success = success && (XML_Parse(m_parser_, buffer, 0, true) != 0);
    }
    catch(string thrown_msg) { // from parsers
        message = thrown_msg;
        success = false;
    }
    catch(BlibException e) { // probably from BuildParser
        if( e.hasFilename() ){
            throw e;
        } else {
            message = e.what();
            success = false;
        }
    }
    catch(std::exception e) {  // other runtime errors (e.g. memory)
        message = e.what();
        success = false;
    }
    
    fclose(pfIn);
    
    if (!success) {
        XML_Error error = XML_GetErrorCode(m_parser_);
        int lineNum = XML_GetCurrentLineNumber(m_parser_);
        ostringstream stringBuilder(ostringstream::out);
        
        stringBuilder << m_strFileName_
                      << "(line " << lineNum
                      << "): " << message << flush;
        
        if (message.length() == 0) {
            switch (error) {
            case XML_ERROR_SYNTAX:
                stringBuilder << "Syntax error parsing XML.";
                break;
            case XML_ERROR_INVALID_TOKEN:
                stringBuilder << "Invalid token error parsing XML.";
                break;
            case XML_ERROR_UNCLOSED_TOKEN:
                stringBuilder << "Unclosed token error parsing XML.";
                break;
            case XML_ERROR_NO_ELEMENTS:
                stringBuilder << "No elements error parsing XML.";
                break;
            case XML_ERROR_TAG_MISMATCH:
                stringBuilder << "Tag mismatch error parsing XML.";
                break;
            case XML_ERROR_DUPLICATE_ATTRIBUTE:
                stringBuilder << "Duplicate attribute error parsing XML.";
                break;
            case XML_ERROR_UNKNOWN_ENCODING:
            case XML_ERROR_INCORRECT_ENCODING:
                stringBuilder << "Unknown or incorrect encoding XML error.";
                break;
            case XML_ERROR_UNCLOSED_CDATA_SECTION:
                stringBuilder << "Unclosed data section error parsing XML.";
                break;
                
            default:
                stringBuilder << "XML parsing error.";
                break;
            }
        }
        
        string er_msg = stringBuilder.str();
        throw BlibException(true, er_msg.c_str());
    }
    
    return true;
}

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
