using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Helpers.TagHelpers {

    [HtmlTargetElement(Attributes = "asp-require-claim")]
    public class RequireClaimTagHelper : TagHelper {
        [HtmlAttributeName("asp-require-claim")]
        public Claim RequiredClaim { get; set; }

        [ViewContext]
        public ViewContext ViewContext { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output){
            if (!ViewContext.HttpContext.User.HasClaim(RequiredClaim.Type, RequiredClaim.Value))
                output.SuppressOutput();
        }
    }
}