//
// Original Author: Parag Mallick
//
// Copyright 2009 Spielberg Family Center for Applied Proteomics 
//   Cedars Sinai Medical Center, Los Angeles, California  90048
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


// -*- C++ -*-
/** @file fasta.h
 *  defines fasta class
 */
#ifndef FASTA_H
#define FASTA_H

#include <string>
#include <algorithm> //fasta::random_shuffle calls std::random_shuffle
#include <vector> //used by fasta::random_shuffle
#include <list>
#include <iostream>
#include "fasta_seq.h"

namespace bioinfo
{
  /**
   * fasta : read and write multi fasta files
   * @ingroup fasta_group
   * 
   * <i>fasta</i> IS-A std::list of fasta<seqtype>*
   * template parameter defines the data type to
   * store sequence information in.  Defaults to 
   * std::std::string
   */
  template <typename seqtype = std::string>
    class fasta : public std::list<fasta_seq<seqtype>*> 
    {
      public:
      /**
       * creates an empty list to be filled later
       */
      fasta() { }

      /**
       * reads and stores a multi fasta file in memory
       * from a file
       * @param const std::string& : filename of multifasta file
       */
      fasta(const std::string&);

      /**
       * destructor
       * individual sequences must be deleted manually
       */
      //~fasta();

      /**
       * writes multifasta file to a file
       * @param const std::string& : output filename for multifasta data
       */
      void write(const std::string&) const;

      /**
       * @param const string& = filename
       * read fasta file
       */
      void read(const std::string&);

      void print_header(std::ostream&) const;

      /**
       * randomly shuffle the order the fasta files are stored.
       * This is useful for carrying out various analysis which
       * requires randomly sampling the sequence data
       */
      void random_shuffle();

      /**
       * add fasta_seq list to the end of *this
       */
      void append(fasta&);

      /**
       * find first fasta_seq with same header
       */
      typename fasta<seqtype>::iterator find_header(const std::string&);
      typename fasta<seqtype>::const_iterator find_header(const std::string&) const;


    };

} // namespace bioinfo;

namespace std
{

/** @{ */
  template <typename seqtype>
  std::ostream& 
    operator<<(std::ostream&, const bioinfo::fasta<seqtype>&);

  template <typename seqtype>
  std::istream& 
    operator>>(std::istream&, bioinfo::fasta<seqtype>&);
/** @} */
}


#include "fasta.hpp"

/** @example main.cc 
 * Example of using the library
 */
#endif // FASTA_H
