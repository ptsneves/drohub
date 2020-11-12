using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DroHub.Areas.DHub.Helpers.ResourceAuthorizationHandlers;
using DroHub.Areas.DHub.Models;
using DroHub.Data;
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

        private static readonly string[] AllowedFileExtensions = {".webp", ".webm", ".mp4"};
        private readonly DroHubContext _db_context;
        private readonly SubscriptionAPI _subscription_api;
        private readonly IAuthorizationService _authorization_service;


        public MediaObjectAndTagAPI(DroHubContext db_context, IAuthorizationService authorization_service,
            SubscriptionAPI subscription_api) {
            _db_context = db_context;
            _authorization_service = authorization_service;
            _subscription_api = subscription_api;
        }

        public static string getConnectionDirectory(long connection_id) {
            return Path.Join(ConnectionBaseDir,connection_id.ToString());
        }

        private static bool isFrontEndMediaObject(string file_path) {
            if (file_path.StartsWith(AnonymousPlaceholder))
                return true;

            if (file_path.StartsWith(ConnectionBaseDir))
                return false;

            throw new MediaObjectAndTagException($"file path {file_path} does not follow any recognizable pattern");
        }

        public static string convertToFrontEndFilePath(MediaObject media_object) {
            if (isFrontEndMediaObject(media_object.MediaPath))
                throw new MediaObjectAndTagException($"Cannot convert {media_object.MediaPath} to front end file path if already a frontend file path");
            var r = anonymizeConnectionDirectory(media_object);
            // r = FileNameTranslator.getUserFriendlyMediaPath(r, media_object.CaptureDateTimeUTC);
            return r;
        }

        public static string convertToBackEndFilePath(string media_path) {
            if (!isFrontEndMediaObject(media_path))
                throw new MediaObjectAndTagException($"Cannot convert to backend file path if not a frontend file path {media_path}");
            var r = deAnonymizeConnectionDirectory(media_path);
            // r = getTechnicalMediaPath(r);
            return r;
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
                public DateTime date_time;
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
                    BACKEND_FILE_NAME_SPLIT_FIELDS.UNIX_TIME_MS])).UtcDateTime;

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

                var date_time = DateTime.ParseExact(file_name.Substring(date_time_index, file_name.Length),
                    user_date_format, CultureInfo.InvariantCulture);

                var filename_split = file_name.Substring(0, date_time_index).Split('-');
                return Path.Join(directory, $"{filename_split[(int)BACKEND_FILE_NAME_SPLIT_FIELDS.DEVICE_TYPE]}" +
                                            $"-{filename_split[(int)BACKEND_FILE_NAME_SPLIT_FIELDS.SERIAL]}" +
                                            $"{(DateTimeOffset)date_time}");
            }
        }

        private static string anonymizeConnectionDirectory(MediaObject media_object) {
            return media_object.MediaPath.Replace(ConnectionBaseDir, AnonymousPlaceholder);
        }

        private static string deAnonymizeConnectionDirectory(string file_path) {
            return file_path.Replace(AnonymousPlaceholder, ConnectionBaseDir);
        }


        public async Task<MediaObject> getMediaObject(string media_path) {
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

        public async Task<FileStream> getFileForStreaming(string media_path) {
            if (!await authorizeMediaObjectOperation(media_path, ResourceOperations.Read))
                throw new MediaObjectAuthorizationException("User is not allowed to access this media");

            return File.OpenRead(media_path);
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

            if (!File.Exists(media_path))
                return false;

            var media_object = await getMediaObject(media_path);

            return await authorizeMediaObjectOperation(media_object, op);
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
            if (! await authorizeMediaObjectOperation(media_path, MediaObjectAuthorizationHandler.MediaObjectResourceOperations.ManipulateTags))
                throw new MediaObjectAuthorizationException("User is not authorized to read this media");

            var mot = await _db_context.MediaObjectTags
                .SingleAsync(m => m.TagName == tag_name && m.MediaPath == media_path);

            _db_context.MediaObjectTags.Remove(mot);

            if (save_changes)
                await _db_context.SaveChangesAsync();
        }

        public async Task addTags(string media_path, IEnumerable<string> tags, DateTime? date_time,
            bool save_changes = true, bool authorize = true) {

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
                    mot.Timestamp = date_time.Value.ToUniversalTime();

                await _db_context.MediaObjectTags.AddIfNotExists(mot,
                    m => m.TagName == tag && m.MediaPath == media_path);
            }
            if (save_changes)
                await _db_context.SaveChangesAsync();
        }

        public static bool isAcceptableExtension(string media_path) {
            return AllowedFileExtensions.Contains(Path.GetExtension(media_path).ToLower());
        }

        public static MediaObject generateMediaObject(string media_path, DateTime create_time, string org_name,
            long device_connection_id) {
            return new MediaObject {
                SubscriptionOrganizationName = org_name,
                DeviceConnectionId = device_connection_id,
                MediaPath = media_path,
                CaptureDateTimeUTC = create_time.ToUniversalTime(),
            };
        }

        public async Task addMediaObject(MediaObject media_object, List<string> tags, bool is_preview) {

            if (!media_object.MediaPath.Contains(getConnectionDirectory(media_object.DeviceConnectionId)))
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