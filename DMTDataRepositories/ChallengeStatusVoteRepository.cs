using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.ServiceRuntime;
using Microsoft.WindowsAzure.StorageClient;
using System.Data.Services.Client;

namespace HowMuchTo.Models
{
    public class ChallengeStatusVoteRepository : IChallengeStatusVoteRepository
    {
        CloudStorageAccount storage;
        CloudTableClient client;
        TableServiceContextV2 context;

        private const string TableName = "ChalStatusVotes";

        public ChallengeStatusVoteRepository()
        {
            storage = CloudStorageAccount.Parse(RoleEnvironment.GetConfigurationSettingValue("StorageConnectionString"));
            client = storage.CreateCloudTableClient();

            client.CreateTableIfNotExist(TableName);

            context = new TableServiceContextV2(client.BaseUri.ToString(), client.Credentials);
            context.MergeOption = MergeOption.NoTracking;
            context.IgnoreResourceNotFoundException = true;
        }

        private string DbPartitionForTaker(long ChallengeID, long CustomerID)
        {
            return "Chal" + ChallengeID.ToString() + "Cust" + CustomerID.ToString();
        }

        private string DbRowForBidder(long CustomerID)
        {
            return "BidderCust" + CustomerID.ToString();
        }

        private ChallengeStatusVoteDb VoteToDbVote(ChallengeStatusVote v)
        {
            if (v == null) return null;

            ChallengeStatusVoteDb d = new ChallengeStatusVoteDb();

            d.Accepted = v.Accepted;
            d.ChallengeID = v.ChallengeID;
            d.CustomerID = v.CustomerID;
            d.BidderCustomerID = v.BidderCustomerID;

            d.PartitionKey = DbPartitionForTaker(d.ChallengeID, d.CustomerID);
            d.RowKey = DbRowForBidder(d.BidderCustomerID);

            return d;
        }

        private ChallengeStatusVote DbVoteToVote(ChallengeStatusVoteDb d)
        {
            if (d == null) return null;

            ChallengeStatusVote v = new ChallengeStatusVote();

            v.Accepted = d.Accepted;
            v.ChallengeID = d.ChallengeID;
            v.CustomerID = d.CustomerID;
            v.BidderCustomerID = d.BidderCustomerID;
            
            return v;
        }

        public bool Add(ChallengeStatusVote vote)
        {
            bool success = false;

            ChallengeStatusVoteDb d = VoteToDbVote(vote);
            context.MergeOption = MergeOption.AppendOnly;
            context.AttachTo(TableName, d, null);
            context.UpdateObject(d);

            try
            {
                context.SaveChangesWithRetries();
                success=true;
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Status repo vote exception: Couldn't add vote " + e.ToString());
                success=false;
            }
            finally
            {
                context.Detach(d);
                context.MergeOption = MergeOption.NoTracking;
            }

            return success;
        }

        public int GetCount(ChallengeStatus status)
        {
            int count=(from e in context.CreateQuery<ChallengeStatusVoteDb>(TableName) where e.PartitionKey==DbPartitionForTaker(status.ChallengeID, status.CustomerID) select e).Count();
            return count;
        }

        public int GetYesVotes(ChallengeStatus status)
        {
            IEnumerable<ChallengeStatusVoteDb> votes = (from e in context.CreateQuery<ChallengeStatusVoteDb>(TableName) where e.PartitionKey == DbPartitionForTaker(status.ChallengeID, status.CustomerID) select e).AsEnumerable<ChallengeStatusVoteDb>();
            return votes.Count(v => v.Accepted == true);
        }

        public int GetNoVotes(ChallengeStatus status)
        {
            IEnumerable<ChallengeStatusVoteDb> votes = (from e in context.CreateQuery<ChallengeStatusVoteDb>(TableName) where e.PartitionKey == DbPartitionForTaker(status.ChallengeID, status.CustomerID) select e).AsEnumerable<ChallengeStatusVoteDb>();
            return votes.Count(v => v.Accepted == false);
        }

        public ChallengeStatusVote BidderDidVote(long ChallengeID, long CustomerID, long BidderID)
        {
            IEnumerable<ChallengeStatusVoteDb> votes;

            try
            {
                votes = (from e in context.CreateQuery<ChallengeStatusVoteDb>(TableName) where e.PartitionKey == DbPartitionForTaker(ChallengeID, CustomerID) && e.RowKey == DbRowForBidder(BidderID) select e).AsEnumerable<ChallengeStatusVoteDb>();
            }
            catch (Exception e)
            {
                return null;
            }

            return DbVoteToVote(votes.FirstOrDefault<ChallengeStatusVoteDb>());
        }
    }
}