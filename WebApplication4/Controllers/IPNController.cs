using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using System.Web.Mvc;
using System.Configuration;
using WebApplication4.Models;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Globalization;

namespace WebApplication4.Controllers
{
	public class IPNController : Controller
	{
		private Entities db = new Entities();
		//
		// GET: /IPN/
		[AllowAnonymous]
		public ActionResult Index()
		{
			try
			{
				if (GetPayPalResponse() == "VERIFIED")
				{
					string transactionId = Request["txn_id"];
					string business = HttpUtility.UrlDecode(Request["business"]); //check that receiver is me
					string receiverEmail = HttpUtility.UrlDecode(Request["receiver_email"]);
					string receiverId = HttpUtility.UrlDecode(Request["receiver_id"]);
					bool verified = String.Compare(business, ConfigurationManager.AppSettings["MyBusinessEmail"], true) == 0 ||
						String.Compare(business, ConfigurationManager.AppSettings["ReceiverEmail"], true) == 0 ||
						String.Compare(business, ConfigurationManager.AppSettings["ReceiverId"], true) == 0;
					//string currencyCode = HttpUtility.UrlDecode(Request["mc_currency"]); //probably not needed
					//verified = verified && String.Compare(currencyCode, ConfigurationManager.AppSettings["CurrencyCode"], true) == 0;
					string paymentStatus = HttpUtility.UrlDecode(Request["payment_status"]);
					verified = verified && String.Compare(paymentStatus, "Completed", true) == 0;
					Decimal amountPaid = GetAmountPaid();
					verified = verified && amountPaid != 0;
					//DEBUG:
					verified = true;

					// process the transaction
					if (verified)
					{
						// check that we have not already processed this transaction
						Payment payment = db.Payments.FirstOrDefault(o => o.TransactionId == transactionId);
						if (payment == null)
						{
							DateTime date;
							string format = "HH:mm:ss dd MMM yyyy PST";
							if (!DateTime.TryParseExact(HttpUtility.UrlDecode(Request["payment_date"]), format, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
								date = DateTime.UtcNow;
							payment = new Payment()
							{
								UserName = HttpUtility.UrlDecode(Request["payer_email"]),//possible to use payer_id, but not now
								TransactionId = transactionId,
								Date = date,
								AmountPaid = amountPaid,
								Currency = HttpUtility.UrlDecode(Request["mc_currency"]),
								FirstName = HttpUtility.UrlDecode(Request["first_name"]),
								LastName = HttpUtility.UrlDecode(Request["last_name"]),
								Email = HttpUtility.UrlDecode(Request["payer_email"]),
								StreetAddress = HttpUtility.UrlDecode(Request["address_street"]),
								City = HttpUtility.UrlDecode(Request["address_city"]),
								State = HttpUtility.UrlDecode(Request["address_state"]),
								Country = HttpUtility.UrlDecode(Request["address_country"]),
								Zip = HttpUtility.UrlDecode(Request["address_zip"]),
							};
							db.Payments.Add(payment);
							db.SaveChanges();

							// generate product codes, send them to buyer’s email
							SendProductCodes();
						}
						else
						{
							//if payment is complete already processed (exists in our DB), log error for further investigation
						}
					}
					else
					{
						// check log for IPN details for further investigation as a hack attempt
					}
				}
				else
				{
					// check log for IPN details for further investigation as a hack attempt
				}
			}
			catch (Exception ex)
			{
				Utils.Log(ex.Message); // log IPN details for debugging
			}
			return null;
		}

		/// <summary>Utility method for handling PayPal Responses</summary>
		private string GetPayPalResponse()
		{
			//var formVals = new Dictionary<string, string>();
			//formVals.Add("cmd", "_notify-validate");
			bool useSandbox = Convert.ToBoolean(System.Configuration.ConfigurationManager.AppSettings["UseSandbox"]);
			string paypalUrl = useSandbox ? "https://www.sandbox.paypal.com/cgi-bin/webscr"
			 : "https://www.paypal.com/cgi-bin/webscr";

			HttpWebRequest req = (HttpWebRequest)WebRequest.Create(paypalUrl);
			// Set values for the request back
			req.Method = "POST";
			req.ContentType = "application/x-www-form-urlencoded";
			byte[] param = Request.BinaryRead(Request.ContentLength);
			string strRequest = Encoding.ASCII.GetString(param);

			Utils.Log(strRequest); //examine complex orders, where many types of items are bought

			//StringBuilder sb = new StringBuilder();
			//sb.Append(strRequest);

			//foreach (string key in formVals.Keys)
			//{
			//	sb.AppendFormat("&{0}={1}", key, formVals[key]);
			//}
			//strRequest += sb.ToString();
			strRequest += "&cmd=_notify-validate";
			req.ContentLength = strRequest.Length;

			//for proxy
			//WebProxy proxy = new WebProxy(new Uri("http://urlort#");
			//req.Proxy = proxy;
			//Send the request to PayPal and get the response
			string response = "";
			using (StreamWriter streamOut = new StreamWriter(req.GetRequestStream(), System.Text.Encoding.ASCII))
			{
				streamOut.Write(strRequest);
				streamOut.Close();
				using (StreamReader streamIn = new StreamReader(req.GetResponse().GetResponseStream()))
				{
					response = streamIn.ReadToEnd();
				}
			}
			return response;
		}

		private Decimal GetAmountPaid()
		{
			Decimal amt = 0;
			//TODO: for each item type check DB.ItemPrice <= mc_gross_x / quantityx, if something wrong return 0
			if (Request != null)
			{
				string sAmountPaid = Request["mc_gross"];
				Decimal.TryParse(sAmountPaid, out amt);
			}
			else Utils.Log("empty request");
			return amt;
		}

		/// <summary>
		/// Request contains IPN from Paypal. Each cart item can have its own quantity of instances. For each instance we generate an activation code.
		/// Increment SoldNumber for module. Send email with activation codes to user. 
		/// User goes to market page on the mobile app, no need to select a module, he just enters the code, 
		/// the app sends request (code+deviceID) to webapp, that finds Device2Module record with the code, checks it is not used for another deviceID already 
		/// and updates the record {moduleID,deviceID,code,activationDate}, sends back response stream with module data. See ModuleController.Download()
		/// </summary>
		private void SendProductCodes()
		{
			try
			{
				Utils.Log(ConfigurationManager.AppSettings["EmailBody"]);	//TODO: remove in production
				Utils.Log(ConfigurationManager.AppSettings["EmailFooter"]);
				TEST();

				string mName = HttpUtility.UrlDecode(Request["item_name" + 1]);		//TODO: comment out these 4 lines for production. In debug msvsmon crashes when EF is called, unless these lines are here.
				List<Module> ml = db.Modules.ToList();
				var mm = ml.FirstOrDefault(o => o.Name == mName);
				Utils.Log(mm.Name);

				int n = 0;
				int.TryParse(Request["num_cart_items"], out n);
				if (n <= 0) throw new Exception("num_cart_items == 0 or not present in IPN");
				string[][] actCode = new string[n][];
				string body = ConfigurationManager.AppSettings["EmailBody"];

				for (int x = 0; x < n; x++)
				{
					//for each instance of bought module create a record with act.code; when user activates it, we'll get his device ID bound to the module instance; only this device will be able to download it.
					string moduleName = HttpUtility.UrlDecode(Request["item_name" + (x + 1)]);
					Module m = db.Modules.FirstOrDefault(o => o.Name == moduleName);
					if (m == null) throw new Exception(moduleName + " is not found in DB.");

					//if more than one instance of a module is bought, generate act.codes for each instance
					int quantity = 0;
					int.TryParse(Request["quantity" + (x + 1)], out quantity);
					if (quantity <= 0) throw new Exception("quantity" + (x + 1) + " == 0 or not present in IPN");
					actCode[x] = new string[quantity];
					for (int i = 0; i < quantity; i++)
					{
						actCode[x][i] = RND.GetString(8);
						var d2m = new Device2Module() { ModuleId = m.Id, ActivationCode = actCode[x][i] };
						db.Device2Module.Add(d2m);
						body += moduleName + ": " + actCode[x][i] + "\r\n";
					}
					m.SoldNumber += quantity;
					db.SaveChanges();
				}

				string smtpServer = ConfigurationManager.AppSettings["SMTPServer"];
				int smtpPort = 0;
				int.TryParse(ConfigurationManager.AppSettings["SMTPPort"], out smtpPort);
				if (smtpPort <= 0) throw new Exception("smtpPort == 0 or not present in web.config");
				string emailSubject = ConfigurationManager.AppSettings["EmailSubject"];
				string emailFooter = ConfigurationManager.AppSettings["EmailFooter"];
				string adminEmail = ConfigurationManager.AppSettings["AdminEmail"];
				string adminEmailPassword = ConfigurationManager.AppSettings["AdminEmailPassword"];

				SmtpClient client = new SmtpClient(smtpServer, smtpPort); //587 no ssl
				client.EnableSsl = true;
				client.DeliveryMethod = SmtpDeliveryMethod.Network;
				client.UseDefaultCredentials = false;
				client.Credentials = new NetworkCredential(adminEmail, adminEmailPassword);
				client.Send(adminEmail, Request["payer_email"], emailSubject, body + emailFooter);
			}
			catch (Exception ex)
			{
				Utils.Log("IPN.SendProductCodes: " + ex.Message);
			}
		}

		/// <summary>
		/// to adjust email with activation codes
		/// </summary>
		private void TEST()
		{
			string smtpServer = ConfigurationManager.AppSettings["SMTPServer"];
			int smtpPort = 0;
			int.TryParse(ConfigurationManager.AppSettings["SMTPPort"], out smtpPort);
			if (smtpPort <= 0) throw new Exception("smtpPort == 0 or not present in web.config");
			string emailSubject = ConfigurationManager.AppSettings["EmailSubject"];
			string body = ConfigurationManager.AppSettings["EmailBody"];
			string emailFooter = ConfigurationManager.AppSettings["EmailFooter"];
			string adminEmail = ConfigurationManager.AppSettings["AdminEmail"];
			string adminEmailPassword = ConfigurationManager.AppSettings["AdminEmailPassword"];

			SmtpClient client = new SmtpClient(smtpServer, smtpPort); //587 no ssl
			client.EnableSsl = true;
			client.DeliveryMethod = SmtpDeliveryMethod.Network;
			client.UseDefaultCredentials = false;
			client.Credentials = new NetworkCredential(adminEmail, adminEmailPassword);
			client.Send(adminEmail, "gronk555@gmail.com", emailSubject, body + emailFooter);
		}

		class RND
		{
			static readonly char[] AvailableCharacters = { 'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };

			internal static string GetString(int length)
			{
				char[] result = new char[length];
				byte[] randomData = new byte[length];
				using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
					rng.GetBytes(randomData);
				for (int idx = 0; idx < result.Length; idx++)
				{
					int pos = randomData[idx] % AvailableCharacters.Length;
					result[idx] = AvailableCharacters[pos];
				}
				return new string(result);
			}
		}
	}
}