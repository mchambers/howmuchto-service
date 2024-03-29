﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HowMuchTo.BillingSystem
{
    public class BillingProcessorResult
    {
        public enum BillingProcessorResultCode
        {
            Paid,
            Declined,
            Expired,
            InvalidAccountNumber,
            ProcessorSpecificFailure,
            SystemFailure
        }

        public string ForeignTransactionID { get; set; }
        public BillingProcessorResultCode Result { get; set; }
        public string ProcessorResponseText { get; set; }
        public int ProcessorResponseCode { get; set; }
    }

    public interface IBillingProcessor
    {
        decimal GetProcessingFeesForAmount(decimal Amount);                                        // ONLY what the billing processor will charge us.
                                                                                                   // should not include "profit" or "vig" of any kind.

        BillingProcessorResult Charge(string ForeignCustomerID, decimal Amount, string Reason=null); // process the transaction w/ the 3rd party.
                                                                                                   // a transaction of Amount will approx. cost us the fees
                                                                                                   // returned by GetProcessingFeesForAmount().
    }
}
