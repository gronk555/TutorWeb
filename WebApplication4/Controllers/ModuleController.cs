using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using WebApplication4.Models;
using Microsoft.AspNet.Identity;
using System.ComponentModel.DataAnnotations;
using System.Configuration;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

//TODO: 
//0. 
//1. create paypal buttons with item name = modulename_Id, put them in module/index and module/details views
//2. test IPN: try to create & catch incomplete transaction or create buttons with 1 cent price and do real payment
//3. for each lang create a separate website, use them as fallback if some of them die
//4. 
//5. 
//6. 
//7. 
//8. 
//9.
//10. add switch to show my/all modules, if login==null, set disabled switch on 'all'
//11.
//12. add filter to Index(): by lang, by Tags, by keyword in name or description (priority: low)
//13. when deploying, change identity of defaultapppool to system:ApplicationPoolIdentity; create a new identity will limited access

namespace WebApplication4.Controllers
{
  public class ModuleController : Controller
  {
    private Entities db = new Entities(); //TODO: probably more efficient to create inside api methods that use it

    public class Phrase
    {
      public string NativeText;
      public bool NativeTextReady;
      public string ForeignText;
      public bool ForeignTextReady;
      public string Explanation;
      public bool ExplanationReady;
      public string NativeLangCode;
      public string ForeignLangCode;
      public List<string> NativeWords;            // TODO: set NativeTextReady = true when all separate NativeWords are downloaded, same for ForeignWords 
      public List<string> ForeignWords;
    }
    // TODO: review the idea: 
    // 1) module builder page sends an ajax call to get TTS for one line at a time, 
    // when native text and all its words are downloaded, the call returns, and page updates UI to show play icon on corresponding line
    // same for foreign and explanation
    // 2) module builder page will start a loop, when a txt file is loaded: 
    // for every line it will detect lang and mark the line red if detected lang does not correspond the line's position (1st: native, 2nd: foreign, 3rd: native),
    // 3) if auto-correct button is pressed, the page will place lines into native/foreign position accordingly, 
    // 4) if start TTS button is pressed, the page will do step 1) to get TTS for every line

    #region
    //public static Phrase[] phrases = new Phrase[0];
    //public static string CurrentlyTTSProcessedModule = "";
    //public static int TotalPhrasesToTTSProcess = 0;
    //public static int NumberOfPhrasesTTSProcessed = 0;
    //public static bool IsTTSRunning = false;
    //public static bool IsTTSComplete = false;
    //public static bool IsTTSStarted = false;

    ///// <summary>Async download of TTS from google. May be used only to restore lost sound files, otherwise it is recommended to run the loop on the client side.</summary>
    ///// <param name="googleCookie">protection cookie</param>
    ///// <param name="ttsPath">We dont have access to Server here, so we have to pass Server.MapPath("~/Content/TTS/"+lang)</param>
    //public static async void RunTTS(string googleCookie, string ttsPath, string nativeLangCode, string foreignLangCode)
    //{
    //  await Task.Run(() =>
    //  {
    //    //TODO: foreach phrase tries to find a file in "~/Content/TTS/xx", where xx is lang, if not found, get it from google
    //    TotalPhrasesToTTSProcess = phrases.Count();
    //    foreach (var phrase in phrases)
    //    {
    //      var foreignPath = Path.Combine(ttsPath, foreignLangCode, phrase.ForeignText + ".wav");
    //      if (!System.IO.File.Exists(foreignPath))
    //      {
    //        byte[] data = DownloadVoice(foreignPath, foreignLangCode);
    //        if (data.Length > 0)
    //        {
    //          System.IO.File.WriteAllBytes(foreignPath, data);
    //        }
    //      }
    //      var nativePath = Path.Combine(ttsPath, nativeLangCode, phrase.NativeText + ".wav");
    //      if (!System.IO.File.Exists(nativePath))
    //      {
    //        byte[] data = DownloadVoice(nativePath, foreignLangCode);
    //        if (data.Length > 0)
    //        {
    //          System.IO.File.WriteAllBytes(nativePath, data);
    //        }
    //      }
    //      //TODO: native and foreign words
    //      phrase.NativeWords.ForEach(w =>
    //      {
    //        nativePath = Path.Combine(ttsPath, nativeLangCode, w + ".wav");
    //        if (!System.IO.File.Exists(nativePath))
    //        {
    //          byte[] data = DownloadVoice(nativePath, foreignLangCode);
    //          if (data.Length > 0)
    //          {
    //            System.IO.File.WriteAllBytes(nativePath, data);
    //          }
    //        }
    //      });
    //      phrase.ForeignWords.ForEach(w =>
    //      {
    //        foreignPath = Path.Combine(ttsPath, nativeLangCode, w + ".wav");
    //        if (!System.IO.File.Exists(nativePath))
    //        {
    //          byte[] data = DownloadVoice(nativePath, foreignLangCode);
    //          if (data.Length > 0)
    //          {
    //            System.IO.File.WriteAllBytes(nativePath, data);
    //          }
    //        }
    //      });
    //      NumberOfPhrasesTTSProcessed++;
    //    }
    //  });
    //}
    #endregion


