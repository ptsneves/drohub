using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DroHub.Areas.DHub.Controllers
{
    [Authorize]
    public class AuthorizedController : Controller
    {
    }
}