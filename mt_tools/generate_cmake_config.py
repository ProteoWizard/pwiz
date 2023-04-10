
import os
import argparse
import sys

def get_dlls(fdir):
    dll_files = []
    
    # Get all dll files in fdir

    for file in os.listdir(fdir):
        if os.path.isfile(os.path.join(fdir, file)):
            if file.endswith('.dll'):
                dll_files.append(file)

    # remove all files with names starting with 'api-ms-win-' from the list

    # dll_files = [x for x in dll_files if not x.startswith('api-ms-win-') 
    #              and not x.startswith('vcruntime')
    #              and not x.startswith('msvc')
    #              and not x.startswith('msvcrt')
    #              and not x.startswith('vcomp')
    #              and not x.startswith('ucrt')]
    
    # dll_files = [x for x in dll_files if not x.startswith('api-ms-win-')]
    # for i in range(len(lib_files)):
    #     lib_files[i] = '${pwiz_LIB_PREFIX}/' + lib_files[i]                

    return dll_files
    


parser = argparse.ArgumentParser()

# Add argument for the path to the data directory
parser.add_argument('--pwiz_dir', type=str, default='../', help='Path to the pwiz directory')

args = parser.parse_args()

print(args) 

# Get the path to the pwiz directory

pwiz_dir = args.pwiz_dir

build_dir = pwiz_dir + '/build-nt-x86'

print(build_dir)

# check that the build directory exists 

if not os.path.exists(build_dir):
    print('Build directory does not exist')
    sys.exit(1)
     
# Get all *.lib files in build directory and subdirectories 

lib_files = []

for root, dirs, files in os.walk(build_dir):
    for file in files:
        if file.endswith('.lib'):
            lib_files.append(os.path.join(root, file))


# Find common prefix of all lib files

common_prefix = os.path.commonprefix(lib_files)

print('Common prefix: ' + common_prefix)

# remove common prefix from all lib files


for i in range(len(lib_files)):
    lib_files[i] = lib_files[i].replace(common_prefix, '')

# remove leading slash from all lib files

for i in range(len(lib_files)):
    lib_files[i] = lib_files[i].lstrip('/')

# change \\ to / in all lib files

for i in range(len(lib_files)):
    lib_files[i] = lib_files[i].replace('\\', '/')

# prepend '${pwiz_LIB_PREFIX}/' to all lib files

for i in range(len(lib_files)):
    lib_files[i] = '${pwiz_LIB_PREFIX}/' + lib_files[i]

# remove all files with 'test' in the name from the list

print('Number of lib files: ' + str(len(lib_files)))

lib_files = [x for x in lib_files if 
             'Test.lib' not in x and 
             'test.lib' not in x and 
             '_cli' not in x
            ]



print('Number of lib files: ' + str(len(lib_files)))

release_lib_files = [x for x in lib_files if '/dbg/' not in x]
debug_lib_files = [x for x in lib_files if '/rls/' not in x]

release_bin_dir = build_dir + '/msvc-release-x86_64'
debug_bin_dir = build_dir + '/msvc-debug-x86_64'

if os.path.exists(release_bin_dir):
    print('release_bin_dir directory  exist')

release_dll_files = get_dlls(release_bin_dir);
debug_dll_files = get_dlls(debug_bin_dir);

print('Number of release dll files: ' + str(len(release_dll_files)))
print('Number of debug dll files: ' + str(len(debug_dll_files)))

for i in range(len(release_dll_files)):
    release_dll_files[i] = '${pwiz_LIB_PREFIX}/msvc-release-x86_64/' + release_dll_files[i]

for i in range(len(debug_dll_files)):
    debug_dll_files[i] = '${pwiz_LIB_PREFIX}/msvc-debug-x86_64/' + debug_dll_files[i]

# write pwiz-config.cmake   

