using System.IO.Ports;
using System.Windows.Forms;
using System.Xml.Linq;

namespace fingerPressure
{
    partial class ConnectSet
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
            button2 = new Button();
            button1 = new Button();
            COMPort = new ComboBox();
            label1 = new Label();
            Handshake = new ComboBox();
            label6 = new Label();
            Parity = new ComboBox();
            label5 = new Label();
            StopBits = new ComboBox();
            label4 = new Label();
            DataBits = new ComboBox();
            label3 = new Label();
            BaudRate = new ComboBox();
            label2 = new Label();
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
            panel1.Controls.Add(button2);
            panel1.Controls.Add(button1);
            panel1.Controls.Add(COMPort);
            panel1.Controls.Add(label1);
            panel1.Controls.Add(Handshake);
            panel1.Controls.Add(label6);
            panel1.Controls.Add(Parity);
            panel1.Controls.Add(label5);
            panel1.Controls.Add(StopBits);
            panel1.Controls.Add(label4);
            panel1.Controls.Add(DataBits);
            panel1.Controls.Add(label3);
            panel1.Controls.Add(BaudRate);
            panel1.Controls.Add(label2);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(20, 60);
            panel1.Name = "panel1";
            panel1.Size = new Size(278, 306);
            panel1.TabIndex = 16;
            // 
            // button2
            // 
            button2.BackColor = Color.White;
            button2.FlatAppearance.BorderSize = 0;
            button2.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 180);
            button2.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 150, 255);
            button2.Font = new Font("微软雅黑", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            button2.ForeColor = Color.Black;
            button2.Location = new Point(166, 243);
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
            button1.Location = new Point(24, 243);
            button1.Name = "button1";
            button1.Size = new Size(85, 44);
            button1.TabIndex = 39;
            button1.Text = "保存";
            button1.UseVisualStyleBackColor = false;
            button1.Click += button1_Click;
            // 
            // COMPort
            // 
            COMPort.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            COMPort.FormattingEnabled = true;
            COMPort.Location = new Point(130, 11);
            COMPort.Name = "COMPort";
            COMPort.Size = new Size(121, 24);
            COMPort.TabIndex = 29;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label1.Location = new Point(24, 14);
            label1.Name = "label1";
            label1.Size = new Size(71, 16);
            label1.TabIndex = 28;
            label1.Text = "串口号：";
            // 
            // Handshake
            // 
            Handshake.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Handshake.FormattingEnabled = true;
            Handshake.Items.AddRange(new object[] { "None", "XOnXOff", "RequestToSend" });
            Handshake.Location = new Point(130, 204);
            Handshake.Name = "Handshake";
            Handshake.Size = new Size(121, 24);
            Handshake.TabIndex = 25;
            // 
            // label6
            // 
            label6.AutoSize = true;
            label6.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label6.Location = new Point(24, 207);
            label6.Name = "label6";
            label6.Size = new Size(71, 16);
            label6.TabIndex = 24;
            label6.Text = "流控制：";
            // 
            // Parity
            // 
            Parity.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Parity.FormattingEnabled = true;
            Parity.Items.AddRange(new object[] { "None", "Odd", "Even", "Mark", "Space" });
            Parity.Location = new Point(130, 166);
            Parity.Name = "Parity";
            Parity.Size = new Size(121, 24);
            Parity.TabIndex = 23;
            // 
            // label5
            // 
            label5.AutoSize = true;
            label5.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label5.Location = new Point(24, 169);
            label5.Name = "label5";
            label5.Size = new Size(71, 16);
            label5.TabIndex = 22;
            label5.Text = "校验位：";
            // 
            // StopBits
            // 
            StopBits.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            StopBits.FormattingEnabled = true;
            StopBits.Items.AddRange(new object[] { "1", "1.5", "2" });
            StopBits.Location = new Point(130, 126);
            StopBits.Name = "StopBits";
            StopBits.Size = new Size(121, 24);
            StopBits.TabIndex = 21;
            // 
            // label4
            // 
            label4.AutoSize = true;
            label4.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label4.Location = new Point(24, 129);
            label4.Name = "label4";
            label4.Size = new Size(71, 16);
            label4.TabIndex = 20;
            label4.Text = "停止位：";
            // 
            // DataBits
            // 
            DataBits.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            DataBits.FormattingEnabled = true;
            DataBits.Items.AddRange(new object[] { "5", "6", "7", "8" });
            DataBits.Location = new Point(130, 87);
            DataBits.Name = "DataBits";
            DataBits.Size = new Size(121, 24);
            DataBits.TabIndex = 19;
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label3.Location = new Point(24, 90);
            label3.Name = "label3";
            label3.Size = new Size(71, 16);
            label3.TabIndex = 18;
            label3.Text = "数据位：";
            // 
            // BaudRate
            // 
            BaudRate.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            BaudRate.FormattingEnabled = true;
            BaudRate.Items.AddRange(new object[] { "9600", "19200", "38400", "57600", "115200", "921600" });
            BaudRate.Location = new Point(130, 49);
            BaudRate.Name = "BaudRate";
            BaudRate.Size = new Size(121, 24);
            BaudRate.TabIndex = 17;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("宋体", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label2.Location = new Point(24, 52);
            label2.Name = "label2";
            label2.Size = new Size(71, 16);
            label2.TabIndex = 16;
            label2.Text = "波特率：";
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            flowLayoutPanel1.Controls.Add(pictureBox4);
            flowLayoutPanel1.FlowDirection = FlowDirection.RightToLeft;
            flowLayoutPanel1.Location = new Point(243, 6);
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
            // ConnectSet
            // 
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(318, 386);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(panel1);
            Font = new Font("宋体", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            Margin = new Padding(3, 2, 3, 2);
            Name = "ConnectSet";
            Text = "串口设置";
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            flowLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox4).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private Panel panel1;
        private ComboBox COMPort;
        private Label label1;
        private ComboBox Handshake;
        private Label label6;
        private ComboBox Parity;
        private Label label5;
        private ComboBox StopBits;
        private Label label4;
        private ComboBox DataBits;
        private Label label3;
        private ComboBox BaudRate;
        private Label label2;
        private Button button2;
        private Button button1;
        private FlowLayoutPanel flowLayoutPanel1;
        private PictureBox pictureBox4;
    }
}