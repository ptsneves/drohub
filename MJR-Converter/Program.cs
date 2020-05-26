using System;
using System.IO;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace MJR_Converter
{
    internal static class Program
    {
        private struct MJRHeader {
            [JsonPropertyName("t")]
            public string MediaType { get; set; } //a => audio , v => video, d = data

            [JsonPropertyName("c")]
            public string CodecType{ get; set; } // too many options. See janus source

            [JsonPropertyName("s")]
            public long CreateTimeMsUnix { get; set; }

            [JsonPropertyName("u")]
            public long FirstFrameTimeMsUnix { get; set; }
        }

        private const string _JANUS_PP_REC_BIN = "/usr/bin/janus-pp-rec";
        private static string mjr_dir;
        private static string mjr_tmp_filter;
        private static bool preserve_after_conversion;

        private static void Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, event_args) => {
                Console.WriteLine("Leaving");
            };

            mjr_dir = Environment.GetEnvironmentVariable("MJR_DIRECTORY") ?? "/var/live-video-storage";
            mjr_tmp_filter = Environment.GetEnvironmentVariable("MJR_FILTER") ?? "*.mjr.tmp";
            preserve_after_conversion = Environment.GetEnvironmentVariable("MJR_PRESERVE_AFTER_CONVERSION") != null;

            //Initialize and sanitize the directory
            foreach (var file_path in Directory.GetFiles(mjr_dir, "*.mjr")) {
                RunConvert(file_path);
            }

            Console.WriteLine($"Running mjr converter in directory {mjr_dir} with file filter for {mjr_tmp_filter}");
            using var _filesystem_watcher =
                new FileSystemWatcher(mjr_dir, mjr_tmp_filter) {NotifyFilter = NotifyFilters.FileName};

            _filesystem_watcher.Renamed += RunConvert;
            _filesystem_watcher.EnableRaisingEvents = true;
            Thread.Sleep(Timeout.Infinite);
        }

        private static Process runProcess(string executable_path, string arguments) {
            var process = new Process {
                StartInfo = {
                    UseShellExecute = false,
                    FileName = executable_path,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            process.Start();
            process.WaitForExit();
            if (process.ExitCode == 0)
                return process;

            process.Dispose();
            throw new InvalidOperationException($"{process.StartInfo.FileName} {process.StartInfo.Arguments} was not successful and exited with {process.ExitCode}");

        }

        private static MJRHeader getMJRHeader(string mjr_src) {
            using var p = runProcess(_JANUS_PP_REC_BIN, $"-j {mjr_src}");
            var output = p.StandardOutput.ReadToEnd().Trim();
            if (string.IsNullOrEmpty(output))
                throw new InvalidDataException("Janus did not provide any header output. Assuming file is corrupted");

            return JsonSerializer.Deserialize<MJRHeader>(output);
        }

        private static string getMRJOutputContainer(MJRHeader mjr_header) {
            if (string.IsNullOrEmpty(mjr_header.CodecType))
                throw new InvalidDataException("Did not parse any codec data from the mjr header?");
            switch (mjr_header.CodecType) {
            case "vp9":
            case "vp8":
                return "webm";
            case "opus":
                return "opus";
            }
            throw new InvalidDataException($"Unknown codec type {mjr_header.CodecType}");
        }

        private static void RunConvert(string mjr_src) {
            try {
                var mjr_src_fn = Path.GetFileName(mjr_src);
                var mjr_header = getMJRHeader(mjr_src);
                var dst_fn = Path.ChangeExtension(mjr_src_fn, getMRJOutputContainer(mjr_header));
                var tmp_dst = Path.Join(Path.GetTempPath(), dst_fn);
                var final_dst = Path.Join(Path.GetDirectoryName(mjr_src), dst_fn);
                if (File.Exists(final_dst)) {
                    throw new InvalidProgramException($"Destination {final_dst} already exists. This should not happen.Keeping the original mjr");
                }

                using var _ = runProcess(_JANUS_PP_REC_BIN, $"{mjr_src} {tmp_dst}");
                if (!File.Exists(tmp_dst)) {
                    throw new InvalidDataException($"Expected {tmp_dst} but it does not exist. mjr file probably empty");
                }
                File.Move(tmp_dst, final_dst);
                Console.WriteLine($"Conversion result available at {final_dst}");
                if (!preserve_after_conversion)
                    File.Delete(mjr_src);
            }
            catch (Exception e) {
                Console.WriteLine($"Exception: {e.Message}");
            }
        }

        private static void RunConvert(object sender, RenamedEventArgs rename_data)
        {
            var src = rename_data.FullPath;

            if (FileSystemName.MatchesSimpleExpression(mjr_tmp_filter, src, false)
                    && ! FileSystemName.MatchesSimpleExpression(mjr_dir, rename_data.OldFullPath)){
                // Console.WriteLine("Not interested in objects renamed to filter only from filter. Skipping...");
                return;
            }

            Console.WriteLine("File {0} renamed to {1}. Ready to try conversion", rename_data.OldFullPath, rename_data.FullPath);
            RunConvert(src);
        }
    }
}
