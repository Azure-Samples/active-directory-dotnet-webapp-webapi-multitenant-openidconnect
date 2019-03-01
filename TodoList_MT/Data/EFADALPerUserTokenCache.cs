/************************************************************************************************
The MIT License (MIT)

Copyright (c) 2015 Microsoft Corporation

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
***********************************************************************************************/

using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Security.Claims;
using System.Web.Security;

namespace TokenCaches.ADALTokenCache.Data
{
    /// <summary>
    /// This is a ADAL's TokenCache implementation for one user. It uses Sql server as a backend store and uses the Entity Framework to read and write to that database.
    /// </summary>
    public class EFADALPerUserTokenCache : TokenCache
    {
        /// <summary>
        /// The EF's DBContext object to be used to read and write from the Sql server database.
        /// </summary>
        private ApplicationDbContext AppDb = new ApplicationDbContext();

        /// <summary>
        /// This keeps the latest copy of the token in memory to save calls to DB, if possible.
        /// </summary>
        private UserTokenCache InMemoryCache;

        /// <summary>
        /// Once the user signes in, this is populated and used for caching queries et al. Contains the App's clientId
        /// </summary>
        internal string SignedInUserUniqueId;

        public EFADALPerUserTokenCache()
        {
            this.Initialize(GetSignedInUsersUniqueId());
        }

        /// <summary>
        /// Parametrized constructor
        /// </summary>
        /// <param name="signedInUserId">the Azure AD objectId of the signed-in user.</param>
        public EFADALPerUserTokenCache(string signedInUserId)
        {
            this.Initialize(signedInUserId);
        }

        /// <summary>
        /// Clears the TokenCache's copy and the database copy of this user's cache.
        /// </summary>
        public override void Clear()
        {
            base.Clear();

            var cacheEntries = this.AppDb.UserTokenCache.Where(c => c.WebUserUniqueId == this.SignedInUserUniqueId);
            this.AppDb.UserTokenCache.RemoveRange(cacheEntries);
            this.AppDb.SaveChanges();
        }

        /// <summary>
        /// Raised AFTER ADAL added the new token in its in-memory copy of the cache.
        /// This notification is called every time ADAL accessed the cache, not just when a write took place:
        /// If ADAL's current operation resulted in a cache change, the property HasStateChanged will be set to true.
        /// If that is the case, we call Serialize() to get a binary blob representing the latest cache content – and persist it.
        /// ADAL NEVER automatically resets HasStateChanged to false. You do it once you are satisfied that you handled the event correctly.
        /// </summary>
        /// <param name="args">Contains parameters used by the ADAL call accessing the cache.</param>
        private void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            this.SetSignedInUsersUniqueIdFromNotificationArgs(args);

            // if state changed, i.e. new token obtained
            if (base.HasStateChanged && !string.IsNullOrWhiteSpace(this.SignedInUserUniqueId))
            {
                if (this.InMemoryCache == null)
                {
                    this.InMemoryCache = new UserTokenCache
                    {
                        WebUserUniqueId = this.SignedInUserUniqueId
                    };
                }

                this.InMemoryCache.CacheBits = MachineKey.Protect(base.Serialize(), "ADALCache");
                this.InMemoryCache.LastWrite = DateTime.Now;

                try
                {
                    // Update the DB and the lastwrite
                    this.AppDb.Entry(InMemoryCache).State = InMemoryCache.UserTokenCacheId == 0 ? EntityState.Added : EntityState.Modified;
                    this.AppDb.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Record already updated on a different thread, so just read the updated record
                    this.ReadCacheForSignedInUser();
                }

                this.HasStateChanged = false;
            }
        }

        /// <summary>
        /// Right before it reads the cache, a call is made to BeforeAccess notification. Here, you have the opportunity of retrieving your persisted cache blob
        /// from the Sql database. We pick it from the database, save it in the in-memory copy, and pass it to the base class by calling the Deserialize().
        /// </summary>
        /// <param name="args">Contains parameters used by the ADAL call accessing the cache.</param>
        private void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {            
            this.ReadCacheForSignedInUser();
        }

