import sys

def getString(name):
    name = name.split("=")[1].lstrip('"').rstrip('"')
    return name

def makeDictionary(protfile):
    protfile = open(protfile, "r")
    protfile = protfile.readlines()

    proteinDict = {}

    for line in protfile:
        line = line.lstrip()
        line = line.split()
        if line[0] == "<protein":
            proteinName = getString(line[1])
            probability = getString(line[3])
            if line[4].split("=")[0] == "percent_coverage":
                coverage = getString(line[4])
            else:
                coverage = "na"
            proteinDict[proteinName] = [probability, coverage]

#            print proteinName, probability, coverage

    return proteinDict

#print "#############"
oneDict = makeDictionary(sys.argv[1])
twoDict = makeDictionary(sys.argv[2])

for thing in oneDict.keys():
    if thing in twoDict.keys():
        print thing + "\t" + oneDict[thing][0] + "\t" + oneDict[thing][1] + "\t" + twoDict[thing][0] + "\t" + twoDict[thing][1]
