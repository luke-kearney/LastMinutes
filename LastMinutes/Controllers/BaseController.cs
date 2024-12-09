using LastMinutes.Models.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LastMinutes.Controllers;

public class BaseController : Controller
{

    protected string? CurrentUser { get; set; }
    
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        base.OnActionExecuting(context);

        ViewData["LayoutModel"] = new LayoutViewModel("Luke Kearney");

        CurrentUser = "Luke Kearney";

    }
    
}