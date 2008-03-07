//
// MSDataAnalyzerApplication.hpp
//
//
// Darren Kessner <Darren.Kessner@cshs.org>
//
// Copyright 2008 Spielberg Family Center for Applied Proteomics
//   Cedars-Sinai Medical Center, Los Angeles, California  90048
//   Unauthorized use or reproduction prohibited
//


#ifndef _MSDATAANALYZERAPPLICATION_HPP_
#define _MSDATAANALYZERAPPLICATION_HPP_


#include "MSDataAnalyzer.hpp"


namespace pwiz {
namespace analysis {


///
/// Utility class for handling command line parsing, filename wrangling, and
/// MSDataAnalyzer driving.
///
struct MSDataAnalyzerApplication
{
    std::string usageOptions;
    std::string outputDirectory;
    std::vector<std::string> filenames;
    std::vector<std::string> commands;

    /// construct and parse command line, filling in the various structure fields
    MSDataAnalyzerApplication(int argc, const char* argv[]);

    /// iterate through file list, running analyzer on each file 
    void run(MSDataAnalyzer& analyzer, std::ostream* log = 0) const;
};


} // namespace analysis 
} // namespace pwiz


#endif // _MSDATAANALYZERAPPLICATION_HPP_

