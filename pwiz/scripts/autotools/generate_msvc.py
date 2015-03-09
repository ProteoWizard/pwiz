# This is about producing a Visual Studio standard library project for 
# using pwiz in non-Proteowizard projects that don't use bjam.
# We do this by observing a bjam -d+2 build in operation
# and making MSVC config files from that.
#
# args are:
#   full_path_to_pwiz_root (where scripts, pwiz_tools, pwiz etc are found)
#   optional -d flag for debug
#   optional full_path_to_msvcbuild_log
#   optional list of msvc versions to build for
#
# places resulting .vc(x)proj and .sln build files in <full_path_to_pwiz_root>/msvc
# and creates a distribution zip file in <full_path_to_pwiz_root>/build-nt-x86
#
# thanks to http://weblog.latte.ca/static/blake/refreshvcproj.py
#
# We assume the presence of an already-in-use Boost library, such as that
# available for download at https://sourceforge.net/projects/boost/files/boost-binaries/
#
# Note that you also have to arrange to build the boost-nowide library, which is not yet part of the official boost release.

import os
import sys
import shutil
import xml.dom.minidom
import zipfile
import stat
import subprocess

import autotools_common as ac

configs=["Debug|Win32","Release|Win32"]

files_not_to_be_shipped=["pwiz\\pwiz.vcproj","pwiz\\pwiz.sln"] # these are just confusing bjam wrappers

dbug = False

buildversion="" # until we find it in the log file

args = sys.argv

if "-d" in args :
	dbug = True
	print ("-d debug option enabled")
	for i in range(1,len(args)) :
		if "-d"==args[i] :
			for j in range(i,len(args)-1) :
				args[j] = args[j+1]
	args = args[:len(args)-1]

if (len(args) == 1) :
	print ("usage: %s <pwizroot> [-d] [<msvc_build_log> <msvc_ver(8,9,10 or 12)]"%sys.argv[0])
	print (" if build log isn't provided we'll make one by doing a clean bjam build.")
	print (" if msvc version isn't provided we'll produce msvc8, msvc9, msvc10 and msvc12 projects.")
	print (" pwizroot is usually \ProteoWizard\pwiz (not \ProteoWizard or \ProteoWizard\pwiz\pwiz).")
	quit(1)
	
ac.set_pwizroot(args[1])

msvc_versions = [12, 10, 9, 8] # build files for MSVC12,10,9,8

buildlog_lines = []

if (len(args) >= 3) :
	buildlog = args[2]
	print ("using build log %s\n"%buildlog)
	buildlog_lines = open(buildlog).readlines()
	if (len(args) >= 4) :
		v = int(args[3])
		msvc_versions = range(v,v+1)
else : # no build log provided, go make one
	print ("performing clean: clean.bat\n")
	here = os.getcwd()
	os.chdir(ac.get_pwizroot())
	os.system("clean.bat")
	buildcmd="quickbuild.bat --without-binary-msdata -d+2"
	print ("performing build: %s\n"%buildcmd)
	p=subprocess.Popen(buildcmd,stdout=subprocess.PIPE,stderr=subprocess.PIPE)
	outs, errors = p.communicate()
	buildlog_lines = outs.split("\r\n")
	print (errors)
	os.chdir(here)

srcs=set()
testhelpers=set()
testargs=dict()
libnames=set()
projects=set()
includes=set([".","$(ProjectDir)..","$(BOOST_ROOT)","$(ZLIB_SOURCE).","$(ProjectDir)..\\libraries\\boost_aux"])
# BOOST_DISABLE_ASSERTS prevents weirdness in debug boost 1_46 in which we're supposed to present our own assertion_failed_msg() implementation
defines=["WITHOUT_MZ5","WINDOWS_NATIVE","_SCL_SECURE_NO_WARNINGS","_CRT_SECURE_NO_DEPRECATE","_USE_MATH_DEFINES","BOOST_DISABLE_ASSERTS","BOOST_NOWIDE_NO_LIB=1","NO_MASCOT_READER"]
disableWarnings=["4996","4244","4355","4800","4146","4748"]
shipdirs=set() # set of directories of interest

