using MetroFramework.Forms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO.Ports;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZedGraph;

namespace fingerPressure
{
    public partial class Form1 : MetroForm
    {
        SerialPort serialPort = new SerialPort();
        Dictionary<int, RollingPointPairList> channelData = new();
        Dictionary<int, LineItem> channelCurves = new();
        int xCount = 0;

        public Form1()
        {
            InitializeComponent();
            InitGraph();
            LoadPorts();
        }

        private void LoadPorts()
        {
            comboBoxPort.Items.AddRange(SerialPort.GetPortNames());
            if (comboBoxPort.Items.Count > 0)
                comboBoxPort.SelectedIndex = 0;
        }

        private void buttonOpen_Click(object sender, EventArgs e)
        {
            if (!serialPort.IsOpen)
            {
                try
                {
                    serialPort.PortName = comboBoxPort.Text;
                    serialPort.BaudRate = 115200;
                    serialPort.DataBits = 8;
                    serialPort.Parity = Parity.None;
                    serialPort.StopBits = StopBits.One;
                    serialPort.DataReceived += SerialPort_DataReceived;
                    serialPort.Open();
                    buttonOpen.Text = "关闭串口";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开串口失败: " + ex.Message);
                }
            }
            else
            {
                serialPort.Close();
                buttonOpen.Text = "打开串口";
            }
        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string line = serialPort.ReadLine();

                Match match = Regex.Match(line, @"multi\s+(\d),.*?([-\d.]+)V");
                if (match.Success)
                {
                    int channel = int.Parse(match.Groups[1].Value);
                    double voltage = double.Parse(match.Groups[2].Value);

                    this.BeginInvoke(new Action(() =>
                    {
                        UpdateGraph(channel, voltage);
                    }));
                }
            }
            catch { /* 忽略异常或断包 */ }
        }

        private void InitGraph()
        {
            GraphPane pane = zedGraphControl1.GraphPane;
            pane.Title.Text = "串口示波器";
            pane.XAxis.Title.Text = "采样点";
            pane.YAxis.Title.Text = "电压 (V)";
            pane.YAxis.Scale.Min = -1;
            pane.YAxis.Scale.Max = 1;
        }

        private void UpdateGraph(int channel, double voltage)
        {
            if (!channelData.ContainsKey(channel))
            {
                var list = new RollingPointPairList(2000);
                var curve = zedGraphControl1.GraphPane.AddCurve(
                    $"CH{channel}", list, GetColor(channel), SymbolType.None);
                channelData[channel] = list;
                channelCurves[channel] = curve;
            }

            channelData[channel].Add(xCount, voltage);
            if (channelData[channel].Count > 2000)
                channelData[channel].RemoveAt(0);

            xCount++;
            zedGraphControl1.Invalidate();
        }

        private Color GetColor(int channel)
        {
            Color[] colors = new Color[]
            {
                Color.Red, Color.Green, Color.Blue, Color.Orange,
                Color.Magenta, Color.Cyan, Color.Brown, Color.Black
            };
            return colors[channel % colors.Length];
        }
    }
}
