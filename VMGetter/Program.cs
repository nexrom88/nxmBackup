using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VMGetter
{
    class Program
    {
        static void Main(string[] args)
        {
            List<string> vms = VSSHelper.WMIHelper.listVMs();

            foreach (string vm in vms)
            {
                Console.WriteLine(vm);
            }

        }
    }
}