testhelpers_projectname = "testhelpers_lib"
zlib_projectname = "zlib"
libpwiz_projectname = "libpwiz"
alltests_projectname = "alltests"
GUIDs = dict()
GUIDs[zlib_projectname]="{499C8B4E-B459-40C5-A6A8-ACBFC45E5E07}"
GUIDs[alltests_projectname]="{A88ECD1F-9D91-4CAE-ABD3-7200B99AD82A}"
GUIDs[libpwiz_projectname]="{FAB19452-D16F-412D-8465-03552CEA2089}"
GUIDs[testhelpers_projectname]="{C44C75B2-43AA-1043-A66D-543EC3181143}"

writers=set()
testGUIDs=set()

relroot = "$(ProjectDir).."
def relname(fname) : # c:\ProteoWizard\pwiz\foo\bar\baz
	if ac.contains_pwizroot(fname) :
		return ac.replace_pwizroot(fname,relroot) # $(ProjectDir)\..\foo\bar\baz
	if fname.startswith("\\") :
		return relroot+fname
	return relroot+"\\"+fname

def treename(fname) : # c:\ProteoWizard\pwiz\foo\bar\baz
	return relname(fname).replace(relroot,"").replace("\\","_")

def absname(infname) :
	fname = infname.replace("$(ProjectDir)..",ac.get_pwizroot())
	if ac.contains_pwizroot(fname) :
		return fname
	else :
		return "%s\\%s"%(ac.get_pwizroot(),fname)

def addShipDir(d,addTree=False) :
	if not ".svn" in d :
		shipdirs.add(os.path.abspath(d.replace(relroot,ac.get_pwizroot())))
		if addTree:
			for dd in os.listdir(d) :
				ddd = d+"\\"+dd
				if stat.S_ISDIR(os.stat(ddd).st_mode) :
					addShipDir(ddd,addTree)

def addFile(file) :
	if not ".svn" in file :
		addShipDir(os.path.dirname(file))
		if ac.isTestFile(file) or ac.isExampleFile(file) :
			projects.add(file)
		elif ("ramp\\" in file or "TestHarness" in file) :
			testhelpers.add(file)
		else :
			srcs.add(file)

def addProjectReference(vcprojDoc,projectName) :
	projectGUID = GUIDs[projectName]
	itemGroup = vcprojDoc.getElementsByTagName("ItemGroup")[2]
	refNode = vcprojDoc.createElement("ProjectReference")
	refNode.setAttribute("Include",projectName+".vcxproj")
	pNode = vcprojDoc.createElement("Project")
	pNode.appendChild(vcprojDoc.createTextNode(projectGUID))
	refNode.appendChild(pNode)
	rNode = vcprojDoc.createElement("ReferenceOutputAssembly")
	rNode.appendChild(vcprojDoc.createTextNode("false"))
	refNode.appendChild(rNode)
	itemGroup.appendChild(refNode)

def makeGUID(n) :
	if (n == 0) : # libpwiz.vcproj
		return libpwizGUID
	else :
		return "{C44C75B2-43AA-%d-A66D-543EC318%d}"%(1000+n,2000+n)

def declareConfigs(sln,GUID) :
	for config in configs:
		sln.write('		%s.%s.ActiveCfg = %s\n'%(GUID,config,config))
		sln.write('		%s.%s.Build.0 = %s\n'%(GUID,config,config))

def makeProjectName(project) :
	noext = os.path.basename(project).rpartition(".")[0] # baz
	if ac.isTestFile(project) : # use dirname to disambiguate
		dirname = project.rpartition("\\")[0].rpartition("\\")[2] # bar
		return "test_"+dirname+"_"+noext # test_bar_baz
	elif noext.startswith("write_"):
		writers.add(noext)
		return noext
	else :
		return "example_"+noext

