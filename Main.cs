using fingerPressure.MODEL;
using System.Text.Json;
using System.Windows.Forms;
using System.IO.Ports;
using System.Text.RegularExpressions;
using ZedGraph;
using MetroFramework.Forms;
using System.Text;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.ML.OnnxRuntime;

namespace fingerPressure
{
    public partial class Main : MetroForm
    {
        private SerialPort serialPort = new SerialPort();
        //private Dictionary<int, RollingPointPairList> channelData = new();
        //private Dictionary<int, PointPairList> channelData = new();
        private Dictionary<int, RollingPointPairList> channelData = new();
        private Dictionary<int, RollingPointPairList> channelData2 = new();
        private Dictionary<int, LineItem> channelCurves = new();
        private Dictionary<int, LineItem> channelCurves2 = new();
        private List<string> currentPacketLines = new();
        private int MaxVisiblePackets = 500;
        private int saveRate = 100;
        private GraphPane selectedChannelPane;
        private PointPairList selectedChannelList = new();
        private Random rand = new Random();

        // 每通道图的数据
        //private Dictionary<int, RollingPointPairList> singleChannelData = new Dictionary<int, RollingPointPairList>();
        private Dictionary<int, PointPairList> singleChannelData = new Dictionary<int, PointPairList>();

        private string serialBuffer = "";
        int packetIndex = 0;
        private int selectedRealtimeChannel = 0;

        //校零
        private double[] channelZeroOffsets = new double[64]; // 默认全为 0.0
        private double[] channelZeroOffsets2 = new double[64]; // 默认全为 0.0
        private double xielv;
        // 用于暂存每个通道的前5个电压值
        private Dictionary<int, Queue<double>> zeroCalibBuffers = new Dictionary<int, Queue<double>>();
        private Dictionary<int, Queue<double>> zeroCalibBuffers2 = new Dictionary<int, Queue<double>>();
        private const int ZeroCalibSampleCount = 5;

        private System.Windows.Forms.Timer simulationTimer;
        private System.Windows.Forms.Timer refreshTimer;
        private int logSampleCounter = 0;
        private int flashCounter = 0;
        private const int LogSampleRate = 30; // 每50包打印一次
        //private const int FlashRate = 5;
        private double[] tempValues = new double[64];
        private double[] pressureValues = new double[64];

        private bool yalitu = false;
        private bool wendutu = false;
        private bool diantu = false;

        private readonly object serialLock = new object(); // 锁，保证线程安全
        //private BlockingCollection<List<string>> packetQueue;
        //private BlockingCollection<List<string>> uiQueue = new BlockingCollection<List<string>>(new ConcurrentQueue<List<string>>());

        // 文件写入用队列
        //private BlockingCollection<List<string>> fileQueue = new BlockingCollection<List<string>>(new ConcurrentQueue<List<string>>());
        // 存储解析后的曲线更新数据
        private ConcurrentQueue<GraphUpdate> graphQueue = new ConcurrentQueue<GraphUpdate>();

        // 存储点阵刷新数据
        private ConcurrentQueue<DotMatrixUpdate> dotQueue = new ConcurrentQueue<DotMatrixUpdate>();
        private StreamWriter packetWriter;
        private Thread fileWriterThread;
        private bool isRunning = true;
        private int packetCounter = 0;

        //采集频率
        private double sampleFrequencyHz = 50; // 默认采集频率 50Hz，可以在界面输入
        private DateTime lastSampleTime = DateTime.MinValue;
        private int flashTime = 50;
        private string excelSavePath = "";

        // 校零控制
        private bool isZeroing = false;
        private int zeroingPacketCount = 0;
        private const int ZeroingTargetPackets = 5;
        private Dictionary<int, List<double>> tempCalibBuffers = new();
        private Dictionary<int, List<double>> pressureCalibBuffers = new();

        private Dictionary<int, Queue<double>> channelBuffers = new Dictionary<int, Queue<double>>();

        private DateTime lastSaveTime = DateTime.Now;
        private object saveLock = new object();

        private const int MaxQueueSize = 200;
        private BlockingCollection<List<string>> uiQueue = new BlockingCollection<List<string>>(MaxQueueSize);

        private long totalPacketCount = 0;
        private long savedPacketCount = 0;

        // 后台线程控制
        private Thread serialThread;
        private CancellationTokenSource cts;

        // 原 fileQueue 改成装“已格式化的一行字符串”
        private readonly BlockingCollection<string> fileQueue =
            new BlockingCollection<string>(new ConcurrentQueue<string>(), 20000);

        // 新增：原始包（List<string>，9 行）的队列，给格式化线程消费
        private readonly BlockingCollection<List<string>> fileRawQueue =
            new BlockingCollection<List<string>>(new ConcurrentQueue<List<string>>(), 20000);

        private Thread formatThread;


        /*        
                private int[,] sensorValues = new int[5, 8];  // 5手指×8通道
                private PointF[,] fingerPoints = new PointF[5, 8]; // 每个通道在原图上的坐标
                private Image handImage;*/
        private int[,] memsData = new int[5, 8];
        private int[,] yingbianhuaData = new int[5, 27];
        private string chuanGanQiType = "";
        private System.Windows.Forms.Timer timer;


        class GraphUpdate
        {
            public int Channel;
            public double Pressure;  // 曲线值
            public int Index;
        }

        class DotMatrixUpdate
        {
            public double[] TempValues;
            public double[] PressureValues;
        }
        #region 模拟

        /// <summary>
        /// 启动模拟器
        /// </summary>
        private void StartSimulation()
        {
            simulationTimer = new System.Windows.Forms.Timer();
            simulationTimer.Interval = 10; // 每 200ms 产生一帧（5Hz）
            simulationTimer.Tick += SimulationTimer_Tick;
            simulationTimer.Start();
        }

        /// <summary>
        /// 停止模拟器
        /// </summary>
        private void StopSimulation()
        {
            if (simulationTimer != null)
            {
                simulationTimer.Stop();
                simulationTimer.Dispose();
                simulationTimer = null;
            }
        }

        /// <summary>
        /// 每次 Tick 生成一帧模拟数据
        /// </summary>
        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            List<string> lines = new List<string>();

            // 第一行：计时器
            lines.Add(packetIndex.ToString());

