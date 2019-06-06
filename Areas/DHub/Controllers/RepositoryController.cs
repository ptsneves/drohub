using Dropbox.Api;
using Dropbox.Api.Files;
using DroHub.Areas.Identity.Data;
using DroHub.Data;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace DroHub.Areas.DHub.Controllers
{
    public static class BlogHelpers
    {
        private static Dictionary<string, Article> ArticleCache = new Dictionary<string, Article>();

        //public static async Task<Article> GetArticle(this DropboxClient client, string blogName, ArticleMetadata metadata, bool bypassCache = false)
        //{
        //    if (metadata == null || string.IsNullOrEmpty(blogName))
        //    {
        //        return null;
        //    }

        //    var key = string.Format(CultureInfo.InvariantCulture, "{0}:{1}", blogName, metadata.Name);

        //    Article article;
        //    if (!bypassCache)
        //    {
        //        lock (ArticleCache)
        //        {
        //            if (ArticleCache.TryGetValue(key, out article))
        //            {
        //                if (article.Metadata.Rev == metadata.Rev)
        //                {
        //                    return article;
        //                }
        //            }
        //        }
        //    }

        //    try
        //    {
        //        using (var download = await client.Files.DownloadAsync("/" + metadata.Filename))
        //        {
        //            var content = await download.GetContentAsStringAsync();

        //            var html = content.ParseMarkdown();

        //            article = Article.FromMetadata(download.Response, html);
        //        }
        //    }
        //    catch (ApiException<DownloadError> e)
        //    {
        //        var pathError = e.ErrorResponse.AsPath;

        //        if (pathError != null && pathError.Value.IsNotFile)
        //        {
        //            return null;
        //        }

        //        throw;
        //    }

        //    lock (ArticleCache)
        //    {
        //        ArticleCache[key] = article;
        //    }

        //    return article;
        //}

        public static void FlushCache(this ControllerBase controller, string blogName)
        {
            var prefix = blogName + ":";

            lock (ArticleCache)
            {
                var keys = from k in ArticleCache.Keys
                           where k.StartsWith(prefix)
                           select k;

                foreach (var key in keys)
                {
                    ArticleCache.Remove(key);
                }
            }
        }

        public static async Task<IEnumerable<ArticleMetadata>> GetArticleList(this DropboxClient client)
        {
            var list = await client.Files.ListFolderAsync(string.Empty);

            var articles = new List<ArticleMetadata>();
            foreach (var item in list.Entries)
            {
                if (!item.IsFile)
                {
                    continue;
                }

                var fileMetadata = item.AsFile;

                var metadata = ArticleMetadata.Parse(item.Name, fileMetadata.Rev);
                if (metadata != null)
                {
                    articles.Add(metadata);
                }
            }

            articles.Sort((l, r) => l.Date.CompareTo(r.Date));

            return articles;
        }

        public static Tuple<string, DateTime, string> ParseBlogFileName(this string filename)
        {
            var elements = filename.Split('.');
            if (elements.Length != 3 || elements[2] != "md" || elements[1].Length != 8)
            {
                return null;
            }

            var name = elements[0];
            ulong dateInteger;
            if (!ulong.TryParse(elements[1], out dateInteger))
            {
                return null;
            }

            int year = (int)(dateInteger / 10000);
            int month = (int)((dateInteger / 100) % 100);
            int day = (int)(dateInteger % 100);

            if (month < 1 || month > 12 || day < 1 || day > 31)
            {
                return null;
            }

            var date = new DateTime(year, month, day);

            return Tuple.Create(
                elements[0],
                date,
                string.Format(CultureInfo.InvariantCulture, "{0}-{1:yyyy-MM-dd}", elements[0], date));
        }
    }

    public class Blog
    {
        public string BlogName { get; private set; }
        public IReadOnlyList<ArticleMetadata> BlogArticles { get; private set; }
    }

    public class ArticleMetadata
    {
        public string Name { get; private set; }
        public string DisplayName { get; private set; }
        public string Rev { get; private set; }
        public DateTime Date { get; private set; }
        public string Filename
        {
            get
            {
                return string.Format(
                    CultureInfo.InvariantCulture,
                    "{0}.{1:yyyyMMdd}.md",
                    this.Name,
                    this.Date);
            }
        }

        public static ArticleMetadata Parse(string filename, string rev)
        {
            var parsed = filename.ParseBlogFileName();
            if (parsed == null)
            {
                return null;
            }

            return new ArticleMetadata
            {
                Name = parsed.Item1,
                Date = parsed.Item2,
                DisplayName = parsed.Item3,
                Rev = rev
            };
        }
    }

    public class Article
    {
        public string Name { get; private set; }
        public ArticleMetadata Metadata { get; private set; }
        public HtmlString Content { get; private set; }

        public Article(string name, ArticleMetadata metadata, HtmlString content)
        {
            this.Name = name;
            this.Metadata = metadata;
            this.Content = content;
        }

        public static Article FromMetadata(FileMetadata metadata, HtmlString content)
        {
            var parsed = metadata.Name.ParseBlogFileName();

            return new Article(
                metadata.Name,
                ArticleMetadata.Parse(metadata.Name, metadata.Rev),
                content);
        }
    }

    [Area("DHub")]
    public class RepositoryController : AuthorizedController
    {
        #region Variables
        private readonly DroHubContext _context;
        private readonly UserManager<DroHubUser> _userManager;


        private static readonly string dBoxAPIKey = "";
        private static readonly string dBoxAPISecret = "";
        private static readonly string dBoxAppName = "DroHub";
        // TODO set URI in runtime, accordingly if you're on localhost, on dev server, or on production server 
        // (see: https://github.com/dropbox/dropbox-sdk-dotnet/blob/91f3f9ff1a6142c35efa9a7b0156cab264439169/dropbox-sdk-dotnet/Examples/SimpleBlogDemo/Controllers/HomeController.cs#L25)
        private static readonly string dBoxAuthRedirectUri = "https://localhost:44379/DHub/Repository/Auth";

        /* Same as the configured under [Dropbox Application] -> settings -> redirect URIs. */
        //private static DropBoxBase dBoxBase = new DropBoxBase(dBoxAPIKey, dBoxAPISecret, dBoxAppName, dBoxAuthRedirectUri);
        #endregion

        #region Constructor
        public RepositoryController(DroHubContext context, UserManager<DroHubUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }
        #endregion

        // TODO Move this methods to a Dropbox helper class
        private DropboxClient GetDropboxClient(DroHubUser user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.DropboxToken))
            {
                return null;
            }
            // else
            return new DropboxClient(user.DropboxToken, new DropboxClientConfig(dBoxAppName));
        }

        // GET: /<controller>/
        public async Task<IActionResult> Index()
        {
            DroHubUser currentUser = await _userManager.GetUserAsync(User);

            if (!string.IsNullOrWhiteSpace(currentUser.DropboxToken))
            {
                return RedirectToAction(nameof(Gallery));
            }
            // else
            currentUser.ConnectState = Guid.NewGuid().ToString("N");
            _context.SaveChanges();

            var redirect = DropboxOAuth2Helper.GetAuthorizeUri(
                OAuthResponseType.Code,
                dBoxAPIKey,
                dBoxAuthRedirectUri,
                currentUser.ConnectState,
                true); // force_reapprove Whether or not to force the user to approve the app again if they've already done so. 
                       // If false (default), a user who has already approved the application may be automatically redirected to the URI specified by redirect_uri. 
                       // If true, the user will not be automatically redirected and will have to approve the app again.

            return Redirect(redirect.ToString());
        }

        // GET: /<controller>/Auth
        public async Task<ActionResult> Auth(string code, string state)
        {
            try
            {
                DroHubUser currentUser = await _userManager.GetUserAsync(User);

                if (currentUser.ConnectState != state)
                {
                    //this.Flash("There was an error connecting to Dropbox.");
                    return RedirectToAction(nameof(Index), "Devices", new { area = "DHub" });
                }

                var response = await DropboxOAuth2Helper.ProcessCodeFlowAsync(
                    code,
                    dBoxAPIKey,
                    dBoxAPISecret,
                    dBoxAuthRedirectUri);

                currentUser.DropboxToken = response.AccessToken;
                currentUser.ConnectState = string.Empty;
                await _context.SaveChangesAsync();

                //this.Flash("This account has been connected to Dropbox.", FlashLevel.Success);
                return RedirectToAction(nameof(Gallery));
            }
            catch (Exception e)
            {
                var message = string.Format(
                    "code: {0}\nAppKey: {1}\nAppSecret: {2}\nRedirectUri: {3}\nException : {4}",
                    code,
                    dBoxAPIKey,
                    dBoxAPISecret,
                    dBoxAuthRedirectUri,
                    e);
                //this.Flash(message, FlashLevel.Danger);
                return RedirectToAction(nameof(Index), "Devices", new { area = "DHub" });
            }
        }

        // POST : /Home/Disconnect
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<ActionResult> Disconnect()
        {
            DroHubUser currentUser = await _userManager.GetUserAsync(User);

            currentUser.DropboxToken = string.Empty;
            await _context.SaveChangesAsync();

            // this.Flash("This account has been disconnected from Dropbox.", FlashLevel.Success);
            return RedirectToAction(nameof(Index), "Devices", new { area = "DHub" });
        }


        // GET: /<controller>/Gallery
        public async Task<IActionResult> Gallery()
        {
            DroHubUser currentUser = await _userManager.GetUserAsync(User);

            var dropBoxClient = GetDropboxClient(currentUser);

            if (dropBoxClient == null)
            {
                return RedirectToAction(nameof(Index), "Devices", new { area = "DHub" });
            }
            // else
            ListFolderResult list = null;
            var articles = new List<FileMetadata>();
            // ---- TEST SECTION ------
            // Try catch added to validate dropboxclient (e.g.: The user may have revoked the access token)
            // This results on a exception when the dropbox client calls the ListFolderAsync() method
            try
            {
                list = await dropBoxClient.Files.ListFolderAsync(string.Empty, true, true); // true to set recursive mode

                foreach (var item in list.Entries)
                {
                    if (!item.IsFile) // With this, only show files (ignoring folders)
                    {
                        continue;
                    }

                    var fileMetadata = item.AsFile;
                    articles.Add(fileMetadata);
                }

            }
            catch (Exception ex)
            {
                // Delete current saved token to force user authorize the app again
                currentUser.DropboxToken = string.Empty;
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index), "Devices", new { area = "DHub" });
            }

            ViewData["Title"] = "My DroHub Repository";

            return View(articles);

            //-------------------------
            /*
            var articles = new List<ArticleMetadata>(await dropBoxClient.GetArticleList());
            bool isEditable = WebSecurity.IsAuthenticated && WebSecurity.CurrentUserId == user.ID;

            Article article = null;

            if (!string.IsNullOrWhiteSpace(id))
            {
                var filtered = from a in articles
                               where a.DisplayName == id
                               select a;
                var selected = filtered.FirstOrDefault();
                if (selected != null)
                {
                    article = await dropBoxClient.GetArticle(blogname, selected);
                }
            }

            if (article == null)
            {
                return View("Index", Tuple.Create(articles, blogname, isEditable));
            }
            else
            {
                return View("Display", Tuple.Create(article, articles, blogname, isEditable));
            }

            ViewData["Title"] = "My DroHub Repository";

            return View(articles);
            */
            //-------------------------
        }
    }
}