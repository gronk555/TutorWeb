using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;

namespace WebApplication4
{
  public class RouteConfig
  {
    public static void RegisterRoutes(RouteCollection routes)
    {
      routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

      routes.MapRoute(
          name: "Default",
          url: "{controller}/{action}/{id}",
          defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
      );

      routes.MapRoute(
          "Modules",
          "Module/nativeLang/{nativeLang}/foreignLang/{foreignLang}/orderBy/{orderBy}/page/{page}/pagesize/{pageSize}/search/{search}",
          new { controller = "Module", action = "Index", nativeLang = UrlParameter.Optional, foreignLang = UrlParameter.Optional, orderBy = UrlParameter.Optional, page = UrlParameter.Optional, pageSize = UrlParameter.Optional, search = UrlParameter.Optional }
      );
      //routes.MapRoute(
      //    name: "Default",
      //    url: "{controller}/{action}/{id}",
      //    defaults: new { controller = "Home", action = "Index", id = UrlParameter.Optional }
      //);
    }
  }
}
