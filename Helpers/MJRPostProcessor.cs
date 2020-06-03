using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace DroHub.Helpers {
    public static class MJRPostProcessor {
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
        private const string _FFMPEG_BIN = "/usr/bin/ffmpeg";
        private const string _MJR_FILE_FILTER = "*.mjr";

        private static Task<Process> runProcess(string executable_path, string arguments) {
            var tcs = new TaskCompletionSource<Process>();
            var process = new Process {
                StartInfo = {
                    UseShellExecute = false,
                    FileName = executable_path,
                    Arguments = arguments,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            process.Exited += (sender, args) => {
                if (process.ExitCode == 0) {
                    tcs.SetResult(process);
                }
                else {
                    tcs.SetException(new InvalidOperationException(
                        $"{process.StartInfo.FileName} {process.StartInfo.Arguments} was not successful and exited with {process.ExitCode}"));
                }
                process.Dispose();
            };
            process.Start();

            return tcs.Task;
        }

        private static async Task<MJRHeader> getMJRHeader(string mjr_src) {
            using var p = await runProcess(_JANUS_PP_REC_BIN, $"-j {mjr_src}");
            var output = (await p.StandardOutput.ReadToEndAsync()).Trim();
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

        public static IEnumerable<string> getMJRFiles(string directory) {
            if (!Directory.Exists(directory))
                throw new InvalidOperationException($"{directory} is not a directory");

            return Directory.GetFiles(directory, _MJR_FILE_FILTER);
        }

        public static async Task RunConvert(string mjr_src, bool preserve_after_conversion, [CanBeNull] ILogger logger) {
            var mjr_src_fn = Path.GetFileName(mjr_src);
            var mjr_header = await getMJRHeader(mjr_src);
            var dst_fn = Path.ChangeExtension(mjr_src_fn, getMRJOutputContainer(mjr_header));
            var tmp_dst = Path.Join(Path.GetTempPath(), dst_fn);
            var final_dst = Path.Join(Path.GetDirectoryName(mjr_src), dst_fn);
            if (File.Exists(final_dst)) {
                throw new InvalidProgramException($"Destination {final_dst} already exists. This should not happen.Keeping the original mjr");
            }

            using var _ = await runProcess(_JANUS_PP_REC_BIN, $"{mjr_src} {tmp_dst}");
            if (!File.Exists(tmp_dst)) {
                throw new InvalidDataException($"Expected {tmp_dst} but it does not exist. mjr file probably empty");
            }

            using var __ = await runProcess(_FFMPEG_BIN, $"-err_detect ignore_err -i {tmp_dst} -c:v copy {final_dst}");
            File.SetCreationTime(final_dst, DateTimeOffset.FromUnixTimeMilliseconds(mjr_header.FirstFrameTimeMsUnix).UtcDateTime);
            logger?.LogInformation($"Conversion result available at {final_dst}");
            if (!preserve_after_conversion)
                File.Delete(mjr_src);

        }
    }
}