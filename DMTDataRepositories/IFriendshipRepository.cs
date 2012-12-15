using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HowMuchTo.Models
{
    public interface IFriendshipRepository
    {
        bool CustomersAreFriends(long Customer1ID, long Customer2ID);
        void Add(long Customer1ID, long Customer2ID);
    }
}
