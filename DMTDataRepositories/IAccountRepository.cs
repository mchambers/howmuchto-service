using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HowMuchTo.Models
{
    public interface IAccountRepository
    {
        decimal BalanceForCustomerAccount(long CustomerID);
        bool ModifyCustomerAccountBalance(long CustomerID, decimal Amount);
        bool TransferFundsForTransaction(Transaction t);
        void AddToFeesCollectedAccount(decimal Amount);
        long CustomerIDForFeesAccount();
    }
}