    //TODO: first check our own location, then  http://soundoftext.com/static/sounds/es/ , then a few more fallback sites
    public static byte[] DownloadVoice(string text, string lang)
    {
      string GoogleTranslateUrl = "http://translate.google.com/translate_tts?tl={0}&q={1}"; //TODO: text length limit = 260, bc we store phrases as file names
      if (String.IsNullOrWhiteSpace(text) || String.IsNullOrWhiteSpace(lang))
        return new byte[0];
      string uri = String.Format(GoogleTranslateUrl, lang, text);
      using (WebClient wc = new WebClient())
      {
        wc.Headers[HttpRequestHeader.UserAgent] = "Mozilla/5.0 (Windows NT 6.2; WOW64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/35.0.1916.114 Safari/537.36";
        wc.Headers[HttpRequestHeader.Cookie] = "SID=DQAAAAgBAAC1kezm3GFmR37qgswR0kxSxxinL36HSEfOptM8ANkd79RQO65R_xgCZyMgMogUK5th2Hfq7K2WWuzpDCqQqRvlW7Fm0pamjZClf3AStZsoYN_hljSwjxAJt5238uqFksyi0fq82Ak4wyxNa_NPj7RvE0uu2DPzErU5MpeJ6C2LB6hHfuan7Xe8J8FXW1r9cs5Pqc19oYEF4I6y2PKSmfnVaC7kqU0ZMv2r82RvLPtzaK85f4mIhXBvZEc_wjuZFaEBV623s2vgfCRqXlRkVKrfaJDx0xPjp-8yPF3ObpLwX0eQFojiDc03_I1ce44kSjCNFXqzl4NUTv_NEc3Z3f5KiuHZlKuhN8IzMOy8aB-deQ; HSID=AFgSPJOot-k4NT9rU; APISID=b1v0Sk4zanwWpSAF/A0kQ5Irytk1r89GYm; __utmx=173272373.3AnMnZNSR6iJ8hF_-Ec65A$52618439-14:; __utmxx=173272373.3AnMnZNSR6iJ8hF_-Ec65A$52618439-14:1433708893:15552000; PREF=ID=1111111111111111:FF=0:LD=en:TM=1433708714:LM=1433708714:GM=1:S=nqQ53F3ZUsOoUm1A; _ga=GA1.3.645219687.1433712944; NID=70=XrDd61kONP0YnZL7Dw8kp7QqfRKNNtK2djjWImSHr-Aabdnr5_sYHem_mw0n7Yo3fRrCBnXYzN0ce1X8ms4vo4MZki9iegH2A79fiQH0PzrXFICkMyi0u_1_bTWBA3bOoR-5hZ87UfZqANuLXOyfK_np_lOSBgHE6x7y3oI; GOOGLE_ABUSE_EXEMPTION=ID=3fa4a76697d6e3dc:TM=1438456085:C=c:IP=209.195.86.104-:S=APGng0u4KAqxh7Li7LsA5uCIxGS2kGiimg";
        return wc.DownloadData(uri);
      }
    }

    //		public button2 = "
    //<form target="paypal" action="https://www.paypal.com/cgi-bin/webscr" method="post">
    //<input type="hidden" name="cmd" value="_s-xclick">
    //<input type="hidden" name="hosted_button_id" value="NW24SPMMZTRFS">
    //<input type="image" src="https://www.paypalobjects.com/en_US/i/btn/btn_cart_LG.gif" border="0" name="submit" alt="PayPal - The safer, easier way to pay online!">
    //<img alt="" border="0" src="https://www.paypalobjects.com/en_US/i/scr/pixel.gif" width="1" height="1">
    //</form>
    //		";
    /*	 abcd_16
  <form target="paypal" action="https://www.paypal.com/cgi-bin/webscr" method="post">
  <input type="hidden" name="cmd" value="_s-xclick">
  <input type="hidden" name="hosted_button_id" value="NW24SPMMZTRFS">
  <input type="image" src="https://www.paypalobjects.com/en_US/i/btn/btn_cart_LG.gif" border="0" name="submit" alt="PayPal - The safer, easier way to pay online!">
  <img alt="" border="0" src="https://www.paypalobjects.com/en_US/i/scr/pixel.gif" width="1" height="1">
  </form>
     * name2_13
<form target="paypal" action="https://www.paypal.com/cgi-bin/webscr" method="post">
<input type="hidden" name="cmd" value="_s-xclick">
<input type="hidden" name="hosted_button_id" value="5MWX7MV6ZKDBC">
<input type="image" src="https://www.paypalobjects.com/en_US/i/btn/btn_cart_LG.gif" border="0" name="submit" alt="PayPal - The safer, easier way to pay online!">
<img alt="" border="0" src="https://www.paypalobjects.com/en_US/i/scr/pixel.gif" width="1" height="1">
</form>
      */

    // GET: /Module/
    public ActionResult Index(string nativeLang, string foreignLang, string orderBy)
    {
      var modules = db.Modules.AsQueryable();
      if (!string.IsNullOrEmpty(nativeLang) && nativeLang != "-?-") //if native is not specified, show modules where native lang is any
        modules = modules.Where(o => o.NativeLang == nativeLang);
      if (!string.IsNullOrEmpty(foreignLang) && foreignLang != "-?-") //if foreign is not specified, show modules where foreign lang is any
        modules = modules.Where(o => o.ForeignLang == foreignLang);

      switch (orderBy)
      {
        case "Name (a-z)": modules = modules.OrderBy(o => o.Name); break;
        case "Name (z-a)": modules = modules.OrderByDescending(o => o.Name); break;
        case "Price (hi-low)": modules = modules.OrderByDescending(o => o.Price); break;
        case "Price (low-hi)": modules = modules.OrderBy(o => o.Price); break;
        case "Popular first": modules = modules.OrderByDescending(o => o.SoldNumber); break;
        case "Popular last": modules = modules.OrderBy(o => o.SoldNumber); break;
        default://Newest first
          modules = modules.OrderByDescending(o => o.Id); break;
      }
      //if not logged in, show only modules that are ready for download
      if (!User.Identity.IsAuthenticated)
        modules = modules.Where(o => o.Locked);

      //TODO: apply last moment filters here

      //add Text preview
      foreach (var m in modules)
      {
        //cut off the unique suffix from Name
        m.Name = m.Name.Substring(0, m.Name.LastIndexOf('_'));
        if (!String.IsNullOrWhiteSpace(m.Text))
          m.Text = m.Text.Substring(0, Math.Min(m.Text.Length, 100)) + "...";
      }

      var curUser = db.AspNetUsers.FirstOrDefault(o => o.UserName == User.Identity.Name);

      IEnumerable<Lang> l = db.Langs;
      var vm = new IndexViewModel()
      {
        IsAdmin = curUser == null ? false : curUser.IsAdmin == true,
        ForeignLangList = new SelectList(l, "LangCode", "LangName"),
        NativeLangList = new SelectList(l, "LangCode", "LangName"),
        ForeignLang = foreignLang,
        NativeLang = nativeLang,
        ModuleList = modules.ToList()
      };
      return View(vm);
    }

