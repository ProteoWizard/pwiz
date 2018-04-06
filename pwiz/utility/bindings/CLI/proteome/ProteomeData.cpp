//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars Sinai Medical Center, Los Angeles, California  90048
// Copyright 2008 Vanderbilt University - Nashville, TN 37232
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

#include "ProteomeData.hpp"


using System::Exception;
using System::String;
using boost::shared_ptr;


namespace b = pwiz::proteome;


namespace pwiz {
namespace CLI {
namespace proteome {


Protein::Protein(String^ id, int index, String^ description, String^ sequence)
: Peptide(new b::Protein(ToStdString(id), index, ToStdString(description), ToStdString(sequence)))
{base_ = new shared_ptr<b::Protein>(static_cast<b::Protein*>(Peptide::base_));}

bool Protein::empty() {return base().empty();}



int ProteinList::size() {return (int) (*base_)->size();}
bool ProteinList::empty() {return (*base_)->empty();}
int ProteinList::find(String^ id) {try {return (int) (*base_)->find(ToStdString(id));} CATCH_AND_FORWARD}

IndexList^ ProteinList::findKeyword(String^ keyword) {return findKeyword(keyword, true);}
IndexList^ ProteinList::findKeyword(String^ keyword, bool caseSensitive)
{
    try
    {
        b::IndexList indexList = (*base_)->findKeyword(ToStdString(keyword), caseSensitive);
        std::vector<size_t>* ownedIndexListPtr = new std::vector<size_t>();
        ownedIndexListPtr->swap(indexList);
        return gcnew IndexList(ownedIndexListPtr);
    }
    CATCH_AND_FORWARD
}

Protein^ ProteinList::protein(int index) {return protein(index, true);}
Protein^ ProteinList::protein(int index, bool getSequence)
{
    try {return gcnew Protein(new b::ProteinPtr((*base_)->protein((size_t) index, getSequence)));} CATCH_AND_FORWARD
}




ProteinListSimple::ProteinListSimple()
: ProteinList(new shared_ptr<b::ProteinList>(new b::ProteinListSimple()))
{base_ = reinterpret_cast<shared_ptr<b::ProteinListSimple>*>(ProteinList::base_);}

int ProteinListSimple::size() {return (int) (*base_)->size();}
bool ProteinListSimple::empty() {return (*base_)->empty();}

Protein^ ProteinListSimple::protein(int index) {return protein(index, true);}
Protein^ ProteinListSimple::protein(int index, bool getSequence)
{
    try {return gcnew Protein(new b::ProteinPtr((*base_)->protein((size_t) index, getSequence)));} CATCH_AND_FORWARD
}




ProteomeData::ProteomeData()
: base_(new shared_ptr<b::ProteomeData>(new b::ProteomeData())), owner_(nullptr)
{
}

bool ProteomeData::empty() {return (*base_)->empty();}


} // namespace proteome
} // namespace CLI
} // namespace pwiz
