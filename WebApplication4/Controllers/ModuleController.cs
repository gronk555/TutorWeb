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

namespace WebApplication4.Controllers
{
  public class ModuleController : Controller
  {
    private Entities db = new Entities();

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
    public ActionResult Create([Bind(Include = "Id,Name,Description,Price,NativeLang,ForeignLang,Text,ForeignName,ForeignDescription", Exclude = "SoldNumber,UserId")] CreateViewModel vm, HttpPostedFileBase file)
    {
      if (User.Identity.Name == null) return RedirectToAction("Index");//must be logged in to create/edit/delete
      IEnumerable<Lang> l = db.Langs.Where(o => o.LangCode != "-?-");
      vm.ForeignLangList = new SelectList(l, "LangCode", "LangName");
      vm.NativeLangList = new SelectList(l, "LangCode", "LangName");
      vm.TransUILabels = transUILabelsTemplate;
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
        Utils.PopulateModuleCache(m.Id, db); // if module is successfully created, then prepare cache for editor
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
      Utils.PopulateModuleCache(m.Id, db); // if module is open for editing, then prepare cache for editor
      var moduleIds = Session["ModuleIds"] as List<int>;
      if (moduleIds == null)
      {
        moduleIds = new List<int>();
        Session["ModuleIds"] = moduleIds; //when session expires, we'll remove its modules from cache (except those still downloading TTS)
      }
      if (!moduleIds.Contains(m.Id)) moduleIds.Add(m.Id);
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
        m.Locked = vm.Locked;
        db.SaveChanges();
        return RedirectToAction("Index"); //on success return to index
      }
      return View(vm);
    }

    /// <summary>
    /// autosave, called from UI when editing
    /// </summary>
    /// <param name="param">{ ModuleId: int, TotalRowCnt: int, DirtyRows: {iRow: int, value: string, langCode: string}[] }</param>
    /// <returns>empty string</returns>
    [HttpPost]
    [Authorize]
    public JsonResult SaveModuleText(Utils.ModuleChanges param)
    {
      Utils.UpdateModuleCache(param);
      return Json(""); // TODO: return progress of getTTS
    }

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
          var key = s.Split(Utils.WordSeparators).Where(o => !string.IsNullOrWhiteSpace(o));
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

}