    // GET: /Module/Details/5
    public ActionResult Details(int? id)
    {
      if (id == null)
      {
        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
      }
      Module module = db.Modules.Find(id);
      if (module == null)
      {
        return HttpNotFound();
      }
      var curUser = db.AspNetUsers.FirstOrDefault(o => o.UserName == User.Identity.Name);
      bool isAdmin = curUser == null ? false : curUser.IsAdmin == true;
      string filePath = Directory.GetFiles(Server.MapPath("~/Content/Upload"), module.Id + ".*").FirstOrDefault();

      var vm = new CreateViewModel()
      {
        Id = module.Id,
        IsAdmin = isAdmin,
        Name = module.Name,
        ForeignName = module.ForeignName,
        Description = module.Description,
        ForeignDescription = module.ForeignDescription,
        ForeignLang = module.ForeignLang,
        NativeLang = module.NativeLang,
        Price = module.Price,
        Text = module.Text,
        SoldNumber = isAdmin ? module.SoldNumber : 0,
        UserName = isAdmin ? module.AspNetUser.UserName : null,
        PaypalButtonId = module.PaypalButtonId,
        ImagePath = filePath != null ? "../../Content/Upload/" + Path.GetFileName(filePath) : "../../Content/Upload/lds-audio-book.jpeg"
      };

      #region
      //Locked modules have all tts ready, dont parse them;
      //if unLocked, and (admin or owner), then for each phrase in Text try to find tts file incrementing NumberOfPhrasesTTSProcessed. Prepare TTSStatus
      //test cases: 1) never started, 2) web service restarted, 3) running this module, 4) running different module, 5) completed, 6) completed and restarted web service
      //if (!module.Locked && isAdmin)
      //{
      //  string moduleName = String.IsNullOrWhiteSpace(CurrentlyTTSProcessedModule) ? module.Name : CurrentlyTTSProcessedModule;
      //  if (!IsTTSStarted && !IsTTSComplete) //never started, or web service was restarted (even if it was completed before)
      //  {
      //    //TODO: when populating phrases, save nativeText, foreignText, expl, string[] foreignWords, nativeLangCode, foreignLangCode
      //    //TODO: validation, check if mp3 exists, detect lang, populate array of Phrase (we only need text and lang for each line, skip link)
      //    //TODO: public class Phrase { NativeText, ForeignText, Expl, WebLinkTitle, WebLink }; get native and foreign langs from module.NativeLang, ForeignLang
      //    phrases = module.Text == null ? new string[0] : module.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None).Select(t => new Phrase() { Text = t, LangCode = });
      //    vm.TTSStatus = "Press Start TTS to process " + moduleName + " module.";
      //  }
      //  foreach (string phrase in phrases)
      //  {
      //    var path = Path.Combine(Server.MapPath("~/Content/TTS"), phrase + ".wav");    //TODO: /TTS/ + lang
      //    if (System.IO.File.Exists(path)) NumberOfPhrasesTTSProcessed++;
      //  }
      //  if (IsTTSStarted && !IsTTSComplete) //started, not yet completed (selected or different module)
      //  {
      //    vm.TTSStatus = "Processing module: " + moduleName +
      //      ". Completed " + NumberOfPhrasesTTSProcessed + " / " + phrases.Count() +
      //      " Status: " + (IsTTSRunning ? "running" : "interrupted");
      //  }
      //  if (IsTTSStarted && IsTTSComplete) //started, completed
      //  {
      //    vm.TTSStatus = "Completed module: " + moduleName +
      //      ". " + NumberOfPhrasesTTSProcessed + " / " + phrases.Count();
      //  }
      //  vm.IsTTSStarted = IsTTSStarted; //used in the view to disable "Start TTS" button
      //}

      //private List<Phrase> LoadPhrases(string text)
      //{
      //  List<Phrase> list = new List<Phrase>();
      //  var phrases = text == null ? new string[0] : text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
      //  if (phrases.Count() == 0) return list;
      //  for (int i = 0; i < phrases.Count() / 5; i += 5)
      //  {
      //    Phrase p = new Phrase()
      //    {
      //      NativeText = phrases[i],
      //      ForeignText = phrases[i + 1],
      //      Explanation = phrases[i + 2],
      //      NativeWords = phrases[i] == null ? new List<string>() : (phrases[i] + " " + phrases[i + 2]).Split(new string[] { " ", "\t", "," }, StringSplitOptions.None).Where(w => w.Length > 0).ToList(),
      //      ForeignWords = phrases[i + 1] == null ? new List<string>() : phrases[i + 1].Split(new string[] { " ", "\t", "," }, StringSplitOptions.None).Where(w => w.Length > 0).ToList()
      //    };
      //    list.Add(p);
      //  }
      //  return list;
      //}
      #endregion

      if (!String.IsNullOrWhiteSpace(vm.Text)) //truncate text for details view, after working on phrases and TTS status!
        vm.Text = vm.Text.Substring(0, Math.Min(vm.Text.Length, 100)) + "...";
      vm.Name = vm.Name.Substring(0, vm.Name.LastIndexOf('_')); //cut off the unique suffix

      return View(vm);
    }

