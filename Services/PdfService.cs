using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using DailyLogSystem.Models;

namespace DailyLogSystem.Services
{
    public interface IPdfService
    {
        Task<byte[]> GenerateMonthlyReportAsync(Employee employee, List<TodayRecord> logs, int month, int year);
        Task<byte[]> GenerateDailyReportAsync(Employee employee, TodayRecord record);
        Task<byte[]> GenerateMonthlyReportAsync(List<TodayRecord> logs, int month, int year);

    }

    public class PdfService : IPdfService
    {

        public Task<byte[]> GenerateMonthlyReportAsync(Employee employee, List<TodayRecord> logs, int month, int year)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeMonthlyContent(c, employee, logs, month, year));
                    page.Footer().AlignCenter().Element(c =>
                    {
                        c.Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                    });
                });
            });

            return Task.FromResult(doc.GeneratePdf());
        }

        // employee report
        public Task<byte[]> GenerateDailyReportAsync(Employee employee, TodayRecord record)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeDailyContent(c, employee, record));
                    page.Footer().AlignCenter().Element(c =>
                    {
                        c.Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                    });
                });
            });

            return Task.FromResult(doc.GeneratePdf());
        }

        // admin report 
        public Task<byte[]> GenerateMonthlyReportAsync(List<TodayRecord> logs, int month, int year)
        {
            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeAdminMonthlyContent(c, logs, month, year));
                    page.Footer().AlignCenter().Element(c =>
                    {
                        c.Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}");
                    });
                });
            });

            return Task.FromResult(doc.GeneratePdf());
        }

       

        void ComposeHeader(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("DailyLogSystem").SemiBold().FontSize(16);
                col.Item().Text("Attendance Report").FontSize(12);
                col.Item().Text(DateTime.UtcNow.ToString("yyyy-MM-dd")).FontSize(10);
                col.Item().Height(1).Background(Colors.Grey.Lighten2);
            });
        }

        void ComposeMonthlyContent(IContainer container, Employee employee, List<TodayRecord> logs, int month, int year)
        {
            container.Column(col =>
            {
                col.Item().Text($"{employee.FullName} — {employee.EmployeeId}").Bold();
                col.Item().Text($"Monthly Report: {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}");

                var totalWorked = logs.Count(l => l.TimeIn.HasValue);
                var totalDays = DateTime.DaysInMonth(year, month);
                var totalHours = logs.Where(l => l.TimeIn.HasValue && l.TimeOut.HasValue)
                                     .Select(l => (l.TimeOut!.Value - l.TimeIn!.Value).TotalHours).Sum();

                var overtime = logs.Where(l => l.TimeOut.HasValue)
                                   .Select(l =>
                                   {
                                       var officeEnd = l.Date.Date.AddHours(17);
                                       return l.TimeOut!.Value > officeEnd ? (l.TimeOut!.Value - officeEnd).TotalHours : 0.0;
                                   }).Sum();

                col.Item().Text($"Worked days: {totalWorked}/{totalDays}");
                col.Item().Text($"Total hours: {totalHours:0.00} hrs");
                col.Item().Text($"Overtime: {overtime:0.00} hrs");
                col.Item().Text($"Attendance rate: {(totalWorked / (double)totalDays * 100):0.00}%");

                col.Item().Height(1).Background(Colors.Grey.Lighten2);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80); 
                        columns.ConstantColumn(80); 
                        columns.ConstantColumn(80); 
                        columns.ConstantColumn(70); 
                        columns.RelativeColumn();   
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Date");
                        header.Cell().Text("Time In");
                        header.Cell().Text("Time Out");
                        header.Cell().Text("Hours");
                        header.Cell().Text("Status");
                    });

                    foreach (var l in logs)
                    {
                        table.Cell().Text(l.Date.ToString("yyyy-MM-dd"));
                        table.Cell().Text(l.TimeIn?.ToString("hh:mm tt") ?? "-");
                        table.Cell().Text(l.TimeOut?.ToString("hh:mm tt") ?? "-");
                        table.Cell().Text(l.TotalHours ?? "-");
                        table.Cell().Text(l.Status ?? "-");
                    }
                });
            });
        }

        void ComposeDailyContent(IContainer container, Employee employee, TodayRecord record)
        {
            container.Column(col =>
            {
                col.Item().Text($"{employee.FullName} — {employee.EmployeeId}").Bold();
                col.Item().Text($"Daily Report: {record.Date:yyyy-MM-dd}");
                col.Item().Text($"Time In: {record.TimeIn?.ToString("hh:mm tt") ?? "No Time In"}");
                col.Item().Text($"Time Out: {record.TimeOut?.ToString("hh:mm tt") ?? "No Time Out"}");
                col.Item().Text($"Total Hours: {record.TotalHours ?? "0"}");
                col.Item().Text($"Status: {record.Status ?? "-"}");
                col.Item().Text("Notes: This report is autogenerated.");
            });
        }

        void ComposeAdminMonthlyContent(IContainer container, List<TodayRecord> logs, int month, int year)
        {
            container.Column(col =>
            {
                col.Item().Text($"Admin Report — {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}").Bold();

                var totalWorked = logs.Count(l => l.TimeIn.HasValue);
                var totalDays = DateTime.DaysInMonth(year, month);
                var totalHours = logs.Where(l => l.TimeIn.HasValue && l.TimeOut.HasValue)
                                     .Select(l => (l.TimeOut!.Value - l.TimeIn!.Value).TotalHours).Sum();

                var overtime = logs.Where(l => l.TimeOut.HasValue)
                                   .Select(l =>
                                   {
                                       var officeEnd = l.Date.Date.AddHours(17);
                                       return l.TimeOut!.Value > officeEnd ? (l.TimeOut!.Value - officeEnd).TotalHours : 0.0;
                                   }).Sum();

                col.Item().Text($"Worked days: {totalWorked}/{totalDays}");
                col.Item().Text($"Total hours: {totalHours:0.00} hrs");
                col.Item().Text($"Overtime: {overtime:0.00} hrs");
                col.Item().Text($"Attendance rate: {(totalWorked / (double)totalDays * 100):0.00}%");

                col.Item().Height(1).Background(Colors.Grey.Lighten2);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(80); 
                        columns.ConstantColumn(100); 
                        columns.ConstantColumn(80); 
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(70); 
                        columns.RelativeColumn();   
                    });

                    table.Header(header =>
                    {
                        header.Cell().Text("Date");
                        header.Cell().Text("Employee");
                        header.Cell().Text("Time In");
                        header.Cell().Text("Time Out");
                        header.Cell().Text("Hours");
                        header.Cell().Text("Status");
                    });

                    foreach (var l in logs)
                    {
                        table.Cell().Text(l.Date.ToString("yyyy-MM-dd"));
                        table.Cell().Text(l.EmployeeId);
                        table.Cell().Text(l.TimeIn?.ToString("hh:mm tt") ?? "-");
                        table.Cell().Text(l.TimeOut?.ToString("hh:mm tt") ?? "-");
                        table.Cell().Text(l.TotalHours ?? "-");
                        table.Cell().Text(l.Status ?? "-");
                    }
                });
            });
        }
    }
}
