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
            Common.DBConnection conn = new Common.DBConnection();

            List<Dictionary<string, string>> retVal = conn.doQuery("SELECT Count(*) AS count FROM compression", null, null);

            conn.Dispose();

        }
    }
}
