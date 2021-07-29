using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Data;
using DroHub.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DroHub.Areas.DHub.API {
    public static class MediaObjectAndTagExtensions {
        public static void AddMediaObjectTagAPI (this IServiceCollection services) {
            services.AddTransient<MediaObjectAndTagAPI>();
        }
    }

    public class MediaObjectAndTagException : Exception {
        internal MediaObjectAndTagException(string message) : base(message) {
        }
    }

    public class MediaObjectAndTagAPI {
        private const string ConnectionBaseDir = "/var/live-video-storage/";
        private const string AnonymousPlaceholder = "storage/";
        public const string PreviewFileNamePrefix = "preview-";
        public const string DroneMediaRemovalTag = "Marked for Drone removal";
        public const string DelayedMediaRemovalTag = "To Remove after Drone Removal";

        public enum MediaType {
            VIDEO,
            PICTURE,
            ANY,
        }

        private static readonly Dictionary<MediaType, string[]> AllowedFileExtensions =
            new Dictionary<MediaType, string[]> {
            { MediaType.VIDEO, new[] {".webm", ".mp4"} },
            { MediaType.PICTURE, new[] {".jpeg"} }
        };

        private readonly DroHubContext _db_context;
        private readonly SubscriptionAPI _subscription_api;
        private readonly IAuthorizationService _authorization_service;


        public MediaObjectAndTagAPI(DroHubContext db_context, IAuthorizationService authorization_service,
            SubscriptionAPI subscription_api) {
            _db_context = db_context;
            _authorization_service = authorization_service;
            _subscription_api = subscription_api;
        }


        public class MediaExtensionValidator : ValidationAttribute {
            public MediaType MediaType { get; set; }

            public MediaExtensionValidator() {
                MediaType = MediaType.ANY;
                ErrorMessage = "Media id is not in a recognizable format";
            }

            public override bool IsValid(object value) {
                if (!(value is string media_id))
                    return false;

                return MediaType switch {
                    MediaType.ANY => isAllowedExtension(media_id),
                    MediaType.VIDEO => isVideo(media_id),
                    MediaType.PICTURE => isPicture(media_id),
                    _ => false
                };
            }
        }

        public class LocalStorageHelper {
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

                Array.Resize(ref skip_filters, skip_filters.Length +1);
                skip_filters[^1] = _CHUNK_FN_END_MAGIC;

                foreach (var file_extension in AllowedFileExtensions[MediaType.VIDEO]) {
                    var paths = Directory.GetFiles(directory, $"*{file_extension}");
                    foreach (var path in paths) {
                        var expected_preview_path = calculatePreviewFilePath(path);
                        if (skip_filters.Any(skip_filter => path.Contains(skip_filter))) {
                            logger.LogInformation("Skipping preview generation for {Path} due to it containing one of {Filters}",
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
                return Path.Join(ConnectionBaseDir,connection_id.ToString());
            }

            private static bool isFrontEndMediaObjectFilePath(string file_path) {
                if (file_path.StartsWith(AnonymousPlaceholder))
                    return true;

                if (file_path.StartsWith(ConnectionBaseDir))
                    return false;

                throw new MediaObjectAndTagException($"file path {file_path} does not follow any recognizable pattern");
            }

            public static string convertToFrontEndFilePath(MediaObject media_object) {
                if (isFrontEndMediaObjectFilePath(media_object.MediaPath))
                    throw new MediaObjectAndTagException($"Cannot convert {media_object.MediaPath} to front end file path if already a frontend file path");
                var r = anonymizeConnectionDirectory(media_object);
                // r = FileNameTranslator.getUserFriendlyMediaPath(r, media_object.CaptureDateTimeUTC);
                return r;
            }

            public static string convertToPreviewFrontEndFilePath(MediaObject media_object) {
                if (isFrontEndMediaObjectFilePath(media_object.MediaPath))
                    throw new MediaObjectAndTagException($"Cannot convert {media_object.MediaPath} to front end file path if already a frontend file path");
                var r = anonymizeConnectionDirectory(media_object);
                r = calculatePreviewFilePath(r);
                // r = FileNameTranslator.getUserFriendlyMediaPath(r, media_object.CaptureDateTimeUTC);
                return r;
            }

            public static string convertToBackEndFilePath(string media_path) {
                if (!isFrontEndMediaObjectFilePath(media_path))
                    throw new MediaObjectAndTagException($"Cannot convert to backend file path if not a frontend file path {media_path}");
                var r = deAnonymizeConnectionDirectory(media_path);
                var real_file_r = calculateFilePathOnHost(r);
                return File.Exists(real_file_r) ? real_file_r : r;
                // r = getTechnicalMediaPath(r);
            }


            public static string calculateFileNameOnHost(bool is_preview, string device_serial, long unix_time_creation_ms,
                string extension) {

                return $"{(is_preview ? PreviewFileNamePrefix : string.Empty)}drone-{device_serial}-{unix_time_creation_ms}{extension}";
            }

            private static bool doesPreviewFileExist(string media_path) {
                return Directory
                    .EnumerateFiles(Path.GetDirectoryName(media_path))
                    .Any(f => f == calculatePreviewFilePath(media_path));
            }

            public static bool doesPreviewExist(MediaObject mo) {
                return doesPreviewFileExist(mo.MediaPath);
            }

            public static bool doesFileExist(string file_path) {
                return (doesPreviewFileExist(file_path) || File.Exists(file_path)) && file_path.StartsWith(ConnectionBaseDir);
            }

            public static bool doesFileExist(MediaObject mo) {
                return File.Exists(calculateFilePathOnHost(mo.MediaPath));
            }

            public static string calculatePreviewFilePath(string file_path) {
                var file_dir = Path.GetDirectoryName(file_path);
                var file_name = Path.GetFileName(file_path);

                foreach (var extension in AllowedFileExtensions[MediaType.VIDEO]) {
                    file_name = file_name.Replace(extension, VideoPreviewGenerator.FILE_EXTENSION);
                }

                if (isVideo(file_path)) {
                    file_name = $"video-{file_name}";
                }
                return !file_name.Contains(PreviewFileNamePrefix)
                    ? Path.Join(file_dir, $"{PreviewFileNamePrefix}{file_name}")
                    : file_path;
            }

            private static string calculateFilePathOnHost(string file_path) {
                return file_path.Replace(PreviewFileNamePrefix, "");
            }

            private static string calculateConnectionFilePath(long connection_id, string file_name) {
                return Path.Join(ConnectionBaseDir, connection_id.ToString(), file_name);
            }

            public bool doesAssembledFileExist() {
                return File.Exists(calculateAssembledFilePath());
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

            public string calculateChunkedFilePath() {
                return calculateConnectionFilePath(_connection_id, getChunkedFileNameOn());
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
                return AllowedFileExtensions.SelectMany(e => e.Value)
                    .Any(extension => doesFileExist(Path.ChangeExtension(backend_path, extension)));
            }

            public long calculateNextChunkOffset() {
                var latest_chunk_file_name = getChunkedFilesList()
                    .OrderByDescending(calculateBytesOffsetFromFile)
                    .FirstOrDefault();

                if (latest_chunk_file_name == null)
                    return 0;

                var latest_byte_offset = calculateBytesOffsetFromFile(latest_chunk_file_name) ;
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

                if (!File.Exists(preview_file_path)) {
                    if (isVideo(generated_file_path)) {
                        await VideoPreviewGenerator.generatePreview(generated_file_path, preview_file_path);
                    }
                    else if (isPicture(generated_file_path)) {
                        await ImagePreviewGenerator.generatePreview(generated_file_path, preview_file_path, 640, 480);
                    }
                }

                return generated_file_path;
            }
        }


        public static class FileNameTranslator {
            private static readonly string user_date_format = "yyyy-MM-dd 'at' HH:mm:ssZ";
            private enum BACKEND_FILE_NAME_SPLIT_FIELDS {
                DEVICE_TYPE = 0,
                SERIAL = 1,
                UNIX_TIME_MS =2,
                MEDIA_TYPE =3,
            }

            public struct MediaPathDescription {
                public string extension;
                public string directory;
                public DateTimeOffset date_time;
                public string date_time_string;
                public string serial;
                public string device_type;
            }

            public static MediaPathDescription getDataFromMediaPath(string media_path) {

                var directory = Path.GetDirectoryName(media_path);
                var file_name = Path.GetFileNameWithoutExtension(media_path);
                var extension = Path.GetExtension(media_path);

                if (file_name == null || extension == null)
                    throw new MediaObjectAndTagException($"Path {media_path} does not contain a recognizable file");

                var filename_split = file_name.Split('-');
                if (filename_split.Length != 4)
                    throw new MediaObjectAndTagException($"Cannot make file user friendly because it does not follow a known pattern");

                var capture_time_utc = DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(filename_split[(int)
                    BACKEND_FILE_NAME_SPLIT_FIELDS.UNIX_TIME_MS]));

                var time = capture_time_utc.ToString(user_date_format);
                return new MediaPathDescription {
                    extension = extension,
                    directory = directory,
                    date_time = capture_time_utc,
                    date_time_string = time,
                    serial = filename_split[(int) BACKEND_FILE_NAME_SPLIT_FIELDS.SERIAL],
                    device_type = filename_split[(int) BACKEND_FILE_NAME_SPLIT_FIELDS.DEVICE_TYPE]
                };
            }

            public static string getTechnicalMediaPath(string file_path) {
                var directory = Path.GetDirectoryName(file_path);
                var file_name = Path.GetFileNameWithoutExtension(file_path);
                var extension = Path.GetExtension(file_path);
                if (file_name == null || extension == null)
                    throw new MediaObjectAndTagException($"Path {file_path} does not contain a recognizable file");

                var date_time_index = file_name.Length - user_date_format.Length;

                var date_time = DateTimeOffset.ParseExact(file_name.Substring(date_time_index, file_name.Length),
                    user_date_format, CultureInfo.InvariantCulture);

                var filename_split = file_name.Substring(0, date_time_index).Split('-');
                return Path.Join(directory, $"{filename_split[(int)BACKEND_FILE_NAME_SPLIT_FIELDS.DEVICE_TYPE]}" +
                                            $"-{filename_split[(int)BACKEND_FILE_NAME_SPLIT_FIELDS.SERIAL]}" +
                                            $"{date_time}");
            }
        }

        private static string anonymizeConnectionDirectory(MediaObject media_object) {
            return media_object.MediaPath.Replace(ConnectionBaseDir, AnonymousPlaceholder);
        }

        private static string deAnonymizeConnectionDirectory(string file_path) {
            return file_path.Replace(AnonymousPlaceholder, ConnectionBaseDir);
        }

        private async Task<MediaObject> getMediaObject(string media_path) {
            var r = await _db_context.MediaObjects
                    .Where(mo => mo.MediaPath == media_path)
                    .SingleOrDefaultAsync();
            if (r == null)
                throw new MediaObjectAndTagException($"Cannot find Media object.");
            return r;
        }

        public async Task deleteMediaObject(string media_path) {
            var media = await getMediaObject(media_path);
            await deleteMediaObject(media);
        }

        private async Task<FileStream> getFileForStreaming(string media_path) {
            if (!await authorizeMediaObjectOperation(media_path, ResourceOperations.Read))
                throw new MediaObjectAuthorizationException("User is not allowed to access this media");

            return File.OpenRead(media_path);
        }

        public enum DownloadType {
            PREVIEW,
            STREAM,
            DOWNLOAD
        }

        public static bool isVideo(string media_id) {
            return AllowedFileExtensions[MediaType.VIDEO].Any(e => media_id.ToLower().EndsWith(e));
        }

        public static bool isAllowedExtension(string media_id) {
            return AllowedFileExtensions
                .SelectMany(e => e.Value)
                .Any(extension => media_id.ToLower().EndsWith(extension));
        }

        public static bool isAllowedPreviewExtension(string media_id) {
            return AllowedFileExtensions[MediaType.PICTURE].Any(e => media_id.ToLower().EndsWith(e));
        }

        public static bool isPicture(string media_id) {
            return AllowedFileExtensions[MediaType.PICTURE].Any(e => media_id.ToLower().EndsWith(e));
        }

        public async Task<FileStreamResult> getFileForDownload(string media_id, DownloadType t,
            ControllerBase controller) {

            media_id = LocalStorageHelper.convertToBackEndFilePath(media_id);
            var stream = await getFileForStreaming(media_id);
            var res = t switch {
                DownloadType.STREAM when isVideo(media_id) => controller.File(stream, "video/webm"),
                DownloadType.PREVIEW when isAllowedExtension(media_id) => controller.File(stream, "image/jpeg",
                    Path.GetFileName(media_id)),
                DownloadType.DOWNLOAD when isAllowedExtension(media_id) => controller.File(stream, "application/octet-stream",
                    Path.GetFileName(media_id)),
                _ => throw new MediaObjectAuthorizationException("User is not authorized to access this file")
            };

            res.EnableRangeProcessing = true;
            return res;
        }

        private async Task deleteMediaObject(MediaObject media) {
            if (! await authorizeMediaObjectOperation(media, ResourceOperations.Delete))
                throw new MediaObjectAuthorizationException("User is not authorized to delete this media");

            var media_object_tag = await _db_context.MediaObjectTags
                .Where(mot => mot.MediaPath == media.MediaPath)
                .ToListAsync();

            if (media_object_tag != null)
                _db_context.MediaObjectTags.RemoveRange(media_object_tag);

            _db_context.MediaObjects.Remove(media);
            File.Delete(media.MediaPath);
            await _db_context.SaveChangesAsync();
        }

        public async Task<bool> authorizeMediaObjectOperation(string media_path, IAuthorizationRequirement op) {
            if (op == ResourceOperations.Create)
                return true;

            if (!LocalStorageHelper.doesFileExist(media_path))
                return false;

            //For authorization purposes the preview is the same as the actual file
            var media_object = await getMediaObject(media_path.Replace(PreviewFileNamePrefix, ""));

            return await authorizeMediaObjectOperation(media_object, op);
        }

        public static async Task<GalleryModel> getGalleryModel(DeviceConnectionAPI device_connection_api) {
            var sessions = (await device_connection_api
                .getSubscribedDeviceConnections())
                .ToList();

            var files_per_timestamp = new Dictionary<string, Dictionary<string, GalleryModel.Session>>();
            foreach (var session in sessions) {
                //This is the milliseconds of the UTC midnight of the day of the media
                var session_day_timestamp_datetime = ((DateTimeOffset) session.StartTime.Date)
                    .ToUnixTimeMilliseconds()
                    .ToString();

                var session_start_timestamp = session.StartTime.ToUnixTimeMilliseconds();

                //We can allow sessions that have not finished yet?
                long session_end_timestamp;
                if (session.EndTime < session.StartTime) {
                    if (sessions.Last() == session)
                        session_end_timestamp =
                            DateTimeOffset.Now.ToUnixTimeMilliseconds();
                    else {
                        continue; //We have an unclosed device connection, skip it.
                    }
                }
                else {
                    session_end_timestamp = session.EndTime.ToUnixTimeMilliseconds();
                }

                if (!files_per_timestamp.ContainsKey(session_day_timestamp_datetime)) {
                    files_per_timestamp[session_day_timestamp_datetime] = new Dictionary<string, GalleryModel.Session>();
                }

                if (!files_per_timestamp[session_day_timestamp_datetime].ContainsKey(session_start_timestamp.ToString())) {
                    files_per_timestamp[session_day_timestamp_datetime][session_start_timestamp.ToString()] =
                        new GalleryModel.Session() {
                            DeviceName = session.Device.Name,
                            DeviceSerial = session.Device.SerialNumber,
                            EndTime = session_end_timestamp,
                            StartTime = session_start_timestamp,
                            SessionMedia = new List<GalleryModel.MediaInfo>()
                        };
                }

                foreach (var media_file in session.MediaObjects) {

                    var media_start_time = media_file.CaptureDateTimeUTC;
                    if (!LocalStorageHelper.doesPreviewExist(media_file))
                        continue;


                    var media_info_model = new GalleryModel.MediaInfo() {
                            MediaPath = LocalStorageHelper.doesFileExist(media_file)
                                ? LocalStorageHelper.convertToFrontEndFilePath(media_file) : string.Empty,

                            PreviewMediaPath = LocalStorageHelper.convertToPreviewFrontEndFilePath(media_file),
                            CaptureDateTime = media_start_time.ToUnixTimeMilliseconds(),
                            Tags = media_file.MediaObjectTags.Select(s => s.TagName),
                    };

                    files_per_timestamp[session_day_timestamp_datetime][session_start_timestamp.ToString()]
                        .SessionMedia
                        .Add(media_info_model);
                }
            }

            return new GalleryModel {FilesPerTimestamp = files_per_timestamp};
        }

        private async Task<bool> authorizeMediaObjectOperation(MediaObject media, IAuthorizationRequirement op) {
            if (media == null)
                return true;

            if (op == ResourceOperations.Create)
                return true;

            var r = await _authorization_service
                .AuthorizeAsync(_subscription_api.getClaimsPrincipal(), media, op);
            return r.Succeeded;
        }

        public async Task removeTag(string tag_name, string media_path, bool save_changes = true) {
            media_path = media_path.Replace(PreviewFileNamePrefix, "");
            if (! await authorizeMediaObjectOperation(media_path, MediaObjectAuthorizationHandler.MediaObjectResourceOperations.ManipulateTags))
                throw new MediaObjectAuthorizationException("User is not authorized to read this media");

            var mot = await _db_context.MediaObjectTags
                .SingleAsync(m => m.TagName == tag_name && m.MediaPath == media_path);

            _db_context.MediaObjectTags.Remove(mot);

            if (save_changes)
                await _db_context.SaveChangesAsync();
        }

        public async Task addTags(string media_path, IEnumerable<string> tags, DateTimeOffset? date_time,
            bool save_changes = true, bool authorize = true) {

            media_path = media_path.Replace(PreviewFileNamePrefix, "");
            if (authorize && ! await authorizeMediaObjectOperation(media_path, MediaObjectAuthorizationHandler.MediaObjectResourceOperations.ManipulateTags))
                throw new MediaObjectAuthorizationException("User is not authorized to read this media");
            var unique_tags = tags.Select(x => x.ToLower()).Distinct().ToList();

            foreach (var tag in unique_tags) {
                var mot = new MediaObjectTag {
                    MediaPath = media_path,
                    TagName = tag,
                    SubscriptionOrganizationName = _subscription_api.getSubscriptionName().Value
                };

                if (date_time.HasValue)
                    mot.Timestamp = date_time;

                await _db_context.MediaObjectTags.AddIfNotExists(mot,
                    m => m.TagName == tag && m.MediaPath == media_path);
            }
            if (save_changes)
                await _db_context.SaveChangesAsync();
        }

        public static MediaObject generateMediaObject(string media_path, DateTimeOffset create_time, string org_name,
            long device_connection_id) {
            return new MediaObject {
                SubscriptionOrganizationName = org_name,
                DeviceConnectionId = device_connection_id,
                MediaPath = media_path,
                CaptureDateTimeUTC = create_time,
            };
        }

        public async Task addMediaObject(MediaObject media_object, List<string> tags, bool is_preview) {

            if (!media_object.MediaPath.Contains(LocalStorageHelper.calculateConnectionDirectory(media_object.DeviceConnectionId)))
                throw new MediaObjectAndTagException("Cannot save media which is not in the connection directory");

            if (!File.Exists(media_object.MediaPath))
                throw new MediaObjectAndTagException("Media path is not a file.");

            var file_name = Path.GetFileName(media_object.MediaPath);
            if (file_name == null)
                throw new MediaObjectAndTagException("Could not extract file name from media path?");

            if (is_preview) {
                if(!file_name.Contains(PreviewFileNamePrefix))
                    throw new MediaObjectAndTagException("Is preview but file name does not have preview prefix");

                media_object.MediaPath = media_object.MediaPath.Replace(PreviewFileNamePrefix, "");
            }

            if (await _db_context.MediaObjects
                .Where(mo => mo.MediaPath == media_object.MediaPath)
                .AnyAsync())
                return;

            await _db_context.AddAsync(media_object);

            if (tags != null)
                await addTags(media_object.MediaPath, tags,  null, false, false);

            await _db_context.SaveChangesAsync();
        }
    }
}