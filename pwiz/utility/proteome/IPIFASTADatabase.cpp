//
// IPIFASTADatabase.cpp 
//
//
// Original author: Darren Kessner <Darren.Kessner@cshs.org>
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


#include "IPIFASTADatabase.hpp"
#include <iostream>
#include <iomanip>
#include <fstream>
#include <stdexcept>


using namespace std;


namespace pwiz {
namespace proteome {


class IPIFASTADatabase::Impl
{
    public:
    Impl(const string& filename);
    const vector<Record>& records() const {return records_;}

    private:
    vector<Record> records_;

    void readRecords(istream& is);
};


IPIFASTADatabase::Impl::Impl(const string& filename)
{
    ifstream is(filename.c_str()); 
    if (!is) throw runtime_error(("[IPIFASTADatabase::Impl::Impl()] Unable to open file " + filename).c_str());

    //cout << "Reading records from " << filename << "..." << flush;
    readRecords(is);
    //cout << "done.\n";
}


void IPIFASTADatabase::Impl::readRecords(istream& is)
{
    Record* current = 0;

    for (string buffer; getline(is, buffer); )
    {
        if (buffer.find(">IPI:IPI") == 0)
        {
            // start a new record, and set current pointer
            records_.push_back(Record(atoi(buffer.c_str()+8)));
            current = &records_.back(); 
        }
        else if (current)
        {
            // update current record with next line of the sequence
            current->sequence += buffer;
        }
    }
}


IPIFASTADatabase::IPIFASTADatabase(const string& filename) : impl_(new Impl(filename)) {}
IPIFASTADatabase::~IPIFASTADatabase(){} // auto-destruction of impl_
const vector<IPIFASTADatabase::Record>& IPIFASTADatabase::records() const {return impl_->records();} 


ostream& operator<<(ostream& os, const IPIFASTADatabase::Record& record)
{
    os << ">IPI:IPI" << setfill('0') << setw(8) << record.id << endl;
    for (unsigned int i=0; i<record.sequence.size(); i++)
    {
        os << record.sequence[i];
        if (i%60==59) os << endl;
    }
    return os;
}


} // namespace proteome
} // namespace pwiz

