/*
##############################################################################
# file: ms_clusterparams.hpp                                                 #
# 'msparser' toolkit                                                         #
# Encapsulates "mascot.dat"-file that describes most important parameters    #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_clusterparams.hpp       $ #
#     $Author: davidc $ #
#       $Date: 2010-10-07 10:07:49 $ #
#   $Revision: 1.11 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_CLUSTERPARAMS_HPP
#define MS_CLUSTERPARAMS_HPP

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
#include <string>


namespace matrix_science {
    /** @addtogroup config_group
     *  
     *  @{
     */

    //! List of all supported operating systems.
    /*!
     * Mascot runs on a number of different operating systems. The 
     * following are currently supported. 
     *
     * See \ref DynLangEnums.
     */
    enum OPERATING_SYS
    {
        _OS_AIX               = 0,   //!< IBM AIX
        _OS_UNKNOWN           = 1,   //!< Unknown OS
        _OS_WINDOWS_NT        = 2,   //!< WindowsXXX
        _OS_IRIX              = 3,   //!< Irix
        _OS_ALPHA_TRUE64      = 4,   //!< Alpha Tru64
        _OS_SOLARIS           = 5,   //!< Solaris
        _OS_LINUX             = 6,   //!< Linux
        _OS_LINUXALPHA        = 7,   //!< Alpha Linux
        _OS_FREE_BSD_         = 8,   //!< FreeBSD
        _OS_NUM_OPERATING_SYS = 9    //!< Placeholder
    };

    //! An instance of this class represents all the parameters specified in the <b>Cluster</b> section of <tt>mascot.dat</tt>.
    /*!
     *  An instance of this class is created and polulated in ms_datfile.
     *  It can also be created separately and initialized with default values. 
     *  One can create an instance of the class or copy from another instance 
     *  in order to pass it then as an parameters-containing object.
     *  For detailed information on any of the options please consult Mascot
     *  manual.
     *
     *  Also get yourselves acquainted with the base class ms_customproperty. 
     *  It facilitates the following tasks:
     *
     *  <ul>
     *  <li>Retrieving an unsupported property.</li>
     *  <li>Retrieving a raw/text/XML property representation.</li>
     *  <li>Checking for existence of a certain property rather than 
     *  dealing with its default value.</li>
     *  <li>Accessing commented lines in a section.</li>
     *  </ul>
     *
     *  More functionality is described in the documentation for
     *  ms_customproperty.
     */
    class MS_MASCOTRESFILE_API ms_clusterparams: public ms_customproperty
    {
        friend class ms_datfile;
    public:
        //! Default constructor.
        ms_clusterparams();

        //! Copying constructor.
        ms_clusterparams(const ms_clusterparams& src);

        //! Destructor.
        ~ms_clusterparams();

        //! Initialises the instance with default values.
        void defaultValues();

        //! Can be used to create a copy of another object.
        void copyFrom(const ms_clusterparams* right);

#ifndef SWIG
        //! Assignment operator for C++ client applications.
        ms_clusterparams& operator=(const ms_clusterparams& right);
#endif
        //! Check whether the section has been actually read from the file.
        /*!
         * By default the <tt>Options</tt> section is unavailable until it has 
         * been set to a different state.
         */
        bool isSectionAvailable() const;

        //! Change availability of the section, i.e. if it should be saved in a file.
        void setSectionAvailable(const bool value);

        //! Returns <b>TRUE</b> if <b>Enabled</b> parameter is set to <b>1</b> and <b>FALSE</b> otherwise.
        /*!
         * Value of <b>1</b> is to enable cluster mode, whereas <b>0</b> is to
         * enable single server mode.  
         *
         * Default is <b>0</b>.
         */
        bool isEnabled() const;

        //! Change <b>Enabled</b>.
        /*!
         *  See #isEnabled() for more information.
         */
        void setEnabled(const bool flag);

        //! Returns an instance of ms_computeraddress created for <b>MasterComputerName</b>.
        /*!
         * The <b>MasterComputerName</b> parameter contains a host name for the
         * master computer and, optionally, the IP address separated by
         * a comma. See ms_computeraddress for information about its parameters
         * and default values.
         */
        const ms_computeraddress* getMasterComputer() const;

        //! Change <b>MasterComputerName</b>.
        /*!
         *  See #getMasterComputer() for more information.
         */
        void setMasterComputer(const ms_computeraddress* value);

        //! Returns value of <b>DefaultNodeOS</b> as one of the enumeration members.
        /*!
         * If no OS is defined for a particular node, then this parameter value
         * is assumed as the OS. See OPERATING_SYS for a list of possible
         * values.  
         *
         * Default is <b>_OS_WINDOWS_NT</b> (2).
         */
        OPERATING_SYS getDefaultNodeOS() const;

        //! Change <b>DefaultNodeOS</b>.
        /*!
         *  See #getDefaultNodeOS() for more information.
         */
        void setDefaultNodeOS(const OPERATING_SYS value);

        //! Returns the value of <b>DefaultNodeHomeDir</b>.
        /*!
         *  If no specific home directory is specified for a particular node in
         *  <tt>nodelist.txt</tt>, then the value of the
         *  <b>DefaultNodeHomeDir</b> parameter is used.  To override this
         *  setting for a particular node, enter the directory on the node line
         *  in <tt>nodelist.txt</tt>.
         *
         *  Default is the <tt>/MascotNode</tt> folder.
         *
         *  \sa #getDefaultPort()
         */
        std::string getDefaultNodeHomeDir() const;

        //! Change <b>DefaultNodeHomeDir</b>.
        /*!
         *  See #getDefaultNodeHomeDir() for more information.
         */
        void setDefaultNodeHomeDir(const char* str);

        //! Returns the value of <b>DefaultPort</b>.
        /*!
         *  If no port number is specified for a node in <tt>nodelist.txt</tt>, 
         *  then this port number will be used.
         *
         *  Default is <b>5001</b>.
         *
         *  \sa #getDefaultNodeHomeDir()
         */
        int getDefaultPort() const;

        //! Change <b>DefaultPort</b>.
        /*!
         *  See #getDefaultPort() for more information.
         */
        void setDefaultPort(const int value);

        //! Returns the value of <b>DefaultNodeHomeDirFromMaster</b>.
        /*!
         *  This is the directory on the node as seen from the master.  For
         *  a Windows cluster, this must be present and specified as a UNC
         *  name.  For a Unix cluster, this parameter must be commented out.
         *
         *  Default is \verbatim  \\<host_name>\c$\mascotnode \endverbatim
         */
        std::string getDefaultNodeHomeDirFromMaster() const;

        //! Change <b>DefaultNodeHomeDirFromMaster</b>.
        /*!
         *  See #getDefaultNodeHomeDirFromMaster() for more information.
         */
        void setDefaultNodeHomeDirFromMaster(const char* str);

        //! Returns the value of <b>MascotNodeScript</b>.
        /*!
         *  The <b>MascotNodeScript</b> parameter specifies a script name which 
         *  is run for each node with different parameters described
         *  in Mascot manual. 
         *
         *  By default this is empty.
         */
        std::string getMascotNodeScript() const;

        //! Change <b>MascotNodeScript</b>.
        /*!
         *  See #getMascotNodeScript() for more information.
         */
        void setMascotNodeScript(const char* str);

        //! Returns the value of <b>MascotNodeRebootScript</b>.
        /*!
         *  <b>MascotNodeRebootScript</b> is the name of an optional script 
         *  to re-boot a cluster node. If this parameter is defined, 
         *  then there will be a link at the bottom of each Mascot Cluster Node 
         *  status page. When this link is clicked, <tt>ms-monitor</tt>
         *  will run the defined script on the master. 
         *  The host name of the specified node will be passed to the script 
         *  as a parameter.
         *
         *  By default this is empty.
         */
        std::string getMascotNodeRebootScript() const;

        //! Change <b>MascotNodeRebootScript</b>.
        /*!
         *  See #getMascotNodeRebootScript() for more information.
         */
        void setMascotNodeRebootScript(const char* str);

        //! Returns the number of <b>SubClusterSet</b> parameter entries, which is a total number of sub-clusters.
        /*!
         *  Large clusters can be divided into sub-clusters. 
         *  There might be several <b>SubClusterSet</b> parameter entries
         *  on separate lines in the configuration file. 
         *  Each parameter line has the following format:
         *  \verbatim  SubClusterSet X Y \endverbatim
         *
         *  <tt>X</tt> is a unique integer value (0-based) used to identify the
         *  sub-cluster (from <b>0</b> to <b>49</b> inclusively).
         *
         *  <tt>Y</tt> is the maximum number of processors in the sub-cluster. 
         *
         *  A single cluster must have a single entry with <tt>X</tt> set to
         *  <b>0</b>.  If no such entries are present in the file, one
         *  subcluster is assumed - default entry (<b>0</b>,<b>0</b>).
         *  Therefore, default value returned by this function is <b>1</b>.
         *
         *  Use #getSubClusterID() and #getSubClusterMaxCPU() to
         *  retrieve the entries.
         */
        int getNumberOfSubClusters() const;

        //! Erases information about all sub clusters.
        /*!
         *  One entry (<b>0</b>,<b>0</b>) for "this subcluster" stays in the
         *  list forever.
         */
        void clearSubClusters();

        //! Returns sub-cluster ID by its index.
        /*!
         *  \param index a number between <b>0</b> and
         *  (#getNumberOfSubClusters()-1).
         *  \return an integer number which serves as a sub-cluster ID.
         */
        int getSubClusterID(const int index) const;

        //! Returns the <tt>Y</tt> part of <b>SubClusterSet</b>.
        /*!
         *  See #getNumberOfSubClusters() for more detailed explanations.
         *
         *  \param index a number in the list (from <b>0</b> to
         *  (#getNumberOfSubClusters()-1)).
         *  \return maximum number of CPUs for the specified sub-cluster
         *  (<b>0</b> by default).
         */
        int getSubClusterMaxCPU(const int index) const;

        //! Add a new <b>SubClusterSet</b> entry.
        /*!
         *  See #getNumberOfSubClusters() for more information.
         *
         *  \param id unique ID identifying a sub-cluster (from <b>0</b> to
         *  <b>49</b>) to set number of CPUs for.
         *  \param maxCPUs maximum number of CPUs for the specified sub-cluster
         *  (from <b>0</b> to <b>1024*64</b>).
         */
        void appendSubCluster(const int id, const int maxCPUs);

        // internal usage only
        int getThisSubClusterID() const;

        // internal usage only
        void setThisSubClusterID(const int id);

        //! Returns the value of <b>IPCTimeout</b>.
        /*!
         *  The <b>IPCTimeout</b> parameter is the timeout in seconds for
         *  inter-process communication.
         *
         *  Default is <b>10</b> seconds.
         *
         *  \sa #getIPCLogging(), #getIPCLogfile()
         */
        int getIPCTimeout() const;

        //! Change <b>IPCTimeout</b>.
        /*!
         *  See #getIPCTimeout() for more information.
         */
        void setIPCTimeout(const int value);

        //! Returns value of <b>IPCLogging</b>.
        /*!
         *  <b>IPCLogging</b> specifies logging level for 
         *  inter-process communications. It has the followings possible values:
         *  <ul>
         *  <li><b>0</b> for no logging of inter-process communication</li>
         *  <li><b>1</b> for for minimal logging</li>
         *  <li><b>2</b> for for verbose logging</li>
         *  </ul>
         *
         *  Default is <b>0</b>.
         *
         *  \sa #getIPCTimeout()
         */
        int getIPCLogging() const;

        //! Change <b>IPCLogging</b>.
        /*!
         *  See #getIPCLogging() for more information.
         */
        void setIPCLogging(const int value);

        //! Returns the value of <b>IPCLogfile</b>.
        /*!
         *  The <b>IPCLogfile</b> parameter specifies a relative path 
         *  to the inter-process communication log file.
         *
         *  Default is <tt>../logs/IPC.log</tt>.
         *
         *  \sa #getIPCTimeout()
         */
        std::string getIPCLogfile() const;

        //! Change <b>IPCLogfile</b>.
        /*!
         *  See #getIPCLogfile() for more information.
         */
        void setIPCLogfile(const char* str);

        //! Returns the value of <b>CheckNodesAliveFreq</b>.
        /*!
         *  The <b>CheckNodesAliveFreq</b> parameter specifies 
         *  the interval in seconds between 'health checks' on the nodes.
         *  Default value is <b>30</b> sec.
         */
        int getCheckNodesAliveFreq() const;

        //! Change <b>CheckNodesAliveFreq</b>.
        /*!
         *  See #getCheckNodesAliveFreq() for more information.
         */
        void setCheckNodesAliveFreq(const int value);

        //! Returns the value of <b>SecsToWaitForNodeAtStartup</b>.
        /*!
         *  The <b>SecsToWaitForNodeAtStartup</b> parameter specifies
         *  a timeout.  At startup, if a node is not available within this
         *  time, the system will continue to startup without that node.  If
         *  the value is set to <b>0</b>, then the system will wait
         *  indefinitely.
         *
         *  This timeout is also used if a node fails while the system is
         *  running.  The system will wait for this number of seconds before
         *  re-initialising <tt>ms-monitor.exe</tt>.  This means that
         *  a short-lived interruption in network communication doesn't create
         *  a major service interruption.
         *
         *  Default is <b>60</b> sec.
         */
        int getSecsToWaitForNodeAtStartup() const;

        //! Change <b>SecsToWaitForNodeAtStartup</b>.
        /*!
         *  See #getSecsToWaitForNodeAtStartup() for more information.
         */
        void setSecsToWaitForNodeAtStartup(const int value);

#ifdef SUPPRESS_MS_CUSTOMPROPERTY_INHERITANCE
#include "suppress_ms_customproperty.hpp"
#endif

    private:
        bool            sectionAvailable_;
        bool            enabled_;

        ms_computeraddress masterComputer_;

        OPERATING_SYS   defaultNodeOS_;
        std::string     szDefaultNodeHomeDir_;
        int             defaultPort_;
        std::string     szDefaultNodeHomeDirFromMaster_;
        std::string     szMascotNodeScript_;
        std::string     szMascotNodeRebootScript_;

        std::vector<int> subClusterIDs_;
        std::vector<int> maxCpusPerSubCluster_;
        bool             anySubCluster_;

        int             thisSubCluster_;

        int             IPCTimeout_;
        int             IPCLogging_;
        std::string     IPCLogfile_;
        int             checkNodesAliveFreq_;
        int             secsToWaitForNodeAtStartup_;
    }; // class ms_clusterparams
    /** @} */ // end of config_group
} // namespace matrix_science

#endif // MS_CLUSTERPARAMS_HPP

/*------------------------------- End of File -------------------------------*/
