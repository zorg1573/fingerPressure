using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Windows.Forms;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;

namespace fingerPressure
{
    public partial class DoubleBufferedPanelCloud : Panel
    {
        private double[] values = new double[8];
        private Rectangle[] dotRects;
        private Bitmap backgroundCache;
        private float fontHeight;
        private bool guiyihua = false;
        private double[] gaussianParams;
        private bool paramsValid = false;

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

        public double[] Values
        {
            get => values;
            set
            {
                if (value != null && value.Length == values.Length)
                {
                    Array.Copy(value, values, values.Length);
                    paramsValid = false;
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
            dotRects = new Rectangle[values.Length + 1];

            using (var g = Graphics.FromImage(backgroundCache))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
                using (Brush b = new SolidBrush(Color.LightGray))
                    g.FillPie(b, ellipseRect, 180, 180);
                g.DrawArc(Pens.Black, ellipseRect, 180, 180);

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

        // ==================== 高斯叠加 ====================

        private double CombinedGaussian(double x, double y)
        {
            double sum = 0;
            double sigma = Math.Min(this.Width, this.Height) / 7; // 更大 σ 提升叠加效果

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] <= 0) continue;
                double cx = dotRects[i].X + dotRects[i].Width / 2.0;
                double cy = dotRects[i].Y + dotRects[i].Height / 2.0;
                double amp = values[i];
                double expTerm = Math.Exp(-((x - cx) * (x - cx) + (y - cy) * (y - cy)) / (2 * sigma * sigma));
                sum += amp * expTerm;
            }
            return sum;
        }

        private Color GetColorFromValue(double value)
        {
            double maxAbs = guiyihua ? 500 : 500000;
            if (value < 0) value = 0;
            if (value > maxAbs) value = maxAbs;
            double ratio = value / maxAbs;

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

            int a = (int)(255 * Math.Pow(ratio, 0.5)); // 提升可见度
            return Color.FromArgb(a, r, g, b);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

        // ==================== 绘制 ====================

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            GenerateBackgroundCache();

            if (backgroundCache != null)
                g.DrawImageUnscaled(backgroundCache, Point.Empty);
            if (dotRects == null || values.Length == 0) return;

            double maxAbs = guiyihua ? 500 : 500000;
            using (Bitmap heatmap = new Bitmap(this.Width, this.Height, PixelFormat.Format32bppArgb))
            {
                var bmpData = heatmap.LockBits(new Rectangle(0, 0, heatmap.Width, heatmap.Height),
                                               ImageLockMode.WriteOnly,
                                               PixelFormat.Format32bppArgb);
                unsafe
                {
                    byte* ptr = (byte*)bmpData.Scan0;
                    int stride = bmpData.Stride;

                    for (int y = 0; y < this.Height; y++)
                    {
                        byte* row = ptr + (y * stride);
                        for (int x = 0; x < this.Width; x++)
                        {
                            double v = CombinedGaussian(x, y);
                            if (v > maxAbs) v = maxAbs;
                            Color c = GetColorFromValue(v);
                            row[x * 4 + 0] = c.B;
                            row[x * 4 + 1] = c.G;
                            row[x * 4 + 2] = c.R;
                            row[x * 4 + 3] = c.A;
                        }
                    }
                }
                heatmap.UnlockBits(bmpData);
                g.DrawImage(heatmap, 0, 0);
            }
            DrawForceArrow8(g);

            /*            // 绘制传感器点
                        for (int i = 0; i < values.Length && i < dotRects.Length; i++)
                        {
                            var rect = dotRects[i];
                            double value = values[i];
                            using (Brush brush = new SolidBrush(GetColorFromValue(value)))
                                g.FillEllipse(brush, rect);

                            string text = value.ToString("F0");
                            SizeF ts = g.MeasureString(text, this.Font);
                            g.DrawString(text, this.Font, Brushes.Black,
                                rect.X + (rect.Width - ts.Width) / 2,
                                rect.Y + (rect.Height - ts.Height) / 2);
                        }*/
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
