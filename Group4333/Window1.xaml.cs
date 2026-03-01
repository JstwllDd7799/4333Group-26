using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ClosedXML.Excel;
using Microsoft.Win32;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;


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
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Excel files (*.xlsx)|*.xlsx";

            if (dialog.ShowDialog() != true)
                return;

            var clients = LoadFromExcel(dialog.FileName);
            SaveToDatabase(clients);

            txtStatus.Text = $"Импортировано записей: {clients.Count}";
        }


        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            var data = LoadFromDatabase();

            var grouped = data.GroupBy(x => x.Street);

            SaveGroupedExcel(grouped);

            txtStatus.Text = "Экспорт завершён";
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
                    cmd.Parameters.AddWithValue("@email", c.Email);
                    cmd.Parameters.AddWithValue("@street", c.Street);

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
                    "SELECT ClientCode, FullName, Email, Street FROM Clients",
                    conn);

                SqlDataReader reader = cmd.ExecuteReader();

                while (reader.Read())
                {
                    list.Add(new Client
                    {
                        ClientCode = (int)reader["ClientCode"],
                        FullName = reader["FullName"].ToString(),
                        Email = reader["Email"].ToString(),
                        Street = reader["Street"].ToString()
                    });
                }
            }

            return list;
        }

        private void SaveGroupedExcel(IEnumerable<IGrouping<string, Client>> groups)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Excel (*.xlsx)|*.xlsx";
            dialog.FileName = "Ludogovskaya4333.xlsx";

            if (dialog.ShowDialog() != true)
                return;

            using (var workbook = new XLWorkbook())
            {
                bool hasSheets = false;

                foreach (var group in groups)
                {
                    string sheetName = string.IsNullOrWhiteSpace(group.Key)
                        ? "Без улицы"
                        : group.Key;

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

    }
}
