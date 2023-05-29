using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Frontend.Filter
{
    public class AuthFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {

//#if DEBUG
//            return;
//#endif

            //when requesting login form, no authentication is required
            if (actionContext.Request.RequestUri.LocalPath == "/Templates/loginForm")
            {
                return;
            }

            //read session cookie
            var accessToken = actionContext.Request.Headers.GetCookies("session_id");

            //no cookie at all
            if (accessToken.Count == 0) {
             actionContext.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            else
            {
                //look for the session cookie
                foreach (var cookieSet in accessToken)
                {
                    foreach (var cookie in cookieSet.Cookies)
                    {
                        if (cookie.Name == "session_id")
                        {
                            //cookie found, check authentication
                            if (!App_Start.Authentication.isAuthenticated(cookie.Value))
                            {
                                //cookie not authenticated
                                actionContext.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
                                return;
                            }
                            else
                            {
                                //session is ok, go on
                                return;
                            }
                        }
                    }
                }

                //when this code gets reached no session cookie was found
                actionContext.Response = new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
        }
    }
}