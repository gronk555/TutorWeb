using System;
using System.Linq;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Collections.Generic;

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
      agentTimer = new Timer(new TimerCallback(Utils.AgentCallback), null, 0, 120000);
    }

    protected void Application_End()
    {
      agentTimer.Change(0, 0); // TODO: test
    }

    protected void Session_OnEnd(object sender, EventArgs e)
    {
      // on GET Edit(id) we add module Id to session[“ModuleIds”] array;
      // when session expires, we remove modules opened by the session from cache.
      // Not all removed, but only completed and not downloadable (EnableTTS == false);
      // other modules should stay in cache for use by getTTS() untill download finishes.
      foreach (int mId in Session["moduleIds"] as List<int>)
      {
        Utils.CachedModule val = null;
        if (!Utils.moduleCache[mId].EnableTTS || Utils.moduleCache[mId].CompletedTTS)
          Utils.moduleCache.TryRemove(mId, out val);
        else
        { // this will force getTTS() un-cache the module after TTS download is finished; 
          // unless the module is reopened in editor, then SessionExpired will be false again;
          Utils.moduleCache[mId].SessionExpired = true;
        }
      }
    }

  }
}
