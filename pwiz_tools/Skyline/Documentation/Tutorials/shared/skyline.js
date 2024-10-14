function skylineOnload () {
    if (self != top)
      targetTop();
    
    addFigureAltText()
    addTableOfContents()
    scrollToAnchor()
}

function targetTop()
{
  document.querySelectorAll('a[href]').forEach(anchor => {
    if (anchor.target !== '_blank') {
        anchor.target = '_top';
    }
});
}

function addFigureAltText()
{
    const images = document.querySelectorAll('img');
    var figureCounter = 1;
    images.forEach((img) => {
        if (img.src.includes("/s-")) {
            img.title = `Figure ${figureCounter}`;

            var anchor = document.createElement('a');
            const anchorId = `figure${figureCounter}`
            anchor.setAttribute('name', anchorId);
            anchor.setAttribute('id', anchorId);
            img.parentNode.parentNode.insertBefore(anchor, img.parentNode);

            figureCounter++
        }
    });
}

function addTableOfContents(){
    var titleElement = document.getElementsByClassName("document-title")[0];
    if(titleElement){
        var tocElement = document.getElementsByClassName("toc")[0];
        if(!tocElement){
            tocElement = document.createElement('div');
            tocElement.setAttribute('class', "toc");
            titleElement.insertAdjacentElement("afterend", tocElement);
        }

        var headings = [].slice.call(document.body.querySelectorAll('h1, h2, h3, h4, h5, h6'));
        headings.forEach(function (heading, index) {
            if(!heading.classList.contains("document-title")){
                var headingTextContent = heading.textContent.trim();
                //remove English and Chinese/Japanese colons
                if (headingTextContent.endsWith(":") || headingTextContent.endsWith("：")){
                    headingTextContent = headingTextContent.slice(0, -1);
                }
                var headingId = headingTextContent.replace(/ /g,"_");

                //create toc link
                var link = document.createElement('a');
                link.setAttribute('href', '#' + headingId);

                link.textContent = headingTextContent;

                //create div for toc row
                var div = document.createElement('div');
                div.setAttribute('class', "toc-" +heading.tagName.toLowerCase());

                //add toc row to toc
                div.appendChild(link);
                tocElement.appendChild(div);

                //create anchor before heading for toc link

                var anchor = document.createElement('a');
                anchor.setAttribute('name', headingId);
                anchor.setAttribute('id', headingId);
                heading.parentNode.insertBefore(anchor, heading);
            }
        });
    }
}

function scrollToAnchor(){
    if(location.hash){
        document.getElementById(location.hash.substring(1)).scrollIntoView();
    }
}