/*using System;
using System.Drawing;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class DoubleBufferedPanel27 : Panel
    {
        private double[] values = new double[27]; // 27通道数据

        public DoubleBufferedPanel27()
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
                if (value != null && value.Length == 27)
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
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // ========= 绘制半椭圆背景 =========
            Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
            using (Brush b = new SolidBrush(Color.LightGray))
                g.FillPie(b, ellipseRect, 180, 180);
            g.DrawArc(Pens.Black, ellipseRect, 180, 180);

            // ========= 绘制点阵（3x3 布局，每点显示3个值） =========
            int totalRows = 3;
            int totalCols = 3;
            int valueIndex = 0;

            // 顶部行缩小比例
            float[] rowScale = { 0.7f, 1f, 1f };

            int marginTop = 30;
            int marginBottom = 10;
            int verticalSpace = this.Height - marginTop - marginBottom;

            for (int row = 0; row < totalRows; row++)
            {
                int cols = totalCols;
                float scale = rowScale[row];

                // 当前行圆点直径
                int circleDiameter = (int)(Math.Min(this.Width, this.Height) / 6 * scale);
                int verticalSpacing = (verticalSpace - circleDiameter * totalRows) / (totalRows - 1);

                int rowY = marginTop + row * (circleDiameter + verticalSpacing);

                // 当前行总宽度
                int totalWidth = cols * circleDiameter + (cols - 1) * circleDiameter / 2;
                int startX = (this.Width - totalWidth) / 2;

                for (int col = 0; col < cols; col++)
                {
                    if (valueIndex >= values.Length) break;

                    int x = startX + col * (circleDiameter + circleDiameter / 2);
                    int y = rowY;

                    Rectangle dotRect = new Rectangle(x, y, circleDiameter, circleDiameter);
                    using (Brush brush = new SolidBrush(Color.LightSkyBlue))
                        g.FillEllipse(brush, dotRect);
                    //g.DrawEllipse(Pens.Black, dotRect);

                    // 绘制每个点内的三个通道值，从上到下
                    int subValueCount = 3;
                    float subHeight = circleDiameter / (float)subValueCount;
                    for (int i = 0; i < subValueCount && valueIndex < values.Length; i++)
                    {
                        string text = values[valueIndex].ToString();
                        SizeF textSize = g.MeasureString(text, this.Font);
                        float textX = x + (circleDiameter - textSize.Width) / 2;
                        float textY = y + i * subHeight + (subHeight - textSize.Height) / 2;
                        g.DrawString(text, this.Font, Brushes.Black, textX, textY);
                        valueIndex++;
                    }
                }
            }
        }

    }
}
*/
using System;
using System.Drawing;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class DoubleBufferedPanel27 : Panel
    {
        private readonly double[] values = new double[27];   // 27通道数据
        private Rectangle[] dotRects;                        // 每个大圆的矩形缓存
        private RectangleF[,] subValueRects;                 // 每个圆中3个子值区域缓存
        private Bitmap backgroundCache;                      // 背景缓存
        private bool needsRefresh;                           // 节流标记

        public DoubleBufferedPanel27()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
            //this.BackColor = Color.White;

            this.Resize += (_, __) => GenerateBackgroundCache();
        }

        /// <summary>
        /// 设置/获取27通道值
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

        private void GenerateBackgroundCache()
        {
            backgroundCache?.Dispose();
            backgroundCache = new Bitmap(this.Width, this.Height);
            dotRects = new Rectangle[9]; // 3×3 大圆
            subValueRects = new RectangleF[9, 3];

            using (var g = Graphics.FromImage(backgroundCache))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(this.BackColor);

                // 半椭圆背景
                Rectangle ellipseRect = new Rectangle(5, 5, this.Width - 10, (this.Height - 10) * 2);
                using (Brush b = new SolidBrush(Color.LightGray))
                {
                    g.FillPie(b, ellipseRect, 180, 180);
                }
                g.DrawArc(Pens.Black, ellipseRect, 180, 180);

                // ========= 点阵布局 (3×3，每点3个子值) =========
                int totalRows = 3;
                int totalCols = 3;
                float[] rowScale = { 0.7f, 1f, 1f };

                int marginTop = 30;
                int marginBottom = 10;
                int verticalSpace = this.Height - marginTop - marginBottom;

                int dotIndex = 0;

                for (int row = 0; row < totalRows; row++)
                {
                    int cols = totalCols;
                    float scale = rowScale[row];
                    int circleDiameter = (int)(Math.Min(this.Width, this.Height) / 6 * scale);
                    int verticalSpacing = (verticalSpace - circleDiameter * totalRows) / (totalRows - 1);
                    int rowY = marginTop + row * (circleDiameter + verticalSpacing);

                    int totalWidth = cols * circleDiameter + (cols - 1) * circleDiameter / 2;
                    int startX = (this.Width - totalWidth) / 2;

                    for (int col = 0; col < cols; col++)
                    {
                        if (dotIndex >= dotRects.Length) break;
                        int x = startX + col * (circleDiameter + circleDiameter / 2);
                        int y = rowY;

                        var rect = new Rectangle(x, y, circleDiameter, circleDiameter);
                        dotRects[dotIndex] = rect;

                        // 缓存三个子值区域
                        float subHeight = circleDiameter / 3f;
                        for (int i = 0; i < 3; i++)
                        {
                            subValueRects[dotIndex, i] = new RectangleF(
                                x,
                                y + i * subHeight,
                                circleDiameter,
                                subHeight
                            );
                        }

                        dotIndex++;
                    }
                }
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 禁止背景清除，避免闪烁
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 画缓存背景
            if (backgroundCache != null)
            {
                g.DrawImageUnscaled(backgroundCache, Point.Empty);
            }

            if (dotRects == null) return;

            int valueIndex = 0;
            for (int dot = 0; dot < dotRects.Length; dot++)
            {
                Rectangle rect = dotRects[dot];

                // 填充大圆
                using (Brush brush = new SolidBrush(Color.LightSkyBlue))
                {
                    g.FillEllipse(brush, rect);
                }

                // 绘制3个子值
                for (int i = 0; i < 3 && valueIndex < values.Length; i++, valueIndex++)
                {
                    string text = values[valueIndex].ToString("F1");
                    var subRect = subValueRects[dot, i];
                    SizeF textSize = g.MeasureString(text, this.Font);
                    float textX = subRect.X + (subRect.Width - textSize.Width) / 2;
                    float textY = subRect.Y + (subRect.Height - textSize.Height) / 2;
                    g.DrawString(text, this.Font, Brushes.Black, textX, textY);
                }
            }
        }
    }
}
