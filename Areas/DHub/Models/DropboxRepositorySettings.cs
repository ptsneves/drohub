namespace DroHub.Areas.DHub.Models
{
    public class DropboxRepositorySettings : RepositoriesSettings
    {
        public string AppName { get; set; }
        public string AuthRedirectUri { get; set; }
    }
}
