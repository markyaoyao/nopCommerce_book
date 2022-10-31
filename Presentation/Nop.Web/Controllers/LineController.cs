using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nop.Core;
using Nop.Core.Domain.Customers;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Logging;
using Nop.Web.Models.Line;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Web.Controllers
{
    public class LineController : Controller
    {
        private string AdminUserId = "U39400fc156fcc7ef73870b5ed03caf0a";

        private readonly ILogger _logger;
        private readonly ICustomerService _customerService;
        private readonly CustomerSettings _customerSettings;
        private readonly IStoreContext _storeContext;
        private readonly ICustomerRegistrationService _customerRegistrationService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IFileProvider _fileProvider;
        private readonly IWebHostEnvironment _env;

        public LineController(ILogger logger,
            ICustomerService customerService,
            CustomerSettings customerSettings,
            IStoreContext storeContext,
            ICustomerRegistrationService customerRegistrationService,
            IGenericAttributeService genericAttributeService,
            IHttpClientFactory httpClientFactory,
            IWebHostEnvironment env)
        {
            _logger = logger;
            _customerService = customerService;
            _customerSettings = customerSettings;
            _storeContext = storeContext;
            _customerRegistrationService = customerRegistrationService;
            _genericAttributeService = genericAttributeService;
            _httpClientFactory = httpClientFactory;
            _env = env;
            _fileProvider = env.WebRootFileProvider;
        }
        public IActionResult AccountLink()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AccountLinkGo(LoginTaazeModel model)
        {

            var account = $"{model.LineUserId}@markyoyo.com";
            Customer importCustomer = _customerService.GetCustomerByEmail(account);

            if (importCustomer != null)
            {
                _genericAttributeService.SaveAttribute(importCustomer, "LineLinkTaaze", "1");

                try
                {
                    var client = _httpClientFactory.CreateClient();
                    client.BaseAddress = new Uri("https://api.line.me");
                    //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
                    var response = client.DeleteAsync($"/v2/bot/user/{model.LineUserId}/richmenu").Result;

                    if (!response.IsSuccessStatusCode)
                    {
                    }
                    else
                    {
                    }
                }
                catch
                {
                }

                try
                {
                    //var defaultRichMenuId = "richmenu-4004418a6bf154fe49866da61eb0cbec";
                    var mainRichMenuId = "richmenu-33a92c3208db6e6d0f525dd5e425e055";

                    var client = _httpClientFactory.CreateClient();
                    client.BaseAddress = new Uri("https://api.line.me");
                    //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
                    client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
                    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                    //JObject postBody = new JObject();
                    //HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(postBody), Encoding.UTF8, "application/json");
                    //var response = await client.PostAsync($"/v2/bot/user/{userId}/richmenu/{richMenuId}", httpContent);

                    var response = client.PostAsync($"/v2/bot/user/{model.LineUserId}/richmenu/{mainRichMenuId}", null).Result;

                    if (!response.IsSuccessStatusCode)
                    {
                    }
                    else
                    {
                    }
                }
                catch
                {
                }

                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false });
            }

        }


        public IActionResult Notify()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult NotifyGo(LoginTaazeModel model)
        {
            //build template content
            JObject main = new JObject();
            main.Add("type", "template");
            main.Add("altText", "二收書徵求通知");

            main.Add("template", new JObject(
                    new JProperty("type", "carousel")
                    , new JProperty("columns", new JArray() {
                        new JObject(
                            new JProperty("thumbnailImageUrl", "https://media.taaze.tw/showThumbnail.html?sc=14100060240&height=400&width=310")
                            , new JProperty("imageBackgroundColor", "#FFFFFF")
                            , new JProperty("title", "你只是看起來很努力（暢銷經典新版）")
                            , new JProperty("text", "本書獻給為生存、生活、夢想掙扎的你。\x0a定價：320元，優惠價：5 折 160 元")
                            , new JProperty("actions", new JArray() {
                                new JObject(new JProperty("type", "uri"), new JProperty("label", "查看"), new JProperty("uri", "https://www.taaze.tw/products/14100060240.html"))
                                //, new JObject(new JProperty("type", "uri"), new JProperty("label", "加入購物車"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                            })
                        )
                        
                    })
                    , new JProperty("imageAspectRatio", "rectangle")
                    , new JProperty("imageSize", "contain")
                ));


            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.line.me");
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            JObject jsonContent = new JObject();

            jsonContent.Add("to", model.LineUserId);

            JArray messages = new JArray();

            JObject jsonNotify = new JObject();
            jsonNotify.Add("type", "text");
            jsonNotify.Add("text", "\udbc0\udc85 您的二收書徵求已上架，趕快去搶購。");

            messages.Add(jsonNotify);

            for (int i = 0; i < 1; i++)
            {
                messages.Add(main);
            }
            jsonContent.Add("messages", messages);

            //HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(richMenuBody));
            HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(jsonContent), Encoding.UTF8, "application/json");

            var response = client.PostAsync("/v2/bot/message/push", httpContent).Result;

            if (!response.IsSuccessStatusCode)
            {
                //return StatusCode(500, response.Content.ReadAsStringAsync());
                //responseMsg = "Reply flex message Error";
                return Json(new { success = false });
            }
            else
            {
                return Json(new { success = true });
            }


        }


    }
}
