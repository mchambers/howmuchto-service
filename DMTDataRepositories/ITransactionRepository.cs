using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HowMuchTo.Models
{
    public interface ITransactionRepository
    {
        string RecordTransactionBatch(List<Transaction> Transactions, string TransactionGroupID = "");
        bool RecordTransaction(Transaction t);
        bool RecordTransaction(long CustomerID, float Amount, TransactionType Type, TransactionReason Reason);

        IEnumerable<Transaction> TransactionsForCustomer(long CustomerID, int StartAt=0, int Amount=10);
        IEnumerable<Transaction> TransactionsForBid(long BidID);
    }
}
