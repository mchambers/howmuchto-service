using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using HowMuchTo.Models;
using System.Web;

namespace HowMuchTo.Controllers
{
    public class ChallengeStatusController : ApiController
    {
        private IChallengeStatusRepository StatusRepo;
        private IChallengeRepository ChalRepo;
        private Security Security;
        private IChallengeBidRepository BidRepo;
        private IChallengeStatusVoteRepository VoteRepo;
        private ICustomerRepository CustRepo;
        private IFriendshipRepository FriendRepo;

        public ChallengeStatusController()
        {
            StatusRepo = RepoFactory.GetChallengeStatusRepo();
            ChalRepo = RepoFactory.GetChallengeRepo();
            Security = new Security();
            BidRepo = RepoFactory.GetChallengeBidRepo();
            VoteRepo = RepoFactory.GetChallengeStatusVoteRepo();
            CustRepo = RepoFactory.GetCustomerRepo();
            FriendRepo = RepoFactory.GetFriendshipRepo();
        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void AcceptClaim(ChallengeStatus status)
        {
            ChallengeStatus s = StatusRepo.Get(status.CustomerID, status.ChallengeID);

            ChallengeBid bid = BidRepo.CustomerDidBidOnChallenge(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, s.ChallengeID);
            if (bid == null)
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            try
            {
                if (VoteRepo.BidderDidVote(s.ChallengeID, s.CustomerID, bid.CustomerID) != null)
                    return; // you already voted, dawg.
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Vote Repo Exception: " + e.ToString());
                throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
            }
            
            ChallengeStatusVote vote = new ChallengeStatusVote();
            vote.ChallengeID = s.ChallengeID;
            vote.CustomerID = s.CustomerID;
            vote.BidderCustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;
            vote.Accepted = true;

            try
            {
                if (!VoteRepo.Add(vote))
                {
                    // if this returns false, they probably already voted.
                    return; // HACK.
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Vote Repo Exception: " + e.ToString());
                throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
            }
            

            try
            {
                Activity activity = new Activity(s.ChallengeID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityVoteYes, TakerCustomerID = vote.CustomerID, CustomerID = vote.BidderCustomerID };
                RepoFactory.GetActivityRepo().Add(activity);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Activity Exception: " + e.ToString());
            }

            int yesVotes = VoteRepo.GetYesVotes(s);
            if (yesVotes > (BidRepo.GetBidCountForChallenge(status.ChallengeID) * 0.33))
            {
                Challenge chal = ChalRepo.Get(s.ChallengeID);
                chal.State = (int)Challenge.ChallengeState.Completed;
                chal.TargetCustomerID = status.CustomerID;

                try
                {
                    ChalRepo.Update(chal);

                    BidRepo.UpdateStatusForBidsOnChallenge(s.ChallengeID, ChallengeBid.BidStatusCodes.Accepted);

                    s.Status = (int)ChallengeStatus.StatusCodes.Completed;
                    StatusRepo.Update(s);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine("Challenge Status Exception: " + e.ToString());
                    throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
                }

                // challenge completed! award the money! DO IT DO IT!
                try
                {
                    Activity activity = new Activity(s.ChallengeID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityComplete, CustomerID = status.CustomerID };
                    RepoFactory.GetActivityRepo().Add(activity);
                }
                catch (Exception e)
                {
                    System.Diagnostics.Trace.WriteLine("Activity Exception: " + e.ToString());
                }

                // queue the billing system to process this challenge status
                Dictionary<string, long> billingQueueItemData=new Dictionary<string,long>();
                billingQueueItemData.Add("ChalID", s.ChallengeID);
                billingQueueItemData.Add("CustID", s.CustomerID);
                RepoFactory.GetProcessingQueue().PutQueueMessage(ProcessingQueue.MessageType.Billing, billingQueueItemData);

                // notify the customer
                CustomerNotifier.NotifyChallengeAwardedToYou(s.CustomerID, s.ChallengeID);
            }
            else
            {
                // keep waitin'.
                

            }
        }
        
        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void RejectClaim(ChallengeStatus status)
        {
            ChallengeStatus s = StatusRepo.Get(status.CustomerID, status.ChallengeID);
            
            ChallengeBid bid=BidRepo.CustomerDidBidOnChallenge(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, s.ChallengeID);
            if (bid==null)
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            if (VoteRepo.BidderDidVote(s.ChallengeID, s.CustomerID, bid.CustomerID)!=null)
                return; // you already voted, dawg.

            // add the "no" vote
            ChallengeStatusVote vote = new ChallengeStatusVote();
            vote.ChallengeID = s.ChallengeID;
            vote.CustomerID = s.CustomerID;
            vote.BidderCustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;
            vote.Accepted = false;

            if (!VoteRepo.Add(vote))
                return; // HACK. 

            try
            {
                Activity activity = new Activity(s.ChallengeID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityVoteNo, TakerCustomerID=vote.CustomerID, CustomerID = vote.BidderCustomerID };
                RepoFactory.GetActivityRepo().Add(activity);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Activity Exception: " + e.ToString());
            }

            int noVotes=VoteRepo.GetNoVotes(s);
            if(noVotes > (BidRepo.GetBidCountForChallenge(status.ChallengeID)*0.66))
            {
                s.Status = (int)ChallengeStatus.StatusCodes.SourceRejected;
                StatusRepo.Update(s);

                // get the next homey who's working on this, if any.
                ChallengeStatus nextStatus = StatusRepo.GetNextVotePendingStatusForChallenge(s.ChallengeID);
                if (nextStatus != null)
                {
                    BidRepo.UpdateVotePendingCustomerIDForChallenge(s.ChallengeID, nextStatus.CustomerID);
                }
                else
                {
                    // reopen the bidding! NO WINNER NO WINNER
                    
                    
                }

                // you've failed this challenge my friend.
                //CustomerNotifier.NotifyChallengeRejected(s.CustomerID, s.ChallengeID);
            }
            else
            {

            }

        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void Claim(ChallengeStatus status)
        {
            ChallengeStatus s = StatusRepo.Get(status.CustomerID, status.ChallengeID);
            //Challenge c = ChalRepo.Get(status.ChallengeID);

            if (s.CustomerID != ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID)
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            s.Status = (int)ChallengeStatus.StatusCodes.ClaimSubmitted;
            StatusRepo.Update(s);

            // close the bidding on this challenge until the claim can be verified.
            //c.State = (int)Challenge.ChallengeState.BidsClosed;
            //ChalRepo.Update(c);
            
            // set all of the bids for this challenge to "VotePending"
            // so the bidders can see what they need to vote on
            BidRepo.UpdateStatusForBidsOnChallenge(status.ChallengeID, ChallengeBid.BidStatusCodes.VotePending);
            
            CustomerNotifier.NotifyChallengeClaimed(s.ChallengeOriginatorCustomerID, s.CustomerID, s.ChallengeID);

            try
            {
                Activity activity = new Activity(s.ChallengeID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityClaimDone, CustomerID = s.CustomerID };
                RepoFactory.GetActivityRepo().Add(activity);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Activity Exception: " + e.ToString());
            }
        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void Accept(ChallengeStatus status)
        {
            Challenge c = ChalRepo.Get(status.ChallengeID);
            //ChallengeStatus s = StatusRepo.Get(((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID, status.ChallengeID);
            
            if (status.CustomerID != c.TargetCustomerID)
                throw new HttpResponseException(System.Net.HttpStatusCode.NotFound);

            if (!Security.CanManipulateContent(c))
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            ChallengeStatus newStatus = new ChallengeStatus();
            newStatus.ChallengeID = c.ID;
            newStatus.ChallengeOriginatorCustomerID = c.CustomerID;
            newStatus.CustomerID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;
            newStatus.Status = (int)ChallengeStatus.StatusCodes.Accepted;

            // add a friendship between these folk if one doesn't exist.
            if (!FriendRepo.CustomersAreFriends(c.CustomerID, ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID))
                FriendRepo.Add(c.CustomerID, ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID);

            c.State = (int)Challenge.ChallengeState.Accepted;
            ChalRepo.Update(c);

            CustomerNotifier.NotifyChallengeAccepted(c.CustomerID, c.TargetCustomerID, c.ID);

            try
            {
                Activity activity = new Activity(c.ID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityTakeDare, CustomerID = newStatus.CustomerID };
                RepoFactory.GetActivityRepo().Add(activity);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Activity Exception: " + e.ToString());
            }
        }

        // Reject is specifically for when a Challenge has been sent directly to you
        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void Reject(ChallengeStatus status)
        {
            Challenge c = ChalRepo.Get((int)status.ChallengeID);

            if (c.TargetCustomerID != ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID)
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            if (!Security.CanManipulateContent(c))
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            // the recipient has rejected this challenge. it dies here.
            // the creator will have to make a new one to re-issue it.
            c.State = (int)Challenge.ChallengeState.Rejected;

            try
            {
                ChalRepo.Update(c);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Reject Challenge Exception: " + e.ToString() + " status: "+status.ToString());
                throw new HttpResponseException(System.Net.HttpStatusCode.InternalServerError);
            }

            CustomerNotifier.NotifyChallengeRejected(c.CustomerID, c.TargetCustomerID, c.ID);

            try
            {
                Activity activity = new Activity(c.ID, DateTime.UtcNow) { Type = (int)ActivityType.ActivityRejectDare, CustomerID = c.TargetCustomerID };
                RepoFactory.GetActivityRepo().Add(activity);
            }
            catch (Exception e)
            {
                System.Diagnostics.Trace.WriteLine("Activity Exception: " + e.ToString());
            }
        }
    }
}