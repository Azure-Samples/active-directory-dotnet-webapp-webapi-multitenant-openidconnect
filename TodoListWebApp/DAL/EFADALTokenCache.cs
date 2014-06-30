using Microsoft.IdentityModel.Clients.ActiveDirectory;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Web;

namespace TodoListWebApp.DAL
{

    public class PerWebUserCache
    {
        [Key]
        public int EntryId { get; set; }
        public string webUserUniqueId { get; set; }
        public byte[] cacheBits { get; set; }
        public DateTime LastWrite { get; set; }
        public bool WriteLock { get; set; }
    }

    public class EFADALTokenCache: TokenCache
    {
        private TodoListWebAppContext db = new TodoListWebAppContext();
        string User;
        PerWebUserCache Cache;
        
        public EFADALTokenCache(string user)
        {
            User = user;
            // look up the entry in the DB
            this.AfterAccess = AfterAccessNotification;
            this.BeforeAccess = BeforeAccessNotification;
            this.BeforeWrite = BeforeWriteNotification;
            Cache = db.PerUserCacheList.FirstOrDefault(c => c.webUserUniqueId == User);
            this.Deserialize((Cache == null) ? null : Cache.cacheBits);
        }

        void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            if (Cache == null)
            {
                // first time access
                Cache = db.PerUserCacheList.FirstOrDefault(c => c.webUserUniqueId == User);
            }
            else
            {
                var status = from e in db.PerUserCacheList
                             where (e.webUserUniqueId == User)
                             select new
                             {
                                 LastWrite = e.LastWrite,
                                 WriteLock = e.WriteLock
                             };

                // retrieve last write from the DB
                // if there's a lock, wait
                // TBD
                // if it's not in memory OR
                // if it's in memory but older than last write
                if (status.First().LastWrite > Cache.LastWrite)
                //// read from from storage, update last read
                {
                    Cache = db.PerUserCacheList.FirstOrDefault(c => c.webUserUniqueId == User);
                }
            }
            this.Deserialize((Cache == null) ? null : Cache.cacheBits);
        }
        void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if state changed
            if (this.HasStateChanged)
            {
                Cache = new PerWebUserCache
                {
                    webUserUniqueId = User,
                    cacheBits = this.Serialize(),
                    LastWrite = DateTime.Now,
                    WriteLock = false
                };

                //// update the DB and the lastwrite
                //db.PerUserCacheList.Attach(Cache);
                db.Entry(Cache).State = Cache.EntryId == 0 ? EntityState.Added : EntityState.Modified;                
                db.SaveChanges();
                this.HasStateChanged = false;
            }
        }
        void BeforeWriteNotification(TokenCacheNotificationArgs args)
        {
            // put a lock on the entry => set write in progress
        }
    }

    //class AzureBlobTokenCache3 : TokenCache
    //{
    //    private const string CloudStorageAccountSetting = "UseDevelopmentStorage=true";
    //    private const string TokenCacheContainerName = "adal";
    //    private readonly CloudBlobContainer container;
    //    private readonly Random rand = new Random();
    //    private string leaseId = null;
    //    private DateTimeOffset? lastModified = null;
    //    private string lastResource = null;

    //    public AzureBlobTokenCache3()
    //    {
    //        this.AfterAccess = CustomTokenCache_AfterAccess;
    //        this.BeforeAccess = CustomTokenCache_BeforeAccess;
    //        this.BeforeWrite = CustomTokenCache_BeforeWrite;

    //        CloudStorageAccount storageAccount = CloudStorageAccount.Parse(CloudStorageAccountSetting);
    //        CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
    //        this.container = blobClient.GetContainerReference(TokenCacheContainerName);
    //        this.container.CreateIfNotExists();
    //    }

    //    void CustomTokenCache_BeforeAccess(TokenCacheNotificationArgs args)
    //    {            
    //        CloudBlockBlob blockBlob = container.GetBlockBlobReference(args.Resource);
    //        if (blockBlob.Exists() && (lastResource != args.Resource || lastModified != blockBlob.Properties.LastModified))
    //        {
    //            byte[] blob = new byte[blockBlob.Properties.Length];
    //            blockBlob.DownloadToByteArray(blob, 0, AccessCondition.GenerateLeaseCondition(leaseId));
    //            lastResource = args.Resource;
    //            lastModified = blockBlob.Properties.LastModified;
    //        }
    //    }

    //    void CustomTokenCache_BeforeWrite(TokenCacheNotificationArgs args)
    //    {
    //        CloudBlockBlob blockBlob = container.GetBlockBlobReference(args.Resource);
    //        if (blockBlob.Exists() && lastModified != blockBlob.Properties.LastModified)
    //        {
    //            AcquireLease(blockBlob);
    //            byte[] blob = new byte[blockBlob.Properties.Length];
    //            blockBlob.DownloadToByteArray(blob, 0, AccessCondition.GenerateLeaseCondition(leaseId));
    //            this.Deserialize(blob);
    //        }
    //    }

    //    void CustomTokenCache_AfterAccess(TokenCacheNotificationArgs args)
    //    {
    //        if (this.HasStateChanged)
    //        {
    //            CloudBlockBlob blockBlob = container.GetBlockBlobReference(args.Resource);
    //            byte[] blob = this.Serialize();
    //            blockBlob.UploadFromByteArray(blob, 0, blob.Length, AccessCondition.GenerateLeaseCondition(leaseId));
    //            this.HasStateChanged = false;
    //            ReleaseLease(blockBlob);
    //        }
    //    }


    //    void AcquireLease(CloudBlockBlob blockBlob)
    //    {
    //        if (leaseId != null || !blockBlob.Exists())
    //            return;

    //        int retryCount = 0;
    //        const int MaxRetryCount = 30;
    //        bool acquired = false;

    //        do
    //        {
    //            try
    //            {
    //                leaseId = Guid.NewGuid().ToString();
    //                blockBlob.AcquireLease(TimeSpan.FromSeconds(15), leaseId);  // 15 seconds is the minimum
    //                acquired = true;
    //            }
    //            catch (StorageException ex)
    //            {
    //                if (ex.RequestInformation.HttpStatusCode == 409)
    //                {
    //                    retryCount++;
    //                    Thread.Sleep(rand.Next(50, 300));
    //                }
    //            }
    //        } while (!acquired && retryCount < MaxRetryCount);

    //        if (!acquired)
    //            throw new TimeoutException("Failed to acquire exclusive access to persistent token cache");
    //    }

    //    void ReleaseLease(CloudBlockBlob blockBlob)
    //    {
    //        if (leaseId != null)
    //        {
    //            blockBlob.ReleaseLease(AccessCondition.GenerateLeaseCondition(leaseId));
    //            leaseId = null;
    //        }
    //    }
    //}

}