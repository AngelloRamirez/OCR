using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Linq;

namespace ConsoleApplication
{
    public class InvoiceParser
    {
        private static readonly string[] SpanishMonths = new string[]
        {
            "enero", "febrero", "marzo", "abril", "mayo", "junio",
            "julio", "agosto", "septiembre", "octubre", "noviembre", "diciembre"
        };

        public static PurchaseInvoiceData Parse(IEnumerable<string> rawPages)
        {
            var pagesList = rawPages?.ToList() ?? new List<string>();
            var fullText = string.Join("\n", pagesList);

            var invoice = new PurchaseInvoiceData
            {
                RawTextByPage = pagesList
            };

            var lines = fullText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(l => l.Trim())
                                .Where(l => !string.IsNullOrWhiteSpace(l))
                                .ToList();

            // 1. Extraer RFC del proveedor
            ExtractVendorRfc(fullText, lines, invoice);

            // 2. Extraer Nombre / Razón Social del proveedor
            ExtractVendorName(lines, invoice);

            // 3. Extraer Dirección del proveedor
            ExtractVendorAddress(lines, invoice);

            // 4. Extraer Número de Factura
            ExtractInvoiceNo(fullText, lines, invoice);

            // 5. Extraer Fecha de Factura
            ExtractInvoiceDate(fullText, lines, invoice);

            // 6. Extraer Moneda
            ExtractCurrency(fullText, invoice);

            // 7. Extraer Líneas de Detalle (con itemNo y claveSAT)
            ExtractLineItems(lines, invoice);

            // 8. Extraer Montos (Subtotal, IVA, Total)
            ExtractAmounts(lines, fullText, invoice);

            // 9. Calcular Nivel de Confianza (Confidence)
            CalculateConfidence(invoice);

            return invoice;
        }

        public static PurchaseInvoiceData Parse(string rawText)
        {
            return Parse(new[] { rawText ?? string.Empty });
        }

