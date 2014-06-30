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
            string signedInUserID = ClaimsPrincipal.Current.FindFirst(ClaimTypes.NameIdentifier).Value;            
            string tenantID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/tenantid").Value;
            string userObjectID = ClaimsPrincipal.Current.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier").Value;
            try
            {
            //bool validTokenPresent = true;
            //TodoListWebApp.Models.TokenCacheEntry tce = null;
            ////get a token using the cached values
            //var existing = db.TokenCache.FirstOrDefault(a => (a.SignedInUser==signedInUserID) && (a.ResourceID == graphResourceID));
            //if(existing!=null) //we have a token cache entry
            //{
            //    tce = existing;
            //    //if the access token is expired
            //    if ( tce.Expiration.DateTime  < DateTime.Now)
            //    {
            //        //use the refresh token to get a fresh set of tokens
            //        try
            //        {
                        ClientCredential clientcred = new ClientCredential(clientId, appKey);
                        AuthenticationContext authContext = new AuthenticationContext(string.Format("https://login.windows.net/{0}", tenantID), new EFADALTokenCache(signedInUserID));
                        //AuthenticationResult result = authContext.AcquireTokenSilent(graphResourceID,clientcred, UserIdentifier.AnyUser);
                        AuthenticationResult result = authContext.AcquireTokenSilent(graphResourceID, clientcred, new UserIdentifier(signedInUserID,UserIdentifierType.UniqueId));

                //        string tempAccessToken = authContext.TokenCache.ReadItems().First().AccessToken;

                       // AuthenticationResult result = authContext.AcquireToken(graphResourceID, clientcred);
                        //AuthenticationResult result = authContext.AcquireTokenByRefreshToken(tce.RefreshToken, clientcred, graphResourceID);
            //            TodoListWebApp.Models.TokenCacheEntry tce2 = new TodoListWebApp.Models.TokenCacheEntry
            //            {
            //                SignedInUser = signedInUserID,
            //                TokenRequestorUser = result.UserInfo.DisplayableId,
            //                ResourceID = graphResourceID,
            //                AccessToken = result.AccessToken,
            //                RefreshToken = result.RefreshToken,
            //                Expiration = result.ExpiresOn.AddMinutes(-5)
            //            };
            //            db.TokenCache.Remove(tce);
            //            db.TokenCache.Add(tce2);
            //            db.SaveChanges();
            //            tce = tce2;
            //        }
            //        catch
            //        {
            //            // the refresh token might be expired
            //            tce = null;
            //        }
            //    }
            //} else // we don't have a cached token
            //{
            //    tce = null;// it's already null, but for good measure...
            //}

           // if (tce != null)
            
               // CallContext currentCallContext = new CallContext { AccessToken = tce.AccessToken, ClientRequestId = Guid.NewGuid(), TenantId = tenantID, ApiVersion = "2013-11-08" };

                // CallContext currentCallContext = new CallContext(tce.AccessToken, Guid.NewGuid(), "2013-11-08");
               
                CallContext currentCallContext = new CallContext(result.AccessToken, Guid.NewGuid(), "2013-11-08");
                //CallContext currentCallContext = new CallContext(tempAccessToken, Guid.NewGuid(), "2013-11-08");

                GraphConnection graphConnection = new GraphConnection(currentCallContext);
                User user = graphConnection.Get<User>(userObjectID);
                return View(user);
            }
            //else
            catch(Exception ee)
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