using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace TestProj
{
    class Program
    {
        static void Main(string[] args)
        {
            Common.DBConnection conn = new Common.DBConnection(".\\SQLEXPRESS", "nxmBackup", "nxm", "test123");

            SqlTransaction transaction = conn.beginTransaction();

            for (int i = 0; i < 10; i++)
            {
                Dictionary<string, string> parameters = new Dictionary<string, string>();
                parameters.Add("@name", "TestJob" + i.ToString());

                List<Dictionary<string, string>> retVal = conn.doQuery("INSERT INTO compression (name) VALUES (@name)", parameters, transaction);
            }

            transaction.Rollback();

            conn.Dispose();

        }
    }
}
