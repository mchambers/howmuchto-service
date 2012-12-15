using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HowMuchTo.Models
{
    public interface IEvidenceRepository
    {
        List<Evidence> GetAllForChallengeStatus(ChallengeStatus status);
        void Add(Evidence e);
    }
}