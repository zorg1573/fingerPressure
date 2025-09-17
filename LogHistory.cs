using System.Data;
using System.Text.Json;
using ClosedXML.Excel;
using MetroFramework.Forms;
using fingerPressure.MODEL;

namespace fingerPressure
{
    public partial class LogHistory : MetroForm
    {
        private readonly string logFilePath = "logs.json";
        private List<LogConfig> allEntries = new List<LogConfig>();
        public LogHistory()
        {
            InitializeComponent();
            LoadLogEntries();
            dateTimePicker1.Value = DateTime.Today.AddDays(-7); // 最近7天
            dateTimePicker2.Value = DateTime.Now;
            this.ControlBox = false;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
        }
        private void LoadLogEntries()
        {
            allEntries.Clear();

            if (File.Exists(logFilePath))
            {
                foreach (var line in File.ReadLines(logFilePath))
                {
                    try
                    {
                        var entry = JsonSerializer.Deserialize<LogConfig>(line);
                        if (entry != null)
                            allEntries.Add(entry);
                    }
                    catch { /* 忽略格式错误行 */ }
                }
            }
            ShowEntries(allEntries);
        }
        private void ShowEntries(List<LogConfig> entries)
        {
            dataGridViewLogs.Rows.Clear();

            foreach (var log in entries)
            {
                dataGridViewLogs.Rows.Add(log.LogTime.ToString("yyyy-MM-dd"), log.MilTime, log.Message);
            }
        }
        private void button1_Click(object sender, EventArgs e)
        {
            DateTime start = dateTimePicker1.Value.Date;
            DateTime end = dateTimePicker2.Value.Date.AddDays(1).AddSeconds(-1); // 包含整天

            var filtered = allEntries
                .Where(e => e.LogTime >= start && e.LogTime <= end)
                .ToList();

            ShowEntries(filtered);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // 获取当前时间并格式化为文件名的一部分
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string defaultFileName = $" 测量记录_{timestamp}.xlsx";

            SaveFileDialog saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Files|*.xlsx",
                Title = "Save an Excel File",
                FileName = defaultFileName // 设置默认文件名
            };
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                string filePath = saveFileDialog.FileName;
                ExportToExcel(filePath);
                MessageBox.Show("Excel 导出成功");
            }
        }
        private void ExportToExcel(string filePath)
        {
            try
            {
                using (var workbook = new XLWorkbook())
                {
                    var worksheet = workbook.Worksheets.Add("日志记录");

                    // 添加表头
                    worksheet.Cell(1, 1).Value = "日期";
                    worksheet.Cell(1, 2).Value = "精确时间";
                    worksheet.Cell(1, 3).Value = "消息";

                    // 写入 DataGridView 数据
                    int row = 3;
                    foreach (DataGridViewRow dgvRow in dataGridViewLogs.Rows)
                    {
                        if (dgvRow.IsNewRow) continue;

                        worksheet.Cell(row, 1).Value = dgvRow.Cells[0].Value?.ToString();
                        worksheet.Cell(row, 2).Value = dgvRow.Cells[1].Value?.ToString();
                        worksheet.Cell(row, 3).Value = dgvRow.Cells[2].Value?.ToString();
                        row++;
                    }

                    // 自动调整列宽
                    worksheet.Columns().AdjustToContents();

                    // 保存
                    workbook.SaveAs(filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("导出 Excel 失败：" + ex.Message);
            }
        }

        // pictureBox4: 关闭
        private void pictureBox4_Click(object sender, EventArgs e)
        {
            this.Close(); // 或 Application.Exit();
        }
    }
}
