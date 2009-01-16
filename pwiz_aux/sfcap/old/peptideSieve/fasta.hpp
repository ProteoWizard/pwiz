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


// fasta.tcc -*- C++ -*-

namespace std
{

  using bioinfo::fasta;
  using bioinfo::fasta_seq;

  template <typename seqtype>
  std::ostream& 
  operator<<(std::ostream& os, const bioinfo::fasta<seqtype>& mf) {
    typename fasta<seqtype>::const_iterator iter = mf.begin();
    typename fasta<seqtype>::const_iterator stop = mf.end();
    while(iter != stop) {
      const fasta_seq<seqtype>& fs = **iter;
      os<<fs;
      ++iter;
    }
    return os;
  }

  template <typename seqtype>
  std::istream& 
  operator>>(std::istream& is, bioinfo::fasta<seqtype>& mf) {
    for(;;) {
      fasta_seq<seqtype>* fseq = new fasta_seq<seqtype>;
      is>>(*fseq);
      if(is.fail()) {
	delete fseq;
	break;
      } else {
	mf.push_back(fseq);
      }
    }
    return is;
  }
}

namespace bioinfo
{

  template <typename seqtype>
  fasta<seqtype>::fasta(const string& filename) {
    read(filename);
  }

#if 0 
  // the individual sequences must be
  // deleted manually
  template <typename seqtype>
  fasta<seqtype>::~fasta() {
    // what's the best way to destroy?
    typename fasta<seqtype>::iterator iter = begin();
    typename fasta<seqtype>::iterator stop = end();
    while(iter != stop) {
      fasta_seq<seqtype>* tmp = *iter;
      delete tmp;
      ++iter;
    }
  }
#endif
  
  template <typename seqtype>
  void
  fasta<seqtype>::append(fasta& lst) {
    for(typename fasta<seqtype>::const_iterator iter = lst.begin(); iter != lst.end(); ++iter) {
      fasta_seq<seqtype>* fs = *iter;
      this->push_back(fs);
    }
  }
  template <typename seqtype>
  typename fasta<seqtype>::const_iterator 
  fasta<seqtype>::find_header(const string& header) const {
    for(typename fasta<seqtype>::const_iterator iter = this->begin(); iter != this->end(); ++iter) {
      const fasta_seq<seqtype>& fs = **iter;
      if(fs.get_header()==header)
	return iter;
    }
    return this->end();

  }
  template <typename seqtype>
  typename fasta<seqtype>::iterator
  fasta<seqtype>::find_header(const string& header) {
    for(typename fasta<seqtype>::iterator iter = this->begin(); iter != this->end(); ++iter) {
      const fasta_seq<seqtype>& fs = **iter;
      if(fs.get_header()==header)
        return iter;
    }
    return this->end();

  }


  template <typename seqtype>
  void 
  fasta<seqtype>::write(const string& filename) const {
    std::ofstream ofs(filename.c_str());
    if( !ofs ) {
      throw std::runtime_error("Unable to write fasta file: " + filename);
    }
    ofs<<*this;
  }

  template <typename seqtype>
  void 
  fasta<seqtype>::read(const string& filename) {
    std::ifstream ifs(filename.c_str());
    if( !ifs ) {
      throw std::runtime_error("Unable to read fasta file: " + filename);
    }
    ifs>>*this;
  }

  template <typename seqtype>
  void
  fasta<seqtype>::print_header(std::ostream& os) const {
    typename fasta<seqtype>::const_iterator iter = this->begin();
    typename fasta<seqtype>::const_iterator stop = this->end();
    while(iter != stop) {
      const fasta_seq<seqtype>* tmp = *iter;
      os<<tmp->get_header()<<std::endl;
      ++iter;
    }
  }

  template <typename seqtype>
  void
  fasta<seqtype>::random_shuffle() {
    typename fasta<seqtype>::iterator iter = this->begin();
    typename fasta<seqtype>::iterator stop = this->end();
    std::vector<fasta_seq<seqtype>*> svec(this->size());
    unsigned cnt =0;
    while(iter != stop) {
      fasta_seq<seqtype>* tmp = *iter;
      svec[cnt] = tmp;
      ++iter;
      ++cnt;
    }
    std::random_shuffle(svec.begin(),svec.end());
    this->clear();
    for(unsigned i = 0; i < svec.size(); ++i) {
      push_back(svec[i]);
    }
  }

}
