function tglDsp(e)
{
	var sElement = document.getElementById(e);
	if( sElement.rows.length > 0 )
	{
		if( sElement.style.display == 'none' )
			sElement.style.display = '';
		else
			sElement.style.display = 'none';
	}
}

function setCptn(b,show)
{
	var aButton = document.getElementById(b);

	var oldValue = aButton.innerHTML;
	var newValue;
	if( show )
		newValue = oldValue.replace( /Show/, "Hide" );
	else
		newValue = oldValue.replace( /Hide/, "Show" );
	aButton.innerHTML = newValue;
}

function tglCptn(b)
{
	var aButton = document.getElementById(b);

	var oldValue = aButton.innerHTML;
	var newValue = oldValue.replace( /Show/, "Hide" );
	if( oldValue == newValue )
		newValue = oldValue.replace( /Hide/, "Show" );
	aButton.innerHTML = newValue;
}

function tglTreeAnchor(b)
{
	var anAnchor = document.getElementById(b);

	if( anAnchor.innerHTML == "+" )
		anAnchor.innerHTML = "-";
	else
		anAnchor.innerHTML = "+";
}

var sortIndices;
function sortByIndices(a,b)
{
	for( var i=0, l=sortIndices.length; i < l; ++i )
	{
		var sortIndex = Math.abs( sortIndices[i] )-1;
		var aValue = a[sortIndex];
		var bValue = b[sortIndex];
		if( aValue == bValue )
			continue;
		if( sortIndices[i] >= 0 )
		{
			if( aValue < bValue )
				return -1;
			else
				return 1;
		} else
		{
			if( aValue > bValue )
				return -1;
			else
				return 1;
		}
	}
	return 0;
}

function addPeptideGroupsTableDataRows(tableId, tableData, numColumns)
{
	if( tableData.curSortIndexes && tableData.defaultSortIndexes && tableData.curSortIndexes.join() != tableData.defaultSortIndexes.join() ) {
		 return addTreeTableDataRows(tableId, tableData, numColumns);
	}

	var tableHtml = [];
	var tableDataRows = tableData.data;
	var tableMetadata = tableData.metadata;
	var lastGID;
	var lastRowClass;
	for( var r=0, maxr=tableDataRows.length; r < maxr; ++r )
	{
		var rowId = tableId + 'r' + r;
		var rowData = tableDataRows[r];
		var rowLength = tableDataRows[r].length;
		var rowClass;
		var childTableId;
		var childTableData;

		if( r > 0 )
			if( tableDataRows[r][2] != tableDataRows[r-1][2] )
				if( lastRowClass == "h2" )
					rowClass = "h3";
				else
					rowClass = "h2";
			else {
				rowClass = lastRowClass;
			}
		else
			rowClass = "h2";

		tableHtml.push('<tr id=' + rowId + ' class=' + rowClass + '>');

		var hasChild = typeof(rowData[rowLength-1]) == "object";
		if(hasChild) {
			childTableId = rowData[rowLength-1].child;
			childTableData = treeTables[childTableId];
			tableHtml.push('<td id=es2>');
			tableHtml.push("<a class=\"tb\" id=\"" + rowId + "b\" onclick=\"tglTreeTable('" + tableId + "'," + r + "); tglTreeAnchor('" + rowId + "b')\">" + ( childTableData && childTableData.show ? '-' : '+' ) + "</a>");
			tableHtml.push('</td>');
		} else
			tableHtml.push('<td id=es2 />');

		for( var c=0; c < rowLength; ++c )
		{
			var colValue = rowData[c];
			if( c == rowLength-1 )
				if( typeof(colValue) == "object" )
					break;
			if( r > 0 && c == 2 && tableDataRows[r][2] == tableDataRows[r-1][2] )
				colValue = "";
			else if( tableMetadata != null ) {
				if( tableMetadata[c] == 1 )
					continue;
				else if( tableMetadata[c] == 2 )
					colValue = stringIndex[colValue];
			}
			var colStyle = "text-align:right";
			if( typeof(colValue) == "string" )
				colStyle = "text-align:left";
			tableHtml.push('<td style="' + colStyle + '">' + colValue + '</td>');
		}
		tableHtml.push('</tr>');

		if( hasChild && childTableData && childTableData.show )
		{
			var childTableRowId = tableId + "_/_" + childTableId + 'r';
			var childTableContainer = makeTreeTable(tableId + "_/_" + childTableId);
			tableHtml.push('<tr id="' + childTableRowId + '"><td /><td colspan="' + numColumns + '">' + childTableContainer.innerHTML + '</td></tr>');
		}

		lastGID = tableDataRows[r][2];
		lastRowClass = rowClass;
	}
	return tableHtml.join("");
}

