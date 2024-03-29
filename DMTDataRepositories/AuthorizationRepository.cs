﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using System.Data.Services.Client;
using System.Configuration;

namespace HowMuchTo.Models
{
    public class AuthorizationRepository : IAuthorizationRepository
    {
        CloudStorageAccount storage;
        CloudTableClient client;
        TableServiceContext context;

        private AuthorizationDb AuthorizationToDbAuthorization(Authorization a)
        {
            AuthorizationDb d = new AuthorizationDb();

            d.CustomerID = a.CustomerID;
            d.EmailAddress = a.EmailAddress;
            d.PartitionKey = a.Token;
            d.RowKey = a.UniqueID;
            d.Valid = a.Valid;
            d.Type = a.Type;
            return d;
        }

        private Authorization DbAuthorizationToAuthorization(AuthorizationDb d)
        {
            Authorization a = new Authorization();

            a.UniqueID = d.RowKey;
            a.Token = d.PartitionKey;
            a.EmailAddress = d.EmailAddress;
            a.CustomerID = d.CustomerID;
            a.Valid = d.Valid;
            a.Type = d.Type;

            return a;
        }

        public AuthorizationRepository()
        {
            storage = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            client = storage.CreateCloudTableClient();
            client.CreateTableIfNotExist("Authorization");
            context = client.GetDataServiceContext();
        }

        public Authorization GetWithToken(string Token)
        {
            AuthorizationDb a = (from e in context.CreateQuery<AuthorizationDb>("Authorization") where e.PartitionKey == Token select e).FirstOrDefault();
            context.Detach(a);
            return DbAuthorizationToAuthorization(a);
        }

        public void Add(Authorization a)
        {
            a.Valid = true;
            context.AddObject("Authorization", AuthorizationToDbAuthorization(a));
            context.SaveChangesWithRetries();
            context.Detach(a);
        }

        public void Remove(string Token)
        {
            throw new NotImplementedException();
        }
    }
}