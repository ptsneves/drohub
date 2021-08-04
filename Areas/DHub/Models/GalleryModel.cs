using System;
using System.Collections.Generic;

namespace DroHub.Areas.DHub.Models {
    public class GalleryModel {
        public class MediaInfo {
            public string MediaPath { get; internal set; }
            public long CaptureDateTime { get; internal set; }
            public IEnumerable<string> Tags { get; internal set; }
        }

        public class Session {
            public string DeviceName { get; internal set; }
            public string DeviceSerial { get; internal set; }

            public long StartTime { get; set; }

            public long EndTime { get; set; }
            public List<MediaInfo> SessionMedia { get; set; }
        }

        public Dictionary<string, Dictionary<string, Session>> FilesPerTimestamp { get; set; }
    }
}