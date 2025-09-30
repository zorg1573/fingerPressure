/*using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace fingerPressure
{
    public class DoubleBufferedPanelCloud27 : Panel
    {
        private double[] values = new double[27]; // 27通道数据

        public DoubleBufferedPanelCloud27()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
        }

        /// <summary>
        /// 设置/获取27通道值
        /// </summary>
        public double[] Values
        {
            get => values;
            set
            {
                if (value != null && value.Length == 9)
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
            DrawForceArrow9(e.Graphics);
        }
        private double Clamp(double value)
        {
            return Math.Max(0, Math.Min(100000, value));
        }
        #region 云图模式（半椭圆内）
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

            // 9 个通道 → 3x3 网格布局
            // 索引对应关系：
            // 0 左上, 1 中上, 2 右上
            // 3 左中, 4 中,   5 右中
            // 6 左下, 7 中下, 8 右下
            PointF[] sensors = new PointF[9];

            for (int row = 0; row < 3; row++)   // 上中下
            {
                for (int col = 0; col < 3; col++) // 左中右
                {
                    int idx = row * 3 + col;

                    // 在 [-1,1] 范围内均匀分布
                    float nx = (col - 1) / 1.0f;  // -1,0,1
                    float ny = (row - 2) / 1.0f;  // -2,-1,0 → 映射到上/中/下

                    // 变换到半椭圆内
                    sensors[idx] = new PointF(
                        cx + nx * a,
                        cy + ny * (b / 2f)   // 垂直方向压缩映射到半椭圆
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

                    if (norm > 1.0) continue; // 半椭圆外部，跳过

                    // 反距离加权插值
                    double val = 0, wsum = 0;
                    for (int i = 0; i < 9; i++)
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

        private void DrawForceArrow9(Graphics g)
        {
            if (values == null || values.Length != 9)
                return;

            // Panel 中心
            float cx = this.Width / 2f;
            float cy = this.Height / 2f;

            // 9 通道的单位向量（对应 3x3 位置）
            PointF[] dirs =
            {
        new PointF(-1,-1), // 0 左上
        new PointF( 0,-1), // 1 上
        new PointF( 1,-1), // 2 右上
        new PointF(-1, 0), // 3 左
        new PointF( 0, 0), // 4 中心 (不计入方向)
        new PointF( 1, 0), // 5 右
        new PointF(-1, 1), // 6 左下
        new PointF( 0, 1), // 7 下
        new PointF( 1, 1)  // 8 右下
    };

            // 计算合力向量
            float fx = 0, fy = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (i == 4) continue; // 中心点不加方向

                float mag = (float)values[i];
                fx += dirs[i].X * mag;
                fy += dirs[i].Y * mag;
            }

            // 向量长度
            float len = (float)Math.Sqrt(fx * fx + fy * fy);
            if (len < 1e-3) return;

            // 缩放比例，避免超出 Panel
            float scale = Math.Min(this.Width, this.Height) / 4f / len;
            fx *= scale;
            fy *= scale;

            // 目标点
            float tx = cx + fx;
            float ty = cy + fy;

            using (Pen pen = new Pen(Color.White, 3)) // 用蓝色区分 9 通道箭头
            {
                pen.CustomEndCap = new AdjustableArrowCap(6, 8, true);
                g.DrawLine(pen, cx, cy, tx, ty);
            }
        }

    }
}
*/

