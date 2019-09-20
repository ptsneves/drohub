using System.Net;

namespace DroHub.Areas.DHub.Models {
    public class DeviceMicroServiceOptions
    {
        public DeviceMicroServiceOptions() {
            Address = "localhost";
            Port = 50051;
        }
        public string Address { get; set; }
        public int Port { get; set; }
    }
}