def saveDoc(doc, projectName, msvcVer, secondExt=""):
	filepath = "%s\\%s.%s%s"%(workdir,projectName,getVCProjExt(msvcVer),secondExt)
	file = open(filepath, "w")
	file.write(doc.toxml())
	file.close()

def updateSettings(vcprojDoc,msvcVer) :
	# update the include path
	if (10 <= msvcVer) :  # vc10 and vc12
		for node in  vcprojDoc.getElementsByTagName("AdditionalIncludeDirectories") :
			incl =  node.childNodes[0].nodeValue
			for i in includes :
				incl = incl + ";\"" + i + "\""
			node.childNodes[0].nodeValue = incl
		for node in vcprojDoc.getElementsByTagName("AdditionalLibraryDirectories") :
			incl =  node.childNodes[0].nodeValue
			incl += ";\"$(BOOST_ROOT)\lib32-msvc-%s.0\""%(msvcVer)
			node.childNodes[0].nodeValue = incl
		for node in vcprojDoc.getElementsByTagName("PreprocessorDefinitions") :
			d =  node.childNodes[0].nodeValue
			for i in defines :
				d = d + ";" + i 
			node.childNodes[0].nodeValue = d
		for node in vcprojDoc.getElementsByTagName("DisableSpecificWarnings") :
			w =  node.childNodes[0].nodeValue
			for i in disableWarnings :
				w = w + ";" + i 
			node.childNodes[0].nodeValue = w
	else :
		for toolNode in vcprojDoc.getElementsByTagName("Tool") :
			tag="AdditionalIncludeDirectories"
			if toolNode.hasAttribute(tag) :
				incl = toolNode.getAttribute(tag)
				for i in includes :
					incl = incl + ";\"" + i + "\""
				toolNode.setAttribute(tag,incl)
			tag="AdditionalLibraryDirectories"
			if toolNode.hasAttribute(tag) :
				incl = toolNode.getAttribute(tag)
				incl = incl + ";\"$(BOOST_ROOT)\lib32-msvc-%s.0\""%str(msvcVer)
				toolNode.setAttribute(tag,incl)
			tag = "PreprocessorDefinitions"
			if toolNode.hasAttribute(tag) :
				d = toolNode.getAttribute(tag)
				for i in defines :
					d = d + ";\"" + i + "\""
				toolNode.setAttribute(tag,d)
			tag = "DisableSpecificWarnings"
			if toolNode.hasAttribute(tag) :
				w = toolNode.getAttribute(tag)
				for i in disableWarnings :
					w = w + ";" + i 
				toolNode.setAttribute(tag,w)

def	addSourceFile(vcprojDoc,msvcVer,file) :
	if (10 <= msvcVer) : # cv10 and vc12
		itemGroup = vcprojDoc.getElementsByTagName("ItemGroup")[1]
		ccNode = vcprojDoc.createElement("ClCompile")
		ccNode.setAttribute("Include",relname(file))
		# avoid name collisions by specifying location of object file in tree
		for config in configs:
			fileConfigNode = vcprojDoc.createElement("ObjectFileName")
			fileConfigNode.setAttribute("Condition","'$(Configuration)|$(Platform)'=='%s'"%config)
			fileConfigNode.appendChild(vcprojDoc.createTextNode("$(IntDir)\\%s.obj"%treename(file).partition(".")[0]))
			ccNode.appendChild(fileConfigNode)
		itemGroup.appendChild(ccNode)
	else :
		filterNode = vcprojDoc.getElementsByTagName("Filter").item(0)
		# create a child "File" node for this file
		childFileNode = vcprojDoc.createElement("File")
		childFileNode.setAttribute("RelativePath", relname(file))
		# avoid name collisions by specifying location of object file in tree
		for config in configs:
			fileConfigNode = vcprojDoc.createElement("FileConfiguration")
			fileConfigNode.setAttribute("Name",config)
			toolNode = vcprojDoc.createElement("Tool")
			toolNode.setAttribute("Name","VCCLCompilerTool")
			toolNode.setAttribute("ObjectFile","$(OutDir)\\%s.obj"%treename(file).partition(".")[0])
			fileConfigNode.appendChild(toolNode)
			childFileNode.appendChild(fileConfigNode)
		filterNode.appendChild(childFileNode)