/*using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace fingerPressure
{
    public class DoubleBufferedPanelCloud27 : Panel
    {
        private double[] values = new double[9];
        private PointF[] sensorPositions;        // 9个传感器坐标缓存
        private Bitmap backgroundCache;          // 背景缓存（半椭圆+边框）
        private bool needsRefresh;               // 节流标记
        private int gridW, gridH;                // 渲染分辨率（降采样用）

        public DoubleBufferedPanelCloud27()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            this.Resize += (_, __) => GenerateBackgroundCache();
        }

        /// <summary>
        /// 设置/获取9通道值
        /// </summary>
        public double[] Values
        {
            get => values;
            set
            {
                if (value != null && value.Length == 9)
                    Array.Copy(value, values, 9);
                Invalidate();
            }
        }

        private void GenerateBackgroundCache()
        {
            backgroundCache?.Dispose();
            backgroundCache = new Bitmap(this.Width, this.Height);

            int outW = this.Width;
            int outH = this.Height;

            gridW = outW / 2; // 降采样，减少计算开销
            gridH = outH / 2;

            float cx = outW / 2f;
            float cy = outH;
            float a = outW / 2f - 5;
            float b = outH - 5;

            // 计算 9个传感器坐标
            sensorPositions = new PointF[9];
            for (int row = 0; row < 3; row++)
            {
                for (int col = 0; col < 3; col++)
                {
                    int idx = row * 3 + col;
                    float nx = (col - 1) / 1.0f;
                    float ny = (row - 2) / 1.0f;
                    sensorPositions[idx] = new PointF(
                        cx + nx * a,
                        cy + ny * (b / 2f)
                    );
                }
            }

            using (var g = Graphics.FromImage(backgroundCache))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                // 半椭圆背景
                Rectangle ellipseRect = new Rectangle(5, 5, outW - 10, (outH - 10) * 2);
                using (Brush b2 = new SolidBrush(Color.LightGray))
                {
                    g.FillPie(b2, ellipseRect, 180, 180);
                }
                g.DrawArc(Pens.Black, ellipseRect, 180, 180);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 禁止背景清除，避免闪烁
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (backgroundCache == null) GenerateBackgroundCache();

            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 绘制背景缓存
            g.DrawImageUnscaled(backgroundCache, Point.Empty);

            // 绘制云图（用矩形块代替逐像素SetPixel，性能高很多）
            DrawCloud(g);

            // 绘制合力箭头
            DrawForceArrow9(g);
        }

        private void DrawCloud(Graphics g)
        {
            int outW = this.Width;
            int outH = this.Height;

            float cx = outW / 2f;
            float cy = outH;
            float a = outW / 2f - 5;
            float b = outH - 5;

            int cellW = outW / gridW;
            int cellH = outH / gridH;

            using (Bitmap bmp = new Bitmap(gridW, gridH))
            {
                for (int x = 0; x < gridW; x++)
                {
                    for (int y = 0; y < gridH; y++)
                    {
                        float rx = x * cellW + cellW / 2f;
                        float ry = y * cellH + cellH / 2f;

                        double norm = ((rx - cx) * (rx - cx)) / (a * a) +
                                      ((ry - cy) * (ry - cy)) / (b * b);
                        if (norm > 1.0) continue;

                        double val = 0, wsum = 0;
                        for (int i = 0; i < 9; i++)
                        {
                            double dx = rx - sensorPositions[i].X;
                            double dy = ry - sensorPositions[i].Y;
                            double dist2 = dx * dx + dy * dy + 1e-6;
                            double w = 1.0 / dist2;
                            val += values[i] * w;
                            wsum += w;
                        }
                        val /= wsum;

                        bmp.SetPixel(x, y, GetColorFromValue(val));
                    }
                }

                // 放大绘制到Panel上
                g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                g.DrawImage(bmp, new Rectangle(0, 0, outW, outH));
            }
        }
        private Color GetColorFromValue(double value)
        {
            double maxAbs = 10000;

            // 限幅
            if (value < 0) value = 0;
            if (value > maxAbs) value = maxAbs;

            // 归一化到 0~1
            double ratio = value / maxAbs;

            int r = 0, g = 0, b = 0;

            if (ratio < 0.33) // 蓝 -> 绿
            {
                double t = ratio / 0.33;
                r = 0;
                g = (int)(255 * t);
                b = (int)(255 * (1 - t));
            }
            else if (ratio < 0.66) // 绿 -> 黄
            {
                double t = (ratio - 0.33) / 0.33;
                r = (int)(255 * t);
                g = 255;
                b = 0;
            }
            else // 黄 -> 红
            {
                double t = (ratio - 0.66) / 0.34;
                r = 255;
                g = (int)(255 * (1 - t));
                b = 0;
            }

            // 透明度：0 时完全透明，100% 力时完全不透明
            int a = (int)(255 * ratio);

            return Color.FromArgb(a, r, g, b);
        }


        *//*        private Color GetColorFromValue(double value)
                {
                    double maxAbs = 10000;

                    // 限幅
                    if (value < 0) value = 0;
                    if (value > maxAbs) value = maxAbs;

                    // 归一化到 0~1
                    double ratio = value / maxAbs;

                    int r, g, b, a;

                    if (ratio <= 0.5)
                    {
                        // 0 ~ 0.5: 灰色 (128,128,128) -> 橙色 (255,165,0)
                        double t = ratio / 0.5;

                        r = (int)(128 + (255 - 128) * t);
                        g = (int)(128 + (165 - 128) * t);
                        b = (int)(128 + (0 - 128) * t);
                    }
                    else
                    {
                        // 0.5 ~ 1: 橙色 (255,165,0) -> 红色 (255,0,0)
                        double t = (ratio - 0.5) / 0.5;

                        r = 255;
                        g = (int)(165 + (0 - 165) * t);
                        b = 0;
                    }

                    // Alpha: 0 力时半透明 (50)，最大力时全不透明 (255)
                    a = (int)(50 + (255 - 50) * ratio);

                    return Color.FromArgb(a, r, g, b);
                }*//*


        private void DrawForceArrow9(Graphics g)
        {
            if (values == null || values.Length != 9) return;

            float cx = this.Width / 2f;
            float cy = this.Height / 2f;

            PointF[] dirs =
            {
                new PointF(-1,-1), new PointF(0,-1), new PointF(1,-1),
                new PointF(-1, 0), new PointF(0,0),  new PointF(1,0),
                new PointF(-1, 1), new PointF(0,1),  new PointF(1,1)
            };

            float fx = 0, fy = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (i == 4) continue;
                float mag = (float)values[i];
                fx += dirs[i].X * mag;
                fy += dirs[i].Y * mag;
            }

            float len = (float)Math.Sqrt(fx * fx + fy * fy);
            if (len < 1e-3) return;

            float scale = Math.Min(this.Width, this.Height) / 4f / len;
            fx *= scale;
            fy *= scale;

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
*/

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class DoubleBufferedPanelCloud27 : Panel
    {
        private double[] values = new double[9]; // 9通道数据
        private Rectangle[] dotRects; // 点阵矩形缓存
        private Bitmap backgroundCache; // 背景缓存
        private Bitmap heatmapCache; // 新增：热力图缓存
        //private bool valuesChanged = true; // 新增：标记值是否变化
        private float fontHeight; // 字体高度缓存
        private bool guiyihua = false; // 是否归一化显示

        public DoubleBufferedPanelCloud27()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
            this.Resize += (_, __) =>
            {
                //valuesChanged = true; // 大小变化时强制重计算
                GenerateBackgroundCache();
            };
            this.FontChanged += (_, __) => CacheFontHeight();
            CacheFontHeight();
        }

        /// <summary>
        /// 设置/获取9通道值
        /// </summary>
        public double[] Values
        {
            get => values;
            set
            {
                if (value != null && value.Length == values.Length)
                {
                    Array.Copy(value, values, values.Length);
                    //valuesChanged = true; // 标记变化
                    Invalidate(); // 触发重绘
                }
            }
        }

        public bool Guiyihua
        {
            get => guiyihua;
            set { guiyihua = value; }
        }

        private void CacheFontHeight()
        {
            using (Graphics g = CreateGraphics())
            {
                fontHeight = g.MeasureString("0", this.Font).Height;
            }
        }

        private void GenerateBackgroundCache()
        {
            backgroundCache?.Dispose();
            backgroundCache = new Bitmap(this.Width, this.Height);
            dotRects = new Rectangle[values.Length];
            using (var g = Graphics.FromImage(backgroundCache))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);
                // 半椭圆背景
                Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
                using (Brush b = new SolidBrush(Color.LightGray))
                {
                    g.FillPie(b, ellipseRect, 180, 180);
                }
                g.DrawArc(Pens.Black, ellipseRect, 180, 180);
                // 点阵布局 (3-3-3)
                int circleDiameter = Math.Min(this.Width, this.Height) / 6;
                int marginTop = 20;
                int marginBottom = 10;
                int verticalSpacing = (this.Height - marginTop - marginBottom - 3 * circleDiameter) / 2;
                int[] rowCols = { 3, 3, 3 }; // 第一行增加到3个点
                int valueIndex = 0;
                for (int row = 0; row < rowCols.Length; row++)
                {
                    int cols = rowCols[row];
                    int rowY = marginTop + row * (circleDiameter + verticalSpacing);
                    int totalWidth = cols * circleDiameter + (cols - 1) * circleDiameter / 2;
                    int startX = (this.Width - totalWidth) / 2;
                    for (int col = 0; col < cols; col++)
                    {
                        if (valueIndex >= values.Length) break;
                        int x = startX + col * (circleDiameter + circleDiameter / 2);
                        int y = rowY;
                        dotRects[valueIndex] = new Rectangle(x, y, circleDiameter, circleDiameter);
                        valueIndex++;
                    }
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 禁止背景清除，避免闪烁
        }

        private Color GetColorFromValue(double value)
        {
            double maxAbs = guiyihua ? 500 : 10000;
            if (value < 0) value = 0;
            if (value > maxAbs) value = maxAbs;
            // 归一化到 0~1
            double ratio = value / maxAbs;
            int r = 0, g = 0, b = 0;
            if (ratio < 0.33) // 蓝 -> 绿
            {
                double t = ratio / 0.33;
                r = 0;
                g = (int)(255 * t);
                b = (int)(255 * (1 - t));
            }
            else if (ratio < 0.66) // 绿 -> 黄
            {
                double t = (ratio - 0.33) / 0.33;
                r = (int)(255 * t);
                g = 255;
                b = 0;
            }
            else // 黄 -> 红
            {
                double t = (ratio - 0.66) / 0.34;
                r = 255;
                g = (int)(255 * (1 - t));
                b = 0;
            }
            // 透明度：0 时完全透明，100% 力时完全不透明
            int a = (int)(255 * ratio);
            return Color.FromArgb(a, r, g, b);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 只当值变化或缓存为空时重计算
            //if (valuesChanged || backgroundCache == null || heatmapCache == null)
                GenerateBackgroundCache(); // 只在需要时生成背景

                // 计算点中心坐标
                PointF[] centers = new PointF[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    centers[i] = new PointF(dotRects[i].X + dotRects[i].Width / 2f, dotRects[i].Y + dotRects[i].Height / 2f);
                }

                // 创建热力图缓存，使用 LockBits 加速像素设置
                heatmapCache?.Dispose();
                heatmapCache = new Bitmap(this.Width, this.Height);
                var bmpData = heatmapCache.LockBits(new Rectangle(0, 0, Width, Height), System.Drawing.Imaging.ImageLockMode.ReadWrite, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0.ToPointer();
                    int bytesPerPixel = 4; // ARGB
                    double power = 2.0; // 插值幂
                    double epsilon = 1e-6;

                    for (int y = 0; y < this.Height; y++)
                    {
                        for (int x = 0; x < this.Width; x++)
                        {
                            double sumValue = 0.0;
                            double sumWeight = 0.0;
                            bool isCenter = false;
                            for (int i = 0; i < values.Length; i++)
                            {
                                double dx = x - centers[i].X;
                                double dy = y - centers[i].Y;
                                double dist = Math.Sqrt(dx * dx + dy * dy);
                                if (dist < epsilon)
                                {
                                    sumValue = values[i];
                                    isCenter = true;
                                    break;
                                }
                                double weight = 1.0 / Math.Pow(dist, power);
                                sumValue += values[i] * weight;
                                sumWeight += weight;
                            }
                            double interpValue = isCenter ? sumValue : (sumValue / sumWeight);
                            Color color = GetColorFromValue(interpValue);

                            int pixelOffset = (y * bmpData.Stride) + (x * bytesPerPixel);
                            ptr[pixelOffset] = color.B;
                            ptr[pixelOffset + 1] = color.G;
                            ptr[pixelOffset + 2] = color.R;
                            ptr[pixelOffset + 3] = color.A;
                        }
                    }
                //}
                heatmapCache.UnlockBits(bmpData);

                //valuesChanged = false; // 重置标记
            }

            // 先画背景缓存
            if (backgroundCache != null)
            {
                g.DrawImageUnscaled(backgroundCache, Point.Empty);
            }

            // 确保数据不为空
            if (dotRects == null || values.Length == 0) return;

            // 将热力图层绘制到主画布，并限制在半椭圆区域内
            using (GraphicsPath clipPath = new GraphicsPath())
            {
                Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
                clipPath.AddPie(ellipseRect, 180, 180);
                g.SetClip(clipPath);
                if (heatmapCache != null)
                {
                    g.DrawImageUnscaled(heatmapCache, Point.Empty);
                }
                g.ResetClip();
            }

            DrawForceArrow9(g);
        }

        private void DrawForceArrow9(Graphics g)
        {
            if (values == null || values.Length != 9) return;
            float cx = this.Width / 2f;
            float cy = this.Height / 2f;

            PointF[] dirs = new PointF[9];
            int index = 0;
            float rowSpacing = this.Height / 3f;
            float colSpacing = this.Width / 4f; // 统一列间距，因为每行3个点

            dirs[index++] = new PointF(-1, -1); // 第一行：左
            dirs[index++] = new PointF(0, -1);  // 第一行：中
            dirs[index++] = new PointF(1, -1);  // 第一行：右
            dirs[index++] = new PointF(-1, 0);  // 第二行：左
            dirs[index++] = new PointF(0, 0);   // 第二行：中
            dirs[index++] = new PointF(1, 0);   // 第二行：右
            dirs[index++] = new PointF(-1, 1);  // 第三行：左
            dirs[index++] = new PointF(0, 1);   // 第三行：中
            dirs[index++] = new PointF(1, 1);   // 第三行：右

            float fx = 0, fy = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float mag = (float)values[i];
                fx += dirs[i].X * mag;
                fy += dirs[i].Y * mag;
            }

            float len = (float)Math.Sqrt(fx * fx + fy * fy);
            if (len < 1e-3) return;

            float scale = Math.Min(this.Width, this.Height) / 4f / len;
            fx *= scale;
            fy *= scale;
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