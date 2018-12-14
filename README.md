
![ProteoWizard Logo](http://www.proteowizard.org/img/proteowizard-logo.jpg "ProteoWizard")

The ProteoWizard Library and Tools are a set of modular and extensible open-source, cross-platform tools and software libraries that facilitate proteomics data analysis.

The libraries enable rapid tool creation by providing a robust, pluggable development framework that simplifies and unifies data file access, and performs standard chemistry and LCMS dataset computations.

Core code and libraries are under the Apache open source license; the vendor libraries fall under various vendor-specific licenses.

## Features
* reference implementation of HUPO-PSI mzML standard mass spectrometry data format
* supports HUPO-PSI mzIdentML 1.1 standard mass spectrometry analysis format
* supports reading directly from many vendor raw data formats (on Windows)
* modern C++ techniques and design principles
* cross-platform with native compilers (MSVC on Windows, gcc on Linux, darwin on OSX)
* modular design, for testability and extensibility
* framework for rapid development of data analysis tools
* open source license suitable for both academic and commercial projects (Apache v2)

## Official build status

| OS      | Status |
| ------- | ------ |
| Windows | ![Windows status](https://img.shields.io/teamcity/http/teamcity.labkey.org/s/bt83.svg?label=VS%202017) |
| Native Linux | ![Linux status](https://img.shields.io/teamcity/http/teamcity.labkey.org/s/bt17.svg?label=GCC%204.9) |
| Wine Linux | ![Docker-Wine status](https://img.shields.io/teamcity/http/teamcity.labkey.org/s/ProteoWizardAndSkylineDockerContainerWineX8664.svg?label=Docker-Wine) |

Click [here](http://proteowizard.sourceforge.net/downloads.shtml) to visit the official download page.

### Unofficial toolsets
| OS      | Toolset   | Status |
| ------- | -------   | ------ |
| Windows | VS2017    | ![VS2017 status](https://img.shields.io/appveyor/ci/chambm/pwiz.svg) |
| Linux   | GCC 7     | ![GCC7 status](https://travis-matrix-badges.herokuapp.com/repos/ProteoWizard/pwiz/branches/master/1) |
| Linux   | Clang 3.6 | ![Clang36 status](https://travis-matrix-badges.herokuapp.com/repos/ProteoWizard/pwiz/branches/master/2) |
| Linux   | Clang 5   | ![Clang5 status](https://travis-matrix-badges.herokuapp.com/repos/ProteoWizard/pwiz/branches/master/3) |
| OS X    | GCC 4.9   | ![OSX status](https://travis-matrix-badges.herokuapp.com/repos/ProteoWizard/pwiz/branches/master/4) |