def getDecoratedVCProjExt(msvcVer) :
	if 10==msvcVer : 
		return "vcxproj"
	if 12==msvcVer : 
		return "vc12.vcxproj"
	return "vcproj"

def getVCProjExt(msvcVer) :
	if 10<=msvcVer : # vc10 and vc12
		return "vcxproj"
	return "vcproj"

def openVCProjTemplate(templateName,msvcVer,projectName) :
	projectGUID = GUIDs[projectName]
	vcprojDoc = xml.dom.minidom.parse(templateName+"."+getDecoratedVCProjExt(msvcVer)+".template")
	if 10<=msvcVer : # vc10 and vc12
		for rns in vcprojDoc.getElementsByTagName("RootNamespace") :
			rns.childNodes[0].nodeValue = projectName
		for guid in vcprojDoc.getElementsByTagName("ProjectGuid") :
			guid.childNodes[0].nodeValue = projectGUID
	else :
		headNode = vcprojDoc.getElementsByTagName("VisualStudioProject").item(0)
		headNode.setAttribute("Version","%d.00"%msvcVer)
		headNode.setAttribute("Name",projectName)
		headNode.setAttribute("RootNamespace",projectName)
		headNode.setAttribute("ProjectGUID",projectGUID)
	return vcprojDoc

slnGUID="{8BC9CEB8-8B4A-11D0-8D11-00A0C91BC942}"
def writeProj(sln,projname,msvcVer,dependGUIDs) :
	projGUID = GUIDs[projname]
	sln.write('Project("%s") = "%s", "%s.%s", "%s"\n'%(slnGUID,projname,projname,getVCProjExt(msvcVer),projGUID))
	if (len(dependGUIDs) != 0) :
		sln.write('	ProjectSection(ProjectDependencies) = postProject\n')
		for dependGUID in dependGUIDs:
			sln.write('		%s = %s\n'%(dependGUID,dependGUID))
		sln.write('	EndProjectSection\n')
	sln.write('EndProject\n')
# locate the source files
# assume a build log generated by "bjam -d+2"
in_rsp = False

zlibPath = ""

for line in buildlog_lines:
	line = line.rstrip('\r\n')
	if "]: " in line : # strip the timestamp if any
		line = line.split("]: ")[1]
	if dbug :
		print (line)
	if ("ProteoWizard " in line and "last committed" in line) :
		buildversion = line.split(" ")[1].replace(".","_")
	if ("file " in line and "obj.rsp" in line) : # beginning of a response file
		in_rsp = True
	elif ("call " in line) : # end of a response file
		in_rsp = False
	elif in_rsp :
		if dbug:
			print ("consider line "+line)
		if ("-I" in line and ac.isWelcomeInclude(line)):
			ipath = relname(line.rpartition('-I')[2].replace('"',''))
			includes.add(ipath)
			addShipDir(ipath)
			if "libraries\\zlib" in line :
				zlibPath = ipath
		if (".cpp" in line or ".cxx" in line) :
			if ("-F" in line) : # strip inlined compiler args if any
				line = line.split("-F")[0].strip()
			if (ac.isWelcomeSrcDir(line) and ac.isNotForbidden(line)) :
				file = absname(line.replace('"',''))
				if dbug:
					print ("consider file "+file)
				if ac.isSrcFile(file) or ac.isExampleFile(file):
					addFile(file)
	elif ".test\\" in line and '.exe" ' in line and "2>&1" in line : # running a test
		import shlex
		test = shlex.split(line.partition(" > ")[0])
		head = test[0].replace('"','').partition(".test")[0].rpartition("\\")[0]
		tail = test[0].replace('"','').rpartition("\\")[2]
		cmd = makeProjectName(head+"\\"+tail)
		args = ""
		for t in range(1,len(test)):
			args = args + relname(test[t])
			if (t < len(test)-1) :
				args += " "
		testargs[cmd]=args

