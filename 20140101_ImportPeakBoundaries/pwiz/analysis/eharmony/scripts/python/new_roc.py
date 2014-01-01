import sys
file = sys.argv[1]
file = open(file, "r")
file = file.readlines()

positives = []
negatives = []

for line in file:
    line = line.split()
    line[0] = float(line[0])
    if line[1] == '1':
        positives.append(line)
    elif line[1] == '0':
        negatives.append(line)
    else:
        print line
        print "What the heck?"
    
positives.sort()
negatives.sort()

truePositives = 0;
falsePositives = 0;
trueNegatives = 0;
falseNegatives = 0;

threshold = float(sys.argv[2])

for thing in positives:
    if thing[0] > threshold:
        truePositives += 1
    else:
        falseNegatives += 1

for thing in negatives:
    if thing[0] > threshold:
        falsePositives +=1
    else:
        trueNegatives += 1


print truePositives
print falsePositives
print trueNegatives
print falseNegatives

outfile = open("roc.txt", "a")
outfile.write(str(falsePositives/float(falsePositives + trueNegatives)))
outfile.write("\t")
outfile.write(str(truePositives/float(truePositives + falseNegatives)))
outfile.write("\n")
