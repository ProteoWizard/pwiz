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

# we want to deal in straight up code that could be found on any platform
forbidden=set(["bindings","mz5","Image.cpp","COM","automation_vector","Pseudo2DGel","pwiz_tools\\commandline","\\utility\\misc\\sha1calc.cpp"])
excepted=set(["pwiz_tools\\commandline\\msconvert","\\Version."])
welcomeIncludes=set(["pwiz\\pwiz","pwiz\\data","libraries\\zlib","libraries\\libsvm","libraries\\boost_aux"])
welcomeSrcDirs=set(["pwiz\\data","pwiz\\analysis","pwiz\\utility","pwiz_tools\\examples\\"])

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
            if ("svm." in file):
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