# add any boost_aux source files
for boostauxsrc in ac.boostAuxSources:
	addFile(ac.get_pwizroot()+"\\"+boostauxsrc)

# set project GUIDs
n_projects = 0
for project in projects:
	n_projects = n_projects+1
	GUIDs[makeProjectName(project)] = makeGUID(n_projects)

#
# write files for msvc 8,9,10 and 12
#
msvcVerYear = dict([(8,2005),(9,2008),(10,2010),(12,2013)]) # yay, MSFT marketing team!
msvcVerFormatSLN = dict([(8,9),(9,10),(10,11),(12,12)]) # more MSFT marketing genious
for msvcVer in msvc_versions :

	workdir = ac.get_pwizroot()+"\\msvc%d"%msvcVer
	addShipDir(workdir)
	if not os.path.exists(workdir) :
		os.mkdir(workdir)

	slnfile = "%s\\libpwiz.sln"%workdir
	print ("creating %s and related %s files\n"%(slnfile,getVCProjExt(msvcVer)))


	# snork up a nearly empty vcproj file
	vcprojDoc = openVCProjTemplate(libpwiz_projectname,msvcVer,libpwiz_projectname)
	# assuming the "Source Files" filter is the first one
	for file in srcs:
		addSourceFile(vcprojDoc,msvcVer,file)
	# update the include path
	updateSettings(vcprojDoc, msvcVer)
	# update the file on disk
	saveDoc(vcprojDoc, libpwiz_projectname, msvcVer)

	for project in projects:
		projectname = makeProjectName(project)
		# snork up a nearly empty vcproj file
		vcprojDoc = openVCProjTemplate("examples",msvcVer,projectname)
		addSourceFile(vcprojDoc,msvcVer,project)
		updateSettings(vcprojDoc,msvcVer)
		saveDoc(vcprojDoc, projectname, msvcVer)

		# set the debug args if there's a file associated with a test
		if projectname in testargs:
			vcprojDoc = xml.dom.minidom.parse("tests.%s.user.template"%getDecoratedVCProjExt(msvcVer))
			if 10 <= msvcVer : # vc10 and vc12
				for setting in vcprojDoc.getElementsByTagName("LocalDebuggerCommandArguments") :
					setting.childNodes[0].nodeValue = testargs[projectname]
			else :
				headNode = vcprojDoc.getElementsByTagName("VisualStudioUserFile").item(0)
				headNode.setAttribute("Version","%d.00"%msvcVer)
				for setting in vcprojDoc.getElementsByTagName("DebugSettings") :
					setting.setAttribute("CommandArguments",testargs[projectname])
				saveDoc(vcprojDoc, projectname, msvcVer, ".user")

	# snork up a nearly empty vcproj file
	vcprojDoc = openVCProjTemplate(libpwiz_projectname,msvcVer,testhelpers_projectname)
	headNode = vcprojDoc.getElementsByTagName("VisualStudioProject").item(0)
	for file in testhelpers:
		addSourceFile(vcprojDoc,msvcVer,file)
	# update the include path
	updateSettings(vcprojDoc,msvcVer)
	# update the file on disk
	saveDoc(vcprojDoc, testhelpers_projectname, msvcVer)

	vcprojDoc = openVCProjTemplate(zlib_projectname,msvcVer,zlib_projectname)
	if len(zlibPath) :
		if 10<=msvcVer :
			fileElems = ["ClInclude","ClCompile"]
			fileAttr = "Include"
		else :
			fileElems = ["File"]
			fileAttr = "RelativePath"
		for fileElem in fileElems :
			for ref in vcprojDoc.getElementsByTagName(fileElem) :
				if ref.hasAttribute(fileAttr) :
					path = ref.getAttribute(fileAttr)
					if "$(ProjectDir)..\\libraries\\zlib-x.x.x" in path :
						ref.setAttribute(fileAttr,path.replace("$(ProjectDir)..\\libraries\\zlib-x.x.x",zlibPath))
	saveDoc(vcprojDoc, zlib_projectname, msvcVer)

	vcprojDoc = openVCProjTemplate(alltests_projectname,msvcVer,alltests_projectname)
	if 10<=msvcVer :
		for project in projects:
			addProjectReference(vcprojDoc,makeProjectName(project))
	updateSettings(vcprojDoc,msvcVer)
	saveDoc(vcprojDoc, alltests_projectname, msvcVer)
	alltests = open("%s\\alltests.cpp"%(workdir),"w")
	alltests.write("// generated file, do not edit\n")
	alltests.write('#include <stdlib.h>\n')
	alltests.write('#include <vector>\n')
	alltests.write('#include <string>\n')
	alltests.write('const char *tests[][2] = {\n')
	for writer in writers :
		alltests.write('{"%s",""},\n'%(writer))
	for test in testargs :
		if (not test.startswith("example_")) :
			alltests.write('{"%s","%s"},\n'%(test,testargs[test].replace(relroot,"").replace("\\","\\\\")))
	alltests.write('{NULL,NULL}};\n')
	alltests.write('#include <direct.h>\n')
	alltests.write('void main(int arg,char *argv[]) {\n')
	alltests.write('	char *curdir = strdup(argv[0]);\n')
	alltests.write("	*strrchr(curdir,'\\\\') = 0;\n")
	alltests.write('	chdir(curdir);\n')
	alltests.write('	char *pwizdir = strdup(curdir);\n')
	alltests.write('	for (int n=(int)strlen(pwizdir);n>4;) {\n')
	alltests.write('		if (strcmp("pwiz",pwizdir+n-3)) {\n')
	alltests.write('			*(pwizdir+n--)=0;\n')
	alltests.write('		} else {\n')
	alltests.write('			break;\n')
	alltests.write('		}\n')
	alltests.write('	}\n')
	alltests.write('	std::vector<int> failures;\n')
	alltests.write('	for (int n=0;tests[n][0];n++) {\n')
	alltests.write('		std::string cmd(tests[n][0]);\n')
	alltests.write('		if (*tests[n][1]) {\n')
	alltests.write('			cmd += " ";\n')
	alltests.write('			cmd += pwizdir;\n')
	alltests.write('			cmd += tests[n][1];\n')
	alltests.write('		}\n')
	alltests.write('		puts(cmd.c_str());\n')
	alltests.write('		if (system(cmd.c_str())) {\n')
	alltests.write('			puts("FAIL");\n')
	alltests.write('			failures.push_back(n);\n')
	alltests.write('		} else {\n')
	alltests.write('			puts("OK");\n')
	alltests.write('		}\n')
	alltests.write('	}\n')
	alltests.write('	printf("%d failures\\n",failures.size());\n')
	alltests.write('	exit((int)failures.size());\n')
	alltests.write('}\n')
	alltests.close()


	libpwizGUID=GUIDs[libpwiz_projectname]
	zlibGUID=GUIDs[zlib_projectname]
	alltestsGUID=GUIDs[alltests_projectname]
	testhelpersGUID=GUIDs[testhelpers_projectname]
	sln=open(slnfile,"w")
	sln.write('Microsoft Visual Studio Solution File, Format Version %d.00\n'%msvcVerFormatSLN[msvcVer])
	sln.write('# Visual C++ Express %d\n'%msvcVerYear[msvcVer])
	writeProj(sln,libpwiz_projectname,msvcVer,set([zlibGUID]))
	for project in projects :
		projname = makeProjectName(project)
		writeProj(sln,projname,msvcVer,set([libpwizGUID,testhelpersGUID]))
		testGUIDs.add(GUIDs[projname])
	writeProj(sln,testhelpers_projectname,msvcVer,set())
	writeProj(sln,zlib_projectname,msvcVer,set())
	writeProj(sln,alltests_projectname,msvcVer,testGUIDs)
	sln.write('Global\n')
	sln.write('	GlobalSection(SolutionConfigurationPlatforms) = preSolution\n')
	sln.write('		Debug|Win32 = Debug|Win32\n')
	sln.write('		Release|Win32 = Release|Win32\n')
	sln.write('	EndGlobalSection\n')
	sln.write('	GlobalSection(ProjectConfigurationPlatforms) = postSolution\n')
	declareConfigs(sln,libpwizGUID)
	for project in projects :
		declareConfigs(sln,GUIDs[makeProjectName(project)])
	for guid in set([zlibGUID,testhelpersGUID,alltestsGUID]) :
		declareConfigs(sln,guid)
	sln.write('	EndGlobalSection\n')
	sln.write('	GlobalSection(SolutionProperties) = preSolution\n')
	sln.write('		HideSolutionNode = FALSE\n')
	sln.write('	EndGlobalSection\n')
	sln.write('EndGlobal\n')
	sln.close()

	faq = open("FAQ","r").read()
	readme = open("%s\\Readme.txt"%workdir,"w")
	readme.write(faq);
	readme.close()

