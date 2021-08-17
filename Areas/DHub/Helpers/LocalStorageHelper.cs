using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.API;
using DroHub.Areas.DHub.Models;
using DroHub.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.DHub.Helpers {
    public class LocalStorageHelper {
        private const string ConnectionBaseDir = "/var/live-video-storage/";
        private const string _PreviewFileNamePrefix = "preview-";
        private const string _CHUNK_FN_BEGIN_MAGIC = "CHUNK_";
        private const string _CHUNK_FN_END_MAGIC = "_CHUNK";

        private readonly long _connection_id;
        private readonly long _start_offset_bytes;
        private readonly bool _is_preview;
        private readonly string _device_serial;
        private readonly long _unix_time_creation_ms;
        private readonly string _extension;

        public LocalStorageHelper(long connection_id, long start_offset_bytes, bool is_preview,
            string device_serial, long unix_time_creation_ms, string extension) {

            _connection_id = connection_id;
            _start_offset_bytes = start_offset_bytes;
            _is_preview = is_preview;
            _device_serial = device_serial;
            _unix_time_creation_ms = unix_time_creation_ms;
            _extension = extension;
        }

        public static async Task generateVideoPreviewBatchForDirectory(string directory, ILogger logger,
            bool continue_on_failures = false, string[] skip_filters = null) {

            skip_filters ??= new string[] { };

            Array.Resize(ref skip_filters, skip_filters.Length + 1);
            skip_filters[^1] = _CHUNK_FN_END_MAGIC;

            foreach (var file_extension in MediaObjectAndTagAPI.AllowedFileExtensions[MediaObjectAndTagAPI.MediaType.VIDEO]) {
                var paths = Directory.GetFiles(directory, $"*{file_extension}");
                foreach (var path in paths) {
                    var expected_preview_path = calculatePreviewFilePath(path);
                    if (skip_filters.Any(skip_filter => path.Contains((string)skip_filter))) {
                        logger.LogInformation(
                            "Skipping preview generation for {Path} due to it containing one of {Filters}",
                            path, skip_filters);

                        continue;
                    }

                    try {
                        logger.LogInformation("Generating preview {Path} to {PreviewPath}",
                            path, expected_preview_path);

                        await VideoPreviewGenerator.generatePreview(path, expected_preview_path);
                        logger.LogInformation("Finished generating preview {Path} to {PreviewPath}",
                            path, expected_preview_path);
                    }
                    catch {
                        if (!continue_on_failures)
                            throw;

                        logger.LogError("Failed to generate preview {Path} to {PreviewPath}. Asked to continue",
                            path, expected_preview_path);
                    }
                }
            }
        }

        public static async Task generateVideoPreviewForConnectionDir(ILogger logger,
            bool continue_on_failures = false, string[] skip_filters = null) {

            var directories = Directory.GetDirectories(ConnectionBaseDir);
            foreach (var directory in directories) {
                await generateVideoPreviewBatchForDirectory(directory, logger, continue_on_failures, skip_filters);
            }
        }

        public static string calculateConnectionDirectory(long connection_id) {
            return Path.Join(ConnectionBaseDir, connection_id.ToString());
        }

        private static bool isFrontEndMediaObjectFilePath(string file_path) {
            if (file_path.StartsWith(MediaObjectAndTagAPI.AnonymousPlaceholder))
                return true;

            if (file_path.StartsWith(ConnectionBaseDir))
                return false;

            throw new MediaObjectAndTagException($"file path {file_path} does not follow any recognizable pattern");
        }

        public static string convertToFrontEndFilePath(MediaObject media_object) {
            if (isFrontEndMediaObjectFilePath(media_object.MediaPath))
                throw new MediaObjectAndTagException(
                    $"Cannot convert {media_object.MediaPath} to front end file path if already a frontend file path");
            var r = anonymizeConnectionDirectory(media_object);
            // r = FileNameTranslator.getUserFriendlyMediaPath(r, media_object.CaptureDateTimeUTC);
            return r;
        }

        public static string convertToBackEndFilePath(string media_path) {
            if (!isFrontEndMediaObjectFilePath(media_path))
                throw new MediaObjectAndTagException(
                    $"Cannot convert to backend file path if not a frontend file path {media_path}");
            return deAnonymizeConnectionDirectory(media_path);
        }


        private static string calculateFileNameOnHost(bool is_preview, string device_serial, long unix_time_creation_ms,
            string extension) {

            return
                $"{(is_preview ? _PreviewFileNamePrefix : string.Empty)}drone-{device_serial}-{unix_time_creation_ms}{extension}";
        }

        public static bool doesPreviewFileExist(string media_path) {
            return Directory
                .EnumerateFiles(Path.GetDirectoryName(media_path))
                .Any(f => f == calculatePreviewFilePath(media_path));
        }

        public static bool doesPreviewExist(MediaObject mo) {
            return doesPreviewFileExist(mo.MediaPath);
        }

        public static bool isValidMediaPath(string file_path) {
            return (doesPreviewFileExist(file_path) || File.Exists(file_path)) &&
                   file_path.StartsWith(ConnectionBaseDir);
        }

        public static bool hasOnlyPreview(MediaObject file_path) {
            return doesPreviewFileExist(file_path.MediaPath) && !File.Exists(file_path.MediaPath);
        }

        public static string calculatePreviewFilePath(string file_path) {
            var file_dir = Path.GetDirectoryName(file_path);
            var file_name = Path.GetFileName(file_path);

            foreach (var extension in MediaObjectAndTagAPI.AllowedFileExtensions[MediaObjectAndTagAPI.MediaType.VIDEO]) {
                file_name = file_name.Replace(extension, VideoPreviewGenerator.FILE_EXTENSION);
            }

            if (MediaObjectAndTagAPI.isVideo(file_path)) {
                file_name = $"video-{file_name}";
            }

            return !file_name.Contains(_PreviewFileNamePrefix)
                ? Path.Join(file_dir, $"{_PreviewFileNamePrefix}{file_name}")
                : file_path;
        }

        public static string stripPreviewPrefix(string file_path) {
            if (!Path.GetFileName(file_path).Contains("video-"))
                return file_path.Replace(_PreviewFileNamePrefix, "");

            foreach (var extension in MediaObjectAndTagAPI.AllowedFileExtensions[MediaObjectAndTagAPI.MediaType.VIDEO]) {
                var attempt = file_path
                    .Replace(VideoPreviewGenerator.FILE_EXTENSION, extension)
                    .Replace($"{_PreviewFileNamePrefix}video-", "");
                if (File.Exists(attempt))
                    return attempt;
            }

            throw new InvalidProgramException("Unreachable situation");

        }

        public static bool containsPreviewPrefix(string file_name) {
            return file_name.Contains(_PreviewFileNamePrefix);
        }

        private static string calculateConnectionFilePath(long connection_id, string file_name) {
            return Path.Join(ConnectionBaseDir, connection_id.ToString(), file_name);
        }

        public bool doesAssembledFileExist() {
            return File.Exists(calculateAssembledFilePath());
        }

        private static string anonymizeConnectionDirectory(MediaObject media_object) {
            return media_object.MediaPath.Replace(ConnectionBaseDir, MediaObjectAndTagAPI.AnonymousPlaceholder);
        }

        private static string deAnonymizeConnectionDirectory(string file_path) {
            return file_path.Replace(MediaObjectAndTagAPI.AnonymousPlaceholder, ConnectionBaseDir);
        }

        private bool doesChunkedFileExist() {
            return File.Exists(calculateChunkedFilePath());
        }

        private string getChunkedFileNameOn() {
            return $"{getChunkedFilePrefix()}{calculateFileNameOnHost()}";
        }

        public void createDirectory() {
            if (!Directory.Exists(calculateConnectionDirectory(_connection_id))) {
                Directory.CreateDirectory(calculateConnectionDirectory(_connection_id));
            }
        }

        private string getChunkedFilePrefix() {
            return $"{_CHUNK_FN_BEGIN_MAGIC}{_start_offset_bytes}{_CHUNK_FN_END_MAGIC}";
        }

        private string calculateAssembledFilePath() {
            return calculateConnectionFilePath(_connection_id, calculateFileNameOnHost());
        }

        private string calculateChunkedFilePath() {
            return calculateConnectionFilePath(_connection_id, getChunkedFileNameOn());
        }

        public async Task saveChunk(IFormFile file) {
            await using var chunk_file_stream = new FileStream(calculateChunkedFilePath(), FileMode.CreateNew);
            await file.CopyToAsync(chunk_file_stream);
        }

        private long calculateBytesOffsetFromFile(string file_name) {
            return long.Parse(file_name
                .Replace(_CHUNK_FN_BEGIN_MAGIC, string.Empty)
                .Replace(_CHUNK_FN_END_MAGIC, string.Empty)
                .Replace(calculateFileNameOnHost(), string.Empty));
        }

        private string calculateFileNameOnHost() {
            return calculateFileNameOnHost(_is_preview, _device_serial, _unix_time_creation_ms, _extension);
        }

        public bool isFrontEndFileNamePreviewOfExistingFile() {
            var file_name = calculateFileNameOnHost(false, _device_serial, _unix_time_creation_ms, _extension);
            var backend_path = Path.Join(calculateConnectionDirectory(_connection_id), file_name);
            return MediaObjectAndTagAPI.AllowedFileExtensions.SelectMany(e => e.Value)
                .Any(extension => isValidMediaPath(Path.ChangeExtension(backend_path, extension)));
        }

        public long calculateNextChunkOffset() {
            var latest_chunk_file_name = getChunkedFilesList()
                .OrderByDescending(calculateBytesOffsetFromFile)
                .FirstOrDefault();

            if (latest_chunk_file_name == null)
                return 0;

            var latest_byte_offset = calculateBytesOffsetFromFile(latest_chunk_file_name);
            var latest_chunk_file_path = calculateConnectionFilePath(_connection_id, latest_chunk_file_name);
            return latest_byte_offset + new FileInfo(latest_chunk_file_path).Length;
        }

        public bool shouldSendNext(long file_end_offset) {
            return doesChunkedFileExist() || file_end_offset < calculateNextChunkOffset();
        }

        private IEnumerable<string> getChunkedFilesList() {
            return Directory.EnumerateFiles(calculateConnectionDirectory(_connection_id))
                .Where(f => f.Contains(_CHUNK_FN_BEGIN_MAGIC)
                            && f.Contains(_CHUNK_FN_END_MAGIC)
                            && f.Contains(calculateFileNameOnHost()))
                .Select(Path.GetFileName);
        }

        public async Task<string> generateAssembledFile(bool remove_chunk_files_afterwards = true) {
            var chunk_files = getChunkedFilesList().OrderBy(calculateBytesOffsetFromFile).ToList();
            if (chunk_files.Count == 1) {
                var file_path = Path.Join(calculateConnectionDirectory(_connection_id), chunk_files.Last());
                File.Move(file_path, calculateAssembledFilePath());
                return calculateAssembledFilePath();
            }

            await using (var assembled_stream = File.OpenWrite(calculateAssembledFilePath())) {
                foreach (var file_name in chunk_files) {
                    var file_path = Path.Join(calculateConnectionDirectory(_connection_id), file_name);
                    await using var file_stream = File.OpenRead(file_path);
                    await file_stream.CopyToAsync(assembled_stream);
                }
            }

            if (remove_chunk_files_afterwards)
                chunk_files.ForEach(file_name =>
                    File.Delete(Path.Join(calculateConnectionDirectory(_connection_id), file_name)));

            var generated_file_path = calculateAssembledFilePath();
            var preview_file_path = calculatePreviewFilePath(generated_file_path);


            var creation_time = DateTimeOffset.FromUnixTimeMilliseconds(_unix_time_creation_ms);
            if (!File.Exists(preview_file_path)) {
                if (MediaObjectAndTagAPI.isVideo(generated_file_path)) {
                    await VideoPreviewGenerator.generatePreview(generated_file_path, preview_file_path);
                }
                else if (MediaObjectAndTagAPI.isPicture(generated_file_path)) {
                    await ImagePreviewGenerator.generatePreview(generated_file_path, preview_file_path, 640, 480);
                }
                File.SetCreationTimeUtc(preview_file_path, creation_time.UtcDateTime);
            }

            File.SetCreationTimeUtc(generated_file_path, creation_time.UtcDateTime);
            return generated_file_path;
        }
    }
}