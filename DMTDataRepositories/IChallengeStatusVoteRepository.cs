using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HowMuchTo.Models
{
    public interface IChallengeStatusVoteRepository
    {
        bool Add(ChallengeStatusVote vote);
        int GetCount(ChallengeStatus status);
        int GetYesVotes(ChallengeStatus status);
        int GetNoVotes(ChallengeStatus status);
        ChallengeStatusVote BidderDidVote(long ChallengeID, long CustomerID, long BidderID);
    }
}