//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace IDPicker
{
    public class Presentation
    {
        public static string Version { get { return Util.GetAssemblyVersion( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static DateTime LastModified { get { return Util.GetAssemblyLastModified( System.Reflection.Assembly.GetExecutingAssembly().GetName() ); } }
        public static string LICENSE = Workspace.LICENSE;

        #region Javascript
        // This function takes a sequence and annotates it with HTML tags to
        // display modifications in different colors and scripts.
        public static string ptmAnnotation = "\tfunction annotatePTMsInPeptide(sequenceIndex) {\n" +
                               "\t\t var str=\"\";\n" +
                               "\t\t if(typeof(sequenceIndex)==\"number\") {\n" +
                               "\t\t\t str=stringIndex[sequenceIndex];\n" +
                               "\t\t } else if(typeof(sequenceIndex)==\"string\"){\n" +
                               "\t\t\t str=sequenceIndex;\n" +
                               "\t\t }\n" +
                               "\t\t var newStr='';\n" +
                               "\t\t var tokens=str.split('/');\n" +
                               "\t\t var isDigit=/\\d/;\n" +
                               "\t\t for(var i = 0; i < tokens.length;++i) {\n" +
                               "\t\t\t var currentSeq=tokens[i];\n" +
                               "\t\t\t var index=0;\n" +
                               "\t\t\t for(index=0;index<currentSeq.length&&currentSeq.charAt(index)!='{';++index){\n" +
                               "\t\t\t\t if(isDigit.test(currentSeq.charAt(index))||currentSeq.charAt(index)==',') {\n" +
                               "\t\t\t\t\t newStr+=\"<span class='index'>\"+currentSeq.charAt(index)+\"</span>\";\n" +
                               "\t\t\t\t } else {\n" +
                               "\t\t\t\t\t newStr+=currentSeq.charAt(index);\n" +
                               "\t\t\t\t }\n" +
                               "\t\t\t }\n" +
                               "\t\t\tif(index<currentSeq.length) {\n" +
                               "\t\t\t\t var tailTokens = currentSeq.split('{');\n" +
                               "\t\t\t\t var massIndex=tailTokens[1].split('}');\n" +
                               "\t\t\t\t var masses = massIndex[0].split(';');\n" +
                               "\t\t\t\t var map = new Object();\n" +
                               "\t\t\t\t for(var massIndex=0; massIndex<masses.length;++massIndex) {\n" +
                               "\t\t\t\t\t var massMap = masses[massIndex].split('=');\n" +
                               "\t\t\t\t\t map[massMap[0]]=massMap[1];\n" +
                               "\t\t\t\t }\n" +
                               "\t\t\t\t newStr+=\"<span class='indexAnn'>{\";\n" +
                               "\t\t\t\t var first=true;\n" +
                               "\t\t\t\t for(var massIndex in map) {\n" +
                               "\t\t\t\t\t newStr+=\"<span title='\"+map[massIndex]+\"'>\";\n" +
                               "\t\t\t\t\t var mass = (Math.round(map[massIndex]*10.0)/10.0);\n" +
                               "\t\t\t\t\t if(first) {\n" +
                               "\t\t\t\t\t\t newStr+=massIndex+\"=\"+mass;\n" +
                               "\t\t\t\t\t\t first=false;\n" +
                               "\t\t\t\t\t } else {\n" +
                               "\t\t\t\t\t\t newStr+=\",\"+massIndex+\"=\"+mass;\n" +
                               "\t\t\t\t\t }\n" +
                               "\t\t\t\t\t newStr+=\"</span>\";\n" +
                               "\t\t\t\t }\n" +
                               "\t\t\t\t newStr+=\"}</span>\";\n" +
                               "\t\t\t  }\n" +
                               "\t\t\t if(i<tokens.length-1) {\n" +
                               "\t\t\t\t newStr+='/';\n" +
                               "\t\t\t }" +
                               "\t\t }\n" +
                               "\t\t return newStr;\n" +
                               "\t}\n";

        public static string assembleJavascript()
        {
            return "function tglDsp(e)\n{\n\tvar sElement = document.getElementById(e);\n\tif( sElement.rows.length > 0 )\n\t{\n\t\tif( sElement.style.display == 'none' )\n\t\t\tsElement.style.display = '';\n\t\telse\n\t\t\tsElement.style.display = 'none';\n\t}\n}\n\nfunction setCptn(b,show)\n{\n\tvar aButton = document.getElementById(b);\n\n\tvar oldValue = aButton.innerHTML;\n\tvar newValue;\n\tif( show )\n\t\tnewValue = oldValue.replace( /Show/, \"Hide\" );\n\telse\n\t\tnewValue = oldValue.replace( /Hide/, \"Show\" );\n\taButton.innerHTML = newValue;\n}\n\nfunction tglCptn(b)\n{\n\tvar aButton = document.getElementById(b);\n\n\tvar oldValue = aButton.innerHTML;\n\tvar newValue = oldValue.replace( /Show/, \"Hide\" );\n\tif( oldValue == newValue )\n\t\tnewValue = oldValue.replace( /Hide/, \"Show\" );\n\taButton.innerHTML = newValue;\n}\n\nfunction tglTreeAnchor(b)\n{\n\tvar anAnchor = document.getElementById(b);\n\n\tif( anAnchor.innerHTML == \"+\" )\n\t\tanAnchor.innerHTML = \"-\";\n\telse\n\t\tanAnchor.innerHTML = \"+\";\n}\n\nvar sortIndices;\nfunction sortByIndices(a,b)\n{\n\tfor( var i=0, l=sortIndices.length; i < l; ++i )\n\t{\n\t\tvar sortIndex = Math.abs( sortIndices[i] )-1;\n\t\tvar aValue = a[sortIndex];\n\t\tvar bValue = b[sortIndex];\n\t\tif( aValue == bValue )\n\t\t\tcontinue;\n\t\tif( sortIndices[i] >= 0 )\n\t\t{\n\t\t\tif( aValue < bValue )\n\t\t\t\treturn -1;\n\t\t\telse\n\t\t\t\treturn 1;\n\t\t} else\n\t\t{\n\t\t\tif( aValue > bValue )\n\t\t\t\treturn -1;\n\t\t\telse\n\t\t\t\treturn 1;\n\t\t}\n\t}\n\treturn 0;\n}\n\nfunction addPeptideGroupsTableDataRows(tableId, tableData, numColumns)\n{\n\tif( tableData.curSortIndexes && tableData.defaultSortIndexes && tableData.curSortIndexes.join() != tableData.defaultSortIndexes.join() ) {\n\t\t return addTreeTableDataRows(tableId, tableData, numColumns);\n\t}\n\n\tvar tableHtml = [];\n\tvar tableDataRows = tableData.data;\n\tvar tableMetadata = tableData.metadata;\n\tvar lastGID;\n\tvar lastRowClass;\n\tfor( var r=0, maxr=tableDataRows.length; r < maxr; ++r )\n\t{\n\t\tvar rowId = tableId + 'r' + r;\n\t\tvar rowData = tableDataRows[r];\n\t\tvar rowLength = tableDataRows[r].length;\n\t\tvar rowClass;\n\t\tvar childTableId;\n\t\tvar childTableData;\n\n\t\tif( r > 0 )\n\t\t\tif( tableDataRows[r][2] != tableDataRows[r-1][2] )\n\t\t\t\tif( lastRowClass == \"h2\" )\n\t\t\t\t\trowClass = \"h3\";\n\t\t\t\telse\n\t\t\t\t\trowClass = \"h2\";\n\t\t\telse {\n\t\t\t\trowClass = lastRowClass;\n\t\t\t}\n\t\telse\n\t\t\trowClass = \"h2\";\n\n\t\ttableHtml.push('<tr id=' + rowId + ' class=' + rowClass + '>');\n\n\t\tvar hasChild = typeof(rowData[rowLength-1]) == \"object\";\n\t\tif(hasChild) {\n\t\t\tchildTableId = rowData[rowLength-1].child;\n\t\t\tchildTableData = treeTables[childTableId];\n\t\t\ttableHtml.push('<td id=es2>');\n\t\t\ttableHtml.push(\"<a class=\\\"tb\\\" id=\\\"\" + rowId + \"b\\\" onclick=\\\"tglTreeTable('\" + tableId + \"',\" + r + \"); tglTreeAnchor('\" + rowId + \"b')\\\">\" + ( childTableData && childTableData.show ? '-' : '+' ) + \"</a>\");\n\t\t\ttableHtml.push('</td>');\n\t\t} else\n\t\t\ttableHtml.push('<td id=es2 />');\n\n\t\tfor( var c=0; c < rowLength; ++c )\n\t\t{\n\t\t\tvar colValue = rowData[c];\n\t\t\tif( c == rowLength-1 )\n\t\t\t\tif( typeof(colValue) == \"object\" )\n\t\t\t\t\tbreak;\n\t\t\tif( r > 0 && c == 2 && tableDataRows[r][2] == tableDataRows[r-1][2] )\n\t\t\t\tcolValue = \"\";\n\t\t\telse if( tableMetadata != null ) {\n\t\t\t\tif( tableMetadata[c] == 1 )\n\t\t\t\t\tcontinue;\n\t\t\t\telse if( tableMetadata[c] == 2 )\n\t\t\t\t\tcolValue = stringIndex[colValue];\n\t\t\t}\n\t\t\tvar colStyle = \"text-align:right\";\n\t\t\tif( typeof(colValue) == \"string\" )\n\t\t\t\tcolStyle = \"text-align:left\";\n\t\t\ttableHtml.push('<td style=\"' + colStyle + '\">' + colValue + '</td>');\n\t\t}\n\t\ttableHtml.push('</tr>');\n\n\t\tif( hasChild && childTableData && childTableData.show )\n\t\t{\n\t\t\tvar childTableRowId = tableId + \"_/_\" + childTableId + 'r';\n\t\t\tvar childTableContainer = makeTreeTable(tableId + \"_/_\" + childTableId);\n\t\t\ttableHtml.push('<tr id=\"' + childTableRowId + '\"><td /><td colspan=\"' + numColumns + '\">' + childTableContainer.innerHTML + '</td></tr>');\n\t\t}\n\n\t\tlastGID = tableDataRows[r][2];\n\t\tlastRowClass = rowClass;\n\t}\n\treturn tableHtml.join(\"\");\n}\n\nfunction makeTreeTable(tableId, curContainer)\n{\n\tvar branch = tableId.split(\"_/_\");\n\tvar leaf = branch[branch.length-1];\n\tvar t = treeTables[leaf];\n\n\tvar tableFontSize = \"12pt\";\n\tif( branch.length > 1 )\n\t\ttableFontSize = \"10pt\";\n\n\tvar tableHtml = [];\n\tvar containerElement = curContainer;\n\tif( containerElement == null ) {\n\t\tcontainerElement = document.createElement('span')\n\t\tcontainerElement.id = tableId + '_container';\n\t}\n\n\tif( t.caption != null && t.caption.length > 0 )\n\t{\n\t\tvar defaultShowStateString = ( t.show ? \"Hide \" : \"Show \" );\n\t\tif( t.ready == null )\n\t\t\tt.ready = ( t.show ? true : false );\n\t\ttableHtml.push(\"<span class=\\\"tglcaption\\\"><a id=\\\"\" + tableId + \"c\\\" onclick=\\\"tglShowTreeTable('\" + tableId + \"'); tglCptn('\" + tableId + \"c')\\\">\" + defaultShowStateString + t.caption + \"</a></span><br />\");\n\t} else\n\t\tt.ready = true;\n\n\tif( !t.ready ) {\n\t\tcontainerElement.innerHTML = tableHtml.join(\"\");\n\t\treturn containerElement;\n\t}\n\n\ttableHtml.push('<table id=\"' + tableId + '\" style=\"font-size:' + tableFontSize + '\">');\n\t\n\tvar numColumns;\n\tif( typeof(t.header) == \"number\" ) {\n\t\tnumColumns = t.header;\n\t\ttableHtml.push('<tr />');\n\t} else if( typeof(t.header) == \"object\" ) {\n\t\tnumColumns = t.header.length;\n\t\ttableHtml.push('<tr><td id=es2 />');\n\t\tif( t.sortable ) {\n\t\t\tif( t.headerSortIndexes == null ) {\n\t\t\t\tt.headerSortIndexes = new Array();\n\t\t\t\tfor( var c=0; c < numColumns; ++c )\n\t\t\t\t\tt.headerSortIndexes[c] = [c+1];\n\t\t\t}\n\t\t\tif( t.defaultSortIndexes == null )\n\t\t\t\tt.defaultSortIndexes = t.headerSortIndexes[0];\n\t\t\tif( t.curSortIndexes == null )\n\t\t\t\tt.curSortIndexes = t.defaultSortIndexes;\n\t\t\tsortIndices = t.curSortIndexes;\n\t\t\tt.data.sort( sortByIndices );\n\t\t\tif( t.titles != null && t.titles.length == t.header.length )\n\t\t\t\tfor( var c=0; c < numColumns; ++c )\n\t\t\t\t\ttableHtml.push('<td id=es1><a class=\"txtBtn\" title=\"Sort by ' + t.titles[c] + '\" onclick=\"sortTreeTable(\\'' + tableId + '\\',[' + t.headerSortIndexes[c].join() + '])\">' + t.header[c] + '</a></td>');\n\t\t\telse\n\t\t\t\tfor( var c=0; c < numColumns; ++c )\n\t\t\t\t\ttableHtml.push('<td id=es1><a class=\"txtBtn\" onclick=\"sortTreeTable(\\'' + tableId + '\\',[' + t.headerSortIndexes[c].join() + '])\">' + t.header[c] + '</a></td>');\n\t\t} else\n\t\t\tif( t.titles != null && t.titles.length == t.header.length )\n\t\t\t\tfor( var c=0; c < numColumns; ++c )\n\t\t\t\t\ttableHtml.push('<td id=es1 title=\"' + t.titles[c].substring(0,1).toUpperCase() + t.titles[c].substring(1) + '\">' + t.header[c] + '</td>');\n\t\t\telse\n\t\t\t\tfor( var c=0; c < numColumns; ++c )\n\t\t\t\t\ttableHtml.push('<td id=es1>' + t.header[c] + '</td>');\n\t\ttableHtml.push('</tr>');\n\t} else\n\t\talert( \"Bad table header in '\" + tableId + \"'\" );\n\n\tif( typeof(t.addDataRowsFunction) == \"function\" )\n\t\ttableHtml.push(t.addDataRowsFunction(tableId, t, numColumns));\n\telse\n\t\ttableHtml.push(addTreeTableDataRows(tableId, t, numColumns));\n\n\ttableHtml.push('</table>');\n\tcontainerElement.innerHTML = tableHtml.join(\"\");\n\treturn containerElement;\n}\n\nfunction tglShowTreeTable(tableId)\n{\n\tvar branch = tableId.split(\"_/_\");\n\tvar leaf = branch[branch.length-1];\n\tvar t = treeTables[leaf];\n\tif( !t.ready ) {\n\t\tt.ready = true;\n\t\tmakeTreeTable(tableId, document.getElementById(tableId + '_container'));\n\t} else\n\t\ttglDsp(tableId);\n}\n\nfunction addTreeTableDataRows(tableId, tableData, numColumns)\n{\n\tvar tableHtml = [];\n\tvar tableDataRows = tableData.data;\n\tvar tableMetadata = tableData.metadata;\n\n\tfor( var r=0, maxr=tableDataRows.length; r < maxr; ++r )\n\t{\n\t\tvar rowId = tableId + 'r' + r;\n\t\tvar rowData = tableDataRows[r];\n\t\tvar rowLength = tableDataRows[r].length;\n\t\tvar childTableId;\n\t\tvar childTableData;\n\n\t\ttableHtml.push('<tr id=' + rowId + '>');\n\n\t\tvar hasChild = typeof(rowData[rowLength-1]) == \"object\";\n\t\tif(hasChild) {\n\t\t\tchildTableId = rowData[rowLength-1].child;\n\t\t\tchildTableData = treeTables[childTableId];\n\t\t\ttableHtml.push('<td id=es2>');\n\t\t\ttableHtml.push(\"<a class=\\\"tb\\\" id=\\\"\" + rowId + \"b\\\" onclick=\\\"tglTreeTable('\" + tableId + \"',\" + r + \"); tglTreeAnchor('\" + rowId + \"b')\\\">\" + ( childTableData && childTableData.show ? '-' : '+' ) + \"</a>\");\n\t\t\ttableHtml.push('</td>');\n\t\t} else\n\t\t\ttableHtml.push('<td id=es2 />');\n\n\t\tfor( var c=0; c < rowLength; ++c )\n\t\t{\n\t\t\tvar colValue = rowData[c];\n\t\t\tif( c == rowLength-1 && typeof(colValue) == \"object\" )\n\t\t\t\tbreak;\n\t\t\tif( tableMetadata != null ) {\n\t\t\t\tif( tableMetadata[c] == 1 )\n\t\t\t\t\tcontinue;\n\t\t\t\telse if( tableMetadata[c] == 2 )\n\t\t\t\t\tcolValue = stringIndex[colValue];\n\t\t\t}\n\t\t\tvar colStyle = \"text-align:right\";\n\t\t\tif( typeof(colValue) == \"string\" )\n\t\t\t\tcolStyle = \"text-align:left\";\n\t\t\ttableHtml.push('<td style=\"' + colStyle + '\">' + colValue + '</td>');\n\t\t}\n\t\ttableHtml.push('</tr>');\n\n\t\tif( hasChild && childTableData && childTableData.show )\n\t\t{\n\t\t\tvar childTableRowId = tableId + \"_/_\" + childTableId + 'r';\n\t\t\tvar childTableContainer = makeTreeTable(tableId + \"_/_\" + childTableId);\n\t\t\ttableHtml.push('<tr id=\"' + childTableRowId + '\"><td /><td colspan=\"' + numColumns + '\">' + childTableContainer.innerHTML + '</td></tr>');\n\t\t}\n\t}\n\treturn tableHtml.join(\"\");\n}\n\nfunction sortTreeTable(tableId, sortIndexes)\n{\n\tvar branch = tableId.split(\"_/_\");\n\tvar leaf = branch[branch.length-1];\n\tvar t = treeTables[leaf];\n\n\tvar tableFontSize = \"12pt\";\n\tif( branch.length > 1 )\n\t\ttableFontSize = \"10pt\";\n\n\tif( sortIndexes.join() == t.curSortIndexes.join() )\n\t\tfor( var i=0; i < sortIndexes.length; ++i )\n\t\t\tsortIndexes[i] *= -1;\n\tt.curSortIndexes = sortIndexes;\n\n\tmakeTreeTable(tableId, document.getElementById(tableId + '_container'));\n}\n\nfunction tglTreeTable(parentTableId, parentRowDataIndex)\n{\n\tvar branch = parentTableId.split(\"_/_\");\n\tvar leaf = branch[branch.length-1];\n\tvar parentTableData = treeTables[leaf];\n\tvar parentTable = document.getElementById(parentTableId);\n\tvar parentRowData = parentTableData['data'][parentRowDataIndex];\n\tvar parentRowId = parentTableId + 'r' + parentRowDataIndex;\n\tvar parentRowTableIndex = document.getElementById(parentRowId).rowIndex;\n\tvar childTableId = parentTable.id + \"_/_\" + parentRowData[parentRowData.length-1]['child'];\n\tvar childTableRowId = childTableId + 'r';\n\n\tif( parentTable.rows[parentRowTableIndex+1] != null && parentTable.rows[parentRowTableIndex+1].id == childTableRowId )\n\t{\n\t\tparentTable.deleteRow(parentRowTableIndex+1);\n\t\treturn;\n\t}\n\n\tvar childTableRow = parentTable.insertRow(parentRowTableIndex+1);\n\tchildTableRow.id = childTableRowId;\n\tvar tmp = childTableRow.insertCell(0);\n\n\tvar childTableCell = childTableRow.insertCell(1);\n\tchildTableCell.colSpan = parentTable.rows[parentRowTableIndex].cells.length-1;\n\tchildTableCell.appendChild(makeTreeTable(childTableId));\n}\n\nvar curHighlightCols = new Array();\nfunction toggleAssTableHighlightCol(t,c)\n{\n\tvar tableElement = document.getElementById(t);\n\tvar targetCells = cellsToColor[t];\n\n\tvar bgcolor;\n\tvar fgcolor;\n\tvar didToggle = false;\n\n\twhile( curHighlightRows.length > 0 )\n\t\ttoggleAssTableHighlightRow(t,curHighlightRows[0]);\n\n\tfor( var r2=0; r2 < curHighlightCols.length; ++r2 )\n\t{\n\t\tvar curHighlightCol = curHighlightCols[r2];\n\t\tif( curHighlightCol == c )\n\t\t{\n\t\t    fgcolor = \"\";\n\t\t    bgcolor = \"\";\n\t\t    for( var j=0; j < targetCells[curHighlightCol-1][1].length; ++j )\n\t\t    {\n\t\t\t    var rowToColor = targetCells[curHighlightCol-1][1][j];\n\t\t\t    for( var i=0; i < tableElement.rows[rowToColor].cells.length; ++i )\n\t\t\t    {\n\t\t\t\t    tableElement.rows[rowToColor].cells[i].style.backgroundColor = bgcolor;\n\t\t\t\t    tableElement.rows[rowToColor].cells[i].style.color = fgcolor;\n\t\t\t    }\n\t\t    }\n\n\t\t    for( var i=0; i < tableElement.rows.length; ++i )\n\t\t    {\n\t\t\t    tableElement.rows[i].cells[curHighlightCol].style.backgroundColor = bgcolor;\n\t\t\t    tableElement.rows[i].cells[curHighlightCol].style.color = fgcolor;\n\t\t    }\n\t        curHighlightCols.splice(r2,1);\n\t\t    didToggle = true;\n\t\t    break;\n\t    }\n\t}\n\n    if( !didToggle )\n\t    curHighlightCols.push(c);\n\t\n\tfgcolor = \"rgb(255,255,255)\";\n\t\n\tfor( var r2=0; r2 < curHighlightCols.length; ++r2 )\n\t{\n\t\tvar curHighlightCol = curHighlightCols[r2];\n\t\tfor( var j=0; j < targetCells[curHighlightCol-1][1].length; ++j )\n\t\t{\n\t\t\tvar rowToColor = targetCells[curHighlightCol-1][1][j];\n\t\t\tfor( var i=0; i < tableElement.rows[rowToColor].cells.length; ++i )\n\t\t\t{\n\t\t\t    bgcolor = \"rgb(150,150,150)\";\n\t\t        if( tableElement.rows[rowToColor].cells[i].innerHTML == \"x\" )\n\t\t        {\n\t\t            var redValue = 255 - Math.min( Math.pow( tableElement.rows[curColorRow].cells[i].innerHTML, 2 ), 255 );\n\t\t\t        var colColorRGB = [255,redValue,redValue];\n\t\t\t        bgcolor = \"rgb(\"+(150-redValue)+\",\"+(150-redValue)+\",\"+(150-redValue)+\")\";\n                }\n\t\t\t\ttableElement.rows[rowToColor].cells[i].style.backgroundColor = bgcolor;\n\t\t\t\ttableElement.rows[rowToColor].cells[i].style.color = fgcolor;\n\t\t\t}\n\t\t}\n\t}\n\n\tfor( var r2=0; r2 < curHighlightCols.length; ++r2 )\n\t{\n\t\tvar curHighlightCol = curHighlightCols[r2];\n\t\tfor( var i=0; i < tableElement.rows.length; ++i )\n\t\t{\n\t\t\tbgcolor = \"rgb(100,100,100)\";\n\t\t    if( tableElement.rows[i].cells[curHighlightCol].innerHTML == \"x\" )\n\t\t    {\n\t\t        var redValue = 255 - Math.min( Math.pow( tableElement.rows[curColorRow].cells[curHighlightCol].innerHTML, 2 ), 255 );\n\t\t\t    var colColorRGB = [255,redValue,redValue];\n\t\t\t    bgcolor = \"rgb(\"+(100-redValue)+\",\"+(100-redValue)+\",\"+(100-redValue)+\")\";\n            }\n\t\t\ttableElement.rows[i].cells[curHighlightCol].style.backgroundColor = bgcolor;\n\t\t\ttableElement.rows[i].cells[curHighlightCol].style.color = fgcolor;\n\t\t}\n\t}\n\tsetAssTableColors(t,curColorRow);\n}\n\nvar curHighlightRows = new Array();\nfunction toggleAssTableHighlightRow(t,r)\n{\n\tvar tableElement = document.getElementById(t);\n\tvar targetCells = cellsToColor[t];\n\n\tvar bgcolor;\n\tvar fgcolor;\n\tvar didToggle = false;\n\n\twhile( curHighlightCols.length > 0 )\n\t\ttoggleAssTableHighlightCol(t,curHighlightCols[0]);\n\n\tfor( var r2=0; r2 < curHighlightRows.length; ++r2 )\n\t{\n\t\tvar curHighlightRow = curHighlightRows[r2];\n\t\tif( curHighlightRow == r )\n\t\t{\n\t\t\tfgcolor = \"\";\n\t\t\tbgcolor = \"\";\n\t\t\tfor( var i=0; i < targetCells.length; ++i )\n\t\t\t{\n\t\t\t\tvar colHasAss = false;\n\t\t\t\tfor( var j=0; j < targetCells[i][1].length && !colHasAss; ++j )\n\t\t\t\t{\n\t\t\t\t\tif( targetCells[i][1][j] == curHighlightRow )\n\t\t\t\t\t\tcolHasAss = true;\n\t\t\t\t}\n\t\t\t\tif( colHasAss )\n\t\t\t\t{\n\t\t\t\t\tvar colToColor = targetCells[i][0];\n\t\t\t\t\tfor( var j=0; j < tableElement.rows.length; ++j )\n\t\t\t\t\t{\n\t\t\t\t\t\ttableElement.rows[j].cells[colToColor].style.backgroundColor = bgcolor;\n\t\t\t\t\t\ttableElement.rows[j].cells[colToColor].style.color = fgcolor;\n\t\t\t\t\t}\n\t\t\t\t}\n\t\t\t}\n\t\n\t\t\tfor( var i=0; i < tableElement.rows[curHighlightRow].cells.length; ++i )\n\t\t\t{\n\t\t\t\ttableElement.rows[curHighlightRow].cells[i].style.backgroundColor = bgcolor;\n\t\t\t\ttableElement.rows[curHighlightRow].cells[i].style.color = fgcolor;\n\t\t\t}\n\t\t\tcurHighlightRows.splice(r2,1);\n\t\t\tdidToggle = true;\n\t\t\tbreak;\n\t\t}\n\t}\n\n\tif( !didToggle )\n\t\tcurHighlightRows.push(r);\n\n\tfgcolor = \"rgb(255,255,255)\";\n\tfor( var r2=0; r2 < curHighlightRows.length; ++r2 )\n\t{\n\t\tvar curHighlightRow = curHighlightRows[r2];\n\t\tfor( var i=0; i < targetCells.length; ++i )\n\t\t{\n\t\t\tvar colHasAss = false;\n\t\t\tfor( var j=0; j < targetCells[i][1].length && !colHasAss; ++j )\n\t\t\t{\n\t\t\t\tif( targetCells[i][1][j] == curHighlightRow )\n\t\t\t\t\tcolHasAss = true;\n\t\t\t}\n\t\t\tif( colHasAss )\n\t\t\t{\n\t\t\t\tvar colToColor = targetCells[i][0];\n\t\t\t\tfor( var j=0; j < tableElement.rows.length; ++j )\n\t\t\t\t{\n\t\t\t\t\tbgcolor = \"rgb(150,150,150)\";\n\t\t\t\t\tif( tableElement.rows[j].cells[colToColor].innerHTML == \"x\" )\n\t\t\t\t    {\n\t\t\t\t        var redValue = Math.min( Math.pow( tableElement.rows[curColorRow].cells[colToColor].innerHTML, 2 ), 255 );\n\t\t\t\t\t    var colColorRGB = [255,redValue,redValue];\n\t\t\t\t\t    bgcolor = \"rgb(\"+(150-redValue)+\",\"+(150-redValue)+\",\"+(150-redValue)+\")\";\n\t\t            }\n\t\t\t\t\ttableElement.rows[j].cells[colToColor].style.backgroundColor = bgcolor;\n\t\t\t\t\ttableElement.rows[j].cells[colToColor].style.color = fgcolor;\n\t\t\t\t}\n\t\t\t}\n\t\t}\n\t}\n\n\tfor( var r2=0; r2 < curHighlightRows.length; ++r2 )\n\t{\n\t\tvar curHighlightRow = curHighlightRows[r2];\n\t\tfor( var i=0; i < tableElement.rows[curHighlightRow].cells.length; ++i )\n\t\t{\n\t\t\tbgcolor = \"rgb(100,100,100)\";\n\t\t\tif( tableElement.rows[curHighlightRow].cells[i].innerHTML == \"x\" )\n\t\t\t{\n\t\t\t\tvar redValue = 255 - Math.min( Math.pow( tableElement.rows[curColorRow].cells[i].innerHTML, 2 ), 255 );\n\t\t\t    var colColorRGB = [255,redValue,redValue];\n\t\t\t    bgcolor = \"rgb(\"+(150-redValue)+\",\"+(100-redValue)+\",\"+(100-redValue)+\")\";\n\t\t\t}\n\t\t\ttableElement.rows[curHighlightRow].cells[i].style.backgroundColor = bgcolor;\n\t\t\ttableElement.rows[curHighlightRow].cells[i].style.color = fgcolor;\n\t\t}\n\t}\n\n\tsetAssTableColors(t,curColorRow);\n}\n\nfunction colorParse(c)\n{\n\tvar col = c.replace(/[\\#rgb\\(]*/,'');\n\tvar num = col.split(',');\n\tvar base = 10;\n\n\tvar ret = new Array(parseInt(num[0],base),parseInt(num[1],base),parseInt(num[2],base));\n\treturn(ret);\n}\n\nvar curColorRow;\nfunction setAssTableColors(t,r)\n{\n\tvar tableElement = document.getElementById(t);\n\tvar targetCells = cellsToColor[t];\n\tcurColorRow = r;\n\n\tfor( var i=1; i < 3; ++i )\n\t\tfor( var j=1; j < tableElement.rows[i].cells.length; ++j )\n\t\t{\n\t\t\tvar curColor = tableElement.rows[i].cells[j].style.backgroundColor;\n\t\t\tvar curColorRGB = colorParse(curColor);\n\t\t\tvar redValue = 255 - Math.min( Math.pow( tableElement.rows[i].cells[j].innerHTML, 2 ), 255 );\n\t\t\tvar colColorRGB = [255,redValue,redValue];\n\t\t\tvar finalColor;\n\t\t\tif( curColor == \"\" )\n\t\t\t\tfinalColor = \"rgb(\" + colColorRGB[0] + \",\" + colColorRGB[1] + \",\" + colColorRGB[2] + \")\";\n\t\t\telse\n\t\t\t\tfinalColor = \"rgb(\" + Math.round((curColorRGB[0]+colColorRGB[0])/2) + \",\" + Math.round((curColorRGB[1]+colColorRGB[1])/2) + \",\" + Math.round((curColorRGB[2]+colColorRGB[2])/2) + \")\";\n\t\t\ttableElement.rows[i].cells[j].style.backgroundColor = finalColor;\n\t\t}\n\n\tfor( var i=0; i < targetCells.length; ++i )\n\t{\n\t\tvar colToColor = targetCells[i][0];\n\t\tvar colColor = tableElement.rows[r].cells[colToColor].style.backgroundColor;\n\n\t\tfor( var j=0; j < targetCells[i][1].length; ++j )\n\t\t{\n\t\t\tvar finalColor;\n\t\t\tvar curColor = tableElement.rows[targetCells[i][1][j]].cells[colToColor].style.backgroundColor;\n\t\t\tif( curColor == \"\" )\n\t\t\t{\n\t\t\t\tfinalColor = colColor;\n\t\t\t} else\n\t\t\t{\n\t\t\t\tvar curColorRGB = colorParse(curColor);\n\t\t\t\tif( curColorRGB[0] == 255 )\n\t\t\t\t\tfinalColor = colColor;\n\t\t\t\telse\n\t\t\t\t{\n\t\t\t\t\tvar colColorRGB = colorParse(colColor);\n\t\t\t\t\tfinalColor = \"rgb(\" + Math.round((curColorRGB[0]+colColorRGB[0])/2) + \",\" + Math.round((curColorRGB[1]+colColorRGB[1])/2) + \",\" + Math.round((curColorRGB[2]+colColorRGB[2])/2) + \")\";\n\t\t\t\t}\n\t\t\t}\n\t\t\ttableElement.rows[targetCells[i][1][j]].cells[colToColor].style.backgroundColor = finalColor;\n\t\t}\n\t}\n}";
        }
        #endregion

        #region Stylesheet
        public static string assembleStylesheet()
        {
            return "span.tglcaption {\n\twhite-space:nowrap; \n\tcursor: pointer;\n\ttext-align:left;\n\ttext-decoration:underline\n}\ntable.t1 { display: table }\ntable.t2 { display: table }\ntable.t3 { display: table; border: 2px solid black; table-layout: fixed; border-collapse: collapse; font-family: 'Courier New', monospace }\n#protID {\n\tcolor: #2222ff;\n\tfont-family: \\\"MS Arial\\\", sans-serif;\n\tfont-style: normal;\n\tfont-weight: bold;\n\tfont-size: 12pt\n}\n#seq {\n\tfont-family: \\\"Courier New\\\", monospace;\n\tfont-style: normal;\n\tfont-size: 12pt\n}\n#es1 {\tbackground-color: #BBBBBB; text-align:center }\n#es2 {\tbackground-color: #FFFFFF }\n#es3 {\tbackground-color: #DCDCFF }\ntd.bs1 {\tborder: 1px solid gray }\ntr.h1 {\tbackground-color: #BBBBBB; text-align:center }\ntr.h2 {\tbackground-color: #FFFFFF }\ntr.h3 {\tbackground-color: #DCDCFF }\na.tb {\n\tcursor: pointer;\n\tfont-family: \\\"Courier New\\\", monospace;\n\tfont-style: normal;\n\tfont-size: 10pt\n}\na.txtBtn {\n\tcursor: pointer;\n\ttext-decoration:underline\n}\n .index {\n\tposition: relative;\n\tbottom: 0.4em;\n\tcolor: blue;\n\t font-size: 0.67em;\n\t font-weight: bold\n}\n .indexAnn {\n\tposition: relative;\n\tbottom: 0.15em;\n\tcolor: blue;\n\t font-size: 0.8em;\n}\n";
        }
        #endregion

        #region HTML/Web presentation
        public static string assembleReportIndex( string outputPrefix, string navFrameFilename, string defaultPageFilename )
        {
            return "<html>\n\t<title>" + outputPrefix[0].ToString().ToUpper() + outputPrefix.Substring( 1 ) +
                    " IDPicker Analysis</title>\n\t<frameset cols=\"240,*\">\n" +
                    "\t\t<frame src=\"" + navFrameFilename + "\" />\n" +
                    "\t\t<frame src=\"" + defaultPageFilename + "\" name=\"mainFrame\" />\n" +
                    "\t\t</frameset>\n</html>\n";
        }

        public static void assembleNavFrameHtml( Workspace ws, StreamWriter sw, string outputPrefix, Dictionary<string, string> navigationMap )
        {
            sw.Write( "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" +
                                "<script language=javascript>\n\tvar treeTables = new Array;\n\ttreeTables['nav'] = { caption:'cluster table of contents', " +
                                "show:true, sortable:true, header:['Id','Protein Groups','Unique Results','Spectra'], " +
                                "titles:['cluster id','number of protein groups in cluster','number of unique results in cluster','total number of spectra in cluster'], " +
                                "metadata:[1,0,0,0,0], headerSortIndexes:[[1],[-3,-4,-5,1],[-4,-3,-5,1],[-5,-4,-3,1]], data:[" );
            for( int i = 0; i < ws.clusters.Count; ++i )
            {
                int spectraCount = 0;
                foreach( ResultInfo r in ws.clusters[i].results )
                    spectraCount += r.spectra.Count;

                if( i > 0 )
                    sw.Write( "," );
                string escapedOutputPrefix = outputPrefix.Replace( "\'", "\\\'" );
                string clusterSummaryFilename = escapedOutputPrefix + "-cluster" + ws.clusters[i].id + ".html";
                sw.Write( "[" + ws.clusters[i].id + ",'<a href=\"" + clusterSummaryFilename + "\" target=\"mainFrame\">" +
                                    ws.clusters[i].id + "</a>'," + ws.clusters[i].proteinGroups.Count + "," +
                                    ws.clusters[i].results.Count + "," + spectraCount + "]" );
            }

            sw.Write( "] };\n</script>\n<html>\n\t<head>\n\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n\t<body>\n" );
            foreach( KeyValuePair<string, string> pair in navigationMap )
            {
                sw.Write( "\t\t<a href=\"" + pair.Value + "\" target=\"mainFrame\">" + pair.Key + "</a><br />\n" );
            }
            sw.Write( "\t\t<br /><script language=javascript>document.body.appendChild(makeTreeTable('nav'))</script>\n" +
                                "\t</body>\n</html>\n" );
        }

        public static void assembleIndexByProteinHtml( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                                "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" +
                                "<html>\n\t<head>\n\t\t<title>" + outputPrefix + " index by protein</title>\n\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n" +
                                "\t<body>\n" );

            sw.Write( "\t\t<table>\n\t\t\t<tr id=es1><td>Protein</td><td>Description</td></tr>\n" );
            foreach( ProteinInfo pro in ws.proteins.Values )
            {
                sw.Write( "\t\t\t<tr><td><a href=\"" + outputPrefix + "-cluster" + pro.proteinGroup.cluster + ".html\">" +
                                    pro.locus + "</a></td><td>" + pro.description + "</td></tr>\n" );
            }
            sw.Write( "\t\t</table>\n" );

            sw.Write( "\t</body>\n</html>\n" );
        }

        public static void assembleIndexBySpectrumHtml( Workspace ws, StreamWriter sw, string outputPrefix, bool allowSharedSourceNames )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                                "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" );

            sw.Write( "<script language=javascript>\n\tvar treeTables = new Array;\n" );

            sw.Write( "\ttreeTables['IndexBySpectrum'] = { caption:'index by spectrum', show:true, sortable:true, " +
                                "headerSortIndexes: [[1],[-2],[-3]], " +
                                "header:['Group','Sources','Spectra'], " +
                                "titles:['canonical group name','number of sources','total number of spectra'], " +
                                "metadata:[2,0,0], data:[" );

            Set<string> usedStrings = new Set<string>();
            foreach( SourceInfo source in ws.groups["/"].getSources( true ) )
            {
                usedStrings.Add( source.group.name );
                usedStrings.Add( source.name );
                usedStrings.Add( source.ToString() );
            }

            foreach( SpectrumList.MapPair sItr in ws.spectra )
            {
                usedStrings.Add( sItr.Value.results[1].info.ToString() );
            }

            Map<string, int> stringIndex = new Map<string, int>();
            foreach( string str in usedStrings )
            {
                stringIndex.Add( str, stringIndex.Count );
            }

            bool tableHasFirstGroup = false;
            StringBuilder sourceDetailsStream = new StringBuilder();
            StringBuilder scanDetailsStream = new StringBuilder();
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                SourceGroupInfo group = groupItr.Value;

                Set<SourceInfo> sources = group.getSources();
                if( sources.Count == 0 )
                    continue;

                int groupIds = 0;
                bool groupHasSourceDetails = false;
                sourceDetailsStream.Append( "\ttreeTables['" + group.name + "'] = { sortable:true, " +
                                            "header:['Source','Spectra'], titles:['source name','number of spectra'], " +
                                            "metadata:[2,0], data:[" );
                foreach( SourceInfo source in sources )
                {
                    if( !groupHasSourceDetails )
                        groupHasSourceDetails = true;
                    else
                        sourceDetailsStream.Append( "," );
                    sourceDetailsStream.Append( "[" + stringIndex[source.name] + "," + source.spectra.Count + ",{child:'" + source.ToString() + "'}]" );
                    scanDetailsStream.Append( "\ttreeTables['" + source.ToString() + "'] = { sortable:true, " +
                                                "header:['Scan','z','Precursor mass','Retention time','FDR','Sequence', 'Calculated Mass', 'Mass Error'], " +
                                                "headerSortIndexes: [[1,3],[3],[4],[5],[6],[7],[8],[9]], " +
                                                "titles:['index number','charge state','precursor mass','retention time in minutes','false discovery rate','peptide sequence', 'calculated mass', 'mass error'], " +
                                                "metadata:[1,0,0,0,0,0,0,0,0], data:[" );
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                    {
                        SpectrumInfo s = sItr.Value;

                        VariantInfo firstVariant = s.results[1].info.peptides.Min;
                        PeptideInfo firstPeptide = firstVariant.peptide;
                        ResultInstanceModList.Enumerator firstMods = s.results[1].mods.Find( firstPeptide );
                        float indistinctPeptideSequenceMass = firstPeptide.mass + ( firstMods.IsValid ? firstMods.Current.Value[0].Mass : 0 );
                        float indistinctPeptideSequenceMassError = indistinctPeptideSequenceMass - s.precursorMass;

                        if( sItr != source.spectra.Min )
                            scanDetailsStream.Append( "," );
                        string scanStr;
                        if( allowSharedSourceNames && ws.sharedSpectra[s.id].Count > 1 )
                            scanStr = "<font style=\\\"color:red\\\">" + s.id.index.ToString() + "</font>";
                        else
                            scanStr = s.id.index.ToString();
                        scanDetailsStream.AppendFormat( "[{0},\"{1}\",{2},{3},{4},{5},annotatePTMsInPeptide({6}),{7},{8}]",
                                                        s.id.index, scanStr, s.id.charge,
                                                        s.precursorMass, s.retentionTime,
                                                        Math.Round( s.results[1].FDR, 2 ),
                                                        stringIndex[s.results[1].info.ToString()],
                                                        indistinctPeptideSequenceMass,
                                                        indistinctPeptideSequenceMassError );
                    }
                    scanDetailsStream.Append( "] };\n" );

                    groupIds += source.spectra.Count;
                }
                sourceDetailsStream.Append( "] };\n" );

                if( tableHasFirstGroup )
                    sw.Write( "," );
                tableHasFirstGroup = true;
                sw.Write( String.Format( "[{0},{1},{2},{{child:'{3}'}}]",
                    stringIndex[group.name], sources.Count, groupIds, group.name ) );
            }
            sw.Write( "] };\n" );

            SortedList<int, string> reverseStringIndex = new SortedList<int, string>();
            foreach( Map<string, int>.MapPair itr in stringIndex )
                reverseStringIndex.Add( itr.Value, itr.Key );

            StringBuilder stringIndexStream = new StringBuilder();
            stringIndexStream.Append( "\tvar stringIndex = new Array( \"" );
            foreach( string str in reverseStringIndex.Values )
            {
                stringIndexStream.Append( ( str == reverseStringIndex.Values[0] ? "" : "\", \"" ) );
                foreach( char charData in str )
                {
                    switch( charData )
                    {
                        case '"': stringIndexStream.Append( "&quot;" ); break;
                        case '\'': stringIndexStream.Append( "&apos;" ); break;
                        case '<': stringIndexStream.Append( "&lt;" ); break;
                        case '>': stringIndexStream.Append( "&gt;" ); break;
                        case '&': stringIndexStream.Append( "&amp;" ); break;
                        default: stringIndexStream.Append( charData ); break;
                    }
                }
            }
            stringIndexStream.Append( "\");\n" );
            sw.WriteLine( ptmAnnotation );
            sw.WriteLine( stringIndexStream.ToString() );
            sw.WriteLine( sourceDetailsStream.ToString() );
            sw.WriteLine( scanDetailsStream.ToString() );
            sw.Write( "</script>\n" );

            sw.Write( "<html>\n\t<head>\n\t\t<title>" + outputPrefix + " index by spectrum</title>\n\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n" +
                      "\t<body>\n" );
            sw.Write( "\t\t<script language=javascript>document.body.appendChild(makeTreeTable('IndexBySpectrum'))</script><br />\n" );
            sw.Write( "\t</body>\n</html>\n" );
        }

        class InstanceComparer : IComparer<ResultInstance>
        {
            public int Compare( ResultInstance x, ResultInstance y )
            {
                return x.spectrum.CompareTo( y.spectrum );
            }
        }

        public static void assembleIndexByModificationHtml( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                      "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" );

            sw.Write( "<script language=javascript>\n" +
                      ptmAnnotation +
                      "\n\tvar treeTables = new Array;\n" );

            sw.Write( "\ttreeTables['IndexByModification'] = { caption:'index by modification', show:true, sortable:true, " +
                                "headerSortIndexes: [[1],[2],[3],[4]], " +
                                "header:['Modified site','Mass','Results','Spectra'], " +
                                "titles:['modified terminus or amino acid residue','the modification\\'s mass','total number of results','total number of spectra'], " +
                                "data:[" );

            int unmodifiedSpectra = 0;
            Map<ModInfo, Map<ResultInfo, Set<ResultInstance>>> modIndex =
                new Map<ModInfo, Map<ResultInfo, Set<ResultInstance>>>( new ModInfo.SortByIgnoringPosition() );
            Set<ResultInfo> unmodifiedResultsSet = new Set<ResultInfo>();

            // For each spectrum
            foreach( SpectrumList.MapPair spectrumItr in ws.spectra )
            {
                bool haveMods = false;
                // For each result instance
                foreach( ResultInstance i in spectrumItr.Value.results.Values )
                {

                    // Get the peptides
                    PeptideList.Enumerator pepItr = i.info.peptides.GetEnumerator();
                    while( pepItr.MoveNext() )
                    {
                        // Get the protein counts mapped to the current peptide
                        // This variable allows us to identify peptides with no
                        // associdated proteins due to the parsimony filtering.
                        int count = pepItr.Current.peptide.proteins.Count;
                        if( count <= 0 )
                        {
                            continue;
                        }
                        // Get the mods for the peptide that are indexed
                        // by their position in the peptide.
                        ModMap.Enumerator mods = pepItr.Current.mods.GetEnumerator();
                        while( mods.MoveNext() )
                        {
                            // Get a list of mods at a particular position
                            List<ModInfo>.Enumerator allMods = mods.Current.Value.GetEnumerator();
                            // For each mod
                            while( allMods.MoveNext() )
                            {
                                // Key the result with the mod
                                ModInfo modKey = allMods.Current;
                                Map<ResultInfo, Set<ResultInstance>>.InsertResult pair =
                                        modIndex[modKey].Insert( i.info,
                                                              new Set<ResultInstance>( new InstanceComparer() ) );
                                pair.Element.Value.Add( i );
                                haveMods = true;
                            }
                        }
                    }
                }
                // Count the number of unmodified peptides
                if( !haveMods )
                {
                    ++unmodifiedSpectra;
                    unmodifiedResultsSet.Insert( spectrumItr.Value.results[1].info );
                }
            }

            /*foreach( SpectrumList.MapPair sItr in ws.spectra )
            {
                int modCount = sItr.Value.results[1].mods.Count;
                if( modCount > 0 )
                {
                    foreach( ResultInstanceModList.MapPair modItr in sItr.Value.results[1].mods )
                    {
                        foreach( ModMap modMap in modItr.Value )
                            foreach( List<ModInfo> modList in modMap.Values )
                                foreach( ModInfo mod in modList )
                                {
                                    //if( ws.distinctPeptideSettings.testModIsDistinct( mod.ToHypotheticalModInfo() ) )
                                    //{

                                    Map<ResultInfo, Set<ResultInstance>>.InsertResult pair =
                                        modIndex[mod].Insert( sItr.Value.results[1].info,
                                                              new Set<ResultInstance>( new InstanceComparer() ) );
                                    pair.Element.Value.Add( sItr.Value.results[1] );
                                    //}
                                }
                    }
                } else 
                {
                    ++unmodifiedSpectra;
                    unmodifiedResultsSet.Insert( sItr.Value.results[1].info );
                }
            }*/

            sw.Write( String.Format( "['(none)',{0},{1},{2}]", 0, unmodifiedResultsSet.Count, unmodifiedSpectra ) );
            //sw.Write( String.Format( ",['Modified',{0},{1}]", 0, modifiedCount );

            StringBuilder modDetailsStream = new StringBuilder();
            //bool isFirstMod = true;
            foreach( Map<ModInfo, Map<ResultInfo, Set<ResultInstance>>>.MapPair modItr in modIndex )
            {
                //if( !isFirstMod )
                sw.Write( "," );
                //else
                //	isFirstMod = false;

                ModInfo mod = modItr.Key;
                modDetailsStream.Append( "\ttreeTables['" + mod.ToString() + "'] = { sortable:true, " +
                                            "header:['Sequence','GID','CID','Spectra'], " +
                                            "titles:['peptide sequence (lexically)','group ID','cluster ID','number of spectra'], " +
                                            "data:[" );
                bool isFirstResult = true;
                int modSpectraCount = 0;
                foreach( Map<ResultInfo, Set<ResultInstance>>.MapPair rItr in modItr.Value )
                {
                    if( !isFirstResult )
                        modDetailsStream.Append( "," );
                    else
                        isFirstResult = false;

                    ResultInfo r = rItr.Key;
                    modDetailsStream.AppendFormat( "[annotatePTMsInPeptide('{0}'),{1},'<a href=\"{2}-cluster{3}.html\">{3}</a>',{4}]", r.ToString(), r.peptideGroup.id, outputPrefix, r.peptideGroup.cluster, rItr.Value.Count );

                    modSpectraCount += rItr.Value.Count;
                }
                modDetailsStream.Append( "] };\n" );

                if( mod.position == 'n' )
                    sw.Write( String.Format( "['N terminus',{0},{1},{2}", mod.mass, modItr.Value.Count, modSpectraCount ) );
                else if( mod.position == 'c' )
                    sw.Write( String.Format( "['C terminus',{0},{1},{2}", mod.mass, modItr.Value.Count, modSpectraCount ) );
                else
                    sw.Write( String.Format( "['{0}',{1},{2},{3}", mod.residue, mod.mass, modItr.Value.Count, modSpectraCount ) );
                sw.Write( ",{child:'" + mod.ToString() + "'}]" );
            }
            sw.Write( "] };\n" + modDetailsStream.ToString() + "</script>\n" );

            sw.Write( "<html>\n\t<head>\n\t\t<title>" + outputPrefix + " index by modification</title>\n\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n" +
                                "\t<body>\n" );
            sw.Write( "\t\t<script language=javascript>document.body.appendChild(makeTreeTable('IndexByModification'))</script><br />\n" );
            sw.Write( "\t</body>\n</html>\n" );
        }

        public static void assembleGroupAssociationHtml( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                                "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" +
                                "<html>\n\t<head>\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n" +
                                "\t<body>\n" );

            List<SourceGroupInfo> sortedGroups = new List<SourceGroupInfo>( ws.groups.Values );
            sortedGroups.Sort( SourceGroupList.SortAscendingByPathDepthThenName );

            SortedList<int, SortedList<int, ProteinGroupList>> sortedProteinGroups = new SortedList<int, SortedList<int, ProteinGroupList>>();
            foreach( ProteinGroupInfo proGroup in ws.proteinGroups )
            {
                if( !sortedProteinGroups.ContainsKey( -proGroup.results.Count ) )
                    sortedProteinGroups[-proGroup.results.Count] = new SortedList<int, ProteinGroupList>();
                if( !sortedProteinGroups[-proGroup.results.Count].ContainsKey( -proGroup.spectra.Count ) )
                    sortedProteinGroups[-proGroup.results.Count][-proGroup.spectra.Count] = new ProteinGroupList();
                sortedProteinGroups[-proGroup.results.Count][-proGroup.spectra.Count].Add( proGroup );
            }

            string table3id = "summaryt3";
            sw.Write( "\t\t<span class=tglcaption id=\"" + table3id + "c\" onclick=\"tglDsp('" + table3id +
                                "'); tglCptn('" + table3id + "c')\">Hide association table</span>\n" +
                                "\t\t<table>\n\t\t\t<tr><td><table style=\"border: 2px solid black; border-collapse: collapse; font-family: 'Courier New', monospace\" id=\"" + table3id + "\">\n" );

            Map<int, SourceGroupList> groupsByDepth = new Map<int, SourceGroupList>();

            int maxPathDepth = 0;
            foreach( SourceGroupInfo group in ws.groups.Values )
            {
                int pathDepth = group.getGroupPathDepth();
                maxPathDepth = Math.Max( maxPathDepth, pathDepth );
                SourceGroupList groupsOfSameDepth = groupsByDepth[pathDepth]; ;
                groupsOfSameDepth.Add( group.name, group );
            }
            /*int maxGroupNameLength = 0;
            foreach( SourceGroupInfo group in ws.groups.Values )
            {
                maxGroupNameLength = Math.Max( maxGroupNameLength, group.name.Length );
            }
            int equalWidth = maxGroupNameLength * 6;*/

            foreach( Map<int, SourceGroupList>.MapPair itr in groupsByDepth )
                if( itr.Key > 0 && itr.Key < maxPathDepth )
                {
                    sw.Write( "\t\t\t\t<tr id=es1 align=\"middle\"><td id=es2 /><td id=es2 /><td id=es2 />" );
                    foreach( SourceGroupInfo group in itr.Value.Values )
                    {
                        int colSpan = group.getChildGroups().Count + ( group.getSources().Count > 0 ? 1 : 0 );
                        sw.Write( "<td class=bs1 colspan=\"" + colSpan + "\">" + group.getGroupName() + "</td>" );
                    }
                    sw.Write( "</tr>\n" );
                }

            sw.Write( "\t\t\t\t<tr id=es1 align=\"middle\" style=\"white-space:nowrap\"><td class=bs1>GID</td>" +
                                "<td class=bs1>Sequences</td><td class=bs1 style=\"border-right: 2px solid black\">Spectra</td>" );
            string longestCommonSubstring = groupsByDepth[maxPathDepth].Values[0].getGroupName();
            foreach( SourceGroupInfo group in groupsByDepth[maxPathDepth].Values )
                Util.LongestCommonSubstring( group.getGroupName(), longestCommonSubstring, out longestCommonSubstring );
            foreach( Map<int, SourceGroupList>.MapPair itr in groupsByDepth )
                foreach( SourceGroupInfo group in itr.Value.Values )
                {
                    if( group.getGroupPathDepth() < maxPathDepth )
                    {
                        if( group.isLeafGroup() )
                            sw.Write( "<td class=bs1 />" );
                        continue;
                    }
                    string shortName = group.getGroupName();
                    if( groupsByDepth.Find( maxPathDepth ).Current.Value.Values.Count > 1 && longestCommonSubstring.Length > 4 )
                        shortName = shortName.Replace( longestCommonSubstring, "..." );
                    sw.Write( "<td class=bs1>" + shortName + "</td>" );
                    if( group.parent != null && group.parent.getSources().Count > 0 )
                        sw.Write( "<td class=bs1 />" );
                }
            sw.Write( "</tr>\n" );

            //proteinGroupId = new AlphaIndex();
            //foreach( ProteinGroupInfo proGroup in proteinGroupsBySource )
            foreach( SortedList<int, ProteinGroupList> bySpectralCount in sortedProteinGroups.Values )
                foreach( ProteinGroupList proGroupList in bySpectralCount.Values )
                    foreach( ProteinGroupInfo proGroup in proGroupList )
                    {
                        sw.Write( "\t\t\t\t<tr align=\"middle\"><td id=es1 class=bs1><a href=\"" + outputPrefix + "-cluster" + proGroup.cluster + ".html#" + proGroup.id + "\">" + proGroup.id + "</td>" +
                                            "<td class=bs1>" + proGroup.results.Count + "</td><td class=bs1 style=\"border-right: 2px solid black\">" + proGroup.spectra.Count + "</td>" );
                        foreach( SourceGroupInfo group in sortedGroups )
                        {
                            if( group.getGroupPathDepth() < maxPathDepth )
                            {
                                if( group.isLeafGroup() )
                                    sw.Write( "<td class=bs1 />" );
                                continue;
                            }
                            if( proGroup.sourceGroups.Contains( group.name ) )
                                sw.Write( "<td class=bs1>x</td>" );
                            else
                                sw.Write( "<td class=bs1 />" );

                            if( group.parent != null && group.parent.getSources().Count > 0 )
                                if( proGroup.sourceGroups.Contains( group.parent.name ) )
                                    sw.Write( "<td class=bs1>x</td>" );
                                else
                                    sw.Write( "<td class=bs1 />" );
                        }
                        sw.Write( "</tr>\n" );
                        //++proteinGroupId;
                    }
            sw.Write( "\t\t\t</table></tr></td>\n\t\t</table>\n" );

            sw.Write( "\t</body>\n</html>\n" );
        }

        public static void assembleSummaryHtml( Workspace ws, StreamWriter sw, string outputPrefix, Dictionary<string, string> parameterMap )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                                "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" );

            List<SourceGroupInfo> sortedGroups = new List<SourceGroupInfo>( ws.groups.Values );
            sortedGroups.Sort( SourceGroupList.SortAscendingByPathDepthThenName );

            sw.Write( "<script language=javascript>\n\tvar treeTables = new Array;\n" );

            sw.Write( "\ttreeTables['params'] = { caption:'analysis parameters', show:true, header:['Parameter','Value'], titles:['name of parameter','value of parameter'], data:[" +
                                "['Protein database','" + ( ws.dbFilepath != null ? ws.dbFilepath.Replace( "\\", "\\\\" ) : "" ) + "']" );
            foreach( KeyValuePair<string, string> pair in parameterMap )
            {
                sw.Write( ",['" + pair.Key + "','" + pair.Value + "']" );
            }
            sw.Write( "] };\n" );

            sw.Write( "\ttreeTables['summary'] = { caption:'groups summary', show:true, sortable:true, " +
                                "headerSortIndexes: [[1],[-3,2],[-4,2],[-5,2],[-6,2],[-7,2]], " +
                                "header:['Group','Confident Ids','Peptides','Peptide Groups','Proteins','Protein Groups'], " +
                                "titles:['canonical group name','total number of results','total number of distinct peptides','total number of peptide groups','total number of proteins','total number of protein groups'], " +
                                "metadata:[1,0,0,0,0,0,0], data:[" );
            StringBuilder sourceDetailsStream = new StringBuilder();
            int groupIndexByDepth = 0;
            foreach( SourceGroupInfo group in sortedGroups )
            {
                sourceDetailsStream.Append( "\ttreeTables['" + group.name + "'] = { sortable:true, header:['Source','Confident Ids','Peptides','Peptide Groups','Proteins','Protein Groups'], titles:['spectra source name','total number of results','total number of distinct peptides','total number of peptide groups','total number of proteins','total number of protein groups'], data:[" );
                int groupIds = 0;
                bool groupHasSourceDetails = false;
                Set<ResultInfo> groupResultSet = new Set<ResultInfo>();
                Set<VariantInfo> groupPeptideSet = new Set<VariantInfo>();
                Set<ProteinInfo> groupProteinSet = new Set<ProteinInfo>();
                Set<ProteinGroupInfo> groupProteinGroupSet = new Set<ProteinGroupInfo>();
                Set<PeptideGroupInfo> groupPeptideGroupSet = new Set<PeptideGroupInfo>();
                foreach( SourceInfo source in group.getSources( true ) )
                {
                    if( source.group == group )
                    {
                        if( !groupHasSourceDetails )
                            groupHasSourceDetails = true;
                        else
                            sourceDetailsStream.Append( "," );
                        string sourceDetailsLink;
                        if( source.filepath != null && File.Exists( source.filepath ) )
                            sourceDetailsLink = String.Format( "<a href=\"{0}/{1}-qonversion.txt\">{2}</a>",
                                                               new Uri(Path.GetDirectoryName(source.filepath)).ToString().Replace("'", "\\'"),
                                                               Uri.EscapeDataString(Path.GetFileNameWithoutExtension( source.filepath )).Replace("'", "\\'"),
                                                               source.name );
                        else
                            sourceDetailsLink = source.name;
                        sourceDetailsStream.Append( "['" + sourceDetailsLink + "'," );
                    }
                    Set<ResultInfo> sourceResultSet = new Set<ResultInfo>();
                    Set<VariantInfo> sourcePeptideSet = new Set<VariantInfo>();
                    Set<ProteinInfo> sourceProteinSet = new Set<ProteinInfo>();
                    Set<ProteinGroupInfo> sourceProteinGroupSet = new Set<ProteinGroupInfo>();
                    Set<PeptideGroupInfo> sourcePeptideGroupSet = new Set<PeptideGroupInfo>();
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values )
                        {
                            ResultInfo r = i.info;
                            if( sourceResultSet.Insert( r ).WasInserted )
                            {
                                sourcePeptideGroupSet.Add( r.peptideGroup );
                                foreach( VariantInfo pep in r.peptides )
                                {
                                    sourcePeptideSet.Add( pep );
                                    foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                                    {
                                        sourceProteinSet.Add( proItr.Value.protein );
                                        sourceProteinGroupSet.Add( proItr.Value.protein.proteinGroup );
                                    }
                                }
                            }
                        }
                    if( source.group == group )
                        sourceDetailsStream.Append( source.spectra.Count + "," + sourceResultSet.Count + "," +
                                                    sourcePeptideGroupSet.Count + "," + sourceProteinSet.Count + "," +
                                                    sourceProteinGroupSet.Count + "]" );

                    groupIds += source.spectra.Count;
                    groupResultSet.Union( sourceResultSet );
                    groupPeptideSet.Union( sourcePeptideSet );
                    groupProteinSet.Union( sourceProteinSet );
                    groupProteinGroupSet.Union( sourceProteinGroupSet );
                    groupPeptideGroupSet.Union( sourcePeptideGroupSet );
                }

                if( group != sortedGroups[0] )
                    sw.Write( "," );
                sw.Write( "[" + groupIndexByDepth + ",'" + group.name + "'," + groupIds + "," + groupResultSet.Count + "," +
                                    groupPeptideGroupSet.Count + "," + groupProteinSet.Count + "," +
                                    groupProteinGroupSet.Count );
                if( groupHasSourceDetails )
                    sw.Write( ",{child:'" + group.name + "'}" );
                sw.Write( "]" );
                sourceDetailsStream.Append( "] };\n" );
                ++groupIndexByDepth;
            }
            sw.Write( "] };\n" + sourceDetailsStream.ToString() + "</script>\n" );

            string summaryHistogramFilename = outputPrefix + "-sequences.svg";
            int svgWidth;

            StreamWriter graphOutputStream;
            graphOutputStream = new StreamWriter( summaryHistogramFilename );
            assembleClusterDensitySvg( ws, graphOutputStream, out svgWidth );
            graphOutputStream.Close();

            sw.Write( "<html>\n\t<head>\n\t\t<title>" + outputPrefix + " Summary</title>\n\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n" +
                                "\t<body>\n" );
            sw.Write( "\t\t<script language=javascript>document.body.appendChild(makeTreeTable('params'))</script><br />\n" );

            sw.Write( String.Format( "\t\tProtein-level FDR for this analysis: {0}<br />\n", Math.Round( ws.proteins.ProteinFDR, 3 ) ) );
            sw.Write( String.Format( "\t\tResult-level FDR for this analysis: {0}<br /><br />\n", Math.Round( ws.results.ResultFDR, 3 ) ) );

            sw.Write( "\t\t<script language=javascript>document.body.appendChild(makeTreeTable('summary'))</script><br />\n" +
                                "\t\t<embed src=\"" + summaryHistogramFilename + "\" width=\"" + svgWidth + "\" height=\"300\" type=\"image/svg+xml\" />\n" );
            sw.Write( "\t</body>\n</html>\n" );
        }

        public static void assembleClusterDensitySvg( Workspace ws, StreamWriter sw, out int svgWidth )
        {
            Histogram<int> hg = new Histogram<int>();

            if( ws.clusters.Count > 0 )
            {
                int minBin = (int) Math.Pow( 2.0, Math.Floor( Math.Log( ws.clusters[ws.clusters.Count - 1].results.Count ) / Math.Log( 2.0 ) ) );
                int maxBin = ws.clusters[0].results.Count;
                for( int bin = minBin; bin <= maxBin; bin <<= 1 )
                    hg.Add( bin, 0 );
            }

            foreach( ClusterInfo c in ws.clusters )
            {
                Histogram<int>.Enumerator itr = hg.UpperBound( c.results.Count );
                itr.MovePrev();
                ++itr.Current.Value;
            }
            int total = hg.getTotal();
            int height = 300;
            int width = hg.Count * 62;
            svgWidth = width;
            int hgWidth = width - 15;	// make room for the vertical axis line and label
            int hgx = 30;
            int hgHeight = height - 30;	// make room for the horizontal axis line and label, and bar labels

            int widthOfBars = 8;
            int spaceBetweenBars = (int) ( (float) ( hgWidth - widthOfBars * hg.Count ) / (float) ( hg.Count + 1 ) ) + widthOfBars;

            sw.Write( "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n" +
                                "<!DOCTYPE svg PUBLIC \"-//W3C//DTD SVG 1.0//EN\" \"http://www.w3.org/TR/2001/REC-SVG-20010904/DTD/svg10.dtd\">\n" +
                                "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"" + width + "\" height=\"" + height + "\">\n" +
                                "\t<script type=\"text/ecmascript\"><![CDATA[\n\t\tvar valueNode = null;\n\t\tvar textNode = null;\n" +
                                "\t\tfunction showBarValue(evt)\n\t\t{\n\t\t\tvar total = " + total + ";\n\t\t\tvalueNode = document.createElementNS( \"http://www.w3.org/2000/svg\", \"text\" );\n" +
                                "\t\t\tvalueNode.setAttributeNS( null, \"x\", parseInt( evt.target.getAttributeNS( null, \"x\" ) ) + " + widthOfBars / 2 + " );\n" +
                                "\t\t\tif( evt.target.parentNode.getAttributeNS( null, \"fill\" ) == \"red\" )\n" +
                                "\t\t\t\tvalueNode.setAttributeNS( null, \"y\", parseInt( evt.target.getAttributeNS( null, \"y\" ) ) - 3 );\n" +
                                "\t\t\telse\n" +
                                "\t\t\t\tvalueNode.setAttributeNS( null, \"y\", parseInt( evt.target.getAttributeNS( null, \"height\" ) ) - 3 );\n" +
                                "\t\t\tvar value = evt.target.getAttributeNS( null, \"value\" );\n\t\t\tvar percent = value / " + total + ";\n" +
                                "\t\t\ttextNode = document.createTextNode( value + \" (\" + ( percent * 100 ).toFixed() + \"%)\" );\n" +
                                "\t\t\tvalueNode.appendChild( textNode );\n\t\t\tdocument.getElementById(\"labels\").appendChild( valueNode );\n\t\t}\n" +
                                "\t\tfunction hideBarValue(evt)\n\t\t{\n\t\t\tdocument.getElementById(\"labels\").removeChild( valueNode );\n\t\t\tvalueNode = null;\n\t\t\ttextNode = null;\n\t\t}\n" +
                                "\t]]></script>\n" );

            StringBuilder axisLabels = new StringBuilder();

            double max = 0;
            foreach( Map<int, int>.MapPair itr in hg )
                max = (double) Math.Max( itr.Value, (double) max );
            max += 0.05 * max;
            max = Math.Min( (double) total, max );

            sw.Write( "\t<g fill=\"red\">\n" );
            int x = hgx;

            for( int i = 0; i < hg.Count; ++i )
            {
                x += spaceBetweenBars - ( widthOfBars / 2 );
                int barHeight = (int) ( ( (double) hg.Values[i] / max ) * 0.95 * (float) hgHeight ) + 1;
                int y = hgHeight - barHeight;
                sw.Write( "\t\t<rect width=\"" + widthOfBars + "\" height=\"" + barHeight +
                                    "\" x=\"" + x + "\" y=\"" + y + "\" value=\"" + hg.Values[i] +
                                    "\" onmouseover=\"showBarValue(evt)\" onmouseout=\"hideBarValue(evt)\" />\n" );

                axisLabels.Append( "\t\t<text x=\"" + ( x + widthOfBars / 2 ) + "\" y=\"" + ( hgHeight + 15 ) + "\">" );

                if( i + 1 == hg.Count )
                    axisLabels.Append( ">= " + hg.Keys[i] );
                else if( hg.Keys[i + 1] - hg.Keys[i] > 1 )
                    axisLabels.Append( hg.Keys[i] + ".." + ( hg.Keys[i + 1] - 1 ) );
                else
                    axisLabels.Append( hg.Keys[i] );
                axisLabels.Append( "</text>\n" );
            }
            sw.Write( "\t</g>\n" );

            sw.Write( "\t<g fill=\"white\">\n" );
            x = hgx;
            for( int i = 0; i < hg.Count; ++i )
            {
                x += spaceBetweenBars - ( widthOfBars / 2 );
                int barHeight = (int) ( ( (double) hg.Values[i] / max ) * 0.95 * (float) hgHeight ) + 1;
                int y = hgHeight - barHeight;
                sw.Write( "\t\t<rect width=\"" + widthOfBars + "\" height=\"" + y +
                          "\" x=\"" + x + "\" y=\"0\" value=\"" + hg.Values[i] +
                          "\" onmouseover=\"showBarValue(evt)\" onmouseout=\"hideBarValue(evt)\" />\n" );
            }
            sw.Write( "\t</g>\n" );

            sw.Write( "\t<g id=\"labels\" stroke=\"black\" stroke-width=\"0.3\" text-anchor=\"middle\" font-size=\"10\">\n" +
                                "\t\t<text font-size=\"12\" x=\"" + ( hgx + ( hgWidth / 2 ) ) + "\" y=\"" + ( height - 3 ) + "\">Total sequences</text>\n" +
                                "\t\t<text font-size=\"12\" x=\"0\" y=\"0\" transform=\"translate( " + ( hgx - 10 ) + ", " + ( hgHeight / 2 ) + " ) rotate(-90)\">Cluster density</text>\n" +
                                "\t\t<text x=\"" + ( hgx - 10 ) + "\" y=\"" + hgHeight + "\">0%</text>\n" +
                                "\t\t<text x=\"" + ( hgx - 15 ) + "\" y=\"" + 10 + "\">" + Math.Round( max / total * 100, 0 ) + "%</text>\n" +
                                axisLabels.ToString() +
                                "\t</g>\n" );

            sw.Write( "\t<g stroke=\"black\" stroke-width=\"2\">\n" +
                      "\t\t<line x1=\"" + hgx + "\" y1=\"" + hgHeight + "\" x2=\"" + width + "\" y2=\"" + hgHeight + "\" />\n" +
                      "\t\t<line x1=\"" + hgx + "\" y1=\"" + ( hgHeight + 1 ) + "\" x2=\"" + hgx + "\" y2=\"" + 0 + "\" />\n" +
                      "\t</g>\n" );

            sw.Write( "</svg>\n" );
        }

        public static void assembleClusterHtml( Workspace ws, StreamWriter sw, string outputPrefix, int index, bool linkToGraph )
        {
            assembleClusterHtml( ws, sw, outputPrefix, index, linkToGraph, "", "", "" );
        }

        public static void assembleClusterHtml( Workspace ws, StreamWriter sw, string outputPrefix, int index, bool linkToGraph,
                                                string rawSourceHostURL, string rawSourcePath, string rawSourceExtension )
        {
            ClusterInfo c = ws.clusters[index];
            int cid = index + 1;
            string table1id = "c" + cid + "t1";
            string table2id = "c" + cid + "t2";
            string table3id = "c" + cid + "t3";

            SpectrumList spectra = ws.spectra;
            ResultList results = ws.results;
            //PeptideList peptides = ws.peptides;
            ProteinList proteins = ws.proteins;
            PeptideGroupList peptideGroups = ws.peptideGroups;
            ProteinGroupList proteinGroups = ws.proteinGroups;
            ClusterList clusters = ws.clusters;
            SourceGroupList groups = ws.groups;

            string showPeptideTables = ( c.results.Count <= 20 ? "true" : "false" );

            SortedList<int, SortedList<int, ProteinGroupList>> sortedProteinGroups = new SortedList<int, SortedList<int, ProteinGroupList>>();
            foreach( ProteinGroupInfo proGroup in c.proteinGroups )
            {
                if( !sortedProteinGroups.ContainsKey( -proGroup.results.Count ) )
                    sortedProteinGroups[-proGroup.results.Count] = new SortedList<int, ProteinGroupList>();
                if( !sortedProteinGroups[-proGroup.results.Count].ContainsKey( -proGroup.spectra.Count ) )
                    sortedProteinGroups[-proGroup.results.Count][-proGroup.spectra.Count] = new ProteinGroupList();
                sortedProteinGroups[-proGroup.results.Count][-proGroup.spectra.Count].Add( proGroup );
            }

            Set<string> usedGroupsAndSources = new Set<string>();
            foreach( PeptideGroupInfo pepGroup in c.peptideGroups )
                foreach( ResultInfo r in pepGroup.results )
                    foreach( SpectrumInfo s in r.spectra.Values )
                    {
                        usedGroupsAndSources.Add( s.id.source.name );
                        usedGroupsAndSources.Add( s.id.source.ToString() );
                    }

            Map<string, int> stringIndex = new Map<string, int>();

            foreach( string str in usedGroupsAndSources )
            {
                stringIndex.Add( str, stringIndex.Count );
            }

            //clusters.Sort( ClusterList.SortDescendingBySequencesThenSpectra );

            /*string createLinkFunction = "var isDigit = /\\d/;\n" +
                                         "function createScanLink(sourceIndex, nativeID, charge, sequenceIndex) {\n" +
                                         "\tvar str = stringIndex[sequenceIndex]+'';\n" +
                                         "\tvar map = new Object();\n" +
                                         "\tvar interps = new Array();\n" +
                                         "\tinterps = str.split('/');\n" +
                                         "\tvar seqTokens = new Array();\n" +
                                         "\tseqTokens = interps[0].split('{');\n" +
                                         "\tvar seq=seqTokens[0];\n" +
                                         "\tvar newStr='';\n" +
                                         "\t\tif(seqTokens.length>1) {\n" +
                                         "\t\t\tvar massIndex=seqTokens[1].split('}');\n" +
                                         "\t\t\tvar masses = massIndex[0].split(',');\n" +
                                         "\t\t\tfor(var index=0; index<masses.length;++index) {\n" +
                                         "\t\t\t\tvar massMap = masses[index].split('=');\n" +
                                         "\t\t\t\tmap[massMap[0]]=massMap[1];\n" +
                                         "\t\t\t}\n" +
                                         "\t\t}\n" +
                                         "\t\tfor(var index = 0; index < seq.length; ++index) {\n" +
                                         "\t\t\tif(isDigit.test(seq.charAt(index)) && map[seq.charAt(index)]!=-100000) {\n" +
                                         "\t\t\t\tnewStr+='('+map[seq.charAt(index)]+')';\n" +
                                         "\t\t\t\tmap[seq.charAt(index)]=-100000;\n" +
                                         "\t\t\t} else {\n" +
                                         "\t\t\t\tnewStr+=seq.charAt(index);\n" +
                                         "\t\t\t}\n" +
                                         "\t\t}\n" +
                                         "\t\treturn '<a href=\"'+rawSourceHostURL+'cgi-bin/generateSpectrumSvg.cgi?source=\'+rawSourcePath+stringIndex[sourceIndex]+rawSourceExtension+'&id='+nativeID+'&charge='+charge+'&sequence='+newStr+'\">'+nativeID+'</a>';\n" +
                                         "\t}\n";*/
            StringBuilder detailsStream = new StringBuilder();
            detailsStream.Append( "\tvar detailsHeader = ['Source file','ID','z','Precursor mass','Calculated mass','Mass error','FDR'"/*,'Sequence'*/+ "];\n" +
                                    "\tvar detailsTitles = ['source file','native spectrum identifier','charge','precursor mass','calculated mass of sequence','difference between calculated mass and precursor mass','false discovery rate'];\n" +
                                    "\tvar detailsSortIndexes = [[1,2,3],[2],[3],[4],[5],[6],[7]"/*,[8]*/+"];\n" +
                                    "\tvar rawSourceHostURL = '" + rawSourceHostURL + "';\n" +
                                    "\tvar rawSourceExtension = '" + rawSourceExtension + "';\n" +
                                    "\tvar rawSourcePath = '" + rawSourcePath + "';\n" +
                "\tfunction createScanLink(sourceIndex, nativeID, charge, sequenceIndex) {\n" +
                "\t\treturn '<a href=\"'+rawSourceHostURL+'cgi-bin/generateSpectrumSvg.cgi?source='+rawSourcePath+" +
                "stringIndex[sourceIndex]+rawSourceExtension+'&id='+nativeID+'&charge='+charge+'&sequence='+" +
                "stringIndex[sequenceIndex]+'\">'+nativeID+'</a>';\n" +
                "\t}\n"
                                              );

            StringBuilder peptideTables = new StringBuilder();
            StringBuilder peptideDataTable = new StringBuilder();
            peptideDataTable.Append( "\tvar treeTables = new Array;\n\ttreeTables['" + table2id + "'] = { caption:'peptide groups', " +
                                    "show:" + showPeptideTables + ", addDataRowsFunction:addPeptideGroupsTableDataRows, sortable:true, headerSortIndexes:" +
                                    "[[3,1,6],[-4,1,6],[1,6],[-7,1,6],[8,1,6],[9,1,6],[10,1,6]], " +
                                    "header:['LGID','U','Sequence','Spectra','Calculated mass','Best FDR'], " +
                                    "titles:['local group ID','sequence uniqueness','sequence offset','spectral count','calculated mass of sequence'," +
                                    "'best FDR'], metadata:[1,1,0,0,1,0,0,0,0,0], data:[" );

            int peptideGroupId = 1;
            int peptidesInTable = 0;
            foreach( PeptideGroupInfo pepGroup in c.peptideGroups )
            {
                int peptideIndex = 0;
                foreach( ResultInfo r in pepGroup.results )
                {
                    float bestFDR = 1.0f;
                    foreach( SpectrumList.MapPair itr in r.spectra )
                    {
                        bestFDR = Math.Min( itr.Value.results.Values[0].FDR, bestFDR );

                    }

                    VariantInfo firstVariant = r.peptides.Min;
                    PeptideInfo firstPeptide = firstVariant.peptide;

                    int distinctResultStringIndex = stringIndex.Insert( r.ToString(), stringIndex.Count ).Element.Value;

                    string rowId = "c" + cid + "g" + peptideGroupId + "p" + peptideIndex;
                    peptideDataTable.Append( ( peptidesInTable == 0 ? "" : "," ) +
                                            "[" + firstPeptide.proteins.Min.Value.offset + "," + cid + "," + peptideGroupId + ",'" +
                                            ( firstPeptide.unique ? "*" : "" ) + "'," + peptideIndex + "," +
                                            "annotatePTMsInPeptide(" + distinctResultStringIndex + ")," + r.spectra.Count + "," + firstVariant.Mass + "," +
                                            Math.Round( bestFDR, 2 ) + ",{child:'" + rowId + "'}]" );


                    detailsStream.Append( "\ttreeTables['" + rowId + "'] = { show:false, sortable:true, metadata:[2,0,0,0,0,0,0], " +
                                            "headerSortIndexes:detailsSortIndexes, header:detailsHeader, data:[" );
                    foreach( SpectrumList.MapPair itr in r.spectra )
                    {
                        SpectrumInfo s = itr.Value;
                        ResultInstance resultInstance = itr.Value.results[1]; // rank 1
                        ResultInstanceModList.Enumerator firstMods = resultInstance.mods.Find( firstPeptide );
                        int sourcePathIndex = stringIndex.Insert( s.id.source.ToString(), stringIndex.Count ).Element.Value;
                        int indistinctResultStringIndex = stringIndex.Insert(resultInstance.ToSimpleString(), stringIndex.Count).Element.Value;

                        string scanLink = s.id.index.ToString();
                        if( rawSourcePath != String.Empty )
                        {
                            int sourceNameIndex = stringIndex.Insert( s.id.source.name, stringIndex.Count ).Element.Value;
                            scanLink = String.Format("createScanLink({0},'{1}',{2},{3})",
                                                     sourceNameIndex,
                                                     s.nativeID,
                                                     s.id.charge,
                                                     indistinctResultStringIndex);
                        }

                        double indistinctPeptideSequenceMass = firstPeptide.mass + ( firstMods.IsValid ? firstMods.Current.Value[0].Mass : 0 );
                        double indistinctPeptideSequenceMassError = indistinctPeptideSequenceMass - s.precursorMass;
                        detailsStream.AppendFormat("{0}[{1},{2},{3},{4},{5},{6},{7}]",
                                                   s.id == r.spectra.Min.Key ? "" : ",",
                                                   sourcePathIndex,
                                                   scanLink,
                                                   s.id.charge,
                                                   s.precursorMass,
                                                   indistinctPeptideSequenceMass,
                                                   indistinctPeptideSequenceMassError,
                                                   Math.Round(s.results.Values[0].FDR, 4)/*,
                                                   indistinctResultStringIndex*/);
                    }
                    detailsStream.Append( "] };\n" );

                    ++peptideIndex;
                    ++peptidesInTable;
                }
                ++peptideGroupId;
            }
            peptideDataTable.Append( "] };\n" );

            Map<int, List<int>> cellsToColorMap = new Map<int, List<int>>();
            StringBuilder cellsToColor = new StringBuilder();
            if( c.proteinGroups.Count > 1 )
            {
                for( int i = 0; i < c.peptideGroups.Count; ++i )
                    cellsToColorMap[i + 1] = new List<int>();

                int row = 3;
                foreach( SortedList<int, ProteinGroupList> bySpectralCount in sortedProteinGroups.Values )
                    foreach( ProteinGroupList proGroupList in bySpectralCount.Values )
                    {
                        ProteinGroupList.Enumerator rowItr = proGroupList.GetEnumerator(); rowItr.MoveNext();
                        for( int i = 0; i < proGroupList.Count; ++i, ++row, rowItr.MoveNext() )
                        {
                            PeptideGroupList.Enumerator colItr = c.peptideGroups.GetEnumerator(); colItr.MoveNext();
                            for( int col = 0; col < c.peptideGroups.Count; ++col, colItr.MoveNext() )
                            {
                                if( rowItr.Current.peptideGroups.Contains( colItr.Current ) )
                                    cellsToColorMap[col + 1].Add( row );
                            }
                        }
                    }

                cellsToColor.Append( "\tvar cellsToColor = new Array;\n\tcellsToColor['" + table3id + "'] = new Array( " );
                bool firstCell = true;
                foreach( Map<int, List<int>>.MapPair itr in cellsToColorMap )
                {
                    if( firstCell )
                        firstCell = false;
                    else
                        cellsToColor.Append( ", " );
                    cellsToColor.Append( "[" + itr.Key + ",[" + itr.Value[0] );
                    for( int i = 1; i < itr.Value.Count; ++i )
                        cellsToColor.Append( "," + itr.Value[i] );
                    cellsToColor.Append( "]]" );
                }
                cellsToColor.Append( " );\n" );
            }

            SortedList<int, string> reverseStringIndex = new SortedList<int, string>();
            foreach( Map<string, int>.MapPair itr in stringIndex )
                reverseStringIndex.Add( itr.Value, itr.Key );

            peptideTables.Append( "\tvar stringIndex = new Array( \"" );
            foreach( string str in reverseStringIndex.Values )
            {
                peptideTables.Append( ( str == reverseStringIndex.Values[0] ? "" : "\", \"" ) );
                //char prevChar = str[0];
                foreach( char charData in str )
                {
                    switch( charData )
                    {
                        case '"': peptideTables.Append( "&quot;" ); break;
                        //case '"': peptideTables.Append("\\\""); break;
                        case '\'': peptideTables.Append( "&apos;" ); break;
                        //case '<': peptideTables.Append( "&lt;" ); break;
                        //case '>': peptideTables.Append( "&gt;" ); break;
                        case '&': peptideTables.Append( "&amp;" ); break;
                        default: peptideTables.Append( charData ); break;
                    }
                    //prevChar = charData;
                }
            }
            peptideTables.Append( "\" );\n" );

            peptideTables.Append( ptmAnnotation );
            peptideTables.Append( peptideDataTable.ToString() );

            StringBuilder scriptStream = new StringBuilder();
            scriptStream.Append( "\tvar numClusters = " + clusters.Count + ";\n" +
                                    "\tvar numGroups = 1;\n" +
                                    "\tvar numSources = 1;\n" +
                                    "\tvar showPeptideModColumn = 0;\n" +
                                    "\tvar showAllTables = Array( true, false, true );\n" );

            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                      "<script language=javascript>\n" + scriptStream.ToString() + "</script>\n" +
                                "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" +
                                "<script language=javascript>\n" + peptideTables.ToString() + cellsToColor.ToString() + "</script>\n" +
                                "<html>\n\t<head>\n\t\t<title>" + outputPrefix + " cluster " + cid + "</title>\n" +
                                "\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />" +
                                "\n\t</head>\n\n\t<body>\n\t\t" );

            if( cid > 1 )
                sw.Write( "<a href=\"" + outputPrefix + "-cluster" + ( cid - 1 ) + ".html\">Prev</a>" );
            else
                sw.Write( "Prev" );
            if( cid < clusters.Count )
                sw.Write( " <a href=\"" + outputPrefix + "-cluster" + ( cid + 1 ) + ".html\">Next</a>" );
            else
                sw.Write( " Next" );
            sw.Write( "\n\t\t<br /><br />\n" );

            int spectraCount = 0;
            foreach( ResultInfo r in c.results.Keys )
                spectraCount += r.spectra.Count;

            if( linkToGraph )
                sw.Write( "\t\t<a href=\"" + outputPrefix + "-cluster" + cid + ".gif\">View bipartite graph for cluster " + cid +
                          "</a>, " + c.results.Count + " total sequences, " + spectraCount + " total spectra<br>\n" );
            else
                sw.Write( "\t\tCluster " + cid + ", " + c.results.Count + " unique results, " + spectraCount + " total spectra<br>\n" );

            string rowColor1 = "h2";
            string rowColor2 = "h3";
            string rowColor = rowColor1;

            sw.Write( "\t\t<span class=tglcaption id=\"" + table1id + "c\" onclick=\"tglDsp('" + table1id +
                                "'); tglCptn('" + table1id + "c')\">Hide protein groups</span>\n" +
                                "\t\t<table>\n\t\t\t<tr><td><table class=t1 id=\"" + table1id + "\">\n" );

            sw.Write( "\t\t\t\t<tr class=h1 align=\"middle\"><td>LGID</td><td>Protein</td><td>Sequences</td><td>Spectra</td><td>Description</td></tr>\n" );

            //int proteinsInTable = 0;
            AlphaIndex proteinGroupId = new AlphaIndex();
            foreach( SortedList<int, ProteinGroupList> bySpectralCount in sortedProteinGroups.Values )
                foreach( ProteinGroupList proGroupList in bySpectralCount.Values )
                    foreach( ProteinGroupInfo proGroup in proGroupList )
                    {
                        int proteinIndex = 0;

                        foreach( ProteinInfo pro in proGroup.proteins.Values )
                        {
                            string rowId = "c" + cid + "g" + proteinGroupId + "p" + proteinIndex;

                            sw.Write( "\t\t\t\t<tr id=\"" + rowId + "\" align=\"middle\" class=" + rowColor + ">" );

                            if( proteinIndex > 0 )
                                sw.Write( "<td />" );
                            else
                                sw.Write( "<td><a name=\"" + proGroup.id + "\">" + proteinGroupId + "</a></td>" );

                            string clippedName;
                            if( pro.locus.Length > 40 )
                                clippedName = pro.locus.Substring( 0, 37 ) + "...";
                            else
                                clippedName = pro.locus;

                            clippedName = String.Format( "<a href=\"http://idpicker/cgi-bin/generateSequenceCoverage.cgi?db={0}&accession={1}\">{2}</a>",
                                                         ws.dbFilepath, pro.locus, clippedName );
                            sw.Write( "<td align=\"left\" id=protID>" + clippedName + "</td>" );

                            if( proteinIndex == 0 )
                            {
                                sw.Write( "<td align=\"right\">" + proGroup.results.Count + "</td>" +
                                                    "<td align=\"right\">" + proGroup.spectra.Count + "</td>" );
                            } else
                            {
                                sw.Write( "<td /><td />" );
                            }

                            StringBuilder fullDesc = new StringBuilder();
                            if( pro.description.Length > 0 )
                                fullDesc.Append( pro.description );
                            else
                                fullDesc.Append( "n/a" );

                            for( int i = 0; i < fullDesc.Length; ++i )
                                if( fullDesc[i] == '|' || fullDesc[i] == ';' || fullDesc[i] == ':' )
                                {
                                    fullDesc.Insert( i + 1, " " );
                                    ++i;
                                }

                            sw.Write( "<td align=\"left\">" + fullDesc.ToString() + "</td></tr>\n" );

                            ++proteinIndex;
                        }
                        ++proteinGroupId;
                        rowColor = ( rowColor == rowColor1 ? rowColor2 : rowColor1 );
                    }
            sw.Write( "\t\t\t</table></tr></td>\n\t\t</table><br />\n" );


            rowColor = rowColor1;

            sw.Write( "\t\t<script language=javascript>document.body.appendChild(makeTreeTable('" + table2id + "'))</script><br />\n" );

            if( c.proteinGroups.Count > 1 )
            {
                sw.Write( "\t\t<span class=tglcaption id=\"" + table3id + "c\" onclick=\"tglDsp('" + table3id +
                                    "'); tglCptn('" + table3id + "c')\">Hide association table</span>\n" +
                                    "\t\t<table>\n\t\t\t<tr><td><table class=t3 id=\"" + table3id + "\">\n" );

                SortedList<PeptideGroupInfo, int> peptideGroupSpectraCounts = new SortedList<PeptideGroupInfo, int>();
                int maxCount = 0;
                foreach( PeptideGroupInfo pepGroup in c.peptideGroups )
                {
                    foreach( ResultInfo r in pepGroup.results )
                    {
                        if( !peptideGroupSpectraCounts.ContainsKey( pepGroup ) )
                            peptideGroupSpectraCounts.Add( pepGroup, 0 );
                        peptideGroupSpectraCounts[pepGroup] += r.spectra.Count;
                    }
                    maxCount = Math.Max( maxCount, peptideGroupSpectraCounts[pepGroup] );
                }
                int equalWidth = (int) Math.Floor( Math.Log( (double) maxCount ) + 1.0 ) * 6;

                sw.Write( "\t\t\t\t<tr id=es1 align=\"middle\" style=\"cursor: pointer; text-decoration: underline\"><td id=es2 class=bs1 style=\"cursor: default; border-right: 2px solid black; width: 70\" />" );
                for( int i = 0; i < c.peptideGroups.Count; ++i )
                    sw.Write( "<td class=bs1 style=\"width: " + equalWidth + "px\"  onclick=\"toggleAssTableHighlightCol('" + table3id + "'," + ( i + 1 ) + ")\" title=\"" + c.peptideGroups.Keys[i].id + "\">" + ( i + 1 ) + "</td>" );
                sw.Write( "</tr>\n" );

                sw.Write( "\t\t\t\t<tr align=\"middle\"><td id=es1 class=bs1 style=\"cursor: pointer; text-decoration: underline; font-size: 12; border-right: 2px solid black\"><a onclick=\"setAssTableColors('" + table3id + "', 1)\">Sequences</a></td>" );
                foreach( PeptideGroupInfo pepGroup in c.peptideGroups )
                    sw.Write( "<td class=bs1>" + pepGroup.results.Count + "</td>" );
                sw.Write( "</tr>\n" );

                sw.Write( "\t\t\t\t<tr align=\"middle\"><td id=es1 class=bs1 style=\"cursor: pointer; letter-spacing: 2; text-decoration: underline; font-size: 12; border-right: 2px solid black; border-bottom: 2px solid black\"><a onclick=\"setAssTableColors('" + table3id + "', 2)\">Spectra</a></td>" );
                foreach( PeptideGroupInfo pepGroup in c.peptideGroups )
                    sw.Write( "<td class=bs1 style=\"border-bottom: 2px solid black\">" + peptideGroupSpectraCounts[pepGroup] + "</td>" );
                sw.Write( "</tr>\n" );

                proteinGroupId = new AlphaIndex();
                foreach( SortedList<int, ProteinGroupList> bySpectralCount in sortedProteinGroups.Values )
                    foreach( ProteinGroupList proGroupList in bySpectralCount.Values )
                        foreach( ProteinGroupInfo proGroup in proGroupList )
                        {
                            sw.Write( "\t\t\t\t<tr align=\"middle\"><td id=es1 class=bs1 style=\"cursor: pointer; text-decoration: underline; border-right: 2px solid black\" onclick=\"toggleAssTableHighlightRow('" + table3id + "'," + ( (int) proteinGroupId + 3 ) + ")\">" + proteinGroupId + "</td>" );
                            foreach( PeptideGroupInfo pepGroup in c.peptideGroups )
                            {
                                if( proGroup.peptideGroups.Contains( pepGroup ) )
                                    sw.Write( "<td class=bs1>x</td>" );
                                else
                                    sw.Write( "<td class=bs1 />" );
                            }
                            sw.Write( "</tr>\n" );
                            ++proteinGroupId;
                        }

                sw.Write( "\t\t\t</table></tr></td>\n\t\t</table><br />\n" +
                          "\t<script language=javascript>setAssTableColors( '" + table3id + "', 1 );</script>\n" );
            }

            sw.Write( "\t</body>\n</html>\n" +
                      "<script language=javascript>\n" + detailsStream.ToString() + "</script>\n" );
        }

        public static void generateCoverageStrings( Workspace ws, StreamWriter sw )
        {
            foreach( ProteinInfo pro in ws.proteins.Values )
            {
                sw.Write( pro.locus );
                foreach( VariantInfo v in pro.peptides )
                    sw.Write( "\t{0},{1}", v.peptide.proteins[pro.locus].offset + 1, v.peptide.sequence.Length );
                sw.WriteLine();
            }
        }

        public static void assembleDataProcessingDetailsHtml( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                      "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" +
                      "<script language=javascript>\n\tvar treeTables = new Array;\n\tvar dpdHeader = ['Event','Start Time','End Time','Duration'];\n\tvar dpdHeader2 = ['Parameter','Value'];\n\tvar dpdTitles = ['event type','start time of event','end time of event','duration of event'];\n\tvar dpdTitles2 = ['name of parameter','value of parameter'];\n" );

            StringBuilder scriptCallStream = new StringBuilder();
            foreach( SourceInfo source in ws.groups["/"].getSources( true ) )
            {
                scriptCallStream.Append( "\t\t\tdocument.body.appendChild(makeTreeTable('" + source.ToString() + "'));\n" );
                sw.Write( "\ttreeTables['" + source.ToString() + "'] = { caption:'" + source.ToString() + " details', header:dpdHeader, titles:dpdTitles, data:[" );
                StringBuilder paramTableStream = new StringBuilder();
                int eventCount = 1;
                foreach( ProcessingEvent evt in source.processingEvents )
                {
                    string eventName = char.ToUpper( evt.type[0] ) + evt.type.Substring( 1 );
                    if( eventCount > 1 )
                        sw.Write( "," );
                    TimeSpan dur = evt.endTime - evt.startTime;
                    dur = TimeSpan.FromSeconds( Math.Round( dur.TotalSeconds, 0 ) );
                    sw.Write( "['" + eventName + "','" + evt.startTime + "','" +
                              evt.endTime + "','" + dur +
                              "',{child:'" + source.ToString() + ".r" + eventCount + "'}]" );
                    paramTableStream.Append( "\ttreeTables['" + source.ToString() + ".r" + eventCount + "'] = { show:true, header:dpdHeader2, titles:dpdTitles2, data:[" );
                    foreach( ProcessingParam param in evt.parameters )
                    {
                        if( param != evt.parameters[0] )
                            paramTableStream.Append( "," );
                        paramTableStream.Append( "['" + char.ToUpper( param.name[0] ) + param.name.Substring( 1 ) + "','" + param.value.Replace("\\", "\\\\").Replace("'", "\\'") + "']" );
                    }
                    paramTableStream.Append( "] };\n" );
                    ++eventCount;
                }
                sw.Write( "] };\n" + paramTableStream.ToString() );
            }
            sw.Write( "</script>\n<html>\n\t<head>\n\t\t<title>" + outputPrefix + " Data Processing Details</title>\n\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n" +
                      "\t<body>\n\t\t<script language=javascript>\n" + scriptCallStream.ToString() + "\t\t</script>\n\t</body>\n</html>\n" );
        }

        public static void assembleWorkspaceGraph( Workspace ws, StreamWriter sw )
        {
            sw.Write( "graph idpicker" +
                      " {\n\tgraph\t[ranksep=2 rankdir=\"LR\"]\n" +
                            "\tnode\t[shape=rect fontname=arial]\n" );// +
            //"\tedge\t[tailclip=false tailport=\"right\" headclip=false headport=\"left\"]\n" );
            foreach( ClusterInfo c in ws.clusters )
            {
                foreach( ProteinGroupInfo proGroup in c.proteinGroups )
                {
                    sw.Write( "\t\"Cluster " + c.id + "\" -- \"Protein Group " + proGroup.id + "\"\n" );
                    //foreach( SpectrumInfo s in proGroup.spectra.Values )
                    //	sw.Write( "\t\"Protein Group " + proGroup.id + "\" -- \"" + s.id + "\"\n" );
                }

                foreach( PeptideGroupInfo pepGroup in c.peptideGroups )
                {
                    foreach( ProteinGroupInfo proGroup in pepGroup.proteinGroups )
                        sw.Write( "\t\"Protein Group " + proGroup.id + "\" -- \"Peptide Group " + pepGroup.id + "\"\n" );
                    sw.Write( "\t\"Cluster " + c.id + "\" -- \"Peptide Group " + pepGroup.id + "\"\n" );
                }

                foreach( ProteinInfo pro in c.proteins.Values )
                {
                    //foreach( SpectrumInfo s in pro.spectra.Values )
                    //	sw.Write( "\t\"Protein " + pro.locus + "\" -- \"" + s.id + "\"\n" );
                    if( pro.proteinGroup != null )
                        sw.Write( "\t\"Protein " + pro.locus + "\" -- \"Protein Group " + pro.proteinGroup.id + "\"\n" );
                    sw.Write( "\t\"Cluster " + c.id + "\" -- \"Protein " + pro.locus + "\"\n" );
                }

                foreach( ResultInfo r in c.results )
                {
                    //foreach( SpectrumInfo s in r.spectra.Values )
                    //	sw.Write( "\t\"Result " + r.ToString() + "\" -- \"" + s.id + "\"\n" );
                    foreach( VariantInfo pep in r.peptides )
                        sw.Write( "\t\"Result " + r.ToString() + "\" -- \"Peptide " + pep.peptide.sequence + "\"\n" );
                    if( r.peptideGroup != null )
                        sw.Write( "\t\"Result " + r.ToString() + "\" -- \"Peptide Group " + r.peptideGroup.id + "\"\n" );
                    sw.Write( "\t\"Cluster " + c.id + "\" -- \"Result " + r.ToString() + "\"\n" );
                }
            }

            foreach( SourceGroupInfo group in ws.groups.Values )
            {
                sw.Write( "\t\"Group " + group.name + "\" -- \"Group " + group.parent + "\"\n" );
                foreach( SourceInfo source in group.getSources() )
                {
                    sw.Write( "\t\"Group " + group.name + "\" -- \"Source " + source.name + "\"\n" );
                    //foreach( SpectrumInfo s in source.spectra.Values )
                    //	sw.Write( "\t\"Source " + source.name + "\" -- \"" + s.id + "\"\n" );
                }
            }
            sw.Write( "}\n\n" );
        }

        public static void assembleClusterGraph( Workspace ws, StreamWriter sw, int index )
        {
            ClusterInfo c = ws.clusters[index];
            int cid = index + 1;

            int maxResultLength = 0;
            foreach( ResultInfo r in c.results )
                maxResultLength = Math.Max( r.ToString().Length, maxResultLength );
            double maxNodeWidth = maxResultLength * 0.13;

            sw.Write( "graph " + cid +
                      " {\n\tgraph\t[ranksep=5 rankdir=\"LR\"]\n" +
                      "\tnode\t[shape=none fontname=arial]\n" +
                      "\tedge\t[tailclip=false tailport=\"right\" headclip=false headport=\"left\"]\n" );

            Set<string> peptideGroupSection = new Set<string>();
            foreach( ProteinGroupInfo proGroup in c.proteinGroups )
            {
                StringBuilder proteinGroupString = new StringBuilder();
                StringBuilder proteinGroupLabel = new StringBuilder();
                foreach( ProteinList.MapPair proItr in proGroup.proteins )
                {
                    if( proteinGroupString.Length > 0 )
                    {
                        proteinGroupString.Append( "\\n" );
                        proteinGroupLabel.Append( "<br/>" );
                    }
                    proteinGroupString.Append( proItr.Value.locus );
                    proteinGroupLabel.Append( proItr.Value.locus );
                }

                sw.Write( "\t\"" + proteinGroupString.ToString() +
                          "\"\t[" + ( proGroup.uniquePeptideCount > 0 ? "color=red " : "" ) +
                          "label=<<table border=\"0\" cellspacing=\"0\"><tr><td border=\"1\">" +
                          proteinGroupLabel.ToString() + "</td><td port=\"right\"></td></tr></table>>]\n" );

                Set<PeptideGroupInfo> peptideGroupSet = new Set<PeptideGroupInfo>();
                foreach( ResultInfo r in proGroup.results.Keys )
                {
                    if( !peptideGroupSet.Contains( r.peptideGroup ) )
                    {
                        peptideGroupSet.Add( r.peptideGroup );

                        StringBuilder peptideGroupString = new StringBuilder();
                        StringBuilder peptideGroupLabel = new StringBuilder();
                        foreach( ResultInfo r2 in r.peptideGroup.results )
                        {
                            if( peptideGroupString.Length > 0 )
                            {
                                peptideGroupString.Append( "\\n" );
                                peptideGroupLabel.Append( "<br/>" );
                            }
                            peptideGroupString.Append( r2.ToString() );
                            peptideGroupLabel.Append( r2.ToString() );
                        }
                        string peptideGroupEntry = "\t\"" + peptideGroupString.ToString() +
                                    "\"\t[width=\"" + maxNodeWidth + "\" label=<<table border=\"0\" cellspacing=\"0\"><tr><td port=\"left\"></td>" +
                                    "<td border=\"1\">" + peptideGroupLabel.ToString() + "</td></tr></table>>]\n";
                        peptideGroupSection.Add( peptideGroupEntry );
                        sw.Write( "\t\"" + proteinGroupString.ToString() + "\" -- \"" +
                                  peptideGroupString.ToString() + "\"\t[weight=" +
                                  r.peptideGroup.results.Count + "]\n" );
                    }
                }
            }

            foreach( string str in peptideGroupSection.Keys )
                sw.Write( str );
            sw.Write( "}\n\n" );
        }

        public static void assembleProteinSequencesTable( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                      "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" +
                                "<html>\n\t<head>\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n" +
                                "\t<body>\n\t\t<table>\n" );

            sw.Write( "<tr id=es1><td>Protein</td><td>Coverage</td><td title=\"Sequence ID\">SID</td><td title=\"Group ID\">GID</td><td title=\"Cluster ID\">CID</td>" );
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
                sw.Write( "<td>" +
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name
                                                   : groupItr.Value.name + '/' ) +
                                   "</td>" );
            sw.Write( "<td>Description</td></tr>\n" );

            Map<string, Map<string, Map<string, int>>> groupTable = new Map<string, Map<string, Map<string, int>>>();
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) )
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values )
                            foreach( VariantInfo pep in i.info.peptides )
                                foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                                    ++groupTable[groupItr.Value.name][proItr.Key][pep.peptide.sequence];
            }

            foreach( ProteinList.MapPair proItr in ws.proteins )
            {
                ProteinInfo pro = proItr.Value;

                sw.Write( String.Format(
                    "<tr><td>{0}</td><td>{1}%<td>{2}</td><td>{3}</td><td><a href=\"{4}-cluster{5}.html\">{5}</a></td>",
                    pro.locus, ( pro.Coverage * 100 ).ToString( "f0" ),
                    pro.proteinGroup.proteins.Keys.IndexOf( pro.locus ) + 1,
                    pro.proteinGroup.id, outputPrefix, pro.proteinGroup.cluster ) );
                foreach( SourceGroupList.MapPair groupItr in ws.groups )
                {
                    int count = 0;
                    if( groupTable[groupItr.Value.name].Contains( pro.locus ) )
                        count = groupTable[groupItr.Value.name][pro.locus].Count;
                    sw.Write( "<td>" + count + "</td>" );
                }
                sw.Write( "<td>" + pro.description + "</td></tr>\n" );
            }

            sw.Write( "\t\t</table>\n\t</body>\n</html>\n" );
        }

        public static void assembleProteinSpectraTable( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                      "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" +
                      "<html>\n\t<head>\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t</head>\n" +
                      "\t<body>\n\t\t<table>\n" );

            sw.Write( "<tr id=es1><td>Protein</td><td>Coverage</td><td title=\"Sequence ID\">SID</td><td title=\"Group ID\">GID</td><td title=\"Cluster ID\">CID</td>" );
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
                sw.Write( "<td>" +
                    ( (groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup()) ? groupItr.Value.name
                                                                                     : groupItr.Value.name + '/' ) +
                          "</td>" );
            sw.Write( "<td>Description</td></tr>\n" );

            Map<string, Map<string, int>> groupTable = new Map<string, Map<string, int>>();
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) )
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values )
                            foreach( VariantInfo pep in i.info.peptides )
                                foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                                    ++groupTable[groupItr.Value.name][proItr.Key];
            }

            foreach( ProteinList.MapPair proItr in ws.proteins )
            {
                ProteinInfo pro = proItr.Value;

                sw.Write( String.Format(
                    "<tr><td>{0}</td><td>{1}%</td><td>{2}</td><td>{3}</td><td><a href=\"{4}-cluster{5}.html\">{5}</a></td>",
                    pro.locus, ( pro.Coverage * 100 ).ToString( "f0" ),
                    pro.proteinGroup.proteins.Keys.IndexOf( pro.locus ) + 1,
                    pro.proteinGroup.id, outputPrefix, pro.proteinGroup.cluster ) );
                foreach( SourceGroupList.MapPair groupItr in ws.groups )
                {
                    int count = 0;
                    if( groupTable[groupItr.Value.name].Contains( pro.locus ) )
                        count = groupTable[groupItr.Value.name][pro.locus];
                    sw.Write( "<td>" + count + "</td>" );
                }
                sw.Write( "<td>" + pro.description + "</td></tr>\n" );
            }

            sw.Write( "\t\t</table>\n\t</body>\n</html>\n" );
        }

        #region Text/Delimited modification_reporting
        /// <summary>
        /// This procedure creates a detailed modification report. The report is
        /// protein centric. It lists each modification seen on a protein along
        /// with its position, peptide sequence, and the spectral counts that
        /// are split according to the sources.
        /// </summary>
        /// <param name="ws">A workspace object</param>
        /// <returns>A string of the protein centric modification table</returns>
        public static void assembleProteinModificationReport( Workspace ws, string filename, string columnDelimiter )
        {
            // this map is source, protein, location, mod, interpretations supporting that mod.
            var proteinModificationTable = new Map<string, Map<ProteinModInfo, int>>();
            // All protein modifications
            Set<ProteinModInfo> modsTable = new Set<ProteinModInfo>();            // For each source
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) )
                    foreach( SpectrumList.MapPair sItr in source.spectra ) 
                       foreach( ResultInstance i in sItr.Value.results.Values ) 
                           foreach( VariantInfo pep in i.info.peptides )                            
                            {
                                // For each variant get the sequence
                                string peptide = pep.peptide.sequence;
                                int pepID = i.info.peptideGroup.id;
                                int clusterID = i.info.peptideGroup.cluster;
                                // March through the sequence and check for any mods
                                for( int aa = 0; aa < peptide.Length; ++aa )
                                {
                                    ModMap.Enumerator posMod = pep.mods.Find( Convert.ToChar( aa + 1 ) );
                                    // If we find a mod
                                    if( posMod.IsValid )                                    {
                                        // Get the mass
                                        float mass = pep.mods.getMassAtResidue( Convert.ToChar( aa + 1 ) );
                                        // Get the proteins that match to this peptide
                                        ProteinInstanceList.Enumerator proItr = pep.peptide.proteins.GetEnumerator();
                                        while( proItr.MoveNext() )
                                        {
                                            // Get the clusterID 
                                           int protID = proItr.Current.Value.protein.proteinGroup.id;
                                            // Protein locus and the peptide offset
                                            string proteinAnn = proItr.Current.Value.protein.locus;
                                            int peptideOffset = pep.peptide.proteins[proItr.Current.Value.protein.locus].offset;
                                            int modPos = aa + peptideOffset + 1;
                                            // Create a protein modification object
                                            ProteinModInfo mod = new ProteinModInfo( clusterID, protID, pepID, proteinAnn, pep.ToInsPecTStyle(),
                                                modPos, mass, peptide[aa]+"", peptideOffset + 1, peptideOffset+peptide.Length );
                                            proteinModificationTable[groupItr.Value.name][mod]++;
                                            modsTable.Add( mod );
                                        }
                                    }
                                }
                                // Check for n-terminal and c-terminal mods
                                ModMap.Enumerator termMod = pep.mods.Find( 'n' );
                                if( termMod.IsValid )
                                {
                                    float mass = pep.mods.getMassAtResidue( 'n' );
                                    ProteinInstanceList.Enumerator proItr = pep.peptide.proteins.GetEnumerator();
                                    while( proItr.MoveNext() )
                                    {
                                        // Get the clusterID
                                        int protID = proItr.Current.Value.protein.proteinGroup.id;
                                        // Protein locus and the peptide offset
                                        string proteinAnn = proItr.Current.Value.protein.locus;
                                        int peptideOffset = pep.peptide.proteins[proItr.Current.Value.protein.locus].offset;
                                        int modPos = peptideOffset + 1;
                                        ProteinModInfo mod = new ProteinModInfo( clusterID, protID, pepID, proteinAnn, pep.ToInsPecTStyle(),
                                            modPos, mass, "nt" , peptideOffset + 1, peptideOffset + peptide.Length ); 
                                       proteinModificationTable[groupItr.Value.name][mod]++;
                                        modsTable.Add( mod );
                                    }
                                }
                                termMod = pep.mods.Find( 'c' );
                                if( termMod.IsValid )
                                {
                                    float mass = pep.mods.getMassAtResidue( 'c' );
                                    ProteinInstanceList.Enumerator proItr = pep.peptide.proteins.GetEnumerator();
                                    while( proItr.MoveNext() ) 
                                   {
                                        // Get the clusterID 
                                       int protID = proItr.Current.Value.protein.proteinGroup.id;
                                        // Protein locus and the peptide offset
                                        string proteinAnn = proItr.Current.Value.protein.locus;
                                        int peptideOffset = pep.peptide.proteins[proItr.Current.Value.protein.locus].offset;
                                        int modPos = peptideOffset + peptide.Length;
                                        ProteinModInfo mod = new ProteinModInfo( clusterID, protID, pepID, proteinAnn, pep.ToInsPecTStyle(),
                                            modPos, mass, "ct", peptideOffset + 1, peptideOffset + peptide.Length );
                                        proteinModificationTable[groupItr.Value.name][mod]++;
                                        modsTable.Add( mod );
                                    }
                                } 
                           }
            }
            // Write out the csv file with protein modification counts by group
            StreamWriter output = new StreamWriter( filename );
            output.Write( "ProteinLocus{0}ModPos{0}ModResidue{0}ModMass{0}Interpretation{0}clusterID{0}pepGroupID{0}protGroupID{0}peptideStart{0}peptideStart", columnDelimiter );
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                output.Write( "{0}{1}", columnDelimiter,
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name
                                                   : groupItr.Value.name + '/' ) );
            }
            output.WriteLine();            var proteinMods = new Map<string, List<string>>();
            foreach( ProteinModInfo protMod in modsTable ) 
           {
                output.Write( "{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}{0}{10}", 
                    columnDelimiter, protMod.proteinID, protMod.modPosition,
                    protMod.modResidue, protMod.modMass, protMod.peptideAnnotation,
                    protMod.clusterID, protMod.peptideGroupID, protMod.protGroupID, protMod.peptideStart,
                    protMod.peptideStop );
                proteinMods[protMod.proteinID].Add( protMod.modPosition + "" );
                foreach( SourceGroupList.MapPair groupItr in ws.groups )
                {
                    int count = 0;
                    if( proteinModificationTable[groupItr.Value.name].Contains( protMod ) )
                    {
                        count = proteinModificationTable[groupItr.Value.name][protMod];
                    }
                    output.Write( "{0}{1}", columnDelimiter, count );
                } 
               output.WriteLine();
            }
            output.Flush();
            output.Close();
            Presentation.exportProteinUnmodifiedCountTable( ws, filename, ',', proteinMods );
        }

        public static void exportProteinUnmodifiedCountTable( Workspace ws, string filename, char columnDelimiter, Map<string, List<string>> mods )
        {
            string tmp = filename.Replace( "protein-by-mod-by-group", "protein-by-mod-matching-unmod-by-group" );
            filename = tmp;
            Console.WriteLine( "Writing matching unmod counts: " + filename );
            var groupTable = new Map<string, Map<ProteinModInfo, int>>();
            Set<ProteinModInfo> unmodObjects = new Set<ProteinModInfo>(); 
           // For each source
            foreach( SourceGroupList.MapPair groupItr in ws.groups ) 
           {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) ) 
                   foreach( SpectrumList.MapPair sItr in source.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values ) 
                           foreach( VariantInfo pep in i.info.peptides )
                            { 
                               // For each variant get the sequence 
                               string peptide = pep.peptide.sequence; 
                               int pepID = i.info.peptideGroup.id;
                                int clusterID = i.info.peptideGroup.cluster;  
                              // March through the sequence and check for any mods
                                for( int aa = 0; aa < peptide.Length; ++aa ) 
                               {
                                    ModMap.Enumerator posMod = pep.mods.Find( Convert.ToChar( aa + 1 ) ); 
                                   // If we didn't find a mod
                                    if( !posMod.IsValid ) 
                                   {     
                                       // Get the proteins that match to this peptide 
                                       ProteinInstanceList.Enumerator proItr = pep.peptide.proteins.GetEnumerator();
                                        while( proItr.MoveNext() ) 
                                       {
                                            // Get the clusterID
                                            int protID = proItr.Current.Value.protein.proteinGroup.id;
                                            // Protein locus and the peptide offset
                                            string proteinAnn = proItr.Current.Value.protein.locus;
                                            int peptideOffset = pep.peptide.proteins[proItr.Current.Value.protein.locus].offset;
                                            int modPos = aa + peptideOffset + 1;
                                            bool isModified = false;
                                            foreach( var mod in mods[proteinAnn] ) 
                                           { 
                                               if( mod.CompareTo( modPos +"" ) == 0 ) 
                                               {  
                                                  isModified = true; 
                                                   break;       
                                                } 
                                           }   
                                         if( isModified )
                                            {
                                                // Create a protein modification object
                                                ProteinModInfo mod = new ProteinModInfo( clusterID, protID, pepID, proteinAnn, pep.ToInsPecTStyle(),
                                                    modPos, 0.0f, peptide[aa] + "", peptideOffset + 1, peptideOffset + peptide.Length );
                                                groupTable[groupItr.Value.name][mod]++;
                                                unmodObjects.Add( mod );
                                            }
                                        }
                                    }
                                }
                            }
            }
            // Write out the csv file with protein modification counts by group
            StreamWriter output = new StreamWriter( filename );
            output.Write( "ProteinLocus{0}ModPos{0}ModResidue{0}ModMass{0}Interpretation{0}clusterID{0}pepGroupID{0}protGroupID{0}peptideStart{0}peptideStart", columnDelimiter );
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                output.Write( "{0}{1}", columnDelimiter,
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name 
                                                  : groupItr.Value.name + '/' ) );
            }
            output.WriteLine();
            foreach( ProteinModInfo protMod in unmodObjects ) 
           {                
                    output.Write( "{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}{0}{10}",
                    columnDelimiter, protMod.proteinID, protMod.modPosition,
                    protMod.modResidue, protMod.modMass, protMod.peptideAnnotation,
                    protMod.clusterID, protMod.peptideGroupID, protMod.protGroupID, protMod.peptideStart,
                    protMod.peptideStop ); 
               foreach( SourceGroupList.MapPair groupItr in ws.groups ) 
               { 
                   int count = 0;
                    if( groupTable[groupItr.Value.name].Contains( protMod ) )
                    {
                        count = groupTable[groupItr.Value.name][protMod];
                    }
                    output.Write( "{0}{1}", columnDelimiter, count ); 
               } 
               output.WriteLine();
            } 
           output.Flush();
            output.Close();
        }
        public static void exportProteinModCountTable( Workspace ws, string filename, char columnDelimiter )
        {
            StreamWriter tableStream = new StreamWriter( filename );
            tableStream.Write( "Protein{0}Coverage{0}Sequence ID{0}Group ID{0}Cluster ID", columnDelimiter );
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                tableStream.Write( "{0}{1}", columnDelimiter,
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name 
                                                  : groupItr.Value.name + '/' ) + " (UU)" );
                tableStream.Write( "{0}{1}", columnDelimiter,
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name
                                                   : groupItr.Value.name + '/' ) + " (UD)" );
                tableStream.Write( "{0}{1}", columnDelimiter,
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name 
                                                  : groupItr.Value.name + '/' ) + " (MU)" ); 
               tableStream.Write( "{0}{1}", columnDelimiter,
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name 
                                                  : groupItr.Value.name + '/' ) + " (MD)" ); 
           }
            tableStream.Write( "{0}Description\n", columnDelimiter );


            var groupTable = new Map<string, Map<string, Map<string, int>>>();
            foreach( SourceGroupList.MapPair groupItr in ws.groups )            {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) )
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values )
                            foreach( VariantInfo pep in i.info.peptides )
                            {
                                string peptideKey = ""; 
                               if( pep.mods.countKnownMods( 'P', 15.9949f ) > 0 ) 
                                   peptideKey = "M";
                                else
                                    peptideKey = "U";
                                if( pep.peptide.proteins.Count > 1 )
                                    peptideKey += "D"; 
                               else 
                                   peptideKey += "U";
                                foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                                    ++groupTable[groupItr.Value.name][proItr.Key][peptideKey]; 
                           }
            }
            foreach( ProteinList.MapPair proItr in ws.proteins )
            {
                ProteinInfo pro = proItr.Value;
                tableStream.Write( "{1}{0}{2}{0}{3}{0}{4}{0}{5}", columnDelimiter, pro.locus,
                                    ( pro.Coverage * 100 ).ToString( "f0" ),
                                    ( pro.proteinGroup.proteins.Keys.IndexOf( pro.locus ) + 1 ), 
                                   pro.proteinGroup.id, pro.proteinGroup.cluster ); 
               foreach( SourceGroupList.MapPair groupItr in ws.groups )
                {
                    int UU = 0, UD = 0, MU = 0, MD = 0;

                    if( groupTable[groupItr.Value.name].Contains( pro.locus ) ) 
                   {
                        UU = groupTable[groupItr.Value.name][pro.locus]["UU"];
                        UD = groupTable[groupItr.Value.name][pro.locus]["UD"];
                        MU = groupTable[groupItr.Value.name][pro.locus]["MU"];
                        MD = groupTable[groupItr.Value.name][pro.locus]["MD"]; 
                   }
                    tableStream.Write( "{0}{1}{0}{2}{0}{3}{0}{4}", columnDelimiter, UU, UD, MU, MD );
                }
                tableStream.Write( "{0}{1}\n", columnDelimiter, pro.description );
            }
            tableStream.Flush();
            tableStream.Close();
        }
        #endregion
        public static void writeAccessReports( Workspace ws, string outputPrefix )
        {
            try
            {
                // Write the peptide and protein relationship table. This table maps each peptide
                // to the proteins that contain the peptide
                string proteinPeptideRelationshipTable = outputPrefix + "-protein-peptide-relationships.csv";
                StreamWriter peptideProteinRelationshipsStream = new StreamWriter( proteinPeptideRelationshipTable );
                // Write the header
                peptideProteinRelationshipsStream.WriteLine( "Peptide,GID,CID,Locus,Description,PeptideStart" );

                string peptideModificationsTable = outputPrefix + "-peptide-modifications-details.csv";
                StreamWriter peptideModificationsStream = new StreamWriter( peptideModificationsTable );
                peptideModificationsStream.WriteLine( "Peptide,GID,CID,ModMass,ModResidue,ModPosition" );
                // For each result
                foreach( ResultInfo result in ws.results )
                {
                    // Get the peptide sequence, groupID, and clusterID.
                    string annotatedPeptideSequence = "\"" + result.ToString() + "\"";
                    int pepID = result.peptideGroup.id;
                    int clusterID = result.peptideGroup.cluster;
                    Map<string, KeyValuePair<int, string>> uniqueProteins = new Map<string, KeyValuePair<int, string>>();

                    bool hasMods = false;
                    // For each variant in the result group.
                    foreach( VariantInfo pepItr in result.peptides )
                    {
                        // For each protein of each variant
                        foreach( ProteinInstanceList.MapPair proItr in pepItr.peptide.proteins )
                        {
                            // Get the locus, peptide offset information, and its description
                            string proteinLocus = proItr.Value.protein.locus;
                            int peptideOffset = pepItr.peptide.proteins[proteinLocus].offset + 1;
                            string proteinDesc = pepItr.peptide.proteins[proteinLocus].protein.description;
                            // Remember the protein, peptide offset, and the description
                            uniqueProteins.Add( proteinLocus, new KeyValuePair<int, string>( peptideOffset, "\"" + proteinDesc + "\"" ) );
                        }
                        if( pepItr.mods.Count > 0 )
                        {
                            hasMods = true;
                        }
                    }
                    // Write the records
                    Map<string, KeyValuePair<int, string>>.Enumerator protein = uniqueProteins.GetEnumerator();
                    while( protein.MoveNext() )
                    {
                        peptideProteinRelationshipsStream.WriteLine( annotatedPeptideSequence + "," + pepID + "," + clusterID + "," +
                                                protein.Current.Key + "," + protein.Current.Value.Value + "," + protein.Current.Value.Key );
                    }
                    if( uniqueProteins.EnumeratedCount > 0 && hasMods )
                    {
                        peptideModificationsStream.WriteLine( annotatedPeptideSequence + "," + pepID + "," + clusterID + "," +
                                            result.ModsToString( ';', ':' ) );
                    }
                    peptideProteinRelationshipsStream.Flush();
                    peptideModificationsStream.Flush();
                }
                peptideProteinRelationshipsStream.Close();
                peptideModificationsStream.Close();
            } catch( Exception ioe )
            {
                Console.Error.WriteLine( "\nError writing accessory tables:" + ioe.Message );
            }
        }

        public static void assemblePeptideSpectraTable( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            sw.Write( "<!DOCTYPE HTML PUBLIC \"-//IETF//DTD HTML//EN\">\n" +
                      "<script src=\"idpicker-scripts.js\" language=javascript></script>\n" +
                      "<html>\n\t<head>\t\t<link rel=\"stylesheet\" type=\"text/css\" href=\"idpicker-style.css\" />\n\t" +
                      "<script language=javascript>\n" +
                      ptmAnnotation +
                      "\n</script>\n" +
                      "</head>\n" +
                      "\t<body>\n\t\t<table>\n" );

            sw.Write( "<tr id=es1><td>Peptide</td><td>Specific Termini</td><td>Decoy</td><td>Mass</td><td title=\"Sequence ID\">SID</td><td title=\"Group ID\">GID</td><td title=\"Cluster ID\">CID</td>" );
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
                sw.Write( "<td>" +
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name
                                                                                       : groupItr.Value.name + '/' ) +
                          "</td>" );
            sw.Write( "</tr>\n" );

            Map<string, Map<ResultInfo, int>> groupTable = new Map<string, Map<ResultInfo, int>>();
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) )
                    foreach( SpectrumList.MapPair spectrumItr in source.spectra )
                        foreach( ResultInstance i in spectrumItr.Value.results.Values )
                        {
                            ++groupTable[groupItr.Value.name][i.info];
                        }
            }

            foreach( ResultInfo r in ws.results )
            {
                string decoyStateStr;
                switch( r.decoyState )
                {
                    case ResultInfo.DecoyState.AMBIGUOUS:
                        decoyStateStr = "Both";
                        break;
                    case ResultInfo.DecoyState.REAL:
                        decoyStateStr = "No";
                        break;
                    case ResultInfo.DecoyState.DECOY:
                        decoyStateStr = "Yes";
                        break;

                    default:
                    case ResultInfo.DecoyState.UNKNOWN:
                        throw new InvalidDataException( "unknown decoy state for result '" + r.ToString() + "'" );
                }

                string peptideAnnString = "<script language=javascript>document.write(annotatePTMsInPeptide('" +
                                          r.ToString() + "'))</script>";

                VariantInfo repVariant = r.peptides.Min;
                sw.Write( String.Format( "<tr><td>{0}</td><td>{1}</td><td>{2}</td><td>{3}</td><td>{4}</td><td><a href=\"{6}-cluster{7}.html#{5}\">{5}</a></td><td>{7}</td>",
                                        peptideAnnString.ToString(), // 0
                                        Convert.ToInt32( repVariant.peptide.NTerminusIsSpecific ) +
                                        Convert.ToInt32( repVariant.peptide.CTerminusIsSpecific ), // 1
                                        decoyStateStr, // 2
                                        r.peptides.Min.Mass, // 3
                                        ( r.peptideGroup.results.Keys.IndexOf( r ) + 1 ), // 4
                                        r.peptideGroup.id, // 5
                                        outputPrefix, // 6
                                        r.peptideGroup.cluster ) ); // 7
                foreach( SourceGroupList.MapPair groupItr in ws.groups )
                {
                    int count = 0;
                    if( groupTable[groupItr.Value.name].Contains( r ) )
                        count = groupTable[groupItr.Value.name][r];
                    sw.Write( String.Format( "<td>{0}</td>", count ) );
                }
                sw.Write( "</tr>\n" );
            }

            sw.Write( "\t\t</table>\n\t</body>\n</html>\n" );
        }
        #endregion

        #region Text/Delimited presentation
        public static void exportSummaryTable( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            exportSummaryTable( ws, sw, outputPrefix, ',' );
        }

        public static void exportSummaryTable( Workspace ws, StreamWriter sw, string outputPrefix, char columnDelimiter )
        {
            sw.Write( String.Format( "Group/Source Name{0}Confident Ids{0}Peptides{0}Peptide Groups{0}Proteins{0}Protein Groups\n", columnDelimiter ) );

            List<SourceGroupInfo> sortedGroups = new List<SourceGroupInfo>( ws.groups.Values );
            sortedGroups.Sort( SourceGroupList.SortAscendingByPathDepthThenName );

            StringBuilder sourceDetailsStream = new StringBuilder();
            int groupIndexByDepth = 0;
            foreach( SourceGroupInfo group in sortedGroups )
            {
                int groupIds = 0;
                Set<ResultInfo> groupResultSet = new Set<ResultInfo>();
                Set<VariantInfo> groupPeptideSet = new Set<VariantInfo>();
                Set<ProteinInfo> groupProteinSet = new Set<ProteinInfo>();
                Set<ProteinGroupInfo> groupProteinGroupSet = new Set<ProteinGroupInfo>();
                Set<PeptideGroupInfo> groupPeptideGroupSet = new Set<PeptideGroupInfo>();
                foreach( SourceInfo source in group.getSources( true ) )
                {
                    Set<ResultInfo> sourceResultSet = new Set<ResultInfo>();
                    Set<VariantInfo> sourcePeptideSet = new Set<VariantInfo>();
                    Set<ProteinInfo> sourceProteinSet = new Set<ProteinInfo>();
                    Set<ProteinGroupInfo> sourceProteinGroupSet = new Set<ProteinGroupInfo>();
                    Set<PeptideGroupInfo> sourcePeptideGroupSet = new Set<PeptideGroupInfo>();
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values )
                        {
                            ResultInfo r = i.info;
                            if( sourceResultSet.Insert( r ).WasInserted )
                            {
                                sourcePeptideGroupSet.Add( r.peptideGroup );
                                foreach( VariantInfo pep in r.peptides )
                                {
                                    sourcePeptideSet.Add( pep );
                                    foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                                    {
                                        sourceProteinSet.Add( proItr.Value.protein );
                                        sourceProteinGroupSet.Add( proItr.Value.protein.proteinGroup );
                                    }
                                }
                            }
                        }
                    if( source.group == group )
                        sourceDetailsStream.AppendFormat( "{1}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}\n", columnDelimiter,
                                                            group.name + ( group.isRootGroup() ? "" : "/" ),
                                                            source.name,
                                                            source.spectra.Count, sourceResultSet.Count,
                                                            sourcePeptideGroupSet.Count, sourceProteinSet.Count,
                                                            sourceProteinGroupSet.Count );

                    groupIds += source.spectra.Count;
                    groupResultSet.Union( sourceResultSet );
                    groupPeptideSet.Union( sourcePeptideSet );
                    groupProteinSet.Union( sourceProteinSet );
                    groupProteinGroupSet.Union( sourceProteinGroupSet );
                    groupPeptideGroupSet.Union( sourcePeptideGroupSet );
                }

                sw.Write( String.Format( "{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}\n", columnDelimiter,
                                         group.name, groupIds, groupResultSet.Count, groupPeptideGroupSet.Count,
                                         groupProteinSet.Count, groupProteinGroupSet.Count ) );
                ++groupIndexByDepth;
            }
            sw.Write( sourceDetailsStream.ToString() );
        }

        public static void exportProteinSequencesTable( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            exportProteinSequencesTable( ws, sw, outputPrefix, ',' );
        }

        public static void exportProteinSequencesTable( Workspace ws, StreamWriter sw, string outputPrefix, char columnDelimiter )
        {
            sw.Write( String.Format( "Protein{0}Coverage{0}Sequence ID{0}Group ID{0}Cluster ID", columnDelimiter ) );
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
                sw.Write( String.Format( "{0}{1}", columnDelimiter,
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name
                                                   : groupItr.Value.name + '/' ) ) );
            sw.Write( String.Format( "{0}Description\n", columnDelimiter ) );

            var groupTable = new Map<string, Map<string, Map<VariantInfo, int>>>();
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) )
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values )
                            foreach( VariantInfo pep in i.info.peptides )
                                foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                                    ++groupTable[groupItr.Value.name][proItr.Key][pep];
            }

            foreach( ProteinList.MapPair proItr in ws.proteins )
            {
                ProteinInfo pro = proItr.Value;

                sw.Write( String.Format( "{1}{0}{2}{0}{3}{0}{4}{0}{5}", columnDelimiter, pro.locus,
                                    ( pro.Coverage * 100 ).ToString( "f0" ),
                                    ( pro.proteinGroup.proteins.Keys.IndexOf( pro.locus ) + 1 ),
                                    pro.proteinGroup.id, pro.proteinGroup.cluster ) );
                foreach( SourceGroupList.MapPair groupItr in ws.groups )
                {
                    int count = 0;
                    if( groupTable[groupItr.Value.name].Contains( pro.locus ) )
                        count = groupTable[groupItr.Value.name][pro.locus].Count;
                    sw.Write( String.Format( "{0}{1}", columnDelimiter, count ) );
                }
                sw.Write( String.Format( "{0}{1}\n", columnDelimiter, pro.description ) );
            }
        }

        public static void exportProteinSpectraTable( Workspace ws, StreamWriter sw, string outputPrefix )
        {
            exportProteinSpectraTable( ws, sw, outputPrefix, ',' );
        }

        public static void exportProteinSpectraTable( Workspace ws, StreamWriter sw, string outputPrefix, char columnDelimiter )
        {
            sw.Write( String.Format( "Protein{0}Coverage{0}Sequence ID{0}Group ID{0}Cluster ID", columnDelimiter ) );
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
                sw.Write( String.Format( "{0}{1}", columnDelimiter,
                    ( ( groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup() ) ? groupItr.Value.name
                                                   : groupItr.Value.name + '/' ) ) );
            sw.Write( String.Format( "{0}Description\n", columnDelimiter ) );

            Map<string, Map<string, int>> groupTable = new Map<string, Map<string, int>>();
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) )
                    foreach( SpectrumList.MapPair sItr in source.spectra )
                        foreach( ResultInstance i in sItr.Value.results.Values )
                            foreach( VariantInfo pep in i.info.peptides )
                                foreach( ProteinInstanceList.MapPair proItr in pep.peptide.proteins )
                                    ++groupTable[groupItr.Value.name][proItr.Key];
            }

            foreach( ProteinList.MapPair proItr in ws.proteins )
            {
                ProteinInfo pro = proItr.Value;

                sw.Write( String.Format( "{1}{0}{2}{0}{3}{0}{4}{0}{5}", columnDelimiter, pro.locus,
                                    ( pro.Coverage * 100 ).ToString( "f0" ),
                                    ( pro.proteinGroup.proteins.Keys.IndexOf( pro.locus ) + 1 ),
                                    pro.proteinGroup.id, pro.proteinGroup.cluster ) );
                foreach( SourceGroupList.MapPair groupItr in ws.groups )
                {
                    int count = 0;
                    if( groupTable[groupItr.Value.name].Contains( pro.locus ) )
                        count = groupTable[groupItr.Value.name][pro.locus];
                    sw.Write( String.Format( "{0}{1}", columnDelimiter, count ) );
                }
                sw.Write( String.Format( "{0}{1}\n", columnDelimiter, pro.description ) );
            }
        }

        public static void exportPeptideSpectraTable (Workspace ws, StreamWriter sw, string outputPrefix, QuantitationInfo.Method quantitationMethod)
        {
            exportPeptideSpectraTable( ws, sw, outputPrefix, quantitationMethod, ',' );
        }



        public static void exportPeptideSpectraTable (Workspace ws, StreamWriter sw, string outputPrefix, QuantitationInfo.Method quantitationMethod, char columnDelimiter)
        {
            if( ws.residueMaps == null )
            {
                ws.residueMaps = new ResidueMaps();
            }

            sw.Write( String.Format( "Peptide{0}Specific Termini{0}Decoy{0}Mass{0}Sequence ID{0}Group ID{0}Cluster ID", columnDelimiter ) );
            foreach (SourceGroupList.MapPair groupItr in ws.groups)
            {
                string groupName = (groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup()) ?
                                    groupItr.Value.name : groupItr.Value.name + '/';
                sw.Write(String.Format("{0}{1}", columnDelimiter, groupName));

                string quantitationColumns = String.Empty;
                if (quantitationMethod == QuantitationInfo.Method.ITRAQ4Plex)
                    quantitationColumns = String.Format("{0}{1}(ITRAQ-114){0}{1}(ITRAQ-115){0}{1}(ITRAQ-116){0}{1}(ITRAQ-117)", columnDelimiter, groupName);
                else if (quantitationMethod == QuantitationInfo.Method.ITRAQ8Plex)
                    quantitationColumns = String.Format("{0}{1}(ITRAQ-113){0}{1}(ITRAQ-114){0}{1}(ITRAQ-115){0}{1}(ITRAQ-116){0}{1}(ITRAQ-117){0}{1}(ITRAQ-118){0}{1}(ITRAQ-119){0}{1}(ITRAQ-121)", columnDelimiter, groupName);
                sw.Write(quantitationColumns);
            }

            #region Surendra's extra annotation
            if ( ws.modificationAnnotations != null && ws.modificationAnnotations.Count > 0 )
            {
                sw.Write( String.Format( "{0}{1}", columnDelimiter, "Modification Annotation" ) );
                sw.Write( String.Format( "{0}Protein Locus{0}Peptide Start{0}PeptideStop{0}Alternative Loci", columnDelimiter ) );
            }

            if( ws.snpAnntoations != null && ws.snpAnntoations.CollectedMetaData.Count > 0 )
                sw.Write( String.Format( "{0}SNP Annotation", columnDelimiter ) );
            if( ws.knownModMasses != null && ws.knownModMasses.Length > 0 )
            {
                sw.Write( String.Format( "{0}Unknown Mods", columnDelimiter ) );
                sw.Write( String.Format( "{0}Best Scores{0}Best Unmodified Peptide", columnDelimiter ) );
            }
            #endregion

            sw.WriteLine();

            var groupTable = new Map<string, Map<ResultInfo, MultiSourceQuantitationInfo>>();
            foreach( SourceGroupList.MapPair groupItr in ws.groups )
            {
                foreach( SourceInfo source in groupItr.Value.getSources( true ) )
                    foreach( SpectrumList.MapPair spectrumItr in source.spectra )
                        foreach( ResultInstance i in spectrumItr.Value.results.Values )
                        {
                            var quantitationInfo = groupTable[groupItr.Value.name][i.info];
                            ++quantitationInfo.spectralCount;

                            if (spectrumItr.Value.quantitation == null)
                                continue;

                            quantitationInfo.method = spectrumItr.Value.quantitation.method;
                            quantitationInfo.ITRAQ_113_intensity += spectrumItr.Value.quantitation.ITRAQ_113_intensity;
                            quantitationInfo.ITRAQ_114_intensity += spectrumItr.Value.quantitation.ITRAQ_114_intensity;
                            quantitationInfo.ITRAQ_115_intensity += spectrumItr.Value.quantitation.ITRAQ_115_intensity;
                            quantitationInfo.ITRAQ_116_intensity += spectrumItr.Value.quantitation.ITRAQ_116_intensity;
                            quantitationInfo.ITRAQ_117_intensity += spectrumItr.Value.quantitation.ITRAQ_117_intensity;
                            quantitationInfo.ITRAQ_118_intensity += spectrumItr.Value.quantitation.ITRAQ_118_intensity;
                            quantitationInfo.ITRAQ_119_intensity += spectrumItr.Value.quantitation.ITRAQ_119_intensity;
                            quantitationInfo.ITRAQ_121_intensity += spectrumItr.Value.quantitation.ITRAQ_121_intensity;
                        }
            }

            foreach( ResultInfo r in ws.results )
            {
                string decoyStateStr;
                switch( r.decoyState )
                {
                    case ResultInfo.DecoyState.AMBIGUOUS:
                        decoyStateStr = "Both";
                        break;
                    case ResultInfo.DecoyState.REAL:
                        decoyStateStr = "No";
                        break;
                    case ResultInfo.DecoyState.DECOY:
                        decoyStateStr = "Yes";
                        break;

                    default:
                    case ResultInfo.DecoyState.UNKNOWN:
                        throw new InvalidDataException( "unknown decoy state for result '" + r.ToString() + "'" );
                }

                VariantInfo repVariant = r.peptides.Min;
                sw.Write( String.Format( "{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}", columnDelimiter,
                                        r.ToString(),
                                        Convert.ToInt32( repVariant.peptide.NTerminusIsSpecific ) +
                                        Convert.ToInt32( repVariant.peptide.CTerminusIsSpecific ),
                                        decoyStateStr, repVariant.Mass,
                                        ( r.peptideGroup.results.Keys.IndexOf( r ) + 1 ),
                                        r.peptideGroup.id, r.peptideGroup.cluster ) );

                foreach( SourceGroupList.MapPair groupItr in ws.groups )
                {
                    var quantitationInfo = new MultiSourceQuantitationInfo
                        { spectralCount = 0, method = quantitationMethod };

                    if( groupTable[groupItr.Value.name].Contains( r ) )
                        quantitationInfo = groupTable[groupItr.Value.name][r];

                    sw.Write( String.Format( "{0}{1}{0}", columnDelimiter, quantitationInfo.spectralCount ) );
                    sw.Write( quantitationInfo.ToDelimitedString(columnDelimiter) );
                }

                #region Surendra's extra annotation

                // If we have unimod annotations, then get them into the export, along with 
                // the protein loci that are matched to the peptide.
                if( ws.modificationAnnotations != null && ws.modificationAnnotations.Count > 0 )
                {
                    sw.Write( String.Format( "{0}{1}", columnDelimiter, r.ModAnnotations() ) );
                    sw.Write( String.Format( "{0}{1}", columnDelimiter, r.getProteinLoci() ) );
                }
                
                //sw.Write(String.Format("{0}{1}", columnDelimiter, repVariant.ToInsPecTStyle()));

                // If we have known SNP annotations from IPI or Swiss-Prot, collect them for
                // any matching SNPs present in the current result
                string snpAnn = "";
                if( ws.snpAnntoations != null )
                {
                    snpAnn += r.getSNPMetaData( ws ) + " ";
                }
                // Collect the known SNP annotations from CanProVar database, if we have them
                if( ws.snpAnntoations != null && ws.snpAnntoations.CollectedMetaData.Count > 0 )
                {
                    snpAnn += r.getSNPMetaDataFromProCanVar( ws );
                }
                // Add the known SNP annotations
                if( ws.snpAnntoations != null && ws.snpAnntoations.CollectedMetaData.Count > 0 )
                {
                    sw.Write( String.Format( "{0}{1}", columnDelimiter, snpAnn ) );
                }
                // If the user wants us to flag any peptides containing unknown mods....
                if( ws.knownModMasses != null && ws.knownModMasses.Length > 0 )
                {
                    // Get the known mods provided by the user
                    string unknownMods = r.extractUnknownMods( ws.knownModResidues, ws.knownModMasses );
                    if( unknownMods == null || unknownMods.Length == 0 )
                        sw.Write( String.Format( "{0}{1}", columnDelimiter, "" ) );
                    else
                        sw.Write( String.Format( "{0}{1}", columnDelimiter, unknownMods ) );
                    // Check to see of the the user also has any previous results to merge with
                    // current results
                    if( ws.spectraExport != null && unknownMods != null && unknownMods.Length > 0 )
                    {
                        // Get the best scores for the set of spectra matching to this peptide
                        List<string> spectrumNames = new List<string>();
                        float[] bestScores = new float[ws.spectraExport.scoreNames.Length];
                        for( int scoreInd = 0; scoreInd < bestScores.Length; ++scoreInd )
                            bestScores[scoreInd] = -1.0f;
                        // for each spectra, find the scores, and remember the best scores for the
                        // set
                        foreach( SpectrumList.MapPair itr in r.spectra )
                        {
                            string spectrumName = itr.Value.results.Values[0].spectrum.id.source.name;
                            spectrumName = spectrumName + "." + itr.Value.results.Values[0].spectrum.nativeID;
                            spectrumName = spectrumName + "." + itr.Value.results.Values[0].spectrum.id.charge;
                            spectrumNames.Add( spectrumName );
                            //Console.WriteLine( spectrumName );
                            float[] searchScores = new float[ws.spectraExport.scoreNames.Length];
                            for( int index = 0; index < searchScores.Length; ++index )
                            {
                                if( itr.Value.results.Values[0].searchScores.ContainsKey( ws.spectraExport.scoreNames[index] ) )
                                    searchScores[index] = itr.Value.results.Values[0].searchScores[ws.spectraExport.scoreNames[index]];
                            }
                            int bitCompare = 0;
                            for( int index = 0; index < searchScores.Length; ++index )
                                if( searchScores[index] > bestScores[index] )
                                {
                                    ++bitCompare;
                                }
                            if( bitCompare == searchScores.Length )
                            {
                                searchScores.CopyTo( bestScores, 0 );
                            }
                        }
                        StringBuilder bestScoresStr = new StringBuilder();
                        for( int index = 0; index < bestScores.Length; ++index )
                        {
                            if( index > 0 )
                                bestScoresStr.Append( " " );
                            bestScoresStr.Append( "(" + ws.spectraExport.scoreNames[index] + " " + bestScores[index] + ")" );
                        }
                        // Get the best alternative interpretation (from a previous search) that matched to the
                        // same set of spectra with higher scores than the current interpretation
                        string bestAlternative = ws.spectraExport.getBestInterpretation( spectrumNames, bestScores );
                        if( bestAlternative != null && bestAlternative.Length > 0 )
                            sw.Write( String.Format( "{0}{1}{0}{2}", columnDelimiter, bestScoresStr.ToString(), bestAlternative ) );
                    }
                }
                #endregion

                sw.WriteLine();
            }
        }

        public static void exportPeptideGroupSpectraTable (Workspace ws, StreamWriter sw, string outputPrefix, QuantitationInfo.Method quantitationMethod, char columnDelimiter)
        {
            sw.Write(String.Format("Peptide Group ID{0}Cluster ID", columnDelimiter));
            foreach (SourceGroupList.MapPair groupItr in ws.groups)
            {
                string groupName = (groupItr.Value.isLeafGroup() || groupItr.Value.isRootGroup()) ?
                                    groupItr.Value.name : groupItr.Value.name + '/';
                sw.Write(String.Format("{0}{1}", columnDelimiter, groupName));

                string quantitationColumns = String.Empty;
                if (quantitationMethod == QuantitationInfo.Method.ITRAQ4Plex)
                    quantitationColumns = String.Format("{0}{1}(ITRAQ-114){0}{1}(ITRAQ-115){0}{1}(ITRAQ-116){0}{1}(ITRAQ-117)", columnDelimiter, groupName);
                else if (quantitationMethod == QuantitationInfo.Method.ITRAQ8Plex)
                    quantitationColumns = String.Format("{0}{1}(ITRAQ-113){0}{1}(ITRAQ-114){0}{1}(ITRAQ-115){0}{1}(ITRAQ-116){0}{1}(ITRAQ-117){0}{1}(ITRAQ-118){0}{1}(ITRAQ-119){0}{1}(ITRAQ-121)", columnDelimiter, groupName);
                sw.Write(quantitationColumns);
            }

            sw.WriteLine();

            foreach (PeptideGroupInfo peptideGroup in ws.peptideGroups)
            {
                var pepGroupRow = new Map<SourceGroupInfo, MultiSourceQuantitationInfo>();
                foreach (ResultInfo resultItr in peptideGroup.results)
                    foreach (SpectrumList.MapPair spectrumItr in resultItr.spectra)
                    {
                        SourceGroupInfo sourceGroup = spectrumItr.Value.id.source.group;
                        while (true)
                        {
                            var quantitationInfo = pepGroupRow[sourceGroup];
                            ++quantitationInfo.spectralCount;

                            if (spectrumItr.Value.quantitation != null)
                            {
                                quantitationInfo.method = spectrumItr.Value.quantitation.method;
                                quantitationInfo.ITRAQ_113_intensity += spectrumItr.Value.quantitation.ITRAQ_113_intensity;
                                quantitationInfo.ITRAQ_114_intensity += spectrumItr.Value.quantitation.ITRAQ_114_intensity;
                                quantitationInfo.ITRAQ_115_intensity += spectrumItr.Value.quantitation.ITRAQ_115_intensity;
                                quantitationInfo.ITRAQ_116_intensity += spectrumItr.Value.quantitation.ITRAQ_116_intensity;
                                quantitationInfo.ITRAQ_117_intensity += spectrumItr.Value.quantitation.ITRAQ_117_intensity;
                                quantitationInfo.ITRAQ_118_intensity += spectrumItr.Value.quantitation.ITRAQ_118_intensity;
                                quantitationInfo.ITRAQ_119_intensity += spectrumItr.Value.quantitation.ITRAQ_119_intensity;
                                quantitationInfo.ITRAQ_121_intensity += spectrumItr.Value.quantitation.ITRAQ_121_intensity;
                            }

                            if (sourceGroup.isRootGroup())
                                break;
                            sourceGroup = sourceGroup.parent;
                        }
                    }

                sw.Write(String.Format("{1}{0}{2}{0}", columnDelimiter,
                                        peptideGroup.id, peptideGroup.cluster));

                foreach (SourceGroupList.MapPair groupItr in ws.groups)
                {
                    var quantitationInfo = new MultiSourceQuantitationInfo { spectralCount = 0, method = quantitationMethod };

                    if (pepGroupRow.Contains(groupItr.Value))
                        quantitationInfo = pepGroupRow[groupItr.Value];

                    sw.Write(String.Format("{1}{0}", columnDelimiter, quantitationInfo.spectralCount));
                    sw.Write(quantitationInfo.ToDelimitedString(columnDelimiter));
                }

                sw.WriteLine();
            }
        }

        public static void exportSpectraTable( Workspace ws, StreamWriter sw, string outputPrefix, QuantitationInfo.Method quantitationMethod )
        {
            exportSpectraTable( ws, sw, outputPrefix, quantitationMethod, ',' );
        }

        public static void exportSpectraTable (Workspace ws, StreamWriter sw, string outputPrefix, QuantitationInfo.Method quantitationMethod, char columnDelimiter)
        {
            string quantitationColumns = String.Empty;
            if (quantitationMethod == QuantitationInfo.Method.ITRAQ4Plex)
                quantitationColumns = String.Format("ITRAQ-114{0}ITRAQ-115{0}ITRAQ-116{0}ITRAQ-117", columnDelimiter);
            else if (quantitationMethod == QuantitationInfo.Method.ITRAQ8Plex)
                quantitationColumns = String.Format("ITRAQ-113{0}ITRAQ-114{0}ITRAQ-115{0}ITRAQ-116{0}ITRAQ-117{0}ITRAQ-118{0}ITRAQ-119{0}ITRAQ-121", columnDelimiter);

            sw.Write( String.Format( "Source{0}ID{0}Index{0}Charge{0}Cluster ID{0}FDR{0}Precursor mass{0}Calculated mass{0}Mass error{0}Peptide{0}Mods{0}{1}\n", columnDelimiter, quantitationColumns ) );
            foreach( SourceGroupInfo group in ws.groups.Values )
                foreach( SourceInfo source in group.getSources() )
                    foreach( SpectrumInfo spectrum in source.spectra.Values )
                    {
                        if (spectrum.quantitation != null)
                            quantitationColumns = spectrum.quantitation.ToDelimitedString(columnDelimiter);
                        else
                            quantitationColumns = String.Empty;

                        ResultInstance ri = spectrum.results[1];
                        VariantInfo vi = ri.info.peptides.Min;
                        List<ModMap> modMaps = ri.mods[vi.peptide];
                        float fullModMass = modMaps.Count > 0 ? modMaps[0].Mass : 0;
                        sw.Write( String.Format( "{1}{0}{2}{0}{3}{0}{4}{0}{5}{0}{6}{0}{7}{0}{8}{0}{9}{0}{10}{0}{11}{0}{12}\n", columnDelimiter,
                            group.name + ( group.isRootGroup() ? "" : "/" ) + source.name,
                            spectrum.nativeID, spectrum.id.index, spectrum.id.charge,
                            ri.info.peptideGroup.cluster, ri.FDR,
                            spectrum.precursorMass, vi.peptide.mass + fullModMass,
                            spectrum.precursorMass - ( vi.peptide.mass + fullModMass ),
                            vi.peptide.sequence, ri.mods.ToString( vi.peptide ),
                            quantitationColumns ) );
                    }
        }

        public static void exportProteinGroupToPeptideGroupTable( Workspace ws, StreamWriter sw )
        {
            exportProteinGroupToPeptideGroupTable( ws, sw, ',' );
        }

        public static void exportProteinGroupToPeptideGroupTable( Workspace ws, StreamWriter sw, char columnDelimiter )
        {
            sw.Write( String.Format( "Protein Group ID{0}Peptide Group ID\n", columnDelimiter ) );
            foreach( ProteinGroupInfo proGroup in ws.proteinGroups )
                foreach( PeptideGroupInfo pepGroup in proGroup.peptideGroups )
                    sw.Write( String.Format( "{1}{0}{2}\n", columnDelimiter, proGroup.id, pepGroup.id ) );
        }
        #endregion
    }
}