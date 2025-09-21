using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Freelancing.Filters
{
    public class RateLimitMessageFilter : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var controller = context.Controller as Controller;
            if (controller != null)
            {
                var session = context.HttpContext.Session;
                if (session.GetString("RateLimitExceeded") == "true")
                {
                    controller.ViewBag.RateLimitError = session.GetString("RateLimitMessage");
                    controller.ViewBag.RateLimitRetryAfter = session.GetInt32("RateLimitRetryAfter") ?? 60;

                    // Clear the session values after displaying
                    session.Remove("RateLimitExceeded");
                    session.Remove("RateLimitMessage");
                    session.Remove("RateLimitRetryAfter");
                }
            }

            base.OnActionExecuting(context);
        }
    }
}