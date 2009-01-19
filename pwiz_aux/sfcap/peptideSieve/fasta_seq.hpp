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


#include <iostream>
#include <string>
#include <stdexcept>
#include <fstream>
#include <assert.h>


#if defined (WIN32)
#define __GNUC__ 5
#endif


namespace std
{
  template <typename seqtype>
  ostream&
  operator<<(ostream& os, const bioinfo::fasta_seq<seqtype>& fasta_seq) {
    os<<">"<<fasta_seq.get_header()<<endl;
    const seqtype& seq = fasta_seq.get_seq();
    unsigned cnt = 0;
    const unsigned num_chars = 60;
    while(cnt < seq.size()) {
      for(unsigned i = cnt; i < seq.size(); 
	  i+=num_chars, cnt+=num_chars) {
	const seqtype& sub_seq = seq.substr(i,num_chars);
	os<<sub_seq<<endl;
      }
      //os<<endl;
    }
    return os;
  }
  
  
  /**
   * this is the most general definition for reading in a fasta_seq sequence
   * we don't know what "seqtype" is so we first have to load the sequence
   * into a string buffer, before transfering the data to the seqtype
   * if this is too slow you can write more specific instantiations...
   */
 /**
   * this is the most general definition for reading in a fasta_seq sequence
   * we don't know what "seqtype" is so we first have to load the sequence
   * into a string buffer, before transfering the data to the seqtype
   * if this is too slow you can write more specific instantiations...
   */
#if __GNUC__ <= 2
  istream& 
  operator>>(istream& is, bioinfo::fasta_seq<std::string>& fasta_seq) {
#else
  template <typename CharT, typename CharTraits>
  basic_istream<CharT,CharTraits>& 
  operator>>(basic_istream<CharT,CharTraits>& is, bioinfo::fasta_seq<string>& fasta_seq) {
#endif
    char idc = '\0';
    if(!(is>>idc) ) {
      return is;
    } 
    if( idc != '>' ) {
      throw std::runtime_error("invalid fasta_seq format");
    }
    const int buff_size = 9000;
    char buff[buff_size];
    if( ! is.getline(buff,buff_size) ) {
      return is;
    }
    string* strPtr = new string(buff);
    fasta_seq.set_header(strPtr);
    string& seq = fasta_seq.get_seq();
    //it's important to note that at this point there
    //has to be at least one line of sequence available
    //other wise this isn't a fasta_seq file, in this case
    //is should return in a failed state
#if __GNUC__ <= 2
    int len = bioinfo::seq_len<char,string::traits_type>(is);
    seq.resize(len);
    bioinfo::seq_copy<char,string::traits_type>(is,seq);
#else
    //int len = bioinfo::seq_len(is);
    //seq.resize(len);
    //bioinfo::seq_copy(is,seq);
    std::istream::pos_type pos = is.tellg(); 
    bool store=false;
    while( is.getline(buff,buff_size) ) {
      if( *buff == '>' ) {
	is.seekg(pos);
	break;
      }
      pos = is.tellg(); 
      seq += buff;
      store = true;
    }
    if(store && is.fail()) {
      is.clear();
    }
#endif
    return is;
  }
}
namespace bioinfo
{


  using std::string;

  template<typename CharT, typename CharTraits>
  int


#if __GNUC__ <= 2
  seq_len(std::istream& is) {
    const typename CharTraits::char_type eof = CharTraits::eos();
    std::streampos pos = is.tellg(); 
    int cnt = 0;
    while( is.peek() != eof && is.peek()!=-1) {
#else
  seq_len(std::basic_istream<CharT,CharTraits>& is) {
    const typename CharTraits::int_type eof = CharTraits::eof();
    std::istream::pos_type pos = is.tellg(); 
    int cnt = 0;
    while( is.peek() != eof && !is.eof() ) {
#endif
#if __GNUC__ <= 2
		typename CharTraits::char_type ch = is.get();
		if( ch  == '>' ) {
	 	   break;
		}
		// once we've succesfully gotten at least one line
		// we don't want to chomp eof, if there's nothing
		// left, leave it for the next call of >>
		if( ch != '\n' ) {
		   ++cnt;
		}
#else
      typename CharTraits::int_type ch = is.get();
      assert(!is.eof());
      assert( is.good() );
      assert( ch != eof );
      if( CharTraits::to_char_type(ch) == '>' ) {
	break;
      }
      // once we've succesfully gotten at least one line
      // we don't want to chomp eof, if there's nothing
      // left, leave it for the next call of >>
      if( CharTraits::to_char_type(ch) != '\n' ) {
	++cnt;
      }
#endif
    }
    //cout<<"pos: "<<pos<<endl;
    assert(is.good());
    is.seekg(pos);
    return cnt;
  }

  template <class CharT, class CharTraits, typename seqtype>
  void
#if __GNUC__ <= 2
  seq_copy(istream& is, seqtype& seq) {
  const typename CharTraits::char_type eof = CharTraits::eos();
#else
  seq_copy(std::basic_istream<CharT,CharTraits>& is, seqtype& seq) {
  const typename CharTraits::int_type eof = CharTraits::eof();
#endif
    for(unsigned i = 0; i < seq.length(); ) {
#if __GNUC__ <= 2
      typename CharTraits::char_type ch = is.get();
      if( ch == eof || ch == -1 ) {
	is.setstate(std::ios::eofbit);
	is.setstate(std::ios::failbit);
	break;
      }
      if( ch == '>' ) {
	break;
      }
      if( ch != '\n' ) {
	seq[i] = ch;
	++i;
      }
#else
      typename CharTraits::int_type ch = is.get();
      if( ch == eof ) {
	is.setstate(std::ios_base::eofbit);
	is.setstate(std::ios_base::failbit);
	break;
      }
      assert(eof != ch);
      if( CharTraits::to_char_type(ch) == '>' ) {
	break;
      }
      if( CharTraits::to_char_type(ch) != '\n' ) {
	seq[i] = ch;
	++i;
      }
#endif
    }
  }
  
  template <typename seqtype>
  fasta_seq<seqtype>::fasta_seq() {
    _fasta_seq.first.reset(new string);
    _fasta_seq.second.reset(new seqtype);
  }

  template <typename seqtype>
  fasta_seq<seqtype>::~fasta_seq() {
    // auto_ptrs get called here
  }

  template <typename seqtype>
  void 
  fasta_seq<seqtype>::write(const std::string& filename) const {
    std::ofstream ofs(filename.c_str());
    if( !ofs ) {
      throw std::runtime_error("unable to write fasta_seq file: " + filename);
    }
    ofs<<*this;
  }

  template <typename seqtype>
  void 
  fasta_seq<seqtype>::read(const std::string& filename) {
    std::ifstream ifs(filename.c_str());
    if( !ifs ) {
      throw std::runtime_error("unable to write fasta_seq file: " + filename);
    }
    ifs>>*this;
  }

}

#if defined (WIN32)
#undef __GNUC__ 
#endif

