﻿using System.Web.Mvc;
using System.Web.Routing;

namespace Platibus.SampleWebApp
{
    public class RouteConfig
    {
        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute("Default", "{controller}/{action}/{id}",
                new {controller = "SendTestMessage", action = "Index", id = UrlParameter.Optional}
                );
        }
    }
}