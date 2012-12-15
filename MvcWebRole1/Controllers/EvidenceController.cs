using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using HowMuchTo.Models;

namespace HowMuchTo.Controllers
{
    public class EvidenceController : ApiController
    {
        private IEvidenceRepository EvidenceRepo;

        public EvidenceController()
        {
            EvidenceRepo = RepoFactory.GetEvidenceRepo();
        }

        // GET /api/<controller>/5
        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Public)]
        public List<Evidence> Get(long id, long CustomerID)
        {
            ChallengeStatus s = new ChallengeStatus();
            s.CustomerID = CustomerID;
            s.ChallengeID = id;
            return EvidenceRepo.GetAllForChallengeStatus(s);
        }

        // POST /api/<controller>
        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Users)]
        public void Post(Evidence value)
        {
            if (value.ChallengeID == 0)
                throw new HttpResponseException(System.Net.HttpStatusCode.Forbidden);

            if (value.Content == null)
                value.Content = "";

            if (value.MediaURL == null)
                value.MediaURL = "";

            value.UniqueID = System.Guid.NewGuid().ToString();

            EvidenceRepo.Add(value);
        }
    }
}