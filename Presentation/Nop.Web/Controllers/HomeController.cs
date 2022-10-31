using Microsoft.AspNetCore.Mvc;

namespace Nop.Web.Controllers
{
    public partial class HomeController : BasePublicController
    {
        public virtual IActionResult Index()
        {
            return View();
        }

        public virtual IActionResult BookIndex()
        {
            return View();
        }

    }
}