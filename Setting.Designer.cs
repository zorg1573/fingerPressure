using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;

namespace fingerPressure
{
    partial class Setting
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            panel1 = new Panel();
            button5 = new Button();
            button4 = new Button();
            button3 = new Button();
            textBox3 = new TextBox();
            textBox2 = new TextBox();
            textBox1 = new TextBox();
            label2 = new Label();
            label7 = new Label();
            button2 = new Button();
            button1 = new Button();
            label1 = new Label();
            flowLayoutPanel1 = new FlowLayoutPanel();
            pictureBox4 = new PictureBox();
            panel1.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox4).BeginInit();
            SuspendLayout();
            // 
            // panel1
            // 
            panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panel1.Controls.Add(button5);
            panel1.Controls.Add(button4);
            panel1.Controls.Add(button3);
            panel1.Controls.Add(textBox3);
            panel1.Controls.Add(textBox2);
            panel1.Controls.Add(textBox1);
            panel1.Controls.Add(label2);
            panel1.Controls.Add(label7);
            panel1.Controls.Add(button2);
            panel1.Controls.Add(button1);
            panel1.Controls.Add(label1);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(20, 60);
            panel1.Name = "panel1";
            panel1.Size = new Size(514, 204);
            panel1.TabIndex = 16;
            // 
            // button5
            // 
            button5.Location = new Point(431, 103);
            button5.Name = "button5";
            button5.Size = new Size(49, 23);
            button5.TabIndex = 48;
            button5.Text = "浏览";
            button5.UseVisualStyleBackColor = true;
            button5.Click += button5_Click;
            // 
            // button4
            // 
            button4.Location = new Point(431, 57);
            button4.Name = "button4";
            button4.Size = new Size(49, 23);
            button4.TabIndex = 47;
            button4.Text = "浏览";
            button4.UseVisualStyleBackColor = true;
            button4.Click += button4_Click;
            // 
            // button3
            // 
            button3.Location = new Point(431, 13);
            button3.Name = "button3";
            button3.Size = new Size(49, 23);
            button3.TabIndex = 46;
            button3.Text = "浏览";
            button3.UseVisualStyleBackColor = true;
            button3.Click += button3_Click;
            // 
            // textBox3
            // 
            textBox3.Font = new Font("宋体", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 134);
            textBox3.Location = new Point(165, 103);
            textBox3.Name = "textBox3";
            textBox3.Size = new Size(260, 23);
            textBox3.TabIndex = 45;
            // 
            // textBox2
            // 
            textBox2.Font = new Font("宋体", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 134);
            textBox2.Location = new Point(165, 57);
            textBox2.Name = "textBox2";
            textBox2.Size = new Size(260, 23);
            textBox2.TabIndex = 44;
            // 
            // textBox1
            // 
            textBox1.Font = new Font("宋体", 10.5F, FontStyle.Regular, GraphicsUnit.Point, 134);
            textBox1.Location = new Point(165, 13);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(260, 23);
            textBox1.TabIndex = 43;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label2.Location = new Point(24, 104);
            label2.Name = "label2";
            label2.Size = new Size(151, 16);
            label2.TabIndex = 42;
            label2.Text = "概率模型文件路径：";
            // 
            // label7
            // 
            label7.AutoSize = true;
            label7.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label7.Location = new Point(24, 58);
            label7.Name = "label7";
            label7.Size = new Size(135, 16);
            label7.TabIndex = 41;
            label7.Text = "力模型文件路径：";
            // 
            // button2
            // 
            button2.BackColor = Color.White;
            button2.FlatAppearance.BorderSize = 0;
            button2.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 180);
            button2.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 150, 255);
            button2.Font = new Font("微软雅黑", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            button2.ForeColor = Color.Black;
            button2.Location = new Point(278, 145);
            button2.Name = "button2";
            button2.Size = new Size(85, 44);
            button2.TabIndex = 40;
            button2.Text = "关闭";
            button2.UseVisualStyleBackColor = false;
            button2.Click += button2_Click;
            // 
            // button1
            // 
            button1.BackColor = Color.White;
            button1.FlatAppearance.BorderSize = 0;
            button1.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 180);
            button1.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 150, 255);
            button1.Font = new Font("微软雅黑", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            button1.ForeColor = Color.Black;
            button1.Location = new Point(136, 145);
            button1.Name = "button1";
            button1.Size = new Size(85, 44);
            button1.TabIndex = 39;
            button1.Text = "保存";
            button1.UseVisualStyleBackColor = false;
            button1.Click += button1_Click;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label1.Location = new Point(24, 14);
            label1.Name = "label1";
            label1.Size = new Size(119, 16);
            label1.TabIndex = 28;
            label1.Text = "结果存储路径：";
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            flowLayoutPanel1.Controls.Add(pictureBox4);
            flowLayoutPanel1.FlowDirection = FlowDirection.RightToLeft;
            flowLayoutPanel1.Location = new Point(479, 6);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(74, 33);
            flowLayoutPanel1.TabIndex = 43;
            // 
            // pictureBox4
            // 
            pictureBox4.Image = Properties.Resources.close;
            pictureBox4.Location = new Point(50, 3);
            pictureBox4.Name = "pictureBox4";
            pictureBox4.Size = new Size(21, 17);
            pictureBox4.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox4.TabIndex = 41;
            pictureBox4.TabStop = false;
            pictureBox4.Click += pictureBox4_Click;
            // 
            // Setting
            // 
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(554, 284);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(panel1);
            Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Margin = new Padding(3, 2, 3, 2);
            Name = "Setting";
            Text = "串口设置";
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            flowLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox4).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel panel1;
        private Label label1;
        private Button button2;
        private Button button1;
        private FlowLayoutPanel flowLayoutPanel1;
        private PictureBox pictureBox4;
        private Label label7;
        private TextBox textBox1;
        private Label label2;
        private TextBox textBox3;
        private TextBox textBox2;
        private Button button5;
        private Button button4;
        private Button button3;
    }
}