            // 剩下 8 行：64 通道，每行 8 个通道（温度、压力）
            for (int row = 0; row < 8; row++)
            {
                StringBuilder sb = new StringBuilder();
                for (int col = 0; col < 8; col++)
                {
                    int temp = rand.Next(20, 50);       // 模拟温度 (20~50)
                    int pressure = rand.Next(200, 400); // 模拟压力 (200~400)

                    // 插入模拟毛刺：例如每行随机 1 个点加一个大偏差
                    if (rand.NextDouble() < 0.1) // 10% 概率生成毛刺
                    {
                        pressure += rand.Next(1000, 2000); // 毛刺值远高于正常值
                    }

                    sb.AppendFormat("{0,6}{1,6}", temp, pressure);
                }
                lines.Add(sb.ToString());
            }

            // 调用和串口接收一致的处理逻辑
            ProcessPacket(lines);
        }

        #endregion


        public Main()
        {
            InitializeComponent();
            this.Load += Main_Load;
        }
        private void Main_Load(object sender, EventArgs e)
        {
            LoadFromJson();
            if (textBox1.Text == null || textBox1.Text == "")
            {
                textBox1.Text = "500";
            }
            if (textBox2.Text == null || textBox2.Text == "")
            {
                textBox2.Text = "50";
            }
            if (comboBox2.SelectedText == null || comboBox2.SelectedText == "")
            {
                comboBox2.SelectedIndex = 0;
            }
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            LoadMeasureSetJson();

            // 生成 64 通道数据源
            var data = new List<object>();

            for (int i = 1; i <= 64; i++)
            {
                data.Add(new { Value = i, Text = $"CH{i}" });
            }

            // 绑定到多选 ComboBox
            uCheckComboBox2.BindingDataList(data, "Value", "Text");
            // 默认全选
            uCheckComboBox2.CheckAll();

            refreshTimer = new System.Windows.Forms.Timer();
            refreshTimer.Interval = flashTime; // 100 ms 刷新一次
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();

            InitGraph();

            try
            {
                StartPacketProcessingThread();
                // 生成文件路径
                string filePath = Path.Combine(excelSavePath,
                    $"packets_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                // 创建全局 StreamWriter，不写表头
                packetWriter = new StreamWriter(filePath, true, new System.Text.UTF8Encoding(false));
                packetWriter.AutoFlush = true; // 每次写入自动刷新

                // 启动后台写线程
                StartWorkers();
                fileWriterThread = new Thread(FileWriterLoop);
                fileWriterThread.IsBackground = true;
                fileWriterThread.Start();

                TestDraw();
            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化日志文件失败: " + ex.Message);
            }

            //StartSimulation(); // 开始模拟
        }
        private void TestDraw()
        {
            timer = new System.Windows.Forms.Timer();
            timer.Interval = 200; // 每200ms更新一次
            timer.Tick += Timer_Tick;
            timer.Start();
        }
        private int counter = 0;
        private void Timer_Tick(object sender, EventArgs e)
        {
            // 模拟递增数据
            double[] values = new double[8];
            for (int i = 0; i < 8; i++)
            {
                values[i] = ((counter + i) * 10000) % 100000; // 循环递增，超过100000从0开始
            }

            panel_finger1_point.Values = values;
            panel_finger1_cloud.Values = values;
            counter++;
        }
        private void StartWorkers()
        {
            // 启动格式化工人线程
            formatThread = new Thread(FormatWorkerLoop) { IsBackground = true, Name = "FormatWorker" };
            formatThread.Start();

            // 你原来的 fileWriterThread 维持不变
            fileWriterThread = new Thread(FileWriterLoop) { IsBackground = true, Name = "FileWriter" };
            fileWriterThread.Start();
        }

        // 打开串口并启动后台读线程
        private void OpenSerialPort()
        {
            try
            {
                if (serialPort != null && serialPort.IsOpen)
                    serialPort.Close();

                serialPort.ReadTimeout = 500;
                serialPort.WriteTimeout = 500;
                serialPort.Open();

                // 启动后台读取线程
                cts = new CancellationTokenSource();
                serialThread = new Thread(() => SerialReadLoop(cts.Token));
                serialThread.IsBackground = true;
                serialThread.Start();

                LogToConsole("串口已打开");
            }
            catch (Exception ex)
            {
                MessageBox.Show("打开串口失败: " + ex.Message);
            }
        }

        // 关闭串口
        private void CloseSerialPort()
        {
            try
            {
                if (cts != null)
                {
                    cts.Cancel();
                    Thread.Sleep(100);
                }

                if (serialPort != null && serialPort.IsOpen)
                    serialPort.Close();

                LogToConsole("串口已关闭");
            }
            catch (Exception ex)
            {
                MessageBox.Show("关闭串口失败: " + ex.Message);
            }
        }

        // 后台串口读取循环
        private void SerialReadLoop(CancellationToken token)
        {
            byte[] buffer = new byte[4096];
            List<byte> recvBuffer = new List<byte>(); // 缓存字节流

            while (!token.IsCancellationRequested && serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        lock (serialLock)
                        {
                            // 加入缓存
                            for (int i = 0; i < bytesRead; i++)
                                recvBuffer.Add(buffer[i]);

                            // 解析缓存中的完整包
                            while (recvBuffer.Count >= 6) // 至少包含 包头2+长度1+其他字段
                            {
                                // 查找包头 0x42 0x54
                                if (!(recvBuffer[0] == 0x42 && recvBuffer[1] == 0x54))
                                {
                                    recvBuffer.RemoveAt(0);
                                    continue;
                                }

                                int length = recvBuffer[2]; // 长度字段（字节数）

                                if (recvBuffer.Count < length)
                                    break; // 数据未收完

                                // 拿出完整包
                                byte[] packet = recvBuffer.GetRange(0, length).ToArray();
                                recvBuffer.RemoveRange(0, length);

                                // 校验和（最后1字节）
                                byte checksum = 0;
                                for (int i = 2; i < length - 1; i++)
                                    checksum += packet[i];
                                checksum = (byte)(checksum & 0xFF);

                                byte checksumInPacket = packet[length - 1];

                                if (checksum == checksumInPacket)
                                {
                                    // 校验成功 → 入队
                                    EnqueuePacket(packet);
                                }
                                else
                                {
                                    LogToConsole("校验失败，丢弃数据包");
                                }
                            }
                        }
                    }
                }
                catch (TimeoutException)
                {
                    // 忽略超时
                }
                catch (IOException)
                {
                    break; // 串口被关闭
                }
                catch (InvalidOperationException)
                {
                    break; // 串口被关闭
                }
                catch (Exception ex)
                {
                    LogToConsole("串口读取异常：" + ex.Message);
                    break;
                }
            }
        }


        // 背景线程解包
        private void StartPacketProcessingThread()
        {
            Task.Run(() =>
            {
                foreach (var packet in uiQueue.GetConsumingEnumerable())
                {
                    ProcessPacketForUI(packet);
                }
            });
        }

        // 通道标识转索引，例如：AA=0, AB=1, ..., HH=63
        private int ParseChannelId(string id)
        {
            // 假设通道ID格式是: [A-H][A-H]
            if (id.Length == 2)
            {
                int row = id[0] - 'A';  // 第一个字母决定行 (0~7)
                int col = id[1] - 'A';  // 第二个字母决定列 (0~7)
                return row * 8 + col;   // 映射到 0~63
            }
            return -1; // 无效ID
        }
        private static List<(string id, string temp, string pressure)> ParseLineFast(string line)
        {
            var result = new List<(string id, string temp, string pressure)>();
            int i = 0, n = line.Length;

            while (i < n)
            {
                // 跳过前导空格
                while (i < n && char.IsWhiteSpace(line[i])) i++;

                if (i >= n) break;

                // === 1. 通道 ID（两个字母） ===
                if (i + 1 >= n || !char.IsLetter(line[i]) || !char.IsLetter(line[i + 1]))
                    break; // 格式异常
                string id = line.Substring(i, 2);
                i += 2;

                // 跳过空格
                while (i < n && char.IsWhiteSpace(line[i])) i++;

                // === 2. 温度 ===
                int start = i;
                while (i < n && (char.IsDigit(line[i]) || line[i] == '-' || line[i] == '+')) i++;
                if (i == start) break; // 格式错误
                string temp = line.Substring(start, i - start);

                // 跳过空格
                while (i < n && char.IsWhiteSpace(line[i])) i++;

                // === 3. 压力 ===
                start = i;
                while (i < n && (char.IsDigit(line[i]) || line[i] == '-' || line[i] == '+')) i++;
                if (i == start) break; // 格式错误
                string pressure = line.Substring(start, i - start);

                result.Add((id, temp, pressure));
            }

            return result;
        }

        private void ProcessPacketForUI(List<string> uiData)
        {
            try
            {
                // uiData 格式：
                // [0] 地址 (1~5)
                // [1] 类型 ("F4"=温度, "F5"=压力)
                // [2]~[9] 8个通道的值

                if (!int.TryParse(uiData[0], out int addr)) return;
                string type = uiData[1];

                // === 校零采集逻辑 ===
                if (isZeroing)
                {
                    for (int i = 0; i < 8; i++)
                    {
                        int channelIndex = (addr - 1) * 8 + i; // 根据地址计算全局通道索引

                        if (channelIndex < 0 || channelIndex >= 64) continue;

                        if (!pressureCalibBuffers.ContainsKey(channelIndex))
                            pressureCalibBuffers[channelIndex] = new List<double>();

                        if (double.TryParse(uiData[2 + i], out double pressure))
                            pressureCalibBuffers[channelIndex].Add(pressure);
                    }

                    zeroingPacketCount++;

                    if (zeroingPacketCount >= ZeroingTargetPackets)
                    {
                        for (int channel = 0; channel < channelZeroOffsets.Length; channel++)
                        {
                            if (pressureCalibBuffers.ContainsKey(channel) && pressureCalibBuffers[channel].Count > 0)
                                channelZeroOffsets[channel] = pressureCalibBuffers[channel].Average();
                        }

                        isZeroing = false;

                        if (console_textBox.InvokeRequired)
                        {
                            console_textBox.BeginInvoke(new Action(() =>
                            {
                                MessageBox.Show("校零完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            }));
                        }
                        else
                        {
                            MessageBox.Show("校零完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }

                    return;
                }

                // === 日志输出 ===
                StringBuilder logBuilder = new StringBuilder();
                if (logSampleCounter % LogSampleRate == 0)
                {
                    logBuilder.AppendLine($"接收到完整数据包：地址={addr}, 类型={type}");
                    for (int i = 2; i < uiData.Count; i++)
                    {
                        logBuilder.AppendLine($"通道 {i - 2 + 1}: {uiData[i]}");
                    }
                    LogToConsole(logBuilder.ToString());
                }
                logSampleCounter++;

                // === 数据更新 ===
                DotMatrixUpdate dotUpdate = new DotMatrixUpdate
                {
                    TempValues = new double[64],
                    PressureValues = new double[64]
                };

                for (int i = 0; i < 8; i++)
                {
                    int channelIndex = (addr - 1) * 8 + i;

                    if (channelIndex < 0 || channelIndex >= 64) continue;

                    if (double.TryParse(uiData[2 + i], out double value))
                    {
                        if (type == "F4") // 温度
                        {
                            double temp = value / 10.0;
                            dotUpdate.TempValues[channelIndex] = temp;
                        }
                        else if (type == "F5") // 压力
                        {
                            double pressure = value - channelZeroOffsets[channelIndex];
                            dotUpdate.PressureValues[channelIndex] = pressure;

                            if (yalitu)
                            {
                                double correctedPressure = DenoiseByMedian(channelIndex, pressure);
                                var graphUpdate = new GraphUpdate
                                {
                                    Channel = channelIndex,
                                    Index = packetIndex,
                                    Pressure = correctedPressure
                                };

                                if (graphQueue.Count >= MaxQueueSize)
                                    graphQueue.TryDequeue(out _);
                                graphQueue.Enqueue(graphUpdate);
                            }
                        }
                    }
                }

                if (diantu)
                {
                    if (dotQueue.Count >= MaxQueueSize)
                        dotQueue.TryDequeue(out _);
                    dotQueue.Enqueue(dotUpdate);
                }

                Interlocked.Increment(ref packetIndex);
            }
            catch (Exception ex)
            {
                MessageBox.Show("ProcessPacketForUI error: " + ex.Message);
            }
        }


        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            bool needRefresh = false;
            GraphUpdate graphUpdate;

            var pane = zedGraphControl2.GraphPane;

            // 控制 X 轴显示范围
            double xMin = packetIndex - MaxVisiblePackets;
            if (xMin < 0) xMin = 0;
            double xMax = packetIndex;

            pane.XAxis.Scale.Min = xMin;
            pane.XAxis.Scale.Max = xMax;
            pane.YAxis.Scale.MagAuto = false;
            pane.YAxis.Scale.Mag = 0;

            // === 更新滚动图（zedGraphControl2） ===
            while (graphQueue.TryDequeue(out graphUpdate))
            {
                needRefresh = true;

                if (!channelData2.ContainsKey(graphUpdate.Channel))
                {
                    var list = new RollingPointPairList(MaxVisiblePackets + 100);
                    var curve = pane.AddCurve($"CH{graphUpdate.Channel + 1}", list, GetColor(graphUpdate.Channel), SymbolType.None);
                    channelData2[graphUpdate.Channel] = list;
                    channelCurves2[graphUpdate.Channel] = curve;
                }

                if (graphUpdate.Index >= xMin)
                {
                    channelData2[graphUpdate.Channel].Add(graphUpdate.Index, graphUpdate.Pressure);
                }
            }

            if (needRefresh)
            {
                zedGraphControl2.AxisChange();
                zedGraphControl2.Invalidate();
            }

            // === 更新点图和云图 Panels ===
            DotMatrixUpdate dotUpdate;
            while (dotQueue.TryDequeue(out dotUpdate))
            {
                // 每个传感器地址 1~5，对应 8 通道
                for (int addr = 1; addr <= 5; addr++)
                {
                    // 计算对应的 8 个通道索引
                    int startIndex = (addr - 1) * 8;

                    // 更新点图 Panel
                    var panelPoint = this.Controls.Find($"panel_finger{addr}_point", true).FirstOrDefault() as DoubleBufferedPanel;
                    if (panelPoint != null)
                    {
                        double[] values = new double[8];
                        Array.Copy(dotUpdate.PressureValues, startIndex, values, 0, 8);
                        panelPoint.Values = values;
                        panelPoint.Invalidate();
                    }

                    // 更新云图 Panel
                    var panelCloud = this.Controls.Find($"panel_finger{addr}_cloud", true).FirstOrDefault() as DoubleBufferedPanelCloud; // 假设你有 FingerCloudPanel
                    if (panelCloud != null)
                    {
                        double[] values = new double[8];
                        Array.Copy(dotUpdate.PressureValues, startIndex, values, 0, 8);
                        panelCloud.Values = values;
                        panelCloud.Invalidate();
                    }
                }
            }
        }



        private void LoadMeasureSetJson()
        {
            try
            {
                string filePath = "MeasureSet.json";
                if (!File.Exists(filePath))
                    return;

                string json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                for (int i = 1; i <= 64; i++)
                {
                    if (data.TryGetValue($"textBox_ch{i}", out object value) && value != null)
                    {
                        channelZeroOffsets[i - 1] = double.Parse(value.ToString());
                    }
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("加载MeasureSet.json失败: " + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// 控制台输出
        /// </summary>
        /// <param name="message"></param>
        public void LogToConsole(string message)
        {
            string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";

            if (console_textBox.InvokeRequired)
            {
                console_textBox.Invoke(new Action(() =>
                {
                    AppendLog(timestampedMessage);
                }));
            }
            else
            {
                AppendLog(timestampedMessage);
            }
        }

        private void AppendLog(string msg)
        {
            console_textBox.AppendText(msg);

            // 控制最大行数
            const int maxLines = 1000;
            if (console_textBox.Lines.Length > maxLines)
            {
                // 删除最早的部分
                var lines = console_textBox.Lines;
                int removeCount = lines.Length - maxLines;
                string[] newLines = new string[maxLines];
                Array.Copy(lines, removeCount, newLines, 0, maxLines);
                console_textBox.Lines = newLines;

                // 滚动到末尾
                console_textBox.SelectionStart = console_textBox.Text.Length;
                console_textBox.ScrollToCaret();
            }
        }

        public void LogToConsole_NotLog(string message)
        {
            string timestampedMessage = $"[{DateTime.Now:HH:mm:ss}] {message}\r\n";

            if (console_textBox.InvokeRequired)
            {
                console_textBox.Invoke(new System.Action(() =>
                {
                    console_textBox.AppendText(timestampedMessage);
                }));
            }
            else
            {
                console_textBox.AppendText(timestampedMessage);
            }
        }
        private void AppendLogToJsonFile(LogConfig config)
        {
            try
            {
                // 将单条 LogEntry 写为一行 JSON
                string json = JsonSerializer.Serialize(config);
                File.AppendAllText("logs.json", json + Environment.NewLine);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"写入日志文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 保存最近一包（用于容错）
        private byte[] lastValidPacket = null;

        private void EnqueuePacket(byte[] packet)
        {
            try
            {
                // 基本校验
                if (packet.Length < 10) return;

                int length = packet[2];     // 长度字段
                byte addr = packet[3];      // 地址
                byte type = packet[4];      // 类型 (F4/F5)

                // 占位4字节 + 序列号4字节
                byte[] serialBytes = packet.Skip(9).Take(4).ToArray();
                Array.Reverse(serialBytes);
                int serial = BitConverter.ToInt32(serialBytes, 0);

                // 数据区
                int dataOffset = 13; // 2包头 +1长度 +1地址 +1类型 +4占位 +4序列号 = 13
                int dataLength = length - (1 + 1 + 4 + 4); // 去掉地址/类型/占位/序列号，剩下就是数据+校验和

                double[] values = new double[8];

                if (type == 0xF4) // 温度：8通道*2字节
                {
                    for (int i = 0; i < 8; i++)
                    {
                        int pos = dataOffset + i * 2;
                        if (pos + 1 >= packet.Length) break;

                        byte[] tmp = { packet[pos], packet[pos + 1] };
                        Array.Reverse(tmp); // 翻转
                        values[i] = BitConverter.ToInt16(tmp, 0);
                    }
                }
                else if (type == 0xF5) // 压力：8通道*4字节
                {
                    for (int i = 0; i < 8; i++)
                    {
                        int pos = dataOffset + i * 4;
                        if (pos + 3 >= packet.Length) break;

                        byte[] tmp = { packet[pos], packet[pos + 1], packet[pos + 2], packet[pos + 3] };
                        Array.Reverse(tmp); // 翻转
                        values[i] = BitConverter.ToInt32(tmp, 0);
                    }
                }
                else
                {
                    LogToConsole($"未知包类型: {type:X2}");
                    return;
                }

                // 容错（校验成功的包才覆盖）
                lastValidPacket = packet;

                // 构造 List<string>
                var uiData = new List<string>(10);
                uiData.Add(addr.ToString());        // [0] 地址
                uiData.Add(type.ToString("X2"));    // [1] 类型 (16进制显示更直观，比如 F4/F5)
                for (int i = 0; i < 8; i++)
                {
                    uiData.Add(values[i].ToString()); // [2] ~ [9] 八个通道值
                }

                // 入UI队列（清空旧的，只保留最新）
                while (uiQueue.Count > 0) uiQueue.TryTake(out _);
                uiQueue.Add(uiData);

                // 存储节流
                var now = HighResDateTime.Now;
                if ((now - lastSaveTime).TotalMilliseconds >= saveRate)
                {
                    lastSaveTime = now;

                    if (fileRawQueue.Count >= 20000) fileRawQueue.TryTake(out _);
                    fileRawQueue.Add(uiData);
                }

                // 包总数 + UI更新
                long newCount = Interlocked.Increment(ref totalPacketCount);
                if (packetCountLabel.InvokeRequired)
                {
                    packetCountLabel.BeginInvoke(new Action(() =>
                    {
                        packetCountLabel.Text = $"接收包数: {newCount}";
                    }));
                }
                else
                {
                    packetCountLabel.Text = $"接收包数: {newCount}";
                }
            }
            catch (Exception ex)
            {
                LogToConsole("EnqueuePacket 异常: " + ex.Message);
            }
        }



        private void FormatWorkerLoop()
        {
            try
            {
                foreach (var packet in fileRawQueue.GetConsumingEnumerable())
                {
                    string line = FormatPacketToOneCsvLineFast(packet);
                    if (line == null) continue;

                    // fileQueue 有界 + 丢最旧，确保不堆积
                    if (fileQueue.Count >= 20000) fileQueue.TryTake(out _);
                    fileQueue.Add(line);
                }
            }
            catch (Exception ex)
            {
                LogToConsole($"[ERR] FormatWorker: {ex.Message}");
            }
        }
        private string FormatPacketToOneCsvLineFast(List<string> packet)
        {
            if (packet == null || packet.Count < 9) return null;

            // 64通道
            string[] pressures = new string[64];
            string[] temps = new string[64];

            // 解析第2~9行
            for (int row = 1; row <= 8; row++)
                ParseLineIntoArrays(packet[row], temps, pressures);

            // 拼CSV：时间 + 64压 + 64温
            var sb = new System.Text.StringBuilder(2048);
            sb.Append(HighResDateTime.Now.ToString("yy:MM:dd:HH:mm:ss.fff"));
            for (int i = 0; i < 64; i++) { sb.Append(','); if (pressures[i] != null) sb.Append(pressures[i]); }
            for (int i = 0; i < 64; i++) { sb.Append(','); if (temps[i] != null) sb.Append(temps[i]); }
            return sb.ToString();
        }

        // 逐字符解析： [A-Z][A-Z] <spaces> temp <spaces> pressure <spaces> ... 重复
        private static void ParseLineIntoArrays(string line, string[] temps, string[] pressures)
        {
            if (string.IsNullOrEmpty(line)) return;
            int i = 0, n = line.Length;

            while (i < n)
            {
                // 跳空白
                while (i < n && char.IsWhiteSpace(line[i])) i++;
                if (i + 1 >= n) break;

                char c0 = line[i], c1 = line[i + 1];
                if (!(c0 >= 'A' && c0 <= 'H' && c1 >= 'A' && c1 <= 'H'))
                {
                    // 若不是合法ID，跳到下一个空白后继续
                    while (i < n && !char.IsWhiteSpace(line[i])) i++;
                    continue;
                }
                int ch = (c0 - 'A') * 8 + (c1 - 'A');
                i += 2;

                // 跳空白到 temp
                while (i < n && char.IsWhiteSpace(line[i])) i++;
                int s = i;
                if (i < n && (line[i] == '-' || line[i] == '+')) i++;
                while (i < n && char.IsDigit(line[i])) i++;
                string tempStr = (i > s) ? line.Substring(s, i - s) : null;

                // 跳空白到 pressure
                while (i < n && char.IsWhiteSpace(line[i])) i++;
                s = i;
                if (i < n && (line[i] == '-' || line[i] == '+')) i++;
                while (i < n && char.IsDigit(line[i])) i++;
                string pressureStr = (i > s) ? line.Substring(s, i - s) : null;

                if (ch >= 0 && ch < 64)
                {
                    temps[ch] = tempStr;
                    pressures[ch] = pressureStr;
                }
            }
        }

        private void FileWriterLoop()
        {
            try
            {
                while (!fileQueue.IsCompleted)
                {
                    var batch = new List<string>();
                    while (fileQueue.TryTake(out var line))
                        batch.Add(line);

                    if (batch.Count > 0)
                    {
                        lock (saveLock)
                        {
                            foreach (var line in batch)
                            {
                                packetWriter.WriteLine(line);
                                Interlocked.Increment(ref savedPacketCount);
                            }
                            packetWriter.Flush();
                        }

                        // UI 更新（保持你的逻辑）
                        if (savedCountLabel.InvokeRequired)
                            savedCountLabel.BeginInvoke(new Action(() =>
                                savedCountLabel.Text = $"已存包数: {savedPacketCount}"));
                        else
                            savedCountLabel.Text = $"已存包数: {savedPacketCount}";
                    }

                    Thread.Sleep(1);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("文件写线程发生错误: " + ex.Message, "错误",
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }




        #region 绘图
        private double DenoiseByMedian(int channelIndex, double newValue)
        {
            if (!channelBuffers.ContainsKey(channelIndex))
                channelBuffers[channelIndex] = new Queue<double>();

            var buffer = channelBuffers[channelIndex];

            if (buffer.Count >= 3)
                buffer.Dequeue();

            buffer.Enqueue(newValue);

            if (buffer.Count < 3)
                return newValue; // 数据不足，直接返回

            double[] arr = buffer.ToArray(); // [prev, mid, next]

            double prev = arr[0], mid = arr[1], next = arr[2];

            // 判断：中点是否远离两边，而两边相近
            if (Math.Abs(mid - prev) > 1000 && Math.Abs(mid - next) > 1000 &&
                Math.Abs(prev - next) < 20)   // 阈值要根据实际量程调
            {
                // 用前后均值替换中点
                arr[1] = (prev + next) / 2.0;
            }

            // 更新队列为修正后的值
            channelBuffers[channelIndex] = new Queue<double>(arr);

            return arr.Last(); // 返回最新点（可能被修正）
        }
        private void ProcessPacket(List<string> lines)
        {
            try
            {
                if (lines.Count != 9) return;

                int channelIndex = 0;

                double[] tempCopy = new double[64];
                double[] pressureCopy = new double[64];

                for (int i = 1; i < 9; i++) // 第2~9行
                {
                    string[] tokens = lines[i].Trim().Split(
                        new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                    for (int j = 0; j < tokens.Length; j += 2)
                    {
                        if (j + 1 >= tokens.Length) break;

                        if (double.TryParse(tokens[j], out double pressure) &&
                            double.TryParse(tokens[j + 1], out double temp))
                        {
                            // 零点修正
                            if (channelIndex < 64)
                            {
                                pressure -= channelZeroOffsets[channelIndex];
                                temp = temp / 10;

                                // 队列保存供点阵刷新
                                tempCopy[channelIndex] = temp;
                                pressureCopy[channelIndex] = pressure;

                                // 添加曲线更新
                                if (yalitu)
                                {
                                    graphQueue.Enqueue(new GraphUpdate
                                    {
                                        Channel = channelIndex,
                                        Pressure = DenoiseByMedian(channelIndex, pressure),
                                        Index = packetIndex
                                    });
                                }

                            }

                            channelIndex++;
                        }
                    }
                }

                // 添加点阵刷新队列
                if (diantu)
                {
                    dotQueue.Enqueue(new DotMatrixUpdate
                    {
                        TempValues = (double[])tempCopy.Clone(),
                        PressureValues = (double[])pressureCopy.Clone()
                    });

                }

                packetIndex++;
            }
            catch (Exception ex)
            {
                MessageBox.Show("解包发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void SetPaneFont(GraphPane pane)
        {
            string fontName = "微软雅黑";

            pane.Title.FontSpec.Family = fontName;
            pane.Title.FontSpec.Size = 16;

            pane.XAxis.Title.FontSpec.Family = fontName;
            pane.XAxis.Title.FontSpec.Size = 12;
            pane.XAxis.Scale.FontSpec.Family = fontName;
            pane.XAxis.Scale.FontSpec.Size = 10;

            pane.YAxis.Title.FontSpec.Family = fontName;
            pane.YAxis.Title.FontSpec.Size = 12;
            pane.YAxis.Scale.FontSpec.Family = fontName;
            pane.YAxis.Scale.FontSpec.Size = 10;
        }

        private void InitGraph()
        {

            GraphPane pane2 = zedGraphControl2.GraphPane;
            SetPaneFont(pane2);
            pane2.Title.Text = "64通道压力总览";
            pane2.XAxis.Title.Text = "数据包编号";
            pane2.YAxis.Title.Text = "压力";
            pane2.YAxis.Scale.MinAuto = true;
            pane2.YAxis.Scale.MaxAuto = true;
            //强制 X 轴显示为整数
            pane2.XAxis.Type = AxisType.Linear;
            pane2.XAxis.Scale.MajorStep = 1;
            pane2.XAxis.Scale.Format = "0";  // 只显示整数，无小数点
            zedGraphControl2.AxisChange(); // 应用更改
        }

        /// 更新通道曲线（只绘制压力，所有通道在一个图里）
        /// </summary>
        /// <param name="channel">通道号 0~63</param>
        /// <param name="pressure">压力值</param>
        private void UpdateGraph2(int channel, double temp)
        {
            var pane = zedGraphControl2.GraphPane;

            // 如果该通道曲线不存在，则初始化
            if (!channelData2.ContainsKey(channel))
            {
                // RollingPointPairList 自动限制点数（这里用 MaxVisiblePackets）
                var list = new RollingPointPairList(MaxVisiblePackets > 0 ? MaxVisiblePackets : 6000);
                var curve = pane.AddCurve($"CH{channel + 1}", list, GetColor(channel), SymbolType.None);
                channelData2[channel] = list;
                channelCurves2[channel] = curve;
            }

            // 添加数据点
            channelData2[channel].Add(packetIndex, temp);
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
        #endregion

        #region 按钮
        /// <summary>
        /// 打开串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (!serialPort.IsOpen)
            {
                string filePath = "SerialConfig.json";
                if (!File.Exists(filePath))
                {
                    MessageBox.Show("未找到配置文件");
                    return;
                }

                string json = File.ReadAllText(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

                if (data == null)
                {
                    MessageBox.Show("配置文件数据为空");
                    return;
                }

                // 构建 SerialConfig 对象
                try
                {
                    string comPort = data.ContainsKey("COMPort") ? data["COMPort"].ToString() : throw new Exception("缺少 COMPort 配置");
                    int baudRate = data.ContainsKey("BaudRate") ? int.Parse(data["BaudRate"].ToString()) : throw new Exception("缺少 BaudRate 配置");
                    int dataBits = data.ContainsKey("DataBits") ? int.Parse(data["DataBits"].ToString()) : throw new Exception("缺少 DataBits 配置");
                    Parity parity = data.ContainsKey("Parity") ? Enum.Parse<Parity>(data["Parity"].ToString(), true) : throw new Exception("缺少 Parity 配置");
                    StopBits stopBits = data.ContainsKey("StopBits") ? Enum.Parse<StopBits>(data["StopBits"].ToString(), true) : throw new Exception("缺少 StopBits 配置");
                    Handshake handshake = data.ContainsKey("Handshake") ? Enum.Parse<Handshake>(data["Handshake"].ToString(), true) : throw new Exception("缺少 Handshake 配置");

                    serialPort.PortName = comPort;
                    serialPort.BaudRate = baudRate;
                    serialPort.DataBits = dataBits;
                    serialPort.Parity = parity;
                    serialPort.StopBits = stopBits;
                    serialPort.Handshake = handshake;
                    serialPort.Encoding = System.Text.Encoding.ASCII;
                    /*                    serialPort.DataReceived -= SerialPort_DataReceived; // 先移除旧的绑定
                                        serialPort.DataReceived += SerialPort_DataReceived;
                                        serialPort.Open();*/
                    OpenSerialPort();
                    state_label.Text = "已连接";
                }
                catch (Exception ex)
                {
                    MessageBox.Show("打开串口失败: " + ex.Message);
                }
            }
            else
            {
                MessageBox.Show("串口已开启");
            }
        }
        /// <summary>
        /// 关闭串口
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                if (!serialPort.IsOpen)
                {
                    MessageBox.Show("请先连接串口！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                //serialPort.Close();
                CloseSerialPort();
                state_label.Text = "未连接";
            }
            catch (Exception ex)
            {
                MessageBox.Show("关闭连接失败，请检查串口状态！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        /// <summary>
        /// 重绘按钮
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button3_Click(object sender, EventArgs e)
        {
            // 设置刷新间隔
            if (textBox2.Text == "" || !int.TryParse(textBox2.Text, out int refreshMs) || refreshMs <= 0)
            {
                MessageBox.Show("刷新时间必须为正整数", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            refreshTimer.Interval = refreshMs;

            LoadMeasureSetJson();

            if (int.TryParse(textBox1.Text, out int maxVisible) && maxVisible > 0)
            {
                MaxVisiblePackets = maxVisible;
            }
            else
            {
                MaxVisiblePackets = -1; // 显示全部
                LogToConsole_NotLog("未设置或输入无效，显示全部数据");
            }

            // 清空图表数据并重建曲线
            channelData2.Clear();
            channelCurves2.Clear();
            var pane = zedGraphControl2.GraphPane;
            pane.CurveList.Clear();

            for (int ch = 0; ch < 64; ch++)
            {
                var list = new RollingPointPairList(MaxVisiblePackets + 100);
                var curve = pane.AddCurve($"CH{ch + 1}", list, GetColor(ch), SymbolType.None);
                channelData2[ch] = list;
                channelCurves2[ch] = curve;
            }

            packetIndex = 0;

            // 重绘主图
            zedGraphControl2.AxisChange();
            zedGraphControl2.Invalidate();

            LogToConsole_NotLog("图表已清空，并应用新的显示点数限制。");
        }
        /// <summary>
        /// 历史记录
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            Form form = new LogHistory();
            form.ShowDialog();
        }

        private void 串口设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form form = new ConnectSet();
            form.ShowDialog();
        }

        private void 文件设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form form = new FileSet();
            form.ShowDialog();
        }

        private void 测量设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form form = new MeasureSet();
            form.ShowDialog();
        }

        // pictureBox2: 最小化（折叠）
        private void pictureBox2_Click(object sender, EventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
        }

        // pictureBox3: 最大化 / 还原
        private void pictureBox3_Click(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                this.WindowState = FormWindowState.Maximized;
            }
            else
            {
                this.WindowState = FormWindowState.Normal;
            }
        }

        // pictureBox4: 关闭
        private void pictureBox4_Click(object sender, EventArgs e)
        {
            refreshTimer.Dispose();
            isRunning = false;

            // 通知队列不再接受新数据
            uiQueue.CompleteAdding();
            fileQueue.CompleteAdding();
            fileRawQueue.CompleteAdding();

            // 等待写线程结束
            //fileWriterThread.Join();

            // 最后 flush & close
            packetWriter?.Flush();
            packetWriter?.Close();
            packetWriter = null;

            this.Close(); // 或 Application.Exit();
        }
        #endregion

        private void button5_Click(object sender, EventArgs e)
        {
            // 开始校零
            isZeroing = true;
            zeroingPacketCount = 0;
            pressureCalibBuffers.Clear();
        }



        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            Form form = new ConnectSet();
            form.ShowDialog();
        }

        private void button6_Click(object sender, EventArgs e)
        {
            // 获取选中的通道文本，例如 "CH1", "CH2" ...
            List<string> selected = uCheckComboBox2.GetSelectedTexts();

            foreach (var kv in channelCurves2)
            {
                int channel = kv.Key;
                LineItem curve = kv.Value;

                string curveName = $"CH{channel + 1}";

                // 如果当前曲线在选中列表里显示，否则隐藏
                curve.IsVisible = selected.Contains(curveName);
            }

            // 刷新图形
            zedGraphControl2.AxisChange();
            zedGraphControl2.Invalidate();
        }

        private void DrawDotMatrix(Graphics g, double[] values, Panel panel)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int totalPoints = values.Length;

            // 自动计算行列数
            int cols = (int)Math.Ceiling(Math.Sqrt(totalPoints));
            int rows = (int)Math.Ceiling((double)totalPoints / cols);

            // 先预留最小间距
            int minPaddingX = 5;
            int minPaddingY = 5;

            // 根据 Panel 尺寸计算圆直径和实际间距
            int diameterX = (panel.Width - (cols + 1) * minPaddingX) / cols;
            int diameterY = (panel.Height - (rows + 1) * minPaddingY) / rows;
            int diameter = Math.Min(diameterX, diameterY);

            // 重新计算行列间距，让圆形铺满 Panel
            float paddingX = (panel.Width - cols * diameter) / (cols + 1f);
            float paddingY = (panel.Height - rows * diameter) / (rows + 1f);

            for (int i = 0; i < totalPoints; i++)
            {
                int row = i / cols;
                int col = i % cols;

                float x = paddingX + col * (diameter + paddingX);
                float y = paddingY + row * (diameter + paddingY);

                double value = values[i];
                Color color = GetColorFromValue(value);

                using (Brush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, x, y, diameter, diameter);
                }

                // 绘制数值
                string text = value.ToString("F1");
                SizeF textSize = g.MeasureString(text, this.Font);
                g.DrawString(text, this.Font, Brushes.White,
                    x + (diameter - textSize.Width) / 2,
                    y + (diameter - textSize.Height) / 2);
            }
        }


        private void DrawDotMatrix2(Graphics g, double[] values, Panel panel)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int totalPoints = values.Length;

            // 自动计算行列数
            int cols = (int)Math.Ceiling(Math.Sqrt(totalPoints));
            int rows = (int)Math.Ceiling((double)totalPoints / cols);

            // 先预留最小间距
            int minPaddingX = 5;
            int minPaddingY = 5;

            // 根据 Panel 尺寸计算圆直径和实际间距
            int diameterX = (panel.Width - (cols + 1) * minPaddingX) / cols;
            int diameterY = (panel.Height - (rows + 1) * minPaddingY) / rows;
            int diameter = Math.Min(diameterX, diameterY);

            // 重新计算行列间距，让圆形铺满 Panel
            float paddingX = (panel.Width - cols * diameter) / (cols + 1f);
            float paddingY = (panel.Height - rows * diameter) / (rows + 1f);

            for (int i = 0; i < totalPoints; i++)
            {
                int row = i / cols;
                int col = i % cols;

                float x = paddingX + col * (diameter + paddingX);
                float y = paddingY + row * (diameter + paddingY);

                double value = values[i];
                Color color = GetColorFromValue2(value);

                using (Brush brush = new SolidBrush(color))
                {
                    g.FillEllipse(brush, x, y, diameter, diameter);
                }

                // 绘制数值
                string text = value.ToString("F1");
                SizeF textSize = g.MeasureString(text, this.Font);
                g.DrawString(text, this.Font, Brushes.White,
                    x + (diameter - textSize.Width) / 2,
                    y + (diameter - textSize.Height) / 2);
            }
        }



        private Color GetColorFromValue(double value)
        {
            // 假设值范围0~100，可根据实际调整
            value = Math.Max(0, Math.Min(100, value));
            int r = (int)(value / 100.0 * 255);
            int g = 0;
            int b = 255 - r;
            return Color.FromArgb(r, g, b);
        }

        private Color GetColorFromValue2(double value)
        {
            // 假设值范围0~100，可根据实际调整
            value = Math.Max(0, Math.Min(1000, value));
            int r = (int)(value / 1000.0 * 255);
            int g = 0;
            int b = 255 - r;
            return Color.FromArgb(r, g, b);
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox2.Checked)
            {
                yalitu = true;
            }
            else
            {
                yalitu = false;
            }
        }

        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox3.Checked)
            {
                diantu = true;
            }
            else
            {
                diantu = false;
            }
        }

        private void button7_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "请选择一个文件夹";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    textBox3.Text = dialog.SelectedPath;
                    excelSavePath = textBox3.Text;
                }
            }
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[textBox3.Name] = textBox3.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("ExcelPath.json", json);
        }
        private void LoadFromJson()
        {
            string filePath = "ExcelPath.json";
            if (!File.Exists(filePath))
                return;

            string json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (data == null)
                return;

            if (data.TryGetValue("textBox1", out object value1))
            {
                textBox1.Text = value1.ToString();
                MaxVisiblePackets = int.Parse(textBox1.Text);
            }
            if (data.TryGetValue("textBox2", out object value2))
            {
                textBox2.Text = value2.ToString();
                flashTime = int.Parse(textBox2.Text);
            }
            if (data.TryGetValue("textBox3", out object value3))
            {
                textBox3.Text = value3.ToString();
                excelSavePath = textBox3.Text;
            }

            if (data.TryGetValue("comboBox1", out object value4))
            {
                comboBox1.SelectedIndex = int.Parse(value4.ToString());
                updateSaveRate(); // 更新保存频率
            }
            if (data.TryGetValue("comboBox2", out object value5))
            {
                comboBox2.SelectedIndex = int.Parse(value4.ToString());
                if (comboBox2.SelectedIndex == 0)
                    chuanGanQiType = "MEMS";
                else
                    chuanGanQiType = "Yingbianhua";
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[textBox3.Name] = textBox3.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("ExcelPath.json", json);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            switch (comboBox1.SelectedItem.ToString())
            {
                case "100Hz":
                    saveRate = 10;
                    break;
                case "10Hz":
                    saveRate = 100;
                    break;
                case "1Hz":
                    saveRate = 1000;
                    break;
                case "0.1Hz":
                    saveRate = 10000;
                    break;
                case "1/60Hz":
                    saveRate = 60000;
                    break;
                default:
                    saveRate = 1000;
                    break;
            }

            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[textBox3.Name] = textBox3.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("ExcelPath.json", json);
        }

        private void updateSaveRate()
        {
            switch (comboBox1.SelectedItem.ToString())
            {
                case "100Hz":
                    saveRate = 10;
                    break;
                case "10Hz":
                    saveRate = 100;
                    break;
                case "1Hz":
                    saveRate = 1000;
                    break;
                case "0.1Hz":
                    saveRate = 10000;
                    break;
                case "1/60Hz":
                    saveRate = 60000;
                    break;
                default:
                    saveRate = 1000;
                    break;
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {
            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[textBox3.Name] = textBox3.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("ExcelPath.json", json);
        }

        private void button4_Click(object sender, EventArgs e)
        {

        }

        private void button8_Click(object sender, EventArgs e)
        {
            // 1. 加载 ONNX 模型
            using var session = new InferenceSession("C:\\Users\\Administrator\\Desktop\\exp1\\model.onnx");

            // 2. 准备输入数据：25 个 float
            float[] inputData = new float[25]
            {
            0.1f, 0.2f, 0.3f, 0.4f, 0.5f,
            0.6f, 0.7f, 0.8f, 0.9f, 1.0f,
            1.1f, 1.2f, 1.3f, 1.4f, 1.5f,
            1.6f, 1.7f, 1.8f, 1.9f, 2.0f,
            2.1f, 2.2f, 2.3f, 2.4f, 2.5f
            };

            // 3. 构建 Tensor（形状 [1, 25]，batch=1）
            var inputTensor = new DenseTensor<float>(inputData, new int[] { 1, 25 });

            // 获取模型输入名（假设只有一个输入）
            string inputName = session.InputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
        };

            // 4. 运行推理
            using var results = session.Run(inputs);

            // 获取模型输出名（假设只有一个输出）
            string outputName = session.OutputMetadata.Keys.First();

            // 5. 取结果：形状 [1, 3]
            var outputTensor = results.First(x => x.Name == outputName).AsTensor<float>();
            float[] outputData = outputTensor.ToArray();

            LogToConsole("模型输出：");
            foreach (var v in outputData)
                LogToConsole(v.ToString());
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[textBox3.Name] = textBox3.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("ExcelPath.json", json);
        }
    }
}
