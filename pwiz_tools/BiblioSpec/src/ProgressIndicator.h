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

#include <time.h>
#include <iostream>

using namespace std;

namespace BiblioSpec {

class ProgressIndicator
{
 public:
  ProgressIndicator(long total)
  {
    // Assume a header message was just output
    _lastOutput = time(NULL);
    
    _total = total;
    _current = 0;
    _percent = 0;
  }
  
  ~ProgressIndicator(void)
  {
    // nested PIs will not count up to total
    if( _current == _total )
      cerr << "100%" <<endl;
  }
  
  /**
   * Create a new PI which increments from current/total to
   * current+1/total. Promise that the parent ProgressIndicator is
   * unchanged.  
   */
  ProgressIndicator* newNestedIndicator(long inner_total) const
  {
    // Make sure inner indicator never outputs 100%
    inner_total++;
    ProgressIndicator* inner = new ProgressIndicator( inner_total * _total );
    inner->add(max((long)0, _current-1) * inner_total);
    return inner;
  }
  
  void increment()
  {
    add(1);
  }
  
  void add(long n)
  {
    _current += n;
    // This function never outputs 100%
    int percentCurrent = min((long)99, 100*max((long)0, _current - 1)/_total);
    if (percentCurrent != _percent) {
        _percent = percentCurrent;
        // If more than 1 second has elapsed, show output
        time_t currentTime = time(NULL);
        if (_lastOutput != currentTime) {
            cerr<<_percent<<"%"<<endl;
            cerr.flush();
            _lastOutput = currentTime;
        }
    }
  }
  
  long processed()
  {
    return _current;
  }
  
  /**
   * Set current to total to indicate that the progress being
   * monitored has finished, even if we didn't count up to total.
   */
  void finish()
  {
      _current = _total;
  }


 private:
  long _total;
  long _current;
  int _percent;
  time_t _lastOutput;
};

} // namespace

/*
 * Local Variables:
 * mode: c
 * c-basic-offset: 4
 * End:
 */
