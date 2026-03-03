using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Win32;
using System.Configuration;

namespace Group4333
{
    /// <summary>
    /// Логика взаимодействия для Window1.xaml
    /// </summary>
    public partial class Window1 : Window
    {
        public Window1()
        {
            InitializeComponent();
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "Excel files (*.xlsx)|*.xlsx";

                if (dialog.ShowDialog() != true)
                    return;

                var clients = LoadFromExcel(dialog.FileName);
                SaveToDatabase(clients);

                txtStatus.Text = $"Импортировано записей из Excel: {clients.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте из Excel: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnImportJson_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "JSON files (*.json)|*.json";

                if (dialog.ShowDialog() != true)
                    return;

                var clients = LoadFromJson(dialog.FileName);
                SaveToDatabase(clients);

                txtStatus.Text = $"Импортировано записей из JSON: {clients.Count}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при импорте из JSON: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExportWord_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = LoadFromDatabase();

                var grouped = data.GroupBy(x => x.Street ?? "Без улицы")
                                 .OrderBy(g => g.Key);

                SaveGroupedToWord(grouped);

                txtStatus.Text = "Экспорт в Word завершён";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Word: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var data = LoadFromDatabase();

                var grouped = data.GroupBy(x => x.Street ?? "Без улицы");

                SaveGroupedExcel(grouped);

                txtStatus.Text = "Экспорт в Excel завершён";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка при экспорте в Excel: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private List<Client> LoadFromJson(string path)
        {
            string jsonContent = File.ReadAllText(path);

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            var clients = JsonSerializer.Deserialize<List<Client>>(jsonContent, options);

            if (clients == null)
                return new List<Client>();

            return clients;
        }

        private List<Client> LoadFromExcel(string path)
        {
            List<Client> list = new List<Client>();

            using (var workbook = new XLWorkbook(path))
            {
                var sheet = workbook.Worksheet(1);
                var rows = sheet.RangeUsed().RowsUsed().Skip(1);

                foreach (var row in rows)
                {
                    list.Add(new Client
                    {
                        ClientCode = row.Cell(1).GetValue<int>(),
                        FullName = row.Cell(2).GetValue<string>(),
                        Email = row.Cell(3).GetValue<string>(),
                        Street = row.Cell(4).GetValue<string>()
                    });
                }
            }

            return list;
        }

        private void SaveToDatabase(List<Client> clients)
        {
            string connStr =
                ConfigurationManager.ConnectionStrings["DbConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                foreach (var c in clients)
                {
                    SqlCommand cmd = new SqlCommand(@"
MERGE Clients AS target
USING (SELECT @code AS ClientCode) AS source
ON target.ClientCode = source.ClientCode

WHEN MATCHED THEN
    UPDATE SET
        FullName = @name,
        Email = @email,
        Street = @street

WHEN NOT MATCHED THEN
    INSERT (ClientCode, FullName, Email, Street)
    VALUES (@code, @name, @email, @street);", conn);

                    cmd.Parameters.AddWithValue("@code", c.ClientCode);
                    cmd.Parameters.AddWithValue("@name", c.FullName);
                    cmd.Parameters.AddWithValue("@email", c.Email ?? "");
                    cmd.Parameters.AddWithValue("@street", c.Street ?? "");

                    cmd.ExecuteNonQuery();
                }
            }
        }

        private List<Client> LoadFromDatabase()
        {
            List<Client> list = new List<Client>();

            string connStr =
                ConfigurationManager.ConnectionStrings["DbConnection"].ConnectionString;

            using (SqlConnection conn = new SqlConnection(connStr))
            {
                conn.Open();

                SqlCommand cmd = new SqlCommand(
                    "SELECT ClientCode, FullName, Email, Street FROM Clients ORDER BY FullName",
                    conn);

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new Client
                    {
                        ClientCode = (int)reader["ClientCode"],
                        FullName = reader["FullName"].ToString(),
                        Email = reader["Email"]?.ToString(),
                        Street = reader["Street"]?.ToString()
                    });
                }
            }

            return list;
        }

        private void SaveGroupedExcel(IEnumerable<IGrouping<string, Client>> groups)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Excel (*.xlsx)|*.xlsx";
            dialog.FileName = $"Ludogovskaya4333_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            if (dialog.ShowDialog() != true)
                return;

