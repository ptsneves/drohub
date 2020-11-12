using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            public long FirstFrameTimeUsUnix { get; set; }
        }

        private const string _JANUS_PP_REC_BIN = "/usr/bin/janus-pp-rec";
        private const string _FFMPEG_BIN = "/usr/bin/ffmpeg";
        private const string _MJR_FILE_FILTER = "*.mjr";

        private static async Task<MJRHeader> getMJRHeader(string mjr_src) {
            var output = await RunProcess.getProcessOutput(_JANUS_PP_REC_BIN, $"-j {mjr_src}");
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

        private static IEnumerable<string> getMJRFiles(string directory) {
            if (!Directory.Exists(directory))
                throw new InvalidOperationException($"{directory} is not a directory");

            return Directory.GetFiles(directory, _MJR_FILE_FILTER);
        }

        public struct ConvertResult {
            public string result_path;
            public DateTime creation_date_utc;
            public enum MediaType {
                VIDEO,
                AUDIO,
                DATA,
            }

            public MediaType media_type;
        }

        private static async Task<ConvertResult> RunMJRConvert(string mjr_src, bool preserve_after_conversion) {
            var mjr_src_fn = Path.GetFileName(mjr_src);
            var mjr_header = await getMJRHeader(mjr_src);
            var first_frame_utc = DateTimeOffset.FromUnixTimeMilliseconds(mjr_header.FirstFrameTimeUsUnix/1000).UtcDateTime;
            var dst_fn = Path.ChangeExtension(mjr_src_fn, getMRJOutputContainer(mjr_header));
            var tmp_dst = Path.Join(Path.GetTempPath(), dst_fn);


            using var _ = await RunProcess.runProcess(_JANUS_PP_REC_BIN, $"{mjr_src} {tmp_dst}");
            if (!File.Exists(tmp_dst)) {
                throw new InvalidDataException($"Expected {tmp_dst} but it does not exist. mjr file probably empty");
            }

            if (!preserve_after_conversion)
                File.Delete(mjr_src);

            return new ConvertResult {
                result_path = tmp_dst,
                creation_date_utc = first_frame_utc,
                media_type = mjr_header.MediaType switch {
                    "a" => ConvertResult.MediaType.AUDIO,
                    "v" => ConvertResult.MediaType.VIDEO,
                    "d" => ConvertResult.MediaType.DATA,
                    _ => throw new InvalidProgramException("Unreachable")
                }
            };
        }

        public static async Task<ConvertResult> RunConvert(string mjr_src_dir, bool preserve_after_conversion,
            [CanBeNull] ILogger logger) {


            var mjr_files = getMJRFiles(mjr_src_dir);
            var conversion_results = new List<ConvertResult>();
            foreach (var mjr_file in mjr_files) {
                try {
                    conversion_results.Add(await RunMJRConvert(mjr_file, preserve_after_conversion));
                }
                catch (Exception) {
                    // ignored
                }
            }

            var video_result = conversion_results.Single(c => c.media_type == ConvertResult.MediaType.VIDEO);
            var final_dst = Path.Join(mjr_src_dir, Path.GetFileName(video_result.result_path));

            var ffmpeg_input_args = "-err_detect ignore_err";
            var ffmpeg_adelay_args = "";
            var ffmpeg_amix_args = "";
            var ffmpeg_map_args = "";
            long i = 0;
            foreach (var conversion_result in conversion_results) {
                ffmpeg_input_args += $" -i {conversion_result.result_path}";
                switch (conversion_result.media_type) {
                    case ConvertResult.MediaType.VIDEO:
                        ffmpeg_map_args += $" -map {i}:v -c:v copy ";
                        break;
                    case ConvertResult.MediaType.AUDIO: {
                        var delay_ms = Math.Max(0, (conversion_result.creation_date_utc - video_result.creation_date_utc)
                            .TotalMilliseconds);

                        ffmpeg_adelay_args += $"[{i}]adelay={delay_ms}|{delay_ms}[s{i}];";
                        ffmpeg_amix_args += $"[s{i}]";
                        break;
                    }
                    case ConvertResult.MediaType.DATA:
                        break;
                    default:
                        throw new NotImplementedException();
                }
                i++;
            }

            ffmpeg_amix_args += $"amix={i - 1}[mixout]";
            ffmpeg_map_args += "-map [mixout]";

            if (File.Exists(final_dst))
                throw new InvalidProgramException($"Destination {final_dst} already exists. This should not happen. Keeping the original mjr");

            var ffmpeg_arguments =
                $"{ffmpeg_input_args} -filter_complex {ffmpeg_adelay_args}{ffmpeg_amix_args} {ffmpeg_map_args} {final_dst}";

            using var __ = await RunProcess.runProcess(_FFMPEG_BIN, ffmpeg_arguments);


            if (!File.Exists(final_dst))
                throw new InvalidDataException($"Expected {final_dst} but it does not exist.\n {__.StandardError}");
            File.SetCreationTime(final_dst, video_result.creation_date_utc);
            logger?.LogInformation($"Conversion result available at {final_dst}");

            return new ConvertResult {
                result_path = final_dst,
                creation_date_utc = video_result.creation_date_utc,
                media_type = ConvertResult.MediaType.VIDEO
            };
        }
    }
}