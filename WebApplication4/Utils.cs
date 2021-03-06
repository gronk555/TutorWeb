﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Configuration;
using WebApplication4.Models;
using System.Net;
using Newtonsoft.Json;
using System.Text;

namespace WebApplication4
{
  public static class Utils
  {
    public static char[] WordSeparators = new char[] { ' ', '\t', '\r', '\n', ',', '.', '!', '?', '-', '+' };
    #region edited modules cache
    //we start agent timer in Global.asax.cs with task to flush module cache every 2 mins
    public class CachedModule
    {
      /// <summary>All rows of the module</summary>
      public List<DirtyRow> Rows = new List<DirtyRow>();
      /// <summary>Must be saved to database</summary>
      public bool IsDirty = false;
      /// <summary>Module text has changed, and TTS must be run again</summary>
      public bool IsModified = false;
      /// <summary>Must download TTS for all phrases</summary>
      public bool EnableTTS = false;
      /// <summary>TTS for all phrases has been downloaded</summary>
      public bool CompletedTTS = false;
      /// <summary>Native lang code to be used in TTS download</summary>
      public string NativeLangCode;
      /// <summary>Foreign lang code to be used in TTS download</summary>
      public string ForeignLangCode;
      /// <summary>If editor/browser was closed/logged out, but getTTS() was still working, we mark the module to be un-cached later when getTTS finishes</summary>
      public bool SessionExpired = false;
      public string LangCode(int iRow)
      {
        return iRow % 2 == 0 ? NativeLangCode : ForeignLangCode;
      }
    }
    public class ModuleChanges
    {
      public int ModuleId { get; set; }
      public int TotalRowCnt { get; set; }
      public DirtyRow[] DirtyRows { get; set; }
      public bool EnableTTS { get; set; }
    }
    public class DirtyRow
    {
      /// <summary>
      /// index of each row helps to determing langCode: (iRow % 2 == 0) ? module.NativeLang : module.ForeignLang; 
      /// also index is passed from client to know position to insert the row in the cached module
      /// </summary>
      public int iRow { get; set; }
      public string value { get; set; }
      /// <summary>TTS for this phrase has been downloaded</summary>
      public bool CompletedTTS { get; set; }
    }


    /// <summary>map of module name to all its rows, for all modules currently being edited</summary>
    public static ConcurrentDictionary<int, CachedModule> moduleCache = new ConcurrentDictionary<int, CachedModule>();

    /// <summary>called from controller only, when a module is open for editing, or created new</summary>
    /// <param name="moduleId"></param>
    /// <param name="db">db context passed from controller, must exist because controller creates it for every request</param>
    /// <returns>true if module exists in db</returns>
    public static bool PopulateModuleCache(int moduleId, Entities db)
    {
      try
      {
        if (moduleCache.Keys.Contains(moduleId))
        { // if session was dead, but getTTS still used the cached module for download TTS, and we reopen the editor, 
          // then mark SessionExpired = false, so that when getTTS finishes it does not clear module from cache
          moduleCache[moduleId].SessionExpired = false;
          return true;
        }
        Module m = db.Modules.FirstOrDefault(o => o.Id == moduleId); // read module from db into the dict
        if (m == null) return false;
        var cm = new CachedModule();
        cm.ForeignLangCode = m.ForeignLang;
        cm.NativeLangCode = m.NativeLang;
        cm.Rows = string.IsNullOrWhiteSpace(m.Text) ?
          cm.Rows :
          m.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None).Select((s, i) => new DirtyRow()
          {
            value = s,
            iRow = i,
          }).ToList();
        moduleCache[moduleId] = cm;
        return true;
      }
      catch (Exception ex)
      {
        Log($"{ex.Message}\r\n{ex.StackTrace}");
        return false;
      }
    }

