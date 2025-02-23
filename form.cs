using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace Winsonic_ModbusTCP
{
    public partial class Form1 : Form
    {
        string ipAddress = string.Empty;
        static int port;
        TcpClient tcpClient;
        public static string[] btn_str;
        public Form1()
        {
            InitializeComponent();
            btn_str = new string[10];
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            timer2.Enabled = true;
            ipAddress = txtIPAddress.Text.ToString();
            int.TryParse(txtPort.Text.ToString(), out port);
            //MessageBox.Show("port is parsed!");
            tcpClient = new TcpClient();
        }


        private void btnReadByTimer_Click(object sender, EventArgs e)
        {
            btnReadIO.Enabled = false;
            int.TryParse(txtInterval.Text.ToString(), out int a);
            timer1.Interval = a;
            timer1.Enabled = true;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            btnDI_1_Color.BackColor = SystemColors.Control;
            btnDI_2_Color.BackColor = SystemColors.Control;
            btnDI_3_Color.BackColor = SystemColors.Control;
            btnDI_4_Color.BackColor = SystemColors.Control;
            btnDI_5_Color.BackColor = SystemColors.Control;
            btnDI_6_Color.BackColor = SystemColors.Control;
            btnDI_7_Color.BackColor = SystemColors.Control;
            try
            {
                using (TcpClient tcpClient = new TcpClient()) // Khi dùng từ khóa using thì không Compiler sẽ tự động đóng và giải phóng bộ nhớ cấp phát TCPClient ở đây. Không cần đóng tcpClient. Nếu không dùng thì sau khi dùng xong sẽ phải đóng TCPClient nếu đã mở nó.
                
                {
                    tcpClient.Connect(IPAddress.Parse(ipAddress), port);
                    NetworkStream stream = tcpClient.GetStream();
                    //txtResult.AppendText($"Connected to {txtIPAddress.ToString()}");
                    ushort.TryParse(txtNoOfBit.Text.ToString(), out ushort noOfBit);
                    // Read 8 Discrete Inputs (1x) starting from address 0x0014 (20)
                    byte[] readInputRequest = ModbusLibs.CreateModbusRequest(2, 0x02, 0x0100, noOfBit);
                    stream.Write(readInputRequest, 0, readInputRequest.Length);
                    byte[] inputResponse = new byte[12]; // Response buffer



                    stream.Read(inputResponse, 0, inputResponse.Length);

                    txtResult.AppendText(BitConverter.ToString(inputResponse) + "\n");
                    txtResult.AppendText(Environment.NewLine);

                    int y;
                    int.TryParse(inputResponse[9].ToString(), out y);

                    btn_str[0] = Convert.ToString(y, 2);
                    btn_str[0] = btn_str[0].PadLeft(8, '0');
                    //MessageBox.Show(btn_str[0]);

                    this.Invoke(new Action(() => {
                        int num = btn_str[0].Length;

                        if (btn_str[0][num - 1] == '1')
                        {
                            btnDI_1_Color.BackColor = Color.Green;
                        }
                        else btnDI_1_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 2] == '1')
                        {
                            btnDI_2_Color.BackColor = Color.Green;
                        }
                        else btnDI_2_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 3] == '1')
                        {
                            btnDI_3_Color.BackColor = Color.Green;
                        }
                        else btnDI_3_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 4] == '1')
                        {
                            btnDI_4_Color.BackColor = Color.Green;
                        }
                        else btnDI_4_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 5] == '1')
                        {
                            btnDI_5_Color.BackColor = Color.Green;
                        }
                        else btnDI_5_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 6] == '1')
                        {
                            btnDI_6_Color.BackColor = Color.Green;
                        }
                        else btnDI_6_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 7] == '1')
                        {
                            btnDI_7_Color.BackColor = Color.Green;
                        }
                        else btnDI_7_Color.UseVisualStyleBackColor = true;

                        Thread.Sleep(2);
                    }));


                }

            }
            catch (Exception ex) { 
                timer1.Enabled=false;
                MessageBox.Show(ex.Message); }
        }


        private void btnStopTimer_Click(object sender, EventArgs e)
        {
            btnReadIO.Enabled = true;
            timer1.Enabled = false;
        }

        private void btnReadIO_Click(object sender, EventArgs e)
        {
            btnDI_1_Color.BackColor = SystemColors.Control;
            btnDI_2_Color.BackColor = SystemColors.Control;
            btnDI_3_Color.BackColor = SystemColors.Control;
            btnDI_4_Color.BackColor = SystemColors.Control;
            btnDI_5_Color.BackColor = SystemColors.Control;
            btnDI_6_Color.BackColor = SystemColors.Control;
            btnDI_7_Color.BackColor = SystemColors.Control;
            try
            {
                using (TcpClient tcpClient = new TcpClient())
                {
                    tcpClient.Connect(IPAddress.Parse(ipAddress), port);
                    NetworkStream stream = tcpClient.GetStream();
                    //txtResult.AppendText($"Connected to {txtIPAddress.ToString()}");

                    // Read 8 Discrete Inputs (1x) starting from address 0x0014 (20)
                    byte[] readInputRequest = ModbusLibs.CreateModbusRequest(2, 0x02, 0x0100, 16);
                    stream.Write(readInputRequest, 0, readInputRequest.Length);
                    byte[] inputResponse = new byte[12]; // Response buffer



                    stream.Read(inputResponse, 0, inputResponse.Length);
                    txtResult.AppendText(BitConverter.ToString(inputResponse) + "\n");
                    txtResult.AppendText(Environment.NewLine);

                    int y;
                    int.TryParse(inputResponse[9].ToString(), out y);

                    btn_str[0] = Convert.ToString(y, 2);
                    btn_str[0] = btn_str[0].PadLeft(8, '0');
                    //MessageBox.Show(btn_str[0]);

                    this.Invoke(new Action(() => {
                        int num = btn_str[0].Length;

                        if (btn_str[0][num - 1] == '1')
                        {
                            btnDI_1_Color.BackColor = Color.Green;
                        }
                        else btnDI_1_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 2] == '1')
                        {
                            btnDI_2_Color.BackColor = Color.Green;
                        }
                        else  btnDI_2_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 3] == '1')
                        {
                            btnDI_3_Color.BackColor = Color.Green;
                        }
                        else btnDI_3_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 4] == '1')
                        {
                            btnDI_4_Color.BackColor = Color.Green;
                        }
                        else btnDI_4_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 5] == '1')
                        {
                            btnDI_5_Color.BackColor = Color.Green;
                        }
                        else btnDI_5_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 6] == '1')
                        {
                            btnDI_6_Color.BackColor = Color.Green;
                        }
                        else btnDI_6_Color.UseVisualStyleBackColor = true;

                        if (btn_str[0][num - 7] == '1')
                        {
                            btnDI_7_Color.BackColor = Color.Green;
                        }
                        else btnDI_7_Color.UseVisualStyleBackColor = true;

                        Thread.Sleep(2);
                    }));

                }

            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (tcpClient != null) tcpClient.Close();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            if (tcpClient != null) tcpClient.Close();
            this.Close();
        }

        private void btnReset_Click(object sender, EventArgs e)
        {
            txtResult.Clear();
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            txtResult.Text = "";
        }
    }
}