    // GET: /Module/Create
    [Authorize]
    public ActionResult Create()
    {
      IEnumerable<Lang> l = db.Langs.Where(o => o.LangCode != "-?-");
      var vm = new CreateViewModel()
      {
        ForeignLangList = new SelectList(l, "LangCode", "LangName"),
        NativeLangList = new SelectList(l, "LangCode", "LangName"),
        TransUILabels = transUILabelsTemplate
      };
      return View(vm);
    }

    // POST: /Module/Create
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public ActionResult Create([Bind(Include = "Id,Name,Description,Price,NativeLang,ForeignLang,Text", Exclude = "SoldNumber,UserId")] CreateViewModel vm, HttpPostedFileBase file)
    {
      if (User.Identity.Name == null) return RedirectToAction("Index");//must be logged in to create/edit/delete
      if (ModelState.IsValid)
      {
        Module m = new Module()
        {
          UserId = User.Identity.GetUserId(),
          Name = vm.Name,
          ForeignName = vm.ForeignName,
          Description = vm.Description,
          ForeignDescription = vm.ForeignDescription,
          ForeignLang = vm.ForeignLang,
          NativeLang = vm.NativeLang,
          Price = vm.Price,
          Text = vm.Text,
          NewNativeLangCode = vm.NewNativeLangCode,
          NewNativeLangName = vm.NewNativeLangName,
          NewForeignLangCode = vm.NewForeignLangCode,
          NewForeignLangName = vm.NewForeignLangName,
          TransUILabels = vm.TransUILabels
        };
        db.Modules.Add(m);
        db.SaveChanges();
        m.Name += "_" + m.Id; //append unique suffix
        m.ImageFileName = SaveFile(m.Id, file);
        db.SaveChanges();
        return RedirectToAction("Index");
      }
      return View(vm);
    }

    // GET: /Module/Edit/5
    [Authorize]
    public ActionResult Edit(int? id)
    {
      if (id == null)
        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
      Module m = db.Modules.Find(id);
      if (m == null)
        return HttpNotFound();

      //check security
      var curUser = db.AspNetUsers.FirstOrDefault(o => o.UserName == User.Identity.Name);
      bool isAdmin = curUser == null ? false : curUser.IsAdmin == true;
      if (!isAdmin)
      {
        if (m.UserId != User.Identity.GetUserId())
          TempData["warning"] = "You must be creator of this module to edit it.";
        if (m.Locked)
          TempData["warning"] = "This module is locked and cannot be edited or deleted.";
        if (TempData["warning"] != null)
          return RedirectToAction("Details", new { id = id });
      }
      IEnumerable<Lang> l = db.Langs.Where(o => o.LangCode != "-?-");
      string filePath = Directory.GetFiles(Server.MapPath("~/Content/Upload"), m.Id + ".*").FirstOrDefault();
      var vm = new CreateViewModel()
      {
        Id = m.Id,
        IsAdmin = isAdmin,
        ForeignLangList = new SelectList(l, "LangCode", "LangName"),
        NativeLangList = new SelectList(l, "LangCode", "LangName"),
        Name = m.Name.Substring(0, m.Name.LastIndexOf('_')),//cut off the unique suffix
        ForeignName = m.ForeignName,
        Description = m.Description,
        ForeignDescription = m.ForeignDescription,
        ForeignLang = m.ForeignLang,
        NativeLang = m.NativeLang,
        Price = m.Price,
        Text = m.Text,
        Locked = m.Locked,
        ImagePath = filePath != null ? "../../Content/Upload/" + Path.GetFileName(filePath) : null,
        NewNativeLangCode = m.NewNativeLangCode,
        NewNativeLangName = m.NewNativeLangName,
        NewForeignLangCode = m.NewForeignLangCode,
        NewForeignLangName = m.NewForeignLangName,
        TransUILabels = m.TransUILabels ?? transUILabelsTemplate //if module have some lang defined, put it here, otherwise, put a template
      };
      return View(vm);
    }

    // POST: /Module/Edit/5
    // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
    // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public ActionResult Edit([Bind(Include = "Id,Name,ForeignName,Description,ForeignDescription,Price,NativeLang,ForeignLang,Text,NewNativeLangCode,NewNativeLangName,NewForeignLangCode,NewForeignLangName,TransUILabels", Exclude = "SoldNumber,UserId")] CreateViewModel vm, HttpPostedFileBase file)
    {
      Module m = db.Modules.Find(vm.Id);
      if (m == null)
        return HttpNotFound();

      //check security
      var curUser = db.AspNetUsers.FirstOrDefault(o => o.UserName == User.Identity.Name);
      bool isAdmin = curUser == null ? false : curUser.IsAdmin == true;
      if (!isAdmin)
      {
        if (m.UserId != User.Identity.GetUserId())
          TempData["warning"] = "You must be creator of this module to edit/delete it.";
        if (m.Locked)
          TempData["warning"] = "This module is locked and cannot be edited or deleted.";
        if (TempData["warning"] != null)
          return RedirectToAction("Details", new { id = m.Id });
      }
      if (ModelState.IsValid) //save module details
      {
        m.ImageFileName = SaveFile(m.Id, file);
        m.NewNativeLangCode = vm.NewNativeLangCode; //new lang and TransUI are optional, when module is approved for locking, admin decides if they are correct and adds new lang to lang table
        m.NewNativeLangName = vm.NewNativeLangName;
        m.NewForeignLangCode = vm.NewForeignLangCode;
        m.NewForeignLangName = vm.NewForeignLangName;
        m.TransUILabels = vm.TransUILabels;
        m.Name = vm.Name += "_" + vm.Id; //append unique suffix
        m.ForeignName = vm.ForeignName;
        m.Description = vm.Description;
        m.ForeignDescription = vm.ForeignDescription;
        m.ForeignLang = vm.ForeignLang;
        m.NativeLang = vm.NativeLang;
        m.Price = vm.Price;
        m.Text = vm.Text;    //TODO: remove all non-alfanumeric chars 
        m.Locked = vm.Locked;
        db.SaveChanges();
        return RedirectToAction("Index"); //on success return to index
      }
      return View(vm);
    }

