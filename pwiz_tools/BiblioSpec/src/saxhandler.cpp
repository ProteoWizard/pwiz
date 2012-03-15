/*
  Copyright (c) 2011, University of Washington
  All rights reserved.

  Redistribution and use in source and binary forms, with or without
  modification, are permitted provided that the following conditions
  are met:

    * Redistributions of source code must retain the above copyright
    notice, this list of conditions and the following disclaimer. 
    * Redistributions in binary form must reproduce the above
    copyright notice, this list of conditions and the following
    disclaimer in the documentation and/or other materials provided
    with the distribution. 
    * Neither the name of the <ORGANIZATION> nor the names of its
    contributors may be used to endorse or promote products derived
    from this software without specific prior written permission.

  THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
  "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
  LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS
  FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE
  COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT,
  INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING,
  BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
  LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
  CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
  LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN
  ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE
  POSSIBILITY OF SUCH DAMAGE.
*/
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
