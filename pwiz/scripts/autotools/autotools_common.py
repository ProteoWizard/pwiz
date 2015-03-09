#
# stuff useful for both autoconf and msvc build generation
#

pwizroot = ""
pwizroot_w = ""

def set_pwizroot(str) :
    global pwizroot
    global pwizroot_w
    pwizroot = str
    pwizroot_w = str.replace("/","\\").lower()

def get_pwizroot() :
    return pwizroot

# we want to deal in straight up code that could be found on any platform - don't pull in libgd etc
# also avoid any non-apache code like RAMP (LGPL) and vendor-supplied headers
forbidden=set(["bindings","mz5","Image.cpp","COM",".svn","automation_vector","MascotReaderTest","Reader_UIMF_Test","Reader_Shimadzu_Test","Pseudo2DGel","pwiz_tools\\commandline","pwiz_tools\\BiblioSpec","\\utility\\misc\\sha1calc.cpp","RegionAnalyzerTest","msbenchmark","data\\msdata\\ramp","hello_ramp","pwiz_aux","RAMPAdapter","MascotReader.cpp","Reader_Agilent_Detail","Reader_ABI_T2D_Detail"])
excepted=set(["pwiz_tools\\commandline\\msconvert","pwiz_tools\\commandline\\idconvert","pwiz_tools\\commandline\\pepcat","\\Version.","ExtendedReaderList"])
welcomeIncludes=set(["pwiz\\pwiz","pwiz\\data","libraries\\zlib","libraries\\libsvm","libraries\\boost_aux","findmf"])
welcomeSrcDirs=set(["pwiz\\data","pwiz\\analysis","pwiz\\utility","pwiz_tools\\examples\\","pwiz_tools\\common\\","libraries\\libsvm"])

# include the whole boost_aux tree, and others with depth but no -I reference
complicatedTrees = ["boost_aux"]
subtleIncludes = ["pwiz_aux","pwiz/utility/findmf"]

# boost aux source files we need to compile
boostAuxSources = ["libraries/boost_aux/libs/nowide/src/iostream.cpp"]


def contains_none(line,badwords) :
	for bad in badwords :
		if bad in line :
			return False
	return True

def contains_pwizroot(str) :
    s = str.lower()
    return pwizroot.lower() in s or pwizroot_w in s # case insensitive

def contains_any(line,goodwords) :
	return not contains_none(line,goodwords)

def isWelcomeSrcDir(str) :
    s = str.replace("/","\\")
    return contains_any(s,welcomeSrcDirs) or contains_any(s,welcomeIncludes) or contains_any(s,excepted)

def isWelcomeInclude(str) :
    return contains_any(str.replace("/","\\"),welcomeIncludes) 

def isNotForbidden(str) :
    s = str.replace("/","\\")
    return contains_none(s,forbidden) or contains_any(s,excepted)

def isSrcFile(filestr) :
    file = filestr.replace("/","\\")
    if isNotForbidden(file) :
        if ("\\pwiz\\pwiz\\" in file):
            return True
        if ("\\pwiz\\libraries\\libsvm" in file):
            if ("svm." in file) or ("COPYRIGHT" in file) or ("README" in file):
                return True
        if ("\\common\\" in file) :
            return True
        if contains_any(file,welcomeSrcDirs) or contains_any(file,excepted) :
            return True
    return False

def isExampleFile(filestr) :
    file = filestr.replace("/","\\")
    return isNotForbidden(file) and (("pwiz_tools\\examples\\" in file) or ("pwiz_tools\\commandline\\" in file))

def isTestFile(filestr) :
    file = filestr.replace("/","\\")
    return isNotForbidden(file)  and ("Test." in file or "test." in file)


def replace_pwizroot(str,repl) : # case insensitive
	if contains_pwizroot(str) :
		import re
		pattern = re.compile(get_pwizroot().replace("\\","\\\\"), re.IGNORECASE)
		ret = pattern.sub(repl,str)
		if repl in ret :
			if not ret.startswith(repl) : # perhaps a stray drive letter
				if ret[1] == ":" :
					ret = ret[2:]
		return ret
	return str

import subprocess

def runcmd(cmd) :
	print ('run "'+cmd+'"...\n')
	p=subprocess.Popen(cmd.split(' '),stdout=subprocess.PIPE,stderr=subprocess.PIPE)
	outs, errors = p.communicate()
	print (outs)
	print (errors)
	print ('done with "'+cmd+'".\n')
