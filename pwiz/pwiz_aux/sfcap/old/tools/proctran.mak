#
# proctran.mak
#

include project_paths.inc

TARGET = proctran.exe

SOURCES = \
  proctran.cpp

OPTIONS = -O2 -Werror 

LINKOPTIONS = -Wl,--allow-multiple-definition

# --allow-multiple-definition is necessary for "multiple definition" error
# (Cygwin gcc 3.4.4, inclusion of Boost 1.34.1 filesystem headers)

ARCHIVES = 

LIBS = pwiz_peaks pwiz_calibration pwiz_proteome pwiz_math \
       pwiz_data pwiz_util \
       fftw3 \
       boost_filesystem-gcc boost_program_options-gcc boost_serialization-gcc boost_iostreams-gcc
        

include $(MSTOOLS_ROOT)/src/make/make_binary.inc

