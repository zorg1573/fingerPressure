using MetroFramework.Forms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class MeasureSet : MetroForm
    {
        public MeasureSet()
        {
            InitializeComponent();
            this.Load += MeasureSet_Load;
        }
        private void MeasureSet_Load(object sender, EventArgs e)
        {
            LoadFromJson();
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
        }
        private void LoadFromJson()
        {
            string filePath = "MeasureSet.json";
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
                        textBox.Text = value.ToString();
                    }
                }
            }
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
        private void SaveToJson()
        {
            var data = new Dictionary<string, object>();

            foreach (Control ctrl in GetAllControls(this))
            {
                if (ctrl is TextBox textBox)
                {
                    data[textBox.Name] = textBox.Text;
                }
            }

            string json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText("MeasureSet.json", json);
        }
        private void button1_Click(object sender, EventArgs e)
        {
            SaveToJson();
            MessageBox.Show("设置已保存！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            foreach (Control ctrl in panel1.Controls)
            {
                if (ctrl is TextBox textBox)
                {
                    textBox.Text = "0";
                }
            }
        }

        // pictureBox4: 关闭
        private void pictureBox4_Click(object sender, EventArgs e)
        {
            this.Close(); // 或 Application.Exit();
        }

    }
}
