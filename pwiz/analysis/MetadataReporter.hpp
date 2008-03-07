//
// MetadataReporter.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _METADATAREPORTER_HPP_ 
#define _METADATAREPORTER_HPP_ 


#include "MSDataAnalyzer.hpp"


namespace pwiz {
namespace analysis {


/// writes file-level metadata to a file
class MetadataReporter : public MSDataAnalyzer
{
    public:

    /// \name MSDataAnalyzer interface 
    //@{
    virtual void open(const DataInfo& dataInfo);
    //@}
};


template<>
struct analyzer_strings<MetadataReporter>
{
    static const char* id() {return "metadata";}
    static const char* description() {return "write file-level metadata";}
    static const char* argsFormat() {return "";}
    static std::vector<std::string> argsUsage() {return std::vector<std::string>();}
};


} // namespace analysis 
} // namespace pwiz


#endif // _METADATAREPORTER_HPP_ 

