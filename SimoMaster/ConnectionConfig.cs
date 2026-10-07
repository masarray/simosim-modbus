using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimoMaster
{
    public class ConnectionConfig
    {
        public string Mode { get; set; } // TCP / RTU
        public string IpAddress { get; set; }
        public byte UnitId { get; set; }

        // RTU
        public string ComPort { get; set; }
        public int BaudRate { get; set; }
        public System.IO.Ports.Parity Parity { get; set; }
        public System.IO.Ports.StopBits StopBits { get; set; }
    }
}