            using (var workbook = new XLWorkbook())
            {
                bool hasSheets = false;

                foreach (var group in groups)
                {
                    string sheetName = string.IsNullOrWhiteSpace(group.Key)
                        ? "Без улицы"
                        : group.Key.Length > 30 ? group.Key.Substring(0, 30) : group.Key;

                    var sheet = workbook.Worksheets.Add(sheetName);

                    sheet.Cell(1, 1).Value = "Код клиента";
                    sheet.Cell(1, 2).Value = "ФИО";
                    sheet.Cell(1, 3).Value = "E-mail";

                    int row = 2;

                    foreach (var client in group.OrderBy(c => c.FullName))
                    {
                        sheet.Cell(row, 1).Value = client.ClientCode;
                        sheet.Cell(row, 2).Value = client.FullName;
                        sheet.Cell(row, 3).Value = client.Email;
                        row++;
                    }

                    sheet.Columns().AdjustToContents();
                    hasSheets = true;
                }

                if (!hasSheets)
                {
                    var sheet = workbook.Worksheets.Add("Данные");
                    sheet.Cell(1, 1).Value = "Код клиента";
                    sheet.Cell(1, 2).Value = "ФИО";
                    sheet.Cell(1, 3).Value = "E-mail";
                }

                workbook.SaveAs(dialog.FileName);
            }
        }

        private void SaveGroupedToWord(IEnumerable<IGrouping<string, Client>> groups)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Word Document (*.docx)|*.docx";
            dialog.FileName = $"Ludogovskaya4333_{DateTime.Now:yyyyMMdd_HHmmss}.docx";

            if (dialog.ShowDialog() != true)
                return;

            using (WordprocessingDocument wordDocument = WordprocessingDocument.Create(dialog.FileName, WordprocessingDocumentType.Document))
            {
                MainDocumentPart mainPart = wordDocument.AddMainDocumentPart();
                mainPart.Document = new Document();
                Body body = mainPart.Document.AppendChild(new Body());

                bool hasData = false;

                foreach (var group in groups)
                {
                    Paragraph headingPara = body.AppendChild(new Paragraph());
                    Run headingRun = headingPara.AppendChild(new Run());
                    headingRun.AppendChild(new Text($"Улица: {group.Key}"));
                    headingRun.RunProperties = new RunProperties();
                    headingRun.RunProperties.AppendChild(new Bold());
                    headingRun.RunProperties.AppendChild(new FontSize() { Val = "28" });

                    body.AppendChild(new Paragraph());

                    Table table = new Table();

                    TableProperties tableProperties = new TableProperties(
                        new TableBorders(
                            new TopBorder() { Val = BorderValues.Single, Size = 6 },
                            new BottomBorder() { Val = BorderValues.Single, Size = 6 },
                            new LeftBorder() { Val = BorderValues.Single, Size = 6 },
                            new RightBorder() { Val = BorderValues.Single, Size = 6 },
                            new InsideHorizontalBorder() { Val = BorderValues.Single, Size = 6 },
                            new InsideVerticalBorder() { Val = BorderValues.Single, Size = 6 }
                        )
                    );
                    table.AppendChild(tableProperties);

                    TableRow headerRow = new TableRow();

                    headerRow.AppendChild(CreateTableCell("Код клиента", true));
                    headerRow.AppendChild(CreateTableCell("ФИО", true));
                    headerRow.AppendChild(CreateTableCell("E-mail", true));

                    table.AppendChild(headerRow);

                    foreach (var client in group.OrderBy(c => c.FullName))
                    {
                        TableRow dataRow = new TableRow();

                        dataRow.AppendChild(CreateTableCell(client.ClientCode.ToString(), false));
                        dataRow.AppendChild(CreateTableCell(client.FullName, false));
                        dataRow.AppendChild(CreateTableCell(client.Email ?? "", false));

                        table.AppendChild(dataRow);
                    }

                    body.AppendChild(table);

                    if (group != groups.Last())
                    {
                        Paragraph pageBreakPara = body.AppendChild(new Paragraph());
                        Run pageBreakRun = pageBreakPara.AppendChild(new Run());
                        pageBreakRun.AppendChild(new Break() { Type = BreakValues.Page });
                    }

                    hasData = true;
                }

                if (!hasData)
                {
                    Paragraph noDataPara = body.AppendChild(new Paragraph());
                    Run noDataRun = noDataPara.AppendChild(new Run());
                    noDataRun.AppendChild(new Text("Нет данных для отображения"));
                }

                mainPart.Document.Save();
            }
        }

        private TableCell CreateTableCell(string text, bool isHeader)
        {
            TableCell cell = new TableCell();

            Paragraph paragraph = new Paragraph();
            Run run = new Run();
            run.AppendChild(new Text(text));

            if (isHeader)
            {
                run.RunProperties = new RunProperties();
                run.RunProperties.AppendChild(new Bold());
            }

            paragraph.AppendChild(run);
            cell.AppendChild(paragraph);

            TableCellProperties cellProperties = new TableCellProperties(
                new TableCellBorders(
                    new TopBorder() { Val = BorderValues.Single, Size = 6 },
                    new BottomBorder() { Val = BorderValues.Single, Size = 6 },
                    new LeftBorder() { Val = BorderValues.Single, Size = 6 },
                    new RightBorder() { Val = BorderValues.Single, Size = 6 }
                )
            );
            cell.AppendChild(cellProperties);

            return cell;
        }
    }

}