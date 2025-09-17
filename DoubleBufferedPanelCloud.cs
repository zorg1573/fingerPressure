using System;
using System.Drawing;
using System.Windows.Forms;

namespace fingerPressure
{
    public class DoubleBufferedPanelCloud : Panel
    {
        private double[] values = new double[8]; // 8通道数据

        public DoubleBufferedPanelCloud()
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
            DrawCloud(e.Graphics);
        }
        private double Clamp(double value)
        {
            return Math.Max(0, Math.Min(100000, value));
        }
        #region 云图模式（半椭圆内）
        private void DrawCloud(Graphics g)
        {
            int gridSize = 6;
            double[,] input = new double[gridSize, gridSize];

            // 填充8通道值到6x6矩阵（重复映射，保证不会越界）
            double Clamp(double v) => Math.Max(0, Math.Min(100000, v));

            input[0, 0] = Clamp(values[0]); input[1, 0] = Clamp(values[0]);
            input[0, 1] = Clamp(values[0]); input[1, 1] = Clamp(values[0]);

            input[2, 0] = Clamp(values[1]); input[3, 0] = Clamp(values[1]);
            input[2, 1] = Clamp(values[1]); input[3, 1] = Clamp(values[1]);

            input[4, 0] = Clamp(values[2]); input[5, 0] = Clamp(values[2]);
            input[4, 1] = Clamp(values[2]); input[5, 1] = Clamp(values[2]);

            input[0, 2] = Clamp(values[3]); input[1, 2] = Clamp(values[3]);
            input[0, 3] = Clamp(values[3]); input[1, 3] = Clamp(values[3]);

            input[2, 2] = Clamp(values[4]); input[3, 2] = Clamp(values[4]);
            input[2, 3] = Clamp(values[4]); input[3, 3] = Clamp(values[4]);

            input[4, 2] = Clamp(values[5]); input[5, 2] = Clamp(values[5]);
            input[4, 3] = Clamp(values[5]); input[5, 3] = Clamp(values[5]);

            input[0, 4] = Clamp(values[6]); input[1, 4] = Clamp(values[6]);
            input[0, 5] = Clamp(values[6]); input[1, 5] = Clamp(values[6]);
            input[2, 4] = Clamp(values[6]); input[2, 5] = Clamp(values[6]);

            input[3, 4] = Clamp(values[7]); input[4, 4] = Clamp(values[7]);
            input[5, 4] = Clamp(values[7]); input[3, 5] = Clamp(values[7]);
            input[4, 5] = Clamp(values[7]); input[5, 5] = Clamp(values[7]);

            // 双线性插值到 60x60
            int outW = 60, outH = 60;
            double[,] output = new double[outW, outH];

            for (int x = 0; x < outW; x++)
            {
                for (int y = 0; y < outH; y++)
                {
                    double gx = (double)x / (outW - 1) * (gridSize - 1);
                    double gy = (double)y / (outH - 1) * (gridSize - 1);

                    int x0 = (int)Math.Floor(gx);
                    int y0 = (int)Math.Floor(gy);
                    int x1 = Math.Min(x0 + 1, gridSize - 1);
                    int y1 = Math.Min(y0 + 1, gridSize - 1);

                    double dx = gx - x0;
                    double dy = gy - y0;

                    double v00 = input[x0, y0];
                    double v10 = input[x1, y0];
                    double v01 = input[x0, y1];
                    double v11 = input[x1, y1];

                    output[x, y] = (1 - dx) * (1 - dy) * v00 +
                                   dx * (1 - dy) * v10 +
                                   (1 - dx) * dy * v01 +
                                   dx * dy * v11;
                }
            }

            // 半椭圆参数
            Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
            float cx = this.Width / 2f;
            float cy = this.Height; // 半椭圆底部
            float a = this.Width / 2f - 5;
            float b = this.Height - 5;

            float cellW = (float)this.Width / outW;
            float cellH = (float)this.Height / outH;

            for (int x = 0; x < outW; x++)
            {
                for (int y = 0; y < outH; y++)
                {
                    float px = x * cellW;
                    float py = y * cellH;

                    double dx = (px - cx) / a;
                    double dy = (py - cy) / b;

                    if (dx * dx + dy * dy <= 1.0 && py >= cy - b)
                    {
                        Color c = GetColorFromValue(output[x, y]);
                        using (Brush brush = new SolidBrush(c))
                            g.FillRectangle(brush, px, py, cellW + 1, cellH + 1);
                    }
                }
            }

            // 绘制半椭圆边框
            using (Pen pen = new Pen(Color.Black, 1))
            {
                g.DrawArc(pen, ellipseRect, 180, 180);
            }
        }
        #endregion

        private Color GetColorFromValue(double value)
        {
            double min = -20000;
            double max = 20000;

            value = Math.Max(min, Math.Min(max, value));
            if (value >= 0)
            {
                int r = (int)(value / max * 255);
                int g = 0;
                int b = 0;
                return Color.FromArgb(r, g, b);
            }
            else
            {
                int r = 0;
                int g = 0;
                int b = (int)(-value / -min * 255);
                return Color.FromArgb(r, g, b);
            }
        }


    }
}
