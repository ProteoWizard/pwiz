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

#pragma once

/**
 * A general exception class for BiblioSpec programs.  Provides a
 * what() string and a hasFilename() flag so that the catcher can
 * decide what to add to the string.
 */

#include <exception>
#include <string>
#include <boost/format.hpp>

namespace BiblioSpec{

    class BlibException : public std::exception{
    protected:
        std::string msgStr_;
        bool hasFilename_;

        static std::string formatStringForward(boost::format& message)
        {
            return message.str();
        }

        template <typename TValue, typename... TArgs>
        std::string formatStringForward(boost::format& message, TValue&& arg, TArgs&&... args)
        {
            message % std::forward<TValue>(arg);
            return formatStringForward(message, std::forward<TArgs>(args)...);
        }

    public:
        BlibException() 
            : hasFilename_(false)
        {
            msgStr_ = "BiblioSpec exception thrown.";
        }

        template <typename... TArgs>
        BlibException(bool filename, const char* format, TArgs&&... args)
            : hasFilename_(filename)
        {
            boost::format message(format);
            msgStr_ = formatStringForward(message, std::forward<TArgs>(args)...);
        }

        BlibException(bool filename, const std::string& message)
            : hasFilename_(filename)
        {
            msgStr_ = message;
        }

        ~BlibException()throw(){}

        virtual void setHasFilename(bool hasIt){
            hasFilename_ = hasIt;
        }

        virtual bool hasFilename(){
            return hasFilename_;
        }

        template <typename... TArgs>
        void addMessage(const char* format, TArgs&&... args)
        {
            boost::format message(format);
            msgStr_ += formatStringForward(message, std::forward<TArgs>(args)...);
        }
        
        virtual const char* what() const throw()
        {
            return msgStr_.c_str();
        }
    };







} // namespace













/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
