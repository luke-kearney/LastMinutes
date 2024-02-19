using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LastMinutes.ActionFilters
{
    public class VersionAppending : IActionFilter
    {

        private readonly IConfiguration _config;

        public VersionAppending(IConfiguration config) 
        {
            _config = config;
        }

        public void OnActionExecuting(ActionExecutingContext filterContext)
        {
            string AppVersion = _config.GetValue<string>("AppVersion") ?? "AppVersion";
            string AppStage = _config.GetValue<string>("AppStage") ?? "AppStage";

            Controller controller = filterContext.Controller as Controller;

            if (controller != null)
            {
                controller.ViewData["AppVersion"] = AppVersion;
                controller.ViewData["AppStage"] = AppStage;
                controller.ViewData["AppVersionComplete"] = $"{AppStage}{AppVersion}";
            }
        }

        public void OnActionExecuted(ActionExecutedContext actionExecutedContext)
        {

            

        }

    }
}
