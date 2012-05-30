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

namespace BiblioSpec{


  /**
   * An interface for a class that will accept data from a
   * DelimitedFileReader.
   */
   template <typename STORAGE_TYPE> class DelimitedFileConsumer {
   public:
     /** 
      * A function to be called by a DelimitedFileReader to hand over
      * the data parsed from one line of a delimited file.
      */
     virtual void addDataLine(STORAGE_TYPE& value) = 0; 
   };
   
} // namespace
