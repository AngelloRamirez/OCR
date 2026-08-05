using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ConsoleApplication
{
    public class PurchaseInvoiceData
    {
        [JsonPropertyName("vendorName")]
        public string? VendorName { get; set; }

        [JsonPropertyName("vendorRfc")]
        public string? VendorRfc { get; set; }

        [JsonPropertyName("invoiceNo")]
        public string? InvoiceNo { get; set; }

        [JsonPropertyName("invoiceDate")]
        public string? InvoiceDate { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("subtotal")]
        public decimal? Subtotal { get; set; }

        [JsonPropertyName("taxAmount")]
        public decimal? TaxAmount { get; set; }

        [JsonPropertyName("taxRate")]
        public decimal? TaxRate { get; set; }

        [JsonPropertyName("totalAmount")]
        public decimal? TotalAmount { get; set; }

        [JsonPropertyName("vendorAddress")]
        public string? VendorAddress { get; set; }

        [JsonPropertyName("lines")]
        public List<PurchaseInvoiceLine> Lines { get; set; } = new List<PurchaseInvoiceLine>();

        [JsonPropertyName("confidence")]
        public InvoiceConfidence Confidence { get; set; } = new InvoiceConfidence();

        [JsonPropertyName("rawTextByPage")]
        public List<string> RawTextByPage { get; set; } = new List<string>();
    }

    public class PurchaseInvoiceLine
    {
        [JsonPropertyName("itemNo")]
        public string? ItemNo { get; set; }

        [JsonPropertyName("claveSAT")]
        public string? ClaveSat { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("quantity")]
        public decimal Quantity { get; set; }

        [JsonPropertyName("unitPrice")]
        public decimal UnitPrice { get; set; }

        [JsonPropertyName("lineAmount")]
        public decimal LineAmount { get; set; }
    }

    public class InvoiceConfidence
    {
        [JsonPropertyName("vendorName")]
        public double VendorName { get; set; }

        [JsonPropertyName("vendorRfc")]
        public double VendorRfc { get; set; }

        [JsonPropertyName("invoiceNo")]
        public double InvoiceNo { get; set; }

        [JsonPropertyName("invoiceDate")]
        public double InvoiceDate { get; set; }

        [JsonPropertyName("lines")]
        public double Lines { get; set; }

        [JsonPropertyName("overall")]
        public double Overall { get; set; }
    }
}
