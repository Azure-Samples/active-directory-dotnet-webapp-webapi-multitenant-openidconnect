using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Web;
using System.Web.Mvc;
using TodoListWebApp.DAL;
//using TodoListWebApp.Models;


namespace TodoListWebApp.Controllers
{
    [Authorize]
    public class UserProfileController : Controller
    {
        private TodoListWebAppContext db = new TodoListWebAppContext();

        // GET: UserProfile
        public ActionResult Index()
        {
            string clientId = ConfigurationManager.AppSettings["ida:ClientID"];
            string appKey = ConfigurationManager.AppSettings["ida:Password"];
            string graphResourceID = "https://graph.windows.net";
            GraphSettings graphSettings = new GraphSettings();
            graphSettings.ApiVersion = "2013-11-08";
            graphSettings.GraphDomainName = "graph.windows.net";
            string signedInUserID = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;            
            string tenantID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;
            string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;

            try
            {
                // get a token for the Graph without triggering any user interaction (from the cache, via multi-resource refresh token, etc)
                ClientCredential clientcred = new ClientCredential(clientId, appKey);
                // initialize AuthenticationContext with the token cache of the currently signed in user, as kept in the app's EF DB
                AuthenticationContext authContext = new AuthenticationContext(string.Format("https://login.windows.net/{0}", tenantID), new EFADALTokenCache(signedInUserID));
                AuthenticationResult result = authContext.AcquireTokenSilent(graphResourceID, clientcred, new UserIdentifier(userObjectID,UserIdentifierType.UniqueId));
                
                // use the token for querying the graph
                Guid ClientRequestId = Guid.NewGuid();
                GraphConnection graphConnection = new GraphConnection(result.AccessToken, ClientRequestId, graphSettings);
                User user = graphConnection.Get<User>(userObjectID);

                return View(user);
            }
            // if the above failed, the user needs to explicitly re-authenticate for the app to obtain the required token
            catch (Exception ee)
            {
                ViewBag.ErrorMessage = "AuthorizationRequired";
                return View();
            }
        }
        public void RefreshSession()
        {
            HttpContext.GetOwinContext().Authentication.Challenge(new AuthenticationProperties { RedirectUri = "/UserProfile" }, OpenIdConnectAuthenticationDefaults.AuthenticationType);
        }
    }
}