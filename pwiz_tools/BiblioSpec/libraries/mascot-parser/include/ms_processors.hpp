/*
##############################################################################
# file: ms_processors.hpp                                                    #
# 'msparser' toolkit                                                         #
# Use this class to retrieve current CPU configuration                       #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_processors.hpp          $ #
#     $Author: villek $ #
#       $Date: 2010-09-10 10:22:35 $ #
#   $Revision: 1.15 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_PROCESSORS_HPP
#define MS_PROCESSORS_HPP

#ifdef _WIN32
#pragma warning(disable:4251)   // Don't want all classes to be exported
#pragma warning(disable:4786)   // Debug symbols too long
#   ifndef _MATRIX_USE_STATIC_LIB
#       ifdef MS_MASCOTRESFILE_EXPORTS
#           define MS_MASCOTRESFILE_API __declspec(dllexport)
#       else
#           define MS_MASCOTRESFILE_API __declspec(dllimport)
#       endif
#   else
#       define MS_MASCOTRESFILE_API
#   endif
#else
#   define MS_MASCOTRESFILE_API
#endif

// for the sake of #include <string>
#ifdef __ALPHA_UNIX__
#include <ctype.h>
#endif
#ifdef __SUNOS__
#include <kstat.h>
#endif
#include <string>
#include <vector>
#include <map>

namespace matrix_science {

#ifndef DOXYGEN_SHOULD_SKIP_THIS
#ifndef SWIG
    class MS_MASCOTRESFILE_API ms_cpuinfo
    {
    public:
        ms_cpuinfo() : cpu_id_(0), 
                       core_id_(0),
                       ht_id(0),
                       processor_speed_MHZ_(0),
                       hyperthreaded_(false), 
                       availableForMascot_(true), 
                       isPrimary_(true) {};
        // int logical_id use as the map
        int  cpu_id_;
        int  core_id_;
        int  ht_id;
        int  processor_speed_MHZ_;
        bool hyperthreaded_;
        bool availableForMascot_;  // Should always be true?
        bool isPrimary_;
    };
#endif  // SWIG
#endif // DOXYGEN_SHOULD_SKIP_THIS


    /** @addtogroup config_group
     *  
     *  @{
     */

    //! This class is designed to retrieve current CPU configuration on the computer.
    /*!
     *  Just create an instance and then read necessary values using other
     *  members. Check for validity (if necessary) and report any errors (if
     *  any) after reading the configuration.
     */
    class MS_MASCOTRESFILE_API ms_processors: public ms_errors
    {
    public:
        typedef std::vector<short> cpu_id_t;

        //! Hyper-threading status for the CPUs.
        /*!
         * This is currently only relevant for Intel Pentium processors
         * and Power 5 processors under AIX. It will be 
         * HT_NOT_CAPABLE for AMD dual processor systems.
         *
         * See \ref DynLangEnums.
         */
        enum HYPERTHREADING
        {
            HT_NOT_CAPABLE           = 0, //!< For older Intel chips, and all other processors.
            HT_ENABLED               = 1, //!< Intel processor that supports hyper-threading and has it enabled in the BIOS.
            HT_DISABLED              = 2, //!< Pentium P4 (non Xeon) that has hyper-threading 'cobbled' so it cannot be used.
            HT_SUPPORTED_NOT_ENABLED = 3, //!< Pentium P4 (Xeon) and later that has hyper-threading turned off in the BIOS.
            HT_CANNOT_DETECT         = 4  //!< Pentium P4, but OS limitation prevents detection of HT.
        };

        //! Retrieves all CPU information immediately.
        ms_processors(bool checkLinuxHT, int numLicensed);

        //! Copying constructor.
        ms_processors(const ms_processors& src);

        //! Destructor.
        ~ms_processors();

        //! Call this function to re-initialise the object.
        void defaultValues();

        //! Allows to make a copy of another instance.
        void copyFrom(const ms_processors* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_processors& operator=(const ms_processors& right);
#endif
        //! Returns the number of logical CPUs installed in the system.
        int getNumOnSystem() const;

        //! Returns the number of logical CPUs that a process can use.
        int getNumAvailableToProcess() const;

        //! Returns true if process affinity is supported by the OS and false if only thread affinity is supported.
        bool isProcessAffinitySupported() const;

        //! Returns a flag which indicates if the platform supports pinning threads to a processor.
        bool isUseProcessesNotThreads() const;

        //! \deprecated Use isLogicalCPUAvailableForMascot() instead.
        UINT64 getWhichAvailableForMascot() const;

        //! Returns a vector of logical CPU numbers available for Mascot (or any other process).
        std::vector<int> getWhichAvailableForMascot2();

        //! Returns true if the numbered logical CPU is available to Mascot  (or any other process).
        bool isLogicalCPUAvailableForMascot(const int cpu) const;

        //! Return the number of physical CPUs on a system.
        int getNumPhysicalOnSystem() const;

        //! Returns the hyper-threading state as best as can be detected .
        HYPERTHREADING getHyperThreadingState() const;

        //! Returns the number of 'logical' CPUs per physical processor.
        int getNumLogicalPerPhysical() const;

        //! Returns the number of cores on the processor specified by the physical CPU number.
        int getNumCores(const int cpu = -1) const;

        //! Returns the physical CPU number for a given logical CPU number.
        int getPhysicalFromLogical(const int cpu) const;

        //! Returns a string that describes the processor.
        std::string getProcessorName() const;

        //! Returns a string that describes multithreading or multi-core capabilities.
        std::string getMultithreadedName() const;

        //! \deprecated Use isPrimaryLogicalProcessor(), getCoreFromLogical() and getPhysicalFromLogical() instead.
        UINT64 getHyperThreadedCPUsMask() const; 

        //! Supersedes getHyperThreadedCPUsMask.
        bool isPrimaryLogicalProcessor(const int cpu) const;

        //! Supersedes getHyperThreadedCPUsMask.
        bool isThreadedLogicalProcessor(const int cpu) const;

        //! For multi-core processors, returns the core number for the give logical CPU.
        int getCoreFromLogical(const int cpu) const;

        //! For Intel processors, return an ID corresponding to the logical processors hyper-threading state.
        int getHT_IDFromLogical(const int cpu) const;

        unsigned char GetAPIC_ID(unsigned char & logical_id,
                                 unsigned char & physical_id,
                                 unsigned char & core_id,
                                 unsigned char & ht_id);

    private:
        void getIntelHyperThreadingInfo();

        typedef std::map<int, ms_cpuinfo> cpuInfo_t;
        cpuInfo_t       cpuInfo_;
        bool            usingINT64values_;        // Temporary kludge. Remove when always false;
        int             numOnSystem_;             // logical number of processors
        int             physicalNumOnSystem_;     // phyiscal number of processors
        int             numAvailableToProcess_;
        bool            processAffinitySupported_;
        bool            useProcessesNotThreads_;
        UINT64          whichAvailableForMascot_;
        HYPERTHREADING  hyperThreading_;
        int             numLogicalPerPhysical_;
        cpu_id_t        logicalToPhysical_;       // Note that the physical ID 
                                                  // stored in this array has 
                                                  // no real significance - it
                                                  // is just a number. To use
                                                  // it, need to see if one or
                                                  // more logical ID's have the
                                                  // same physical ID.
        UINT64          hyperThreadedCPUs_;       // Bit mask for hyperthreaded
        int             numLicensed_;
        bool            checkLinuxHT_;
        std::string     processorName_;           // e.g. "Intel", "AMD Opteron", "Power 5"
        std::string     multiThreadName_;         // e.g. "Hyper-threading", "Dual Core", "SMT"
        int             coresPerCpu_;
        bool            isIntelProcessor_;
        bool            multiCoreOrHTcapable_;    // Intel/AMD only. Corresponds to HT bit set.

        void checkProcessorsAvailable();
        unsigned findIntelMaskWidth(unsigned Max_Count) const;
        unsigned char findIntelIdFromAPIC(const unsigned char apic, 
                                          const unsigned char maxSubIdValue,
                                          const unsigned char shiftCount) const;

        void SMTOnPower5();
        bool setLogicalToPhysicalWithoutAffinity(bool & foundAnyNonZeroLogical);
        bool setLogicalToPhysicalUsingAffinity(bool & foundAnyNonZeroLogical);
        void getSolarisInfo();
#ifdef __SUNOS__
        int ms_processors::getKstatDataAsInt(kstat_named_t *knp) const;
#endif

#if defined(__LINUX__) || defined(__LINUX64__)
    public:
        typedef std::vector<unsigned char> cpu_apic_list_t;
        cpu_apic_list_t  linuxApic_;
        pthread_mutex_t  mutex_;
        bool             ht_linux_detected_;

#endif

           
    }; // class ms_processors
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_PROCESSORS_HPP

/*------------------------------- End of File -------------------------------*/

