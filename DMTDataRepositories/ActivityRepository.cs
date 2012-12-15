using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using System.Data.Services.Client;
using ServiceStack.Redis;
using ServiceStack.Common;
using ServiceStack.Redis.Generic;

namespace HowMuchTo.Models
{
    public class ActivityRepository : IActivityRepository
    {
        CloudStorageAccount storage;
        CloudTableClient client;
        TableServiceContextV2 context;

        private const string TableName = "Activity";

        public ActivityRepository()
        {
            storage = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));
            client = storage.CreateCloudTableClient();

            client.CreateTableIfNotExist(TableName);

            context = new TableServiceContextV2(client.BaseUri.ToString(), client.Credentials);
            context.IgnoreResourceNotFoundException = true;
        }

        public IEnumerable<Activity> GetActivityForChallenge(long ChallengeID)
        {
            CloudTableQuery<Activity> query = (from e in context.CreateQuery<Activity>(TableName) where e.PartitionKey == "Chal" + ChallengeID select e).AsTableServiceQuery<Activity>();
            return query.AsEnumerable<Activity>();
        }

        private string RedisKeyForLatestActivity(long ChallengeID)
        {
            return "Chal" + ChallengeID.ToString() + ":LatestActivity";
        }

        private IRedisClient GetRedisClient()
        {
            return new RedisClient(RoleEnvironment.GetConfigurationSettingValue("RedisServerHost"), 6379, RoleEnvironment.GetConfigurationSettingValue("RedisServerAuth"));
        }

        public void Add(Activity value)
        {
            if (value.Created == null || value.ChallengeID == 0)
                return;

            if (value.RowKey == null)
                value.RowKey = value.Created.ToString();

            if (value.PartitionKey == null)
                value.PartitionKey = "Chal" + value.ChallengeID.ToString();

            context.AttachTo(TableName, value, null);
            context.UpdateObject(value);

            context.SaveChangesWithRetries();
            context.Detach(value);

            /*
            try
            {
                using (var redisClient = GetRedisClient())
                {
                    IRedisTypedClient<Activity> redis = redisClient.As<Activity>();
                    redis.SetEntry(this.RedisKeyForLatestActivity(value.ChallengeID), value);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Activity repo exception: " + e.ToString());
            } */
        }

        public Activity GetMostRecentForChallenge(long ChallengeID)
        {
            /*
            try
            {
                using (var redisClient = GetRedisClient())
                {
                    IRedisTypedClient<Activity> redis = redisClient.As<Activity>();
                    return redis.GetValue(this.RedisKeyForLatestActivity(ChallengeID));
                }
            } 
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Activity repo exception: " + e.ToString());
                return null;
            }*/

            CloudTableQuery<Activity> query = (from e in context.CreateQuery<Activity>(TableName) where e.PartitionKey == "Chal" + ChallengeID select e).AsTableServiceQuery<Activity>();

            List<Activity> listActs = query.ToList<Activity>();

            try
            {
                listActs.Sort(new ActivitySortOnDate());
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e.ToString());
            }

            return listActs[listActs.Count - 1];
        }


        public int CountTypeForChallenge(long ChallengeID, int Type)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<Activity> GetActivityForChallengeByType(long ChallengeID, int Type)
        {
            throw new NotImplementedException();
        }
    }
}
