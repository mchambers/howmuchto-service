using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;
using System.Data.Services.Client;
using System.Configuration;

namespace HowMuchTo.Models
{
    class CustomerNotificationDb : TableServiceEntity
    {
        public long CustomerID { get; set; }
        public string NotificationID { get; set; }
        public string Text { get; set; }
        public int Type { get; set; }
        public long ChallengeID { get; set; }
        public int Read { get; set; }
    }

    class CustomerNotificationRepository : ICustomerNotificationRepository
    {
        CloudStorageAccount storage;
        CloudTableClient client;
        TableServiceContextV2 context;

        private const string TableName = "Notification";

        public CustomerNotificationRepository()
        {
            storage = CloudStorageAccount.Parse(ConfigurationManager.AppSettings["StorageConnectionString"]);
            client = storage.CreateCloudTableClient();

            client.CreateTableIfNotExist(TableName);

            //context = client.GetDataServiceContext();
            context = new TableServiceContextV2(client.BaseUri.ToString(), client.Credentials);
            context.IgnoreResourceNotFoundException = true;
        }

        private CustomerNotification DbNotificationToNotification(CustomerNotificationDb d)
        {
            return new CustomerNotification()
            {
                CustomerID=d.CustomerID,
                NotificationID=d.NotificationID,
                Text=d.Text,
                Type=d.Type,
                ChallengeID=d.ChallengeID,
                Read=d.Read
            };
        }

        private CustomerNotificationDb NotificationToDbNotification(CustomerNotification c)
        {
            return new CustomerNotificationDb()
            {
                PartitionKey="Cust"+c.CustomerID.ToString(),
                RowKey=c.NotificationID,
                CustomerID=c.CustomerID,
                NotificationID=c.NotificationID,
                Text=c.Text,
                Type=c.Type,
                ChallengeID=c.ChallengeID,
                Read=c.Read
            };
        }

        public void Add(CustomerNotification notification)
        {
            notification.NotificationID = System.Guid.NewGuid().ToString();

            CustomerNotificationDb d = NotificationToDbNotification(notification);
            context.AttachTo(TableName, d, null);
            context.UpdateObject(d);

            context.SaveChangesWithRetries();

            context.Detach(d);
        }

        public void ClearAllForCustomer(long CustomerID)
        {
            CloudTableQuery<CustomerNotificationDb> b = (from e in context.CreateQuery<CustomerNotificationDb>(TableName) where e.PartitionKey == "Cust" + CustomerID.ToString() select e).AsTableServiceQuery<CustomerNotificationDb>();

            foreach (CustomerNotificationDb item in b)
            {
                context.DeleteObject(item);
            }

            context.SaveChangesWithRetries(SaveChangesOptions.Batch);
        }

        public IEnumerable<CustomerNotification> Get(long CustomerID)
        {
            CloudTableQuery<CustomerNotificationDb> b = (from e in context.CreateQuery<CustomerNotificationDb>(TableName) where e.PartitionKey == "Cust"+CustomerID.ToString() select e).AsTableServiceQuery<CustomerNotificationDb>();
            List<CustomerNotification> items = new List<CustomerNotification>();

            foreach (CustomerNotificationDb item in b)
            {
                items.Add(DbNotificationToNotification(item));
                context.Detach(item);
            }

            return items;
        }
    }
}
