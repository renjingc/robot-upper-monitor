using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace Robot
{
    public class PortInfo
    {
        //Private member variables
        private int baudrate;
        private int dataBits;
        private Parity parity;
        private StopBits stopBits;
        private int[] buffer;
        private bool listening;//是否没有执行完invoke相关操作
        private bool closing;//是否正在关闭串口，执行Application.DoEvents，并阻止再次invoke  


        //Public property
        public int Baudrate
        {
            get { return baudrate; }
            set { baudrate = value; }
        }
        public int DataBits
        {
            get { return dataBits; }
            set { dataBits = value; }
        }
        public Parity PARITY
        {
            get { return parity; }
            set { parity = value; }
        }
        public StopBits STOPBITS
        {
            get { return stopBits; }
            set { stopBits = value; }
        }
        public int[] Buffer
        { get { return buffer; } set { buffer = value; } }
        public bool Listening
        { get { return listening; } set { listening = value; } }
        public bool Closing
        { get { return closing; } set { closing = value; } }

        public PortInfo(int br = 115200,
                        int db = 8,
                        Parity p = Parity.None,
                        StopBits sb = StopBits.One)
        {
            baudrate = br;
            dataBits = db;
            parity = p;
            stopBits = sb;

            listening = false;
            closing = false;
        }
    }
}
