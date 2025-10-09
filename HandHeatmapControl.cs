using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace fingerPressure
{
    public partial class HandHeatmapControl : UserControl
    {
        private Image handImage;
        private Rectangle[] fingerRects;    // 每根手指矩形区域（旋转前）
        private double[][] fingerValues;    // 5 根手指，每根 9 通道数据
        private float[] fingerAngles;       // 每根手指旋转角度（度）
        //private int danwei = 0;
        //public int Danwei
        //{
        //    get => danwei;
        //    set { danwei = value; }
        //}

        public HandHeatmapControl()
        {
            this.DoubleBuffered = true;
            this.Resize += (_, __) => CalculateFingerRects();

            fingerValues = new double[5][];
            fingerAngles = new float[5]; // 默认为0
            for (int i = 0; i < 5; i++)
                fingerValues[i] = new double[9];

            handImage = Properties.Resources.hand_new1; // 手掌背景图

            // 设置五根手指角度（度），示例：自然展开状态
            /*            fingerAngles[0] = -31f;  // 拇指
                        fingerAngles[1] = -19f;  // 食指
                        fingerAngles[2] = 4f;    // 中指
                        fingerAngles[3] = 18f;   // 无名指
                        fingerAngles[4] = 47f;   // 小指*/
            fingerAngles[0] = 0f;  // 拇指
            fingerAngles[1] = 0f;  // 食指
            fingerAngles[2] = 0f;    // 中指
            fingerAngles[3] = 0f;   // 无名指
            fingerAngles[4] = 0f;   // 小指
        }

        /// <summary>
        /// 设置某根手指的 9 通道值
        /// </summary>
        public void SetFingerValues(int fingerIndex, double[] values)
        {
            if (fingerIndex < 0 || fingerIndex >= 5) return;
            if (values == null || values.Length != 9) return;

            Array.Copy(values, fingerValues[fingerIndex], 9);
            if (fingerRects != null)
                Invalidate(fingerRects[fingerIndex]);
            else
                Invalidate();
        }

        private void CalculateFingerRects()
        {
            if (handImage == null) return;

            int w = this.Width;
            int h = this.Height;

            fingerRects = new Rectangle[5];

            int fingerH = h / 5;   // 手指矩形长度缩短比例

            // 手动设置五根手指的位置和宽度（根据你的背景图）
            fingerRects[0] = new Rectangle(150, 150, 96, 85);      // 拇指
            fingerRects[1] = new Rectangle(267, 105, 102, 85);     // 食指
            fingerRects[2] = new Rectangle(400, 75, 106, 90);     // 中指
            fingerRects[3] = new Rectangle(537, 100, 102, 90);     // 无名指
            fingerRects[4] = new Rectangle(687, 225, 116, 100);    // 小指
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            // 绘制手掌背景
            if (handImage != null)
                g.DrawImage(handImage, this.ClientRectangle);

            // 绘制每根手指热力图
            if (fingerRects == null) CalculateFingerRects();

            for (int i = 0; i < 5; i++)
            {
                DrawFingerHeatmap(g, fingerRects[i], fingerValues[i], fingerAngles[i]);
            }
        }
        private void DrawFingerHeatmap(Graphics g, Rectangle rect, double[] values, float angle)
        {
            if (values == null || values.Length != 9) return;

            // 保存状态
            GraphicsState state = g.Save();

            // 旋转整个 Graphics（热力图 + 手指形状一起旋转）
            float cx = rect.X + rect.Width / 2f;
            float cy = rect.Y + rect.Height;
            g.TranslateTransform(cx, cy);
            g.RotateTransform(angle);
            g.TranslateTransform(-cx, -cy);

            // 创建手指路径（半圆 + 矩形）
            GraphicsPath fingerPath = new GraphicsPath();
            int arcHeight = rect.Width;
            Rectangle halfCircle = new Rectangle(rect.X, rect.Y, rect.Width, arcHeight);
            Rectangle rectangle = new Rectangle(rect.X, rect.Y + arcHeight / 2, rect.Width, rect.Height - arcHeight / 2);
            fingerPath.AddArc(halfCircle, 180, 180);
            fingerPath.AddRectangle(rectangle);

            // Clip 限制在手指形状
            g.SetClip(fingerPath);

            // 绘制热力图
            int gridW = rect.Width;
            int gridH = rect.Height;
            using (Bitmap bmp = new Bitmap(gridW, gridH))
            {
                float cxLocal = gridW / 2f;
                float cyLocal = gridH * 1.0f;
                float a = gridW / 2f;
                float b = gridH * 1.0f;

                PointF[] sensorPos = new PointF[9];
                for (int row = 0; row < 3; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        int idx = row * 3 + col;
                        float nx = (col - 1) / 1.0f;
                        float ny = (row - 2) / 1.0f;
                        sensorPos[idx] = new PointF(cxLocal + nx * a, cyLocal + ny * (b / 2f));
                    }
                }

                for (int x = 0; x < gridW; x++)
                {
                    for (int y = 0; y < gridH; y++)
                    {
                        double val = 0, wsum = 0;
                        for (int i = 0; i < 9; i++)
                        {
                            double dx = x - sensorPos[i].X;
                            double dy = y - sensorPos[i].Y;
                            double dist2 = dx * dx + dy * dy + 1e-6;
                            double w = 1.0 / dist2;
                            val += values[i] * w;
                            wsum += w;
                        }
                        val /= wsum;
                        bmp.SetPixel(x, y, GetColorFromValue(val));
                    }
                }

                // 直接绘制到手指矩形（Graphics 已旋转）
                g.DrawImage(bmp, rect);
            }

            g.ResetClip();
            g.Restore(state);
        }

        /*        private void DrawFingerHeatmap(Graphics g, Rectangle rect, double[] values, float angle)
                {
                    if (values == null || values.Length != 9) return;

                    // 创建半圆+矩形手指形状
                    GraphicsPath fingerPath = new GraphicsPath();

                    // 半圆矩形比宽度稍高，让半圆更圆润
                    int arcHeight = rect.Width;         // 弧的高度
                    Rectangle halfCircle = new Rectangle(rect.X, rect.Y, rect.Width, arcHeight);

                    // 矩形部分从半圆底部开始，延伸到手指底部
                    Rectangle rectangle = new Rectangle(rect.X, rect.Y + arcHeight / 2, rect.Width, rect.Height - arcHeight / 2);

                    fingerPath.AddArc(halfCircle, 180, 180);
                    fingerPath.AddRectangle(rectangle);

                    // 保存状态
                    GraphicsState state = g.Save();

                    // 旋转手指
                    float cx = rect.X + rect.Width / 2f;
                    float cy = rect.Y + rect.Height;
                    g.TranslateTransform(cx, cy);
                    g.RotateTransform(angle);
                    g.TranslateTransform(-cx, -cy);

                    // 创建局部 Bitmap 绘制热力图（9传感器）
                    int gridW = rect.Width;
                    int gridH = rect.Height;
                    using (Bitmap bmp = new Bitmap(gridW, gridH))
                    {
                        float cxLocal = gridW / 2f;
                        float cyLocal = gridH * 1.0f;
                        float a = gridW / 2f;
                        float b = gridH * 1.0f;

                        // 计算9个传感器局部坐标
                        PointF[] sensorPos = new PointF[9];
                        for (int row = 0; row < 3; row++)
                        {
                            for (int col = 0; col < 3; col++)
                            {
                                int idx = row * 3 + col;
                                float nx = (col - 1) / 1.0f;
                                float ny = (row - 2) / 1.0f;
                                sensorPos[idx] = new PointF(cxLocal + nx * a, cyLocal + ny * (b / 2f));
                            }
                        }

                        // 遍历 Bitmap 每个像素
                        for (int x = 0; x < gridW; x++)
                        {
                            for (int y = 0; y < gridH; y++)
                            {
                                double norm = ((x - cxLocal) * (x - cxLocal)) / (a * a) +
                                              ((y - cyLocal) * (y - cyLocal)) / (b * b);
                                if (norm > 1.0) continue;

                                double val = 0, wsum = 0;
                                for (int i = 0; i < 9; i++)
                                {
                                    double dx = x - sensorPos[i].X;
                                    double dy = y - sensorPos[i].Y;
                                    double dist2 = dx * dx + dy * dy + 1e-6;
                                    double w = 1.0 / dist2;
                                    val += values[i] * w;
                                    wsum += w;
                                }
                                val /= wsum;

                                bmp.SetPixel(x, y, GetColorFromValue(val));
                            }
                        }

                        // 将 Bitmap 限制在手指形状内绘制
                        g.SetClip(fingerPath);
                        g.DrawImage(bmp, rect);
                        g.ResetClip();
                    }

                    // 恢复状态
                    g.Restore(state);
                }*/

        private Color GetColorFromValue(double value)
        {
            double maxAbs = 6000;
            //if (danwei == 1) maxAbs = 1.5;
            //if (danwei == 2) maxAbs = 6000;

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
        /*        private Color GetColorFromValue(double value)
                {
                    double maxAbs = 10000;

                    if (value < -maxAbs) value = -maxAbs;
                    if (value > maxAbs) value = maxAbs;

                    double r = 0, g = 0, b = 0;
                    int a = 0;
                    double ratio = 0;

                    if (value < 0)
                    {
                        ratio = (value + maxAbs) / maxAbs;
                        if (ratio < 0.5) { r = 0; g = ratio * 2; b = 1; }
                        else { r = 0; g = 1; b = 2 * (1 - ratio); }
                    }
                    else
                    {
                        ratio = value / maxAbs;
                        if (ratio < 0.5) { r = ratio * 2; g = 1; b = 0; }
                        else { r = 1; g = 2 * (1 - ratio); b = 0; }
                    }

                    a = (int)(50 + (255 - 50) * ratio);
                    return Color.FromArgb(
                        a,
                        (int)(r * 255),
                        (int)(g * 255),
                        (int)(b * 255));
                }*/
    }
}
