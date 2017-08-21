using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Configuration;


namespace WebApplication4
{
	public static class Utils
	{
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