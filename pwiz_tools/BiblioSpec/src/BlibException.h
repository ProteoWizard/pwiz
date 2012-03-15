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
#pragma once

/**
 * A general exception class for BiblioSpec programs.  Provides a
 * what() string and a hasFilename() flag so that the catcher can
 * decide what to add to the string.
 */

#include <exception>

namespace BiblioSpec{

    class BlibException : public exception{
    protected:
        string msgStr_;
        bool hasFilename_;
        
    public:
        BlibException() 
            : hasFilename_(false)
        {
            msgStr_ = "BiblioSpec exception thrown.";
        }
        
        BlibException(bool filename, const char* format, ...) 
            : hasFilename_(filename)
        {
            va_list args;
            va_start(args, format);
            char buffer[4096];
            
            vsprintf(buffer, format, args);
            msgStr_ = buffer;
        }

        ~BlibException()throw(){}

        virtual void setHasFilename(bool hasIt){
            hasFilename_ = hasIt;
        }

       virtual bool hasFilename(){
            return hasFilename_;
        }

        virtual void addMessage(const char* format, ...){
            va_list args;
            va_start(args, format);
            char buffer[4096];
            
            vsprintf(buffer, format, args);
            msgStr_ += buffer;
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
