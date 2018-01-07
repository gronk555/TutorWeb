using System;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace WebApplication4
{
  public class MvcApplication : HttpApplication
  {
    private Timer agentTimer = null;
    protected void Application_Start()
    {
      AreaRegistration.RegisterAllAreas();
      FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
      RouteConfig.RegisterRoutes(RouteTable.Routes);
      BundleConfig.RegisterBundles(BundleTable.Bundles);
      
      //start agent timer with task to flush module cache every 2 mins
      agentTimer = new Timer(new TimerCallback(Utils.AgentCallback), null, 0, 2000);      // TODO: test
    }

    protected void Application_End()
    {
      agentTimer.Change(0, 0); // TODO: test
    }
  }
}
