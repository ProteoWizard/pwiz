/*
    File: $Id$
    Description: Basic stack-based XML writer.
    Date: July 25, 2007

    Copyright (C) 2007 Joshua Tasman, ISB Seattle


    This library is free software; you can redistribute it and/or
    modify it under the terms of the GNU Lesser General Public
    License as published by the Free Software Foundation; either
    version 2.1 of the License, or (at your option) any later version.

    This library is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
    Lesser General Public License for more details.

    You should have received a copy of the GNU Lesser General Public
    License along with this library; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA

*/

#ifndef _INCLUDED_SIMPLEXMLWRITER_H_
#define _INCLUDED_SIMPLEXMLWRITER_H_

#include "stdafx.h"


class SimpleXMLWriter {
public:
    SimpleXMLWriter() : 
        condenseAttr_(false),
        pOut_(NULL),
        curFileLength_(0),
        indent_(0),
        tagOpen_(false),
        hasAttr_(false),
        hasData_(false),
        indentStr_(""),
        spaceStr_(" ")
    {
    }

    virtual ~SimpleXMLWriter() {
        pOut_ = NULL;
    }

    void setOutputStream(ostream& o) { pOut_ = &o; }

    void startDocument(void) {
        if (!*pOut_)
            throw runtime_error("output XML stream not ready");
        string xmlHeader = "<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>\n";
        (*pOut_) << xmlHeader;
        curFileLength_ += xmlHeader.length();
    }

    void open(const string& tagname) {
        if (!*pOut_)
            throw runtime_error("output XML stream not ready");
        if (tagOpen_) {
            /*if (hasAttr_) {
                (*pOut_) << " ";
                ++curFileLength_;
            }*/
            (*pOut_) << ">";
            ++curFileLength_;
            tagOpen_ = false;
        } 
        if (!tags_.empty()) {
            (*pOut_) << '\n';
            ++curFileLength_;
        }
        tags_.push(tagname);

        tagOpen_ = true;
        indent_ += 1;
        setIndentStr();
        (*pOut_) << indentStr_ << "<" << tagname;
        curFileLength_ += indentStr_.length() + 1 + tagname.length();
        hasAttr_ = false;
        hasData_ = false;
    }

    //void open(const string& tagname, const std::vector< std::pair<string, string> > & attrlist);

    template<int N>
    void attr(const string& attrname, char const (val)[N]) {
        /*if (!*pOut_)
            throw runtime_error("output XML stream not ready");
        if (!hasAttr_) {
          // whitespace formatting to assist parsing:
          // first attribute should appear after element name,
          // separated by one space character, on same line
          // as the element start.
          (*pOut_) << " ";
          ++curFileLength_;
          hasAttr_ = true;
        }
        else if (!condenseAttr_) { 
            (*pOut_) << '\n' << indentStr_ << spaceStr_;
            curFileLength_ += 1 + indentStr_.length() + spaceStr_.length();
        }
        else {
          (*pOut_) << spaceStr_;
          curFileLength_ += spaceStr_.length();
        }
        (*pOut_) << attrname << "=\"";
        curFileLength_ += attrname.length() + 2;
        size_t valLen = strlen(val);
        for( size_t i=0; i < valLen; ++i )
            switch( val[i] )
            {
                case '"':    (*pOut_) << "&quot;"; curFileLength_ += 6; break;
                case '\'':    (*pOut_) << "&apos;"; curFileLength_ += 6; break;
                case '<':    (*pOut_) << "&lt;"; curFileLength_ += 4; break;
                case '>':    (*pOut_) << "&gt;"; curFileLength_ += 4; break;
                case '&':    (*pOut_) << "&amp;"; curFileLength_ += 5; break;
                default:    (*pOut_) << val[i]; ++curFileLength_; break;
            }
        (*pOut_) << "\"";
        ++curFileLength_;*/
        attr(attrname, (const char*)val);
    }

    inline void init_attr() {
        if (pOut_ == NULL)
            throw runtime_error("output XML stream is not initialized");
        if (!*pOut_)
            throw runtime_error("output XML stream not ready");
        if (!hasAttr_) {
          // whitespace formatting to assist parsing:
          // first attribute should appear after element name,
          // separated by one space character, on same line
          // as the element start.
          (*pOut_) << " ";
          ++curFileLength_;
          hasAttr_ = true;
        }
        else if (!condenseAttr_) { 
            (*pOut_) << '\n' << indentStr_ << spaceStr_;
            curFileLength_ += 1 + indentStr_.length() + spaceStr_.length();
        }
        else {
          (*pOut_) << spaceStr_;
          curFileLength_ += spaceStr_.length();
        }
    }

    void attr(const string& attrname, const char val) {
        init_attr();
        (*pOut_) << attrname << "=\"";
        curFileLength_ += attrname.length() + 2;
        switch( val )
        {
            case '"':    (*pOut_) << "&quot;"; curFileLength_ += 6; break;
            case '\'':    (*pOut_) << "&apos;"; curFileLength_ += 6; break;
            case '<':    (*pOut_) << "&lt;"; curFileLength_ += 4; break;
            case '>':    (*pOut_) << "&gt;"; curFileLength_ += 4; break;
            case '&':    (*pOut_) << "&amp;"; curFileLength_ += 5; break;
            default:    (*pOut_) << val; ++curFileLength_; break;
        }
        (*pOut_) << "\"";
        ++curFileLength_;
    }