# create a distribution zipfile
addShipDir(ac.get_pwizroot())
faq = open("README_MSVC.txt","r").read()
readmeMSVC = open("%s\\README_MSVC.txt"%(ac.get_pwizroot()),"w")
readmeMSVC.write(faq)
readmeMSVC.close()

fz="%s\\build-nt-x86\\libpwiz_msvc_%s.zip"%(ac.get_pwizroot(),buildversion)
print ("creating MSVC source build distribution kit %s"%(fz))
z = zipfile.ZipFile(fz,"w",zipfile.ZIP_DEFLATED)
exts = ["h","hpp","c","cpp","cxx","sln","vcproj.user","vcxproj.user","vcproj","vcxproj","txt","inl"]

addedFiles = set()

# include the whole boost_aux tree, and others with depth but no -I reference
addShipDir(ac.get_pwizroot()+"\\libraries\\boost_aux",addTree=True)
for tree in ac.complicatedTrees:
	for shipdir in shipdirs :
		if tree in shipdir :
			addShipDir(shipdir,addTree=True)
			break
for d in ac.subtleIncludes : # any others not mentioned?
	addShipDir(ac.get_pwizroot()+"\\"+d,addTree=True)
if (dbug) :
	print ('processing directories:')
	print (shipdirs)

for shipdir in shipdirs :
	if (dbug) :
		print ('processing directory %s'%shipdir)
	for file in os.listdir(shipdir) :
		f = shipdir+"\\"+file
		ext = file.partition(".")[2]
		if (not stat.S_ISDIR(os.stat(f).st_mode)) and ext in exts or ext=="":
			tname = ac.replace_pwizroot(f,"pwiz")
			if not tname in files_not_to_be_shipped :
				print ('adding %s as %s'%(f,tname))
				if tname not in addedFiles :
					z.write(f,tname)
					addedFiles.add(tname)
			
testfiles = set()
for test in testargs : # grab data files
	f = absname(testargs[test])
	if (os.path.exists(f)) :
		ext = f.rpartition(".")[2]
		d = os.path.dirname(f) # go ahead and grab anything else with same .ext
		for file in os.listdir(d) :
			ff = d+"\\"+file
			ext2 = ff.rpartition(".")[2]
			if (ext==ext2 and not stat.S_ISDIR(os.stat(ff).st_mode)):
				testfiles.add(ff)
for f in testfiles :
	tname = ac.replace_pwizroot(f,"pwiz")
	print ('adding %s as %s'%(f,tname))
	if tname not in addedFiles :
		z.write(f,tname)
		addedFiles.add(tname)
