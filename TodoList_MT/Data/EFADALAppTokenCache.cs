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
using System.Web.Security;

namespace TokenCaches.ADALTokenCache.Data
{
    /// <summary>
    /// This is a ADAL's TokenCache implementation for an App that obtains tokens for itself.
    /// It uses Sql server as a backend store and uses the Entity Framework to read and write to that database.
    /// </summary>
    public class EFADALAppTokenCache : TokenCache
    {
        /// <summary>
        /// The EF's DBContext object to be used to read and write from the Sql server database.
        /// </summary>
        private ApplicationDbContext AppDb = new ApplicationDbContext();

        /// <summary>
        /// This keeps the latest copy of the token in memory to save calls to DB, if possible.
        /// </summary>
        private AppTokenCache InMemoryCache;

        /// <summary>
        /// Once a app obtains a token, this is populated and used for caching queries et al. Contains the App's AppId/ClientID as obtained from the Azure AD portal
        /// </summary>
        internal string ActiveClientId;

        public EFADALAppTokenCache(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentNullException(nameof(clientId), "The app token cache needs the clientId of the application to instantiate correctly");
            }

            this.AfterAccess = AfterAccessNotification;
            this.BeforeAccess = BeforeAccessNotification;
            this.BeforeWrite = BeforeWriteNotification;

            this.ActiveClientId = clientId;
            this.ReadCacheForSignedInApp();
        }

        /// <summary>
        /// Clears the TokenCache's copy and the database copy of this app's token cache.
        /// </summary>
        public override void Clear()
        {
            base.Clear();
            var cacheEntries = this.AppDb.AppTokenCache.Where(c => c.ClientID == this.ActiveClientId);
            this.AppDb.AppTokenCache.RemoveRange(cacheEntries);
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
            // if state changed, i.e. new token obtained
            if (base.HasStateChanged && !string.IsNullOrWhiteSpace(this.ActiveClientId))
            {
                if (this.InMemoryCache == null)
                {
                    this.InMemoryCache = new AppTokenCache
                    {
                        ClientID = this.ActiveClientId
                    };
                }

                this.InMemoryCache.CacheBits = MachineKey.Protect(base.Serialize(), "ADALCache");
                this.InMemoryCache.LastWrite = DateTime.Now;

                try
                {
                    // Update the DB and the lastwrite
                    this.AppDb.Entry(this.InMemoryCache).State = this.InMemoryCache.AppTokenCacheId == 0 ? EntityState.Added : EntityState.Modified;
                    this.AppDb.SaveChanges();
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Record already updated on a different thread, so just read the updated record
                    this.ReadCacheForSignedInApp();
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
            this.ReadCacheForSignedInApp();
        }

        /// <summary>
        /// if you want to ensure that no concurrent write take place, use this notification to place a lock on the entry
        /// </summary>
        /// <param name="args">Contains parameters used by the ADAL call accessing the cache.</param>
        private void BeforeWriteNotification(TokenCacheNotificationArgs args)
        {
            // Since we are using a Rowversion for concurrency, we need not to do anything in this handler.
        }

        private IOrderedQueryable<AppTokenCache> GetLatestAppRecordQuery()
        {
            return this.AppDb.AppTokenCache.Where(c => c.ClientID == this.ActiveClientId).OrderByDescending(d => d.LastWrite);
        }

        /// <summary>
        /// Reads the cache data from the backend database.
        /// </summary>
        private void ReadCacheForSignedInApp()
        {
            if (this.InMemoryCache == null) // first time access
            {
                this.InMemoryCache = GetLatestAppRecordQuery().FirstOrDefault();
            }
            else
            {
                // retrieve last written record from the DB
                var lastwriteInDb = GetLatestAppRecordQuery().Select(n => n.LastWrite).FirstOrDefault();

                // if the persisted copy is newer than the in-memory copy
                if (lastwriteInDb > InMemoryCache.LastWrite)
                {
                    // read from from storage, update in-memory copy
                    this.InMemoryCache = GetLatestAppRecordQuery().FirstOrDefault();
                }
            }

            // Send data up the base class
            base.Deserialize((InMemoryCache == null) ? null : MachineKey.Unprotect(InMemoryCache.CacheBits, "ADALCache"));
        }
    }

    /// <summary>
    /// Represents an app's token cache entry in database
    /// </summary>
    public class AppTokenCache
    {
        [Key]
        public int AppTokenCacheId { get; set; }

        /// <summary>
        /// The Appid or ClientId of the app
        /// </summary>
        public string ClientID { get; set; }

        public byte[] CacheBits { get; set; }

        public DateTime LastWrite { get; set; }

        /// <summary>
        /// Provided here as a precaution against concurrent updates by multiple threads.
        /// </summary>
        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}