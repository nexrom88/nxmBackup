using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data.SqlClient;
using HyperVBackupRCT;

namespace TestProj
{
    class Program
    {
        static void Main(string[] args)
        {
            eventStatus a = eventStatus.successful;
            Console.WriteLine(a.ToString());
            Console.ReadLine();
        }
        public enum eventStatus
        {
            warning, error, inProgress, successful
        }
    }
}
