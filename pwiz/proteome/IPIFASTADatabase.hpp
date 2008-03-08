//
// IPIFASTADatabase.hpp 
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2006 Louis Warschaw Prostate Cancer Center
//   Cedars Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _IPIFASTADATABASE_HPP_
#define _IPIFASTADATABASE_HPP_


#include <memory>
#include <string>
#include <vector>
#include <iosfwd>


namespace pwiz {
namespace proteome {


/// class for accessing data in ipi.*.fasta files
class IPIFASTADatabase
{
    public:

    /// constructor reads in entire file
    IPIFASTADatabase(const std::string& filename);
    ~IPIFASTADatabase();

    /// structure for holding peptide info
    struct Record
    {
        int id;
        std::string sequence;
        Record(int _id=0) : id(_id) {}
    };

    /// access to the data in memory
    const std::vector<Record>& records() const; 
    
    /// typedef to simplify declaration of Record iterator
    typedef std::vector<Record>::const_iterator const_iterator;

    private:
    class Impl;
    std::auto_ptr<Impl> impl_;
};


std::ostream& operator<<(std::ostream& os, const IPIFASTADatabase::Record& record);


} // namespace proteome
} // namespace pwiz


#endif // _IPIFASTADATABASE_HPP_

