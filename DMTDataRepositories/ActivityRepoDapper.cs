using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Data.SqlClient;
using System.Data;
using Dapper;
using System.Configuration;

namespace HowMuchTo.Models
{
    class ActivityRepoDapper : IActivityRepository
    {
        private string connStr;

        private DynamicParameters ActivityToDynParm(Activity a, bool inclID = false)
        {
            var p = new DynamicParameters();

            p.Add("@Type", a.Type, DbType.Int32, ParameterDirection.Input);
            p.Add("@SourceCustomer", a.SourceCustomer, DbType.Int64, ParameterDirection.Input);
            p.Add("@TargetObject", a.TargetObject, DbType.Int64, ParameterDirection.Input);
            p.Add("@Content", a.Content, DbType.String, ParameterDirection.Input);
            p.Add("@MediaURL", a.MediaURL, DbType.String, ParameterDirection.Input);
            p.Add("@Created", a.Created, DbType.DateTime, ParameterDirection.Input);
            if (inclID)
                p.Add("@ID", a.ID, DbType.Int64, ParameterDirection.Input);

            return p;
        }

        public ActivityRepoDapper()
        {
            connStr = ConfigurationManager.ConnectionStrings["DMTPrimary"].ConnectionString;
        }

        public IEnumerable<Activity> GetActivityForChallenge(long ChallengeID)
        {
            using (SqlConnection db = new SqlConnection(connStr))
            {
                db.Open();
                var activities = db.Query<Activity>("spActivityGetForChallenge", new { ChallengeID = ChallengeID }, commandType: CommandType.StoredProcedure);
                return activities;
            }
        }

        public void Add(Activity value)
        {
            using (SqlConnection db = new SqlConnection(connStr))
            {
                db.Open();
                db.Execute("spActivityAdd", ActivityToDynParm(value, false), commandType: CommandType.StoredProcedure);
            }
        }

        public Activity GetMostRecentForChallenge(long ChallengeID)
        {
            throw new NotImplementedException();
        }
  
        public int CountTypeForChallenge(long ChallengeID, int Type)
        {
            using (SqlConnection db = new SqlConnection(connStr))
            {
                db.Open();

                DynamicParameters p = new DynamicParameters();
                p.Add("@ChallengeID", ChallengeID, DbType.Int64, ParameterDirection.Input);
                p.Add("@Type", Type, DbType.Int32, ParameterDirection.Input);
                p.Add("@Count", dbType: DbType.Int32, direction: ParameterDirection.Output);

                db.Execute("spActivityGetTypeCountForChallenge", p, commandType: CommandType.StoredProcedure);
                return p.Get<int>("@Count");
            }
        }

        public IEnumerable<Activity> GetActivityForChallengeByType(long ChallengeID, int Type)
        {
            using (SqlConnection db = new SqlConnection(connStr))
            {
                db.Open();
                var activities = db.Query<Activity>("spActivityGetByTypeForChallenge", new { Type = Type, TargetObject = ChallengeID }, commandType: CommandType.StoredProcedure);
                return activities;
            }
        }
    }
}
