using System.IO.Ports;

namespace fingerPressure
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.ComboBox comboBoxPort;
        private System.Windows.Forms.Button buttonOpen;
        private ZedGraph.ZedGraphControl zedGraphControl1;

        /// <summary>
        /// 清理所有正在使用的资源。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
                serialPort.Dispose(); // 防止串口未释放
            }
            base.Dispose(disposing);
        }

        #region Windows 窗体设计器生成的代码

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            comboBoxPort = new ComboBox();
            buttonOpen = new Button();
            zedGraphControl1 = new ZedGraph.ZedGraphControl();
            SuspendLayout();
            // 
            // comboBoxPort
            // 
            comboBoxPort.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBoxPort.FormattingEnabled = true;
            comboBoxPort.Location = new Point(12, 12);
            comboBoxPort.Name = "comboBoxPort";
            comboBoxPort.Size = new Size(121, 25);
            comboBoxPort.TabIndex = 0;
            // 
            // buttonOpen
            // 
            buttonOpen.Location = new Point(150, 10);
            buttonOpen.Name = "buttonOpen";
            buttonOpen.Size = new Size(100, 25);
            buttonOpen.TabIndex = 1;
            buttonOpen.Text = "打开串口";
            buttonOpen.UseVisualStyleBackColor = true;
            buttonOpen.Click += buttonOpen_Click;
            // 
            // zedGraphControl1
            // 
            zedGraphControl1.Location = new Point(12, 50);
            zedGraphControl1.Margin = new Padding(4, 4, 4, 4);
            zedGraphControl1.Name = "zedGraphControl1";
            zedGraphControl1.ScrollGrace = 0D;
            zedGraphControl1.ScrollMaxX = 0D;
            zedGraphControl1.ScrollMaxY = 0D;
            zedGraphControl1.ScrollMaxY2 = 0D;
            zedGraphControl1.ScrollMinX = 0D;
            zedGraphControl1.ScrollMinY = 0D;
            zedGraphControl1.ScrollMinY2 = 0D;
            zedGraphControl1.Size = new Size(760, 400);
            zedGraphControl1.TabIndex = 2;
            zedGraphControl1.UseExtendedPrintDialog = true;
            // 
            // Form1
            // 
            ClientSize = new Size(789, 463);
            Controls.Add(zedGraphControl1);
            Controls.Add(buttonOpen);
            Controls.Add(comboBoxPort);
            Name = "Form1";
            Text = "串口示波器";
            ResumeLayout(false);
        }

        #endregion
    }
}
