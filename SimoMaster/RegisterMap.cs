using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SimoMaster
{
    public class RegisterMap
    {
        public ushort StatusWord { get; set; } = 1024;

        public ushort CurrentStart { get; set; } = 2055;
        public ushort VoltageStart { get; set; } = 2060;

        public ushort ActivePower { get; set; } = 2070;
        public ushort ApparentPower { get; set; } = 2071;

        public bool PlcBaseAddressing { get; set; } = false;

        public ushort CoilOpen { get; set; } = 0;
        public ushort CoilClose { get; set; } = 1;
        public ushort CoilStart { get; set; } = 2;
        public ushort CoilStop { get; set; } = 3;
        public ushort CoilFault { get; set; } = 4;
        public ushort CoilReset { get; set; } = 5;

        public bool PollStatusWord { get; set; } = true;
        public bool PollCurrent { get; set; } = true;
        public bool PollVoltage { get; set; } = true;
        public bool PollPower { get; set; } = true;
    }
}