#!/usr/bin/python
# example OpenSWATH job submission script.  You will need to change filenames, directory etc.
import os
fileNames = ["T130510_Study9_2_sampleJ_01.mzXML.gz","T130510_Study9_2_sampleJ_02.mzXML.gz", "T130510_Study9_2_sampleJ_03.mzXML.gz", "T130510_Study9_2_sampleI_01.mzXML.gz","T130510_Study9_2_sampleI_02.mzXML.gz", "T130510_Study9_2_sampleI_03.mzXML.gz","T130510_Study9_2_sampleH_01.mzXML.gz","T130510_Study9_2_sampleH_02.mzXML.gz", "T130510_Study9_2_sampleH_03.mzXML.gz","T130510_Study9_2_sampleG_01.mzXML.gz","T130510_Study9_2_sampleG_02.mzXML.gz", "T130510_Study9_2_sampleG_03.mzXML.gz","T130510_Study9_2_sampleF_01.mzXML.gz","T130510_Study9_2_sampleF_02.mzXML.gz", "T130510_Study9_2_sampleF_03.mzXML.gz","T130510_Study9_2_sampleE_01.mzXML.gz","T130510_Study9_2_sampleE_02.mzXML.gz", "T130510_Study9_2_sampleE_03.mzXML.gz","T130510_Study9_2_sampleD_01.mzXML.gz","T130510_Study9_2_sampleD_02.mzXML.gz", "T130510_Study9_2_sampleD_03.mzXML.gz","T130510_Study9_2_sampleC_01.mzXML.gz","T130510_Study9_2_sampleC_02.mzXML.gz", "T130510_Study9_2_sampleC_03.mzXML.gz","T130510_Study9_2_sampleB_01.mzXML.gz","T130510_Study9_2_sampleB_02.mzXML.gz", "T130510_Study9_2_sampleB_03.mzXML.gz","T130510_Study9_2_sampleA_01.mzXML.gz","T130510_Study9_2_sampleA_02.mzXML.gz", "T130510_Study9_2_sampleA_03.mzXML.gz"]
fileNames = ["~/Wiff_files/" + x for x in fileNames]
for fileName in fileNames:
    command = "qsub ~/runjob.single " + fileName
    print command
    os.system(command)

