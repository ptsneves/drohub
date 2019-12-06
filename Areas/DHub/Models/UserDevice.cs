using DroHub.Areas.Identity.Data;
namespace DroHub.Areas.DHub.Models {
    //Required for many to many
    public class UserDevice
    {
        public int DeviceId { get; set; }
        public Device Device { get; set; }

        public string DroHubUserId { get; set; }
        public DroHubUser DroHubUser { get; set; }
    }
}