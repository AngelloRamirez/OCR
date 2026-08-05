using System;
using System.Text.Json;

namespace ConsoleApplication
{
    public static class ParserTests
    {
        public static bool RunTests()
        {
            Console.WriteLine("=== Iniciando Pruebas Unitarias de InvoiceParser ===");
            bool allPassed = true;

            try
            {
                // Ejemplo 1: Factura CFDI típica mexicana con itemNo y claveSAT en las líneas
                string sampleText1 = @"
FACTURA
Proveedor XYZ S.A. de C.V.
RFC: XAXX010101000
Av. Reforma 123, Col. Centro, CDMX
Folio: F-2026-00456
Fecha: 15/07/2026

CANTIDAD DESCRIPCION PRECIO IMPORTE
EG SECC-E16/16 JN 25174406 10 Instrumental Panel for Appliance 150.00 1500.00
EG SECC-E16/16 JN 25174406 1 Servicio B 3200.00 3200.00

Subtotal: $4,700.00
IVA 16%: $752.00
Total: $5,452.00
";

                var result1 = InvoiceParser.Parse(sampleText1);

                Console.WriteLine("\n--- Prueba 1: Factura con itemNo y claveSAT en líneas ---");
                Console.WriteLine($"VendorName: {result1.VendorName}");
                Console.WriteLine($"VendorRfc: {result1.VendorRfc}");
                Console.WriteLine($"InvoiceNo: {result1.InvoiceNo}");
                Console.WriteLine($"InvoiceDate: {result1.InvoiceDate}");
                Console.WriteLine($"Subtotal: {result1.Subtotal}");
                Console.WriteLine($"TaxAmount: {result1.TaxAmount}");
                Console.WriteLine($"TotalAmount: {result1.TotalAmount}");
                Console.WriteLine($"Líneas encontradas: {result1.Lines.Count}");

                if (result1.Lines.Count > 0)
                {
                    Console.WriteLine($"Línea 1 ItemNo: {result1.Lines[0].ItemNo}, ClaveSAT: {result1.Lines[0].ClaveSat}");
                }

                bool check1 = result1.VendorRfc == "XAXX010101000" &&
                              result1.InvoiceNo == "F-2026-00456" &&
                              result1.InvoiceDate == "2026-07-15" &&
                              result1.Subtotal == 4700.00m &&
                              result1.TaxAmount == 752.00m &&
                              result1.TotalAmount == 5452.00m &&
                              result1.Lines.Count == 2;

                if (check1)
                {
                    Console.WriteLine("✅ Prueba 1 PASÓ exitosamente.");
                }
                else
                {
                    Console.WriteLine("❌ Prueba 1 FALLÓ.");
                    allPassed = false;
                }

                // Imprimir JSON resultado formateado de la prueba 1
                var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                string jsonOutput = JsonSerializer.Serialize(result1, jsonOptions);
                Console.WriteLine("\n--- Ejemplo del JSON generado (con itemNo y claveSAT) ---");
                Console.WriteLine(jsonOutput);

                // Ejemplo 2: Fecha en texto y RFC con prefijo R.F.C.
                string sampleText2 = @"
EMISOR: INDUSTRIALES DEL NORTE S.A. DE C.V.
R.F.C. INO990101ABC
FECHA DE EMISION: 20 de mayo de 2026
FACTURA NO: 88492

CANTIDAD DESCRIPCION PRECIO IMPORTE
5 REFACCION CILINDRO 500.00 2500.00
";
                var result2 = InvoiceParser.Parse(sampleText2);
                Console.WriteLine("\n--- Prueba 2: Fecha en texto 'de mayo de' y RFC con puntos ---");
                Console.WriteLine($"VendorRfc: {result2.VendorRfc}");
                Console.WriteLine($"InvoiceDate: {result2.InvoiceDate}");
                Console.WriteLine($"InvoiceNo: {result2.InvoiceNo}");
                Console.WriteLine($"TotalAmount: {result2.TotalAmount}");
                Console.WriteLine($"Lines.Count: {result2.Lines.Count}");

                bool check2 = result2.VendorRfc == "INO990101ABC" &&
                              result2.InvoiceDate == "2026-05-20" &&
                              result2.InvoiceNo == "88492" &&
                              result2.TotalAmount == 2900.00m &&
                              result2.Lines.Count == 1;

                if (check2)
                {
                    Console.WriteLine("✅ Prueba 2 PASÓ exitosamente.");
                }
                else
                {
                    Console.WriteLine("❌ Prueba 2 FALLÓ.");
                    allPassed = false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Excepción en pruebas: {ex}");
                allPassed = false;
            }

            Console.WriteLine($"\n=== Fin de Pruebas Unitarias. Resultado final: {(allPassed ? "EXITO" : "FALLO")} ===");
            return allPassed;
        }
    }
}
