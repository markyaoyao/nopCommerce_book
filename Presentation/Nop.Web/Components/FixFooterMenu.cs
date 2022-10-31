using Microsoft.AspNetCore.Mvc;
using Nop.Web.Factories;
using Nop.Web.Framework.Components;

namespace Nop.Web.Components
{
    public class FixFooterMenuViewComponent : NopViewComponent
    {
        private readonly ICommonModelFactory _commonModelFactory;

        public FixFooterMenuViewComponent(ICommonModelFactory commonModelFactory)
        {
            _commonModelFactory = commonModelFactory;
        }

        public IViewComponentResult Invoke()
        {
            return View();
        }
    }
}

