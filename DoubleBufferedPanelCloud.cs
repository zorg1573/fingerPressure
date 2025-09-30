/*using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace fingerPressure
{
    public class DoubleBufferedPanelCloud : Panel
    {
        private double[] values = new double[8];    // 8通道数据
        private Bitmap backgroundCache;             // 背景缓存
        private Bitmap cloudBitmap;                 // 热力图缓存
        private PointF[] sensors;                   // 传感器点位缓存
        private bool needsRefresh;                  // 节流标记
        private bool guiyihua = false;            // 是否归一化显示

        public DoubleBufferedPanelCloud()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
            this.Resize += (_, __) => GenerateBackgroundCache();
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
                    Array.Copy(value, values, values.Length);
                Invalidate();
            }
        }
        public bool Guiyihua
        {
            get => guiyihua;
            set
            {
                guiyihua = value;
            }
        }

        private void GenerateBackgroundCache()
        {
            backgroundCache?.Dispose();
            cloudBitmap?.Dispose();

            backgroundCache = new Bitmap(this.Width, this.Height);
            cloudBitmap = new Bitmap(this.Width, this.Height);

            float cx = this.Width / 2f;
            float cy = this.Height;
            float a = this.Width / 2f - 5;
            float b = this.Height - 5;

            double[] angles =
            {
                -45 * Math.PI / 180, // 左上
                -135  * Math.PI / 180, // 右上
                0  * Math.PI / 180, // 左
                0,                    // 中（特殊）
                180,                    // 右
                45 * Math.PI / 180,  // 左下
                90  * Math.PI / 180,  // 下
                135  * Math.PI / 180   // 右下
            };

            sensors = new PointF[8];
            for (int i = 0; i < 8; i++)
            {
                if (i == 3)
                    sensors[i] = new PointF(cx, cy - b / 2); // 中点
                else
                    sensors[i] = new PointF(
                        cx + (float)(a * Math.Cos(angles[i])),
                        cy + (float)(b * Math.Sin(angles[i]))
                    );
            }

            using (var g = Graphics.FromImage(backgroundCache))
            {
                g.Clear(this.BackColor);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
                g.FillPie(Brushes.LightGray, ellipseRect, 180, 180);
                g.DrawArc(Pens.Black, ellipseRect, 180, 180);
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 不清背景，避免闪烁
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (backgroundCache != null)
                e.Graphics.DrawImageUnscaled(backgroundCache, 0, 0);

            if (cloudBitmap != null && sensors != null)
            {
                RenderCloud(cloudBitmap);
                e.Graphics.DrawImageUnscaled(cloudBitmap, 0, 0);
            }
            var g = e.Graphics;
            DrawForceArrow8(g);
        }

        private void RenderCloud(Bitmap bmp)
        {
            int w = bmp.Width;
            int h = bmp.Height;

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h),
                                           ImageLockMode.WriteOnly,
                                           PixelFormat.Format32bppArgb);

            int stride = data.Stride;
            IntPtr scan0 = data.Scan0;
            byte[] buffer = new byte[stride * h];

            float cx = w / 2f;
            float cy = h;
            float a = w / 2f - 5;
            float b = h - 5;

            for (int y = 0; y < h; y++)
            {
                int row = y * stride;
                for (int x = 0; x < w; x++)
                {
                    double norm = ((x - cx) * (x - cx)) / (a * a) +
                                  ((y - cy) * (y - cy)) / (b * b);

                    if (norm > 1.0) continue;

                    double val = 0, wsum = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        double dx = x - sensors[i].X;
                        double dy = y - sensors[i].Y;
                        double dist2 = dx * dx + dy * dy + 1e-6;
                        double weight = 1.0 / dist2;
                        val += values[i] * weight;
                        wsum += weight;
                    }
                    val /= wsum;

                    Color c = GetColorFromValue(val);
                    int idx = row + x * 4;
                    buffer[idx + 0] = c.B;
                    buffer[idx + 1] = c.G;
                    buffer[idx + 2] = c.R;
                    buffer[idx + 3] = c.A;  // ← 保留透明度

                }
            }

            Marshal.Copy(buffer, 0, scan0, buffer.Length);
            bmp.UnlockBits(data);
        }
        private Color GetColorFromValue(double value)
        {
            double maxAbs = 100000;
            if (guiyihua)
            {
                maxAbs = 500;
            }
            else
            {
                maxAbs = 500000;
            }

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
                    double maxAbs = 100000;
                    if (guiyihua)
                    {
                        maxAbs = 500;
                    }
                    else
                    {
                        maxAbs = 500000;
                    }

                    if (value < -maxAbs) value = -maxAbs;
                    if (value > maxAbs) value = maxAbs;

                    double r = 0, g = 0, b = 0;

                    if (value < 0)
                    {
                        double ratio = (value + maxAbs) / maxAbs;
                        if (ratio < 0.5) { r = 0; g = ratio * 2; b = 1; }
                        else { r = 0; g = 1; b = 2 * (1 - ratio); }
                    }
                    else
                    {
                        double ratio = value / maxAbs;
                        if (ratio < 0.5) { r = ratio * 2; g = 1; b = 0; }
                        else { r = 1; g = 2 * (1 - ratio); b = 0; }
                    }

                    return Color.FromArgb(
                        (int)(r * 255),
                        (int)(g * 255),
                        (int)(b * 255));
                }*//*

        private void DrawForceArrow8(Graphics g)
        {
            if (values == null || values.Length != 8) return;

            float cx = this.Width / 2f;
            float cy = this.Height / 2f;

            // 三行布局：2-3-3
            // 第一行 2 点
            // 第二行 3 点
            // 第三行 3 点
            PointF[] dirs = new PointF[8];

            int index = 0;
            float rowSpacing = this.Height / 3f;
            float colSpacingTop = this.Width / 3f;
            float colSpacingMiddle = this.Width / 4f;
            float colSpacingBottom = this.Width / 4f;

            // 第一行 (2 点)
            dirs[index++] = new PointF(1, -1); // 左
            dirs[index++] = new PointF(-1, -1);  // 右

            // 第二行 (3 点)
            dirs[index++] = new PointF(1, 0);  // 左
            dirs[index++] = new PointF(0, 0);   // 中
            dirs[index++] = new PointF(-1, 0);   // 右

            // 第三行 (3 点)
            dirs[index++] = new PointF(1, 1);  // 左
            dirs[index++] = new PointF(0, 1);   // 中
            dirs[index++] = new PointF(-1, 1);   // 右

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
        private float fontHeight; // 字体高度缓存
        private bool guiyihua = false; // 是否归一化显示

        public DoubleBufferedPanelCloud()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
            this.Resize += (_, __) => GenerateBackgroundCache();
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
                    Array.Copy(value, values, values.Length);
                Invalidate();
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
            // 每次绘制时更新背景缓存
            GenerateBackgroundCache();
            // 先画缓存的背景
            if (backgroundCache != null)
            {
                g.DrawImageUnscaled(backgroundCache, Point.Empty);
            }
            // 确保数据不为空
            if (dotRects == null || values.Length == 0) return;

            // 创建一个临时的位图用于绘制渐变，覆盖整个半椭圆区域
            using (Bitmap heatmap = new Bitmap(this.Width, this.Height))
            using (Graphics heatmapGraphics = Graphics.FromImage(heatmap))
            {
                heatmapGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                heatmapGraphics.Clear(Color.Transparent);

                // 遍历每个点，绘制大范围渐变
                for (int i = 0; i < values.Length && i < dotRects.Length; i++)
                {
                    var rect = dotRects[i];
                    double value = values[i];
                    // 计算颜色
                    Color centerColor = GetColorFromValue(value);
                    // 创建更大的渐变区域（圆点直径的3倍）
                    int gradientDiameter = (int)(rect.Width * 3);
                    Rectangle gradientRect = new Rectangle(
                        rect.X - (gradientDiameter - rect.Width) / 2,
                        rect.Y - (gradientDiameter - rect.Height) / 2,
                        gradientDiameter,
                        gradientDiameter
                    );
                    // 使用径向渐变刷
                    using (GraphicsPath path = new GraphicsPath())
                    {
                        path.AddEllipse(gradientRect);
                        using (PathGradientBrush brush = new PathGradientBrush(path))
                        {
                            brush.CenterColor = centerColor;
                            brush.SurroundColors = new[] { Color.FromArgb(0, centerColor) }; // 边缘透明
                            brush.FocusScales = new PointF(0.3f, 0.3f); // 缩小焦点，扩大渐变范围
                            heatmapGraphics.FillEllipse(brush, gradientRect);
                        }
                    }
                }

                // 将渐变图层绘制到主画布，并限制在半椭圆区域内
                using (GraphicsPath clipPath = new GraphicsPath())
                {
                    Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
                    clipPath.AddPie(ellipseRect, 180, 180);
                    g.SetClip(clipPath);
                    g.DrawImageUnscaled(heatmap, Point.Empty);
                    g.ResetClip();
                }
            }

            // 再次绘制中心圆点，确保清晰
            for (int i = 0; i < values.Length && i < dotRects.Length; i++)
            {
                var rect = dotRects[i];
                double value = values[i];
                // 计算颜色
                Color centerColor = GetColorFromValue(value);
                // 绘制中心圆点
                using (Brush brush = new SolidBrush(centerColor))
                {
                    g.FillEllipse(brush, rect);
                }
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
        private double[] values = new double[8]; // 8通道数据
        private Rectangle[] dotRects; // 点阵矩形缓存
        private Bitmap backgroundCache; // 背景缓存
        private Bitmap heatmapCache; // 新增：热力图缓存
        //private bool valuesChanged = true; // 新增：标记值是否变化
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
                //valuesChanged = true; // 大小变化时强制重计算
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
/*            if (valuesChanged || backgroundCache == null || heatmapCache == null)
            {*/
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
}