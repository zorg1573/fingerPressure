using MetroFramework.Forms;
using System.IO.Ports;
using System.Text.Json;

namespace fingerPressure
{
    public partial class Setting : MetroForm
    {
        public Setting()
        {
            InitializeComponent();
            this.Load += Setting_Load;
        }
        private void Setting_Load(object sender, EventArgs e)
        {
            LoadFromJson();
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
            string filePath = "Setting.json";
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
                    if (ctrl is TextBox textBox)
                    {
                        string strVal = value.ToString();
                        textBox.Text = strVal;
                    }
                }
            }
        }

        private void SaveToJson()
        {
            var data = new Dictionary<string, object>();

            foreach (Control ctrl in GetAllControls(this))
            {
                if (ctrl is TextBox textBox)
                {
                    data[textBox.Name] = textBox.Text ?? "";
                }
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("Setting.json", json);
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

        private void button3_Click(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "请选择一个文件夹";
                dialog.ShowNewFolderButton = true;

                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    textBox1.Text = dialog.SelectedPath;
                }
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "请选择算法模型文件";
                dialog.Filter = "ONNX(*.onnx)|*.onnx";
                dialog.Multiselect = false;

                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    textBox2.Text = dialog.FileName;
                }
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "请选择算法模型文件";
                dialog.Filter = "ONNX(*.onnx)|*.onnx";
                dialog.Multiselect = false;

                if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.FileName))
                {
                    textBox3.Text = dialog.FileName;
                }
            }
        }
    }
}
