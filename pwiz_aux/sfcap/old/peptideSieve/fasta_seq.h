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
/** 
 * @file fasta_seq.h
 */

#ifndef FASTA_SEQ_H
#define FASTA_SEQ_H

#include <string>
#include <iostream>
#include <memory>

/**
 * \defgroup fasta_group Fasta Module
 * Fasta Sequence handling module
 * @{
 * @}
 */

/** @ingroup fasta_group bio_string
 *  namespace bioinfo is in two groups
 *  @sa @link fasta_group The first group@endlink, bio_string
 *  @brief bioinformatics generic programming toolkit
 *
 */
namespace bioinfo 
{
  /**
   * fasta_seq : represents a sequence type (<i>seqtype</i>)
   * and a <i>string</i> identifier corresponding to
   * the header in a fasta_seq file
   * @ingroup fasta_group
   *
   * The two stored pieces of information: the header
   * and the sequence, are stored as pointers.
   * <br>Design comments:
   * 2 pointers are store resulting in added 
   * memory overhead.  The benifit is that the
   * sequence type can be created else where
   * and efficiently stored in the fasta_seq object.
   * the assumption is there will not be an extrodinary
   * number of fasta_seq objects in memory.  It is assumed 
   * that the sequence string dominates the memory
   * space
   * Riddle me this, can you come up with a good way to 
   * not have to commit to making the <i>seqtype</i> a pointer?
  **/
  template <typename seqtype = std::string>
    class fasta_seq 
    {
     public:
      //typedef typename std::pair<std::auto_ptr<std::string>,std::auto_ptr<seqtype> > fasta_seq_t;

      /**
       * default constructor allocates space for a point to seqtype
       * @see fasta_seq(const std::string&)
       */
      fasta_seq();

     /**
      * @param const string& : fasta_seq filename
      * @see fasta_seq()
      */
      fasta_seq(const std::string&);

      /**
       * default make sure you clean up
       */
      ~fasta_seq();

     /**
      * @param const std::string& : output filename for fasta_seq file
      */
      void 
      write(const std::string&) const;

     /**
      * @return string containing fasta header 
      */
     const 
     std::string& get_header() const {
       return *(_fasta_seq.first);
     }

      /**
       * @param std::string* : assign header to this string pointer
       *                  fasta_seq object will take ownership of string
       */
      void 
      set_header(std::string* str) {
	_fasta_seq.first.reset(str); // 1 liners ok in header...
      }

      /**
       * @param std::string* : assign seq to this string pointer
       *                  fasta_seq object will take ownership of string
       */
      void 
      set_seq(std::string* str) {
	_fasta_seq.second.reset(str); // 1 liners ok in header...
      }


     /**
      * if you want to make this compatible with char*
      * will have to return const seqtype*
      * @return constant reference to sequence
      * @see seqtype& get_seq();
      */
      const seqtype&
      get_seq() const {
	return *(_fasta_seq.second);
      }

     /**
      * @return non-constant reference to sequence
      * @see const seqtype& get_seq() const;
      */
      seqtype& 
      get_seq() {
	return *(_fasta_seq.second);
      }

      /**
       * @param const std::string& : filename containing fasta sequence
       */
      void read(const std::string&);



    private:
      std::pair<std::auto_ptr<std::string>,std::auto_ptr<seqtype> > _fasta_seq; //< private
  };

#if defined (WIN32)
#define __GNUC__ 5
#endif

#if __GNUC__ <= 2
  template<typename CharT, typename CharTraits>
  int
  seq_len(istream&); 

  template<typename CharT, typename CharTraits, typename seqtype>
  static 
  void seq_copy(istream&,seqtype&);
#else
  template<typename CharT, typename CharTraits>
  int
  seq_len(std::basic_istream<CharT,CharTraits>&); 

  template<typename CharT, typename CharTraits, typename seqtype>
  static 
  void seq_copy(std::basic_istream<CharT,CharTraits>&,seqtype&);
#endif

#if defined (WIN32)
#undef __GNUC__ 
#endif

} // bioinfo namespace

namespace std
{
  /** @{ */
  template<typename seqtype>
  ostream& operator<<(ostream&, const bioinfo::fasta_seq<seqtype>& );

  //template<typename seqtype>
  //istream& operator>>(istream&, bioinfo::fasta_seq<seqtype>& );  
  /** @} */
}


#include "fasta_seq.hpp"

#endif // FASTA_SEQ_H
