using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace MFUserMode
{


    public class MFUserModeWrapper
    {

        private static string portName = "\\nxmQueryPort";

        [DllImport("fltlib", SetLastError = true)]
        public static extern int FilterConnectCommunicationPort
            ([MarshalAs(UnmanagedType.LPWStr)]
            string portName,
            uint options,
            IntPtr context,
            uint sizeOfContext,
            IntPtr securityAttributes,
            IntPtr portPtr);

        public MFUserModeWrapper()
        {

        }

        //connects to the minifilter driver within kernel mode
        public void connectToMF()
        {
            IntPtr port = new IntPtr();
            FilterConnectCommunicationPort(portName, 0, new IntPtr(), 0, new IntPtr(), port);
        }

    }
}
