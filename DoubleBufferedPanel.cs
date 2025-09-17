using System;
using System.Drawing;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class DoubleBufferedPanel : Panel
    {
        private double[] values = new double[8]; // 8通道数据

        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }

        /// <summary>
        /// 设置/获取8通道值
        /// </summary>
        public double[] Values
        {
            get => values;
            set
            {
                if (value != null && value.Length == 8)
                    values = value;
                Invalidate(); // 刷新绘制
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不调用 base，避免闪烁
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // ========= 绘制指腹半椭圆背景 =========
            Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
            using (Brush b = new SolidBrush(Color.LightGray))
            {
                g.FillPie(b, ellipseRect, 180, 180);  // 填充半椭圆
            }
            g.DrawArc(Pens.Black, ellipseRect, 180, 180); // 边框

            // ========= 绘制点阵（2-3-3 布局） =========
            int circleDiameter = Math.Min(this.Width, this.Height) / 6; // 点直径
            int marginTop = 20;
            int marginBottom = 10;
            int verticalSpacing = (this.Height - marginTop - marginBottom - 3 * circleDiameter) / 2;

            int[] rowCols = { 2, 3, 3 }; // 每行点数
            int totalRows = rowCols.Length;

            int valueIndex = 0;

            for (int row = 0; row < totalRows; row++)
            {
                int cols = rowCols[row];
                int rowY = marginTop + row * (circleDiameter + verticalSpacing);

                // 当前行总宽度
                int totalWidth = cols * circleDiameter + (cols - 1) * circleDiameter / 2;
                int startX = (this.Width - totalWidth) / 2;

                for (int col = 0; col < cols; col++)
                {
                    if (valueIndex >= values.Length) break;

                    int x = startX + col * (circleDiameter + circleDiameter / 2);
                    int y = rowY;

                    double value = values[valueIndex];

                    Rectangle dotRect = new Rectangle(x, y, circleDiameter, circleDiameter);
                    using (Brush brush = new SolidBrush(Color.LightSkyBlue))
                    {
                        g.FillEllipse(brush, dotRect);
                    }
                    g.DrawEllipse(Pens.Black, dotRect);

                    // 数值居中绘制
                    string text = value.ToString();
                    SizeF textSize = g.MeasureString(text, this.Font);
                    g.DrawString(
                        text,
                        this.Font,
                        Brushes.Black,
                        x + (circleDiameter - textSize.Width) / 2,
                        y + (circleDiameter - textSize.Height) / 2
                    );

                    valueIndex++;
                }
            }
        }
    }
}