        private static void ExtractVendorRfc(string fullText, List<string> lines, PurchaseInvoiceData invoice)
        {
            var rfcRegex = new Regex(@"\b([A-Z&Ñ]{3,4}\d{6}[A-Z0-9]{3})\b", RegexOptions.IgnoreCase);

            string? foundRfc = null;
            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"\b(EMISOR|PROVEEDOR|VENDEDOR|RFC|R\.F\.C\.)\b", RegexOptions.IgnoreCase))
                {
                    var match = rfcRegex.Match(CleanOcrCharsInRfcCandidate(line));
                    if (match.Success)
                    {
                        foundRfc = match.Value.ToUpper();
                        break;
                    }
                }
            }

            if (foundRfc == null)
            {
                var match = rfcRegex.Match(CleanOcrCharsInRfcCandidate(fullText));
                if (match.Success)
                {
                    foundRfc = match.Value.ToUpper();
                }
            }

            if (!string.IsNullOrEmpty(foundRfc))
            {
                invoice.VendorRfc = foundRfc;
                invoice.Confidence.VendorRfc = 0.98;
            }
            else
            {
                invoice.Confidence.VendorRfc = 0.0;
            }
        }

        private static string CleanOcrCharsInRfcCandidate(string text)
        {
            var cleaned = Regex.Replace(text, @"R\.?F\.?C\.?\s*[:\s]?", "", RegexOptions.IgnoreCase);
            return cleaned;
        }

        private static void ExtractVendorName(List<string> lines, PurchaseInvoiceData invoice)
        {
            foreach (var line in lines)
            {
                var match = Regex.Match(line, @"(?:PROVEEDOR|EMISOR|RAZ[OÓ]N\s+SOCIAL|NOMBRE|VENDIDO\s+POR|EXPEDIDO\s+POR)\s*[:\s]\s*(.+)", RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    var candidate = match.Groups[1].Value.Trim();
                    if (!string.IsNullOrWhiteSpace(candidate) && candidate.Length > 3)
                    {
                        invoice.VendorName = CleanText(candidate);
                        invoice.Confidence.VendorName = 0.95;
                        return;
                    }
                }
            }

            var corpRegex = new Regex(@"\b(S\.?A\.?\s+DE\s+C\.?V\.?|S\.?\s+A\.\s+DE\s+C\.\s+V\.|S\.?\s+DE\s+R\.?L\.?|S\.?A\.?B\.?\s+DE\s+C\.?V\.?|S\.?C\.?|SOCIEDAD\s+ANONIMA|SOCIEDAD\s+DE\s+RESPONSABILIDAD)\b", RegexOptions.IgnoreCase);
            foreach (var line in lines.Take(10))
            {
                if (corpRegex.IsMatch(line))
                {
                    invoice.VendorName = CleanText(line);
                    invoice.Confidence.VendorName = 0.90;
                    return;
                }
            }

            var fallbackLine = lines.Take(5).FirstOrDefault(l => 
                !Regex.IsMatch(l, @"\b(FACTURA|INVOICE|FOLIO|FECHA|RFC|PAGE|P[ÁA]GINA)\b", RegexOptions.IgnoreCase) &&
                l.Length >= 4);

            if (fallbackLine != null)
            {
                invoice.VendorName = CleanText(fallbackLine);
                invoice.Confidence.VendorName = 0.70;
            }
            else
            {
                invoice.Confidence.VendorName = 0.0;
            }
        }

        private static void ExtractVendorAddress(List<string> lines, PurchaseInvoiceData invoice)
        {
            var addressKeywords = new Regex(@"\b(CALLE|AV\.?|AVENIDA|COL\.?|COLONIA|C\.?P\.?|DOMICILIO|DIRECCI[OÓ]N|DIRECCION)\b", RegexOptions.IgnoreCase);
            var addressLine = lines.FirstOrDefault(l => addressKeywords.IsMatch(l));
            if (addressLine != null)
            {
                invoice.VendorAddress = CleanText(addressLine);
            }
        }

        private static void ExtractInvoiceNo(string fullText, List<string> lines, PurchaseInvoiceData invoice)
        {
            var pattern = new Regex(@"(?:FOLIO\s+FISCAL|FACTURA\s+N[Oº°\.]?|FOLIO\s+N[Oº°\.]?|FOLIO|INVOICE\s+NO\.?|FACTURA)\s*[:#\s]?\s*([A-Z0-9\-_]{3,36})", RegexOptions.IgnoreCase);
            
            foreach (var line in lines)
            {
                var match = pattern.Match(line);
                if (match.Success)
                {
                    var val = match.Groups[1].Value.Trim();
                    if (!Regex.IsMatch(val, @"^(FACTURA|INVOICE|FECHA|SERIE)$", RegexOptions.IgnoreCase))
                    {
                        invoice.InvoiceNo = val;
                        invoice.Confidence.InvoiceNo = 0.95;
                        return;
                    }
                }
            }

            var seriesMatch = Regex.Match(fullText, @"\b([A-Z]{1,3}-\d{3,10})\b");
            if (seriesMatch.Success)
            {
                invoice.InvoiceNo = seriesMatch.Groups[1].Value;
                invoice.Confidence.InvoiceNo = 0.85;
            }
            else
            {
                invoice.Confidence.InvoiceNo = 0.0;
            }
        }

        private static void ExtractInvoiceDate(string fullText, List<string> lines, PurchaseInvoiceData invoice)
        {
            var dateRegex = new Regex(@"\b(\d{1,2}[\/\.-]\d{1,2}[\/\.-]\d{2,4}|\d{4}[\/\.-]\d{1,2}[\/\.-]\d{1,2})\b");

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"\b(FECHA|DATE|EMISI[OÓ]N|EXPEDICI[OÓ]N)\b", RegexOptions.IgnoreCase))
                {
                    var match = dateRegex.Match(line);
                    if (match.Success && TryParseDate(match.Value, out string isoDate))
                    {
                        invoice.InvoiceDate = isoDate;
                        invoice.Confidence.InvoiceDate = 0.97;
                        return;
                    }

                    var textDateMatch = Regex.Match(line, @"(\d{1,2})\s+de\s+([a-z]+)\s+de\s+(\d{4})", RegexOptions.IgnoreCase);
                    if (textDateMatch.Success)
                    {
                        int day = int.Parse(textDateMatch.Groups[1].Value);
                        string monthStr = textDateMatch.Groups[2].Value.ToLower();
                        int year = int.Parse(textDateMatch.Groups[3].Value);

                        int monthIdx = Array.IndexOf(SpanishMonths, monthStr) + 1;
                        if (monthIdx > 0)
                        {
                            invoice.InvoiceDate = $"{year:D4}-{monthIdx:D2}-{day:D2}";
                            invoice.Confidence.InvoiceDate = 0.97;
                            return;
                        }
                    }
                }
            }

            var fallbackMatch = dateRegex.Match(fullText);
            if (fallbackMatch.Success && TryParseDate(fallbackMatch.Value, out string fallbackIsoDate))
            {
                invoice.InvoiceDate = fallbackIsoDate;
                invoice.Confidence.InvoiceDate = 0.80;
            }
            else
            {
                invoice.Confidence.InvoiceDate = 0.0;
            }
        }

        private static bool TryParseDate(string input, out string isoDate)
        {
            isoDate = string.Empty;
            string[] formats = new string[]
            {
                "dd/MM/yyyy", "dd-MM-yyyy", "dd.MM.yyyy",
                "d/M/yyyy", "d-M-yyyy",
                "yyyy-MM-dd", "yyyy/MM/dd",
                "dd/MM/yy", "dd-MM-yy"
            };

            if (DateTime.TryParseExact(input, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime dt))
            {
                isoDate = dt.ToString("yyyy-MM-dd");
                return true;
            }

            if (DateTime.TryParse(input, out dt))
            {
                isoDate = dt.ToString("yyyy-MM-dd");
                return true;
            }

            return false;
        }

        private static void ExtractCurrency(string fullText, PurchaseInvoiceData invoice)
        {
            if (Regex.IsMatch(fullText, @"\b(USD|D[OÓ]LARES)\b", RegexOptions.IgnoreCase))
            {
                invoice.Currency = "USD";
            }
            else if (Regex.IsMatch(fullText, @"\b(EUR|EUROS)\b", RegexOptions.IgnoreCase))
            {
                invoice.Currency = "EUR";
            }
            else
            {
                invoice.Currency = "MXN";
            }
        }

        private static void ExtractAmounts(List<string> lines, string fullText, PurchaseInvoiceData invoice)
        {
            var amountRegex = new Regex(@"\$?\s*([0-9]+(?:[\.,]\d+)*)");

            decimal? subtotal = null;
            decimal? taxAmount = null;
            decimal? taxRate = null;
            decimal? totalAmount = null;

            foreach (var line in lines)
            {
                var upperLine = line.ToUpper();

                if (upperLine.Contains("SUBTOTAL") || upperLine.Contains("SUB-TOTAL") || upperLine.Contains("SUB TOTAL"))
                {
                    var matches = amountRegex.Matches(line);
                    foreach (Match m in matches)
                    {
                        if (TryParseDecimal(m.Groups[1].Value, out decimal val) && val > 0)
                        {
                            subtotal = val;
                        }
                    }
                }
                else if (upperLine.Contains("IVA") || upperLine.Contains("IMPUESTO") || upperLine.Contains("TAX"))
                {
                    var rateMatch = Regex.Match(line, @"(\d{1,2})\s*%");
                    if (rateMatch.Success && decimal.TryParse(rateMatch.Groups[1].Value, out decimal rate))
                    {
                        taxRate = rate;
                    }

                    var matches = amountRegex.Matches(line);
                    foreach (Match m in matches)
                    {
                        if (TryParseDecimal(m.Groups[1].Value, out decimal val) && val > 0 && val != taxRate)
                        {
                            taxAmount = val;
                        }
                    }
                }
                else if (upperLine.Contains("TOTAL") && !upperLine.Contains("SUBTOTAL") && !upperLine.Contains("SUB TOTAL"))
                {
                    var matches = amountRegex.Matches(line);
                    foreach (Match m in matches)
                    {
                        if (TryParseDecimal(m.Groups[1].Value, out decimal val) && val > 0)
                        {
                            totalAmount = val;
                        }
                    }
                }
            }

            // Fallback si subtotal no vino explícito pero hay líneas de detalle
            if (!subtotal.HasValue && invoice.Lines.Count > 0)
            {
                subtotal = invoice.Lines.Sum(l => l.LineAmount);
            }

            if (taxRate == null && subtotal.HasValue && subtotal > 0 && taxAmount.HasValue)
            {
                taxRate = Math.Round((taxAmount.Value / subtotal.Value) * 100, 0);
            }
            else if (taxRate == null && (subtotal.HasValue || totalAmount.HasValue))
            {
                taxRate = 16m;
            }

            if (subtotal.HasValue && !taxAmount.HasValue && taxRate.HasValue)
            {
                taxAmount = Math.Round(subtotal.Value * (taxRate.Value / 100m), 2);
            }

            if (subtotal.HasValue && taxAmount.HasValue && !totalAmount.HasValue)
            {
                totalAmount = subtotal.Value + taxAmount.Value;
            }
            else if (totalAmount.HasValue && subtotal.HasValue && !taxAmount.HasValue)
            {
                taxAmount = totalAmount.Value - subtotal.Value;
            }
            else if (totalAmount.HasValue && !subtotal.HasValue && taxRate.HasValue)
            {
                subtotal = Math.Round(totalAmount.Value / (1 + (taxRate.Value / 100m)), 2);
                taxAmount = totalAmount.Value - subtotal.Value;
            }

            invoice.Subtotal = subtotal;
            invoice.TaxAmount = taxAmount;
            invoice.TaxRate = taxRate;
            invoice.TotalAmount = totalAmount;
        }

        private static bool TryParseDecimal(string input, out decimal result)
        {
            result = 0m;
            if (string.IsNullOrWhiteSpace(input)) return false;

            string cleaned = input.Replace("$", "").Replace(" ", "").Trim();

            if (cleaned.Contains(",") && cleaned.Contains("."))
            {
                if (cleaned.IndexOf(',') < cleaned.IndexOf('.'))
                {
                    cleaned = cleaned.Replace(",", "");
                }
                else
                {
                    cleaned = cleaned.Replace(".", "").Replace(",", ".");
                }
            }
            else if (cleaned.Contains(","))
            {
                var parts = cleaned.Split(',');
                if (parts.Length == 2 && parts[1].Length <= 2)
                {
                    cleaned = cleaned.Replace(",", ".");
                }
                else
                {
                    cleaned = cleaned.Replace(",", "");
                }
            }

            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out result);
        }

        private static void ExtractLineItems(List<string> lines, PurchaseInvoiceData invoice)
        {
            var lineItems = new List<PurchaseInvoiceLine>();

            var trailingAmountsRegex = new Regex(@"\$?\s*([0-9]{1,3}(?:[,\.]\d{3})*(?:[,\.]\d{2})|[0-9]+(?:[,\.]\d{2}))\s+\$?\s*([0-9]{1,3}(?:[,\.]\d{3})*(?:[,\.]\d{2})|[0-9]+(?:[,\.]\d{2}))\s*$", RegexOptions.IgnoreCase);

            bool bodySectionStarted = false;

            foreach (var line in lines)
            {
                if (Regex.IsMatch(line, @"\b(CANTIDAD|CANT|DESCRIPCI[OÓ]N|CONCEPTO|PRECIO|PRECIO\s+UNITARIO|IMPORTE)\b", RegexOptions.IgnoreCase))
                {
                    bodySectionStarted = true;
                    continue;
                }

                if (Regex.IsMatch(line, @"\b(SUBTOTAL|SUB-TOTAL|IVA|TOTAL|IMPORTE\s+CON\s+LETRA|OBSERVACIONES)\b", RegexOptions.IgnoreCase))
                {
                    break;
                }

                if (bodySectionStarted || lineItems.Count > 0)
                {
                    var amountsMatch = trailingAmountsRegex.Match(line);
                    if (amountsMatch.Success)
                    {
                        if (TryParseDecimal(amountsMatch.Groups[1].Value, out decimal unitPrice) &&
                            TryParseDecimal(amountsMatch.Groups[2].Value, out decimal lineAmount))
                        {
                            string prefixText = line.Substring(0, amountsMatch.Index).Trim();
                            
                            string? itemNo = null;
                            string? claveSat = null;
                            decimal qty = 1m;
                            string description = prefixText;

                            var satMatch = Regex.Match(prefixText, @"\b(\d{8})\b");
                            if (satMatch.Success)
                            {
                                claveSat = satMatch.Value;
                                string beforeSat = prefixText.Substring(0, satMatch.Index).Trim();
                                string afterSat = prefixText.Substring(satMatch.Index + satMatch.Length).Trim();

                                if (!string.IsNullOrWhiteSpace(beforeSat))
                                {
                                    itemNo = beforeSat;
                                }

                                var qtyAfterMatch = Regex.Match(afterSat, @"^\s*(\d+(?:[\.,]\d+)?)\b");
                                if (qtyAfterMatch.Success && TryParseDecimal(qtyAfterMatch.Groups[1].Value, out decimal parsedQty))
                                {
                                    qty = parsedQty;
                                    description = afterSat.Substring(qtyAfterMatch.Index + qtyAfterMatch.Length).Trim();
                                }
                                else
                                {
                                    description = afterSat;
                                }
                            }
                            else
                            {
                                var qtyMatch = Regex.Match(prefixText, @"^\s*(\d+(?:[\.,]\d+)?)\s+(.+)");
                                if (qtyMatch.Success && TryParseDecimal(qtyMatch.Groups[1].Value, out decimal parsedQty))
                                {
                                    qty = parsedQty;
                                    description = qtyMatch.Groups[2].Value.Trim();
                                }
                                else
                                {
                                    var isolatedQtyMatch = Regex.Match(prefixText, @"(?<=\s|^)(\d+(?:[\.,]\d+)?)(?=\s)");
                                    if (isolatedQtyMatch.Success && TryParseDecimal(isolatedQtyMatch.Value, out decimal pQty))
                                    {
                                        qty = pQty;
                                        string beforeQty = prefixText.Substring(0, isolatedQtyMatch.Index).Trim();
                                        string afterQty = prefixText.Substring(isolatedQtyMatch.Index + isolatedQtyMatch.Length).Trim();
                                        if (!string.IsNullOrWhiteSpace(beforeQty)) itemNo = beforeQty;
                                        description = afterQty;
                                    }
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(description) || !string.IsNullOrWhiteSpace(itemNo))
                            {
                                lineItems.Add(new PurchaseInvoiceLine
                                {
                                    ItemNo = string.IsNullOrWhiteSpace(itemNo) ? null : CleanText(itemNo),
                                    ClaveSat = string.IsNullOrWhiteSpace(claveSat) ? null : claveSat,
                                    Description = CleanText(description),
                                    Quantity = qty,
                                    UnitPrice = unitPrice,
                                    LineAmount = lineAmount
                                });
                            }
                        }
                    }
                }
            }

            invoice.Lines = lineItems;
        }

        private static void CalculateConfidence(PurchaseInvoiceData invoice)
        {
            if (invoice.Lines.Count > 0)
            {
                decimal linesSum = invoice.Lines.Sum(l => l.LineAmount);
                if (invoice.Subtotal.HasValue && Math.Abs(linesSum - invoice.Subtotal.Value) < 0.05m)
                {
                    invoice.Confidence.Lines = 0.98;
                }
                else
                {
                    invoice.Confidence.Lines = 0.85;
                }
            }
            else
            {
                invoice.Confidence.Lines = 0.0;
            }

            double totalConf = invoice.Confidence.VendorName +
                               invoice.Confidence.VendorRfc +
                               invoice.Confidence.InvoiceNo +
                               invoice.Confidence.InvoiceDate +
                               invoice.Confidence.Lines;

            invoice.Confidence.Overall = Math.Round(totalConf / 5.0, 2);
        }

        private static string CleanText(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return Regex.Replace(input.Trim(), @"\s+", " ");
        }
    }
}
