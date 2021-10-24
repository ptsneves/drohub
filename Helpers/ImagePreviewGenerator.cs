using System.IO;
using System.Threading.Tasks;

namespace DroHub.Helpers {
    public static class ImagePreviewGenerator {
        private const string FILE_EXTENSION = ".jpeg";
        private const string _IMG2MAGICKCONVERT_BIN = "/usr/bin/convert";

        public static async Task generatePreview(string src, string dst, int width_px, int height_px) {
            if (!File.Exists(src))
                throw new InvalidDataException($"{src} is not a valid video path");
            if (File.Exists(dst))
                throw new InvalidDataException($"{dst} already exists");
            if (Path.GetExtension(dst) != FILE_EXTENSION)
                throw new InvalidDataException($"Provided destination extension must be {FILE_EXTENSION}");
            var convert_args = $"{src} -thumbnail {width_px}x{height_px} {dst}";
            await RunProcess.runProcess(_IMG2MAGICKCONVERT_BIN, convert_args);
        }
    }
}