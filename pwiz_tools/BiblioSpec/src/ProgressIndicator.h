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

#include <ctime>
#include <iostream>
#include <algorithm>
#include "pwiz/utility/misc/Stream.hpp"
using std::max;
using std::min;


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
