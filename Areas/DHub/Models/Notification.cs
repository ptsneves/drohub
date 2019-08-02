namespace DroHub.Areas.DHub.Models {
    public class Notification {
        public const string Information = "text-primary";
        public const string Warning = "text-warning";
        public const string Error = "text-danger";
        public string type { get; }
        public string message { get; }
        public int id { get; }
        public Notification(string type, string message, int id) {
            this.type = type;
            this.message = message;
            this.id = id;
        }
    }
}