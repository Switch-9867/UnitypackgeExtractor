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
        private static object global = new object(); // to let log output properly
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

            outputMetaFiles = !args.Contains("-nometa");
            outputPreviewFiles = args.Contains("-outputPreview");
            bool waitBeforeExit = args.Contains("-wait");

            if (args.Length < 1 || args[0] == null || args[0].Length < 1 || !File.Exists(args[0]) || !Path.HasExtension(".unitypackage"))
            {
                UnitypackagePath = GetUnityPackage();
                waitBeforeExit = true; // force wait if it is launched by drag-drop mode
            }
            else
            {
                UnitypackagePath = args[0].Trim('"'); // handle the path with 'space' properly
            }

            if (UnitypackagePath.Length < 2)
            {
                Console.WriteLine("[ERROR] Empty Unitypackage Path: " + UnitypackagePath);
                Console.ReadKey();
                return;
            }

            UserTempFolder = Path.GetTempPath();
            if (UserTempFolder.Length < 1)
            {
                Console.WriteLine("[ERROR] Empty TempFolder Path" + UserTempFolder);
                Console.ReadKey();
                return;
            }

            OutputDirectory = GenreateOutputDirectory();

            string tempFolder = Path.Combine(UserTempFolder, "UnityPackage." + Guid.NewGuid().ToString());

            Console.WriteLine("Extracting '" + UnitypackagePath + "' to temp folder: " + tempFolder);

            FileStream ups = File.OpenRead(UnitypackagePath);
            try
            {
                ExtractTGZ(tempFolder, ups);

                string[] folders = Directory.GetDirectories(tempFolder);

                if (folders.Count() < 1)
                {
                    Console.WriteLine("[ERROR] No data extracted from unity package.");
                    Console.ReadKey();
                    return;
                }

                Console.WriteLine($"Extracting {folders.Count()} assets to: {OutputDirectory}");

                var errors = new List<string>();
                var maxThread = Environment.ProcessorCount;
                var threads = new Thread[maxThread];
                for (int i = 0; i < maxThread; i++)
                {
                    Thread t = new Thread(new ThreadStart(() =>
                    {
                        while (true)
                        {
                            string folder = null;
                            lock (folders)
                            {
                                if (ThreadWork >= folders.Length)
                                    return;

                                folder = folders[ThreadWork++];
                            }

                            try
                            {
                                string filename = ExtractFile(folder);
                                lock (global)
                                {
                                    Console.WriteLine("Extracted file '" + filename + "' from " + Path.GetFileName(folder));
                                }
                            }
                            catch (Exception e)
                            {
                                lock (errors)
                                {
                                    errors.Add("[ERROR] Failed to extract file from " + Path.GetFileName(folder) + "\n" + e.Message);
                                }
                            }
                        }
                    }));
                    t.Start();
                    threads[i] = t;
                }

                // properly wait all threads to finish then print the logs
                foreach (var thread in threads)
                {
                    thread.Join();
                }

                // print all errors in the end
                foreach (var err in errors)
                    Console.WriteLine(err);

                Console.WriteLine("Finished!");

                if(waitBeforeExit)
                {
                    Console.WriteLine($"Press any key to delete the {tempFolder}...");
                    Console.ReadKey();
                }
            }
            finally
            {
                Console.WriteLine("Cleaning up...");
                Console.WriteLine($"Deleting {tempFolder}");
                Directory.Delete(tempFolder, true);
                Console.WriteLine("Done!");

                if(waitBeforeExit)
                {
                    Console.WriteLine("Press any key to close...");
                    Console.ReadKey();
                }
            }
        }

        private static void OutputHelp()
        {
            Console.WriteLine("Usage: \"extract.exe [unitypackage file] [options]\"");
            Console.WriteLine("Use -nometa to skip output unity's meta files");
            Console.WriteLine("Use -outputPreview to extract unity's preview files");
            Console.WriteLine("Use -wait to let user press a key before exit the app");
        }

        private static string GenreateOutputDirectory()
        {
            // must append a slash here to avoid 'space' ended filename
            var filename = Path.GetFileNameWithoutExtension(UnitypackagePath);
            var trimmed = filename.Trim();
            if (trimmed != filename)
                Console.WriteLine("[WARNING] The input package filename is different from the output folder because it begins/ends with spaces");

            return Path.Combine(Path.GetFullPath(Path.Combine(UnitypackagePath, @"..\")), trimmed);
        }

        private static void ExtractTGZ(string outputDir, FileStream stream)
        {
            Stream gzipStream = new GZipInputStream(stream);
            TarArchive tarArchive = TarArchive.CreateInputTarArchive(gzipStream, Encoding.ASCII);

            tarArchive.ExtractContents(outputDir);
            tarArchive.Close();

            gzipStream.Close();
            stream.Close();
        }

        private static string ExtractFile(string folder)
        {
            byte[] fileData = null;
            string filePath = "";
            byte[] fileMetaData = null;
            byte[] filePreviewData = null;

            IEnumerable<string> files = Directory.EnumerateFiles(folder);
            foreach (string file in files)
            {
                switch (Path.GetFileName(file))
                {
                    case "asset":
                        fileData = File.ReadAllBytes(file);
                        break;
                    case "pathname":
                        filePath = File.ReadAllText(file).Split('\n')[0].Trim('\r', '\n', '\0');
                        break;
                    case "asset.meta":
                        if (outputMetaFiles) fileMetaData = File.ReadAllBytes(file);
                        break;
                    case "preview.png":
                        if (outputPreviewFiles) filePreviewData = File.ReadAllBytes(file);
                        break;
                    default:
                        lock (global) { Console.WriteLine($"[WARNING] Unexpected file: {file}"); }
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(filePath))
                throw new FileNotFoundException($"File path missing in {folder}");

            filePath = Path.Combine(OutputDirectory, filePath);
            
            var assetFolder = Path.GetDirectoryName(filePath);
            lock (global)
            {
                try
                {
                    if (!Directory.Exists(assetFolder))
                        Directory.CreateDirectory(assetFolder);
                }
                catch
                {
                    throw;
                }
            }

            if (fileData != null && fileData.Length > 0)
            {
                File.WriteAllBytes(filePath, fileData);
            }
            else
            {
                // Theese are most likely 'assets' generated
                // by unity that define directories

                // lock (global) { Console.WriteLine($"[WARNING] Asset \"{filePath}\" is size 0 in \"{folder}\""); }
                //File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + ".emptyasset") , new byte[0]);
            }

            if (filePreviewData != null && filePreviewData.Length > 0)
            {
                File.WriteAllBytes(Path.Combine(Path.GetDirectoryName(filePath), Path.GetFileNameWithoutExtension(filePath) + "_preview.png"), filePreviewData);
            }

            if (fileMetaData != null && fileMetaData.Length > 0)
            {
                File.WriteAllBytes(filePath + ".meta", fileMetaData);
            }

            return filePath;
        }

        private static string GetUnityPackage()
        {
            Console.WriteLine("Please drop a unitypackge on this window and press enter.");
            var upp = Console.ReadLine();
            upp = upp.Trim('"'); // handle the path with 'space' properly

            if (!File.Exists(upp))
            {
                Console.WriteLine("Cannot find file: " + upp);
                Console.WriteLine(" ");
                upp = GetUnityPackage();
            }

            if (Path.GetExtension(upp) != ".unitypackage")
            {
                Console.WriteLine("Wrong Extension: " + Path.GetExtension(upp));
                Console.WriteLine(" ");
                upp = GetUnityPackage();
            }
            return upp;
        }

    }
}