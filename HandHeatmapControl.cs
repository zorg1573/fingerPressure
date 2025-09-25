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

        public HandHeatmapControl()
        {
            this.DoubleBuffered = true;
            this.Resize += (_, __) => CalculateFingerRects();

            fingerValues = new double[5][];
            fingerAngles = new float[5]; // 默认为0
            for (int i = 0; i < 5; i++)
                fingerValues[i] = new double[9];

            handImage = Properties.Resources.handpng; // 手掌背景图

            // 设置五根手指角度（度），示例：自然展开状态
            fingerAngles[0] = -31f;  // 拇指
            fingerAngles[1] = -19f;  // 食指
            fingerAngles[2] = 4f;    // 中指
            fingerAngles[3] = 18f;   // 无名指
            fingerAngles[4] = 47f;   // 小指
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
            fingerRects[0] = new Rectangle(115, 110, 80, 80);      // 拇指
            fingerRects[1] = new Rectangle(220, 25, 110, 86);     // 食指
            fingerRects[2] = new Rectangle(415, 0, 110, 100);     // 中指
            fingerRects[3] = new Rectangle(572, 40, 100, 100);     // 无名指
            fingerRects[4] = new Rectangle(720, 230, 90, 100);    // 小指
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
        }

        private Color GetColorFromValue(double value)
        {
            double maxAbs = 10000;
            if (value < -maxAbs) value = -maxAbs;
            if (value > maxAbs) value = maxAbs;

            double r = 0, g = 0, b = 0;

            if (value < 0)
            {
                double ratio = (value + maxAbs) / maxAbs;
                if (ratio < 0.5)
                {
                    g = ratio * 2;
                    b = 1;
                }
                else
                {
                    g = 1;
                    b = 2 * (1 - ratio);
                }
            }
            else
            {
                double ratio = value / maxAbs;
                if (ratio < 0.5)
                {
                    r = ratio * 2;
                    g = 1;
                }
                else
                {
                    r = 1;
                    g = 2 * (1 - ratio);
                }
            }

            return Color.FromArgb((int)(r * 255), (int)(g * 255), (int)(b * 255));
        }
    }
}
