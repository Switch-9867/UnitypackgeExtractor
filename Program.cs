using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Tar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace UnitypackgeExtractor
{
	internal class Program
	{

		static string UnitypackagePath;
		static string UserTempFolder;
		static string OutputDirectory;
		static long ThreadWork = 0;

		static void Main(string[] args)
		{
			if(args.Length < 1)
			{
				Console.WriteLine("Drop a unitypackge on this window and press enter.");
				UnitypackagePath = Console.ReadLine();
				Console.WriteLine("Extracting...");
			}

			if (UnitypackagePath == null) {
				UnitypackagePath = args[0];
			}

			if(UnitypackagePath.Length < 2)
			{
				Console.WriteLine("UnitypackagePath Error");
				Console.ReadKey();
				return;
			}

			UserTempFolder = Path.GetTempPath();
			if (UserTempFolder.Length < 1)
			{
				Console.WriteLine("TempFolder Error");
				Console.ReadKey();
				return;
			}

			OutputDirectory = GenreateOutputDirectory();

			FileStream ups = File.OpenRead(UnitypackagePath);

			string tempFolder = ExtractTGZ(ups);

			IEnumerable<string> folders = Directory.EnumerateDirectories(tempFolder);

			if (folders.Count() < 1)
			{
				Console.WriteLine("Directory Error");
				Console.ReadKey();
				return;
			}

			Console.WriteLine($"Extracting {folders.Count()} assets to: {OutputDirectory}");

			foreach (string folder in folders)
			{
				Thread t = new Thread(new ThreadStart(() =>
				{
					ExtractFile(folder, OutputDirectory);
				}));
				t.Start();
			}

			while(ThreadWork < folders.Count())
			{
				//Console.WriteLine($"Extracting file {ThreadWork} of {folders.Count()}");
				Thread.Sleep(100);
			}
			Console.WriteLine("Finished!");

			Console.WriteLine("Cleaning up...");
			Console.WriteLine($"Deleting {tempFolder}");
			Directory.Delete(tempFolder, true);
			Console.WriteLine("Done!");

			Console.WriteLine("Press any key to close.");
			Console.ReadKey();
		}

		private static string GenreateOutputDirectory()
		{
			return Path.GetFullPath(Path.Combine(UnitypackagePath, @"..\"));
		}

		private static string ExtractTGZ(FileStream stream)
		{
			string uuid = Guid.NewGuid().ToString();
			string outputDir = Path.Combine(UserTempFolder, uuid);

			Stream gzipStream = new GZipInputStream(stream);
			TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.ASCII);

			tarArchive.ExtractContents(outputDir);
			tarArchive.Close();

			gzipStream.Close();
			stream.Close();

			return outputDir + Path.DirectorySeparatorChar;
		}

		private static void ExtractFile(string folder, string output)
		{
			byte[] fileData = new byte[0];
			string filePath = "";
			byte[] fileMetaData = new byte[0];
			byte[] filePreviewData = new byte[0];

			string filename = "";

			IEnumerable<string> files = Directory.EnumerateFiles(folder);
			foreach (string file in files)
			{
				switch (Path.GetFileName(file))
				{
					case "asset":
						fileData = File.ReadAllBytes(file);
						break;
					case "pathname":
						filePath = File.ReadAllText(file);
						filename = Path.GetFileName(filePath);
						break;
					case "asset.meta":
						fileMetaData = File.ReadAllBytes(file);
						break;
					case "preview.png":
						filePreviewData = File.ReadAllBytes(file);
						break;
					default:
						Console.WriteLine($"Unexpected file: {file}");
						break;
				}
			}

			if (filePath == "")
			{
				Console.Error.WriteLine($"File path missing! {folder}");
				return ;
			}

			filePath = Path.Combine(OutputDirectory, filePath);
			Directory.CreateDirectory(Path.GetDirectoryName(filePath));

			if (fileData.Length < 1)
			{
				//Console.Error.WriteLine($"Asset is size 0 \"{folder}\"");
				//File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".emptyasset") , new byte[0]);
			}
			else
			{
				File.WriteAllBytes(filePath, fileData);
			}

			if(filePreviewData.Length > 0)
			{
				File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_preview.png"), filePreviewData);
			}

			if (fileMetaData.Length > 0)
			{
				File.WriteAllBytes(filePath + ".meta", fileMetaData);
			}

			Interlocked.Increment(ref ThreadWork);
		}

	}
}