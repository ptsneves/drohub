using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DroHub.Helpers {
    public class VideoPreviewGenerator : IDisposable {
        public const string FILE_EXTENSION = ".jpeg";
        private const string _FFMPEG_BIN = "/usr/bin/ffmpeg";
        private const string _FFPROBE_BIN = "/usr/bin/ffprobe";
        private const string _IMG2MAGICKCONVERT_BIN = "/usr/bin/convert";

        private readonly string _thumbnail_pattern;
        private readonly string _thumbnail_glob_pattern;

        private VideoPreviewGenerator() {
            var _guid = Guid.NewGuid();
            _thumbnail_pattern = Path.Join(Path.GetTempPath(), $"{_guid.ToString()}-%d{FILE_EXTENSION}");
            _thumbnail_glob_pattern = $"{_guid.ToString()}-*{FILE_EXTENSION}";
        }

        private static async Task<TimeSpan> getVideoDuration(string src) {
            var ffprobe_arg =
                $"-loglevel quiet -of compact=nokey=1:print_section=0 -show_entries format=duration {src}";

            var output = await RunProcess.getProcessOutput(_FFPROBE_BIN, ffprobe_arg);
            return TimeSpan.FromSeconds(double.Parse(output));
        }

        private async Task generateVideoStills(string src) {
            var chunk_duration = (await getVideoDuration(src)).TotalSeconds / 4;

            var ffmpeg_args = $"-i {src} -vf fps=1/{chunk_duration} -an {_thumbnail_pattern}";
            await RunProcess.runProcess(_FFMPEG_BIN, ffmpeg_args);
        }

        private async Task generatePreviewSprites(string dst) {
            var convert_args = $"{string.Join(" ", getStillsList())} +append {dst}";
            await RunProcess.runProcess(_IMG2MAGICKCONVERT_BIN, convert_args);
        }

        private List<string> getStillsList() {
            return Directory.GetFiles(Path.GetTempPath(), _thumbnail_glob_pattern).ToList();
        }

        public static async Task generatePreview(string src, string dst) {
            if (!File.Exists(src))
                throw new InvalidDataException($"{src} is not a valid video path");
            if (File.Exists(dst))
                throw new InvalidDataException($"{dst} already exists");
            if (Path.GetExtension(dst) != FILE_EXTENSION)
                throw new InvalidDataException($"Provided destination extension must be {FILE_EXTENSION}");
            var generator = new VideoPreviewGenerator();
            await generator.generateVideoStills(src);
            await generator.generatePreviewSprites(dst);
            generator.Dispose();
        }

        public void Dispose() {
            getStillsList().ForEach(File.Delete);
        }
    }
}