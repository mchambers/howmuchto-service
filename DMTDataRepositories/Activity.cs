using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.WindowsAzure.StorageClient;

namespace HowMuchTo.Models
{
    public enum ActivityType
    {
        ActivityCreateDare,
        ActivityTakeDare,
        ActivityAddEvidence,
        ActivityComment,
        ActivityLikeDare,
        ActivityBackDare,
        ActivityVoteYes,
        ActivityVoteNo,
        ActivityComplete,
        ActivityClaimDone,
        ActivityRejectDare,
        ActivityTargetPriceSet
    }

    public class ActivitySortOnDate : IComparer<Activity>
    {
        public int Compare(Activity x, Activity y)
        {
            if (x.Created > y.Created)
                return -1;
            else if (x.Created < y.Created)
                return 1;
            else
                return 0;
        }
    }

    public class Activity : TableServiceEntity
    {
        public Activity()
        {
        }

        public Activity(long ChallengeID, DateTime Created)
        {
            this.PartitionKey = "Chal" + ChallengeID.ToString();
            this.RowKey = System.Guid.NewGuid().ToString();

            this.ChallengeID = ChallengeID;
            this.Created = Created;
        }

        public long ID { get; set; }
        public int Type { get; set; }
        public string Content { get; set; }
        public string MediaURL { get; set; }
        public long TargetObject { get; set; }
        public long SourceCustomer { get; set; }
        public long CustomerID { get; set; }
        public long TakerCustomerID { get; set; }
        public long ChallengeID { get; set; }
        public DateTime Created { get; set; }
        public string Text { get; set; }
        public Customer Customer { get; set; }
    }
}
