#ifndef VISIBILITY_DECL_H_78A6D83FD1F447999B187B28E3CDB981
#define VISIBILITY_DECL_H_78A6D83FD1F447999B187B28E3CDB981

#if defined(BDAL_DOXYGEN_INVOKED)
// See:
// http://gcc.gnu.org/wiki/Visibility
// for more technical details of visibility control on different systems.

/// @brief Declaration specifier for exported entities.
/**
 * An object-like macro that can be used to mark a declaration as usable
 * for exported entities. This ensures that the entity is exported from
 * shared libraries.
 **/
# define BDAL_VIS_EXPORT_DECL /**< @hideinitializer */

/// @brief Declaration specifier for imported entities.
/**
 * An object-like macro that can be used to mark a declaration as usable
 * for imported entities. This ensures that the entity is imported from
 * shared libraries.
 **/
# define BDAL_VIS_IMPORT_DECL /**< @hideinitializer */

/// @brief Declaration specifier for local entities.
/**
 * An object-like macro that can be used to mark a declaration as usable
 * for local entities only. Such entities are intended not to be visible outside
 * of a shared library (even though they have external linkage). For entities
 * with internal linkage this declaration specifier is not needed nor useful.
 **/
# define BDAL_VIS_LOCAL_DECL  /**< @hideinitializer */

/// @brief Declaration specifier for public entities.
/**
 * An object-like macro that can be used to mark a declaration as usable
 * for globally known entities. Such entities are intended to be visible outside
 * of a shared library, such as exception types.
 **/
# define BDAL_VIS_GLOBAL_DECL  /**< @hideinitializer */
#else
#  if defined(_WIN32) || defined(__WIN32__) || defined(WIN32) || defined(MSC_VER) || defined(__CYGWIN__)
#    define BDAL_VIS_EXPORT_DECL __declspec(dllexport)
#    define BDAL_VIS_IMPORT_DECL __declspec(dllimport)
#    define BDAL_VIS_LOCAL_DECL
#    define BDAL_VIS_GLOBAL_DECL
#  elif __GNUC__ >= 4
#    define BDAL_VIS_EXPORT_DECL __attribute__((visibility("default")))
#    define BDAL_VIS_IMPORT_DECL __attribute__((visibility("default")))
#    define BDAL_VIS_LOCAL_DECL  __attribute__((visibility("hidden")))
#    define BDAL_VIS_GLOBAL_DECL __attribute__((visibility("default")))
#  else
#    define BDAL_VIS_EXPORT_DECL
#    define BDAL_VIS_IMPORT_DECL
#    define BDAL_VIS_LOCAL_DECL
#    define BDAL_VIS_GLOBAL_DECL
#endif

#endif

#endif /* VISIBILITY_DECL_H_78A6D83FD1F447999B187B28E3CDB981 */