    [HttpPost]
    [Authorize]
    public JsonResult SaveModuleText(string param)
    {
      var o = JObject.Parse(param);
      //Console.WriteLine(s);
      return Json("SaveModuleText");
    }






    //[HttpPost]
    //public ActionResult StartTTS([Bind(Include = "Id,Name,GoogleTTSCookie", Exclude = "SoldNumber,UserId")] CreateViewModel vm)
    //{
    //  if (!IsTTSStarted)
    //  {
    //    Module module = db.Modules.Find(vm.Id);
    //    if (module == null)
    //    {
    //      return HttpNotFound();
    //    }
    //    var curUser = db.AspNetUsers.FirstOrDefault(o => o.UserName == User.Identity.Name);
    //    bool isAdmin = curUser == null ? false : curUser.IsAdmin == true;
    //    if (phrases.Count() == 0) //usually it will be already populated by Details action, but just in case...
    //    {
    //      //TODO: when populating phrases, specify lang for each phrase!
    //      phrases = module.Text == null ? new string[0] : module.Text.Split(new string[] { "\r\n", "\n" }, StringSplitOptions.None);
    //    }
    //    if (!module.Locked && isAdmin && phrases.Count() > 0)
    //    {
    //      RunTTS(vm.GoogleTTSCookie, Server.MapPath("~/Content/TTS/"));
    //    }
    //  }
    //  return RedirectToAction("Details", new { id = vm.Id });
    //}

    //public ActionResult RunTTS(int? id, string GoogleTTSCookie)
    //{
    //  if (id == null)
    //    return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
    //  Module m = db.Modules.Find(id);
    //  if (m == null)
    //    return HttpNotFound();

    //  //check security
    //  var curUser = db.AspNetUsers.FirstOrDefault(o => o.UserName == User.Identity.Name);
    //  bool isAdmin = curUser == null ? false : curUser.IsAdmin == true;
    //  if (!isAdmin)
    //  {
    //    if (m.UserId != User.Identity.GetUserId())
    //      TempData["warning"] = "You must be admin to run TTS.";
    //    if (m.Locked)
    //      TempData["warning"] = "This module is locked and cannot be edited or deleted.";
    //    if (TempData["warning"] != null)
    //      return RedirectToAction("Details", new { id = id });
    //  }
    //  //TODO: get value from GoogleTTSCookie, 
    //  //add a static string CurrentlyProcessedModule; - name of the module, we are currently getting tts for
    //  //static int TotalPhrasesToProcess; - number of phrases in the CurrentlyProcessedModule
    //  //static int NumberOfPhrasesProcessed;
    //  //start a Task thread that will read module text, fill in TotalPhrasesToProcess, NumberOfPhrasesProcessed, and send requests to google to translate each phrase, 
    //  //save TTS files into Content/TTS, give names exactly as phrase text
    //  //if google blocks requests, then set GoogleTTSCookie == "enter new cookie", and stop requesting
    //  //
    //  //StartTTS(googleCookie)

    //  return RedirectToAction("Details", new { id = id });
    //}

    public string SaveFile(int moduleId, HttpPostedFileBase file)
    {
      string fileName = null;
      if (file != null && file.ContentLength > 0)
      {
        int MaxContentLength = 1024 * 1024 * 3; //3 MB
        string[] AllowedFileExtensions = new string[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".tif" };

        if (!AllowedFileExtensions.Contains(file.FileName.Substring(file.FileName.LastIndexOf('.')))) //validate extension
          ModelState.AddModelError(string.Empty, "Please file of type: " + string.Join(", ", AllowedFileExtensions));
        else if (file.ContentLength > MaxContentLength) //validate size
          ModelState.AddModelError(string.Empty, "Your file is too large, maximum allowed size is: " + MaxContentLength + " MB");
        else
        {
          fileName = moduleId.ToString() + Path.GetExtension(file.FileName);
          var path = Path.Combine(Server.MapPath("~/Content/Upload"), fileName);
          file.SaveAs(path);
          ModelState.Clear();
        }
      }
      return fileName;
    }

    // GET: /Module/Delete/5
    [Authorize]
    public ActionResult Delete(int? id)
    {
      if (id == null)
        return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
      Module module = db.Modules.Find(id);
      if (module == null)
        return HttpNotFound();

      //check security
      var curUser = db.AspNetUsers.FirstOrDefault(o => o.UserName == User.Identity.Name);
      bool isAdmin = curUser == null ? false : curUser.IsAdmin == true;
      if (!isAdmin)
      {
        if (module.UserId != User.Identity.GetUserId())
          TempData["warning"] = "You must be creator of this module to edit/delete it.";
        if (module.Locked)
          TempData["warning"] = "This module is locked and cannot be edited or deleted.";
        if (TempData["warning"] != null)
          return RedirectToAction("Details", new { id = module.Id });
      }
      return View(module);
    }

