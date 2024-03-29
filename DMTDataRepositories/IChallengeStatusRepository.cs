﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HowMuchTo.Models
{
    public interface IChallengeStatusRepository
    {
        List<ChallengeStatus> GetActiveChallengesForCustomer(long CustomerID);
        List<ChallengeStatus> GetActiveStatusesForChallenge(long ChallengeID);
        List<ChallengeStatus> GetChallengesBySourceCustomer(long CustomerID);
        List<ChallengeStatus> GetActiveChallengesBySourceCustomer(long CustomerID);

        void Add(ChallengeStatus value);
        ChallengeStatus Get(long CustomerID, long ChallengeID);
        void Update(ChallengeStatus value);
        ChallengeStatus GetNextVotePendingStatusForChallenge(long ChallengeID);
        void MoveStatusesToNewCustomer(long OriginalCustomerID, long NewCustomerID);

        void ClearAll(long ChallengeID);
        void MoveToLocker(long CustomerID, long ChallengeID);

        bool CustomerTookChallenge(long CustomerID, long ChallengeID);
    }
}
