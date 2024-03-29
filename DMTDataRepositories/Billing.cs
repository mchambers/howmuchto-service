﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using HowMuchTo.Models;

namespace HowMuchTo.BillingSystem
{
    public class Billing
    {
        public static decimal ComputeVigForAmount(decimal Amount)
        {
            return Amount * 0.10m;
        }

        public static decimal GetFeesForBounty(IBillingProcessor processor, decimal Bounty)
        {
            decimal fees;
            decimal netBountyFloored;
            decimal bountyChange;

            fees = processor.GetProcessingFeesForAmount(Bounty);
            fees += Billing.ComputeVigForAmount(Bounty);

            decimal netBounty = Bounty - fees;

            if (netBounty <= 1)
            {
                fees = .50m;
            }
            else if (netBounty <= 2)
            {
                fees = 1.0m;
            }
            else
            {
                fees = Math.Ceiling(fees);
                //netBountyFloored = Math.Floor(netBounty);
                //bountyChange = netBounty - netBountyFloored;
                //fees += (1.0m - bountyChange); // get us down to the nearest whole dollar
            }

            return fees;
        }
    }
}