    // POST: /Module/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize]
    public ActionResult DeleteConfirmed(int id)
    {
      Module module = db.Modules.Find(id);

      //check security
      var curUser = db.AspNetUsers.FirstOrDefault(o => o.UserName == User.Identity.Name);
      bool isAdmin = curUser == null ? false : curUser.IsAdmin == true;
      if (!isAdmin)
      {
        if (module.UserId != User.Identity.GetUserId())
          TempData["warning"] = "You must be creator of this module to edit it.";
        if (module.Locked)
          TempData["warning"] = "This module is locked and cannot be edited or deleted.";
        if (TempData["warning"] != null)
          return RedirectToAction("Details", new { id = module.Id });
      }
      //delete uploaded image file and module
      var path = Path.Combine(Server.MapPath("~/Content/Upload"), module.ImageFileName);
      System.IO.File.Delete(path);
      db.Modules.Remove(module);
      db.SaveChanges();
      return RedirectToAction("Index");
    }

    // GET: /Module/Download/
    /// <summary>
    /// User goes to market page on the mobile app, pays for module, gets activation code by email, then goes to modules on device, he just enters the code, 
    /// the app sends request (code+deviceID) to webapp, that finds Device2Module record with the code, checks it is not used for another deviceID already 
    /// and updates the record {moduleID,deviceID,code,activationDate}, sends back serialized module data.
    /// </summary>
    /// <param name="deviceId">Not db ID! It is a long string, uniquely identifying a device.</param>
    /// <param name="actCode">Activation code.</param>
    /// <returns>serialized module data, see ModuleData class.</returns>
    [HttpGet]
    public string Download(string deviceId, string actCode)
    {
      var d = db.Devices.FirstOrDefault(o => o.UDID == deviceId);
      var d2m = d == null ? db.Device2Module.FirstOrDefault(o =>
        o.ActivationCode == actCode && //someone paid for the module
        o.DeviceId == null) //nobody uses it yet
        :
        db.Device2Module.FirstOrDefault(o =>
        o.ActivationCode == actCode && //someone paid for the module
        (o.DeviceId == d.Id || o.DeviceId == null) //this device already uses the module or nobody uses it yet
        );
      if (d2m == null) return null;//we are not allowed to take the module
      if (d == null) //register our device, if new
      {
        d = new Device() { UDID = deviceId };
        db.Devices.Add(d);
        db.SaveChanges();
      }
      d2m.DeviceId = d.Id; //update device2module registration
      if (d2m.ActivationDate == null) d2m.ActivationDate = DateTime.UtcNow;
      db.SaveChanges();
      ModuleData result = new ModuleData()
      {
        Id = d2m.ModuleId,
        Name = d2m.Module.Name,
        ForeignName = d2m.Module.ForeignName,
        Description = d2m.Module.Description,
        ForeignDescription = d2m.Module.ForeignDescription,
        FileName = d2m.Module.NativeLang + "_" + d2m.Module.ForeignLang + "_" + d2m.Module.Name + ".txt",
        ImageFileName = d2m.Module.ImageFileName,
        Text = d2m.Module.Text,
        NewNativeLangCode = string.IsNullOrWhiteSpace(d2m.Module.NewNativeLangCode) ? d2m.Module.NativeLang : d2m.Module.NewNativeLangCode, //this is simpler than extracting lang code from file name
        NewNativeLangName = d2m.Module.NewNativeLangName, //we pass them, so that device can add new langs, instead of sending whole list of langs which user might not need
        NewForeignLangCode = string.IsNullOrWhiteSpace(d2m.Module.NewForeignLangCode) ? d2m.Module.ForeignLang : d2m.Module.NewForeignLangCode, //also, for convenience, if no new lang is used, I pass builtin lang codes here instead of adding extra property, the builtin lang name is known by device
        NewForeignLangName = d2m.Module.NewForeignLangName,
        TransUILabels = PrepareTransUILabels(d2m.Module.TransUILabels, d2m.Module.NewNativeLangCode, d2m.Module.NewForeignLangCode) //prepare so that device just saves it to file
      };
      return SerializeJSon<ModuleData>(result);
    }

    private string PrepareTransUILabels(string text, string nativeLangCode, string foreignLangCode)
    {
      if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(nativeLangCode) || string.IsNullOrWhiteSpace(foreignLangCode)) return null;
      var lines = text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
      var engLines = transUILabelsTemplate.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
      StringBuilder sb = new StringBuilder(text.Length * 3);
      if (nativeLangCode != "en")
      {
        for (int i = 0; i <= lines.Length; i = i + 2)
        {
          sb.AppendLine(engLines[i]);    //Market
          sb.AppendLine(nativeLangCode); //es
          sb.AppendLine(lines[i]);       //Mercado
        }
      }
      for (int i = 0; i <= lines.Length; i = i + 2)
      {
        sb.AppendLine(engLines[i]);    //Market
        sb.AppendLine(nativeLangCode); //ru
        sb.AppendLine(lines[i + 1]);     //Рынок
      }
      return sb.ToString();
    }

    public static string SerializeJSon<T>(T t)
    {
      using (MemoryStream stream = new MemoryStream())
      {
        DataContractJsonSerializer ds = new DataContractJsonSerializer(typeof(T));
        ds.WriteObject(stream, t);
        return Encoding.UTF8.GetString(stream.ToArray());
      }
    }