function makeTreeTable(tableId, curContainer)
{
	var branch = tableId.split("_/_");
	var leaf = branch[branch.length-1];
	var t = treeTables[leaf];

	var tableFontSize = "12pt";
	if( branch.length > 1 )
		tableFontSize = "10pt";

	var tableHtml = [];
	var containerElement = curContainer;
	if( containerElement == null ) {
		containerElement = document.createElement('span')
		containerElement.id = tableId + '_container';
	}

	if( t.caption != null && t.caption.length > 0 )
	{
		var defaultShowStateString = ( t.show ? "Hide " : "Show " );
		if( t.ready == null )
			t.ready = ( t.show ? true : false );
		tableHtml.push("<span class=\"tglcaption\"><a id=\"" + tableId + "c\" onclick=\"tglShowTreeTable('" + tableId + "'); tglCptn('" + tableId + "c')\">" + defaultShowStateString + t.caption + "</a></span><br />");
	} else
		t.ready = true;

	if( !t.ready ) {
		containerElement.innerHTML = tableHtml.join("");
		return containerElement;
	}

	tableHtml.push('<table id="' + tableId + '" style="font-size:' + tableFontSize + '">');
	
	var numColumns;
	if( typeof(t.header) == "number" ) {
		numColumns = t.header;
		tableHtml.push('<tr />');
	} else if( typeof(t.header) == "object" ) {
		numColumns = t.header.length;
		tableHtml.push('<tr><td id=es2 />');
		if( t.sortable ) {
			if( t.headerSortIndexes == null ) {
				t.headerSortIndexes = new Array();
				for( var c=0; c < numColumns; ++c )
					t.headerSortIndexes[c] = [c+1];
			}
			if( t.defaultSortIndexes == null )
				t.defaultSortIndexes = t.headerSortIndexes[0];
			if( t.curSortIndexes == null )
				t.curSortIndexes = t.defaultSortIndexes;
			sortIndices = t.curSortIndexes;
			t.data.sort( sortByIndices );
			if( t.titles != null && t.titles.length == t.header.length )
				for( var c=0; c < numColumns; ++c )
					tableHtml.push('<td id=es1><a class="txtBtn" title="Sort by ' + t.titles[c] + '" onclick="sortTreeTable(\'' + tableId + '\',[' + t.headerSortIndexes[c].join() + '])">' + t.header[c] + '</a></td>');
			else
				for( var c=0; c < numColumns; ++c )
					tableHtml.push('<td id=es1><a class="txtBtn" onclick="sortTreeTable(\'' + tableId + '\',[' + t.headerSortIndexes[c].join() + '])">' + t.header[c] + '</a></td>');
		} else
			if( t.titles != null && t.titles.length == t.header.length )
				for( var c=0; c < numColumns; ++c )
					tableHtml.push('<td id=es1 title="' + t.titles[c].substring(0,1).toUpperCase() + t.titles[c].substring(1) + '">' + t.header[c] + '</td>');
			else
				for( var c=0; c < numColumns; ++c )
					tableHtml.push('<td id=es1>' + t.header[c] + '</td>');
		tableHtml.push('</tr>');
	} else
		alert( "Bad table header in '" + tableId + "'" );

	if( typeof(t.addDataRowsFunction) == "function" )
		tableHtml.push(t.addDataRowsFunction(tableId, t, numColumns));
	else
		tableHtml.push(addTreeTableDataRows(tableId, t, numColumns));

	tableHtml.push('</table>');
	containerElement.innerHTML = tableHtml.join("");
	return containerElement;
}

function tglShowTreeTable(tableId)
{
	var branch = tableId.split("_/_");
	var leaf = branch[branch.length-1];
	var t = treeTables[leaf];
	if( !t.ready ) {
		t.ready = true;
		makeTreeTable(tableId, document.getElementById(tableId + '_container'));
	} else
		tglDsp(tableId);
}

