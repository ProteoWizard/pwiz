function skylineOnload (documentRef) {
    var documentRef = documentRef || document;
    var titleElement = documentRef.getElementsByClassName("document-title")[0];
    if(titleElement){
        var tocElement = documentRef.getElementsByClassName("toc")[0];
        if(!tocElement){
            tocElement = documentRef.createElement('div');
            tocElement.setAttribute('class', "toc");
            titleElement.insertAdjacentElement("afterend", tocElement);
        }

        var headings = [].slice.call(documentRef.body.querySelectorAll('h1, h2, h3, h4, h5, h6'));
        headings.forEach(function (heading, index) {
            if(!heading.classList.contains("document-title")){
                var headingTextContent = heading.textContent.trim();
                if (headingTextContent.endsWith(":")){
                    headingTextContent = headingTextContent.slice(0, -1);
                }
                var headingId = headingTextContent.replace(/ /g,"_");

                //create toc link
                var link = documentRef.createElement('a');
                link.setAttribute('href', '#' + headingId);

                link.textContent = headingTextContent;

                //create div for toc row
                var div = documentRef.createElement('div');
                div.setAttribute('class', "toc-" +heading.tagName.toLowerCase());

                //add toc row to toc
                div.appendChild(link);
                tocElement.appendChild(div);

                //create anchor before heading for toc link

                var anchor = documentRef.createElement('a');
                anchor.setAttribute('name', headingId);
                anchor.setAttribute('id', headingId);
                heading.parentNode.insertBefore(anchor, heading);
            }
        });
    }
}