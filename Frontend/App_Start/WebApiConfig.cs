﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace Frontend
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // Web-API-Konfiguration und -Dienste

            // Web-API-Routen
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.Routes.MapHttpRoute(
                name: "TeamplatesApi",
                routeTemplate: "{controller}/{name}",
                defaults: new { id = RouteParameter.Optional }
            );

            //enable auth filter
            
        }
    }
}
