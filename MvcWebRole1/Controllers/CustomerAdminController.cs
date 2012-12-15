using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using HowMuchTo.Models;
using System.Web;

namespace HowMuchTo.Controllers
{
    public class CustomerAdminController : ApiController
    {
        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Admin)]
        public Customer Get(long id)
        {
            return RepoFactory.GetCustomerRepo().GetWithID(id);
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Admin)]
        public Customer GetWithEmail(string email)
        {
            return RepoFactory.GetCustomerRepo().GetWithEmailAddress(email.Trim().ToLower());
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Admin)]
        public IEnumerable<Customer> List(int StartAt = 0, int Amount = 10)
        {
            return RepoFactory.GetCustomerRepo().List(StartAt, StartAt + Amount);
        }

        [HttpGet]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Admin)]
        public List<Transaction> Transactions(long id, int StartAt=0, int Amount=10)
        {
            ITransactionRepository transRepo = RepoFactory.GetTransactionRepo();

            return transRepo.TransactionsForCustomer(id, StartAt, Amount).ToList<Transaction>();
        }

        [HttpPost]
        [HowMuchTo.Filters.DYAuthorization(Filters.DYAuthorizationRoles.Admin)]
        public void Transaction(Transaction t)
        {
            if (t.ID != 0)
                t.ID = 0;

            t.AgentID = ((DareyaIdentity)HttpContext.Current.User.Identity).CustomerID;

            RepoFactory.GetTransactionRepo().RecordTransaction(t);
        }

        // POST /api/customeradmin
        public void Post(string value)
        {
        }

        // PUT /api/customeradmin/5
        public void Put(int id, string value)
        {
        }

        // DELETE /api/customeradmin/5
        public void Delete(int id)
        {
        }
    }
}
