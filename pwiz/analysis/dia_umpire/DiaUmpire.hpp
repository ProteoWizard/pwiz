//
// Java author: Chih-Chiang Tsou <chihchiang.tsou@gmail.com>
//              Nesvizhskii Lab, Department of Computational Medicine and Bioinformatics
//
// Copyright 2014 University of Michigan, Ann Arbor, MI
//
//
// C++ port: Matt Chambers <matt.chambers42 .@. gmail.com>
//
// Copyright 2020 Matt Chambers
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


#ifndef _DIAUMPIRE_HPP_
#define _DIAUMPIRE_HPP_

#include "pwiz/utility/misc/Export.hpp"
#include "pwiz/data/msdata/MSData.hpp"
#include "pwiz/utility/misc/IterationListener.hpp"
#include "InstrumentParameter.hpp"


namespace DiaUmpire {


struct PWIZ_API_DECL TargetWindow
{
    enum class Scheme
    {
        SWATH_Fixed,
        SWATH_Variable
    };

    TargetWindow(MzRange mzRange) : mzRange(mzRange) {}

    bool operator== (const TargetWindow& rhs) const { return mzRange == rhs.mzRange; }

    MzRange mzRange;
    std::vector<size_t> spectraInRange;
};

typedef std::unique_ptr<TargetWindow> TargetWindowPtr;

struct PWIZ_API_DECL Config
{
    InstrumentParameter instrumentParameters;
    TargetWindow::Scheme diaTargetWindowScheme;
    int diaFixedWindowSize;
    std::vector<TargetWindow> diaVariableWindows;

    bool exportMs1ClusterTable = false;
    bool exportMs2ClusterTable = false;
    bool exportSeparateQualityMGFs = false;

    Config(const std::string& paramsFilepath = "");
};

struct DiaWindow;

class PWIZ_API_DECL DiaUmpire
{
    public:

    DiaUmpire(const pwiz::msdata::MSData& msd, const pwiz::msdata::SpectrumListPtr& spectrumList, const Config& config, const pwiz::util::IterationListenerRegistry* ilr = nullptr);
    ~DiaUmpire();

    pwiz::msdata::SpectrumListPtr outputSpectrumList() const;

    private:
    class Impl;
    std::unique_ptr<Impl> impl_;
    friend struct DiaWindow;
};

} // namespace DiaUmpire

#endif // _DIAUMPIRE_HPP_