function addTreeTableDataRows(tableId, tableData, numColumns)
{
	var tableHtml = [];
	var tableDataRows = tableData.data;
	var tableMetadata = tableData.metadata;

	for( var r=0, maxr=tableDataRows.length; r < maxr; ++r )
	{
		var rowId = tableId + 'r' + r;
		var rowData = tableDataRows[r];
		var rowLength = tableDataRows[r].length;
		var childTableId;
		var childTableData;

		tableHtml.push('<tr id=' + rowId + '>');

		var hasChild = typeof(rowData[rowLength-1]) == "object";
		if(hasChild) {
			childTableId = rowData[rowLength-1].child;
			childTableData = treeTables[childTableId];
			tableHtml.push('<td id=es2>');
			tableHtml.push("<a class=\"tb\" id=\"" + rowId + "b\" onclick=\"tglTreeTable('" + tableId + "'," + r + "); tglTreeAnchor('" + rowId + "b')\">" + ( childTableData && childTableData.show ? '-' : '+' ) + "</a>");
			tableHtml.push('</td>');
		} else
			tableHtml.push('<td id=es2 />');

		for( var c=0; c < rowLength; ++c )
		{
			var colValue = rowData[c];
			if( c == rowLength-1 && typeof(colValue) == "object" )
				break;
			if( tableMetadata != null ) {
				if( tableMetadata[c] == 1 )
					continue;
				else if( tableMetadata[c] == 2 )
					colValue = stringIndex[colValue];
			}
			var colStyle = "text-align:right";
			if( typeof(colValue) == "string" )
				colStyle = "text-align:left";
			tableHtml.push('<td style="' + colStyle + '">' + colValue + '</td>');
		}
		tableHtml.push('</tr>');

		if( hasChild && childTableData && childTableData.show )
		{
			var childTableRowId = tableId + "_/_" + childTableId + 'r';
			var childTableContainer = makeTreeTable(tableId + "_/_" + childTableId);
			tableHtml.push('<tr id="' + childTableRowId + '"><td /><td colspan="' + numColumns + '">' + childTableContainer.innerHTML + '</td></tr>');
		}
	}
	return tableHtml.join("");
}

function sortTreeTable(tableId, sortIndexes)
{
	var branch = tableId.split("_/_");
	var leaf = branch[branch.length-1];
	var t = treeTables[leaf];

	var tableFontSize = "12pt";
	if( branch.length > 1 )
		tableFontSize = "10pt";

	if( sortIndexes.join() == t.curSortIndexes.join() )
		for( var i=0; i < sortIndexes.length; ++i )
			sortIndexes[i] *= -1;
	t.curSortIndexes = sortIndexes;

	makeTreeTable(tableId, document.getElementById(tableId + '_container'));
}

function tglTreeTable(parentTableId, parentRowDataIndex)
{
	var branch = parentTableId.split("_/_");
	var leaf = branch[branch.length-1];
	var parentTableData = treeTables[leaf];
	var parentTable = document.getElementById(parentTableId);
	var parentRowData = parentTableData['data'][parentRowDataIndex];
	var parentRowId = parentTableId + 'r' + parentRowDataIndex;
	var parentRowTableIndex = document.getElementById(parentRowId).rowIndex;
	var childTableId = parentTable.id + "_/_" + parentRowData[parentRowData.length-1]['child'];
	var childTableRowId = childTableId + 'r';

	if( parentTable.rows[parentRowTableIndex+1] != null && parentTable.rows[parentRowTableIndex+1].id == childTableRowId )
	{
		parentTable.deleteRow(parentRowTableIndex+1);
		return;
	}

	var childTableRow = parentTable.insertRow(parentRowTableIndex+1);
	childTableRow.id = childTableRowId;
	var tmp = childTableRow.insertCell(0);

	var childTableCell = childTableRow.insertCell(1);
	childTableCell.colSpan = parentTable.rows[parentRowTableIndex].cells.length-1;
	childTableCell.appendChild(makeTreeTable(childTableId));
}

