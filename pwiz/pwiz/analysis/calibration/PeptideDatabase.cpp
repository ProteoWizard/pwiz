//
// $Id$ 
//
//
// Original author: Darren Kessner <darren@proteowizard.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
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


#define PWIZ_SOURCE

#include "PeptideDatabase.hpp"
#include "boost/iostreams/device/mapped_file.hpp"
#include <iostream>
#include <vector>
#include <stdexcept>
#include <fstream>


namespace pwiz {
namespace proteome {


using namespace std;


//
//  The PeptideDatabase interface has two implementations:
//
//  1) PeptideDatabaseImpl_Memory is instantiated by PeptideDatabase::create(void).
//     It is a writeable database that uses dynamic arrays internally.
//     It can be written out to a binary file, which can be read by PeptideDatabaseImpl_File.
//
//  2) PeptideDatabaseImpl_File is instantiated by PeptideDatabase::create(filename).
//     It is a read-only database that is backed by a binary file. 
//     It uses the boost memory-mapped file objects, which abstract the different
//     memory-mapped file facilities in Windows and POSIX. 
//
//  The binary file format was designed to have constant-size records, to allow for 
//  efficient binary searches.  To accomodate variable-length amino acid sequences, 
//  each PeptideDatabaseRecord has a sequenceKey, which is an offset into a string table.
//  The string table follows the main records in the file.
//
//  pseudo_struct binary_file_format
//  {
//      PDBHeader header;
//      PeptideDatabaseRecord records[header.recordCount];
//      char stringTable[header.stringTableSize];
//  };
//


PWIZ_API_DECL ostream& operator<<(ostream& os, const PeptideDatabaseFormula& formula)
{
    os << "C" << formula.C 
        << " H" << formula.H
        << " N" << formula.N
        << " O" << formula.O
        << " S" << formula.S;
	
	return os;
}


PWIZ_API_DECL bool operator==(const PeptideDatabaseFormula& r, const PeptideDatabaseFormula& s) 
{
    return (r.C == s.C &&
            r.H == s.H &&
            r.N == s.N &&
            r.O == s.O &&
            r.S == s.S);
}


PWIZ_API_DECL ostream& operator<<(ostream& os, const PeptideDatabaseRecord& record)
{
    os << record.abundance << " " << record.mass << " " << record.formula; 
	return os;
}


#pragma pack(1)
struct PDBHeader
{
    char magic[4]; // "PCC\0"
    char type[4];  // "PDB\0" (Peptide DataBase)
    int version;
    int recordsOffset;
    int recordCount;
    int stringTableOffset;
    int stringTableSize;

    PDBHeader()
    :   version(1), // increment here if format changes
        recordsOffset(0),
        recordCount(0),
        stringTableOffset(0),
        stringTableSize(0)
    {
        strncpy(magic, "PCC\0", 4);
        strncpy(type, "PDB\0", 4);
    }
};
#pragma pack()


class PeptideDatabaseImpl_Memory : public PeptideDatabase 
{
    public:
    PeptideDatabaseImpl_Memory();
    virtual int size() const;
    virtual const PeptideDatabaseRecord* records() const;
    virtual string sequence(const PeptideDatabaseRecord& record) const;
    virtual void append(const PeptideDatabaseRecord& record, const string& sequence);
    virtual void write(const string& filename) const;

    private:
    vector<PeptideDatabaseRecord> records_;
    string stringTable_;
};


PeptideDatabaseImpl_Memory::PeptideDatabaseImpl_Memory()
{
    stringTable_ += '\0'; // sequenceKey==0 -> null string
}


int PeptideDatabaseImpl_Memory::size() const 
{
    return (int)records_.size();
}


const PeptideDatabaseRecord* PeptideDatabaseImpl_Memory::records() const 
{
    if (records_.empty())
        throw logic_error("[PeptideDatabaseImpl_Memory::records()]  No records.");
    return &records_[0];
} 


string PeptideDatabaseImpl_Memory::sequence(const PeptideDatabaseRecord& record) const
{
    return string(stringTable_.data() + record.sequenceKey); 
}


void PeptideDatabaseImpl_Memory::append(const PeptideDatabaseRecord& record, const string& sequence)
{
    if (record.sequenceKey != 0) 
        throw logic_error("[PeptideDatabaseImpl_Memory::append()] Non-zero sequenceKey."); 

    records_.push_back(record);
    
    if (!sequence.empty())
    {
        PeptideDatabaseRecord& r = records_[records_.size()-1];
        r.sequenceKey = (int)stringTable_.size(); 
        stringTable_ += sequence + '\0';
    }
}


void PeptideDatabaseImpl_Memory::write(const string& filename) const
{
    if (records_.empty())
        throw logic_error("[PeptideDatabaseImpl_Memory::write()]  No records.");
    
    ofstream os(filename.c_str(), ios::binary);
    if (!os)
        throw runtime_error("[PeptideDatabaseImpl_Memory::write()] Unable to open file " + filename);
    
    PDBHeader header;
    header.recordsOffset = sizeof(header);
    header.recordCount = (int)records_.size();
    header.stringTableOffset = header.recordsOffset + header.recordCount*sizeof(PeptideDatabaseRecord);
    header.stringTableSize = (int)stringTable_.size();

    os.write((const char*)&header, sizeof(header));
    os.write((const char*)&records_[0], header.recordCount*sizeof(PeptideDatabaseRecord)); 
    os.write(stringTable_.c_str(), (streamsize)stringTable_.size());
}


class PeptideDatabaseImpl_File : public PeptideDatabase 
{
    public:
    PeptideDatabaseImpl_File(const string& filename);
    virtual int size() const;
    virtual const PeptideDatabaseRecord* records() const;
    virtual string sequence(const PeptideDatabaseRecord& record) const;
    virtual void append(const PeptideDatabaseRecord& record, const string& sequence);
    virtual void write(const string& filename) const;

