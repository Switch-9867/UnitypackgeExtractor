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
		private static string UnitypackagePath;
		private static string UserTempFolder;
		private static string OutputDirectory;
		static long ThreadWork = 0;

		static bool outputMetaFiles = false;
		static bool outputPreviewFiles = true;

		static void Main(string[] args)
		{

			if (args.Contains("-help"))
			{
				OutputHelp();
				return;
			}

			if (args.Contains("-outputMeta"))
			{
				outputMetaFiles = true;
			}

			if (args.Contains("-noPreview"))
			{
				outputPreviewFiles = false;
			}

			if (args.Length < 1 || args[0] == null || args[0].Length < 1 || !File.Exists(args[0]) || !Path.HasExtension(".unitypackage"))
			{
				UnitypackagePath = GetUnityPackage();
				Console.WriteLine("Extracting...");
			}
			else
			{
				UnitypackagePath = args[0];
			}

			if (UnitypackagePath.Length < 2)
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

		private static void OutputHelp()
		{
			Console.WriteLine("Usage: \"extract.exe [unitypackage file] [options]\"");
			Console.WriteLine("Use -outputMeta to output unity's meta files");
			Console.WriteLine("Use -noPreview to skip unity's preview files");
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
				// Theese are most likely 'assets' generated
				// by unity that define directories

				//Console.Error.WriteLine($"Asset is size 0 \"{folder}\"");
				//File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".emptyasset") , new byte[0]);
			}

			else
			{
				File.WriteAllBytes(filePath, fileData);
			}

			if(filePreviewData.Length > 0)
			{
				if (outputPreviewFiles) File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_preview.png"), filePreviewData);
			}

			if (fileMetaData.Length > 0)
			{
				if (outputMetaFiles) File.WriteAllBytes(filePath + ".meta", fileMetaData);
			}

			Interlocked.Increment(ref ThreadWork);
		}

		private static string GetUnityPackage()
		{
			Console.WriteLine("Please drop a unitypackge on this window and press enter.");
			var upp = Console.ReadLine();
			if (!File.Exists(upp))
			{
				Console.WriteLine("Error");
				Console.WriteLine(" ");
				upp = GetUnityPackage();
			}

			if(Path.GetExtension(upp) != ".unitypackage")
			{
				Console.WriteLine("Error");
				Console.WriteLine(" ");
				upp = GetUnityPackage();
			}
			return upp;
		}

	}
}