var curHighlightCols = new Array();
function toggleAssTableHighlightCol(t,c)
{
	var tableElement = document.getElementById(t);
	var targetCells = cellsToColor[t];

	var bgcolor;
	var fgcolor;
	var didToggle = false;

	while( curHighlightRows.length > 0 )
		toggleAssTableHighlightRow(t,curHighlightRows[0]);

	for( var r2=0; r2 < curHighlightCols.length; ++r2 )
	{
		var curHighlightCol = curHighlightCols[r2];
		if( curHighlightCol == c )
		{
		    fgcolor = "";
		    bgcolor = "";
		    for( var j=0; j < targetCells[curHighlightCol-1][1].length; ++j )
		    {
			    var rowToColor = targetCells[curHighlightCol-1][1][j];
			    for( var i=0; i < tableElement.rows[rowToColor].cells.length; ++i )
			    {
				    tableElement.rows[rowToColor].cells[i].style.backgroundColor = bgcolor;
				    tableElement.rows[rowToColor].cells[i].style.color = fgcolor;
			    }
		    }

		    for( var i=0; i < tableElement.rows.length; ++i )
		    {
			    tableElement.rows[i].cells[curHighlightCol].style.backgroundColor = bgcolor;
			    tableElement.rows[i].cells[curHighlightCol].style.color = fgcolor;
		    }
	        curHighlightCols.splice(r2,1);
		    didToggle = true;
		    break;
	    }
	}

    if( !didToggle )
	    curHighlightCols.push(c);
	
	fgcolor = "rgb(255,255,255)";
	
	for( var r2=0; r2 < curHighlightCols.length; ++r2 )
	{
		var curHighlightCol = curHighlightCols[r2];
		for( var j=0; j < targetCells[curHighlightCol-1][1].length; ++j )
		{
			var rowToColor = targetCells[curHighlightCol-1][1][j];
			for( var i=0; i < tableElement.rows[rowToColor].cells.length; ++i )
			{
			    bgcolor = "rgb(150,150,150)";
		        if( tableElement.rows[rowToColor].cells[i].innerHTML == "x" )
		        {
		            var redValue = 255 - Math.min( Math.pow( tableElement.rows[curColorRow].cells[i].innerHTML, 2 ), 255 );
			        var colColorRGB = [255,redValue,redValue];
			        bgcolor = "rgb("+(150-redValue)+","+(150-redValue)+","+(150-redValue)+")";
                }
				tableElement.rows[rowToColor].cells[i].style.backgroundColor = bgcolor;
				tableElement.rows[rowToColor].cells[i].style.color = fgcolor;
			}
		}
	}

	for( var r2=0; r2 < curHighlightCols.length; ++r2 )
	{
		var curHighlightCol = curHighlightCols[r2];
		for( var i=0; i < tableElement.rows.length; ++i )
		{
			bgcolor = "rgb(100,100,100)";
		    if( tableElement.rows[i].cells[curHighlightCol].innerHTML == "x" )
		    {
		        var redValue = 255 - Math.min( Math.pow( tableElement.rows[curColorRow].cells[curHighlightCol].innerHTML, 2 ), 255 );
			    var colColorRGB = [255,redValue,redValue];
			    bgcolor = "rgb("+(100-redValue)+","+(100-redValue)+","+(100-redValue)+")";
            }
			tableElement.rows[i].cells[curHighlightCol].style.backgroundColor = bgcolor;
			tableElement.rows[i].cells[curHighlightCol].style.color = fgcolor;
		}
	}
	setAssTableColors(t,curColorRow);
}