    private:
    boost::iostreams::mapped_file_source file_;
    const PDBHeader* header_;
    int recordCount_;
    const PeptideDatabaseRecord* records_;
    int stringTableSize_;
    const char* stringTable_;
};


PeptideDatabaseImpl_File::PeptideDatabaseImpl_File(const string& filename)
:   file_(filename), 
    header_(0),
    recordCount_(0), records_(0),
    stringTableSize_(0), stringTable_(0)
{
    header_ = reinterpret_cast<const PDBHeader*>(file_.data());

    PDBHeader good;
    if (strncmp(header_->magic, good.magic, 4) ||
        strncmp(header_->type, good.type, 4) ||
        header_->version != good.version)
        throw runtime_error("[PeptideDatabaseImpl_File::PeptideDatabaseImpl_File] '" + filename + "' has an invalid header.");

    recordCount_ = header_->recordCount;
    records_ = reinterpret_cast<const PeptideDatabaseRecord*>(file_.data() + header_->recordsOffset); 
    stringTableSize_ = header_->stringTableSize;
    stringTable_ = reinterpret_cast<const char*>(file_.data() + header_->stringTableOffset);
}


int PeptideDatabaseImpl_File::size() const
{
    return recordCount_; 
}


const PeptideDatabaseRecord* PeptideDatabaseImpl_File::records() const
{
    return records_; 
}


string PeptideDatabaseImpl_File::sequence(const PeptideDatabaseRecord& record) const
{
    return string(stringTable_ + record.sequenceKey);
}


void PeptideDatabaseImpl_File::append(const PeptideDatabaseRecord& record, const string& sequence)
{
    // this will never be called, since clients cannot obtain a non-const this
    throw logic_error("PeptideDatabaseImpl_File::append() not implemented.");
}


void PeptideDatabaseImpl_File::write(const string& filename) const
{
    // this should never be needed
    throw logic_error("PeptideDatabaseImpl_File::write() not implemented.");
}


PWIZ_API_DECL auto_ptr<PeptideDatabase> PeptideDatabase::create()
{
    return auto_ptr<PeptideDatabase>(new PeptideDatabaseImpl_Memory);
}


PWIZ_API_DECL auto_ptr<const PeptideDatabase> PeptideDatabase::create(const string& filename)
{
    try 
    {
        return auto_ptr<const PeptideDatabase>(new PeptideDatabaseImpl_File(filename));
    }
    catch (BOOST_IOSTREAMS_FAILURE&)
    {
        throw runtime_error("[PeptideDatabase::create()] Unable to map file " + filename); 
    }
}


namespace {
bool hasSmallerMass(const PeptideDatabaseRecord& a, const PeptideDatabaseRecord& b)
{
    return a.mass < b.mass;
}
} // namespace


PWIZ_API_DECL PeptideDatabase::iterator PeptideDatabase::mass_lower_bound(double mass) const
{
    PeptideDatabaseRecord record;
    record.mass = mass;
    return lower_bound(begin(), end(), record, hasSmallerMass);
}


PWIZ_API_DECL PeptideDatabase::iterator PeptideDatabase::mass_upper_bound(double mass) const
{
    PeptideDatabaseRecord record;
    record.mass = mass;
    return upper_bound(begin(), end(), record, hasSmallerMass);
}


} // namespace proteome
} // namespace pwiz

