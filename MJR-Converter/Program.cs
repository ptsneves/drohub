using System;
using System.IO;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Threading;

namespace MJR_Converter
{
    class Program
    {
        private static string mjr_dir;
        private static string mjr_filter;
        private static bool preserve_after_conversion;

        static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, event_args) =>
            {
                Console.WriteLine("Leaving");
            };

            mjr_dir = Environment.GetEnvironmentVariable("MJR_DIRECTORY") ?? "/var/live-video-storage";
            mjr_filter = Environment.GetEnvironmentVariable("MJR_FILTER") ?? "*.mjr.tmp";
            preserve_after_conversion = Environment.GetEnvironmentVariable("PRESERVE_AFTER_CONVERSION") != null ? true : false;

            //Initialize and sanitize the directory
            foreach (var file_path in Directory.GetFiles(mjr_dir, "*.mjr")) {
                RunConvert(file_path);
            }

            Console.WriteLine($"Running mjr converter in directory {mjr_dir} with file filter for {mjr_filter}");
            using (var _filesystem_watcher = new FileSystemWatcher(mjr_dir, "*.*"))
            {
                _filesystem_watcher.NotifyFilter = NotifyFilters.FileName;

                _filesystem_watcher.Renamed += RunConvert;
                _filesystem_watcher.EnableRaisingEvents = true;
                Thread.Sleep(Timeout.Infinite);
            }
        }

        private static void RunConvert(string mjr_src) {
            var mjr_src_fn = Path.GetFileName(mjr_src);
            var dst_fn = Path.ChangeExtension(mjr_src_fn, "mp4");
            var tmp_dst = Path.Join(Path.GetTempPath(), dst_fn);
            var final_dst = Path.Join(Path.GetDirectoryName(mjr_src), dst_fn);
            if (File.Exists(final_dst))
            {
                Console.WriteLine("Destination already exists. This should not happen. Keeping the original mjr file");
                return;
            }

            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = "/usr/bin/janus-pp-rec";
                    process.StartInfo.Arguments = $"{mjr_src} {tmp_dst}";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();
                    process.WaitForExit();
                    if (process.ExitCode !=0) {
                        throw new InvalidOperationException($"{process.StartInfo.FileName} {process.StartInfo.Arguments} was not successful an exited with {process.ExitCode}");
                    }
                }
                if (!File.Exists(tmp_dst))
                {
                    throw new InvalidDataException($"Expected {tmp_dst} but it does not exist. mjr file probably empty");
                }
                File.Move(tmp_dst, final_dst);
                Console.WriteLine($"Convertion result available at {final_dst}");
                if (!preserve_after_conversion)
                    File.Delete(mjr_src);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception: {e.Message}\n {e.StackTrace}");
            }
        }

        private static void RunConvert(object sender, RenamedEventArgs rename_data)
        {
            var src = rename_data.FullPath;

            if (FileSystemName.MatchesSimpleExpression(mjr_filter, src, false)
                    && ! FileSystemName.MatchesSimpleExpression(mjr_dir, rename_data.OldFullPath)){
                // Console.WriteLine("Not interested in objects renamed to filter only from filter. Skipping...");
                return;
            }

            Console.WriteLine("File {0} renamed to {1}. Ready to try conversion", rename_data.OldFullPath, rename_data.FullPath);
            RunConvert(src);
        }
    }
}
