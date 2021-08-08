using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Helpers;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        public static readonly string AnonymousPlaceholder = "storage/";
        public const string DroneMediaRemovalTag = "Marked for Drone removal";
        public const string DelayedMediaRemovalTag = "To Remove after Drone Removal";

        public enum MediaType {
            VIDEO,
            PICTURE,
            ANY,
        }

        public static readonly Dictionary<MediaType, string[]> AllowedFileExtensions =
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
                    throw new MediaObjectAndTagException("Cannot make file user friendly because it does not follow a known pattern");

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


        private async Task<MediaObject> getMediaObject(string media_path) {
            var r = await _db_context.MediaObjects
                    .Where(mo => mo.MediaPath == media_path)
                    .SingleOrDefaultAsync();
            if (r == null)
                throw new MediaObjectAndTagException("Cannot find Media object.");
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

            if (t == DownloadType.PREVIEW)
                media_id = LocalStorageHelper.calculatePreviewFilePath(media_id);

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
            var media_object = await getMediaObject(LocalStorageHelper.stripPreviewPrefix(media_path));

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
                        new GalleryModel.Session {
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


                    var media_info_model = new GalleryModel.MediaInfo {
                            MediaPath = LocalStorageHelper.convertToFrontEndFilePath(media_file),
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
            media_path = LocalStorageHelper.stripPreviewPrefix(media_path);
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

            media_path = LocalStorageHelper.stripPreviewPrefix(media_path);
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

        public async Task addMediaObject(string media_path, DateTimeOffset create_time, string org_name,
            long device_connection_id, IEnumerable<string> tags, bool is_preview) {

            var media_object = new MediaObject {
                SubscriptionOrganizationName = org_name,
                DeviceConnectionId = device_connection_id,
                MediaPath = media_path,
                CaptureDateTimeUTC = create_time,
            };

            if (!media_object.MediaPath.Contains(LocalStorageHelper.calculateConnectionDirectory(media_object.DeviceConnectionId)))
                throw new MediaObjectAndTagException("Cannot save media which is not in the connection directory");

            if (!File.Exists(media_object.MediaPath))
                throw new MediaObjectAndTagException("Media path is not a file.");

            var file_name = Path.GetFileName(media_object.MediaPath);
            if (file_name == null)
                throw new MediaObjectAndTagException("Could not extract file name from media path?");

            if (is_preview) {
                if(!LocalStorageHelper.containsPreviewPrefix(file_name))
                    throw new MediaObjectAndTagException("Is preview but file name does not have preview prefix");

                media_object.MediaPath = LocalStorageHelper.stripPreviewPrefix(media_object.MediaPath);
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