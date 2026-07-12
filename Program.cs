using System;
using Tesseract;
using System.Diagnostics;
using System.IO;
using PDFtoImage;

namespace ConsoleApplication
{
	class Program
	{
		public static void Main(string[] args)
		{
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