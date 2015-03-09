# Example makefile for building hello_analyzer.cpp on Linux with gcc


CC = g++
APP = hello_pwiz
SRC = hello_pwiz.cpp
INSTALL_DIR = installed-libs
ROOT = ../../../
CFLAGS = -I$(INSTALL_DIR)/include/pwiz -I$(INSTALL_DIR)/include/boost -I${ROOT}
LDFLAGS = -L$(INSTALL_DIR)/lib \
          -lpwiz_data_msdata \
          -lpwiz_data_common \
          -lboost_system-gcc44-mt-s \
          -lboost_iostreams-gcc44-mt-s \
          -lpwiz_utility_misc \
          -lboost_filesystem-gcc44-mt-s \
          -lpwiz_utility_minimxml \
          -lz-gcc44-mt-s-1_2 \
          -lboost_thread-gcc44-mt-s \
          -lboost_regex-gcc44-mt-s \
          -pthread \
          -lpwiz_data_msdata_version

${APP}: ${SRC} ${INSTALL_DIR}
	${CC} ${CFLAGS} ${SRC} ${LDFLAGS} -o ${APP}

${INSTALL_DIR}:
	cd ${ROOT} ; ./quickbuild.sh libraries --prefix=doc/technical/hello_pwiz/${INSTALL_DIR}
