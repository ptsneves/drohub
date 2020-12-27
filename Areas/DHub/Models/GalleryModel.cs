using System;
using System.Collections.Generic;

namespace DroHub.Areas.DHub.Models {
    public class GalleryModel {
        public class MediaInfo {
            public string MediaPath { get; internal set; }
            public string PreviewMediaPath { get; internal set; }
            public long CaptureDateTime { get; internal set; }
            public IEnumerable<string> Tags { get; internal set; }
        }
        public class FileInfoModel {
            public string device_name { get; internal set; }
            public MediaInfo media_object { get; internal set; }
        }

        public Dictionary<string, Dictionary<string, List<FileInfoModel>>> FilesPerTimestamp { get; set; }
    }
}