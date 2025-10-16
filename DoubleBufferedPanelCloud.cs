using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class DoubleBufferedPanelCloud : Panel
    {
        private Bitmap backgroundCache;
        private Bitmap heatmapCache;
        private bool cacheInvalid = true;
        private float fontHeight;
        private double[] values = new double[8]; // 8通道数据
        private Rectangle[] dotRects;

        // 新增属性：由高斯拟合结果提供
        public double CenterX { get; set; } = 100;
        public double CenterY { get; set; } = 100;
        public double Sigma { get; set; } = 0;     // 扩散半径
        public double Amplitude { get; set; } = 0; // 最大值
        public bool Guiyihua { get; set; } = false;

        public DoubleBufferedPanelCloud()
        {
            this.DoubleBuffered = true;
            this.Resize += (_, __) => { cacheInvalid = true; };
            this.FontChanged += (_, __) => CacheFontHeight();
            CacheFontHeight();
        }

        private void CacheFontHeight()
        {
            using (Graphics g = CreateGraphics())
                fontHeight = g.MeasureString("0", this.Font).Height;
        }
        public double[] Values
        {
            get => values; set
            {
                if (value != null && value.Length == values.Length)
                {
                    Array.Copy(value, values, values.Length);
                }
            }
        }
        public Rectangle[] DotRectss
        {
            get => dotRects;
        }

        private void GenerateBackground()
        {
            backgroundCache?.Dispose();
            backgroundCache = new Bitmap(this.Width, this.Height);
            dotRects = new Rectangle[values.Length];

            using (var g = Graphics.FromImage(backgroundCache))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                // ---------- 梯形背景 ----------
                int topWidth = (int)(this.Width * 0.5);     // 顶边宽
                int bottomWidth = (int)(this.Width * 0.9);  // 底边宽
                int topY = 5;
                int bottomY = this.Height - 5;

                int topWidth2 = (int)(this.Width * 1);     // 顶边宽
                int bottomWidth2 = (int)(this.Width * 1);  // 底边宽


                Point[] pts =
                {
            new Point((this.Width - topWidth2) / 2, topY),
            new Point((this.Width + topWidth2) / 2, topY),
            new Point((this.Width + bottomWidth2) / 2, bottomY),
            new Point((this.Width - bottomWidth2) / 2, bottomY)
        };

                using (Brush b = new SolidBrush(Color.Black))
                    g.FillPolygon(b, pts);
                g.DrawPolygon(Pens.Black, pts);

                // ---------- 三行传感器点 ----------
                int circleDiameter = Math.Min(this.Width, this.Height) / 6;
                int marginTop = 20;
                int marginBottom = 20;
                int verticalSpacing = (this.Height - marginTop - marginBottom - 3 * circleDiameter) / 2;

                int[] rowCols = { 2, 3, 3 };
                int valueIndex = 0;

                // 每行的横向收缩比例（越小越集中）
                double[] shrinkFactors = { 0.7, 0.8, 0.9 };
                // ↑ 可调：第一行更集中，最后一行更接近梯形宽度

                for (int row = 0; row < rowCols.Length; row++)
                {
                    int cols = rowCols[row];
                    int y = marginTop + row * (circleDiameter + verticalSpacing);

                    double t = row / (double)(rowCols.Length - 1);
                    double baseWidth = topWidth + (bottomWidth - topWidth) * t;
                    double rowWidth = baseWidth * shrinkFactors[row]; // 收缩

                    int startX = (int)((this.Width - rowWidth) / 2);
                    double spacing = rowWidth / (cols - 1);

                    for (int col = 0; col < cols; col++)
                    {
                        if (valueIndex >= values.Length) break;
                        int x = (int)(startX + col * spacing) - circleDiameter / 2;
                        dotRects[valueIndex] = new Rectangle(x, y, circleDiameter, circleDiameter);
                        valueIndex++;
                    }
                }

                // ---------- 调整索引顺序（保持原逻辑） ----------
                if (dotRects.Length >= 8)
                {
                    var tmp = dotRects[5];
                    dotRects[5] = dotRects[7];
                    dotRects[7] = tmp;

                    tmp = dotRects[0];
                    dotRects[0] = dotRects[1];
                    dotRects[1] = tmp;

                    tmp = dotRects[2];
                    dotRects[2] = dotRects[4];
                    dotRects[4] = tmp;
                }
            }
        }




        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 禁止默认背景清除
        }
        private Color GetColorFromValue(double value)
        {
            //double ratio = value / Amplitude;
            if (value > 0)
            {
                double ratio = value;
                if (ratio > 1) ratio = 1;
                if (ratio < 0) ratio = 0;
                int r = 0, g = 0, b = 0;
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
            else
            {
                return Color.Empty;
            }

        }

        //    protected override void OnPaint(PaintEventArgs e)
        //    {
        //        var g = e.Graphics;
        //        g.SmoothingMode = SmoothingMode.AntiAlias;

        //        // 背景
        //        GenerateBackground();

        //        // 生成热力云图（整幅）
        //        heatmapCache?.Dispose();
        //        heatmapCache = new Bitmap(this.Width, this.Height);

        //        var bmpData = heatmapCache.LockBits(
        //            new Rectangle(0, 0, Width, Height),
        //            System.Drawing.Imaging.ImageLockMode.WriteOnly,
        //            System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        //        unsafe
        //        {
        //            byte* ptr = (byte*)bmpData.Scan0.ToPointer();
        //            int bytesPerPixel = 4;

        //            for (int y = 0; y < this.Height; y++)
        //            {
        //                for (int x = 0; x < this.Width; x++)
        //                {
        //                    double dx = x - CenterX;
        //                    double dy = y - CenterY;
        //                    double dist2 = dx * dx + dy * dy;

        //                    // 高斯函数
        //                    double val = Amplitude * Math.Exp(-dist2 / (2 * Sigma * Sigma));
        //                    Color color = GetColorFromValue(val);

        //                    int offset = y * bmpData.Stride + x * bytesPerPixel;
        //                    ptr[offset + 0] = color.B;
        //                    ptr[offset + 1] = color.G;
        //                    ptr[offset + 2] = color.R;
        //                    ptr[offset + 3] = color.A;
        //                }
        //            }
        //        }
        //        heatmapCache.UnlockBits(bmpData);

        //        // 绘制背景
        //        if (backgroundCache != null)
        //            g.DrawImageUnscaled(backgroundCache, Point.Empty);

        //        // 绘制热力云图（不再限制区域，全屏叠加）
        //        if (heatmapCache != null)
        //            g.DrawImageUnscaled(heatmapCache, Point.Empty);

        //        // 可选：标出中心点
        //        using (Pen pen = new Pen(Color.White, 2))
        //            g.DrawEllipse(pen, (float)(CenterX - 5), (float)(CenterY - 5), 10, 10);

        //        // 绘制每个传感器值
        //        for (int i = 0; i < values.Length && i < dotRects.Length; i++)
        //        {
        //            var rect = dotRects[i];
        //            double value = values[i];
        //            string text = value.ToString("F0");
        //            SizeF ts = g.MeasureString(text, this.Font);
        //            g.DrawString(
        //                text,
        //                this.Font,
        //                Brushes.White,
        //                rect.X + (rect.Width - ts.Width) / 2,
        //                rect.Y + (rect.Height - ts.Height) / 2
        //            );
        //        }
        //    }

        //}
        protected override void OnPaint(PaintEventArgs e)
        {
            try
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 绘制背景
                GenerateBackground();

                // 取传感器最大值作为参考
                double maxValue = Math.Max(1e-6, values.Max());

                // 创建热力云图缓存
                heatmapCache?.Dispose();
                heatmapCache = new Bitmap(this.Width, this.Height);

                var bmpData = heatmapCache.LockBits(
                    new Rectangle(0, 0, Width, Height),
                    System.Drawing.Imaging.ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0.ToPointer();
                    int bytesPerPixel = 4;

                    // 扩散半径由面板大小和 maxValue 动态控制（值越大范围越大）
                    double maxRadius = Math.Min(this.Width, this.Height) / 2.0;
                    double influenceRadius = maxRadius * (maxValue / 800000.0);
                    if (influenceRadius < 50) influenceRadius = 10; // 最小扩散半径

                    double maxDist2 = influenceRadius * influenceRadius;

                    for (int y = 0; y < this.Height; y++)
                    {
                        for (int x = 0; x < this.Width; x++)
                        {
                            double dx = x - CenterX;
                            double dy = y - CenterY;
                            double dist2 = dx * dx + dy * dy;

                            // 简单线性衰减（距中心越远值越小）
                            double val = 0;
                            if (dist2 < maxDist2)
                                val = maxValue * (1.0 - Math.Sqrt(dist2) / influenceRadius);

                            // 保留 normalized 逻辑
                            double normalized = val / maxValue;
                            if (normalized > 1) normalized = 1;
                            if (normalized < 0) normalized = 0;

                            Color color = GetColorFromValue(normalized);
                            int offset = y * bmpData.Stride + x * bytesPerPixel;
                            ptr[offset + 0] = color.B;
                            ptr[offset + 1] = color.G;
                            ptr[offset + 2] = color.R;
                            ptr[offset + 3] = color.A;
                        }
                    }
                }
                heatmapCache.UnlockBits(bmpData);

                // 绘制背景
                if (backgroundCache != null)
                    g.DrawImageUnscaled(backgroundCache, Point.Empty);

                // 绘制热力云图
                if (heatmapCache != null)
                    g.DrawImageUnscaled(heatmapCache, Point.Empty);

                // 绘制力中心点
                using (Pen pen = new Pen(Color.White, 2))
                    g.DrawEllipse(pen, (float)(CenterX - 5), (float)(CenterY - 5), 10, 10);

/*                // 绘制传感器数值
                for (int i = 0; i < values.Length && i < dotRects.Length; i++)
                {
                    var rect = dotRects[i];
                    double value = values[i];
                    string text = value.ToString("F0");
                    SizeF ts = g.MeasureString(text, this.Font);
                    g.DrawString(
                        text,
                        this.Font,
                        Brushes.White,
                        rect.X + (rect.Width - ts.Width) / 2,
                        rect.Y + (rect.Height - ts.Height) / 2
                    );
                }*/
            }
            catch (Exception ex)
            {

            }

        }


    }
}