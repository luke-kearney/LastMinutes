using LastMinutes.Models.Home;
using LastMinutes.Models.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LastMinutes.Controllers;

[Route("/{controller}/{action=Index}")]
public class HomeController : BaseController
{
    
    #region Properties
    
    #endregion
    
    #region Constructor

    public HomeController()
    {
        
    }
    
    #endregion


    public IActionResult Index()
    {
        var model = new IndexViewModel() { IndexTest = CurrentUser ?? "Logged out" };
        return View(model);
    }
    
    
}