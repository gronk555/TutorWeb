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
    public static ConcurrentDictionary<string, CachedModule> ModuleCache = new ConcurrentDictionary<string, CachedModule>();

    /// <summary>called from controller only, when a module is open for editing, or created new</summary>
    /// <param name="moduleName"></param>
    /// <param name="db">db context passed from controller, must exist because controller creates it for every request</param>
    /// <returns>true if module exists in db</returns>
    public static bool PopulateModuleCache(string moduleName, Entities db)
    {
      if (ModuleCache.Keys.Contains(moduleName)) return true;
      Module m = db.Modules.FirstOrDefault(o => o.Name == moduleName);
      if (m == null) return false;
      var cm = new CachedModule(); // read module from db into the dict
      cm.Rows = string.IsNullOrWhiteSpace(m.Text) ? cm.Rows : m.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None).ToList();
      ModuleCache[moduleName] = cm;
      return true;
    }

    /// <summary>called from SaveModuleText api</summary>
    /// <param name="moduleName"></param>
    /// <param name="totalRowCnt">if total row count in edited module is less than in saved module, then delete all extra rows from saved module</param>
    /// <param name="dirtyRows">modified rows in edited module</param>
    public static void UpdateModuleCache(string moduleName, int totalRowCnt, List<Tuple<int, string>> dirtyRows)
    {
      var cm = ModuleCache[moduleName];
      cm.IsDirty = true;
      if (totalRowCnt < cm.Rows.Count)
        cm.Rows.RemoveRange(totalRowCnt, int.MaxValue);
      dirtyRows.ForEach(t => cm.Rows[t.Item1] = t.Item2);
    }

    /// <summary>
    /// called from agent callback every N mins, also from Application_End, and when user closes module editor
    /// </summary>
    /// <param name="moduleName">if null or not passed, we flush all dirty modules to db</param>
    /// <param name="db">db context created in the agent for just one task, or in the controller, and passed here</param>
    /// <returns>true if module exists</returns>
    public static bool FlushModuleCache(Entities db, string moduleName = null)
    {
      if (string.IsNullOrWhiteSpace(moduleName))
      {
        foreach (var cm in ModuleCache) FlushModuleCache(db, cm.Key);
      }
      try
      {
        if (!ModuleCache[moduleName].IsDirty) return true; // skip unmodified modules, also skips modules currently being saved by other threads
        Module m = db.Modules.FirstOrDefault(o => o.Name == moduleName);
        if (m == null) return false;
        m.Text = string.Join("\r\n", ModuleCache[moduleName].Rows);
        ModuleCache[moduleName].IsDirty = false; // to prevent other threads from saving this module again while current thread is still doing it
        db.SaveChanges(); // this can take long
      }
      catch
      {
        ModuleCache[moduleName].IsDirty = true; 
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
      Entities db = new Entities();
      FlushModuleCache(db);
    }

    /// <summary>
    /// called from module index if admin or author deletes the module
    /// </summary>
    /// <param name="moduleName"></param>
    public static void DeleteModuleCache(string moduleName = null)
    {
      CachedModule m = null;
      ModuleCache.TryRemove(moduleName, out m);
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