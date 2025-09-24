/*using System;
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
                    //g.DrawEllipse(Pens.Black, dotRect);

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
*/
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class DoubleBufferedPanel : Panel
    {
        private double[] values = new double[8]; // 8通道数据
        private Rectangle[] dotRects;            // 点阵矩形缓存
        private Bitmap backgroundCache;          // 背景缓存
        private float fontHeight;                // 字体高度缓存
        private bool needsRefresh;               // 节流标记

        public DoubleBufferedPanel()
        {
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.AllPaintingInWmPaint |
                          ControlStyles.UserPaint |
                          ControlStyles.OptimizedDoubleBuffer, true);
            this.UpdateStyles();
            //this.BackColor = Color.White;
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
            dotRects = new Rectangle[values.Length+1];

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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 先画缓存的背景
            if (backgroundCache != null)
            {
                g.DrawImageUnscaled(backgroundCache, Point.Empty);
            }

            // 再绘制点值
            if (dotRects == null) return;

            for (int i = 0; i < values.Length && i < dotRects.Length; i++)
            {
                var rect = dotRects[i];
                double value = values[i];

                // 填充圆点
                using (Brush brush = new SolidBrush(Color.LightSkyBlue))
                {
                    g.FillEllipse(brush, rect);
                }

                // 数值绘制
                string text = value.ToString("F0"); // 固定格式，避免字符串过长
                SizeF textSize = g.MeasureString(text, this.Font);
                g.DrawString(
                    text,
                    this.Font,
                    Brushes.Black,
                    rect.X + (rect.Width - textSize.Width) / 2,
                    rect.Y + (rect.Height - textSize.Height) / 2
                );
            }
        }
    }
}
