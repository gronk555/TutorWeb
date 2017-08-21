using Microsoft.AspNet.Identity;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Owin;

namespace WebApplication4
{
    public partial class Startup
    {
        // For more information on configuring authentication, please visit http://go.microsoft.com/fwlink/?LinkId=301864
        public void ConfigureAuth(IAppBuilder app)
        {
            // Enable the application to use a cookie to store information for the signed in user
            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = DefaultAuthenticationTypes.ApplicationCookie,
                LoginPath = new PathString("/Account/Login")
            });
            // Use a cookie to temporarily store information about a user logging in with a third party login provider
            app.UseExternalSignInCookie(DefaultAuthenticationTypes.ExternalCookie);

            // Uncomment the following lines to enable logging in with third party login providers
            //app.UseMicrosoftAccountAuthentication(
            //  clientId: "0000000040126F31",
            //  clientSecret: "kKbjad9kzDT0M97uHFhc-DURSgeqtz27");

            //app.UseTwitterAuthentication(
            // consumerKey: "c5ACsT9gd5nayMp8Y0q7mBQnN",
            // consumerSecret: "IcUQesKDomGNHkx6J20Q2WxLlh3lkYwWdsyU6uu62aAC3f7o0z");

            //app.UseFacebookAuthentication(
            // appId: "1515432395356257",
            // appSecret: "1bf1de1529a931e297e27fbb2818ee50");

            //app.UseGoogleAuthentication();
        }
    }
}