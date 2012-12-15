using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace HowMuchTo.Models
{
    public class CustomerNotification
    {
        public long CustomerID { get; set; }
        public string NotificationID { get; set; }
        public string Text { get; set; }
        public int Type { get; set; }
        public long ChallengeID { get; set; }
        public int Read { get; set; }
    }
}