    /// <summary>called from SaveModuleText api</summary>
    /// <param name="moduleId"></param>
    /// <param name="totalRowCnt">if total row count in edited module is less than in saved module, then delete all extra rows from saved module</param>
    /// <param name="dirtyRows">modified rows in edited module</param>
    public static void UpdateModuleCache(ModuleChanges param)
    {
      try
      {
        var cm = moduleCache[param.ModuleId];
        if (param.TotalRowCnt < cm.Rows.Count)
          cm.Rows.RemoveRange(param.TotalRowCnt, cm.Rows.Count - param.TotalRowCnt);
        else if (param.TotalRowCnt > cm.Rows.Count)
        {
          // allocate for new rows
          var newRange = Enumerable.Range(0, param.TotalRowCnt - cm.Rows.Count)
            .Select(i => new DirtyRow() { iRow = cm.Rows.Count, value = "", CompletedTTS = false }); // this is not obvious how indexing increments
          cm.Rows.AddRange(newRange);
        };
        Array.ForEach<DirtyRow>(param.DirtyRows, r =>
        {
          r.value = r.value == null ? r.value : string.Join(" ", r.value.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim(); //replace invalid file chars with space
          cm.Rows[r.iRow] = r;
        }); // copy all updated rows to cache
        cm.IsDirty = true; //set after copying is done, otherwise FlushModuleCache may flush the cached module and mark it as clean before we finished copying dirtyRows
        cm.IsModified = true; //even if getTTS has just finished downloading all sounds for the module, the new change must make getTTS run again.
        cm.EnableTTS = param.EnableTTS; // will be used by getTTS
      }
      catch (Exception ex)
      {
        Log($"{ex.Message}\r\n{ex.StackTrace}");
      }
    }

    /// <summary>
    /// called from agent callback every N mins, also from Application_End
    /// </summary>
    /// <param name="moduleId">if null or not passed, we flush all dirty modules to db</param>
    /// <param name="db">db context created in the agent for just one task, or in the controller, and passed here</param>
    /// <returns>true if module exists</returns>
    /// <remarks>probably it is safe in the way, that with short timer interval we'll have multiple threads running this method in AgentCallback, 
    /// and it will distribute work evenly among them, so it will finish faster that using one thread</remarks>
    public static bool FlushModuleCache(int moduleId = 0)
    {
      Entities db = new Entities();  // must be separate instance for each thread

      if (moduleId == 0)
      {
        foreach (var cm in moduleCache) FlushModuleCache(cm.Key);
        return true;
      }
      try
      {
        if (!moduleCache[moduleId].IsDirty) return true; // skip unmodified modules, also skips modules currently being saved by other threads
        moduleCache[moduleId].IsDirty = false; // to prevent other threads from saving this module again while current thread is still doing it
        Module m = db.Modules.FirstOrDefault(o => o.Id == moduleId);
        if (m == null)
        {
          CachedModule val = null;
          moduleCache.TryRemove(moduleId, out val);
          return false;
        }
        m.Text = string.Join("\r\n", moduleCache[moduleId].Rows.Select(r => r.value));
        db.SaveChanges(); // this can take long
      }
      catch (Exception ex)
      {
        moduleCache[moduleId].IsDirty = true;
        Log($"{ex.Message}\r\n{ex.StackTrace}");
        return false;
      }
      return true;
    }

    private static bool getTTSIsRunning = false;
    /// <summary>
    /// for all cached modules with EnableTTS and not CompletedTTS: take each phrase, decompose into words, call DownloadTTS for each phrase/word;
    /// in AgentCallback() call getTTs() after FlushModuleCache(),
    /// this will block the thread, next thread created by agent will flush again, but getTTS() will exit if it is already running
    /// </summary>
    public static void getTTS()
    {
      if (getTTSIsRunning) return;
      getTTSIsRunning = true;
      // download for one module at a time
      foreach (var m in moduleCache.Where(mm => mm.Value.EnableTTS && !mm.Value.CompletedTTS))
      {
        try
        {
          m.Value.CompletedTTS = true;
          m.Value.IsModified = false; // may be flipped in outside thread by UpdateModuleCach
          foreach (var r in m.Value.Rows.Where(rr => !rr.CompletedTTS && !string.IsNullOrWhiteSpace(rr.value)))
          {
            string langCode = m.Value.LangCode(r.iRow);
            r.CompletedTTS = DownloadTTS(r.value, langCode, ttsFilePath(r.value, langCode));
            foreach (var word in r.value.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
              var _word = word.Where(c => !char.IsPunctuation(c)).Aggregate("", (current, c) => current + c); // remove remaining punctuation from separate words
              if (!string.IsNullOrWhiteSpace(_word))
                r.CompletedTTS &= DownloadTTS(_word, langCode, ttsFilePath(_word, langCode));
            }
            m.Value.CompletedTTS &= (r.CompletedTTS && !m.Value.IsModified);
          }
          // after module download finished, check if user's session is expired, then remove module from cache
          if (moduleCache[m.Key].SessionExpired && m.Value.CompletedTTS)
          {
            CachedModule val = null;
            moduleCache.TryRemove(m.Key, out val);
          }
        }
        catch (Exception ex)
        {
          // TODO: if we call UpdateModuleCache while getTTS is working, getTTS will throw "collection was modified" and will restart all modules in 2 mins
          // probably more graceful is to 
          // 1. create a copy of module rows by calling Rows.ToList(), 
          // 2. iterate all of them, 
          // 3. after finishing each module, check a flag module.IsModified that is set outside of loop by UpdateModuleCache: if (m.IsModified) CompletedTTS = false;
          // so that external modification during getTTS will mark module dirty for another round
          m.Value.CompletedTTS = false;
          Log($"{ex.Message}\r\n{ex.StackTrace}");
        }
      }
      getTTSIsRunning = false;
    }

    public static string ttsFilePath(string phrase, string langCode)
    {
      var ttsPath = HttpRuntime.Cache["ttsPath"] as string;
      phrase = phrase.ToLowerInvariant() == "con" ? "conn" : phrase; // "con" file is reserved for system console
      var dir = Directory.CreateDirectory(Path.Combine(ttsPath, langCode)); // if lang is new, create a folder for it
      return Path.Combine(dir.FullName, phrase + ".mp3");
    }

    /// first check https://api.soundoftext.com/sounds , then a few more fallback sites
    /// <summary>
    /// POST to https://api.soundoftext.com/sounds 
    /// {
    ///  "engine": "Google",
    ///  "data": {
    ///    "text": "Hello, world",
    ///    "voice": "en-US"
    ///  }
    /// }
    /// response: 
    /// {success: true, id: "981684a0-15fa-11e8-9416-5382295fa6ae"}
    /// GET https://api.soundoftext.com/sounds/981684a0-15fa-11e8-9416-5382295fa6ae
    /// response:
    /// {status: "Done", location:"https://soundoftext.nyc3.digitaloceanspaces.com/981684a0-15fa-11e8-9416-5382295fa6ae.mp3"}
    /// GET https://soundoftext.nyc3.digitaloceanspaces.com/981684a0-15fa-11e8-9416-5382295fa6ae.mp3
    /// 
    /// </summary>
    /// <param name="text">phrase/word to be converted to mp3 file</param>
    /// <param name="langCode">must be compliant with the list (case sensitive match!) https://soundoftext.com/docs#index </param>
    /// <param name="filePath">location of mp3 file</param>
    /// <returns>true if success or already exists</returns>
    public static bool DownloadTTS(string text, string langCode, string filePath)
    {
      bool result = false;
      if (String.IsNullOrWhiteSpace(text) ||
        String.IsNullOrWhiteSpace(langCode) ||
        String.IsNullOrWhiteSpace(filePath))
        return result;
      text = text.ToLowerInvariant() == "con" ? "conn" : text; // "con" file is reserved for system console
      if (File.Exists(filePath)) return true;
      var url = "https://api.soundoftext.com/sounds/";
      using (WebClient wc = new WebClient())
      {
        var data = new
        {
          engine = "Google",
          data = new
          {
            text = text,
            voice = langCode
          }
        };
        wc.Headers[HttpRequestHeader.ContentType] = "application/json";
        wc.Encoding = Encoding.UTF8;
        try
        {
          dynamic res = System.Web.Helpers.Json.Decode(wc.UploadString(url, "POST", JsonConvert.SerializeObject(data)));
          res = System.Web.Helpers.Json.Decode(wc.DownloadString(url + res.id));
          if (res.status == "Pending" || res.location == null)
          {
            Log($"pending: {text}");
            return result;
          }
          else
            wc.DownloadFile(res.location, filePath);
          result = true;
        }
        catch (Exception ex)
        {
          Log($"{ex.Message}\r\n{ex.StackTrace}");
          return result;
        }
      }
      return result;
    }


    /// <summary>
    /// called from agent timer every N mins, also from Application_End, and when user closes module editor
    /// </summary>
    /// <param name="state"></param>
    public static void AgentCallback(object state)
    {
      FlushModuleCache();
      getTTS();
    }
    #endregion module cache

    private static object locker = new object();
    public static void Log(string msg)
    {
      try
      {
        lock (locker)
        {
          if (ConfigurationManager.AppSettings["EnableLog"] != "true") return;
          StreamWriter s = new StreamWriter(ConfigurationManager.AppSettings["LogFileName"], true);
          s.WriteLine(DateTime.UtcNow.ToString("dd/MM/yy HH:mm:ss") + " " + msg);
          s.Close();
        }
      }
      catch (Exception ex)
      {
        Console.WriteLine(ex.Message);
      }
    }

  }
}