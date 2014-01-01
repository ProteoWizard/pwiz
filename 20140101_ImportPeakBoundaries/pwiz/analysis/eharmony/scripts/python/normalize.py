import sys
file = sys.argv[1]
file = open(file,"r")
file = file.readlines()

max = 0
for line in file:
    line = float(line)
    if line > max:
        max = line

for line in file:
    line = float(line)
    line /= max
    print line
