using fingerPressure.MODEL;
using MetroFramework.Forms;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO.Ports;
using System.Linq;
using System.Numerics.Tensors;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ZedGraph;

namespace fingerPressure
{
    public partial class Main : MetroForm
    {
        private SerialPort serialPort = new SerialPort();
        //private Dictionary<int, RollingPointPairList> channelData = new();
        //private Dictionary<int, PointPairList> channelData = new();
        private Dictionary<int, RollingPointPairList> channelData_temp = new();
        private Dictionary<int, RollingPointPairList> channelData2 = new();
        private Dictionary<int, LineItem> channelCurves_temp = new();
        private Dictionary<int, LineItem> channelCurves2 = new();
        private List<string> currentPacketLines = new();
        private int MaxVisiblePackets = 200;
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
        private double[] channelZeroOffsets = new double[40]; // 默认全为 0.0
        private readonly int[] channelZeroingCounts = new int[40];
        private double[] channelZeroOffsets27 = new double[135]; // 默认全为 0.0
        private readonly int[] channelZeroingCounts27 = new int[135];
        private double xielv;
        // 用于暂存每个通道的前5个电压值
        private Dictionary<int, Queue<double>> zeroCalibBuffers = new Dictionary<int, Queue<double>>();
        private Dictionary<int, Queue<double>> zeroCalibBuffers27 = new Dictionary<int, Queue<double>>();
        private const int ZeroCalibSampleCount = 5;

        private System.Windows.Forms.Timer simulationTimer;
        private System.Windows.Forms.Timer refreshTimer;
        private int logSampleCounter = 0;
        private int flashCounter = 0;
        private const int LogSampleRate = 50; // 每50包打印一次
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
        private ConcurrentQueue<GraphUpdate> tempQueue = new ConcurrentQueue<GraphUpdate>();

        // 存储点阵刷新数据
        private ConcurrentQueue<DotMatrixUpdate_Temp> dotQueue_Temp = new ConcurrentQueue<DotMatrixUpdate_Temp>();
        private ConcurrentQueue<DotMatrixUpdate_Pres> dotQueue_Pres = new ConcurrentQueue<DotMatrixUpdate_Pres>();
        private StreamWriter packetWriter;
        private Thread fileWriterThread;
        private bool isRunning = true;
        private int packetCounter = 0;

        //采集频率
        private double sampleFrequencyHz = 50; // 默认采集频率 50Hz，可以在界面输入
        private DateTime lastSampleTime = DateTime.MinValue;
        private int flashTime = 50;

        //Setting.json
        private string excelSavePath = "";
        private string model1Path = "";
        private string model2Path = "";

        // 校零控制
        private bool isZeroing = false;
        private int zeroingPacketCount = 0;
        private const int ZeroingTargetPackets = 5;
        private Dictionary<int, List<double>> tempCalibBuffers = new();
        private Dictionary<int, List<double>> pressureCalibBuffers = new();
        private Dictionary<int, List<double>> pressureCalibBuffers27 = new();

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

        string portName = "";
        // 保存所有的 TabPage 引用，避免丢失
        private TabPage tp1, tp2, tp3, tp4;

        //调用模型
        private InferenceSession sessionModel1;
        private InferenceSession sessionModel2;
        private BlockingCollection<float[]> inferenceQueue = new BlockingCollection<float[]>(new ConcurrentQueue<float[]>());

        /*        private StreamWriter monitorWriter;
                private Thread monitorThread;
                private bool monitorRunning = true;*/

        private string[] fingerNames = { "大拇指", "食指", "中指", "无名指", "小拇指" };
        private int choosedFinger1 = -1; // 默认大拇指
        private int choosedFinger3 = -1; // 默认大拇指
        private int choosedFinger19 = -1; // 默认大拇指

        private bool guiyihua = false;

        public struct SensorInferenceResult
        {
            public float Fx;
            public float Fy;
            public float Fz;
            public long Label;          // model2 输出标签
            public int MaxProbIndex;    // model2 最大概率索引
            public float MaxProb;       // model2 最大概率
        }
        // 保存所有传感器结果
        private ConcurrentDictionary<int, SensorInferenceResult> latestResults = new ConcurrentDictionary<int, SensorInferenceResult>();

        /*        class GraphUpdate
                {
                    public int Channel;
                    public double Pressure;  // 曲线值
                    public int Index;
                }*/
        public class GraphUpdate
        {
            public int SensorIndex { get; set; }     // 传感器编号 (0~4)
            public int Channel { get; set; }         // 压力通道编号 (0~26)
            public long Index { get; set; }          // 包序号
            public double Pressure { get; set; }     // 当前压力值
            public double Temperature { get; set; }  // 当前温度值
            public double[] GyroValues { get; set; } // 当前传感器的6个陀螺仪值
        }


        class DotMatrixUpdate_Temp
        {
            public double[] TempValues;
            //public double[] PressureValues;
            public int SensorIndex;
        }
        class DotMatrixUpdate_Pres
        {
            //public double[] TempValues;
            public double[] PressureValues;
            public int SensorIndex;
            public double[] TempValues;
        }

        /// <summary> 计算环形缓冲区可用字节数 </summary>
        private static int GetAvailableBytes(int head, int tail, int capacity)
        {
            return (tail - head + capacity) % capacity;
        }

        /// <summary> 从环形缓冲区读取一个字节 </summary>
        private static byte PeekByte(byte[] buffer, int head, int offset, int capacity)
        {
            return buffer[(head + offset) % capacity];
        }

        /// <summary> 拷贝环形缓冲区到数组 </summary>
        private static void CopyFromRingBuffer(byte[] ring, int head, byte[] dest, int length, int capacity)
        {
            int firstPart = Math.Min(length, capacity - head);
            Buffer.BlockCopy(ring, head, dest, 0, firstPart);
            if (length > firstPart)
            {
                Buffer.BlockCopy(ring, 0, dest, firstPart, length - firstPart);
            }
        }

        byte[][] memsCommands = new byte[5][];
        private readonly DotMatrixUpdate_Temp[] dotUpdatesTemp = new DotMatrixUpdate_Temp[5];
        private readonly DotMatrixUpdate_Pres[] dotUpdatesPres = new DotMatrixUpdate_Pres[5];

        private readonly DotMatrixUpdate_Temp[] dotUpdatesTemp27 = new DotMatrixUpdate_Temp[5];
        private readonly DotMatrixUpdate_Pres[] dotUpdatesPres27 = new DotMatrixUpdate_Pres[5];

        List<int> activeSensors = new List<int>();

        private void InitializeDotUpdates()
        {
            for (int i = 0; i < 5; i++)
            {
                dotUpdatesTemp[i] = new DotMatrixUpdate_Temp
                {
                    SensorIndex = i,
                    TempValues = new double[40] // 可按实际通道数修改
                };
                dotUpdatesPres[i] = new DotMatrixUpdate_Pres
                {
                    SensorIndex = i,
                    PressureValues = new double[40]
                };
            }
            for (int i = 0; i < 5; i++)
            {
                dotUpdatesTemp27[i] = new DotMatrixUpdate_Temp
                {
                    SensorIndex = i,
                    TempValues = new double[5] // 可按实际通道数修改
                };
                dotUpdatesPres27[i] = new DotMatrixUpdate_Pres
                {
                    SensorIndex = i,
                    PressureValues = new double[135]
                };
            }
        }

