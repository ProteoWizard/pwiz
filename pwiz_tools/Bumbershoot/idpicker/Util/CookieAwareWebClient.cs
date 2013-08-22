//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is StackOverflow developer "AppDeveloper".
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Matt Chambers
//

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace IDPicker
{
    public class CookieAwareWebClient : WebClient
    {
        public CookieContainer CookieContainer { get; set; }
        public Uri Uri { get; set; }

        public CookieAwareWebClient()
            : this(new CookieContainer()) { }

        public CookieAwareWebClient(CookieContainer cookies) { this.CookieContainer = cookies; }

        protected override WebRequest GetWebRequest(Uri address)
        {
            WebRequest request = base.GetWebRequest(address);
            if (request is HttpWebRequest)
                (request as HttpWebRequest).CookieContainer = this.CookieContainer;
            HttpWebRequest httpRequest = (HttpWebRequest) request;
            httpRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            return httpRequest;
        }

        protected override WebResponse GetWebResponse(WebRequest request)
        {
            WebResponse response = base.GetWebResponse(request);
            String setCookieHeader = response.Headers[HttpResponseHeader.SetCookie];

            if (setCookieHeader != null)
            {
                //do something if needed to parse out the cookie.
                if (setCookieHeader != null)
                {
                    Cookie cookie = new Cookie(); //create cookie
                    cookie.Name = request.RequestUri.Host + "_" + setCookieHeader.GetHashCode().ToString();
                    cookie.Domain = request.RequestUri.Host;
                    this.CookieContainer.Add(cookie);
                }
            }
            return response;
        }
    }
}