    #region lang detection
    private static Dictionary<string, List<string>> phrase2lang = new Dictionary<string, List<string>>();
    /// <summary>
    /// detects language(s) the phrases belong to, does it with a batch of lines
    /// </summary>
    /// <param name="q">keys batch: list of short phrases, each phrase is a key</param>
    /// <returns>JSon string: list of languages, that are likely for each line</returns>
    [HttpGet]
    public string DetectLang(List<string> q)
    {
      if (q == null || q.Count == 0) return "";
      // some of the keys already exist in our dictionary, we dont send them to langDetectApi,
      // therefore sequence of returned detections is not aligned with initial keys batch.
      // We have to prepare a batch of keys, that are not yet in our dictionary.
      var newKeysBatch = new List<string>();
      var res = new List<string>();
      var tmp = "";
      var query = "";
      string detectLangFileName = ConfigurationManager.AppSettings["DetectLangFileName"];

      // if first time, try to populate dictionary from file
      if (phrase2lang.Count == 0) // first time load of dict
        try
        {
          using (StreamReader sr = System.IO.File.OpenText(detectLangFileName))
          {
            while ((tmp = sr.ReadLine()) != null)
            {
              var ls = tmp.Split('\t');
              res = ls[1].Split('|').ToList();
              phrase2lang[ls[0]] = res;
            }
          }
        }
        catch { }

      // build a batch of keys that we dont have in our dictionary yet
      q.ForEach(s =>
      {
        if (!phrase2lang.TryGetValue(s, out res))
        {
          newKeysBatch.Add(s);
          // trim all whitespaces, non-alpha chars
          var key = s.Split(new char[] { ' ', '\t', '\r', '\n', ',', '.', '!', '?', '-', '+' }).Where(o => !string.IsNullOrWhiteSpace(o));
          query += $"q[]={string.Join("+", key)}&";
        }
      });

      // get lang detections for new keys online
      if (newKeysBatch.Count > 0)
      {
        var uri = new Uri($"http://ws.detectlanguage.com/0.2/detect?{query}key=0643422d83ee480a5338712dbf20694a");
        WebClient wc = new WebClient();
        var json = wc.DownloadString(uri);
        res = JObject.Parse(json)["data"]["detections"].Select(o => o[0]["language"].ToString()).ToList();
        // TODO: when detector will recognize >1 langs per key, this should be SelectMany()
      }

      // add new lang detections to dictionary and file
      if (newKeysBatch.Count > 0)
      {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(detectLangFileName));
        using (StreamWriter sw = System.IO.File.AppendText(detectLangFileName))
          for (int i = 0; i < newKeysBatch.Count; i++)
          {
            // TODO: when detector will recognize >1 langs per key, this should be string.Join('|',res[i])
            phrase2lang[newKeysBatch[i]] = new List<string>() { res[i] }; // put lang detection into dictionary
            sw.WriteLine($"{newKeysBatch[i]}\t{res[i]}"); // append result to file
          };
      }