    void attr(const string& attrname, const char* val) {
        init_attr();
        (*pOut_) << attrname << "=\"";
        curFileLength_ += attrname.length() + 2;
        for( ; *val != '\0'; ++val )
            switch( *val )
            {
                case '"':    (*pOut_) << "&quot;"; curFileLength_ += 6; break;
                case '\'':    (*pOut_) << "&apos;"; curFileLength_ += 6; break;
                case '<':    (*pOut_) << "&lt;"; curFileLength_ += 4; break;
                case '>':    (*pOut_) << "&gt;"; curFileLength_ += 4; break;
                case '&':    (*pOut_) << "&amp;"; curFileLength_ += 5; break;
                default:    (*pOut_) << *val; ++curFileLength_; break;
            }
        (*pOut_) << "\"";
        ++curFileLength_;
    }

    void attr(const string& attrname, const string& val) {
        //attr(attrname, val.c_str());
        init_attr();
        (*pOut_) << attrname << "=\"";
        curFileLength_ += attrname.length() + 2;
        for( size_t i=0; i < val.length(); ++i )
            switch( val[i] )
            {
                case '"':    (*pOut_) << "&quot;"; curFileLength_ += 6; break;
                case '\'':    (*pOut_) << "&apos;"; curFileLength_ += 6; break;
                case '<':    (*pOut_) << "&lt;"; curFileLength_ += 4; break;
                case '>':    (*pOut_) << "&gt;"; curFileLength_ += 4; break;
                case '&':    (*pOut_) << "&amp;"; curFileLength_ += 5; break;
                default:    (*pOut_) << val[i]; ++curFileLength_; break;
            }
        (*pOut_) << "\"";
        ++curFileLength_;
    }

    /*void attr(const string& attrname, const int val) {
        init_attr();
        string valStr = lexical_cast<string>(val);
        (*pOut_) << attrname << "=\"" << valStr << "\"";
        curFileLength_ += attrname.length() + 3 + valStr.length();
    }

    void attr(const string& attrname, const unsigned int val) {
        init_attr();
        string valStr = lexical_cast<string>(val);
        (*pOut_) << attrname << "=\"" << valStr << "\"";
        curFileLength_ += attrname.length() + 3 + valStr.length();
    }

    void attr(const string& attrname, const long val) {
        init_attr();
        string valStr = lexical_cast<string>(val);
        (*pOut_) << attrname << "=\"" << valStr << "\"";
        curFileLength_ += attrname.length() + 3 + valStr.length();
    }

    void attr(const string& attrname, const unsigned long val) {
        init_attr();
        string valStr = lexical_cast<string>(val);
        (*pOut_) << attrname << "=\"" << valStr << "\"";
        curFileLength_ += attrname.length() + 3 + valStr.length();
    }

    void attr(const string& attrname, const float val) {
        init_attr();
        string valStr = lexical_cast<string>(val);
        (*pOut_) << attrname << "=\"" << valStr << "\"";
        curFileLength_ += attrname.length() + 3 + valStr.length();
    }

    void attr(const string& attrname, const double val) {
        init_attr();
        string valStr = lexical_cast<string>(val);
        (*pOut_) << attrname << "=\"" << valStr << "\"";
        curFileLength_ += attrname.length() + 3 + valStr.length();
    }*/

    template<typename StreamableType>
    void attr(const string& attrname, const StreamableType& val) {
        init_attr();
        string valStr = lexical_cast<string>(val);
        (*pOut_) << attrname << "=\"" << valStr << "\"";
        curFileLength_ += attrname.length() + 3 + valStr.length();
    }

    void attr(const std::vector< std::pair<string, string> > & attrlist) {
        for(size_t i=0; i < attrlist.size(); ++i)
            attr(attrlist[i].first, attrlist[i].second);
    }

    void noattr(void) {
        if (!*pOut_)
            throw runtime_error("output XML stream not ready");
        if (tagOpen_) {
            (*pOut_) << ">";
            ++curFileLength_;
            tagOpen_ = false;
        } 
    }

    void data(const string& data) {
        if (!*pOut_)
            throw runtime_error("output XML stream not ready");
        if (tagOpen_) {
            /*if (hasAttr_) {
                (*pOut_) << " ";
                ++curFileLength_;
            }*/
            (*pOut_) << ">";
            ++curFileLength_;
            tagOpen_ = false;
        } 
        (*pOut_) << data;
        curFileLength_ += data.length();
        hasData_ = true;
    }

    void close() {
        if (!*pOut_)
            throw runtime_error("output XML stream not ready");
        if (tagOpen_) {
            (*pOut_) << " />";
            curFileLength_ += 3;
        } else {
            if (!hasData_) {
                (*pOut_) << '\n' << indentStr_;
                curFileLength_ += 1 + indentStr_.length();
            }
            (*pOut_) << "</" << tags_.top() << ">";
            curFileLength_ += 3 + tags_.top().length();
        }

        tagOpen_ = false;
        hasData_ = false;

        --indent_;
        setIndentStr();

        tags_.pop();
        if (tags_.empty()) {
            (*pOut_) << '\n';
            ++curFileLength_;
        }
    }

    void closeAll() {
        while (!tags_.empty()) {
            close();
        }
    }

    void flush()
    {
        if(*pOut_)
            (*pOut_).flush();
    }

    boost::int64_t getCurFileLength() { return curFileLength_; }

    bool condenseAttr_;

protected:
    ostream* pOut_; // must be set before use.  Add error checking for unset case.
    boost::int64_t curFileLength_;

    void setIndentStr() {
        indentStr_ = "";
        for (int i=1; i<indent_; i++) {
            indentStr_ += spaceStr_;
        }
    }

    int indent_;
    bool tagOpen_;
    bool hasAttr_;
    bool hasData_;

    std::stack<string> tags_;
    string indentStr_;
    string spaceStr_;
};


#endif // _INCLUDED_SIMPLEXMLWRITER_H_
