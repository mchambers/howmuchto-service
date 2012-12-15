using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HowMuchTo.Models
{
    public interface IActivityRepository
    {
        IEnumerable<Activity> GetActivityForChallenge(long ChallengeID);
        void Add(Activity value);
        Activity GetMostRecentForChallenge(long ChallengeID);
        int CountTypeForChallenge(long ChallengeID, int Type);
        IEnumerable<Activity> GetActivityForChallengeByType(long ChallengeID, int Type);
    }
}
