import sys
import string

if len(sys.argv) != 5:
	print "Usage: python makeHandler.py flat.txt nested.txt structName writtenStructName"
	print "\n\nwhere flat.txt is a file listing all of the attributes of the top level element represented by the pwiz struct structName and represented in the XML file as <writtenStructName...> </writtenStructName>.  These attributes should be listed in two tab-separated columns, the first being the name of the pwiz attribute of writtenStruct that it will be read into and the second being the string representing the attribute in XML."
	print "\n\nwhere nested.txt is a file listing all of the child elements of the top level element.  These attributes should be listed in four tab=separated columns, the first being the name of the pwiz struct that this element is meant to be read into, the second being the name of the pwiz attribute of the pwiz top level struct that will hold this element, the third being the string representing the element tag in XML, and the fourth being a bool to denote whether or not the top level struct holds a vector of these elements. (1 = yes, 0 = no.  Not cool enough to implement 'true' or 'false')\n\n"
  	print "Example files in this directory correspond to these descriptions."
	print "For an example, in this directory run: "
	print "python makeHandler.py flat.txt nested.txt BoogerPicker booger_picker"

flat = sys.argv[1]
flat = open(flat,"r")
flat = flat.readlines()
for i in range(len(flat)):
	flat[i] = flat[i].strip("\n").split("\t")

nested = sys.argv[2]
nested = open(nested, "r")
nested = nested.readlines()
for i in range(len(nested)):
	nested[i] = nested[i].strip("\n").split("\t")

structName = sys.argv[3]
writtenStructName = sys.argv[4]

tab = "    " # four spaces for pwiz

###################################
## generate write function
###################################

print "void " + structName + "::write(XMLWriter& writer) const"
print "{"
print tab + "XMLWriter::Attributes attributes;"
for thing in flat:
	print tab + 'attributes.push_back(make_pair("' + thing[1] + '", boost::lexical_cast<string>(' + thing[0] + ')));'

print ''

if len(nested) == 0: 
	print tab + 'writer.startElement("' + writtenStructName + '", attributes, XMLWriter::EmptyElement);'
	print ''
if len(nested) > 0:
	print tab + 'writer.startElement("' + writtenStructName + '", attributes);'
	
	for thing in nested: 
		print ''
		if thing[3] == '0':
			print tab + thing[1] + '->write(writer);'
		elif thing[3] == '1':		
			print tab + 'vector<' + thing[0] + '>::iterator it_' + thing[1] + ' = ' + thing[1] + '.begin();'
			print tab + 'for(; it_' + thing[1] + ' != ' + thing[1] + '.end(); ++it_' + thing[1] + ') it_' +thing[1]+'->write(writer);'

		
	print ''
	print tab + 'writer.endElement();'
	print ''

print '}'

#####################
## generate Handler
######################

print ''
print 'struct Handler' + structName + ' : public SAXParser::Handler'
print '{'
print tab + structName + '* _' + string.lower(structName) + ';'
print tab + 'Handler' + structName + '(' + structName + '* ' + string.lower(structName) + ' = 0) : _' + string.lower(structName) + '(' + string.lower(structName) + ') {}'

print tab + 'virtual Status startElement(const string& name, const Attributes& attributes, stream_offset position)'

print tab + '{'

print tab + tab + 'if (name == "' + writtenStructName + '")'
print tab + tab + '{'
for thing in flat:
	print tab + tab + tab + 'getAttribute(attributes, "' + thing[1] + '" , _' +string.lower(structName) +'->' + thing[0] + ');'

print ''
print tab + tab + tab + 'return Status::Ok;'
print tab + tab + '}'
print ''

for thing in nested:
	if thing[3] == '0':
		print tab + tab + 'else if (name == "' + thing[2] + '")'
		print tab + tab + '{'
		print tab + tab + tab + '_handler' + thing[0] + '._' + thing[1] + '=&_' + string.lower(structName) + '->' + thing[1] + ';'
		print tab + tab + tab + 'return Status(Status::Delegate, &_handler' + thing[0] + ');'
		print tab + tab + '}'
		print ''

	elif thing[3] == '1':
		print tab + tab + 'else if (name == "' + thing[2] + '")'
		print tab + tab + '{'
		print tab + tab + tab + '_' + string.lower(structName) + '->' + thing[1] + '.push_back(' + thing[0] + '());'
		print tab + tab + tab + '_handler' + thing[0] + '._' + thing[1] + '= &_' + string.lower(structName) + '->' + thing[1] + '.back();'
		print tab + tab + tab + 'return Status(Status::Delegate, &_handler' + thing[0] + ');'

		print tab + tab + '}'
		print ''

	
print tab + tab + 'else throw runtime_error(("[Handler' + structName + '] Unexpected element name:"  + name).c_str());'
print tab + '}'
print ''
print 'private:'
print ''
for thing in nested:
	print tab + "Handler" + thing[0] + " _handler" + thing[0] + ";"

print ''

print '};'

print ''

#########################
## generate read function
##########################

print 'void ' + structName + '::read(istream& is)'
print '{'
print tab + 'Handler' + structName + ' _handler' + structName + '(this);'
print tab + 'parse(is, _handler' + structName +');'
print ''
print '}'
