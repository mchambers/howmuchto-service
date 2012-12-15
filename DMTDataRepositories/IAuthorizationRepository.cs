using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HowMuchTo.Models
{
    public interface IAuthorizationRepository
    {
        Authorization GetWithToken(String Token);
        void Add(Authorization a);
        void Remove(String Token);
    }
}