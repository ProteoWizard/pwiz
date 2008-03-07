//
// SpectrumTable.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _SPECTRUMTABLE_HPP_ 
#define _SPECTRUMTABLE_HPP_ 


#include "MSDataAnalyzer.hpp"
#include "MSDataCache.hpp"


namespace pwiz {
namespace analysis {


/// writes table of spectrum metadata to a file
class SpectrumTable : public MSDataAnalyzer
{
    public:

    SpectrumTable(const MSDataCache& cache);

    /// \name MSDataAnalyzer interface
    //@{
    virtual UpdateRequest updateRequested(const DataInfo& dataInfo,
                                          const SpectrumIdentity& spectrumIdentity) const;

    virtual void close(const DataInfo& dataInfo);
    //@}

    private:
    const MSDataCache& cache_;
};


template<>
struct analyzer_strings<SpectrumTable>
{
    static const char* id() {return "spectrum_table";}
    static const char* description() {return "write spectrum metadata in a table format";}
    static const char* argsFormat() {return "";}
    static std::vector<std::string> argsUsage() {return std::vector<std::string>();}
};


} // namespace analysis 
} // namespace pwiz


#endif // _SPECTRUMTABLE_HPP_ 

