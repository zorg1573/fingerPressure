using MetroFramework.Forms;
using System.IO.Ports;
using System.Text.Json;

namespace fingerPressure
{
    public partial class ConnectSet : MetroForm
    {
        public ConnectSet()
        {
            InitializeComponent();
            this.Load += ConnectSet_Load;
        }
        private void ConnectSet_Load(object sender, EventArgs e)
        {
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            LoadPorts();
            LoadFromJson();
        }
        private void LoadPorts()
        {
            COMPort_left.Items.AddRange(SerialPort.GetPortNames());
/*            if (COMPort_left.Items.Count > 0)
                COMPort_left.SelectedIndex = 0;*/
            COMPort_right.Items.AddRange(SerialPort.GetPortNames());
/*            if (COMPort_left.Items.Count > 0)
                COMPort_left.SelectedIndex = 0;*/
        }
        private IEnumerable<Control> GetAllControls(Control parent)
        {
            foreach (Control ctrl in parent.Controls)
            {
                yield return ctrl;

                foreach (var child in GetAllControls(ctrl))
                    yield return child;
            }
        }
        private void LoadFromJson()
        {
            string filePath = "SerialConfig.json";
            if (!File.Exists(filePath))
                return;

            string json = File.ReadAllText(filePath);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(json);

            if (data == null)
                return;

            // 设置控件值（用于界面还原）
            foreach (Control ctrl in GetAllControls(this))
            {
                if (data.TryGetValue(ctrl.Name, out object value))
                {
                    if (ctrl is ComboBox comboBox)
                    {
                        string strVal = value.ToString();
                        if (comboBox.Items.Contains(strVal))
                            comboBox.SelectedItem = strVal;
                        else if (comboBox.Items.Count > 0)
                            comboBox.SelectedIndex = 0;
                    }
                }
            }
        }

        private void SaveToJson()
        {
            var data = new Dictionary<string, object>();

            foreach (Control ctrl in GetAllControls(this))
            {
                if (ctrl is ComboBox comboBox)
                {
                    data[comboBox.Name] = comboBox.SelectedItem?.ToString() ?? "";
                }
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("SerialConfig.json", json);
        }
        private void button1_Click(object sender, EventArgs e)
        {
            SaveToJson();
            MessageBox.Show("设置已保存！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void button2_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // pictureBox4: 关闭
        private void pictureBox4_Click(object sender, EventArgs e)
        {
            this.Close(); // 或 Application.Exit();
        }

    }
}
