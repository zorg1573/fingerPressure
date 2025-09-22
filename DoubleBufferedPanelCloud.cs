using System;
using System.Drawing;
using System.Drawing.Drawing2D;
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
            DrawForceArrow(e.Graphics);
        }

        #region 云图模式（半椭圆内）

        /*        private void DrawCloud(Graphics g)
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

                    // 双线性插值到 200x200 （分辨率提高，图像更平滑）
                    int outW = 200, outH = 200;
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

                    // === 生成位图 ===
                    Bitmap bmp = new Bitmap(outW, outH);
                    for (int x = 0; x < outW; x++)
                    {
                        for (int y = 0; y < outH; y++)
                        {
                            bmp.SetPixel(x, y, GetColorFromValue(output[x, y]));
                        }
                    }

                    // 半椭圆参数
                    Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);

                    // 定义半椭圆路径
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddArc(ellipseRect, 180, 180);
                        path.CloseFigure();

                        // 开启高质量渲染
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                        // 设置裁剪区域（半椭圆内）
                        g.SetClip(path);

                        // 将热力图缩放绘制到 Panel
                        g.DrawImage(bmp, new Rectangle(0, 0, this.Width, this.Height));

                        g.ResetClip();

                        // 画半椭圆边框
                        using (Pen pen = new Pen(Color.Black, 1))
                        {
                            g.DrawArc(pen, ellipseRect, 180, 180);
                        }
                    }
                }*/
        private void DrawCloud(Graphics g)
        {
            int outW = this.Width;
            int outH = this.Height;
            Bitmap bmp = new Bitmap(outW, outH);

            // 半椭圆参数
            float cx = outW / 2f;
            float cy = outH;             // 半椭圆底部
            float a = outW / 2f - 5;     // 水平方向半径
            float b = outH - 5;          // 垂直方向半径

            // 8 个方向对应的角度（弧度制）
            double[] angles =
            {
        -135 * Math.PI / 180, // 左上
        -45  * Math.PI / 180, // 右上
        180  * Math.PI / 180, // 左
        0,                    // 中（特殊，放在底边中心）
        0,                    // 右
        135 * Math.PI / 180,  // 左下
        90  * Math.PI / 180,  // 下
        45  * Math.PI / 180   // 右下
    };

            // 8 个通道点坐标
            PointF[] sensors = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                if (i == 3)
                {
                    sensors[i] = new PointF(cx, cy - b / 2); // 中间通道，放在半椭圆中点
                }
                else
                {
                    sensors[i] = new PointF(
                        cx + (float)(a * Math.Cos(angles[i])),
                        cy + (float)(b * Math.Sin(angles[i]))
                    );
                }
            }

            // 遍历半椭圆区域
            for (int x = 0; x < outW; x++)
            {
                for (int y = 0; y < outH; y++)
                {
                    double norm = ((x - cx) * (x - cx)) / (a * a) +
                                  ((y - cy) * (y - cy)) / (b * b);

                    if (norm > 1.0) continue; // 在半椭圆外部，跳过

                    // 反距离加权插值
                    double val = 0, wsum = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        double dx = x - sensors[i].X;
                        double dy = y - sensors[i].Y;
                        double dist2 = dx * dx + dy * dy + 1e-6; // 防止除零
                        double w = 1.0 / dist2;
                        val += values[i] * w;
                        wsum += w;
                    }
                    val /= wsum;

                    bmp.SetPixel(x, y, GetColorFromValue(val));
                }
            }

            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.DrawImage(bmp, new Rectangle(0, 0, outW, outH));

            // 半椭圆边框
            using (Pen pen = new Pen(Color.Black, 1))
            {
                g.DrawArc(pen, new Rectangle(5, 5, this.Width - 10, this.Height * 2 - 10), 180, 180);
            }
        }


        #endregion

        private Color GetColorFromValue(double value)
        {
            double maxAbs = 10000; // 你传感器的最大绝对值

            // 限制范围
            if (value < -maxAbs) value = -maxAbs;
            if (value > maxAbs) value = maxAbs;

            double r = 0, g = 0, b = 0;

            if (value < 0)
            {
                // 负值：蓝 -> 绿
                double ratio = (value + maxAbs) / maxAbs; // -maxAbs → 0 映射到 0~1

                if (ratio < 0.5)
                {
                    // 深蓝 -> 青
                    r = 0;
                    g = ratio * 2;
                    b = 1;
                }
                else
                {
                    // 青 -> 绿
                    r = 0;
                    g = 1;
                    b = 2 * (1 - ratio);
                }
            }
            else
            {
                // 正值：绿 -> 红
                double ratio = value / maxAbs; // 0 → maxAbs 映射到 0~1

                if (ratio < 0.5)
                {
                    // 绿 -> 黄
                    r = ratio * 2;
                    g = 1;
                    b = 0;
                }
                else
                {
                    // 黄 -> 红
                    r = 1;
                    g = 2 * (1 - ratio);
                    b = 0;
                }
            }

            // Clamp & 转成Color
            r = Math.Max(0, Math.Min(1, r));
            g = Math.Max(0, Math.Min(1, g));
            b = Math.Max(0, Math.Min(1, b));

            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }

        /*        private Color GetColorFromValue(double value)
                {
                    value = Math.Max(0, Math.Min(value, 100000));
                    int r = (int)(value / 100000.0 * 255);
                    int g = 0;
                    int b = 255 - r;
                    return Color.FromArgb(r, g, b);
                }*/

        private void DrawForceArrow(Graphics g)
        {
            if (values == null || values.Length != 8)
                return;

            // Panel 中心
            float cx = this.Width / 2f;
            float cy = this.Height / 2f;

            // 八个方向的单位向量
            PointF[] dirs =
            {
                new PointF(-1,-1), // 左上 0
                new PointF( 1,-1), // 右上 1
                new PointF(-1, 0), // 左   2
                new PointF( 0, 0), // 中   3 （通常不用作为方向，可忽略或仅作权重）
                new PointF( 1, 0), // 右   4
                new PointF(-1, 1), // 左下 5
                new PointF( 0, 1), // 下   6
                new PointF( 1, 1)  // 右下 7
            };

            // 计算合力向量
            float fx = 0, fy = 0;
            for (int i = 0; i < values.Length; i++)
            {
                // 中点 (索引3) 可以不加方向，只算强度
                if (i == 3) continue;

                float mag = (float)values[i];
                fx += dirs[i].X * mag;
                fy += dirs[i].Y * mag;
            }

            // 长度缩放，避免超出
            float len = (float)Math.Sqrt(fx * fx + fy * fy);
            if (len < 1e-3) return;

            float scale = Math.Min(this.Width, this.Height) / 4f / len;
            fx *= scale;
            fy *= scale;

            // 目标点
            float tx = cx + fx;
            float ty = cy + fy;

            using (Pen pen = new Pen(Color.White, 3))
            {
                pen.CustomEndCap = new AdjustableArrowCap(6, 8, true);
                g.DrawLine(pen, cx, cy, tx, ty);
            }
        }


    }
}
