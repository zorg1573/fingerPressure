namespace fingerPressure
{
    partial class LogHistory
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
            splitContainer1 = new SplitContainer();
            panel1 = new Panel();
            button2 = new Button();
            button1 = new Button();
            label2 = new Label();
            dateTimePicker2 = new DateTimePicker();
            label1 = new Label();
            dateTimePicker1 = new DateTimePicker();
            dataGridViewLogs = new DataGridView();
            flowLayoutPanel1 = new FlowLayoutPanel();
            pictureBox4 = new PictureBox();
            LogTime = new DataGridViewTextBoxColumn();
            MilTime = new DataGridViewTextBoxColumn();
            Message = new DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)splitContainer1).BeginInit();
            splitContainer1.Panel1.SuspendLayout();
            splitContainer1.Panel2.SuspendLayout();
            splitContainer1.SuspendLayout();
            panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewLogs).BeginInit();
            flowLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)pictureBox4).BeginInit();
            SuspendLayout();
            // 
            // splitContainer1
            // 
            splitContainer1.Dock = DockStyle.Fill;
            splitContainer1.Location = new Point(20, 60);
            splitContainer1.Name = "splitContainer1";
            splitContainer1.Orientation = Orientation.Horizontal;
            // 
            // splitContainer1.Panel1
            // 
            splitContainer1.Panel1.Controls.Add(panel1);
            // 
            // splitContainer1.Panel2
            // 
            splitContainer1.Panel2.Controls.Add(dataGridViewLogs);
            splitContainer1.Size = new Size(935, 447);
            splitContainer1.SplitterDistance = 67;
            splitContainer1.TabIndex = 0;
            // 
            // panel1
            // 
            panel1.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            panel1.Controls.Add(button2);
            panel1.Controls.Add(button1);
            panel1.Controls.Add(label2);
            panel1.Controls.Add(dateTimePicker2);
            panel1.Controls.Add(label1);
            panel1.Controls.Add(dateTimePicker1);
            panel1.Dock = DockStyle.Fill;
            panel1.Location = new Point(0, 0);
            panel1.Name = "panel1";
            panel1.Size = new Size(935, 67);
            panel1.TabIndex = 0;
            // 
            // button2
            // 
            button2.BackColor = Color.White;
            button2.FlatAppearance.BorderSize = 0;
            button2.FlatAppearance.MouseDownBackColor = Color.FromArgb(0, 100, 180);
            button2.FlatAppearance.MouseOverBackColor = Color.FromArgb(0, 150, 255);
            button2.Font = new Font("微软雅黑", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            button2.ForeColor = Color.Black;
            button2.Location = new Point(13, 11);
            button2.Name = "button2";
            button2.Size = new Size(98, 44);
            button2.TabIndex = 41;
            button2.Text = "导出Excel";
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
            button1.Location = new Point(820, 11);
            button1.Name = "button1";
            button1.Size = new Size(85, 44);
            button1.TabIndex = 40;
            button1.Text = "搜索";
            button1.UseVisualStyleBackColor = false;
            button1.Click += button1_Click;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label2.Location = new Point(489, 22);
            label2.Name = "label2";
            label2.Size = new Size(90, 21);
            label2.TabIndex = 9;
            label2.Text = "结束时间：";
            // 
            // dateTimePicker2
            // 
            dateTimePicker2.CalendarFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            dateTimePicker2.Location = new Point(585, 21);
            dateTimePicker2.Name = "dateTimePicker2";
            dateTimePicker2.Size = new Size(200, 23);
            dateTimePicker2.TabIndex = 8;
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Regular, GraphicsUnit.Point, 134);
            label1.Location = new Point(163, 22);
            label1.Name = "label1";
            label1.Size = new Size(90, 21);
            label1.TabIndex = 7;
            label1.Text = "起始时间：";
            // 
            // dateTimePicker1
            // 
            dateTimePicker1.CalendarFont = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular, GraphicsUnit.Point, 134);
            dateTimePicker1.Location = new Point(259, 21);
            dateTimePicker1.Name = "dateTimePicker1";
            dateTimePicker1.Size = new Size(200, 23);
            dateTimePicker1.TabIndex = 6;
            // 
            // dataGridViewLogs
            // 
            dataGridViewLogs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            dataGridViewLogs.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewLogs.Columns.AddRange(new DataGridViewColumn[] { LogTime, MilTime, Message });
            dataGridViewLogs.Dock = DockStyle.Fill;
            dataGridViewLogs.Location = new Point(0, 0);
            dataGridViewLogs.Name = "dataGridViewLogs";
            dataGridViewLogs.Size = new Size(935, 376);
            dataGridViewLogs.TabIndex = 0;
            // 
            // flowLayoutPanel1
            // 
            flowLayoutPanel1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            flowLayoutPanel1.Controls.Add(pictureBox4);
            flowLayoutPanel1.FlowDirection = FlowDirection.RightToLeft;
            flowLayoutPanel1.Location = new Point(870, 5);
            flowLayoutPanel1.Name = "flowLayoutPanel1";
            flowLayoutPanel1.Size = new Size(106, 33);
            flowLayoutPanel1.TabIndex = 43;
            // 
            // pictureBox4
            // 
            pictureBox4.Image = Properties.Resources.close;
            pictureBox4.Location = new Point(82, 3);
            pictureBox4.Name = "pictureBox4";
            pictureBox4.Size = new Size(21, 17);
            pictureBox4.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox4.TabIndex = 41;
            pictureBox4.TabStop = false;
            pictureBox4.Click += pictureBox4_Click;
            // 
            // LogTime
            // 
            LogTime.FillWeight = 30F;
            LogTime.HeaderText = "发送时间";
            LogTime.Name = "LogTime";
            // 
            // MilTime
            // 
            MilTime.FillWeight = 30F;
            MilTime.HeaderText = "精确时间";
            MilTime.Name = "MilTime";
            // 
            // Message
            // 
            Message.FillWeight = 98.47716F;
            Message.HeaderText = "内容";
            Message.Name = "Message";
            // 
            // LogHistory
            // 
            AutoScaleDimensions = new SizeF(7F, 17F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(975, 527);
            Controls.Add(flowLayoutPanel1);
            Controls.Add(splitContainer1);
            Name = "LogHistory";
            Text = "历史记录";
            splitContainer1.Panel1.ResumeLayout(false);
            splitContainer1.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)splitContainer1).EndInit();
            splitContainer1.ResumeLayout(false);
            panel1.ResumeLayout(false);
            panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridViewLogs).EndInit();
            flowLayoutPanel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)pictureBox4).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private SplitContainer splitContainer1;
        private DataGridView dataGridViewLogs;
        private Panel panel1;
        private Label label2;
        private DateTimePicker dateTimePicker2;
        private Label label1;
        private DateTimePicker dateTimePicker1;
        private Button button2;
        private Button button1;
        private FlowLayoutPanel flowLayoutPanel1;
        private PictureBox pictureBox4;
        private DataGridViewTextBoxColumn LogTime;
        private DataGridViewTextBoxColumn MilTime;
        private DataGridViewTextBoxColumn Message;
    }
}