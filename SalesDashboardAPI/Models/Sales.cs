namespace SalesDashboardAPI.Models
{
    public class Sales
    {
        public int Id { get; set; }
        public string OrderId { get; set; } // Map to CSV OrderID
        public DateTime OrderDate { get; set; }
        public string Customer { get; set; } // Map to CSV Customer
        public string Region { get; set; }
        public string Product { get; set; }
        public string Category { get; set; }
        public int Quantity { get; set; }
        public decimal Price { get; set; }
        public decimal TotalSales { get; set; }
    }
}