        public Main()
        {
            InitializeComponent();
            this.Load += Main_Load;
        }
        private void Main_Load(object sender, EventArgs e)
        {
            // === MEMS 指令预生成（5 个地址） ===

            for (int i = 0; i < 5; i++)
            {
                memsCommands[i] = new byte[] { 0xA5, 0x5A, (byte)(i + 1) };
            }

            // 先把页面保存下来
            tp1 = tabPage1;
            tp2 = tabPage2;
            tp3 = tabPage3;
            tp4 = tabPage4;

            // 根据默认选项显示
            UpdateTabPages();

            LoadFromJson();
            LoadFromSettingJson();
            if (textBox1.Text == null || textBox1.Text == "")
            {
                textBox1.Text = "500";
            }
            if (textBox2.Text == null || textBox2.Text == "")
            {
                textBox2.Text = "50";
            }
            if (comboBox2.SelectedIndex == -1)
            {
                comboBox2.SelectedIndex = 0;
            }
            if (comboBox5.SelectedText == null || comboBox5.SelectedText == "")
            {
                comboBox5.SelectedIndex = 0;
            }
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            LoadMeasureSetJson();

            // 生成 8 通道数据源
            var data = new List<object>();

            for (int i = 1; i <= 5; i++)
            {
                for (int j = 1; j <= 8; j++)
                {
                    data.Add(new { Value = i, Text = $"CH{i}-{j}" });
                }

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
            InitializeDotUpdates();

            if (chuanGanQiType == "Yingbianhua")
            {
                InitModel();
            }


            try
            {
                StartPacketProcessingThread();
                StartInferenceThread();

                // 生成文件路径
                string filePath = Path.Combine(excelSavePath,
                    $"packets_{DateTime.Now:yyyyMMdd_HHmmss}.csv");

                // 创建全局 StreamWriter，不写表头
                packetWriter = new StreamWriter(filePath, true, new System.Text.UTF8Encoding(false));
                packetWriter.AutoFlush = true; // 每次写入自动刷新

                // 启动后台写线程
                StartWorkers();
                /*                fileWriterThread = new Thread(FileWriterLoop);
                                fileWriterThread.IsBackground = true;
                                fileWriterThread.Start();*/

                //TestDraw();

                /*                // 打开监控日志文件
                                monitorWriter = new StreamWriter("monitor_log.txt", append: true, Encoding.UTF8) { AutoFlush = true };

                                // 启动监控线程
                                monitorThread = new Thread(() =>
                                {
                                    try
                                    {
                                        while (monitorRunning)
                                        {
                                            string logLine = string.Format(
                                                "[{0:HH:mm:ss}] UIQ={1}, RawQ={2}, FileQ={3}, Recv={4}, Saved={5}, Drop={6}, GC0={7}, GC1={8}, GC2={9}",
                                                DateTime.Now,
                                                uiQueue.Count,
                                                fileRawQueue.Count,
                                                fileQueue.Count,
                                                totalPacketCount,
                                                savedPacketCount,
                                                totalPacketCount - savedPacketCount,
                                                GC.CollectionCount(0),
                                                GC.CollectionCount(1),
                                                GC.CollectionCount(2)
                                            );

                                            lock (monitorWriter)
                                            {
                                                monitorWriter.WriteLine(logLine);
                                            }

                                            Thread.Sleep(5000); // 每5秒记录一次
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        MessageBox.Show("监控线程错误: " + ex.Message);
                                    }
                                });
                                monitorThread.IsBackground = true;
                                monitorThread.Start();*/

            }
            catch (Exception ex)
            {
                MessageBox.Show("初始化日志文件失败: " + ex.Message);
            }

            //StartSimulation(); // 开始模拟
        }
        public void InitModel()
        {
            try
            {
                if (model1Path != "" && model2Path != "")
                {
                    sessionModel1 = new InferenceSession(model1Path);
                    sessionModel2 = new InferenceSession(model2Path);
                }

            }
            catch (Exception ex)
            {
                MessageBox.Show("加载模型失败: " + ex.Message);
            }

        }

        private void UpdateTabPages()
        {
            tabControl1.TabPages.Clear(); // 清空所有页面

            if (chuanGanQiType == "MEMS")
            {
                //tabControl1.TabPages.Add(tp2);
                tabControl1.TabPages.Add(tp3);
                tabControl1.TabPages.Add(tp4);
            }
            else if (chuanGanQiType == "Yingbianhua")
            {
                tabControl1.TabPages.Add(tp1);
            }
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
            double[] values = new double[9];
            for (int i = 0; i < 9; i++)
            {
                values[i] = ((counter + i) * 10000) % 100000; // 循环递增，超过100000从0开始
            }

            //panel_finger1_point.Values = values;
            panel_finger1_cloud27.Values = values;
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


                for (int i = 0; i < memsCommands.Length; i++)
                {
                    serialPort.Write(memsCommands[i], 0, memsCommands[i].Length);
                    Thread.Sleep(2);
                    if (serialPort.BytesToRead > 0)
                        activeSensors.Add(i);
                }

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
        /*        private void SerialReadLoop(CancellationToken token)
                {
                    byte[] buffer = new byte[4096];
                    const int MaxBufferSize = 65536;
                    byte[] recvBuffer = new byte[MaxBufferSize];
                    int recvHead = 0; // 有效数据起始
                    int recvTail = 0; // 有效数据末尾

                    // 使用 ArrayPool 管理包缓冲区
                    ArrayPool<byte> pool = ArrayPool<byte>.Shared;

                    while (!token.IsCancellationRequested && serialPort != null && serialPort.IsOpen)
                    {
                        try
                        {
                            int bytesRead = serialPort.Read(buffer, 0, buffer.Length);
                            if (bytesRead <= 0) continue;

                            lock (serialLock)
                            {
                                // 写入环形缓冲区
                                for (int i = 0; i < bytesRead; i++)
                                {
                                    recvBuffer[recvTail] = buffer[i];
                                    recvTail = (recvTail + 1) % MaxBufferSize;

                                    // 覆盖模式：避免写满
                                    if (recvTail == recvHead)
                                        recvHead = (recvHead + 1) % MaxBufferSize;
                                }

                                // 解析数据
                                if (chuanGanQiType == "MEMS")
                                {
                                    while (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) >= 6)
                                    {
                                        if (!(PeekByte(recvBuffer, recvHead, 0, MaxBufferSize) == 0x42 &&
                                              PeekByte(recvBuffer, recvHead, 1, MaxBufferSize) == 0x54))
                                        {
                                            recvHead = (recvHead + 1) % MaxBufferSize;
                                            continue;
                                        }

                                        int length = PeekByte(recvBuffer, recvHead, 2, MaxBufferSize);
                                        if (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) < length)
                                            break;

                                        byte[] packet = pool.Rent(length); // 从池里拿内存
                                        CopyFromRingBuffer(recvBuffer, recvHead, packet, length, MaxBufferSize);
                                        recvHead = (recvHead + length) % MaxBufferSize;

                                        // 校验
                                        byte checksum = 0;
                                        for (int i = 2; i < length - 1; i++)
                                            checksum += packet[i];

                                        if (checksum == packet[length - 1])
                                        {
                                            EnqueuePacket(packet);
                                        }
                                        else
                                        {
                                            LogToConsole("MEMS 校验失败");
                                            pool.Return(packet);
                                        }
                                    }
                                }
                                else if (chuanGanQiType == "Yingbianhua")
                                {
                                    const int PACKET_LENGTH = 343;

                                    while (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) >= PACKET_LENGTH)
                                    {
                                        if (!(PeekByte(recvBuffer, recvHead, 0, MaxBufferSize) == 0xAA &&
                                              PeekByte(recvBuffer, recvHead, 1, MaxBufferSize) == 0xAA &&
                                              PeekByte(recvBuffer, recvHead, 2, MaxBufferSize) == 0xAA &&
                                              PeekByte(recvBuffer, recvHead, 3, MaxBufferSize) == 0xAA))
                                        {
                                            recvHead = (recvHead + 1) % MaxBufferSize;
                                            continue;
                                        }

                                        if (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) < PACKET_LENGTH)
                                            break;

                                        byte[] packet = pool.Rent(PACKET_LENGTH);
                                        CopyFromRingBuffer(recvBuffer, recvHead, packet, PACKET_LENGTH, MaxBufferSize);
                                        recvHead = (recvHead + PACKET_LENGTH) % MaxBufferSize;

                                        if (packet[PACKET_LENGTH - 4] == 0xBB &&
                                            packet[PACKET_LENGTH - 3] == 0xBB &&
                                            packet[PACKET_LENGTH - 2] == 0xBB &&
                                            packet[PACKET_LENGTH - 1] == 0xBB)
                                        {
                                            EnqueuePacket(packet.AsSpan(0, PACKET_LENGTH).ToArray());
                                        }
                                        else
                                        {
                                            LogToConsole("Yingbianhua 包尾错误");
                                            pool.Return(packet);
                                        }
                                    }
                                }
                            }
                        }
                        catch (TimeoutException) { }
                        catch (IOException) { break; }
                        catch (InvalidOperationException) { break; }
                        catch (Exception ex)
                        {
                            LogToConsole("串口读取异常：" + ex.Message);
                            break;
                        }
                    }
                }*/
        private void SerialReadLoop(CancellationToken token)
        {
            byte[] buffer = new byte[4096];
            const int MaxBufferSize = 65536;
            byte[] recvBuffer = new byte[MaxBufferSize];
            int recvHead = 0;
            int recvTail = 0;

            ArrayPool<byte> pool = ArrayPool<byte>.Shared;

            /*            int memsSensorIndex = 0;
                        int responseTimeoutMs = 5; // 等待应答超时时间
                        Stopwatch sw = new Stopwatch();*/
            int memsSensorIndex = 0;

            // === 精确计时器 ===
            Stopwatch sw = Stopwatch.StartNew();
            //double pollIntervalMs = 2; // 每 2ms 轮询一次（5 个传感器 = 10ms，100Hz）

            long nextPollTicks = 0;
            long ticksPerMs = Stopwatch.Frequency / 1000;
            double cycleMs = 10.0; // 一圈 10ms
            double pollIntervalMs = cycleMs / activeSensors.Count;


            while (!token.IsCancellationRequested && serialPort != null && serialPort.IsOpen)
            {
                try
                {
                    // === 定时发送 MEMS 轮询命令 ===
                    if (chuanGanQiType == "MEMS")
                    {
                        long nowTicks = sw.ElapsedTicks;
                        if (nowTicks >= nextPollTicks)
                        {
                            int sensorIndex = activeSensors[memsSensorIndex];
                            serialPort.Write(memsCommands[sensorIndex], 0, memsCommands[sensorIndex].Length);

                            memsSensorIndex = (memsSensorIndex + 1) % activeSensors.Count;
                            nextPollTicks = sw.ElapsedTicks + (long)(pollIntervalMs * ticksPerMs);

                        }
                    }

                    // === 读取串口数据 ===
                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);
                    if (bytesRead > 0)
                    {
                        lock (serialLock)
                        {
                            for (int i = 0; i < bytesRead; i++)
                            {
                                recvBuffer[recvTail] = buffer[i];
                                recvTail = (recvTail + 1) % MaxBufferSize;

                                if (recvTail == recvHead)
                                    recvHead = (recvHead + 1) % MaxBufferSize; // 覆盖模式
                            }

                            if (chuanGanQiType == "MEMS")
                            {
                                while (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) >= 6)
                                {
                                    if (!(PeekByte(recvBuffer, recvHead, 0, MaxBufferSize) == 0x42 &&
                                          PeekByte(recvBuffer, recvHead, 1, MaxBufferSize) == 0x54))
                                    {
                                        recvHead = (recvHead + 1) % MaxBufferSize;
                                        continue;
                                    }

                                    int length = PeekByte(recvBuffer, recvHead, 2, MaxBufferSize);
                                    if (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) < length)
                                        break;

                                    byte[] packet = pool.Rent(length);
                                    CopyFromRingBuffer(recvBuffer, recvHead, packet, length, MaxBufferSize);
                                    recvHead = (recvHead + length) % MaxBufferSize;

                                    byte checksum = 0;
                                    for (int i = 2; i < length - 1; i++)
                                        checksum += packet[i];

                                    if (checksum == packet[length - 1])
                                    {
                                        EnqueuePacket(packet);
                                    }
                                    else
                                    {
                                        //LogToConsole("MEMS 校验失败");
                                        pool.Return(packet);
                                    }
                                }
                            }
                            else if (chuanGanQiType == "Yingbianhua")
                            {
                                const int PACKET_LENGTH = 343;

                                while (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) >= PACKET_LENGTH)
                                {
                                    if (!(PeekByte(recvBuffer, recvHead, 0, MaxBufferSize) == 0xAA &&
                                          PeekByte(recvBuffer, recvHead, 1, MaxBufferSize) == 0xAA &&
                                          PeekByte(recvBuffer, recvHead, 2, MaxBufferSize) == 0xAA &&
                                          PeekByte(recvBuffer, recvHead, 3, MaxBufferSize) == 0xAA))
                                    {
                                        recvHead = (recvHead + 1) % MaxBufferSize;
                                        continue;
                                    }

                                    if (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) < PACKET_LENGTH)
                                        break;

                                    byte[] packet = pool.Rent(PACKET_LENGTH);
                                    CopyFromRingBuffer(recvBuffer, recvHead, packet, PACKET_LENGTH, MaxBufferSize);
                                    recvHead = (recvHead + PACKET_LENGTH) % MaxBufferSize;

                                    if (packet[PACKET_LENGTH - 4] == 0xBB &&
                                        packet[PACKET_LENGTH - 3] == 0xBB &&
                                        packet[PACKET_LENGTH - 2] == 0xBB &&
                                        packet[PACKET_LENGTH - 1] == 0xBB)
                                    {
                                        EnqueuePacket(packet.AsSpan(0, PACKET_LENGTH).ToArray());
                                    }
                                    else
                                    {
                                        LogToConsole("Yingbianhua 包尾错误");
                                        pool.Return(packet);
                                    }
                                }
                            }
                        }
                    }
                }
                catch (TimeoutException) { }
                catch (IOException) { break; }
                catch (InvalidOperationException) { break; }
                catch (Exception ex)
                {
                    LogToConsole("串口读取异常：" + ex.Message);
                    break;
                }
            }
            /*            while (!token.IsCancellationRequested && serialPort != null && serialPort.IsOpen)
                        {
                            try
                            {
                                if (chuanGanQiType == "MEMS")
                                {
                                    // === 1. 发送当前传感器命令 ===
                                    serialPort.Write(memsCommands[memsSensorIndex], 0, memsCommands[memsSensorIndex].Length);

                                    // === 2. 等待应答 ===
                                    sw.Restart();
                                    bool gotResponse = false;

                                    while (sw.ElapsedMilliseconds < responseTimeoutMs)
                                    {
                                        int available = serialPort.BytesToRead;
                                        if (available > 0)
                                        {
                                            int toRead = Math.Min(available, buffer.Length);
                                            int bytesRead = serialPort.Read(buffer, 0, toRead);

                                            lock (serialLock)
                                            {
                                                for (int i = 0; i < bytesRead; i++)
                                                {
                                                    recvBuffer[recvTail] = buffer[i];
                                                    recvTail = (recvTail + 1) % MaxBufferSize;

                                                    if (recvTail == recvHead)
                                                        recvHead = (recvHead + 1) % MaxBufferSize; // 覆盖模式
                                                }

                                                // === 尝试解析 MEMS 包 ===
                                                while (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) >= 6)
                                                {
                                                    if (!(PeekByte(recvBuffer, recvHead, 0, MaxBufferSize) == 0x42 &&
                                                          PeekByte(recvBuffer, recvHead, 1, MaxBufferSize) == 0x54))
                                                    {
                                                        recvHead = (recvHead + 1) % MaxBufferSize;
                                                        continue;
                                                    }

                                                    int length = PeekByte(recvBuffer, recvHead, 2, MaxBufferSize);
                                                    if (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) < length)
                                                        break;

                                                    byte[] packet = pool.Rent(length);
                                                    CopyFromRingBuffer(recvBuffer, recvHead, packet, length, MaxBufferSize);
                                                    recvHead = (recvHead + length) % MaxBufferSize;

                                                    byte checksum = 0;
                                                    for (int i = 2; i < length - 1; i++)
                                                        checksum += packet[i];

                                                    if (checksum == packet[length - 1])
                                                    {
                                                        EnqueuePacket(packet);
                                                        gotResponse = true;
                                                        break;
                                                    }
                                                    else
                                                    {
                                                        pool.Return(packet);
                                                    }
                                                }
                                            }
                                        }

                                        if (gotResponse) break;
                                        Thread.Sleep(1); // 避免空转 CPU
                                    }


                                    // === 3. 切换下一个传感器（无论是否超时） ===
                                    memsSensorIndex = (memsSensorIndex + 1) % memsCommands.Length;
                                }
                                else if (chuanGanQiType == "Yingbianhua")
                                {
                                    int bytesRead = serialPort.Read(buffer, 0, buffer.Length);
                                    if (bytesRead > 0)
                                    {
                                        lock (serialLock)
                                        {
                                            for (int i = 0; i < bytesRead; i++)
                                            {
                                                recvBuffer[recvTail] = buffer[i];
                                                recvTail = (recvTail + 1) % MaxBufferSize;

                                                if (recvTail == recvHead)
                                                    recvHead = (recvHead + 1) % MaxBufferSize; // 覆盖模式
                                            }
                                            const int PACKET_LENGTH = 343;

                                            while (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) >= PACKET_LENGTH)
                                            {
                                                if (!(PeekByte(recvBuffer, recvHead, 0, MaxBufferSize) == 0xAA &&
                                                      PeekByte(recvBuffer, recvHead, 1, MaxBufferSize) == 0xAA &&
                                                      PeekByte(recvBuffer, recvHead, 2, MaxBufferSize) == 0xAA &&
                                                      PeekByte(recvBuffer, recvHead, 3, MaxBufferSize) == 0xAA))
                                                {
                                                    recvHead = (recvHead + 1) % MaxBufferSize;
                                                    continue;
                                                }

                                                if (GetAvailableBytes(recvHead, recvTail, MaxBufferSize) < PACKET_LENGTH)
                                                    break;

                                                byte[] packet = pool.Rent(PACKET_LENGTH);
                                                CopyFromRingBuffer(recvBuffer, recvHead, packet, PACKET_LENGTH, MaxBufferSize);
                                                recvHead = (recvHead + PACKET_LENGTH) % MaxBufferSize;

                                                if (packet[PACKET_LENGTH - 4] == 0xBB &&
                                                    packet[PACKET_LENGTH - 3] == 0xBB &&
                                                    packet[PACKET_LENGTH - 2] == 0xBB &&
                                                    packet[PACKET_LENGTH - 1] == 0xBB)
                                                {
                                                    EnqueuePacket(packet.AsSpan(0, PACKET_LENGTH).ToArray());
                                                }
                                                else
                                                {
                                                    LogToConsole("Yingbianhua 包尾错误");
                                                    pool.Return(packet);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            catch (TimeoutException) { }
                            catch (IOException) { break; }
                            catch (InvalidOperationException) { break; }
                            catch (Exception ex)
                            {
                                LogToConsole("串口读取异常：" + ex.Message);
                                break;
                            }
                        }*/
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
        /*        private void StartRequestThread()
                {
                    Task.Run(() =>
                    {
                        int memsSensorIndex = 0;

                        // === 精确计时器 ===
                        Stopwatch sw = Stopwatch.StartNew();
                        double pollIntervalMs = 2.0; // 每 2ms 轮询一次（5 个传感器 = 10ms，100Hz）

                        long nextPollTicks = 0;
                        long ticksPerMs = Stopwatch.Frequency / 1000;

                        while (serialPort != null && serialPort.IsOpen)
                        {
                            try
                            {
                                // === 定时发送 MEMS 轮询命令 ===
                                if (chuanGanQiType == "MEMS")
                                {
                                    long nowTicks = sw.ElapsedTicks;
                                    if (nowTicks >= nextPollTicks)
                                    {
                                        // 发送当前传感器指令
                                        serialPort.Write(memsCommands[memsSensorIndex], 0, memsCommands[memsSensorIndex].Length);

                                        // 切换下一个传感器
                                        memsSensorIndex = (memsSensorIndex + 1) % memsCommands.Length;

                                        // 设置下一次发送时刻
                                        nextPollTicks = nowTicks + (long)(pollIntervalMs * ticksPerMs);

                                        LogToConsole("Sending");

                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                LogToConsole("请求线程异常: " + ex.Message);
                            }
                        }
                    });
                }*/

        private bool[] activeChannels = new bool[40];   // 标记哪些通道有数据
        private void ProcessPacketForUI(List<string> uiData)
        {
            if (chuanGanQiType == "MEMS")
            {

                try
                {

                    // 地址解析
                    if (!int.TryParse(uiData[0].Replace("S", ""), out int addr)) return;
                    int sensorIndex = addr - 1;
                    if (sensorIndex < 0 || sensorIndex >= 5) return;

                    string type = uiData[1];

                    if (isZeroing && type == "F5")
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            int channelIndex = sensorIndex * 8 + i;
                            if (channelIndex < 0 || channelIndex >= 40) continue;

                            if (!pressureCalibBuffers.ContainsKey(channelIndex))
                                pressureCalibBuffers[channelIndex] = new List<double>();

                            if (double.TryParse(uiData[2 + i], out double pressure))
                            {
                                pressureCalibBuffers[channelIndex].Add(pressure);
                                channelZeroingCounts[channelIndex]++;
                                activeChannels[channelIndex] = true; // 标记该通道有效
                            }
                        }

                        // 判断已激活的通道是否都采满
                        bool allActiveDone = true;
                        for (int ch = 0; ch < 40; ch++)
                        {
                            if (activeChannels[ch] && channelZeroingCounts[ch] < ZeroingTargetPackets)
                            {
                                allActiveDone = false;
                                break;
                            }
                        }

                        if (allActiveDone)
                        {
                            // 计算零点偏移
                            for (int ch = 0; ch < 40; ch++)
                            {
                                if (activeChannels[ch] &&
                                    pressureCalibBuffers.ContainsKey(ch) &&
                                    pressureCalibBuffers[ch].Count > 0)
                                {
                                    channelZeroOffsets[ch] = pressureCalibBuffers[ch].Average();
                                }
                            }

                            // 校零完成
                            isZeroing = false;
                            Array.Clear(channelZeroingCounts, 0, channelZeroingCounts.Length);
                            Array.Clear(activeChannels, 0, activeChannels.Length); // 清理激活状态

                            Action showMsg = () => MessageBox.Show("校零完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            if (console_textBox.InvokeRequired)
                                console_textBox.BeginInvoke(showMsg);
                            else
                                showMsg();
                        }
                    }

                    // === 获取复用对象 ===
                    var dotUpdate_Temp = dotUpdatesTemp[sensorIndex];
                    var dotUpdate_Pres = dotUpdatesPres[sensorIndex];

                    // === 解析值并填充对象 ===
                    for (int i = 0; i < 8; i++)
                    {
                        int channelIndex = sensorIndex * 8 + i;
                        if (!double.TryParse(uiData[2 + i], out double value)) continue;

                        if (type == "F4") // 温度
                        {
                            //dotUpdate_Temp.TempValues[channelIndex] = Math.Round(value / 1000.0, 1);
                            dotUpdate_Temp.TempValues[channelIndex] = value;
                            if (wendutu)
                            {
                                var graphUpdate = new GraphUpdate
                                {
                                    SensorIndex = sensorIndex,
                                    Channel = channelIndex,
                                    Index = packetIndex,
                                    Temperature = dotUpdate_Temp.TempValues[channelIndex]
                                };

                                if (tempQueue.Count >= MaxQueueSize) tempQueue.TryDequeue(out _);
                                tempQueue.Enqueue(graphUpdate);
                            }
                        }
                        else if (type == "F5") // 压力
                        {
                            double correctedPressure = value - channelZeroOffsets[channelIndex];
                            if (correctedPressure > 1000000)
                            {
                                Console.Write("");
                            }
                            double pressureDenoised = DenoiseByMedian(channelIndex, correctedPressure);

                            dotUpdate_Pres.PressureValues[channelIndex] = pressureDenoised;

                            if (yalitu)
                            {

                                var graphUpdate = new GraphUpdate
                                {
                                    SensorIndex = sensorIndex,
                                    Channel = channelIndex,
                                    Index = packetIndex,
                                    Pressure = pressureDenoised
                                };

                                if (graphQueue.Count >= MaxQueueSize) graphQueue.TryDequeue(out _);
                                graphQueue.Enqueue(graphUpdate);
                            }
                        }
                    }

                    // === 入队 UI 显示前检查是否全 0 ===
                    bool HasNonZero(double[] arr)
                    {
                        foreach (var v in arr)
                            if (v != 0) return true;
                        return false;
                    }

                    if (diantu)
                    {
                        if (HasNonZero(dotUpdate_Temp.TempValues))
                        {
                            if (dotQueue_Temp.Count >= MaxQueueSize) dotQueue_Temp.TryDequeue(out _);
                            dotQueue_Temp.Enqueue(dotUpdate_Temp);
                        }

                        if (HasNonZero(dotUpdate_Pres.PressureValues))
                        {
                            if (dotQueue_Pres.Count >= MaxQueueSize) dotQueue_Pres.TryDequeue(out _);
                            dotQueue_Pres.Enqueue(dotUpdate_Pres);
                        }
                    }

                    Interlocked.Increment(ref packetIndex);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("ProcessPacketForUI error: " + ex.ToString());
                }
                /*            {
                                try
                                {
                                    // uiData 格式：
                                    // [0] 地址 (1~5)
                                    // [1] 类型 ("F4"=温度, "F5"=压力)
                                    // [2]~[9] 8个通道的值

                                    if (!int.TryParse(uiData[0].Replace("S", ""), out int addr)) return;
                                    string type = uiData[1];

                                    // === 校零采集逻辑 ===
                                    if (isZeroing)
                                    {
                                        for (int i = 0; i < 8; i++)
                                        {
                                            int channelIndex = (addr - 1) * 8 + i; // 根据地址计算全局通道索引
                                            //int channelIndex = i;

                                            if (channelIndex < 0 || channelIndex >= 40) continue;

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
                                    DotMatrixUpdate_Temp dotUpdate_Temp = new DotMatrixUpdate_Temp
                                    {
                                        TempValues = new double[40],
                                        SensorIndex = addr - 1
                                    };
                                    DotMatrixUpdate_Pres dotUpdate_Pres = new DotMatrixUpdate_Pres
                                    {
                                        PressureValues = new double[40],
                                        SensorIndex = addr - 1
                                    };
                                    for (int i = 0; i < 8; i++)
                                    {
                                        int channelIndex = (addr - 1) * 8 + i;
                                        //int channelIndex = i;

                                        if (channelIndex < 0 || channelIndex >= 40) continue;

                                        if (double.TryParse(uiData[2 + i], out double value))
                                        {
                                            if (type == "F4") // 温度
                                            {
                                                double temp = Math.Round(value / 1000.0, 1);
                                                dotUpdate_Temp.TempValues[channelIndex] = temp;
                                                if(temp == 0 || dotUpdate_Temp.SensorIndex == 4)
                                                {
                                                    Console.Write("");
                                                }
                                                if (wendutu)
                                                {
                                                    var graphUpdate = new GraphUpdate
                                                    {
                                                        SensorIndex = addr - 1,
                                                        Channel = channelIndex,
                                                        Index = packetIndex,
                                                        Temperature = temp
                                                    };

                                                    if (tempQueue.Count >= MaxQueueSize)
                                                        tempQueue.TryDequeue(out _);
                                                    tempQueue.Enqueue(graphUpdate);
                                                }
                                            }
                                            else if (type == "F5") // 压力
                                            {
                                                double pressure = value - channelZeroOffsets[channelIndex];
                                                dotUpdate_Pres.PressureValues[channelIndex] = pressure;

                                                if (yalitu)
                                                {
                                                    double correctedPressure = DenoiseByMedian(channelIndex, pressure);
                                                    var graphUpdate = new GraphUpdate
                                                    {
                                                        SensorIndex = addr - 1,
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
                                        if (dotQueue_Temp.Count >= MaxQueueSize)
                                            dotQueue_Temp.TryDequeue(out _);
                                        dotQueue_Temp.Enqueue(dotUpdate_Temp);

                                        if (dotQueue_Pres.Count >= MaxQueueSize)
                                            dotQueue_Pres.TryDequeue(out _);
                                        dotQueue_Pres.Enqueue(dotUpdate_Pres);
                                    }

                                    Interlocked.Increment(ref packetIndex);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show("ProcessPacketForUI error: " + ex.ToString());
                                }*/
            }
            else if (chuanGanQiType == "Yingbianhua")
            {
                try
                {
                    if (!int.TryParse(uiData[0].Replace("S", ""), out int addr))
                        return;
                    int sensorIndex = addr - 1;
                    // === 校零采集逻辑 ===
                    if (isZeroing)
                    {
                        for (int i = 0; i < 27; i++) // 遍历该传感器的通道
                        {
                            int channelIndex = sensorIndex * 27 + i;
                            if (channelIndex < 0 || channelIndex >= 135) continue;

                            if (!pressureCalibBuffers27.ContainsKey(channelIndex))
                                pressureCalibBuffers27[channelIndex] = new List<double>();

                            if (double.TryParse(uiData[1 + i], out double pressure))
                            {
                                pressureCalibBuffers27[channelIndex].Add(pressure);
                                channelZeroingCounts27[channelIndex]++;
                            }
                        }

                        // 判断所有通道是否都达到目标采样数
                        bool allChannelsDone = true;
                        for (int ch = 0; ch < 135; ch++)
                        {
                            if (channelZeroingCounts27[ch] < ZeroingTargetPackets)
                            {
                                allChannelsDone = false;
                                break;
                            }
                        }

                        if (allChannelsDone)
                        {
                            // 计算每个通道零点偏移
                            for (int ch = 0; ch < 135; ch++)
                            {
                                if (pressureCalibBuffers27.ContainsKey(ch) && pressureCalibBuffers27[ch].Count > 0)
                                    channelZeroOffsets27[ch] = pressureCalibBuffers27[ch].Average();
                            }

                            // 校零完成
                            isZeroing = false;
                            Array.Clear(channelZeroingCounts27, 0, channelZeroingCounts27.Length); // 清理计数

                            Action showMsg = () => MessageBox.Show("校零完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            if (console_textBox.InvokeRequired)
                                console_textBox.BeginInvoke(showMsg);
                            else
                                showMsg();
                        }
                    }



                    int sensorCount = 5;
                    int pressureCount = 27;

                    // === 获取复用对象 ===
                    var dotUpdate_Temp27 = dotUpdatesTemp27[sensorIndex];
                    var dotUpdate_Pres27 = dotUpdatesPres27[sensorIndex];

                    int index = 0;
                    for (int s = 0; s < sensorCount; s++)
                    {
                        string sensorLabel = uiData[index++]; // "S1", "S2"...

                        // 压力值
                        double[] pressures = new double[pressureCount];
                        for (int p = 0; p < pressureCount; p++)
                        {
                            if (double.TryParse(uiData[index++], out double rawPressure))
                            {
                                int channelIndex = s * pressureCount + p;
                                double pressure = rawPressure - channelZeroOffsets27[channelIndex];
                                dotUpdate_Pres27.PressureValues[channelIndex] = pressure;
                                pressures[p] = pressure;
                            }
                        }

                        // 温度值
                        if (double.TryParse(uiData[index++], out double rawTemp))
                        {
                            dotUpdate_Pres27.TempValues[s] = rawTemp;
                        }

                        // 陀螺仪值 (6个)
                        double[] gyros = new double[6];
                        for (int g = 0; g < 6; g++)
                        {
                            if (double.TryParse(uiData[index++], out double gyro))
                                gyros[g] = gyro;
                        }

                        // === GraphUpdate：每个压力通道绑定该传感器的6轴值 ===
                        if (yalitu)
                        {
                            for (int p = 0; p < pressureCount; p++)
                            {
                                int channelIndex = s * pressureCount + p;
                                double correctedPressure = DenoiseByMedian(channelIndex, pressures[p]);

                                var graphUpdate = new GraphUpdate
                                {
                                    SensorIndex = s,
                                    Channel = p,
                                    Index = packetIndex,
                                    Pressure = correctedPressure,
                                    GyroValues = gyros
                                };

                                if (graphQueue.Count >= MaxQueueSize)
                                    graphQueue.TryDequeue(out _);
                                graphQueue.Enqueue(graphUpdate);
                            }
                        }
                    }

                    // === UI更新 ===
                    if (diantu)
                    {
                        if (dotQueue_Pres.Count >= MaxQueueSize)
                            dotQueue_Pres.TryDequeue(out _);
                        dotQueue_Pres.Enqueue(dotUpdate_Pres27);
                    }

                    Interlocked.Increment(ref packetIndex);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("ProcessPacketForUI error: " + ex.Message);
                }
            }

        }

        private int tickCount = 0;
        private const int GraphRefreshInterval = 2;  // 每 2 个 tick 刷新滚动图
        private const int PanelRefreshInterval = 2;  // 每 2 个 tick 刷新点阵和云图
        //private readonly double[] panelValuesBuffer8 = new double[5 * 8]; // 预分配数组，零分配
        //private readonly double[] cloudValuesBuffer8 = new double[5 * 8];  // 每个 Panel 9 个点
        private readonly double[] panelValuesBuffer = new double[5 * 27]; // 预分配数组，零分配
        private readonly double[] cloudValuesBuffer = new double[5 * 9];  // 每个 Panel 9 个点
        private readonly double[] gyroValuesBuffer = new double[5 * 6];  // 每个 Panel 9 个点
        private readonly double[][] panelValuesPerSensor = Enumerable.Range(0, 5).Select(_ => new double[8]).ToArray();
        private readonly double[][] cloudValuesPerSensor = Enumerable.Range(0, 5).Select(_ => new double[8]).ToArray();


        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            bool needRefresh = false;
            GraphUpdate graphUpdate;

            if (chuanGanQiType == "MEMS")
            {
                var pane = zedGraphControl1.GraphPane;
                var pane_temp = zedGraphControl19.GraphPane;

                // 控制 X 轴显示范围
                double xMin = packetIndex - MaxVisiblePackets;
                if (xMin < 0) xMin = 0;
                double xMax = packetIndex;

                pane.XAxis.Scale.Min = xMin;
                pane.XAxis.Scale.Max = xMax;
                pane.YAxis.Scale.MagAuto = false;
                pane.YAxis.Scale.Mag = 0;

                pane_temp.XAxis.Scale.Min = xMin;
                pane_temp.XAxis.Scale.Max = xMax;
                pane_temp.YAxis.Scale.MagAuto = false;
                pane_temp.YAxis.Scale.Mag = 0;


                // === 更新滚动图（zedGraphControl1） ===
                while (graphQueue.TryDequeue(out graphUpdate))
                {
                    if (choosedFinger1 != -1)
                    {
                        int chuanganqiIndex = choosedFinger1;
                        if (chuanganqiIndex == graphUpdate.SensorIndex)
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
                    }
                }

                if (needRefresh)
                {
                    zedGraphControl1.AxisChange();
                    zedGraphControl1.Invalidate();
                }

                while (tempQueue.TryDequeue(out graphUpdate))
                {
                    if (choosedFinger19 != -1)
                    {
                        int chuanganqiIndex = choosedFinger19;
                        if (chuanganqiIndex == graphUpdate.SensorIndex)
                        {
                            needRefresh = true;

                            if (!channelData_temp.ContainsKey(graphUpdate.Channel))
                            {
                                var list = new RollingPointPairList(MaxVisiblePackets + 100);
                                var curve = pane.AddCurve($"CH{graphUpdate.Channel + 1}", list, GetColor(graphUpdate.Channel), SymbolType.None);
                                channelData_temp[graphUpdate.Channel] = list;
                                channelCurves_temp[graphUpdate.Channel] = curve;
                            }

                            if (graphUpdate.Index >= xMin)
                            {
                                channelData_temp[graphUpdate.Channel].Add(graphUpdate.Index, graphUpdate.Temperature);
                            }
                        }
                    }
                }

                if (needRefresh)
                {
                    zedGraphControl19.AxisChange();
                    zedGraphControl19.Invalidate();
                }

                // === 更新点图和云图 Panels ===
                while (dotQueue_Pres.TryDequeue(out var dequeuedUpdate))
                {
                    var dotUpdate = dequeuedUpdate; // 建立副本，避免闭包问题
                    if (dotUpdate == null)
                        continue; // 跳过 null 元素

                    int sensorIndex = dotUpdate.SensorIndex;

                    if (sensorIndex < 0 || sensorIndex >= 5)
                        return; // 越界检查

                    // --- 压力点值 ---
                    Array.Copy(dotUpdate.PressureValues, sensorIndex * 8, panelValuesPerSensor[sensorIndex], 0, 8);

                    int startIndex = sensorIndex * 8;
                    int addr = sensorIndex + 1;

                    // 更新点图 Panel
                    var panelPoint = this.Controls.Find($"panel_finger{addr}_point", true).FirstOrDefault() as DoubleBufferedPanel;
                    if (panelPoint != null)
                    {
                        Array.Copy(panelValuesPerSensor[sensorIndex], panelPoint.Values, 8);
                        panelPoint.Invalidate();
                    }

                    // 更新云图 Panel
                    var panelCloud = this.Controls.Find($"panel_finger{addr}_cloud", true).FirstOrDefault() as DoubleBufferedPanelCloud;
                    var labelMax = this.Controls.Find($"label_finger{addr}_max", true).FirstOrDefault() as System.Windows.Forms.Label;
                    var labelMin = this.Controls.Find($"label_finger{addr}_min", true).FirstOrDefault() as System.Windows.Forms.Label;
                    if (panelCloud != null)
                    {
                        if (guiyihua)
                        {
                            panelCloud.Guiyihua = true;
                        }
                        else
                        {
                            panelCloud.Guiyihua = false;
                        }
                            Array.Copy(panelValuesPerSensor[sensorIndex], panelCloud.Values, 8);
                        panelCloud.Invalidate();

                        // 更新最大最小值标签
                        if (panelCloud.Values.Length > 0)
                        {
                            double maxVal = panelCloud.Values.Max();
                            double minVal = panelCloud.Values.Min();

                            if (labelMax != null)
                                labelMax.Text = $"Max: {maxVal:F0}";

                            if (labelMin != null)
                                labelMin.Text = $"Min: {minVal:F0}";
                        }
                    }

                }
                while (dotQueue_Temp.TryDequeue(out var dequeuedUpdate))
                {
                    var dotUpdate = dequeuedUpdate; // 建立副本，避免闭包问题
                    if (dotUpdate == null)
                        continue; // 跳过 null 元素

                    int sensorIndex = dotUpdate.SensorIndex;

                    if (sensorIndex < 0 || sensorIndex >= 5)
                        return; // 越界检查

                    Array.Copy(dotUpdate.TempValues, sensorIndex * 8, cloudValuesPerSensor[sensorIndex], 0, 8);

                    int startIndex = sensorIndex * 8;
                    int addr = sensorIndex + 1;


                    var panelPoint2 = this.Controls.Find($"panel_finger{addr}_point_temp", true).FirstOrDefault() as DoubleBufferedPanel;
                    if (panelPoint2 != null)
                    {
                        Array.Copy(dotUpdate.TempValues, startIndex, panelPoint2.Values, 0, 8);
                        panelPoint2.Invalidate();
                    }

                    // 更新云图 Panel
                    var panelCloud2 = this.Controls.Find($"panel_finger{addr}_cloud_temp", true).FirstOrDefault() as DoubleBufferedPanelCloud;
                    var labelMax_temp = this.Controls.Find($"label_finger{addr}_max_temp", true).FirstOrDefault() as System.Windows.Forms.Label;
                    var labelMin_temp = this.Controls.Find($"label_finger{addr}_min_temp", true).FirstOrDefault() as System.Windows.Forms.Label;
                    if (panelCloud2 != null)
                    {
                        Array.Copy(dotUpdate.TempValues, startIndex, panelCloud2.Values, 0, 8);
                        panelCloud2.Invalidate();

                        // 更新最大最小值标签
                        if (panelCloud2.Values.Length > 0)
                        {
                            double maxVal = panelCloud2.Values.Max();
                            double minVal = panelCloud2.Values.Min();

                            if (labelMax_temp != null)
                                labelMax_temp.Text = $"Max: {maxVal:F1}";

                            if (labelMin_temp != null)
                                labelMin_temp.Text = $"Min: {minVal:F1}";
                        }
                    }
                }
            }
            else if (chuanGanQiType == "Yingbianhua")
            {
                /*                // === 更新滚动图（zedGraphControl2） ===
                                while (graphQueue.TryDequeue(out graphUpdate))
                                {
                                    needRefresh = true;

                                    if (!channelData2.ContainsKey(graphUpdate.Channel))
                                    {
                                        var list = new RollingPointPairList(MaxVisiblePackets + 100);
                                        var curve = pane.AddCurve(
                                            $"CH{graphUpdate.SensorIndex + 1}-{graphUpdate.Channel + 1}",
                                            list,
                                            GetColor(graphUpdate.Channel),
                                            SymbolType.None);
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
                                    int sensorCount = 5;
                                    int pressureCount = 27;
                                    int groupSize = 3;   // 每3个通道归为一组

                                    for (int s = 0; s < sensorCount; s++)
                                    {
                                        //温度
                                        var labelTemp27 = this.Controls.Find($"label_finger{s + 1}_temp27", true).FirstOrDefault() as System.Windows.Forms.Label;
                                        if (labelTemp27 != null)
                                        {
                                            double[] values = new double[pressureCount];
                                            Array.Copy(dotUpdate.TempValues, 0, values, 0, sensorCount);

                                            labelTemp27.Text = $"{fingerNames[s]}      温度: {values[s]}";
                                        }

                                        //// === 压力点图 ===
                                        var panelPoint = this.Controls.Find($"panel_finger{s + 1}_point27", true).FirstOrDefault() as DoubleBufferedPanel27;
                                        if (panelPoint != null)
                                        {
                                            double[] values = new double[pressureCount];
                                            Array.Copy(dotUpdate.PressureValues, s * pressureCount, values, 0, pressureCount);
                                            panelPoint.Values = values;
                                            panelPoint.Invalidate();
                                        }

                                        // === 压力云图 ===
                                        var panelCloud = this.Controls.Find($"panel_finger{s + 1}_cloud27", true).FirstOrDefault() as DoubleBufferedPanelCloud27;
                                        var labelMax = this.Controls.Find($"label_finger{s + 1}_max27", true).FirstOrDefault() as System.Windows.Forms.Label;
                                        var labelMin = this.Controls.Find($"label_finger{s + 1}_min27", true).FirstOrDefault() as System.Windows.Forms.Label;
                                        if (panelCloud != null)
                                        {
                                            int pointCount = pressureCount / groupSize; // 27 / 3 = 9

                                            double[] values = new double[pointCount];
                                            for (int g = 0; g < pointCount; g++)
                                            {
                                                double sum = 0;
                                                for (int k = 0; k < groupSize; k++)
                                                {
                                                    int idx = s * pressureCount + g * groupSize + k;
                                                    sum += dotUpdate.PressureValues[idx];
                                                }
                                                values[g] = sum / groupSize;
                                            }

                                            panelCloud.Values = values; // 9 个点
                                            panelCloud.Invalidate();

                                            // 更新最大最小值标签
                                            if (values.Length > 0)
                                            {
                                                double maxVal = values.Max();
                                                double minVal = values.Min();

                                                if (labelMax != null)
                                                    labelMax.Text = $"Max: {maxVal:F1}";

                                                if (labelMin != null)
                                                    labelMin.Text = $"Min: {minVal:F1}";
                                            }
                                        }

                                        // === 显示模型推理结果 ===
                                        if(comboBox4.SelectedIndex != -1)
                                        {
                                            int chuanganqiIndex = comboBox4.SelectedIndex;
                                            if (latestResults.TryGetValue(chuanganqiIndex, out var result))
                                            {
                                                label_fxy.Text = $"Fxy: {result.Fx:F2}";
                                                label_fyx.Text = $"Fyx: {result.Fy:F2}";
                                                label_fz.Text = $"Fz: {result.Fz:F2}";
                                                label_label.Text = $"Label: {result.Label}";
                                                label_prob.Text = $"概率: {result.MaxProb:F2}";
                                            }
                                        }



                                        //// === 温度点图 ===
                                        //var panelPointTemp = this.Controls.Find($"panel_finger{s + 1}_point_temp", true).FirstOrDefault() as DoubleBufferedPanel;
                                        //if (panelPointTemp != null)
                                        //{
                                        //    double[] values = { dotUpdate.TempValues[s] };
                                        //    panelPointTemp.Values = values;
                                        //    panelPointTemp.Invalidate();
                                        //}

                                        //// === 温度云图 ===
                                        //var panelCloudTemp = this.Controls.Find($"panel_finger{s + 1}_cloud_temp", true).FirstOrDefault() as DoubleBufferedPanelCloud;
                                        //if (panelCloudTemp != null)
                                        //{
                                        //    double[] values = { dotUpdate.TempValues[s] };
                                        //    panelCloudTemp.Values = values;
                                        //    panelCloudTemp.Invalidate();
                                        //}
                                    }
                                }*/
                var pane3 = zedGraphControl3.GraphPane;

                // 控制 X 轴显示范围
                double xMin = packetIndex - MaxVisiblePackets;
                if (xMin < 0) xMin = 0;
                double xMax = packetIndex;

                pane3.XAxis.Scale.Min = xMin;
                pane3.XAxis.Scale.Max = xMax;
                pane3.YAxis.Scale.MagAuto = false;
                pane3.YAxis.Scale.Mag = 0;

                while (graphQueue.TryDequeue(out graphUpdate))
                {

                    if (choosedFinger3 != -1)
                    {
                        int chuanganqiIndex = choosedFinger3;
                        if (chuanganqiIndex == graphUpdate.SensorIndex)
                        {
                            needRefresh = true;

                            if (!channelData2.ContainsKey(graphUpdate.Channel))
                            {
                                var list = new RollingPointPairList(MaxVisiblePackets + 100);
                                var curve = pane3.AddCurve(
                                    $"CH{graphUpdate.Channel + 1}",
                                    list,
                                    GetColor(graphUpdate.Channel),
                                    SymbolType.None);
                                channelData2[graphUpdate.Channel] = list;
                                channelCurves2[graphUpdate.Channel] = curve;
                            }

                            if (graphUpdate.Index >= xMin)
                                channelData2[graphUpdate.Channel].Add(graphUpdate.Index, graphUpdate.Pressure);


                            label_ax.Text = $"Ax: {graphUpdate.GyroValues[0]:F2}";
                            label_ay.Text = $"Ay: {graphUpdate.GyroValues[1]:F2}";
                            label_az.Text = $"Az: {graphUpdate.GyroValues[2]:F2}";
                            label_gx.Text = $"Gx: {graphUpdate.GyroValues[3]:F2}";
                            label_gy.Text = $"Gy: {graphUpdate.GyroValues[4]:F2}";
                            label_gz.Text = $"Gz: {graphUpdate.GyroValues[5]:F2}";
                        }
                    }
                }

                // 每 GraphRefreshInterval tick 批量刷新滚动图
                if (tickCount % GraphRefreshInterval == 0 && needRefresh)
                {
                    zedGraphControl3.AxisChange();
                    zedGraphControl3.Invalidate();
                }

                // === 更新点图和云图 Panels，每 PanelRefreshInterval tick 批量刷新 ===
                if (tickCount % PanelRefreshInterval == 0)
                {
                    //DotMatrixUpdate dotUpdate;
                    while (dotQueue_Pres.TryDequeue(out var dequeuedUpdate))
                    {
                        var dotUpdate = dequeuedUpdate; // 建立副本，避免闭包问题
                        if (dotUpdate == null)
                            continue; // 跳过 null 元素

                        int sensorCount = 5;
                        int pressureCount = 27;
                        int groupSize = 3; // 每 3 个通道归为一组，9 个点

                        // 预分配数组避免重复分配
                        //Array.Clear(panelValuesBuffer, 0, panelValuesBuffer.Length);
                        //Array.Clear(cloudValuesBuffer, 0, cloudValuesBuffer.Length);


                        for (int s = 0; s < sensorCount; s++)
                        {
                            // --- 压力点值 ---
                            Array.Copy(dotUpdate.PressureValues, s * pressureCount, panelValuesBuffer, s * pressureCount, pressureCount);

                            // --- 云图 9 点值 ---
                            for (int g = 0; g < 9; g++)
                            {
                                double sum = 0;
                                for (int k = 0; k < groupSize; k++)
                                {
                                    int idx = s * pressureCount + g * groupSize + k;
                                    sum += dotUpdate.PressureValues[idx];
                                }
                                cloudValuesBuffer[s * 9 + g] = sum / groupSize;
                            }
                        }

                        // 更新 UI 线程

                        for (int s = 0; s < sensorCount; s++)
                        {
                            // --- 压力点图 ---
                            var panelPoint = this.Controls.Find($"panel_finger{s + 1}_point27", true).FirstOrDefault() as DoubleBufferedPanel27;
                            if (panelPoint != null)
                            {
                                //double[] values = new double[pressureCount];
                                //Array.Copy(panelValuesBuffer, s * pressureCount, values, 0, pressureCount);
                                //panelPoint.Values = values;
                                Array.Copy(panelValuesBuffer, s * pressureCount, panelPoint.Values, 0, pressureCount);
                                panelPoint.Invalidate();
                            }

                            // --- 压力云图 ---
                            var panelCloud = this.Controls.Find($"panel_finger{s + 1}_cloud27", true).FirstOrDefault() as DoubleBufferedPanelCloud27;
                            var labelMax = this.Controls.Find($"label_finger{s + 1}_max27", true).FirstOrDefault() as System.Windows.Forms.Label;
                            var labelMin = this.Controls.Find($"label_finger{s + 1}_min27", true).FirstOrDefault() as System.Windows.Forms.Label;

                            if (panelCloud != null)
                            {
                                /*                                        double[] values = new double[9];
                                                                        Array.Copy(cloudValuesBuffer, s * 9, values, 0, 9);
                                                                        panelCloud.Values = values;*/
                                Array.Copy(cloudValuesBuffer, s * 9, panelCloud.Values, 0, 9);
                                panelCloud.Invalidate();

                                if (panelCloud.Values.Length > 0)
                                {
                                    if (labelMax != null)
                                        labelMax.Text = $"Max: {panelCloud.Values.Max():F1}";
                                    if (labelMin != null)
                                        labelMin.Text = $"Min: {panelCloud.Values.Min():F1}";
                                }
                            }


                            // --- 显示模型推理结果 ---
                            if (choosedFinger3 != -1)
                            {
                                int chuanganqiIndex = choosedFinger3;
                                if (latestResults.TryGetValue(chuanganqiIndex, out var result))
                                {
                                    label_fxy.Text = $"Fxy: {result.Fx:F2}";
                                    label_fyx.Text = $"Fyx: {result.Fy:F2}";
                                    label_fz.Text = $"Fz: {result.Fz:F2}";
                                    label_label.Text = $"Label: {result.Label}";
                                    label_prob.Text = $"概率: {result.MaxProb:F2}";
                                }
                            }

                            // --- 温度标签 ---
                            var labelTemp27 = this.Controls.Find($"label_finger{s + 1}_temp27", true).FirstOrDefault() as System.Windows.Forms.Label;
                            if (labelTemp27 != null && dotUpdate.TempValues.Length >= sensorCount)
                            {
                                labelTemp27.Text = $"{fingerNames[s]} 温度: {dotUpdate.TempValues[s]}";
                            }
                        }

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
        public class ListStringPool
        {
            private readonly ConcurrentBag<List<string>> pool = new ConcurrentBag<List<string>>();

            public List<string> Rent()
            {
                if (pool.TryTake(out var list))
                {
                    list.Clear();
                    return list;
                }
                return new List<string>(50); // 初始容量预分配
            }

            public void Return(List<string> list)
            {
                list.Clear();
                pool.Add(list);
            }
        }

        private static ListStringPool uiDataPool = new ListStringPool();

        /*        private void EnqueuePacket(byte[] packet)
                {
                    if(chuanGanQiType == "MEMS")
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

                                    // 取 4 个字节
                                    byte[] tmp = { packet[pos], packet[pos + 1], packet[pos + 2], packet[pos + 3] };

                                    // 或者用 BitConverter
                                    values[i] = BitConverter.ToInt32(tmp, 0); // 但不要 Array.Reverse


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
                    else if(chuanGanQiType == "Yingbianhua")
                    {
                        try
                        {
                            if (packet.Length != 343)
                            {
                                LogToConsole($"无效包长度: {packet.Length}");
                                return;
                            }

                            // 校验包头和包尾
                            if (!(packet[0] == 0xAA && packet[1] == 0xAA && packet[2] == 0xAA && packet[3] == 0xAA &&
                                  packet[339] == 0xBB && packet[340] == 0xBB && packet[341] == 0xBB && packet[342] == 0xBB))
                            {
                                LogToConsole("包头或包尾错误，丢弃数据包");
                                return;
                            }

                            int dataOffset = 4; // 数据从第5个字节开始
                            int sensorCount = 5;

                            // 压力值：每个传感器 27 个 2 字节 = 54 字节
                            int pressureCount = 27;
                            short[][] pressureValues = new short[sensorCount][];
                            for (int s = 0; s < sensorCount; s++)
                            {
                                pressureValues[s] = new short[pressureCount];
                                for (int i = 0; i < pressureCount; i++)
                                {
                                    int pos = dataOffset + s * (pressureCount * 2 + 1 + 12) + i * 2; // 每个传感器块偏移
                                    byte[] tmp = { packet[pos + 1], packet[pos] }; // 高低字节翻转
                                    pressureValues[s][i] = BitConverter.ToInt16(tmp, 0);
                                }
                            }

                            // 温度值：每个传感器 1 个字节
                            byte[] temperatureValues = new byte[sensorCount];
                            for (int s = 0; s < sensorCount; s++)
                            {
                                int pos = dataOffset + s * (pressureCount * 2 + 1 + 12) + pressureCount * 2;
                                temperatureValues[s] = packet[pos];
                            }

                            // 陀螺仪值：每个传感器 6 个 2 字节 = 12 字节
                            short[][] gyroValues = new short[sensorCount][];
                            for (int s = 0; s < sensorCount; s++)
                            {
                                gyroValues[s] = new short[6];
                                int gyroOffset = dataOffset + s * (pressureCount * 2 + 1 + 12) + pressureCount * 2 + 1;
                                for (int i = 0; i < 6; i++)
                                {
                                    int pos = gyroOffset + i * 2;
                                    byte[] tmp = { packet[pos + 1], packet[pos] }; // 高低字节翻转
                                    gyroValues[s][i] = BitConverter.ToInt16(tmp, 0);
                                }
                            }

                            // 容错（校验成功的包才覆盖）
                            lastValidPacket = packet;

                            // 构造 UI 数据
                            var uiData = new List<string>();
                            for (int s = 0; s < sensorCount; s++)
                            {
                                uiData.Add($"S{s + 1}");
                                for (int i = 0; i < pressureValues[s].Length; i++)
                                    uiData.Add(pressureValues[s][i].ToString());
                                uiData.Add(temperatureValues[s].ToString());
                                for (int i = 0; i < gyroValues[s].Length; i++)
                                    uiData.Add(gyroValues[s][i].ToString());
                            }

                            // 入UI队列（只保留最新）
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

                }*/

        private void EnqueuePacket(byte[] packet)
        {
            if (chuanGanQiType == "MEMS")
            {
                try
                {
                    if (packet.Length < 10) return;

                    int length = packet[2];
                    byte addr = packet[3];
                    byte type = packet[4];

                    // 复用数组
                    Span<double> values = stackalloc double[8];

                    int dataOffset = 13;

                    if (type == 0xF4) // 温度
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            if (dataOffset + i * 2 + 1 >= packet.Length) break;
                            //double v = BinaryPrimitives.ReadInt16BigEndian(packet.AsSpan(dataOffset + i * 2, 2));
                            double v = BinaryPrimitives.ReadInt16LittleEndian(packet.AsSpan(dataOffset + i * 2, 2));
                            values[7 - i] = v / 10;
                        }
                    }
                    else if (type == 0xF5) // 压力
                    {
                        for (int i = 0; i < 8; i++)
                        {
                            if (dataOffset + i * 4 + 3 >= packet.Length) break;
                            double v = BitConverter.ToInt32(packet, dataOffset + i * 4); // 保持小端或按协议
                            if (guiyihua)
                            {
                                v = v / 1000.0; // 压力值归一化，单位 kPa

                                // 保证最小值为 1
                                if (v > 0 && v < 1) v = 1;
                                if (v< 0 && v > -1) v = -1;

                            }

                            values[7 - i] = v; // 保持小端或按协议
                        }
                    }
                    else
                    {
                        LogToConsole($"未知包类型: {type:X2}");
                        return;
                    }

                    lastValidPacket = packet;

                    // 获取 List<string> 对象池
                    var uiData = uiDataPool.Rent();
                    uiData.Clear();
                    uiData.Add("S" + addr.ToString());
                    uiData.Add(type.ToString("X2"));
                    for (int i = 0; i < 8; i++)
                        uiData.Add(values[i].ToString());

                    while (uiQueue.Count > 0) uiQueue.TryTake(out _);
                    uiQueue.Add(uiData);

                    var now = HighResDateTime.Now;
                    if ((now - lastSaveTime).TotalMilliseconds >= saveRate)
                    {
                        lastSaveTime = now;
                        if (fileRawQueue.Count >= 20000) fileRawQueue.TryTake(out _);
                        fileRawQueue.Add(uiData);
                    }

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
            else if (chuanGanQiType == "Yingbianhua")
            {
                try
                {
                    if (packet.Length != 343)
                    {
                        LogToConsole($"无效包长度: {packet.Length}");
                        return;
                    }

                    if (!(packet[0] == 0xAA && packet[1] == 0xAA && packet[2] == 0xAA && packet[3] == 0xAA &&
                          packet[339] == 0xBB && packet[340] == 0xBB && packet[341] == 0xBB && packet[342] == 0xBB))
                    {
                        LogToConsole("包头或包尾错误，丢弃数据包");
                        return;
                    }

                    lastValidPacket = packet;

                    int dataOffset = 4;
                    int sensorCount = 5;
                    int pressureCount = 27;
                    float[] pressureValues = new float[sensorCount * pressureCount];

                    // 使用 stackalloc + Span 避免 new
                    Span<short> pressureBuffer = stackalloc short[pressureCount];
                    Span<short> gyroBuffer = stackalloc short[6];

                    // 获取 List<string> 对象池
                    var uiData = uiDataPool.Rent();
                    uiData.Clear();

                    for (int s = 0; s < sensorCount; s++)
                    {
                        uiData.Add($"S{s + 1}");

                        // 压力值
                        int sensorOffset = s * (pressureCount * 2 + 1 + 12);
                        for (int i = 0; i < pressureCount; i++)
                        {
                            int pos = dataOffset + sensorOffset + i * 2;
                            pressureBuffer[i] = BinaryPrimitives.ReadInt16BigEndian(packet.AsSpan(pos, 2));
                            uiData.Add(pressureBuffer[i].ToString());
                            pressureValues[s * pressureCount + i] = pressureBuffer[i];
                        }

                        // 温度值
                        byte temp = packet[dataOffset + sensorOffset + pressureCount * 2];
                        uiData.Add(temp.ToString());

                        // 陀螺仪
                        int gyroOffset = dataOffset + sensorOffset + pressureCount * 2 + 1;
                        for (int i = 0; i < 6; i++)
                        {
                            int pos = gyroOffset + i * 2;
                            gyroBuffer[i] = BinaryPrimitives.ReadInt16BigEndian(packet.AsSpan(pos, 2));
                            uiData.Add(gyroBuffer[i].ToString());
                        }
                    }

                    inferenceQueue.Add(pressureValues);

                    while (uiQueue.Count > 0) uiQueue.TryTake(out _);
                    uiQueue.Add(uiData);

                    var now = HighResDateTime.Now;
                    if ((now - lastSaveTime).TotalMilliseconds >= saveRate)
                    {
                        lastSaveTime = now;
                        if (fileRawQueue.Count >= 20000) fileRawQueue.TryTake(out _);
                        fileRawQueue.Add(uiData);
                    }

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
        }

        private void StartInferenceThread()
        {
            Task.Run(() =>
            {
                // 预分配输出缓冲区
                float[] model1OutputBuffer = new float[15];  // 5 sensors × 3 forces
                float[] model2ProbBuffer = new float[405];   // 5 sensors × 81 probs

                foreach (var input in inferenceQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        // --- Model1 推理 ---
                        var inputTensor = new DenseTensor<float>(input, new int[] { 5, 27 });
                        var inputs1 = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(sessionModel1.InputMetadata.Keys.First(), inputTensor)
                };

                        using var results1 = sessionModel1.Run(inputs1);
                        var tensor1 = results1.First(x => x.Name == "y").AsTensor<float>();

                        // tensor1.Dimensions 可能是 [5,3]
                        int rows = tensor1.Dimensions[0]; // 5
                        int cols = tensor1.Dimensions[1]; // 3

                        // 将二维数据拷贝到一维缓冲区
                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                model1OutputBuffer[r * cols + c] = tensor1[r, c];
                            }
                        }

                        // --- Model2 推理 ---
                        var inputs2 = new List<NamedOnnxValue>
                {
                    NamedOnnxValue.CreateFromTensor(sessionModel2.InputMetadata.Keys.First(), inputTensor)
                };

                        using var results2 = sessionModel2.Run(inputs2);
                        var labelTensor = results2.First(x => x.Name == "label").AsTensor<long>();
                        var probTensor = results2.First(x => x.Name == "probabilities").AsTensor<float>();

                        // 零分配拷贝 405 个概率
                        int rows2 = probTensor.Dimensions[0];
                        int cols2 = probTensor.Dimensions[1];

                        // 将二维数据拷贝到一维缓冲区
                        for (int r = 0; r < rows; r++)
                        {
                            for (int c = 0; c < cols; c++)
                            {
                                model2ProbBuffer[r * cols + c] = probTensor[r, c];
                            }
                        }

                        // 按 5 sensors × 81 probs 找每个传感器最大概率
                        for (int s = 0; s < 5; s++)
                        {
                            int baseProbIdx = s * 81;
                            float maxProb = float.MinValue;
                            int maxIdx = 0;

                            for (int i = 0; i < 81; i++)
                            {
                                float prob = model2ProbBuffer[baseProbIdx + i];
                                if (prob > maxProb)
                                {
                                    maxProb = prob;
                                    maxIdx = i;
                                }
                            }

                            var result = new SensorInferenceResult
                            {
                                Fx = model1OutputBuffer[s * 3 + 0],
                                Fy = model1OutputBuffer[s * 3 + 1],
                                Fz = model1OutputBuffer[s * 3 + 2],
                                Label = labelTensor[s],
                                MaxProbIndex = maxIdx,
                                MaxProb = maxProb
                            };

                            latestResults.AddOrUpdate(s, result, (_, __) => result);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToConsole("模型推理异常：" + ex.ToString());
                    }
                }
            });
        }







        private void FormatWorkerLoop()
        {
            try
            {
                foreach (var packet in fileRawQueue.GetConsumingEnumerable())
                {
                    string line = "";
                    if (chuanGanQiType == "MEMS")
                    {
                        line = FormatPacketToOneCsvLineFast(packet);
                    }
                    else
                    {
                        line = FormatPacketToOneCsvLineFast27(packet);
                    }

                    if (line == null) continue;

                    // fileQueue 有界 + 丢最旧，确保不堆积
                    if (fileQueue.Count >= 20000) fileQueue.TryTake(out _);
                    fileQueue.Add(line);
                }
            }
            catch (Exception ex)
            {
                LogToConsole($"[ERR] FormatWorker: {ex.ToString()}");
            }
        }
        /*        private string FormatPacketToOneCsvLineFast(List<string> packet)
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
                }*/
        [ThreadStatic] private static StringBuilder _sbCache;
        private string FormatPacketToOneCsvLineFast(List<string> packet)
        {
            if (packet == null) return null;

            // 初始化缓存（只在第一次调用时分配）
            if (_sbCache == null) _sbCache = new StringBuilder(4096);

            // 清空缓存
            _sbCache.Clear();

            // 先写时间戳
            _sbCache.Append(HighResDateTime.Now.ToString("yy:MM:dd:HH:mm:ss.fff"));

            if (packet[1] == "F4")
            {
                for (int i = 0; i < 8; i++)
                {
                    _sbCache.Append(',');
                    _sbCache.Append(packet[i]);
                }
            }
            else if (packet[1] == "F5")
            {
                for (int i = 0; i < 10; i++)
                {
                    _sbCache.Append(',');
                    _sbCache.Append(packet[i]);
                }
            }

            return _sbCache.ToString();
        }
        private string FormatPacketToOneCsvLineFast27(List<string> packet)
        {
            if (packet == null || packet.Count < 175) return null;

            // 初始化缓存（只在第一次调用时分配）
            if (_sbCache == null) _sbCache = new StringBuilder(4096);

            // 清空缓存
            _sbCache.Clear();

            // 先写时间戳
            _sbCache.Append(HighResDateTime.Now.ToString("yy:MM:dd:HH:mm:ss.fff"));

            // 拼接 175 个值
            for (int i = 0; i < 175; i++)
            {
                _sbCache.Append(',');
                _sbCache.Append(packet[i]);
            }

            return _sbCache.ToString();
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
        /*        private double DenoiseByMedian(int channelIndex, double newValue)
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
                }*/
        private double DenoiseByMedian(int channelIndex, double newValue)
        {
            if (!channelBuffers.ContainsKey(channelIndex))
                channelBuffers[channelIndex] = new Queue<double>();

            var buffer = channelBuffers[channelIndex];

            // 添加新值
            buffer.Enqueue(newValue);
            if (buffer.Count > 4)
                buffer.Dequeue();

            // 数据量不足直接返回
            if (buffer.Count < 3)
                return newValue;

            // 转数组排序
            double[] arr = buffer.ToArray();
            double[] sorted = arr.OrderBy(v => v).ToArray();
            double median = sorted[sorted.Length / 2];

            // 计算中位绝对偏差 (MAD)
            double mad = sorted.Select(v => Math.Abs(v - median)).OrderBy(d => d).ElementAt(sorted.Length / 2);
            double threshold = Math.Max(20, 5 * mad); // 动态阈值, 保证极小MAD也有最小阈值

            // 如果新值偏离中位数过大，视为异常，用中位数替代
            if (Math.Abs(newValue - median) > threshold)
                newValue = median;

            return newValue;
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
            //GraphPane pane2 = zedGraphControl2.GraphPane;
            //SetPaneFont(pane2);
            //pane2.Title.Text = "全通道压力总览";
            //pane2.XAxis.Title.Text = "数据包编号";
            //pane2.YAxis.Title.Text = "压力";
            //pane2.YAxis.Scale.MinAuto = true;
            //pane2.YAxis.Scale.MaxAuto = true;
            ////强制 X 轴显示为整数
            //pane2.XAxis.Type = AxisType.Linear;
            //pane2.XAxis.Scale.MajorStep = 1;
            //pane2.XAxis.Scale.Format = "0";  // 只显示整数，无小数点
            //zedGraphControl2.AxisChange(); // 应用更改

            GraphPane pane3 = zedGraphControl3.GraphPane;
            pane3.Title.Text = "27通道压力总览";
            pane3.XAxis.Title.Text = "数据包编号";
            pane3.YAxis.Title.Text = "压力";
            pane3.YAxis.Scale.MinAuto = true;
            pane3.YAxis.Scale.MaxAuto = true;
            //强制 X 轴显示为整数
            pane3.XAxis.Type = AxisType.Linear;
            pane3.XAxis.Scale.MajorStep = 1;
            pane3.XAxis.Scale.Format = "0";  // 只显示整数，无小数点
            zedGraphControl3.AxisChange(); // 应用更改

            GraphPane pane1 = zedGraphControl1.GraphPane;
            pane1.Title.Text = "8通道压力总览";
            pane1.XAxis.Title.Text = "数据包编号";
            pane1.YAxis.Title.Text = "压力";
            pane1.YAxis.Scale.MinAuto = true;
            pane1.YAxis.Scale.MaxAuto = true;
            //强制 X 轴显示为整数
            pane1.XAxis.Type = AxisType.Linear;
            pane1.XAxis.Scale.MajorStep = 1;
            pane1.XAxis.Scale.Format = "0";  // 只显示整数，无小数点
            zedGraphControl1.AxisChange(); // 应用更改

            GraphPane pane19 = zedGraphControl19.GraphPane;
            pane19.Title.Text = "8通道温度总览";
            pane19.XAxis.Title.Text = "数据包编号";
            pane19.YAxis.Title.Text = "温度";
            pane19.YAxis.Scale.MinAuto = true;
            pane19.YAxis.Scale.MaxAuto = true;
            //强制 X 轴显示为整数
            pane19.XAxis.Type = AxisType.Linear;
            pane19.XAxis.Scale.MajorStep = 1;
            pane19.XAxis.Scale.Format = "0";  // 只显示整数，无小数点
            zedGraphControl19.AxisChange(); // 应用更改
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
                if (comboBox5.SelectedIndex == 0)
                {
                    portName = "COMPort_left";
                }
                else
                {
                    portName = "COMPort_right";
                }
                // 构建 SerialConfig 对象
                try
                {
                    string comPort = data.ContainsKey(portName) ? data[portName].ToString() : throw new Exception("缺少 COMPort 配置");
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
                    //serialPort.Encoding = System.Text.Encoding.ASCII;
                    /*                    serialPort.DataReceived -= SerialPort_DataReceived; // 先移除旧的绑定
                                        serialPort.DataReceived += SerialPort_DataReceived;
                                        serialPort.Open();*/
                    OpenSerialPort();
                    if (serialPort.IsOpen)
                    {
                        state_label.Text = "已连接";
                    }

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

            channelData_temp.Clear();
            channelCurves_temp.Clear();

            packetIndex = 0;

            if (chuanGanQiType == "MEMS")
            {
                var pane = zedGraphControl1.GraphPane;
                pane.CurveList.Clear();

                for (int ch = 0; ch < 8; ch++)
                {
                    var list = new RollingPointPairList(MaxVisiblePackets + 100);
                    var curve = pane.AddCurve($"CH{ch + 1}", list, GetColor(ch), SymbolType.None);
                    channelData2[ch] = list;
                    channelCurves2[ch] = curve;
                }

                // 重绘主图
                zedGraphControl1.AxisChange();
                zedGraphControl1.Invalidate();

                var pane19 = zedGraphControl19.GraphPane;
                pane19.CurveList.Clear();

                for (int ch = 0; ch < 8; ch++)
                {
                    var list = new RollingPointPairList(MaxVisiblePackets + 100);
                    var curve = pane19.AddCurve($"CH{ch + 1}", list, GetColor(ch), SymbolType.None);
                    channelData_temp[ch] = list;
                    channelCurves_temp[ch] = curve;
                }

                // 重绘主图
                zedGraphControl19.AxisChange();
                zedGraphControl19.Invalidate();
            }
            else if (chuanGanQiType == "Yingbianhua")
            {
                var pane3 = zedGraphControl3.GraphPane;
                pane3.CurveList.Clear();


                for (int ch = 0; ch < 27; ch++)
                {
                    var list = new RollingPointPairList(MaxVisiblePackets + 100);
                    var curve = pane3.AddCurve($"CH{ch + 1}", list, GetColor(ch), SymbolType.None);
                    channelData2[ch] = list;
                    channelCurves2[ch] = curve;
                }

                zedGraphControl3.AxisChange();
                zedGraphControl3.Invalidate();
            }

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
            fileWriterThread.Join();

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


        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;
            data[comboBox5.Name] = comboBox5.SelectedIndex;

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

            if (data.TryGetValue("comboBox1", out object value4))
            {
                comboBox1.SelectedIndex = int.Parse(value4.ToString());
                updateSaveRate(); // 更新保存频率
            }

            if (data.TryGetValue("comboBox2", out object value5))
            {
                comboBox2.SelectedIndex = int.Parse(value5.ToString());
                if (comboBox2.SelectedIndex == 0)
                    chuanGanQiType = "MEMS";
                else
                    chuanGanQiType = "Yingbianhua";
            }

            if (data.TryGetValue("comboBox5", out object value6))
            {
                comboBox5.SelectedIndex = int.Parse(value6.ToString());
                if (comboBox5.SelectedIndex == 0)
                    portName = "COMPort_left";
                else
                    portName = "COMPort_right";
            }
        }

        private void LoadFromSettingJson()
        {
            string filePath = "Setting.json";
            if (!File.Exists(filePath))
                return;

            string json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (data == null)
                return;

            if (data.TryGetValue("textBox1", out object value1))
            {
                excelSavePath = value1.ToString();
            }
            if (data.TryGetValue("textBox2", out object value2))
            {
                model1Path = value2.ToString();
            }
            if (data.TryGetValue("textBox3", out object value3))
            {
                model2Path = value3.ToString();
            }

        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;
            data[comboBox5.Name] = comboBox5.SelectedIndex;

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
                case "50Hz":
                    saveRate = 50;
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
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;
            data[comboBox5.Name] = comboBox5.SelectedIndex;

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
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;
            data[comboBox5.Name] = comboBox5.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("ExcelPath.json", json);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            choosedFinger1 = comboBox3.SelectedIndex;

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
            var pane = zedGraphControl1.GraphPane;
            pane.CurveList.Clear();

            for (int ch = 0; ch < 8; ch++)
            {
                var list = new RollingPointPairList(MaxVisiblePackets + 100);
                var curve = pane.AddCurve($"CH{ch + 1}", list, GetColor(ch), SymbolType.None);
                channelData2[ch] = list;
                channelCurves2[ch] = curve;
            }

            packetIndex = 0;

            // 重绘主图
            zedGraphControl1.AxisChange();
            zedGraphControl1.Invalidate();
        }

        private void button8_Click(object sender, EventArgs e)
        {
            // 1. 加载 ONNX 模型
            using var session = new InferenceSession("C:\\Users\\Administrator\\Desktop\\fingerApp\\pymode\\model1.onnx");

            // 2. 准备输入数据：25 个 float
            float[] inputData = new float[54]
            {
            0.1f, 0.2f, 0.3f, 0.4f, 0.5f,
            0.6f, 0.7f, 0.8f, 0.9f, 1.0f,
            1.1f, 1.2f, 1.3f, 1.4f, 1.5f,
            1.6f, 1.7f, 1.8f, 1.9f, 2.0f,
            2.1f, 2.2f, 2.3f, 2.4f, 2.5f, 2.4f, 2.5f,

            0.1f, 0.2f, 0.3f, 0.4f, 0.5f,
            0.6f, 0.7f, 0.8f, 0.9f, 1.0f,
            1.1f, 1.2f, 1.3f, 1.4f, 1.5f,
            1.6f, 1.7f, 1.8f, 1.9f, 1.0f,
            2.1f, 2.2f, 2.3f, 2.4f, 1.5f, 1.4f, 1.5f
            };

            // 3. 构建 Tensor（形状 [1, 25]，batch=1）
            var inputTensor = new DenseTensor<float>(inputData, new int[] { 2, 27 });

            // 获取模型输入名（假设只有一个输入）
            string inputName = session.InputMetadata.Keys.First();

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(inputName, inputTensor)
            };

            // 4. 运行推理
            using var results = session.Run(inputs);

            /*            // 获取模型输出名（假设只有一个输出）
                        string outputName = session.OutputMetadata.Keys.First();

                        // 5. 取结果：形状 [1, 3]
                        var outputTensor = results.First(x => x.Name == outputName).AsTensor<float>();
                        float[] outputData = outputTensor.ToArray();*/
            var outputTensor = results.First(x => x.Name == "y").AsTensor<float>();
            float[] outputData = outputTensor.ToArray();

            LogToConsole("模型输出：");
            foreach (var v in outputData)
                LogToConsole(v.ToString());
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBox2.SelectedIndex == 0)
            {
                chuanGanQiType = "MEMS";
            }
            else
            {
                chuanGanQiType = "Yingbianhua";
                InitModel();
            }

            UpdateTabPages();

            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;
            data[comboBox5.Name] = comboBox5.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("ExcelPath.json", json);
        }

        private void button10_Click(object sender, EventArgs e)
        {
            using var session = new InferenceSession("C:\\Users\\Administrator\\Desktop\\fingerApp\\pymode\\model2_new.onnx");

            // 假设只推理一条数据：27 个 float
            float[] inputData = new float[27]
            {
                    0.1f, 0.2f, 0.3f, 0.4f, 0.5f,
                    0.6f, 0.7f, 0.8f, 0.9f, 1.0f,
                    1.1f, 1.2f, 1.3f, 1.4f, 1.5f,
                    1.6f, 1.7f, 1.8f, 1.9f, 2.0f,
                    2.1f, 2.2f, 2.3f, 2.4f, 2.5f, 2.6f, 2.7f
            };

            var inputTensor = new DenseTensor<float>(inputData, new int[] { 1, 27 });
            var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), inputTensor)
                    };

            using var results = session.Run(inputs);

            // label 输出为 Int64
            var labelTensor = results.First(x => x.Name == "label").AsTensor<long>();
            long[] labels = labelTensor.ToArray();

            // probabilities 输出为 float
            var probTensor = results.First(x => x.Name == "probabilities").AsTensor<float>();
            float[] probs = probTensor.ToArray();

            LogToConsole("预测标签 (label): " + labels[0].ToString());
            LogToConsole("最大概率类别索引: " + Array.IndexOf(probs, probs.Max()) + ", 概率=" + probs.Max());
        }
        //    private void button10_Click(object sender, EventArgs e)
        //    {
        //        using var session = new InferenceSession(@"C:\Users\Administrator\Desktop\fingerApp\pymode\model2_new.onnx");

        //        // batch=1，每条样本 27 个特征，总共 54 个 float
        //        float[] inputData = new float[27]
        //        {
        //            0.1f, 0.2f, 0.3f, 0.4f, 0.5f,
        //            0.6f, 0.7f, 0.8f, 0.9f, 1.0f,
        //            1.1f, 1.2f, 1.3f, 1.4f, 1.5f,
        //            1.6f, 1.7f, 1.8f, 1.9f, 2.0f,
        //            2.1f, 2.2f, 2.3f, 2.4f, 2.5f, 2.6f, 2.7f
        //        };

        //        int batch = 1;
        //        int numFeatures = 27;
        //        int numClasses = 81;

        //        var inputTensor = new DenseTensor<float>(inputData, new int[] { batch, numFeatures });

        //        var inputs = new List<NamedOnnxValue>
        //{
        //    NamedOnnxValue.CreateFromTensor(session.InputMetadata.Keys.First(), inputTensor)
        //};

        //        using var results = session.Run(inputs);

        //        // label 输出为 Int64，每条样本一个值
        //        var labelTensor = results.First(x => x.Name == "label").AsTensor<long>();
        //        long[] labels = labelTensor.ToArray(); // 长度 = batch

        //        // probabilities 输出为 float，每条样本 numClasses 个概率
        //        var probTensor = results.First(x => x.Name == "probabilities").AsTensor<float>();
        //        float[] flatProbs = probTensor.ToArray();
        //        float[,] probs = new float[batch, numClasses];
        //        for (int b = 0; b < batch; b++)
        //            for (int i = 0; i < numClasses; i++)
        //                probs[b, i] = flatProbs[b * numClasses + i];

        //        // 输出每条样本的预测结果
        //        for (int b = 0; b < batch; b++)
        //        {
        //            float[] rowProbs = GetRow(probs, b);
        //            int predictedClass = Array.IndexOf(rowProbs, rowProbs.Max());
        //            LogToConsole($"样本 {b + 1}: label={labels[b]}, 预测类别索引={predictedClass}, 最大概率={rowProbs.Max():F4}");
        //        }
        //    }

        //    // 辅助函数：获取二维数组某行
        //    private float[] GetRow(float[,] array, int row)
        //    {
        //        int cols = array.GetLength(1);
        //        float[] result = new float[cols];
        //        for (int i = 0; i < cols; i++)
        //            result[i] = array[row, i];
        //        return result;
        //    }

        private void comboBox5_SelectedIndexChanged(object sender, EventArgs e)
        {
            var data = new Dictionary<string, object>();

            data[textBox1.Name] = textBox1.Text;
            data[textBox2.Name] = textBox2.Text;
            data[comboBox1.Name] = comboBox1.SelectedIndex;
            data[comboBox2.Name] = comboBox2.SelectedIndex;
            data[comboBox5.Name] = comboBox5.SelectedIndex;

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("ExcelPath.json", json);
        }

        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            Form form = new Setting();
            form.ShowDialog();
        }

        private void button9_Click(object sender, EventArgs e)
        {
            choosedFinger3 = comboBox4.SelectedIndex;

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
            var pane3 = zedGraphControl3.GraphPane;
            pane3.CurveList.Clear();

            for (int ch = 0; ch < 27; ch++)
            {
                var list = new RollingPointPairList(MaxVisiblePackets + 100);
                var curve = pane3.AddCurve($"CH{ch + 1}", list, GetColor(ch), SymbolType.None);
                channelData2[ch] = list;
                channelCurves2[ch] = curve;
            }

            packetIndex = 0;

            // 重绘主图
            zedGraphControl3.AxisChange();
            zedGraphControl3.Invalidate();
        }

        private void button11_Click(object sender, EventArgs e)
        {
            choosedFinger19 = comboBox6.SelectedIndex;

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
            channelData_temp.Clear();
            channelCurves_temp.Clear();
            var pane = zedGraphControl19.GraphPane;
            pane.CurveList.Clear();

            for (int ch = 0; ch < 8; ch++)
            {
                var list = new RollingPointPairList(MaxVisiblePackets + 100);
                var curve = pane.AddCurve($"CH{ch + 1}", list, GetColor(ch), SymbolType.None);
                channelData_temp[ch] = list;
                channelCurves_temp[ch] = curve;
            }

            packetIndex = 0;

            // 重绘主图
            zedGraphControl19.AxisChange();
            zedGraphControl19.Invalidate();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox1.Checked)
            {
                wendutu = true;
            }
            else
            {
                wendutu = false;
            }
        }

        private void checkBox4_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBox4.Checked)
            {
                guiyihua = true;
                for (int ch = 0; ch < 40; ch++)
                {
                    channelZeroOffsets[ch] = 0;
                }
            }
            else
            {
                guiyihua = false;
                for (int ch = 0; ch < 40; ch++)
                {
                    channelZeroOffsets[ch] = 0;
                }
            }
        }
    }
}
