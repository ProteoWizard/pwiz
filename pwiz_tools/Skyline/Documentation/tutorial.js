function renderPage(tutorialName, pdf_ver, cover_ver, data_suffix, lang)
{
  var version = '25-1';  // change to update version - must exist https://skyline.ms/tutorials/24-1/
  var altVersion = '';  // change to update beta version - must exist https://skyline.ms/tutorials/25-0-9/
  pdf_ver = null;  // Comment this line to add back links to last PDF files
  cover_ver = cover_ver.replaceAll('.', '_'); // No longer used
  var lang_img_suffix = '';
  var lang_pdf_suffix = '';
  if (lang != 'en')
  {
    lang_img_suffix = '-' + lang;
    lang_pdf_suffix = '_' + lang;
  }
  var lang_dir = langLookup(lang, '', 'Japanese/', 'Chinese/');

  var search = window.location.search;
  var paramVer = new URLSearchParams(search).get('ver');
  if (paramVer != null)
    version = paramVer;
  var showHtmlIndex = search.indexOf('&show=html');
  var showAltHtmlIndex = search.indexOf('&show=alt-html');
  if (showHtmlIndex == -1 && showAltHtmlIndex == -1)
  {
    document.getElementById("summary").style.display = 'block';
    var htmlRef = window.location.href + '&show=html';
    if (pdf_ver == null)
    {
      document.getElementById("doc_pdf").style.display = 'none';
      document.getElementById("doc_img_a").href = htmlRef;
    }
    else
    {
      pdf_ver = pdf_ver.replaceAll('.', '_');
      var pdfSrc = '/_webdav/home/software/Skyline/%40files/tutorials/' + lang_dir + tutorialName + '-' + pdf_ver + lang_pdf_suffix + '.pdf';
      document.getElementById("doc_pdf_a").href = pdfSrc;
      document.getElementById("doc_pdf_a").textContent = langLookup(lang, 'pdf', 'ダウンロード', '下载');
      document.getElementById("doc_img_a").href = pdfSrc;
      document.getElementById("doc_img_a").target = '_blank';
    }
    document.getElementById("doc_html_a").href = htmlRef;
    document.getElementById("doc_html_a").textContent = langLookup(lang, 'html', 'html', '哈特莫');
    if (altVersion.length > 0)
      addAltHtmlLink(tutorialName, lang, altVersion);    
    document.getElementById("doc_img").src = '/tutorials/' + version + '/' + tutorialName + '/' + lang + '/cover.png';
    if (document.getElementById("data_a") != null)
    {
      if (data_suffix.includes('.'))
        data_suffix = '-' + data_suffix.replaceAll('.', '_');
      document.getElementById("data_a").href = '/tutorials/' + tutorialName + data_suffix + '.zip';
      document.getElementById("data_a").textContent = langLookup(lang, 'data', 'データ', '数据');
    }
  }
  else if (showAltHtmlIndex != -1)
  {
    showHtmlVersion(altVersion, tutorialName, lang, showAltHtmlIndex);
  }
  else
  {
    showHtmlVersion(version, tutorialName, lang, showHtmlIndex);
  }
}

function adjustHtmlSize(event)
{
    document.getElementById('html_frame').style.width = (window.innerWidth - 350) + 'px';
}

function scrollToHash(event)
{
  var hash = window.location.hash;
  if (hash) {
    document.getElementById('html_frame').contentWindow.location.hash = hash;
  }
}

function langLookup(lang, textEn, textJa, textZh)
{
  if (lang.startsWith('ja'))
    return textJa;
  if (lang.startsWith('zh'))
    return textZh;
  return textEn;
}

function resizeIframe(obj)
{
    obj.style.height = obj.contentWindow.document.documentElement.scrollHeight + 'px';
}

function addAltHtmlLink(tutorialName, lang, altVersion) {
  var htmlLink = document.getElementById("doc_html_a");
  if (!htmlLink || !htmlLink.parentElement)
    return;

  var currentUrl = window.location.href;
  var sep = currentUrl.includes('?') ? '&' : '?';
  var altHtmlRef = currentUrl + sep + 'show=alt-html';

  var altAnchor = document.createElement("a");
  altAnchor.id = "doc_html_alt_a";
  altAnchor.href = altHtmlRef;
  altAnchor.textContent = langLookup(lang, 'html ' + altVersion.replace(/-/g, '.'), 'html ' + altVersion.replace(/-/g, '.'), '哈特莫 ' + altVersion.replace(/-/g, '.'));
  altAnchor.target = '_self';

  var wrapper = document.createElement("span");
  wrapper.appendChild(document.createTextNode("["));
  wrapper.appendChild(altAnchor);
  wrapper.appendChild(document.createTextNode("] "));

  htmlLink.parentElement.insertAdjacentElement("afterend", wrapper);
}

function showHtmlVersion(version, tutorialName, lang, showParamIndex) {
  var htmlSrc = '/tutorials/' + version + '/' + tutorialName + '/' + lang + '/';
  document.getElementById("html_frame").src = htmlSrc;
  document.getElementById("html_frame").height = '2000px'; // default size, resized on load
  document.getElementById("html").style.display = 'block';

  var printAnchor = document.evaluate("//a[text()='Print']",
    document, null, XPathResult.FIRST_ORDERED_NODE_TYPE, null).singleNodeValue;
  if (printAnchor)
    printAnchor.href = htmlSrc;

  document.getElementById("html_link").href =
    window.location.pathname + window.location.search.substring(0, showParamIndex);

  window.addEventListener('resize', adjustHtmlSize, true);
  window.addEventListener('load', scrollToHash);
  window.addEventListener('hashchange', scrollToHash);
  adjustHtmlSize(0);
}