        /// <summary>
        /// If you want to ensure that no concurrent write take place, use this notification to place a lock on the entry.
        /// </summary>
        /// <param name="args">Contains parameters used by the ADAL call accessing the cache.</param>
        private void BeforeWriteNotification(TokenCacheNotificationArgs args)
        {
            this.SetSignedInUsersUniqueIdFromNotificationArgs(args);
            // Since we are using a Rowversion for concurrency, we need not to do anything in this handler.
        }

        private IOrderedQueryable<UserTokenCache> GetLatestUserRecordQuery()
        {
            return this.AppDb.UserTokenCache.Where(c => c.WebUserUniqueId == this.SignedInUserUniqueId).OrderByDescending(d => d.LastWrite);
        }

        /// <summary>
        /// Explores the Claims of a signed-in user (if available) to populate the unique Id of this cache's instance.
        /// </summary>
        /// <returns>The signed in user's object Id , if available in the ClaimsPrincipal.Current instance</returns>
        private string GetSignedInUsersUniqueId()
        {
            return ClaimsPrincipal.Current?.FindFirst("http://schemas.microsoft.com/identity/claims/objectidentifier")?.Value;
        }

        /// <summary>
        /// Initializes the cache instance
        /// </summary>
        /// <param name="signedInUserUniqueId">If the program has it available, then it should pass it themselves.</param>
        private void Initialize(string signedInUserUniqueId)
        {
            this.AfterAccess = AfterAccessNotification;
            this.BeforeAccess = BeforeAccessNotification;
            this.BeforeWrite = BeforeWriteNotification;

            if (string.IsNullOrWhiteSpace(signedInUserUniqueId))
            {
                // No users signed in yet, so we return
                return;
            }

            this.SignedInUserUniqueId = signedInUserUniqueId;
            this.ReadCacheForSignedInUser();
        }

        /// <summary>
        /// Reads the cache data from the backend database.
        /// </summary>
        private void ReadCacheForSignedInUser()
        {
            if (this.InMemoryCache == null) // first time access
            {
                this.InMemoryCache = GetLatestUserRecordQuery().FirstOrDefault();
            }
            else
            {
                // retrieve last written record from the DB
                var lastwriteInDb = GetLatestUserRecordQuery().Select(n => n.LastWrite).FirstOrDefault();

                // if the persisted copy is newer than the in-memory copy
                if (lastwriteInDb > InMemoryCache.LastWrite)
                {
                    // read from from storage, update in-memory copy
                    this.InMemoryCache = GetLatestUserRecordQuery().FirstOrDefault();
                }
            }

            // Send data up the base class
            base.Deserialize((InMemoryCache == null) ? null : MachineKey.Unprotect(InMemoryCache.CacheBits, "ADALCache"));
        }

        /// <summary>
        /// To keep the cache, ClaimsPrincipal and Sql in sync, we ensure that the user's object Id we obtained by ADAL after successful sign-in is set as the key for the cache.
        /// </summary>
        /// <param name="args">Contains parameters used by the ADAL call accessing the cache.</param>
        private void SetSignedInUsersUniqueIdFromNotificationArgs(TokenCacheNotificationArgs args)
        {
            if (string.IsNullOrWhiteSpace(this.SignedInUserUniqueId))
            {
                this.SignedInUserUniqueId = args.UniqueId;
            }
        }
    }

    /// <summary>
    /// Represents a user's token cache entry in database
    /// </summary>
    public class UserTokenCache
    {
        [Key]
        public int UserTokenCacheId { get; set; }

        /// <summary>
        /// The objectId of the signed-in user's object in Azure AD
        /// </summary>
        public string WebUserUniqueId { get; set; }

        public byte[] CacheBits { get; set; }

        public DateTime LastWrite { get; set; }

        /// <summary>
        /// Provided here as a precaution against concurrent updates by multiple threads.
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}