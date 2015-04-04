using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;

namespace Robot
{
    public partial class PortSetting : Form
    {
        public PortSetting()
        {
            InitializeComponent();
        }

        private void OKButton_Click(object sender, EventArgs e)
        {
            Form1 form1 = (Form1)this.Owner;

            try
            {
                form1.PortInfo.Baudrate = int.Parse(comboBox2.SelectedItem.ToString());
                form1.PortInfo.DataBits = int.Parse(comboBox4.SelectedItem.ToString());
                form1.PortInfo.PARITY = (Parity)comboBox3.SelectedIndex;
                form1.PortInfo.STOPBITS = (StopBits)comboBox5.SelectedIndex;
            }
            catch (NullReferenceException)
            {
                //不理会，以后再赋给串口
            }

            this.Close();
        }

        private void cancelButton_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
