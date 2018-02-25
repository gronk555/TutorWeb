using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Configuration;
using WebApplication4.Models;

namespace WebApplication4
{
  public static class Utils
  {
    #region edited modules cache
    public class CachedModule
    {
      /// <summary>All rows of the module</summary>
      public List<string> Rows = new List<string>();
      /// <summary>Must be saved to file</summary>
      public bool IsDirty = false;
    }

    /// <summary>map of module name to all its rows, for all modules currently being edited</summary>
    private static ConcurrentDictionary<int, CachedModule> moduleCache = new ConcurrentDictionary<int, CachedModule>();

    /// <summary>called from controller only, when a module is open for editing, or created new</summary>
    /// <param name="moduleId"></param>
    /// <param name="db">db context passed from controller, must exist because controller creates it for every request</param>
    /// <returns>true if module exists in db</returns>
    public static bool PopulateModuleCache(int moduleId, Entities db)
    {
      if (moduleCache.Keys.Contains(moduleId)) return true;
      Module m = db.Modules.FirstOrDefault(o => o.Id == moduleId);
      if (m == null) return false;
      var cm = new CachedModule(); // read module from db into the dict
      cm.Rows = string.IsNullOrWhiteSpace(m.Text) ? cm.Rows : m.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
      moduleCache[moduleId] = cm;
      return true;
    }

    /// <summary>called from SaveModuleText api</summary>
    /// <param name="moduleId"></param>
    /// <param name="totalRowCnt">if total row count in edited module is less than in saved module, then delete all extra rows from saved module</param>
    /// <param name="dirtyRows">modified rows in edited module</param>
    public static void UpdateModuleCache(int moduleId, int totalRowCnt, List<Tuple<int, string, string>> dirtyRows)
    {
      var cm = moduleCache[moduleId];
      if (totalRowCnt < cm.Rows.Count)
        cm.Rows.RemoveRange(totalRowCnt, int.MaxValue);
      else if (totalRowCnt > cm.Rows.Count)
        cm.Rows.AddRange(new string[totalRowCnt - cm.Rows.Count]); // allocate for new rows
      dirtyRows.ForEach(t => cm.Rows[t.Item1] = t.Item2); // copy all updated rows to cache
      cm.IsDirty = true; //set after copying is done, otherwise FlushModuleCache may flush the cached module and mark it as clean before we finished copying dirtyRows
    }

    /// <summary>
    /// called from agent callback every N mins, also from Application_End, and when user closes module editor
    /// </summary>
    /// <param name="moduleId">if null or not passed, we flush all dirty modules to db</param>
    /// <param name="db">db context created in the agent for just one task, or in the controller, and passed here</param>
    /// <returns>true if module exists</returns>
    /// <remarks>probably it is safe in the way, that with short timer interval we'll have multiple threads running this method in AgentCallback, 
    /// and it will distribute work evenly among them, so it will finish faster that using one thread</remarks>
    public static bool FlushModuleCache(Entities db, int moduleId = 0)
    {
      if (moduleId == 0)
      {
        foreach (var cm in moduleCache) FlushModuleCache(db, cm.Key);
        return true;
      }
      try
      {
        if (!moduleCache[moduleId].IsDirty) return true; // skip unmodified modules, also skips modules currently being saved by other threads
        moduleCache[moduleId].IsDirty = false; // to prevent other threads from saving this module again while current thread is still doing it
        Module m = db.Modules.FirstOrDefault(o => o.Id == moduleId);
        if (m == null)
        {
          var val = new CachedModule();
          moduleCache.TryRemove(moduleId, out val);
          return false;
        }
        m.Text = string.Join("\r\n", moduleCache[moduleId].Rows);
        db.SaveChanges(); // this can take long
      }
      catch
      {
        moduleCache[moduleId].IsDirty = true; 
        return false;
      }
      return true;
    }

    /// <summary>
    /// called from agent timer every N mins, also from Application_End, and when user closes module editor
    /// </summary>
    /// <param name="state"></param>
    public static void AgentCallback(object state)
    {
      Entities db = new Entities();  // must be separate instance for each thread
      FlushModuleCache(db);
    }

    /// <summary>
    /// called from module index if admin or author deletes the module
    /// </summary>
    /// <param name="moduleId"></param>
    public static void DeleteModuleCache(int moduleId = 0)
    {
      CachedModule m = null;
      moduleCache.TryRemove(moduleId, out m);
    }

    //we start agent timer in Global.asax.cs with task to flush module cache every 2 mins
    #endregion module cache

    public static void Log(string msg)
    {
      try
      {
        if (ConfigurationManager.AppSettings["EnableLog"] != "true") return;
        StreamWriter s = new StreamWriter(ConfigurationManager.AppSettings["LogFileName"], true);
        s.WriteLine(DateTime.UtcNow.ToString("dd/MM/yy HH:mm:ss") + " " + msg);
        s.Close();
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }

  }
}