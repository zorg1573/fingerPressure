using System.IO.Ports;

namespace fingerPressure.MODEL
{
    public class SerialConfig
    {
        public string COMPort { get; set; } //串口号
        public int BaudRate { get; set; } //波特率
        public int DataBits { get; set; } //数据位
        public StopBits StopBits { get; set; } //停止位
        public Parity Parity { get; set; } //校验位
        public Handshake Handshake { get; set; } //流控方式
    }
}
