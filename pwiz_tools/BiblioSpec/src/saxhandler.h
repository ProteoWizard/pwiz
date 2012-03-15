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
#if !defined(SAXHANDLER_H)
#define SAXHANDLER_H

#include <string>
#include <iostream>
#include <sstream>
#include "expat.h"
#include "Verbosity.h"
#include "BlibUtils.h"

using namespace std;

namespace BiblioSpec {

/**
* eXpat SAX parser wrapper.
*/
class SAXHandler
{
public:
    SAXHandler();
    virtual ~SAXHandler();

    /**
    * Receive notification of the start of an element.
    *
    * <p>By default, do nothing.  Application writers may override this
    * method in a subclass to take specific actions at the start of
    * each element (such as allocating a new tree node or writing
    * output to a file).</p>
    */
    virtual void startElement(const XML_Char *el, const XML_Char **attr);

    /**
    * Receive notification of the end of an element.
    *
    * <p>By default, do nothing.  Application writers may override this
    * method in a subclass to take specific actions at the end of
    * each element (such as finalising a tree node or writing
    * output to a file).</p>
    */
    virtual void endElement(const XML_Char *el);

    /**
    * Receive notification of character data inside an element.
    *
    * <p>By default, do nothing.  Application writers may override this
    * method to take specific actions for each chunk of character data
    * (such as adding the data to a node or buffer, or printing it to
    * a file).</p>
    */
    virtual void characters(const XML_Char *s, int len);

    /**
    * Open file and stream data to the SAX parser.  Must call
    * setFileName before calling this function.
    */
    bool parse();

    inline void setFileName(const char* fileName)
    {
        m_strFileName_ = fileName;
    }

    // Helper functions
    inline bool isElement(const char *n1, const XML_Char *n2)
    {    return (strcmp(n1, n2) == 0); }

    inline bool isAttr(const char *n1, const XML_Char *n2)
    {    return (strcmp(n1, n2) == 0); }

    inline const char* getAttrValue(const char* name, const XML_Char **attr)
    {
        for (int i = 0; attr[i]; i += 2) {
            if (isAttr(name, attr[i]))
                return attr[i + 1];
        }
        
        return "";
    }

    /**
     * Throws an error message including the line number if the
     * attribute is not there.
     */
    inline const char* getRequiredAttrValue(const char* name, 
                                            const XML_Char **attr)
    {
          const char* value = getAttrValue(name, attr);
          if( strcmp(value, "") != 0) {
            return value;
          }

          //we didn't find it
          throwParseError("Missing required attribute '%s'.", name);
          
          return "";
    }

    /**
     * Helper function for checking valid numbers.  Returns true
     * iff the first non-whitepsace character of str is zero.
     */
    bool startsWithZero(const char* str) {
        
        const char* firstNonWhite = str;
        
        while( (firstNonWhite[0] == ' ' || 
                firstNonWhite[0] == '\t' ||
                firstNonWhite[0] == '\n' ) &&
               firstNonWhite[0] != '\0') {
            firstNonWhite++;
        }
        return (firstNonWhite[0] == '0');
        
    }
    
    /**
     * Throws an error message including the line number if the
     * attribute is not there and if the value is not an int.
     */
    int getIntRequiredAttrValue(const char* name, const XML_Char **attr) {
        
        const char* value_str = getRequiredAttrValue(name, attr); 
        
        int value = atoi(value_str);
        
        // error value is 0
        if( value == 0 && !startsWithZero(value_str) ) {
            throwParseError("The value '%s' in attribute '%s' is not a valid "
                       "integer value.", value_str, name);
        }// else valid int
        
        return value;
    }
    
    int getIntRequiredAttrValue(const char* name, const XML_Char **attr, 
                                int min, int max) {
        
        int value = getIntRequiredAttrValue(name, attr);
        
        // error value is not between min and max
        if( min > value || value > max ) {
            throwParseError("The value '%d' in the attribute '%s' is not "
                            "between %d and %d", value, name, min, max);
        }// else valid int
        
        return value;
    }
    
    /**
     * Throws an error message including the line number if the
     * attribute is not there and if the value is not an double.
     */
    double getDoubleRequiredAttrValue(const char* name, const XML_Char **attr) {
        
        const char* value_str = getRequiredAttrValue(name, attr); 
        
        double value = atof(value_str);
        
        // error value is 0
        if( value == 0 && !startsWithZero(value_str) ) {
            throwParseError("The value '%s' in attribute '%s' is not a valid "
                       "floating point value.", value_str, name);
        }// else valid double value
        
        return value;
    }
    
    /**
     * Returns the current position in the file in bytes.
     */
    int getCurrentByteIndex(){
        return XML_GetCurrentByteIndex(m_parser_);
    }

protected:
    XML_Parser m_parser_;
    string  m_strFileName_;

    /**
     * Sax parsers can throw this error to be caught in parse() where
     * filename and line number info is added.
     */
    void throwParseError(const char* format, ...)
    {
        va_list args;
        va_start(args, format);
        char buffer[4096];
        
        vsprintf(buffer, format, args);
        string er_msg(buffer);
        throw er_msg;
    }
};

} // namespace

#endif              //SAXHANDLER_H

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
