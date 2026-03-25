using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesDashboardAPI.Data;
using SalesDashboardAPI.Models;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SalesDashboardAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class SalesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SalesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> UploadCsv(IFormFile file)
        {
            // 1. Basic Validation
            if (file == null || file.Length == 0)
                return BadRequest("File is empty or not provided.");

            // 2. Extension Check
            var extension = Path.GetExtension(file.FileName).ToLower();
            if (extension != ".csv")
                return BadRequest("Invalid file type. Please upload a .csv file.");

            // 3. File Size Limit (e.g., 10MB)
            if (file.Length > 10 * 1024 * 1024)
                return BadRequest("File size exceeds 10MB limit.");

            // Fetch existing OrderIds to skip duplicates
            var existingOrderIds = _context.Sales.Select(s => s.OrderId).Distinct().ToHashSet();
            var salesList = new List<Sales>();
            int skippedRows = 0;
            int duplicateRows = 0;
            int processedRows = 0;

            try
            {
                using (var reader = new StreamReader(file.OpenReadStream()))
                {
                    // 4. Header Validation
                    var headerLine = await reader.ReadLineAsync();
                    if (string.IsNullOrWhiteSpace(headerLine))
                        return BadRequest("CSV file is empty or header is missing.");

                    var headers = headerLine.Split(',').Select(h => h.Trim().ToLower()).ToList();
                        bool hasCustomer = headers.Contains("customer");
                        bool hasProduct = headers.Contains("product");

                    if (!hasCustomer && !hasProduct)
                    {
                        return BadRequest("CSV must contain either 'Customer' or 'Product' column.");
                    }

                    // Required columns check (case insensitive)
                   // var requiredHeaders = new[] { "orderid", "orderdate", "customer", "category", "region", "quantity", "price" };
                   var requiredHeaders = new[] { "orderid", "orderdate", "category", "region", "quantity", "price" };
                    if (!requiredHeaders.All(req => headers.Contains(req)))
                    {
                        return BadRequest("Invalid CSV structure. Required headers: OrderID, OrderDate, (Customer or Product), Category, Region, Quantity, Price");
                    }

                    // Map header indices for flexibility
                     int idxId = headers.IndexOf("orderid");
                     int idxDate = headers.IndexOf("orderdate");

                   // ✅ FIXED LINE (IMPORTANT)
                   int idxCust = headers.Contains("customer") 
                   ? headers.IndexOf("customer") 
                   : headers.IndexOf("product");

                   int idxCat = headers.IndexOf("category");
                   int idxReg = headers.IndexOf("region");
                   int idxQty = headers.IndexOf("quantity");
                   int idxPrice = headers.IndexOf("price");

                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        processedRows++;

                        if (string.IsNullOrWhiteSpace(line))
                        {
                            skippedRows++;
                            continue;
                        }

                        // Robust parsing for commas inside quotes (e.g., "John, Smith")
                        var values = ParseCsvLine(line);

                        if (values.Count < requiredHeaders.Length)
                        {
                            skippedRows++;
                            continue;
                        }

                        var rawOrderId = values[idxId]?.Trim();

                        // Skip duplicates
                        if (existingOrderIds.Contains(rawOrderId))
                        {
                            duplicateRows++;
                            continue;
                        }

                        // Data validation
                        if (!DateTime.TryParse(values[idxDate], out DateTime orderDate) ||
                            !int.TryParse(values[idxQty], out int quantity) ||
                            !decimal.TryParse(values[idxPrice], NumberStyles.Any, CultureInfo.InvariantCulture, out decimal price))
                        {
                            skippedRows++;
                            continue;
                        }

                        var region = values[idxReg]?.Trim();
                        var category = values[idxCat]?.Trim();

                        if (string.IsNullOrEmpty(region) || string.IsNullOrEmpty(category))
                        {
                            skippedRows++;
                            continue;
                        }

                        var sale = new Sales
                        {
                            OrderId = rawOrderId,
                            OrderDate = orderDate,
                            Customer = values[idxCust]?.Trim(),
                            Product = values[idxCust]?.Trim(),
                            Region = region,
                            Category = category,
                            Quantity = quantity,
                            Price = price,
                            TotalSales = quantity * price
                        };

                        salesList.Add(sale);
                        existingOrderIds.Add(rawOrderId);
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }

            if (!salesList.Any())
                return Ok(new { message = "No new valid data found in file", duplicate = duplicateRows, skipped = skippedRows });

            await _context.Sales.AddRangeAsync(salesList);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Data updated successfully",
                inserted = salesList.Count,
                duplicate = duplicateRows,
                skipped = skippedRows
            });
        }

        // Helper to handle CSV lines with quotes: "Field, with comma", Field2
        private List<string> ParseCsvLine(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '\"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }
            result.Add(current.ToString());
            return result;
        }

        // 🔥 FILTER BASE QUERY (COMMON)
        private IQueryable<Sales> ApplyFilters(string? month, string? region)
        {
            var query = _context.Sales.AsQueryable();

            if (!string.IsNullOrEmpty(region))
                query = query.Where(x => x.Region.ToLower() == region.ToLower());
           // query = query.Where(x => x.Region.Trim().ToLower() == region.Trim().ToLower());

            if (!string.IsNullOrEmpty(month) && DateTime.TryParse(month + "-01", out var date))
            {
                query = query.Where(x => x.OrderDate.Month == date.Month && x.OrderDate.Year == date.Year);
            }

            return query;
        }

        // 🔥 Dashboard Metrics (FILTERED)
        [HttpGet("dashboard-metrics")]
        public IActionResult GetDashboardMetrics(string? month, string? region)
        {
            var data = ApplyFilters(month, region);

            var totalRevenue = data.Sum(s => s.TotalSales);
            // var totalOrders = data.Count();
            var totalOrders = data.Sum(s => s.Quantity);

            var topRegion = data
                .GroupBy(s => s.Region)
                .OrderByDescending(g => g.Sum(x => x.TotalSales))
                .Select(g => g.Key)
                .FirstOrDefault();

            var topCategory = data
                .GroupBy(s => s.Category)
                .OrderByDescending(g => g.Sum(x => x.TotalSales))
                .Select(g => g.Key)
                .FirstOrDefault();

            var topProduct = data
                .GroupBy(s => s.Product)
                .OrderByDescending(g => g.Sum(x => x.TotalSales))
                .Select(g => g.Key)
                .FirstOrDefault();

            return Ok(new
            {
                totalRevenue,
                totalOrders,
                topRegion,
                topCategory,
                topProduct
            });
        }

        // 🔥 Monthly Sales (FILTERED)
        [HttpGet("monthly-sales")]
        public IActionResult GetMonthlySales(string? month, string? region)
        {
            var data = ApplyFilters(month, region);

            var monthlyData = data
                .AsEnumerable()
                .GroupBy(s => new { s.OrderDate.Year, s.OrderDate.Month })
                .Select(g => new
                {
                    year = g.Key.Year,
                    month = g.Key.Month,
                    totalSales = g.Sum(x => x.TotalSales)
                })
                .OrderBy(x => x.year)
                .ThenBy(x => x.month)
                .ToList();

            return Ok(monthlyData);
        }

        // 🔥 Region (FILTERED)
        [HttpGet("sales-by-region")]
        public IActionResult GetSalesByRegion(string? month, string? region)
        {
            var data = ApplyFilters(month, region);

            var result = data
                .GroupBy(s => s.Region)
                .Select(g => new
                {
                    region = g.Key,
                    totalSales = g.Sum(x => x.TotalSales)
                })
                .OrderByDescending(x => x.totalSales)
                .ToList();

            return Ok(result);
        }

        // 🔥 Category (FILTERED)
        [HttpGet("sales-by-category")]
        public IActionResult GetSalesByCategory(string? month, string? region)
        {
            var data = ApplyFilters(month, region);

            var result = data
                .GroupBy(s => s.Category)
                .Select(g => new
                {
                    category = g.Key,
                    totalSales = g.Sum(x => x.TotalSales)
                })
                .OrderByDescending(x => x.totalSales)
                .ToList();

            return Ok(result);
        }

        // 🔥 Forecast (SIMPLE TREND)
        [HttpGet("forecast")]
        public IActionResult GetForecast(string? month, string? region)
        {
            var data = ApplyFilters(month, region)
                .AsEnumerable()
                .GroupBy(s => new { s.OrderDate.Year, s.OrderDate.Month })
                .Select(g => new
                {
                    totalSales = g.Sum(x => x.TotalSales),
                    date = new DateTime(g.Key.Year, g.Key.Month, 1)
                })
                .OrderBy(x => x.date)
                .ToList();

            if (data.Count < 2)
                return Ok(new List<object>());

            // 🔥 Better Growth Calculation
            decimal totalGrowth = 0;
            int count = 0;

            for (int i = 1; i < data.Count; i++)
            {
                var prev = data[i - 1].totalSales;
                var curr = data[i].totalSales;

                if (prev != 0)
                {
                    totalGrowth += (curr - prev) / prev;
                    count++;
                }
            }

            var avgGrowth = count > 0 ? totalGrowth / count : 0;
            var lastSales = data.Last().totalSales;

            var forecast = new List<object>();

            for (int i = 1; i <= 3; i++)
            {
                lastSales *= (1 + avgGrowth);

                forecast.Add(new
                {
                    month = data.Last().date.AddMonths(i).ToString("MMM"),
                    totalSales = Math.Round(lastSales, 2)
                });
            }

            return Ok(forecast);
        }



        // 🔥 Correlation Matrix (Simple Pearson)
        [HttpGet("correlation")]
        public IActionResult GetCorrelationData(string? month, string? region)
        {
            var data = ApplyFilters(month, region).ToList();

            if (data.Count < 2)
                return Ok(new List<object>());

            // Encode Regions numerically
            var regions = data.Select(s => s.Region).Distinct().ToList();
            var regionMap = regions.Select((r, i) => new { r, i }).ToDictionary(x => x.r, x => (double)x.i);

            var variables = new List<string> { "TotalSales", "Price", "Quantity", "Region" };
            var matrix = new List<object>();

            foreach (var var1 in variables)
            {
                foreach (var var2 in variables)
                {
                    double correlation = CalculateCorrelation(data, var1, var2, regionMap);
                    matrix.Add(new
                    {
                        x = var1,
                        y = var2,
                        v = Math.Round(correlation, 2)
                    });
                }
            }

            return Ok(matrix);
        }

        private double CalculateCorrelation(List<Sales> data, string v1, string v2, Dictionary<string, double> regionMap)
        {
            var xValues = GetValues(data, v1, regionMap);
            var yValues = GetValues(data, v2, regionMap);

            if (xValues.Count != yValues.Count || xValues.Count == 0) return 0;

            double avgX = xValues.Average();
            double avgY = yValues.Average();

            double sumXY = 0;
            double sumX2 = 0;
            double sumY2 = 0;

            for (int i = 0; i < xValues.Count; i++)
            {
                double diffX = xValues[i] - avgX;
                double diffY = yValues[i] - avgY;

                sumXY += diffX * diffY;
                sumX2 += diffX * diffX;
                sumY2 += diffY * diffY;
            }

            double denominator = Math.Sqrt(sumX2 * sumY2);
            return denominator == 0 ? 0 : sumXY / denominator;
        }

        private List<double> GetValues(List<Sales> data, string variable, Dictionary<string, double> regionMap)
        {
            return variable switch
            {
                "TotalSales" => data.Select(s => (double)s.TotalSales).ToList(),
                "Price" => data.Select(s => (double)s.Price).ToList(),
                "Quantity" => data.Select(s => (double)s.Quantity).ToList(),
                "Region" => data.Select(s => regionMap[s.Region]).ToList(),
                _ => new List<double>()
            };
        }
    }
}