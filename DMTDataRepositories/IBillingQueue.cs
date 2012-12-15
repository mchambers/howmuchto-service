using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HowMuchTo.Models
{
    public interface IBillingQueue
    {
        void ProcessBilling(ChallengeStatus s);
    }
}