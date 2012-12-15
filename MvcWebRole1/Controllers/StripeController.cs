using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using Stripe;

namespace HowMuchTo.Controllers
{
    public class StripeController : ApiController
    {
        class StripeWebHookData
        {
            public double Amount { get; set; }
            public Stripe.CreditCard Card { get; set; }
        }

        class StripeWebHookDataObject
        {
            public StripeWebHookData Object { get; set; }
        }

        class StripeWebHookEvent
        {
            public string ID { get; set; }
            public DateTime Created { get; set; }
            public string Type { get; set; }
            public string Object { get; set; }
            public StripeWebHookDataObject Data { get; set; }
        }

        [HttpPost]
        public void Hook(dynamic value)
        {
            string hookType = value.type;
            if (hookType.Equals("charge.succeeded"))
            {

            }
        }
    }
}
