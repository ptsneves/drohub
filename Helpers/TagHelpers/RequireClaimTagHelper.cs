using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Helpers.TagHelpers {

    [HtmlTargetElement(Attributes = "asp-require-claim")]
    [HtmlTargetElement(Attributes = "asp-require-claim,asp-claim-type")]
    [HtmlTargetElement(Attributes = "asp-require-claim,asp-claim-value")]
    public class RequireClaimTagHelper : TagHelper {
        private readonly IHttpContextAccessor _http_context_accessor;
        [HtmlAttributeName("asp-claim-type")]
        public string ClaimType { get; set; }

        [HtmlAttributeName("asp-claim-value")]
        public string ClaimValue { get; set; }

        public RequireClaimTagHelper(IHttpContextAccessor http_context_accessor)
        {
            _http_context_accessor = http_context_accessor;
        }

        public override void Process(TagHelperContext context, TagHelperOutput output){
            if (!_http_context_accessor.HttpContext.User.HasClaim(ClaimType, ClaimValue))
                output.SuppressOutput();
        }
    }
}