/*
##############################################################################
# file: ms_mascotrespeptide.hpp                                              #
# 'msparser' toolkit                                                         #
# Encapsulates a Unigene file (from NCBI)                                    #
#                                                                            #
##############################################################################
# COPYRIGHT NOTICE                                                           #
# Copyright 1998-2003 Matrix Science Limited  All Rights Reserved.           #
#                                                                            #
##############################################################################
#    $Archive:: /Mowse/ms_mascotresfile/include/ms_mascotresunigene.hpp    $ #
#     $Author: davidc $ #
#       $Date: 2010-07-22 14:22:18 $ #
#   $Revision: 1.11 $ #
# $NoKeywords::                                                            $ #
##############################################################################
*/

#ifndef MS_MASCOTRESUNIGENE_HPP
#define MS_MASCOTRESUNIGENE_HPP

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
#include <stdio.h>
#include <string>
#include <vector>



namespace matrix_science {
    class ms_tinycdb;

    /** @addtogroup resfile_group
     *  
     *  @{
     */

    //! This class encapsulates a single entry from a unigene file
    /*!
     * A number of objects of this class will be created when
     * a ms_unigene object is created.
     *
     */
    class MS_MASCOTRESFILE_API ms_unigene_entry
    {
        public:
#ifdef __MINGW__
            //! The constructor should only be called from within the library. MINGW compiler crashes without 'inline'
            inline ms_unigene_entry(FILE * f, ms_unigene & unigene);
#else
            //! This constructor should only be called from within the library
            ms_unigene_entry(FILE * f, ms_unigene & unigene);
#endif
            //! This constructor should only be called from within the library
            ms_unigene_entry(const std::string & id,
                             const std::string & title,
                             const std::string & gene,
                             const std::string & cytoBand,
                             const std::string & locusLink,
                             const std::string & express,
                             const long          chromosome,
                             const int           numAccessions,
                             const OFFSET64_T    fileOffset);

            //! Destructor
            ~ms_unigene_entry();

            //! Return the number of accessions (gi numbers or EMBL accessions) that comprise this entry
            /*!
             * The accessions themselves cannot be returned from this object. 
             * See ms_unigene::findEntry
             * \return The number of accessions
             */
            int getNumAccessions() const     { return numAccessions_; }

            //! Return the 'ID' of this entry - e.g. Hs.4
            /*!
             * \return the ID (also called accession) for the entry
             */
            std::string getID() const        { return id_;                }

            //! Return the 'title' of this entry - e.g. "alcohol dehydrogenase..."
            /*!
             * \return a readable name for the entry.
             */
            std::string getTitle() const     { return title_;             }

            //! Return the gene name for this entry - e.g. "ADH1B"
            /*!
             * \return the gene name
             */
            std::string getGene() const      { return gene_;              }

            //! Return the CYTOBAND - e.g. 4q21-q23
            /*!
             * \return the CYTOBAND
             */
            std::string getCytoBand() const  { return cytoBand_;          }

            //! Return the LocusLink - e.g. 125
            /*!
             * Seems to be identical to the gene number in most cases?
             * \return 
             */
            std::string getLocusLink() const { return locuslink_;         }

            //! Return the EXPRESS entry. Can be very long - 5000 bytes
            /*!
             * For example, can be of the form:  \verbatim
               adipose tissue| blood| bone marrow| brain| connective tissue| dorsal
               \endverbatim
             * \return the express entry string.
             */
            std::string getExpress() const   { return express_;           }

            //! Return the chromosome that contains the unignene entry
            /*!
             * \return the chromosome number
             */
            long getChromosome() const       { return chromosome_;        }

            //! Return the offset into the unigene data file for this entry
            /*!
             * This function returns quickly if an index file has been created.
             * \return the number of bytes into the file where this entry starts
             */
            OFFSET64_T getFileOffset() const { return fileOffset_;        }

        protected:
            // Not safe to copy or assign this object.
#ifndef SWIG
            ms_unigene_entry(const ms_unigene_entry & rhs);
            ms_unigene_entry & operator=(const ms_unigene_entry & rhs);
#endif
        private:
            //! Private function to parse a string from an input line
            bool getString(const char * buf, const char * id,
                           const int idLen,  std::string &res);

            //! Private function to parse a long integer from an input line
            bool getLong(const char * buf, const char * id, 
                         const int idLen,  long &res);

            //! Private function to parse a gi number and accession from an input line
            bool getAccessions(const char * buf, const char * id, const int idLen,  
                               std::string & giNumber, std::string & accession);
            std::string id_;
            std::string title_;
            std::string gene_;
            std::string cytoBand_;
            std::string locuslink_;
            std::string express_;
            long        chromosome_;
            int         numAccessions_;
            OFFSET64_T  fileOffset_;
    };

    //! This class encapsulates a complete unigene file
    /*!
     * Creating one of these objects reads in the unigene file,
     * creating a number of ms_unigene_entry objects. The gi and EMBL accessions
     * are all indexed.
     */
    class MS_MASCOTRESFILE_API ms_unigene
    {
        friend class ms_unigene_entry;

        public:
            //! The constructor for a unigene object
            ms_unigene(ms_mascotresfile  &resfile, const char * filename);

            //! Destructor
            ~ms_unigene();

            //! Given an accession, return a pointer to the relevant unigene entry
            const ms_unigene_entry * findEntry(const char * id);

            //! Return the Unigene 'accession' (ID) for a given EST accession
            std::string getUnigeneForAccession(const std::string accession, 
                                               const int index);

        protected:
            // Not safe to copy or assign this object.
#ifndef SWIG
            ms_unigene(const ms_unigene & rhs);
            ms_unigene & operator=(const ms_unigene & rhs);
#endif
    private:
            ms_mascotresfile  &resfile_;
            std::vector<ms_unigene_entry *> entries_;
            std::string filename_;
            int numAccessions_;
            ms_tinycdb * pcdb_;
            typedef std::multimap<std::string, const ms_unigene_entry *> unigenesForAcc;
            unigenesForAcc accessionToUnigene_;

            //! Used internally to create a lookup of gi numbers to a list of accessions
            void addAccessionUnigenePair(const ms_unigene_entry * unigene, 
                                         const std::string & accession);
    };
    /** @} */ // end of resfile_group
}   // matrix_science namespace

#endif // MS_MASCOTRESUNIGENE_HPP

/*------------------------------- End of File -------------------------------*/
