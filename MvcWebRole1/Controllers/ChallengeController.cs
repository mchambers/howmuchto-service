using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using HowMuchTo.Models;
using System.Diagnostics;
using System.Web;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Security.Principal;
using HowMuchTo.BillingSystem;

namespace HowMuchTo.Controllers
{
    public class ChallengeController : ApiController
    {
        public class ChallengeTargetPrice
        {
            public decimal Price { get; set; }
            public long ChallengeID { get; set; }
        }

        private IChallengeRepository ChalRepo;
        private IChallengeBidRepository BidRepo;
        private IChallengeStatusRepository StatusRepo;
        private ICustomerRepository CustRepo;
        private Security Security;

        public ChallengeController()
        {
            ChalRepo = RepoFactory.GetChallengeRepo();
            BidRepo = RepoFactory.GetChallengeBidRepo();
            StatusRepo = RepoFactory.GetChallengeStatusRepo();
            CustRepo = RepoFactory.GetCustomerRepo();
            Security = new Security();
        }

        private Challenge PrepOutboundChallenge(Challenge c)
        {
            if (c == null) return null;

            c.Customer = CustRepo.GetBasicWithID(c.CustomerID);

            if (c.TargetCustomerID > 0)
            {
                c.TargetCustomer = CustRepo.GetBasicWithID(c.TargetCustomerID);
            }

            c.Disposition = (int)Security.DetermineDisposition(c);

            try
            {
                if (c.Disposition == (int)Security.Disposition.Taker)
                    c.Status = StatusRepo.Get(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, c.ID);
                else if (c.Disposition == (int)Security.Disposition.Backer || c.Disposition==(int)Security.Disposition.Originator)
                    c.Bid = BidRepo.CustomerDidBidOnChallenge(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, c.ID);
            }
            catch (Exception e)
            {
            }

            c.NumberOfBidders = BidRepo.GetBidCountForChallenge(c.ID);
            c.NumberOfTakers = StatusRepo.GetActiveStatusesForChallenge(c.ID).Count;
            
            c.Activity = RepoFactory.GetActivityRepo().GetMostRecentForChallenge(c.ID);
            
            if (c.Activity != null && c.Activity.CustomerID != 0)
                c.Activity.Customer = CustRepo.GetBasicWithID(c.Activity.CustomerID);

            return c;
        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Admin)]
        public void RetryFailedBillings()
        {
            IEnumerable<Challenge> chals = ChalRepo.GetUnbilledChallenges();
            foreach (Challenge c in chals)
            {
                Dictionary<string, long> billingQueueItemData = new Dictionary<string, long>();
                billingQueueItemData.Add("ChalID", c.ID);
                billingQueueItemData.Add("CustID", c.TargetCustomerID);
                RepoFactory.GetProcessingQueue().PutQueueMessage(ProcessingQueue.MessageType.Billing, billingQueueItemData);
            }
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Public)]
        public List<Challenge> Featured(int StartAt = 0, int Limit = 10)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Public)]
        public List<Challenge> Open(int StartAt = 0, int Limit = 10)
        {
            List<Challenge> chals = ChalRepo.GetOpen(StartAt, Limit).ToList<Challenge>();
            List<Challenge> outChals = new List<Challenge>(chals.Count);

            foreach (Challenge c in chals)
            {
                outChals.Add(PrepOutboundChallenge(c));
            }

            return outChals;
        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Moderator)]
        public void Takedown(long id)
        {
            DareManager dareMgr = new DareManager();
            dareMgr.Takedown(id);
            dareMgr = null;
        }
        
        // GET /api/challenge
        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Public)]
        public List<Challenge> Get(int StartAt=0, int Limit=10)
        {
            List<Challenge> chals = ChalRepo.GetNewest(StartAt, Limit).ToList<Challenge>();
            List<Challenge> outChals = new List<Challenge>(chals.Count);

            foreach (Challenge c in chals)
            {
                outChals.Add(PrepOutboundChallenge(c));
            }

            return outChals;
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Public)]
        public List<Activity> Activity(long id)
        {
            IEnumerable<Activity> activities = RepoFactory.GetActivityRepo().GetActivityForChallenge(id);

            List<Activity> listActs = activities.ToList<Activity>();

            foreach (Activity a in listActs)
            {
                a.Customer = RepoFactory.GetCustomerRepo().GetBasicWithID(a.CustomerID);
            }

            try
            {
                listActs.Sort(new ActivitySortOnDate());
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine(e.ToString());
            }
            
            return listActs;
        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void Comment(Activity c)
        {
            if (c.ChallengeID == 0)
                throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);

            if (c.Content == null || c.Content.Equals(""))
                throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);

            c.CustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;

            Activity a = new Activity(c.ChallengeID, DateTime.Now) { CustomerID = c.CustomerID, Content=c.Content, Type=(int)ActivityType.ActivityComment };

            try
            {
                RepoFactory.GetActivityRepo().Add(a);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("EXCEPTION /challenge/comment " + e.ToString());
            }
        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void TargetPrice(ChallengeTargetPrice p)
        {
            if (p.ChallengeID == 0)
                throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);

            Challenge c = ChalRepo.Get(p.ChallengeID);

            if (Security.DetermineDisposition(c) != Security.Disposition.Taker)
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            try
            {
                ChalRepo.SetTargetPrice(c.ID, p.Price);

                RepoFactory.GetActivityRepo().Add(new Activity(p.ChallengeID, DateTime.UtcNow)
                {
                    CustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID,
                    Type = (int)ActivityType.ActivityTargetPriceSet
                });
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("EXCEPTION: /challenge/targetprice " + e.ToString());
            }
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public List<Challenge> ActiveOutboundForCustomer(long id)
        {
            throw new NotImplementedException();
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public List<Challenge> OpenForCustomer(long id)
        {
            if (id == 0) id = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;

            Customer c = CustRepo.GetWithID(id);
            Security.Audience audience = Security.DetermineAudience(c);
            if ((audience != Security.Audience.Owner) && (audience != Security.Audience.Friends))
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            List<Challenge> chals = ChalRepo.GetOpenForCustomer(id).ToList<Challenge>();
            List<Challenge> outChals = new List<Challenge>(chals.Count);

            foreach (Challenge chal in chals)
            {
                outChals.Add(PrepOutboundChallenge(chal));
            }

            return outChals;
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public List<Challenge> ActiveForCustomer(long id)
        {
            if (id == 0) id = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;

            Customer c = CustRepo.GetWithID(id);
            Security.Audience audience = Security.DetermineAudience(c);
            if ((audience != Security.Audience.Owner) && (audience != Security.Audience.Friends))
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            /*
             * 
             * array of challenge objects with a Status object in them
             * 
             * */
            List<ChallengeStatus> statuses = StatusRepo.GetActiveChallengesForCustomer(id);
            List<Challenge> challenges = new List<Challenge>();

            foreach (ChallengeStatus s in statuses)
            {
                try
                {
                    Challenge chal = ChalRepo.Get(s.ChallengeID);
                    //if (chal.Expires > DateTime.Now)
                    //{
                    chal = PrepOutboundChallenge(chal);

                    if (chal.State < 4) // TODO: We need to filter this way before it gets this far.
                    {
                        chal.Status = s;
                        //chal.Status.Disposition = (int)Security.DetermineDisposition(chal.Status);
                        challenges.Add(chal);
                    }
                    //}
                } 
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("EXCEPTION in /challenge/activeforcustomer: " + ex.ToString());
                }
            }

            return challenges;
        }

        // GET /api/challenge/5
        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Public)]
        public Challenge Get(long id)
        {
            Challenge c = PrepOutboundChallenge(ChalRepo.Get(id));

            //c.Bids = BidRepo.Get(c.ID);

            return c;
        }

        [HttpGet]
        public List<ChallengeStatus> Status(long id)
        {
            return StatusRepo.GetActiveStatusesForChallenge(id);
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public List<Challenge> ActiveBids()
        {
            List<ChallengeBid> bids=BidRepo.GetForCustomer(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID);
            List<Challenge> chals = new List<Challenge>(bids.Count);

            foreach (ChallengeBid b in bids)
            {
                try
                {
                    Challenge chal = PrepOutboundChallenge(ChalRepo.Get(b.ChallengeID));

                    if (chal.Bid.Status == (int)ChallengeBid.BidStatusCodes.VotePending)
                    {
                        if (chal.Bid.PendingVoteCustomerID == 0)
                            chal.Bid.VotePendingStatus = StatusRepo.GetNextVotePendingStatusForChallenge(chal.ID);
                        else
                            chal.Bid.VotePendingStatus = StatusRepo.Get(chal.Bid.PendingVoteCustomerID, chal.Bid.ChallengeID);
                    }

                    chals.Add(chal);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine("EXCEPTION in /activebids: " + ex.ToString());
                }
                
            }

            return chals;
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public List<ChallengeBid> Bids(long id)
        {
            List<ChallengeBid> bids = BidRepo.Get(id);

            // we can optimize this by getting all the votes for this challenge
            // and matching them against the bidders and doing that sort of shit

            foreach (ChallengeBid b in bids)
            {
                b.Customer = CustRepo.GetBasicWithID(b.CustomerID);
                ChallengeStatusVote vote = RepoFactory.GetChallengeStatusVoteRepo().BidderDidVote(b.ChallengeID, ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, b.CustomerID);
                if (vote == null)
                {
                    b.VoteStatus = (int)ChallengeBid.BidVoteStatus.NoVote;
                }
                else
                {
                    if (vote.Accepted)
                        b.VoteStatus = (int)ChallengeBid.BidVoteStatus.BidderAccepts;
                    else
                        b.VoteStatus = (int)ChallengeBid.BidVoteStatus.BidderRejects;
                }
            }

            return bids;
        }

        // prices a dare for the current customer; works for bids or new dares.
        // returns payment required if they don't have a payment provider.
        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public ChallengeBid Price(ChallengeBid value)
        {
            Customer cust = CustRepo.GetWithID(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID);

            IBillingProcessor processor = BillingSystem.BillingProcessorFactory.
                GetBillingProcessor((BillingSystem.BillingProcessorFactory.SupportedBillingProcessor)cust.BillingType);

            if (processor == null)
            {
                // the customer doesn't have a valid billing provider. don't accept the bid.
                throw new HttpResponseException(System.Net.HttpStatusCode.PaymentRequired);
            }

            decimal approximateFees = Billing.GetFeesForBounty(processor, value.Amount);

            return new ChallengeBid { ComputedFees = approximateFees, Amount = value.Amount };
        }

        // PUT /api/challenge/bid/5
        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void Bid(ChallengeBid value)
        {
            Challenge c = ChalRepo.Get(value.ChallengeID);
            Customer cust=CustRepo.GetWithID(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID);

            if (c == null)
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);
            
            if (c.Privacy == (int)Challenge.ChallengePrivacy.FriendsOnly)
            {
                if (Security.DetermineAudience(c) < Security.Audience.Friends)
                    throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);
            }

            if (BidRepo.CustomerDidBidOnChallenge(cust.ID, c.ID) != null)
                throw new HttpResponseException(System.Net.HttpStatusCode.Conflict);

            IBillingProcessor processor = BillingSystem.BillingProcessorFactory.
                GetBillingProcessor((BillingSystem.BillingProcessorFactory.SupportedBillingProcessor)cust.BillingType);

            if (processor == null)
            {
                // the customer doesn't have a valid billing provider. don't accept the bid.
                throw new HttpResponseException(System.Net.HttpStatusCode.PaymentRequired);
            }
            
            decimal approximateFees = Billing.GetFeesForBounty(processor, value.Amount);

            ChalRepo.AddBidToChallenge(c, ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, value.Amount, approximateFees);
            
            CustomerNotifier.NotifyChallengeBacked(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, c.CustomerID, c.ID);

            Activity activity = new Activity(c.ID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityBackDare, CustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID };
            RepoFactory.GetActivityRepo().Add(activity);

            // auto-accept the dare if the target price is met.
            if (c.TargetPrice > 0 && (c.CurrentBid + (value.Amount - approximateFees) > c.TargetPrice))
                this.Take(c.ID);
        }

        // POST /api/challenge
        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public Challenge New(NewChallenge value)
        {
            if (value.Description.Equals(""))
            {
                Trace.WriteLine("EXCEPTION: You must specify a description for a challenge", "ChallengeController::New");

                throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
            }

            Challenge possibleDupe = ChalRepo.CheckForDuplicate(new Challenge() { Created=DateTime.Now, Description = value.Description, CustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, Privacy=value.Privacy });
            if (possibleDupe != null)
            {
                // FUCK ME BRO.
                return possibleDupe;
            }

            Customer cust = CustRepo.GetWithID(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID);

            IBillingProcessor processor = BillingSystem.BillingProcessorFactory.GetBillingProcessor((BillingSystem.BillingProcessorFactory.SupportedBillingProcessor)cust.BillingType);
            if (processor == null)
            {
                // No billing processor exists to service this request. All challenges must originate from customers
                // that have set up their billing. Therefore we must reject this request outright.

                // let's be naughty and throw a "reserved for future use" HTTP status code.
                throw new HttpResponseException(System.Net.HttpStatusCode.PaymentRequired);
            }

            Trace.WriteLine("Creating a new challenge for customer " + ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID.ToString(), "ChallengeController::New");

            value.CustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;
            
            decimal firstBid = (decimal)value.CurrentBid;
            
            value.ID=ChalRepo.Add(value);
            
            decimal approxFees = Billing.GetFeesForBounty(processor, value.CurrentBid);

            Trace.WriteLine("Adding a bid of " + firstBid.ToString() + " to challenge ID " + value.ID.ToString(), "ChallengeController::New");
            ChalRepo.AddBidToChallenge(value, value.CustomerID, firstBid, approxFees);

            value.CurrentBid = (firstBid - approxFees);

            bool createTargetStatus=false;

            Trace.WriteLine("The target customer type for challenge " + value.ID.ToString() + " is " + value.ForeignNetworkType.ToString());

            // Try using a supplied email address to target the challenge first...
            if (value.EmailAddress != null && !value.EmailAddress.Equals(""))
            {
                value.EmailAddress = value.EmailAddress.ToLower().Trim();

                Customer tryEMail = CustRepo.GetWithEmailAddress(value.EmailAddress);
                if (tryEMail != null && tryEMail.EmailAddress.Equals(value.EmailAddress))
                {
                    Trace.WriteLine("Found the email customer " + value.EmailAddress + " for new challenge " + value.ID.ToString(), "ChallengeController::New");
                    value.TargetCustomerID = tryEMail.ID;
                }
                else
                {
                    Trace.WriteLine("Couldn't find the email customer " + value.EmailAddress + " for new challenge " + value.ID.ToString(), "ChallengeController::New");
                    Customer unclaimedEmail = new Customer();
                    unclaimedEmail.EmailAddress = value.EmailAddress;
                    unclaimedEmail.Type = (int)Customer.TypeCodes.Unclaimed;
                    value.TargetCustomerID = CustRepo.Add(unclaimedEmail);
                }
                createTargetStatus = true;
            }
            else // then try using a supplied foreign network connection...
            {
                if (value.ForeignNetworkType != Customer.ForeignUserTypes.Undefined)
                {
                    long fnCustID = CustRepo.GetIDForForeignUserID(value.ForeignNetworkUserID, value.ForeignNetworkType);
                    if (fnCustID > 0)
                    {
                        Trace.WriteLine("Found the foreign network customer " + value.ForeignNetworkUserID + " as customer ID " + fnCustID.ToString());
                        value.TargetCustomerID = fnCustID;
                    }
                    else
                    {
                        Customer unclaimedCust = new Customer
                        {
                            FirstName = value.FirstName,
                            LastName = value.LastName,
                            Type = (int)Customer.TypeCodes.Unclaimed,
                            ForeignUserType = (int)value.ForeignNetworkType
                        };

                        value.TargetCustomerID = CustRepo.Add(unclaimedCust);

                        Trace.WriteLine("Created a new foreign network customer " + value.ForeignNetworkUserID + " as customer ID " + value.TargetCustomerID.ToString());

                        CustRepo.AddForeignNetworkForCustomer(value.TargetCustomerID, value.ForeignNetworkUserID, value.ForeignNetworkType);
                    }

                    createTargetStatus = true;
                }
            }
            
            ChalRepo.Update(value);

            try
            {
                Activity activity = new Activity(value.ID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityCreateDare, CustomerID = value.CustomerID };
                RepoFactory.GetActivityRepo().Add(activity);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("New Challenge Exception: " + e.ToString());
            }
            
            if (createTargetStatus)
            {
                // notify the receipient of the new challenge.
                CustomerNotifier.NotifyNewChallenge(value.CustomerID, value.TargetCustomerID, value.ID);
            }

            return PrepOutboundChallenge(value);
        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public ChallengeStatus Take(long id)
        {
            Trace.WriteLine("Customer " + ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID.ToString() + " wants to take challenge "+id.ToString(), "ChallengeController::Take");

            Challenge c = ChalRepo.Get(id);
            
            if (c == null)
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);

            if (!Security.CanManipulateContent(c))
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            /*
            if (c.TargetCustomerID == ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID)
                throw new DaremetoResponseException("This Challenge was sent to the current user; call /challengestatus/accept instead", System.Net.HttpStatusCode.Conflict);
            */

            if (c.CustomerID == ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID)
                throw new HttpResponseException(System.Net.HttpStatusCode.Conflict);

            if (c.TargetCustomerID == ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID)
            {
                // if this dare was sent directly to the customer,
                // we can go ahead and mark it Accepted since it's no
                // longer open for additional takers.
                c.State = (int)Challenge.ChallengeState.Accepted;
                ChalRepo.Update(c);
            }

            // already took it?
            if(StatusRepo.CustomerTookChallenge(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, id))
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            ChallengeStatus s = new ChallengeStatus();
            s.ChallengeID = c.ID;
            s.ChallengeOriginatorCustomerID = c.CustomerID;
            s.CustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;
            s.Status = (int)ChallengeStatus.StatusCodes.Accepted;

            Trace.WriteLine("Adding 'taking this dare' status for customer " + ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID.ToString() + " and challenge " + id.ToString(), "ChallengeController::Take");
            StatusRepo.Add(s);

            CustomerNotifier.NotifyChallengeAccepted(s.ChallengeOriginatorCustomerID, s.CustomerID, c.ID);

            Activity activity = new Activity(s.ChallengeID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityTakeDare, CustomerID = s.CustomerID };
            RepoFactory.GetActivityRepo().Add(activity);

            return s;
        }

        // PUT /api/challenge/5
        [HttpPut]
        public void Put(int id, string value)
        {
        }
        
        [HttpDelete]
        // DELETE /api/challenge/5
        public void Delete(int id)
        {
        }
    }
}