var curHighlightRows = new Array();
function toggleAssTableHighlightRow(t,r)
{
	var tableElement = document.getElementById(t);
	var targetCells = cellsToColor[t];

	var bgcolor;
	var fgcolor;
	var didToggle = false;

	while( curHighlightCols.length > 0 )
		toggleAssTableHighlightCol(t,curHighlightCols[0]);

	for( var r2=0; r2 < curHighlightRows.length; ++r2 )
	{
		var curHighlightRow = curHighlightRows[r2];
		if( curHighlightRow == r )
		{
			fgcolor = "";
			bgcolor = "";
			for( var i=0; i < targetCells.length; ++i )
			{
				var colHasAss = false;
				for( var j=0; j < targetCells[i][1].length && !colHasAss; ++j )
				{
					if( targetCells[i][1][j] == curHighlightRow )
						colHasAss = true;
				}
				if( colHasAss )
				{
					var colToColor = targetCells[i][0];
					for( var j=0; j < tableElement.rows.length; ++j )
					{
						tableElement.rows[j].cells[colToColor].style.backgroundColor = bgcolor;
						tableElement.rows[j].cells[colToColor].style.color = fgcolor;
					}
				}
			}
	
			for( var i=0; i < tableElement.rows[curHighlightRow].cells.length; ++i )
			{
				tableElement.rows[curHighlightRow].cells[i].style.backgroundColor = bgcolor;
				tableElement.rows[curHighlightRow].cells[i].style.color = fgcolor;
			}
			curHighlightRows.splice(r2,1);
			didToggle = true;
			break;
		}
	}

	if( !didToggle )
		curHighlightRows.push(r);

	fgcolor = "rgb(255,255,255)";
	for( var r2=0; r2 < curHighlightRows.length; ++r2 )
	{
		var curHighlightRow = curHighlightRows[r2];
		for( var i=0; i < targetCells.length; ++i )
		{
			var colHasAss = false;
			for( var j=0; j < targetCells[i][1].length && !colHasAss; ++j )
			{
				if( targetCells[i][1][j] == curHighlightRow )
					colHasAss = true;
			}
			if( colHasAss )
			{
				var colToColor = targetCells[i][0];
				for( var j=0; j < tableElement.rows.length; ++j )
				{
					bgcolor = "rgb(150,150,150)";
					if( tableElement.rows[j].cells[colToColor].innerHTML == "x" )
				    {
				        var redValue = Math.min( Math.pow( tableElement.rows[curColorRow].cells[colToColor].innerHTML, 2 ), 255 );
					    var colColorRGB = [255,redValue,redValue];
					    bgcolor = "rgb("+(150-redValue)+","+(150-redValue)+","+(150-redValue)+")";
		            }
					tableElement.rows[j].cells[colToColor].style.backgroundColor = bgcolor;
					tableElement.rows[j].cells[colToColor].style.color = fgcolor;
				}
			}
		}
	}

	for( var r2=0; r2 < curHighlightRows.length; ++r2 )
	{
		var curHighlightRow = curHighlightRows[r2];
		for( var i=0; i < tableElement.rows[curHighlightRow].cells.length; ++i )
		{
			bgcolor = "rgb(100,100,100)";
			if( tableElement.rows[curHighlightRow].cells[i].innerHTML == "x" )
			{
				var redValue = 255 - Math.min( Math.pow( tableElement.rows[curColorRow].cells[i].innerHTML, 2 ), 255 );
			    var colColorRGB = [255,redValue,redValue];
			    bgcolor = "rgb("+(150-redValue)+","+(100-redValue)+","+(100-redValue)+")";
			}
			tableElement.rows[curHighlightRow].cells[i].style.backgroundColor = bgcolor;
			tableElement.rows[curHighlightRow].cells[i].style.color = fgcolor;
		}
	}

	setAssTableColors(t,curColorRow);
}

function colorParse(c)
{
	var col = c.replace(/[\#rgb\(]*/,'');
	var num = col.split(',');
	var base = 10;

	var ret = new Array(parseInt(num[0],base),parseInt(num[1],base),parseInt(num[2],base));
	return(ret);
}

var curColorRow;
function setAssTableColors(t,r)
{
	var tableElement = document.getElementById(t);
	var targetCells = cellsToColor[t];
	curColorRow = r;

	for( var i=1; i < 3; ++i )
		for( var j=1; j < tableElement.rows[i].cells.length; ++j )
		{
			var curColor = tableElement.rows[i].cells[j].style.backgroundColor;
			var curColorRGB = colorParse(curColor);
			var redValue = 255 - Math.min( Math.pow( tableElement.rows[i].cells[j].innerHTML, 2 ), 255 );
			var colColorRGB = [255,redValue,redValue];
			var finalColor;
			if( curColor == "" )
				finalColor = "rgb(" + colColorRGB[0] + "," + colColorRGB[1] + "," + colColorRGB[2] + ")";
			else
				finalColor = "rgb(" + Math.round((curColorRGB[0]+colColorRGB[0])/2) + "," + Math.round((curColorRGB[1]+colColorRGB[1])/2) + "," + Math.round((curColorRGB[2]+colColorRGB[2])/2) + ")";
			tableElement.rows[i].cells[j].style.backgroundColor = finalColor;
		}

	for( var i=0; i < targetCells.length; ++i )
	{
		var colToColor = targetCells[i][0];
		var colColor = tableElement.rows[r].cells[colToColor].style.backgroundColor;

		for( var j=0; j < targetCells[i][1].length; ++j )
		{
			var finalColor;
			var curColor = tableElement.rows[targetCells[i][1][j]].cells[colToColor].style.backgroundColor;
			if( curColor == "" )
			{
				finalColor = colColor;
			} else
			{
				var curColorRGB = colorParse(curColor);
				if( curColorRGB[0] == 255 )
					finalColor = colColor;
				else
				{
					var colColorRGB = colorParse(colColor);
					finalColor = "rgb(" + Math.round((curColorRGB[0]+colColorRGB[0])/2) + "," + Math.round((curColorRGB[1]+colColorRGB[1])/2) + "," + Math.round((curColorRGB[2]+colColorRGB[2])/2) + ")";
				}
			}
			tableElement.rows[targetCells[i][1][j]].cells[colToColor].style.backgroundColor = finalColor;
		}
	}
}