/* This is a SWIG interface file for exposing pwiz library function points to java via
   SWIG (could also be used for Python, Perl, etc).

   $Id$

   There are two sections - declarations of things you want literally included in
   the wrapper code, and things you want magically turned into java (or python etc).
   These lists tend to be the same, but its possible that you'd need to include some
   declarations of private stuff in the first list but not the second.
   
   Note that if there are things in your headers that you don't want SWIG to deal
   with, just surround them #ifndef SWIG / #endif .
   
   by Brian Pratt 2009 Insilicos LLC (see the Proteowizard code being wrapped for license info)
*/  


/* set the module name - this needs to match the name of the DLL containing the
   the code that the module will look to for its inner workings */
%module pwiz_swigbindings


/* STL support - generates smarter wrappers for functions that return std::string etc */
%include "std_deque.i" 
%include "std_map.i" 
%include "std_pair.i"
%include "std_string.i" 
%include "std_vector.i" 
namespace std {
   %template(vectord) vector<double>;
};


%{
  /* everything in this section (including this comment) appears in the generated
     wrapper code, so here's where you declare the C/C++ functions that the wrapper
     will find in the DLL.
     $Id$
     Note that if there are things in your headers that you don't want SWIG to deal
	 with, just surround them #ifndef SWIG / #endif .  
	 */
#include "pwiz_RAMPAdapter.hpp"
#include "../../../data/common/cv.hpp"
#include "../../../data/common/ParamTypes.hpp"
#include "../../../data/msdata/MSData.hpp"
using namespace pwiz;
using namespace pwiz::msdata;
using namespace pwiz::data;

%}

/* and here put declarations of things you want SWIG to express in the target language 
  (tends to be the same list as above) 
   Note that if there are things in your headers that you don't want SWIG to deal
   with, just surround them #ifndef SWIG / #endif .  
  */
#define RAMP_STRUCT_DECL_ONLY
#define PWIZ_API_DECL
%include "../../../data/msdata/ramp/ramp.h" 
%include "pwiz_RAMPAdapter.hpp"
