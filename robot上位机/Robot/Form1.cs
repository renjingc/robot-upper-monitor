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
using System.IO;
using System.Threading;
using System.Text.RegularExpressions;

namespace Robot
{
    public partial class Form1 : Form
    {
        int displayNum = 0;
        #region/* Serial Port Relative */

        private int receiveForm = 1;
        private int sendForm = 1;
        private List<byte> buffer = new List<byte>(4096);//默认分配1页内存，并始终限制不允许超过
        private byte[] binary_data_1 = new byte[10];//AA 44 05 01 02 03 04 05 EA
        private long received_count = 0;//接收计数
        private StringBuilder builder = new StringBuilder();//避免在事件处理方法中反复的创建，定义到外面。
        //Port Information
        public PortInfo portInfo;
        public PortInfo PortInfo
        {
            get { return portInfo; }
            set { portInfo = value; }
        }
        #endregion
        public Form1()
        {
            InitializeComponent();
            BTserialPort = new SerialPort();
            BTserialPort.NewLine = "\r\n";
            //BTserialPort.RtsEnable = true;
            portInfo = new PortInfo(115200, 8, Parity.None, StopBits.One);
            saveFileDialog1 = new SaveFileDialog();
            openFileDialog1 = new OpenFileDialog();
            saveFileDialog1.RestoreDirectory = true;
            openFileDialog1.RestoreDirectory = true;
            AutoFindPort();
            //this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.textBox1_KeyDown);
        }
       /* private void textBox1_KeyDown(object sender, KeyEventArgs e)
        { 
                if (e.KeyData == Keys.A)
                {
                    dataReceiveTextBox.AppendText("a\n");
                }
        }*/
        private void BTserialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {

            if (portInfo.Closing) return;//如果正在关闭，忽略操作，直接返回，尽快的完成串口监听线程的一次循环
            try
            {
                portInfo.Listening = true;//设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。
                int n = BTserialPort.BytesToRead;//先记录下来，避免某种原因，人为的原因，操作几次之间时间长，缓存不一致
                byte[] buf = new byte[n];//声明一个临时数组存储当前来的串口数据
                received_count += n;//增加接收计数
                BTserialPort.Read(buf, 0, n);//读取缓冲数据
                /////////////////////////////////////////////////////////////////////////////////////////////////////////////
                //<协议解析>
                bool data_1_catched = false;//缓存记录数据是否捕获到
                //1.缓存数据
                buffer.AddRange(buf);
                //2.完整性判断
                while (buffer.Count >= 2)//至少要包含头（2字节）+长度（1字节）+校验（1字节）
                {
                    //请不要担心使用>=，因为>=已经和>,<,=一样，是独立操作符，并不是解析成>和=2个符号
                    //2.1 查找数据头
                    if (buffer[0] == 0xAA)
                    {
                        //if (buffer.Count < 10)
                        //break;
                        if (buffer.Count < 10)
                            break;
                        byte checksum = 0;
                        for (int i = 0; i < 9; i++)//len+3表示校验之前的位置
                        {
                            checksum ^= buffer[i];
                        }
                        if (checksum != buffer[9]) //如果数据校验失败，丢弃这一包数据
                        {
                            buffer.RemoveRange(0, 10);//从缓存中删除错误数据
                            continue;//继续下一次循环
                        }
                        //至此，已经被找到了一条完整数据。我们将数据直接分析，或是缓存起来一起分析
                        //我们这里采用的办法是缓存一次，好处就是如果你某种原因，数据堆积在缓存buffer中
                        //已经很多了，那你需要循环的找到最后一组，只分析最新数据，过往数据你已经处理不及时
                        //了，就不要浪费更多时间了，这也是考虑到系统负载能够降低。
                        buffer.CopyTo(0, binary_data_1, 0, 10);//复制一条完整数据到具体的数据缓存
                        data_1_catched = true;
                        buffer.RemoveRange(0, 10);//正确分析一条数据，从缓存中移除数据。
                    }
                    else
                    {
                        //这里是很重要的，如果数据开始不是头，则删除数据
                        //buffer.RemoveAt(0);
                        buffer.RemoveRange(0, 1);
                    }
                    if (buffer[0] == '#')
                    {
                        displayNum = 1;
                    }
                }
                //分析数据
                if (data_1_catched)
                {
                }
                //如果需要别的协议，只要扩展这个data_n_catched就可以了。往往我们协议多的情况下，还会包含数据编号，给来的数据进行
                //编号，协议优化后就是： 头+编号+长度+数据+校验
                //</协议解析>
                /////////////////////////////////////////////////////////////////////////////////////////////////////////////

                builder.Clear();//清除字符串构造器的内容
                //因为要访问ui资源，所以需要使用invoke方式同步ui。
                this.Invoke((EventHandler)(delegate
                {
                    //判断是否是显示为16进制
                    if (receiveForm == 0)
                    {
                        //依次的拼接出16进制字符串
                        foreach (byte b in buf)
                        {
                            builder.Append(b.ToString("X2") + " ");
                        }
                    }
                    else
                    {
                        //直接按ASCII规则转换成字符串
                        builder.Append(Encoding.ASCII.GetString(buf));
                    }
                    if(displayNum==0)
                         //追加的形式添加到文本框末端，并滚动到最后。
                        this.dataReceiveTextBox.AppendText(builder.ToString());
                    else if(displayNum==1)
                        this.functionTextBox.AppendText(builder.ToString());
                }));
            }
            finally
            {
                portInfo.Listening = false;//我用完了，ui可以关闭串口了。
            }
        }
        public void AutoFindPort()
        {
            Boolean display_port = true;//表示是否有可用串口
            portComboBox.Items.Clear();
            foreach (string portname in SerialPort.GetPortNames())//向serial_port加入可用串口号
            {
                if (display_port)
                {
                    display_port = false;
                    portComboBox.Text = portname;
                }
                portComboBox.Items.Add(portname);
            }
        }
        private void openPortButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (portComboBox.SelectedItem == null)
                {
                    MessageBox.Show("无串口！.");
                }
                else
                {
                    if (BTserialPort.IsOpen == true)
                    {
                        portInfo.Closing = true;
                        while (portInfo.Listening) 
                            Application.DoEvents();

                        BTserialPort.DataReceived -= BTserialPort_DataReceived;
                        BTserialPort.Close();
                        openPortButton.Text = "打开串口";
                        portComboBox.Enabled = true;
                        portInfo.Closing = false;
                    }
                    else
                    {
                        //初始化SerialPort对象
                        BTserialPort.NewLine = "\r\n";
                        BTserialPort.RtsEnable = true;//根据实际情况吧。
                        //实例化委托对象
                        BTserialPort.DataReceived += new SerialDataReceivedEventHandler(BTserialPort_DataReceived);

                        BTserialPort.PortName = portComboBox.SelectedItem.ToString();
                        BTserialPort.BaudRate = portInfo.Baudrate;
                        BTserialPort.DataBits = portInfo.DataBits;
                        BTserialPort.Parity = portInfo.PARITY;
                        BTserialPort.StopBits = portInfo.STOPBITS;
                        BTserialPort.Open();
                        openPortButton.Text = "关闭串口";
                        portComboBox.Enabled = false;
                    }
                }
            }
            catch
            {
                MessageBox.Show("Serial port not available or in use now. Please try another port.");
            }
        }

        private void setButton_Click(object sender, EventArgs e)
        {
            PortSetting ps = new PortSetting();
            ps.Owner = this;
            if (ps.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                BTserialPort.BaudRate = portInfo.Baudrate;
                BTserialPort.DataBits = portInfo.DataBits;
                BTserialPort.Parity = portInfo.PARITY;
                BTserialPort.StopBits = portInfo.STOPBITS;
                this.dataReceiveTextBox.AppendText("1\n");
            }
        }
        private void selectReceiveDataForm_Click(object sender, EventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.Name == "receiveHexRadioButton")
            {
                receiveForm = 0;
                //dataReceiveTextBox.Clear();
                //dataReceiveTextBox.AppendText("HEX码");
            }
            else if (rb.Name == "receiveStringRadioButton")
            {
                receiveForm = 1;
                //dataReceiveTextBox.Clear();
                //dataReceiveTextBox.AppendText("字符串");
            }
        }
        private void selectSendDataForm_Click(object sender, EventArgs e)
        {
            RadioButton rb = sender as RadioButton;
            if (rb.Name == "sendHexRadioButton")
            {
                sendForm = 0;
                //dataSendTextBox.Clear();
                //dataSendTextBox.AppendText("HEX码");
            }
            else if (rb.Name == "sendStringRadioButton")
            {
                sendForm = 1;
                //dataSendTextBox.Clear();
                //dataSendTextBox.AppendText("字符串");
            }
        }

        private void clearReceivebutton_Click(object sender, EventArgs e)
        {
            dataReceiveTextBox.Clear();
            functionTextBox.Clear();
        }

        private void clearSendbutton_Click(object sender, EventArgs e)
        {
            dataSendTextBox.Clear();
        }
        public void CarControlButton_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            string send;
            string speed;
            string angle;
            if(BTserialPort.IsOpen)
            {
                switch(btn.Name)
                {
                    case "stopRobotButton":
                        send = "$zs#";
                        BTserialPort.Write(send);
                        break;
                    case "autoRobotButton":
                        send = "$zx$";
                        BTserialPort.Write(send);
                        break;
                    case "frontButton":
                        if (int.Parse(speedTextBox.Text) <= 999 && int.Parse(speedTextBox.Text) >= 0)
                        {
                            speed = speedTextBox.Text;
                            send = "$zq" + speed+"#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("速度没设置好！");
                        }
                        break;
                    case "backButton":
                        if (int.Parse(speedTextBox.Text) <= 999 && int.Parse(speedTextBox.Text) >= 0)
                        {
                            speed = speedTextBox.Text;
                            send = "$zw" + speed + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("速度没设置好！");
                        }
                        break;
                    case "leftButton":
                        if (int.Parse(speedTextBox.Text) <= 999 && int.Parse(speedTextBox.Text) >= 0)
                        {
                            speed = speedTextBox.Text;
                            send = "$ze" + speed + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("速度没设置好！");
                        }
                        break;
                    case "rightButton":
                        if (int.Parse(speedTextBox.Text) <= 999 && int.Parse(speedTextBox.Text) >= 0)
                        {
                            speed = speedTextBox.Text;
                            send = "$zr" + speed + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("速度没设置好！");
                        }
                        break;
                    case "leftStraightButton":
                        if (int.Parse(speedTextBox.Text) <= 999 && int.Parse(speedTextBox.Text) >= 0)
                        {
                            speed = speedTextBox.Text;
                            send = "$zy" + speed + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("速度没设置好！");
                        }
                        break;
                    case "rightStraightButton":
                        if (int.Parse(speedTextBox.Text) <= 999 && int.Parse(speedTextBox.Text) >= 0)
                        {
                            speed = speedTextBox.Text;
                            send = "$zt" + speed + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("速度没设置好！");
                        }
                        break;
                    case "rightBackButton":
                        if (int.Parse(speedTextBox.Text) <= 999 && int.Parse(speedTextBox.Text) >= 0)
                        {
                            speed = speedTextBox.Text;
                            send = "$zu" + speed + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("速度没设置好！");
                        }
                        break;
                    case "leftBackButton":
                        if (int.Parse(speedTextBox.Text) <= 999 && int.Parse(speedTextBox.Text) >= 0)
                        {
                            speed = speedTextBox.Text;
                            send = "$zi" + speed + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("速度没设置好！");
                        }
                        break;
                    case "rightRoateButton":
                        if (int.Parse(rightRoateTextBox.Text) <= 360 && int.Parse(rightRoateTextBox.Text) >= 0)
                        {
                            angle = rightRoateTextBox.Text;
                            send = "$zo" + angle + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("角度没设置好！");
                        }
                        break;
                    case "leftRoateButton":
                        if (int.Parse(leftRoateTextBox.Text) <= 360 && int.Parse(leftRoateTextBox.Text) >= 0)
                        {
                            angle = leftRoateTextBox.Text;
                            send = "$zp" + angle + "#";
                            BTserialPort.Write(send);
                        }
                        else
                        {
                            MessageBox.Show("角度没设置好！");
                        }
                        break;
                    default:
                        break;
                }

            }
            else
            {
                MessageBox.Show("串口未打开，请打开串口!");
            }
        }
        public void steerControlButton_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            string send="$c";
            string[] steerPWM=new string[12];
            switch(btn.Name)
            { 
                case "steerOpenButton":
                    break;
                case "steerSaveButton":
                    break;
                case "steerInitButton":
                    if (BTserialPort.IsOpen)
                    {
                        steerTextBox0.Text = "75";
                        steerTextBox1.Text = "75";
                        steerTextBox2.Text = "75";
                        steerTextBox3.Text = "75";
                        steerTextBox4.Text = "75";
                        steerTextBox5.Text = "75";
                        steerTextBox6.Text = "75";
                        steerTextBox7.Text = "75";
                        steerTextBox8.Text = "75";
                        steerTextBox9.Text = "75";
                        steerTextBox10.Text = "75";
                        steerTextBox11.Text = "75";
                        send += "75,75,75,75,75,75,75,75,75,75,75,75#";
                        BTserialPort.Write(send);
                    }
                    else
                    {
                        MessageBox.Show("串口未打开，请打开串口!");
                    }
                    break;
                case "steerSendDataButton":
                    if (BTserialPort.IsOpen)
                    {
                        steerPWM[0] = steerTextBox0.Text;
                        steerPWM[1] = steerTextBox1.Text;
                        steerPWM[2] = steerTextBox2.Text;
                        steerPWM[3] = steerTextBox3.Text;
                        steerPWM[4] = steerTextBox4.Text;
                        steerPWM[5] = steerTextBox5.Text;
                        steerPWM[6] = steerTextBox6.Text;
                        steerPWM[7] = steerTextBox7.Text;
                        steerPWM[8] = steerTextBox8.Text;
                        steerPWM[9] = steerTextBox9.Text;
                        steerPWM[10] = steerTextBox10.Text;
                        steerPWM[11] = steerTextBox11.Text;
                        send = send + steerPWM[0] + "," + steerPWM[1] + ","+ steerPWM[2] + "," + steerPWM[3] + "," + steerPWM[4]
                            + "," + steerPWM[5] + "," + steerPWM[6] + "," + steerPWM[7] + "," + steerPWM[8] + "," + steerPWM[9]
                            + "," + steerPWM[10] + "," + steerPWM[11] + "#";
                        BTserialPort.Write(send);
                    }
                    else
                    {
                        MessageBox.Show("串口未打开，请打开串口!");
                    }
                    break;
                default: break;
            }
        }
        public void functionControlButton_Click(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            string send = "$s";
            switch (btn.Name)
            {
                case "registerFunctionButton":
                    break;
                case "displaySteerButton":
                    send += "@#";
                    BTserialPort.Write(send);
                    break;
                case "clearFunctionButton":
                    send += "i#";
                    BTserialPort.Write(send);
                    break;
                case "testFunctionButton":
                    send += "t#";
                    BTserialPort.Write(send);
                    break;
                case "saveFunctionButton":
                    break;
                case "openFunctionButton":
                    break;
                default: break;
            }
        }
        private void freshPortButton_Click(object sender, EventArgs e)
        {
            AutoFindPort();
        }

        private void sendButton_Click(object sender, EventArgs e)
        {
            //定义一个变量，记录发送了几个字节
            if (BTserialPort.IsOpen)
            {
                int n = 0;
                //16进制发送
                if (sendForm == 0)
                {
                    //我们不管规则了。如果写错了一些，我们允许的，只用正则得到有效的十六进制数
                    MatchCollection mc = Regex.Matches(dataSendTextBox.Text, @"(?i)[\da-f]{2}");
                    List<byte> buf = new List<byte>();//填充到这个临时列表中
                    //依次添加到列表中
                    foreach (Match m in mc)
                    {
                        buf.Add(byte.Parse(m.Value, System.Globalization.NumberStyles.HexNumber));
                    }
                    //转换列表为数组后发送
                    BTserialPort.Write(buf.ToArray(), 0, buf.Count);
                    //记录发送的字节数
                    n = buf.Count;
                }
                else//ascii编码直接发送
                {
                    //包含换行符
                    //if (checkBoxNewlineSend.Checked)
                    //{
                    //    serialPort1.WriteLine(dataSendTextBox.Text);
                    //    n = dataSendTextBox.Text.Length + 2;
                    //}
                    //else//不包含换行符
                    //{
                    BTserialPort.Write(dataSendTextBox.Text);
                    n = dataSendTextBox.Text.Length;
                    //}
                }
                //labelSendCount.Text = "Send:" + send_count.ToString();//更新界面
            }
            else
            {
                MessageBox.Show("串口未打开，请打开串口!");
            }
        }
    }
}
