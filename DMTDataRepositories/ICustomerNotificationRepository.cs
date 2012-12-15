using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HowMuchTo.Models
{
    public interface ICustomerNotificationRepository
    {
        void Add(CustomerNotification notification);
        void ClearAllForCustomer(long CustomerID);
        IEnumerable<CustomerNotification> Get(long CustomerID);
    }
}
