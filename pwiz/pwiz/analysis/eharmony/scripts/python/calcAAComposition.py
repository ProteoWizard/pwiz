import sys
import math
sequences = sys.argv[1]
sequences = open(sequences, "r")
sequences = sequences.readlines()

sequences2 = sys.argv[2]
sequences2 = open(sequences2, "r")
sequences2 = sequences2.readlines()

def getMean(dictionary):
    sum = 0
    for thing in dictionary.keys():
        sum += dictionary[thing]
    
    sum /= float(len(dictionary.keys()))
    return sum

def getStDev(dictionary, mean):
    sum = 0
    for thing in dictionary.keys():
        sum += (dictionary[thing] - mean)**2
    sum = math.sqrt(sum)
    sum /= float(len(dictionary.keys()))
    return sum

def calcAADict(foo):
    AADict = {}
    total = 0

    for line in foo:
        for char in line[:-1]:
            if char in AADict.keys():
                AADict[char] += 1
            else:
                AADict[char] = 1
            
            total += 1

    return AADict, total

firstAA, firstCount = calcAADict(sequences)
secondAA, secondCount = calcAADict(sequences2)


meanDict = {}

for thing in firstAA.keys():

    print thing + ": " + str(firstAA[thing]/float(firstCount) - secondAA[thing]/float(secondCount)) 
    meanDict[thing] = firstAA[thing]/float(firstCount) - secondAA[thing]/float(secondCount)
print "Mean: "
print getMean(meanDict)
print "Stdev: "
print getStDev(meanDict, getMean(meanDict))

