using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestProj
{
    class Program
    {
        static void Main(string[] args)
        {
            Common.DBConnection conn = new Common.DBConnection(".\\SQLEXPRESS", "nxmBackup", "nxm", "test123");

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            parameters.Add("@id", "2");

            List<Dictionary<string, string>> retVal = conn.singleQuery("SELECT * FROM Compression WHERE id= @id", parameters);
        }
    }
}