      // build result based on initial keys batch
      return SerializeJSon<List<List<string>>>(q.Select(s => phrase2lang[s]).ToList());
    }
    #endregion 

    // GET: /Module/Verify/
    /// <summary>
    /// User switches to a new module on the mobile app, before downloading voice we check that module is paid for (registered for user's device id, and contains same text as genuine module).
    /// Device app takes foreign line at random position of the module text, we verify that the same text exists on the same position of the module with given moduleID.
    /// This is to prevent users from using stolen texts.
    /// </summary>
    /// <param name="deviceId">Not db ID! It is a long string, uniquely identifying a device.</param>
    /// <param name="moduleId">DB Id of the module.</param>
    /// <param name="position">Position in the module text to check matching word.</param>
    /// <param name="word">The word in module text that should match.</param>
    /// <returns>Text of module.</returns>
    [HttpGet]
    public string Verify(string deviceId, int moduleId, int position, string word)
    {
      var d2m = db.Device2Module.FirstOrDefault(o => o.Device.UDID == deviceId && o.ModuleId == moduleId);
      if (d2m == null) return "";//the device is not allowed to use the module
      string[] lines = d2m.Module.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
      if (word == "" || word != (lines.Length > position * 3 + 1 ? lines[position * 3 + 1] : "")) return "";//module text was corrupted on the device
      return "ok";
    }

    protected override void Dispose(bool disposing)
    {
      if (disposing)
      {
        db.Dispose();
      }
      base.Dispose(disposing);
    }

    #region if new lang definition is present, these are labels for tutor app, passed in ModuleData.TransUILabels
    private string transUILabelsTemplate =
      "Modules" + Environment.NewLine +
      "Módulos" + Environment.NewLine +
      "Market" + Environment.NewLine +
      "Mercado" + Environment.NewLine +
      "Custom" + Environment.NewLine +
      "Costumbre" + Environment.NewLine +
      "Play Native ON" + Environment.NewLine +
      "Jugar Nativo 1" + Environment.NewLine +
      "Play Native OFF" + Environment.NewLine +
      "Jugar Nativo 0" + Environment.NewLine +
      "Play Native First" + Environment.NewLine +
      "Jugar Nativo Primero" + Environment.NewLine +
      "Play Foreign First" + Environment.NewLine +
      "Jugar Extranjero Primero" + Environment.NewLine +
      "Review in the end ON" + Environment.NewLine +
      "Revisar al final 1" + Environment.NewLine +
      "Review in the end OFF" + Environment.NewLine +
      "Revisar al final 0" + Environment.NewLine +
      "Speak Slowly" + Environment.NewLine +
      "Hablar despacio" + Environment.NewLine +
      "Speak Fluently" + Environment.NewLine +
      "Hablar rápido" + Environment.NewLine +
      "Pause for " + Environment.NewLine +
      "Pausa de " + Environment.NewLine +
      " sec" + Environment.NewLine +
      " segundos" + Environment.NewLine +
      "Play" + Environment.NewLine +
      "Tocar" + Environment.NewLine +
      "Pause" + Environment.NewLine +
      "Pausa" + Environment.NewLine +
      "Mark For Review" + Environment.NewLine +
      "Marca para su Revisión" + Environment.NewLine +
      "Selected foreign: " + Environment.NewLine +
      "Lengua extranjera: " + Environment.NewLine +
      "Selected native: " + Environment.NewLine +
      "Lengua materna: " + Environment.NewLine +
      "Selected Module: " + Environment.NewLine +
      "Módulo: " + Environment.NewLine +
      "Change Module" + Environment.NewLine +
      "Cambiar Módulo" + Environment.NewLine +
      "Preview Module" + Environment.NewLine +
      "Previa Módulo" + Environment.NewLine +
      "Enter code to activate new module:" + Environment.NewLine +
      "Introduzca el código para activar nuevo módulo:" + Environment.NewLine +
      "Activate" + Environment.NewLine +
      "Active" + Environment.NewLine +
      "Activating..." + Environment.NewLine +
      "Activando..." + Environment.NewLine +
      "DONE!" + Environment.NewLine +
      "HECHO!" + Environment.NewLine +
      "invalid code" + Environment.NewLine +
      "código inválido" + Environment.NewLine +
      "the module is pirated or corrupt" + Environment.NewLine +
      "el módulo es pirata o corrupta" + Environment.NewLine +
      "please dont mess with files on your device and be a good customer" + Environment.NewLine +
      "por favor no se metan con los archivos en el dispositivo y ser un buen cliente" + Environment.NewLine +
      "Custom Module" + Environment.NewLine +
      "Módulo Costumbre" + Environment.NewLine +
      "Words and Phrases added by me" + Environment.NewLine +
      "Palabras y frases agregadas por mí" + Environment.NewLine +
      "My new phrase" + Environment.NewLine +
      "Mi frase nueva" + Environment.NewLine +
      "Native: " + Environment.NewLine +
      "Nativo: " + Environment.NewLine +
      "Foreign: " + Environment.NewLine +
      "Extranjero: " + Environment.NewLine +
      "Explanation: " + Environment.NewLine +
      "Explicación: " + Environment.NewLine +
      "Topic: " + Environment.NewLine +
      "Tema: " + Environment.NewLine +
      "Topic URL: " + Environment.NewLine +
      "URL del Tema: " + Environment.NewLine +
      "Insert New" + Environment.NewLine +
      "Inserte Nuevo" + Environment.NewLine +
      "Update" + Environment.NewLine +
      "Actualizar" + Environment.NewLine +
      "Delete" + Environment.NewLine +
      "Eliminar" + Environment.NewLine +
      "Test Play" + Environment.NewLine +
      "Prueba de Juego" + Environment.NewLine +
      "Downloading..." + Environment.NewLine +
      "Descargando..." + Environment.NewLine +
      "Playing..." + Environment.NewLine +
      "Jugando..." + Environment.NewLine;
    #endregion
  }

  /// <summary>When a module is created/edited/viewed, we use this class as a web UI proxy for Module class</summary>
  public class CreateViewModel
  {
    public int Id { get; set; }
    public bool Locked { get; set; }
    public bool IsAdmin { get; set; }
    //[Display(ResourceType = typeof(Resources), Name = "ResourceKeyForSomeProperty")]
    [Required]
    public string Name { get; set; }
    [Required]
    public string ForeignName { get; set; }
    [Required]
    public string Description { get; set; }
    [Required]
    public string ForeignDescription { get; set; }
    [Required]
    public decimal Price { get; set; }
    [Required]
    public string NativeLang { get; set; }
    [Required]
    public string ForeignLang { get; set; }
    public string Text { get; set; }
    public int SoldNumber { get; set; }
    public string ImagePath { get; set; }
    public string UserName { get; set; }
    public string PaypalButtonId { get; set; }
    //[DataType(DataType.Upload)]   may be in future
    //public HttpPostedFileBase file { get; set; }
    public IEnumerable<SelectListItem> ForeignLangList { get; set; }
    public IEnumerable<SelectListItem> NativeLangList { get; set; }
    public string NewNativeLangCode { get; set; }
    public string NewNativeLangName { get; set; }
    public string NewForeignLangCode { get; set; }
    public string NewForeignLangName { get; set; }
    public string TransUILabels { get; set; }

    /// <summary>if we use translate.google.com tts, then we have to pass the cookie TODO: remove?</summary>
    public string GoogleTTSCookie { get; set; }
    /// <summary>Number of phrases converted to sound, total, state, e.g. "14/1000 Running..." or "14/1000 Cookie expired..."  TODO: remove?</summary>
    public string TTSStatus { get; set; }
    public bool IsTTSStarted { get; set; }
  }

  public class IndexViewModel
  {
    public bool IsAdmin { get; set; }
    [Required]
    [Display(Name = "Native Lang")]
    public string NativeLang { get; set; }
    [Required]
    [Display(Name = "Foreign Lang")]
    public string ForeignLang { get; set; }
    public SelectList ForeignLangList { get; set; }
    public SelectList NativeLangList { get; set; }
    public string OrderBy { get; set; }
    public List<Module> ModuleList { get; set; }
  }

  /// <summary>Metadata and text for each module (built-in or downloaded), including UI labels for Tutor app, if custom lang is defined.
  /// When module is downloaded by device, the ModuleData is serialized on web server and deserialized on device. So, keep the class the same in both projects.
  /// </summary>
  public class ModuleData
  {
    public int Id;
    public string Name;
    public string ForeignName;
    public string Description;
    public string ForeignDescription;
    public string FileName;
    public string ImageFileName;
    public string Text;
    public string NewNativeLangCode;
    public string NewNativeLangName;
    public string NewForeignLangCode;
    public string NewForeignLangName;
    public string TransUILabels;
  }

  public class ModuleTextUpdate
  {
    public string ModuleName;
    //public int TotalRowCnt;
    //public List<object> DirtyRows;
  }
}
