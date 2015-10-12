' Download psi-ms.obo
psimsURL = "https://raw.githubusercontent.com/HUPO-PSI/mzML/master/cv/psi-ms.obo"
psimsDestination = "..\..\pwiz\data\common\psi-ms.obo"
Set objXMLHTTP = CreateObject("MSXML2.XMLHTTP")

objXMLHTTP.open "GET", psimsURL, false
objXMLHTTP.setRequestHeader "If-Modified-Since", "Sat, 1 Jan 2000 00:00:00 GMT"
objXMLHTTP.send()

If objXMLHTTP.Status = 200 Then
  Set objADOStream = CreateObject("ADODB.Stream")
  objADOStream.Open
  objADOStream.Type = 1 'adTypeBinary

  objADOStream.Write objXMLHTTP.ResponseBody
  objADOStream.Position = 0    'Set the stream position to the start

  Set objFSO = Createobject("Scripting.FileSystemObject")
    If objFSO.Fileexists(psimsDestination) Then objFSO.DeleteFile psimsDestination
  Set objFSO = Nothing

  objADOStream.SaveToFile psimsDestination
  objADOStream.Close
  Set objADOStream = Nothing
End if

' Download unit.obo
unitURL = "http://obo.cvs.sourceforge.net/*checkout*/obo/obo/ontology/phenotype/unit.obo"
unitDestination = "..\..\pwiz\data\common\unit.obo"
objXMLHTTP.open "GET", unitURL, false
objXMLHTTP.send()

If objXMLHTTP.Status = 200 Then
  Set objADOStream = CreateObject("ADODB.Stream")
  objADOStream.Open
  objADOStream.Type = 1 'adTypeBinary

  objADOStream.Write objXMLHTTP.ResponseBody
  objADOStream.Position = 0    'Set the stream position to the start

  Set objFSO = Createobject("Scripting.FileSystemObject")
    If objFSO.Fileexists(unitDestination) Then objFSO.DeleteFile unitDestination
  Set objFSO = Nothing

  objADOStream.SaveToFile unitDestination
  objADOStream.Close
  Set objADOStream = Nothing
End if

' Download unimod.obo
unitURL = "http://www.unimod.org/obo/unimod.obo"
unitDestination = "..\..\pwiz\data\common\unimod.obo"
objXMLHTTP.open "GET", unitURL, false
objXMLHTTP.send()

If objXMLHTTP.Status = 200 Then
  Set objADOStream = CreateObject("ADODB.Stream")
  objADOStream.Open
  objADOStream.Type = 1 'adTypeBinary

  objADOStream.Write objXMLHTTP.ResponseBody
  objADOStream.Position = 0    'Set the stream position to the start

  Set objFSO = Createobject("Scripting.FileSystemObject")
    If objFSO.Fileexists(unitDestination) Then objFSO.DeleteFile unitDestination
  Set objFSO = Nothing

  objADOStream.SaveToFile unitDestination
  objADOStream.Close
  Set objADOStream = Nothing
End if

Set objXMLHTTP = Nothing