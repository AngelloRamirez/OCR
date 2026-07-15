using System;
using Tesseract;
using System.Diagnostics;
using System.IO;
using PDFtoImage;
using System.Text.Json;

namespace ConsoleApplication
{
	class Program
	{
		public static void Main(string[] args)
		{
			if (args.Length > 0 && args[0].Equals("--test-token", StringComparison.OrdinalIgnoreCase))
			{
				TestAzureAdToken();
				return;
			}

			if (args.Length > 0 && args[0].Equals("--test-bc", StringComparison.OrdinalIgnoreCase))
			{
				TestBusinessCentral();
				return;
			}

			var testImagePath = "./phototest.tif";
			if (args.Length > 0)
			{
				testImagePath = args[0];
			}

			try
			{
				var logger = new FormattedConsoleLogger();
				var resultPrinter = new ResultPrinter(logger);
				using (var engine = new TesseractEngine(@"./tessdata/eng", "eng", EngineMode.Default))
				{
					if (Path.GetExtension(testImagePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
					{
						string tempDir = Path.Combine(Path.GetTempPath(), "OCR_PDF_" + Guid.NewGuid().ToString("N"));
						try
						{
							using (logger.Begin("Process PDF"))
							{
								logger.Log("Converting {0} to PNG images...", testImagePath);
								string[] images = ConvertPdfToImages(testImagePath, tempDir);
								logger.Log("Converted {0} pages.", images.Length);

								for (int pageIdx = 0; pageIdx < images.Length; pageIdx++)
								{
									string imgPath = images[pageIdx];
									using (logger.Begin("Page {0}", pageIdx + 1))
									{
										ProcessImageFile(engine, imgPath, logger);
									}
								}
							}
						}
						finally
						{
							if (Directory.Exists(tempDir))
							{
								Directory.Delete(tempDir, true);
							}
						}
					}
					else
					{
						using (logger.Begin("Process image"))
						{
							ProcessImageFile(engine, testImagePath, logger);
						}
					}
				}
			}
			catch (Exception e)
			{
				Trace.TraceError(e.ToString());
				Console.WriteLine("Unexpected Error: " + e.Message);
				Console.WriteLine("Details: ");
				Console.WriteLine(e.ToString());
			}

			if (!Console.IsInputRedirected)
			{
				Console.Write("Press any key to continue . . . ");
				Console.ReadKey(true);
			}
		}

		private static void TestAzureAdToken()
		{
			Console.WriteLine("=== Iniciando prueba de AzureAdTokenProvider ===");
			try
			{
				var provider = new AzureAdTokenProvider();
				Console.WriteLine("Instanciado correctamente.");
				
				// Intentamos primero con el scope configurado en appsettings
				Console.WriteLine("1. Intentando con el scope configurado en appsettings...");
				try
				{
					var token1 = provider.GetAccessTokenAsync().GetAwaiter().GetResult();
					Console.WriteLine($"Token 1 (scope de appsettings) obtenido con éxito. Longitud: {token1.Length}");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error con scope de appsettings: {ex.Message}");
				}

				// Intentamos con Microsoft Graph
				Console.WriteLine("\n2. Intentando con scope de Microsoft Graph...");
				try
				{
					var graphToken1 = provider.GetAccessTokenAsync("https://graph.microsoft.com/.default").GetAwaiter().GetResult();
					Console.WriteLine($"Graph Token obtenido con éxito. Longitud: {graphToken1.Length}");
					Console.WriteLine($"Token corto: {graphToken1.Substring(0, Math.Min(30, graphToken1.Length))}...");

					Console.WriteLine("Obteniendo segundo Graph Token (debería venir de caché)...");
					var graphToken2 = provider.GetAccessTokenAsync("https://graph.microsoft.com/.default").GetAwaiter().GetResult();
					if (graphToken1 == graphToken2)
					{
						Console.WriteLine("¡Éxito! El token de Graph proviene de la caché (mismo valor).");
					}
					else
					{
						Console.WriteLine("Advertencia: El token obtenido es diferente.");
					}
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error con scope de Graph: {ex.Message}");
				}

				// Intentamos con Business Central
				Console.WriteLine("\n3. Intentando con scope de Business Central...");
				try
				{
					var bcToken = provider.GetAccessTokenAsync("https://api.businesscentral.dynamics.com/.default").GetAwaiter().GetResult();
					Console.WriteLine($"Business Central Token obtenido con éxito. Longitud: {bcToken.Length}");
					Console.WriteLine($"Token corto: {bcToken.Substring(0, Math.Min(30, bcToken.Length))}...");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error con scope de Business Central: {ex.Message}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error durante la prueba general: {ex}");
			}
			Console.WriteLine("=== Fin de la prueba ===");
		}

		private static void TestBusinessCentral()
		{
			Console.WriteLine("=== Iniciando prueba de BusinessCentralClient con OCRUtilities_ProcessText ===");
			try
			{
				var tokenProvider = new AzureAdTokenProvider();
				var client = new BusinessCentralClient(tokenProvider);
				Console.WriteLine("Cliente instanciado correctamente.");

				// Simulamos datos de prueba de OCR
				var dummyData = new
				{
					inputText = "{\"1\":\"Línea de prueba de OCR 1\",\"2\":\"Línea de prueba de OCR 2\"}"
				};

				string serviceName = "OCRUtilities_ProcessText"; 
				string url = client.BuildUrl(serviceName);
				Console.WriteLine($"\nURL generada para el servicio '{serviceName}':");
				Console.WriteLine(url);

				Console.WriteLine("\nIntentando enviar petición de prueba...");
				try
				{
					var response = client.SendRequestAsync(serviceName, dummyData).GetAwaiter().GetResult();
					Console.WriteLine("Respuesta recibida exitosamente:");
					Console.WriteLine(response);
				}
				catch (HttpRequestException httpEx)
				{
					Console.WriteLine($"Petición HTTP falló: {httpEx.Message}");
				}
				catch (Exception ex)
				{
					Console.WriteLine($"Error al enviar: {ex.Message}");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error durante la prueba general: {ex}");
			}
			Console.WriteLine("=== Fin de la prueba ===");
		}

		private static string[] ConvertPdfToImages(string pdfPath, string outputDir)
		{
			var imagePaths = new List<string>();
			if (!Directory.Exists(outputDir))
			{
				Directory.CreateDirectory(outputDir);
			}

			int pageCount;
			using (var pageCountStream = File.OpenRead(pdfPath))
			{
				pageCount = PDFtoImage.Conversion.GetPageCount(pageCountStream);
			}

			for (int i = 0; i < pageCount; i++)
			{
				string outPath = Path.Combine(outputDir, $"page_{i + 1}.png");
				using (var pageStream = File.OpenRead(pdfPath))
				{
					// Use 300 DPI for high quality OCR output
					PDFtoImage.Conversion.SavePng(outPath, pageStream, page: i, options: new RenderOptions(Dpi: 300));
				}
				imagePaths.Add(outPath);
			}
			return imagePaths.ToArray();
		}

		private static void ProcessImageFile(TesseractEngine engine, string imagePath, FormattedConsoleLogger logger)
		{
			using (var img = Pix.LoadFromFile(imagePath))
			{
				using (var page = engine.Process(img))
				{
					var text = page.GetText();
					logger.Log("Text: {0}", text);
					logger.Log("Mean confidence: {0}", page.GetMeanConfidence());

					// Tomar línea por línea y meterlo dentro de un JSON (llave = número de línea, valor = texto)
					var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
					var lineDict = new Dictionary<string, string>();
					for (int idx = 0; idx < lines.Length; idx++)
					{
						lineDict[(idx + 1).ToString()] = lines[idx];
					}
					string linesJson = JsonSerializer.Serialize(lineDict);

					using (logger.Begin("Send OCR JSON to Business Central"))
					{
						try
						{
							var tokenProvider = new AzureAdTokenProvider();
							var client = new BusinessCentralClient(tokenProvider);

							var payload = new
							{
								inputText = linesJson
							};

							logger.Log("Enviando JSON al servicio OCRUtilities_ProcessText...");
							string response = client.SendRequestAsync("OCRUtilities_ProcessText", payload).GetAwaiter().GetResult();
							logger.Log("Respuesta de Business Central: {0}", response);

							// Parsear y comprobar el booleano devuelto
							using var responseDoc = JsonDocument.Parse(response);
							if (responseDoc.RootElement.TryGetProperty("value", out var valProp) && valProp.ValueKind == JsonValueKind.True)
							{
								logger.Log("El servicio regresó True (se recibió y procesó el texto con éxito).");
							}
							else
							{
								logger.Log("El servicio regresó False o no se recibió el texto.");
							}
						}
						catch (Exception ex)
						{
							logger.Log("Error al enviar al servicio de Business Central: {0}", ex.Message);
						}
					}

					using (var iter = page.GetIterator())
					{
						iter.Begin();
						var i = 1;
						do
						{
							if (i % 2 == 0)
							{
								using (logger.Begin("Line {0}", i))
								{
									do
									{
										using (logger.Begin("Word Iteration"))
										{
											if (iter.IsAtBeginningOf(PageIteratorLevel.Block))
											{
												logger.Log("New block");
											}
											if (iter.IsAtBeginningOf(PageIteratorLevel.Para))
											{
												logger.Log("New paragraph");
											}
											if (iter.IsAtBeginningOf(PageIteratorLevel.TextLine))
											{
												logger.Log("New line");
											}
											logger.Log("word: " + iter.GetText(PageIteratorLevel.Word));
										}
									} while (iter.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));
								}
							}
							i++;
						} while (iter.Next(PageIteratorLevel.Para, PageIteratorLevel.TextLine));
					}
				}
			}
		}



		private class ResultPrinter
		{
			readonly FormattedConsoleLogger logger;

			public ResultPrinter(FormattedConsoleLogger logger)
			{
				this.logger = logger;
			}

			public void Print(ResultIterator iter)
			{
				logger.Log("Is beginning of block: {0}", iter.IsAtBeginningOf(PageIteratorLevel.Block));
				logger.Log("Is beginning of para: {0}", iter.IsAtBeginningOf(PageIteratorLevel.Para));
				logger.Log("Is beginning of text line: {0}", iter.IsAtBeginningOf(PageIteratorLevel.TextLine));
				logger.Log("Is beginning of word: {0}", iter.IsAtBeginningOf(PageIteratorLevel.Word));
				logger.Log("Is beginning of symbol: {0}", iter.IsAtBeginningOf(PageIteratorLevel.Symbol));

				logger.Log("Block text: \"{0}\"", iter.GetText(PageIteratorLevel.Block));
				logger.Log("Para text: \"{0}\"", iter.GetText(PageIteratorLevel.Para));
				logger.Log("TextLine text: \"{0}\"", iter.GetText(PageIteratorLevel.TextLine));
				logger.Log("Word text: \"{0}\"", iter.GetText(PageIteratorLevel.Word));
				logger.Log("Symbol text: \"{0}\"", iter.GetText(PageIteratorLevel.Symbol));
			}
		}
	}
}