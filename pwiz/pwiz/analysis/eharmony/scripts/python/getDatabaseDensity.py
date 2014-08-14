import sys
import math

db = sys.argv[1]
db = open(db,"r")
db = db.readlines()

def getMean(list):
    sum =0
    for thing in list:
        sum += thing
    sum /= len(list)
    return sum

def getStdDev(list, mean):
    sum = 0
    for thing in list:
        sum += (thing - mean)**2
    sum /= len(list)
    sum = math.sqrt(sum)
    return sum

peptideDict = {}

for line in db:
    line = line.split("\t")
    if line[2] in peptideDict.keys():
        peptideDict[line[2]].append([float(line[0]), float(line[1])])
    else:
        peptideDict[line[2]] = [[float(line[0]),float(line[1])]]

mzStdevList = []
rtStdevList = []

for thing in peptideDict.keys():
    mz = []
    rt = []

    for item in peptideDict[thing]:
        mz.append(item[0]);
        rt.append(item[1]);

    stdevMz = getStdDev(mz, getMean(mz))
    stdevRt = getStdDev(rt, getMean(rt))

    mzStdevList.append(stdevMz)
    rtStdevList.append(stdevRt)

#do a rank sum test for the distribution differences
for thing in rtStdevList:
    print thing
    
    