with open('pwiz-config.cmake', 'w') as f:
    f.write('set(pwiz_INCLUDE_DIRS\n') 
    f.write('    ${CMAKE_CURRENT_LIST_DIR}\n')
    f.write('    ${CMAKE_CURRENT_LIST_DIR}/libraries/boost_1_76_0\n')
    f.write('    ${CMAKE_CURRENT_LIST_DIR}/libraries/boost_aux\n')
    f.write('    )\n')
    f.write('\n')
 
    f.write('set(pwiz_LIB_PREFIX ${CMAKE_CURRENT_LIST_DIR}/build-nt-x86)\n')
    f.write('set(pwiz_LIBS_DEBUG\n')

    for lib_file in debug_lib_files:
        f.write('    ' + lib_file + '\n')

    f.write('    )\n')
    f.write('\n')

    f.write('set(pwiz_LIBS_RELEASE\n')

    for lib_file in release_lib_files:
        f.write('    ' + lib_file + '\n')

    f.write('    )\n')
    f.write('\n')
    
    f.write('set(pwiz_REDISTR_DEBUG\n')
    for file in debug_dll_files:
        f.write('    ' + file + '\n')

    f.write('    )\n')
    f.write('\n')

    f.write('set(pwiz_REDISTR_RELEASE\n')
    for file in release_dll_files:
        f.write('    ' + file + '\n')

    f.write('    )\n')
    f.write('\n')

    f.write('if (CMAKE_BUILD_TYPE STREQUAL "Debug")\n')
    f.write('    set(pwiz_LIBS ${pwiz_LIBS_DEBUG})\n')
    f.write('    set(pwiz_REDISTR ${pwiz_REDISTR_DEBUG})\n')
    f.write('else()\n')
    f.write('    set(pwiz_LIBS ${pwiz_LIBS_RELEASE})\n')
    f.write('    set(pwiz_REDISTR ${pwiz_REDISTR_RELEASE})\n')
    f.write('endif()\n')

# read version from Version.cpp

version_major = '0'
version_minor = '0'
version_build = '0'

version_file = pwiz_dir + '/pwiz/Version.cpp'

have_major = False
have_minor = False
have_revision = False

with open(version_file, 'r') as f:
    for line in f:
        if 'Major' in line and not have_major:
            version_major_str = line.split()[3]
            version_major = version_major_str.split(';')[0]
            have_major = True
        if 'Minor' in line and not have_minor:
            version_minor_str = line.split()[3]
            version_minor = version_minor_str.split(';')[0]
            have_minor = True
        if 'Revision' in line and not have_revision:
            version_build_str = line.split()[3]
            version_build = version_build_str.split(';')[0]
            have_revision = True

version_str = str(version_major) + '.' + str(version_minor) + '.' + str(version_build)
print('version_str: ' + version_str)

# write pwiz-config-version.cmake

with open('pwiz-config-version.cmake', 'w') as f:
    f.write('set(PACKAGE_VERSION ' + version_str + ')\n')
    f.write('if("${PACKAGE_FIND_VERSION_MAJOR}" EQUAL ' + version_major + ')\n')
    f.write('    set(PACKAGE_VERSION_COMPATIBLE 1)\n')
    f.write('endif("${PACKAGE_FIND_VERSION_MAJOR}" EQUAL ' + version_major + ')\n')
    f.write('\n')


# set(PACKAGE_VERSION 3.0.22166)
# if("${PACKAGE_FIND_VERSION_MAJOR}" EQUAL 3)
#   set(PACKAGE_VERSION_COMPATIBLE 1) # compatible with any version 1.x
#   if("${PACKAGE_FIND_VERSION_MINOR}" EQUAL 0)
#     set(PACKAGE_VERSION_EXACT 1)    # exact match for version 1.3
#   endif("${PACKAGE_FIND_VERSION_MINOR}" EQUAL 0)
# endif("${PACKAGE_FIND_VERSION_MAJOR}" EQUAL 3)






    




    





