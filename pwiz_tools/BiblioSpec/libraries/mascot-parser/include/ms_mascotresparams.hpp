/*
##############################################################################
# file: ms_mascotresparams.hpp                                               #
# 'msparser' toolkit                                                         #
# Encapsulates the parameters & masses sections from the mascot results file #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2002 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /MowseBranches/ms_mascotresfile_1.2/include/ms_mascotrespa $ #
#     $Author: villek $ #
#       $Date: 2010-09-06 16:18:57 $ #
#   $Revision: 1.14 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTRESPARAMS_HPP
#define MS_MASCOTRESPARAMS_HPP

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

// Includes from the standard template library
#include <string>

namespace matrix_science {

    /** @addtogroup resfile_group
     *  
     *  @{
     */

    //! This class encapsulates the search parameters in the mascot results file.
    /*!
     * Although all these parameters could be obtained using the
     * lower level functions in the ms_mascotresfile class, it is
     * generally more convenient to use this object.
     */

    class MS_MASCOTRESFILE_API ms_searchparams
    {
        public:
            //! Either explicitily create an object using this constructor or call ms_mascotresfile.params().
            ms_searchparams(const ms_mascotresfile &resfile);

            //! Should never need to call this destructor directly.
            ~ms_searchparams() {};

            //! Returns the Mascot license string.
            std::string  getLICENSE();

            //! This is the comment or "Search title" entered by the user in the search form.
            std::string  getCOM();

            //! Returns the segment or protein mass. 
            int          getSEG();

            //! Returns the peptide tolerance for the search.
            double       getTOL();

            //! Returns the peptide tolerance units for the search.
            std::string  getTOLU();

            //! Returns the tolerance for msms ions.
            double       getITOL();

            //! Returns the units that the ions tolerance is specified in.
            std::string  getITOLU();

            //! Returns the number of missed cleavage points allowed for in a search.
            int          getPFA();

            //! Returns one of the database names used for the search.
            std::string  getDB(int idx = 1);

            //! Returns total number of databases used for the search.
            int getNumberOfDatabases();

            //! Returns a comma separated list of the fixed modifications used in a search.
            std::string  getMODS();

            //! Returns whether monoisotopic or average masses have been specified.
            std::string  getMASS();

            //! Returns either MASS_TYPE_MONO or MASS_TYPE_AVE.
            MASS_TYPE    getMassType();

            //! Returns the name of the enzyme used for the search. 
            std::string  getCLE();

            //! Returns the name of the uploaded file (if any).
            std::string  getFILENAME();

            //! Currently returns an empty string.
            std::string  getQUE();

            //! Returns the type of search -- this will be PMF, SQ, MIS.
            std::string  getSEARCH();

            //! Returns the user name (if any) as entered in the search form.
            std::string  getUSERNAME();

            //! Returns the user's email address (if any) as entered in the search form.
            std::string  getUSEREMAIL();

            //! Returns the user's \c USERID.
            int getUSERID();

            //! Returns the charge state of the peptide masses entered.
            std::string  getCHARGE();

            //! For a repeat search, it may be necessary to know the "parent" search.
            std::string  getINTERMEDIATE();

            //! Returns the number of results to show in the report.
            int          getREPORT();

            //! Returns true if the user selected the option to show the overview table.
            bool         getOVERVIEW();

            //! Returns the file format type selected by the user.
            std::string  getFORMAT();

            //! Returns the version of the form used to submit the search.
            std::string  getFORMVER();

            //! Returns a comma separated list of the variable modifications used in a search.
            std::string  getIT_MODS();

            //! Returns the user fields \c USER00, \c USER01, \c USER02 ... \c USER12.
            std::string  getUSERField(int num);

            //! Returns all the 'user defined' parameters.
            std::string getAllUSERParams() const;

            //! Returns the precursor mass for an MS-MS query.
            double       getPRECURSOR();

            //! Returns the name of the taxonomy selection.
            std::string  getTAXONOMY();

            //! Returns the type of report requested by the user. 
            std::string  getREPTYPE();

            //! This function returns a comma separated list of accession strings.
            std::string  getACCESSION();

            //! Returns the sub cluster number.
            int          getSUBCLUSTER();

            //! Returns true if the user selected the ICAT button on the search form.
            bool         getICAT();

            //! Returns the instrument name that the user selected.
            std::string  getINSTRUMENT();

            //! Returns 'true' for an error tolerant search.
            bool         getERRORTOLERANT();

            //! Returns as a comma separated list, the rules / ions series used for a search.
            std::string  getRULES();

            //! Returns the minimum mass to be considered for internal fragments
            double getMinInternalMass();

            //! Returns the maximum mass to be considered for internal fragments
            double getMaxInternalMass();

            //! Returns the mass of the specified residue (must be uppercase letter A-Z).
            double       getResidueMass(char residue);

            //! Returns the C terminus mass. 
            double       getCTermMass();

            //! Returns the N terminus mass. 
            double       getNTermMass();

            //! Returns the mass of nitrogen as specified in the <code>mascot/config/masses</code> file. 
            double       getHydrogenMass() const;

            //! Returns the mass of oxygen as specified in the <code>mascot/config/masses</code> file. 
            double       getOxygenMass() const;

            //! Returns the mass of carbon as specified in the <code>mascot/config/masses</code> file. 
            double       getCarbonMass() const;

            //! Returns the mass of nitrogen as specified in the <code>mascot/config/masses</code> file. 
            double       getNitrogenMass() const;

            //! Returns the mass of an electron as specified in the <code>mascot/config/masses</code> file.
            double       getElectronMass() const;

            // Returns the name of the specified variable modification selected by the user.
            std::string  getVarModsName(int num);

            //! Returns the delta mass for a variable modification.
            double       getVarModsDelta(int num);

            //! Returns the first neutral loss value for the specified variable modification.
            double       getVarModsNeutralLoss(int num);

            //! Returns all the neutral loss values for the specified variable modification.
            std::vector<double> getVarModsNeutralLosses(int num);

            //! Returns the peptide neutral loss value(s) for the specified variable modification.
            std::vector<double> getVarModsPepNeutralLoss(int num);

            //! Returns the required peptide neutral loss value(s) for the specified variable modification.
            std::vector<double> getVarModsReqPepNeutralLoss(int num);

            // Returns the name of the specified fixed modification selected by the user.
            std::string  getFixedModsName(int num);

            //! Returns the delta mass for a fixed modification.
            double       getFixedModsDelta(int num);

            //! Returns the neutral loss value for the specified fixed modification.
            double       getFixedModsNeutralLoss(int num);

            //! Returns the residues modified for the specified fixed modification.
            std::string  getFixedModsResidues(int num);

            //! Returns the filename of the 'parent' search for an error tolerant search.
            std::string  getErrTolParentFilename();

            //! Returns the name of the quantitation method used.
            std::string  getQUANTITATION() const;

            //! Returns a value showing how a peak detection error is handled.
            int  getPEP_ISOTOPE_ERROR() const;

            //! Returns a value showing if a decoy database has also been searched.
            int  getDECOY() const;

        protected:
            const ms_mascotresfile &resfile_;
            // Not safe to copy or assign this object.
#ifndef SWIG
            ms_searchparams(const ms_searchparams & rhs);
            ms_searchparams & operator=(const ms_searchparams & rhs);
#endif

        private:
            std::string decryptNumber(const std::string & input) const;
    };
    /** @} */ // end of resfile_group
}   // matrix_science namespace

#endif // MS_MASCOTRESPARAMS_HPP

/*------------------------------- End of File -------------------------------*/
