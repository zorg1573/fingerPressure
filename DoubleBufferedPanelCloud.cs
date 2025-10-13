/*using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class DoubleBufferedPanelCloud : Panel
    {
        private double[] values = new double[8]; // 8通道数据
        private Rectangle[] dotRects; // 点阵矩形缓存
        private Bitmap backgroundCache; // 背景缓存
        private Bitmap heatmapCache; // 新增：热力图缓存
        private bool valuesChanged = true; // 新增：标记值是否变化
        private float fontHeight; // 字体高度缓存
        private bool guiyihua = false; // 是否归一化显示

        public DoubleBufferedPanelCloud()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
            this.Resize += (_, __) =>
            {
                valuesChanged = true; // 大小变化时强制重计算
                GenerateBackgroundCache();
            };
            this.FontChanged += (_, __) => CacheFontHeight();
            CacheFontHeight();
        }

        /// <summary>
        /// 设置/获取8通道值
        /// </summary>
        public double[] Values
        {
            get => values;
            set
            {
                if (value != null && value.Length == values.Length)
                {
                    Array.Copy(value, values, values.Length);
                    valuesChanged = true; // 标记变化
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
            dotRects = new Rectangle[values.Length + 1];
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
                // 点阵布局 (2-3-3)
                int circleDiameter = Math.Min(this.Width, this.Height) / 6;
                int marginTop = 20;
                int marginBottom = 10;
                int verticalSpacing = (this.Height - marginTop - marginBottom - 3 * circleDiameter) / 2;
                int[] rowCols = { 2, 3, 3 };
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
                dotRects[8] = dotRects[5];
                dotRects[5] = dotRects[7];
                dotRects[7] = dotRects[8];
                dotRects[8] = dotRects[0];
                dotRects[0] = dotRects[1];
                dotRects[1] = dotRects[8];
                dotRects[8] = dotRects[2];
                dotRects[2] = dotRects[4];
                dotRects[4] = dotRects[8];
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 禁止背景清除，避免闪烁
        }

        private Color GetColorFromValue(double value)
        {
            double maxAbs = guiyihua ? 500 : 500000;
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
            if (valuesChanged || backgroundCache == null || heatmapCache == null)
            {
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
                }
                heatmapCache.UnlockBits(bmpData);

                valuesChanged = false; // 重置标记
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

            DrawForceArrow8(g);
        }

        private void DrawForceArrow8(Graphics g)
        {
            if (values == null || values.Length != 8) return;
            float cx = this.Width / 2f;
            float cy = this.Height / 2f;
            PointF[] dirs = new PointF[8];
            int index = 0;
            float rowSpacing = this.Height / 3f;
            float colSpacingTop = this.Width / 3f;
            float colSpacingMiddle = this.Width / 4f;
            float colSpacingBottom = this.Width / 4f;
            dirs[index++] = new PointF(1, -1);  // 左
            dirs[index++] = new PointF(-1, -1); // 右
            dirs[index++] = new PointF(1, 0);   // 左
            dirs[index++] = new PointF(0, 0);   // 中
            dirs[index++] = new PointF(-1, 0);  // 右
            dirs[index++] = new PointF(1, 1);   // 左
            dirs[index++] = new PointF(0, 1);   // 中
            dirs[index++] = new PointF(-1, 1);  // 右
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
}*/
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class DoubleBufferedPanelCloud : Panel
    {
        private double[] values = new double[8];
        private Rectangle[] dotRects;
        private Bitmap backgroundCache;
        private Bitmap heatmapCache;
        private bool valuesChanged = true;
        private float fontHeight;
        private bool guiyihua = false;

        public DoubleBufferedPanelCloud()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();

            this.Resize += (_, __) =>
            {
                valuesChanged = true;
                GenerateBackgroundCache();
            };

            this.FontChanged += (_, __) => CacheFontHeight();
            CacheFontHeight();
        }

        public double[] Values
        {
            get => values;
            set
            {
                if (value != null && value.Length == values.Length)
                {
                    Array.Copy(value, values, values.Length);
                    valuesChanged = true;
                    Invalidate();
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

                Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
                using (Brush b = new SolidBrush(Color.LightGray))
                    g.FillPie(b, ellipseRect, 180, 180);
                g.DrawArc(Pens.Black, ellipseRect, 180, 180);

                // 点阵布局 (2-3-3)
                int circleDiameter = Math.Min(this.Width, this.Height) / 6;
                int marginTop = 20;
                int marginBottom = 10;
                int verticalSpacing = (this.Height - marginTop - marginBottom - 3 * circleDiameter) / 2;
                int[] rowCols = { 2, 3, 3 };
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
            // 禁止默认背景清除
        }

        private Color GetColorFromValue(double value)
        {
            double maxAbs = guiyihua ? 500 : 500000;
            if (value < 0) value = 0;
            if (value > maxAbs) value = maxAbs;
            double ratio = value / maxAbs;

            int r, g, b;
            if (ratio < 0.33)
            {
                double t = ratio / 0.33;
                r = 0;
                g = (int)(255 * t);
                b = (int)(255 * (1 - t));
            }
            else if (ratio < 0.66)
            {
                double t = (ratio - 0.33) / 0.33;
                r = (int)(255 * t);
                g = 255;
                b = 0;
            }
            else
            {
                double t = (ratio - 0.66) / 0.34;
                r = 255;
                g = (int)(255 * (1 - t));
                b = 0;
            }

            int a = (int)(255 * ratio);
            return Color.FromArgb(a, r, g, b);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (valuesChanged || backgroundCache == null || heatmapCache == null)
            {
                GenerateBackgroundCache();

                // 计算中心点坐标
                PointF[] centers = new PointF[values.Length];
                for (int i = 0; i < values.Length; i++)
                {
                    centers[i] = new PointF(dotRects[i].X + dotRects[i].Width / 2f, dotRects[i].Y + dotRects[i].Height / 2f);
                }

                // 创建热力图（二维高斯叠加）
                heatmapCache?.Dispose();
                heatmapCache = new Bitmap(this.Width, this.Height);
                var bmpData = heatmapCache.LockBits(
                    new Rectangle(0, 0, Width, Height),
                    System.Drawing.Imaging.ImageLockMode.ReadWrite,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0.ToPointer();
                    int bytesPerPixel = 4;
                    double sigma = Math.Min(Width, Height) / 10.0;
                    double twoSigmaSq = 2 * sigma * sigma;

                    for (int y = 0; y < Height; y++)
                    {
                        for (int x = 0; x < Width; x++)
                        {
                            double sumValue = 0.0;

                            for (int i = 0; i < values.Length; i++)
                            {
                                double dx = x - centers[i].X;
                                double dy = y - centers[i].Y;
                                double distSq = dx * dx + dy * dy;

                                // 二维高斯核
                                double gaussian = Math.Exp(-distSq / twoSigmaSq);
                                sumValue += values[i] * gaussian;
                            }

                            Color color = GetColorFromValue(sumValue);
                            int pixelOffset = (y * bmpData.Stride) + (x * bytesPerPixel);
                            ptr[pixelOffset] = color.B;
                            ptr[pixelOffset + 1] = color.G;
                            ptr[pixelOffset + 2] = color.R;
                            ptr[pixelOffset + 3] = color.A;
                        }
                    }
                }
                heatmapCache.UnlockBits(bmpData);
                valuesChanged = false;
            }

            // 绘制背景
            if (backgroundCache != null)
                g.DrawImageUnscaled(backgroundCache, Point.Empty);

            // 热力图（限定区域）
            using (GraphicsPath clipPath = new GraphicsPath())
            {
                Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
                clipPath.AddPie(ellipseRect, 180, 180);
                g.SetClip(clipPath);

                if (heatmapCache != null)
                    g.DrawImageUnscaled(heatmapCache, Point.Empty);

                g.ResetClip();
            }

            DrawForceArrow8(g);
        }

        private void DrawForceArrow8(Graphics g)
        {
            if (values == null || values.Length != 8) return;
            float cx = this.Width / 2f;
            float cy = this.Height / 2f;

            PointF[] dirs = new PointF[8];
            dirs[0] = new PointF(1, -1);
            dirs[1] = new PointF(-1, -1);
            dirs[2] = new PointF(1, 0);
            dirs[3] = new PointF(0, 0);
            dirs[4] = new PointF(-1, 0);
            dirs[5] = new PointF(1, 1);
            dirs[6] = new PointF(0, 1);
            dirs[7] = new PointF(-1, 1);

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

            using (Pen pen = new Pen(Color.White, 3))
            {
                pen.CustomEndCap = new AdjustableArrowCap(6, 8, true);
                g.DrawLine(pen, cx, cy, cx + fx, cy + fy);
            }
        }
    }
}
