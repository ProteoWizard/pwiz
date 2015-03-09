import sys

def read(file):
    file = open(file,"r")
    file = file.readlines()
    return file

one = read(sys.argv[1])
two = read(sys.argv[2])

outfile = open("list_diff.txt","w")

for thing in one:
    if thing not in two:
        outfile.write(thing)

outfile.close()

    

