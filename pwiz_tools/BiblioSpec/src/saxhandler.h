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

#if !defined(SAXHANDLER_H)
#define SAXHANDLER_H

#include <string>
#include <iostream>
#include <sstream>
#include "expat.h"
#include "Verbosity.h"
#include "BlibUtils.h"
#include <boost/algorithm/string.hpp>


namespace BiblioSpec {

/**
* eXpat SAX parser wrapper.
*/
class SAXHandler
{
public:
    SAXHandler();
    virtual ~SAXHandler();

    class EndEarlyException : public std::exception {};

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
        m_bytes_ = NULL;
        m_bytesLen_ = 0;
    }

    inline void setXmlData(string& xmlData)
    {
        m_strFileName_.clear();
        m_bytes_ = xmlData.c_str();
        m_bytesLen_ = xmlData.length();
    }

    // Helper functions
    inline bool isElement(const char *n1, const XML_Char *n2)
    {    return (strcmp(n1, n2) == 0); }

    inline bool isIElement(const char *n1, const XML_Char *n2)
    {    return boost::iequals(n1, n2); }

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

    double getDoubleAttrValueOr(const char* name, const XML_Char **attr, double defaultValue) {

        const char* value = getAttrValue(name, attr);
        if (strlen(value) == 0)
            return defaultValue;
        try
        {
            return lexical_cast<double>(value);
        }
        catch (bad_lexical_cast&)
        {
            throwParseError("The value '%s' in attribute '%s' could not be cast to .", value, name);
        }
    }
    
    /**
     * Returns the current position in the file in bytes.
     */
    boost::int64_t getCurrentByteIndex();

protected:
    XML_Parser m_parser_;
    string m_strFileName_;
    const char* m_bytes_;
    size_t m_bytesLen_;

    void initParser();
    std::string generateError(const std::string& message);
    std::string getParserError();

    /**
     * Sax parsers can throw this error to be caught in parse() where
     * filename and line number info is added.
     */
    [[ noreturn ]]
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
