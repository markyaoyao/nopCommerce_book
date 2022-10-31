using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Line.Messaging;
using Nop.Core;
using Nop.Services.Logging;
using Newtonsoft.Json;
using Nop.Services.Customers;
using Nop.Core.Domain.Customers;
using Nop.Services.Stores;
using Nop.Services.Common;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.IO;
using System.Net;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Hosting;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Nop.Web.Controllers.APIs
{
    public partial class LineBotController : isRock.LineBot.LineWebHookControllerBase
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

        public LineBotController(ILogger logger,
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

        [HttpPost]
        public IActionResult BookHook()
        {
            try
            {
                
                //設定ChannelAccessToken
                this.ChannelAccessToken = "zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=";
                //配合Line Verify
                if (ReceivedMessage.events == null || ReceivedMessage.events.Count() <= 0 ||
                    ReceivedMessage.events.FirstOrDefault().replyToken == "00000000000000000000000000000000") return Ok();

                _logger.Information(JsonConvert.SerializeObject(this.ReceivedMessage.events));

                //取得Line Event
                var defaultRichMenuId = "richmenu-4004418a6bf154fe49866da61eb0cbec";
                var mainRichMenuId = "richmenu-33a92c3208db6e6d0f525dd5e425e055";
                var LineEvent = this.ReceivedMessage.events.FirstOrDefault();
                var responseMsg = "";
                var userId = LineEvent.source.userId;
                var account = $"{userId}@markyoyo.com";
                Customer importCustomer = _customerService.GetCustomerByEmail(account);
                string taazeLogin = importCustomer != null && !string.IsNullOrEmpty(_genericAttributeService.GetAttribute<string>(importCustomer, "LineLinkTaaze")) 
                        ? _genericAttributeService.GetAttribute<string>(importCustomer, "LineLinkTaaze") : "0";

                //管理登入TAAZE的RichMenu--Start
                var userRichMenuId = Get_rich_menu_ID_of_user(userId).Result;
                if (string.IsNullOrEmpty(userRichMenuId))
                {
                    //user沒有綁定richmenu
                    if (!taazeLogin.Equals("1"))
                    {
                        var setUserRichMenuId = Link_rich_menu_to_user(userId, defaultRichMenuId).Result;
                    }
                    else
                    {
                        var setUserRichMenuId = Link_rich_menu_to_user(userId, mainRichMenuId).Result;
                    }
                }
                else
                {
                    //user有綁定richmenu
                    //先判斷帳號是否綁定
                    if (!taazeLogin.Equals("1"))//帳號沒綁定
                    {
                        if (!userRichMenuId.Equals(defaultRichMenuId))//原本richmenu不是登入，先解除再綁登入
                        {
                            var delUserRichMenuId = Unlink_rich_menu_from_user(userId).Result;
                            var setUserRichMenuId = Link_rich_menu_to_user(userId, defaultRichMenuId).Result;
                        }
                    }
                    else
                    {
                        if (!userRichMenuId.Equals(mainRichMenuId))//原本richmenu不是主選單，先解除再綁主選單
                        {
                            var delUserRichMenuId = Unlink_rich_menu_from_user(userId).Result;
                            var setUserRichMenuId = Link_rich_menu_to_user(userId, mainRichMenuId).Result;
                        }
                    }

                    
                }
                //管理登入TAAZE的RichMenu--End


                //準備回覆訊息
                if (LineEvent.type.ToLower() == "message" && LineEvent.message.type == "text")
                {
                    if(!taazeLogin.Equals("1"))
                    {
                        responseMsg = $"請先登入TAAZE，提供完整的服務。";
                    }
                    else
                    {
                        responseMsg = $"你說了: {LineEvent.message.text}。";
                    }
                }
                else if (LineEvent.type.ToLower() == "message")
                {
                    if (!taazeLogin.Equals("1"))
                    {
                        responseMsg = $"請先登入TAAZE，提供完整的服務。";
                    }
                    else
                    {
                        responseMsg = $"收到 event : {LineEvent.type} type: {LineEvent.message.type} ";
                    }
                }
                else if (LineEvent.type.ToLower() == "follow")
                {
                    //自動產生Customer帳號
                    if (!string.IsNullOrEmpty(userId))
                    {
                        if(importCustomer == null)
                        {
                            //新增
                            var _customer = _customerService.InsertGuestCustomer();
                            bool isApproved = _customerSettings.UserRegistrationType == UserRegistrationType.Standard;
                            var registrationRequest = new CustomerRegistrationRequest(_customer,
                                account,
                                account,
                                "!@#123QWEqwe",
                                _customerSettings.DefaultPasswordFormat,
                                _storeContext.CurrentStore.Id,
                                isApproved);

                            var registrationResult = _customerRegistrationService.RegisterCustomer(registrationRequest);
                            if (registrationResult.Success)
                            {
                                //form fields
                                _genericAttributeService.SaveAttribute(_customer, NopCustomerDefaults.FirstNameAttribute, "Guest");
                                _genericAttributeService.SaveAttribute(_customer, "LineCustomer", "1");

                                responseMsg = $"歡迎加入TAAZE讀冊生活網路書店\x0a請先登入TAAZE，提供完整的服務。";

                            }
                            else
                            {
                                responseMsg = $"收到 event : {LineEvent.type}，Register Error ";
                            }

                        }
                        else
                        {
                            if (importCustomer.Deleted)
                            {
                                importCustomer.Deleted = false;
                                _customerService.UpdateCustomer(importCustomer);
                            }
                            if (!taazeLogin.Equals("1"))
                            {
                                responseMsg = $"歡迎回到TAAZE讀冊生活網路書店\x0a請先登入TAAZE，提供完整的服務。";
                            }
                            else
                            {
                                responseMsg = $"歡迎回到TAAZE讀冊生活網路書店";
                            }
                            
                        }
                    }
                    else
                    {
                        responseMsg = $"收到 event : {LineEvent.type}，User ID 錯誤 ";
                    }
                }
                else if (LineEvent.type.ToLower() == "postback")
                {
                    //responseMsg = $"收到 event : {LineEvent.type} type: {LineEvent.postback.data} ";
                    if (string.IsNullOrEmpty(LineEvent.postback.data))
                    {
                        responseMsg = "Postback data is empty or null";
                    }
                    else
                    {
                        if (LineEvent.postback.data.Equals("ChangeToMemberRichMenu"))
                        {
                            var richMenuId = "richmenu-3b04ee73c6bd4499cfc0e4560c728b70";
                            var client = _httpClientFactory.CreateClient();
                            client.BaseAddress = new Uri("https://api.line.me");
                            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
                            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");

                            var response = client.PostAsync($"/v2/bot/user/{LineEvent.source.userId}/richmenu/{richMenuId}", null).Result;
                            if (!response.IsSuccessStatusCode)
                            {
                                //return StatusCode(500, response.Content.ReadAsStringAsync());
                                responseMsg = "Change Member Rich Menu Error";
                            }
                            else
                            {
                                return Ok();
                            }
                        }
                        else if (LineEvent.postback.data.Equals("ChangeToMainRichMenu"))
                        {
                            var richMenuId = "richmenu-252163330921c795b2fc7871d9da5689";
                            var client = _httpClientFactory.CreateClient();
                            client.BaseAddress = new Uri("https://api.line.me");
                            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
                            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");

                            var response = client.PostAsync($"/v2/bot/user/{LineEvent.source.userId}/richmenu/{richMenuId}", null).Result;
                            if (!response.IsSuccessStatusCode)
                            {
                                //return StatusCode(500, response.Content.ReadAsStringAsync());
                                responseMsg = "Change Member Rich Menu Error";
                            }
                            else
                            {
                                return Ok();
                            }
                            
                        }
                        else if (LineEvent.postback.data.Equals("OpenMyBookCase"))
                        {
                            //build template content
                            JObject main = new JObject();
                            main.Add("type", "template");
                            main.Add("altText", "我的書櫃");

                            main.Add("template", new JObject(
                                    new JProperty("type", "carousel")
                                    , new JProperty("columns", new JArray() {
                                        new JObject(
                                            new JProperty("thumbnailImageUrl", "https://media.taaze.tw/showThumbnail.html?sc=14100060240&height=400&width=310")
                                            , new JProperty("imageBackgroundColor", "#FFFFFF")
                                            , new JProperty("title", "你只是看起來很努力（暢銷經典新版）")
                                            , new JProperty("text", "本書獻給為生存、生活、夢想掙扎的你。\x0a定價：320元，優惠價：5 折 160 元")
                                            , new JProperty("actions", new JArray() {
                                                new JObject(new JProperty("type", "uri"), new JProperty("label", "查看"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                                                //, new JObject(new JProperty("type", "uri"), new JProperty("label", "加入購物車"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                                            })
                                        )
                                        , new JObject(
                                            new JProperty("thumbnailImageUrl", "https://media.taaze.tw/showThumbnail.html?sc=11100932950&height=400&width=310")
                                            , new JProperty("imageBackgroundColor", "#FFFFFF")
                                            , new JProperty("title", "獨裁者的廚師")
                                            , new JProperty("text", "定價：450，優惠價：79 折 356 元")
                                            , new JProperty("actions", new JArray() {
                                                new JObject(new JProperty("type", "uri"), new JProperty("label", "查看"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                                                //, new JObject(new JProperty("type", "uri"), new JProperty("label", "加入購物車"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                                            })
                                        )
                                        , new JObject(
                                            new JProperty("thumbnailImageUrl", "https://media.taaze.tw/showThumbnail.html?sc=11100975011&height=400&width=310")
                                            , new JProperty("imageBackgroundColor", "#FFFFFF")
                                            , new JProperty("title", "翻轉首爾：叛民城市議題漫遊")
                                            , new JProperty("text", "定價：500，優惠價：79 折 395 元")
                                            , new JProperty("actions", new JArray() {
                                                new JObject(new JProperty("type", "uri"), new JProperty("label", "查看"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                                                //, new JObject(new JProperty("type", "uri"), new JProperty("label", "加入購物車"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                                            })
                                        )
                                        , new JObject(
                                            new JProperty("thumbnailImageUrl", "https://media.taaze.tw/showThumbnail.html?sc=11100974407&height=400&width=310")
                                            , new JProperty("imageBackgroundColor", "#FFFFFF")
                                            , new JProperty("title", "走路：獨處的實踐（平裝）")
                                            , new JProperty("text", "定價：360，優惠價：79 折 284 元")
                                            , new JProperty("actions", new JArray() {
                                                new JObject(new JProperty("type", "uri"), new JProperty("label", "查看"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                                                //, new JObject(new JProperty("type", "uri"), new JProperty("label", "加入購物車"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
                                            })
                                        )
                                        , new JObject(
                                            new JProperty("thumbnailImageUrl", "https://media.taaze.tw/showThumbnail.html?sc=11100882937&height=400&width=310")
                                            , new JProperty("imageBackgroundColor", "#FFFFFF")
                                            , new JProperty("title", "David Bowie：百變前衛的大衛‧鮑伊")
                                            , new JProperty("text", "定價：680，優惠價：79 折 537 元")
                                            , new JProperty("actions", new JArray() {
                                                new JObject(new JProperty("type", "uri"), new JProperty("label", "查看"), new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL"))
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

                            jsonContent.Add("replyToken", LineEvent.replyToken);

                            JArray messages = new JArray();

                            for (int i = 0; i < 1; i++)
                            {
                                messages.Add(main);
                            }

                            jsonContent.Add("messages", messages);

                            //HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(richMenuBody));
                            HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(jsonContent), Encoding.UTF8, "application/json");

                            var response = client.PostAsync("/v2/bot/message/reply", httpContent).Result;

                            if (!response.IsSuccessStatusCode)
                            {
                                //return StatusCode(500, response.Content.ReadAsStringAsync());
                                responseMsg = "Reply flex message Error";
                            }
                            else
                            {
                                return Ok();
                            }

                        }
                        else if (LineEvent.postback.data.Equals("OpenCouponItems"))
                        {
                            responseMsg = "\udbc0\udc83還未完成索取折價券的Demo";
                        }
                        else if (LineEvent.postback.data.Equals("OpenMyOrders"))
                        {
                            responseMsg = "\udbc0\udc38 還未完成訂單查詢的Demo";
                        }
                        else if (LineEvent.postback.data.Equals("OpenAccountLink"))
                        {
                            responseMsg = "\udbc0\udc7c還未完成帳號綁定的Demo";
                        }
                        else if (LineEvent.postback.data.Equals("OpenMyCoupons"))
                        {
                            responseMsg = "\udbc0\udc78還未完成我的折價券的Demo";
                        }
                        else
                        {
                            responseMsg = LineEvent.postback.data;
                        }
                    }

                }
                else
                {
                    responseMsg = $"收到 event : {LineEvent.type} ";
                }

                //回覆訊息
                this.ReplyMessage(LineEvent.replyToken, responseMsg);
                //response OK
                return Ok();
            }
            catch (Exception ex)
            {
                //回覆訊息
                this.PushMessage(AdminUserId, "發生錯誤:\n" + ex.Message);
                //response OK
                return Ok();
            }
        }

        /// <summary>
        /// 刪除Rich Menu
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> DelRichMenuById(string richMenuId)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.line.me");
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
            var response = await client.DeleteAsync($"/v2/bot/richmenu/{richMenuId}");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, response.Content.ReadAsStringAsync());
            }

            return Ok(response.Content.ReadAsStringAsync());
        }

        /// <summary>
        /// 取得所有Rich Menu Id
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetRichMenuList()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.line.me");
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
            string result = await client.GetStringAsync("/v2/bot/richmenu/list");
            return Ok(result);
        }

        /// <summary>
        /// 新增 Rich Menu
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> AddRichMenu()
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.line.me");
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            JObject richMenuBody = new JObject();

            JObject size = new JObject();
            size.Add("width", 1200);
            size.Add("height", 810);
            richMenuBody.Add("size", size);

            #region 主選單
            //richMenuBody.Add("selected", true);
            //richMenuBody.Add("name", $"main menu");
            //richMenuBody.Add("chatBarText", $"主題選單");
            //richMenuBody.Add("areas", new JArray() { 
            //        //block 1
            //        new JObject(
            //            new JProperty("bounds", new JObject(
            //                new JProperty("x", 0)
            //                , new JProperty("y", 0)
            //                ,new JProperty("width", 400)
            //                ,new JProperty("height", 405)
            //            ))
            //            , new JProperty("action", new JObject(
            //                new JProperty("type", "uri")
            //                , new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL")
            //            ))
            //        ),
            //        //block 2
            //        new JObject(
            //            new JProperty("bounds", new JObject(
            //                new JProperty("x", 400)
            //                , new JProperty("y", 0)
            //                ,new JProperty("width", 400)
            //                ,new JProperty("height", 405)
            //            ))
            //            , new JProperty("action", new JObject(
            //                new JProperty("type", "uri")
            //                , new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL")
            //            ))
            //        ),
            //        //block 3
            //        new JObject(
            //            new JProperty("bounds", new JObject(
            //                new JProperty("x", 800)
            //                , new JProperty("y", 0)
            //                ,new JProperty("width", 400)
            //                ,new JProperty("height", 405)
            //            ))
            //            , new JProperty("action", new JObject(
            //                new JProperty("type", "postback")
            //                , new JProperty("data", "OpenMyBookCase")
            //            ))
            //        ),
            //        //block 4
            //        new JObject(
            //            new JProperty("bounds", new JObject(
            //                new JProperty("x", 0)
            //                , new JProperty("y", 405)
            //                ,new JProperty("width", 400)
            //                ,new JProperty("height", 405)
            //            ))
            //            , new JProperty("action", new JObject(
            //                new JProperty("type", "uri")
            //                , new JProperty("uri", "https://www.markyoyo.com/apple-macbook-pro-13-inch")
            //            ))
            //        ),
            //        //block 5
            //        new JObject(
            //            new JProperty("bounds", new JObject(
            //                new JProperty("x", 400)
            //                , new JProperty("y", 405)
            //                ,new JProperty("width", 400)
            //                ,new JProperty("height", 405)
            //            ))
            //            , new JProperty("action", new JObject(
            //                new JProperty("type", "postback")
            //                , new JProperty("data", "OpenCouponItems")
            //            ))
            //        ),
            //        //block 6
            //        new JObject(
            //            new JProperty("bounds", new JObject(
            //                new JProperty("x", 800)
            //                , new JProperty("y", 405)
            //                ,new JProperty("width", 400)
            //                ,new JProperty("height", 405)
            //            ))
            //            , new JProperty("action", new JObject(
            //                new JProperty("type", "postback")
            //                , new JProperty("data", "ChangeToMemberRichMenu")
            //            ))
            //        )
            //});
            #endregion

            #region 會員中心選單

            richMenuBody.Add("selected", true);
            richMenuBody.Add("name", $"member menu");
            richMenuBody.Add("chatBarText", $"會員中心選單");
            richMenuBody.Add("areas", new JArray() { 
                    //block 1
                    new JObject(
                        new JProperty("bounds", new JObject(
                            new JProperty("x", 0)
                            , new JProperty("y", 0)
                            ,new JProperty("width", 400)
                            ,new JProperty("height", 405)
                        ))
                        , new JProperty("action", new JObject(
                            new JProperty("type", "postback")
                            , new JProperty("data", "OpenMyOrders")
                        ))
                    ),
                    //block 2
                    new JObject(
                        new JProperty("bounds", new JObject(
                            new JProperty("x", 400)
                            , new JProperty("y", 0)
                            ,new JProperty("width", 400)
                            ,new JProperty("height", 405)
                        ))
                        , new JProperty("action", new JObject(
                            new JProperty("type", "postback")
                            , new JProperty("data", "OpenAccountLink")
                        ))
                    ),
                    //block 3
                    new JObject(
                        new JProperty("bounds", new JObject(
                            new JProperty("x", 800)
                            , new JProperty("y", 0)
                            ,new JProperty("width", 400)
                            ,new JProperty("height", 405)
                        ))
                        , new JProperty("action", new JObject(
                            new JProperty("type", "uri")
                            , new JProperty("uri", "https://liff.line.me/1594612944-KGnP0zDL")
                        ))
                    ),
                    //block 4
                    new JObject(
                        new JProperty("bounds", new JObject(
                            new JProperty("x", 0)
                            , new JProperty("y", 405)
                            ,new JProperty("width", 400)
                            ,new JProperty("height", 405)
                        ))
                        , new JProperty("action", new JObject(
                            new JProperty("type", "postback")
                            , new JProperty("data", "ChangeToMainRichMenu")
                        ))
                    ),
                    //block 5
                    new JObject(
                        new JProperty("bounds", new JObject(
                            new JProperty("x", 400)
                            , new JProperty("y", 405)
                            ,new JProperty("width", 400)
                            ,new JProperty("height", 405)
                        ))
                        , new JProperty("action", new JObject(
                            new JProperty("type", "uri")
                            , new JProperty("uri", "https://www.facebook.com/TAAZE")
                        ))
                    ),
                    //block 6
                    new JObject(
                        new JProperty("bounds", new JObject(
                            new JProperty("x", 800)
                            , new JProperty("y", 405)
                            ,new JProperty("width", 400)
                            ,new JProperty("height", 405)
                        ))
                        , new JProperty("action", new JObject(
                            new JProperty("type", "postback")
                            , new JProperty("data", "OpenMyCoupons")
                        ))
                    )
            });
            #endregion

            //JArray areas = new JArray();

            //for (int i = 0; i < 4; i++)
            //{
            //    JObject area = new JObject();
            //    JObject bounds = new JObject();
            //    JObject action = new JObject();

            //    if (i == 0)
            //    {
            //        bounds.Add("x", 0);
            //        bounds.Add("y", 0);
            //        bounds.Add("width", 1250);
            //        bounds.Add("height", 843);

            //        action.Add("type", "uri");
            //        action.Add("uri", "https://shop.markyoyo.com/home/bookindex");

            //    }
            //    else if (i == 1)
            //    {
            //        bounds.Add("x", 1250);
            //        bounds.Add("y", 0);
            //        bounds.Add("width", 1250);
            //        bounds.Add("height", 843);

            //        action.Add("type", "uri");
            //        action.Add("uri", "https://liff.line.me/1594612944-KGnP0zDL");

            //    }
            //    else if (i == 2)
            //    {
            //        bounds.Add("x", 0);
            //        bounds.Add("y", 843);
            //        bounds.Add("width", 1250);
            //        bounds.Add("height", 843);

            //        action.Add("type", "message");
            //        action.Add("text", "up1");

            //    }
            //    else if (i == 3)
            //    {
            //        bounds.Add("x", 1250);
            //        bounds.Add("y", 843);
            //        bounds.Add("width", 1250);
            //        bounds.Add("height", 843);

            //        action.Add("type", "postback");
            //        action.Add("data", "up2");

            //    }

            //    area.Add("bounds", bounds);
            //    area.Add("action", action);
            //    areas.Add(area);
            //}

            //richMenuBody.Add("areas", areas);

            //HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(richMenuBody));
            HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(richMenuBody), Encoding.UTF8, "application/json");

            var response = await client.PostAsync("/v2/bot/richmenu", httpContent);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, response.Content.ReadAsStringAsync());
            }

            return Ok(response.Content.ReadAsStringAsync());

        }

        /// <summary>
        /// 刪除Rich Menu
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> UploadRichMenuImage(string richMenuId)
        {
            var client = _httpClientFactory.CreateClient();
            //client.BaseAddress = new Uri("https://api.line.me");
            client.BaseAddress = new Uri("https://api-data.line.me");
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/jpeg"));
            //client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("image/png"));

            //使url
            ByteArrayContent content = null;
            using (WebClient webClient = new WebClient())
            {
                byte[] dataArr = webClient.DownloadData(@$"http://localhost:15536/images/uploaded/richmenu_3.jpg");
                content = new ByteArrayContent(dataArr);
                //save file to local
                //File.WriteAllBytes(@"path.png", dataArr);
            }
            HttpContent httpContent = content;
            httpContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            var response = await client.PostAsync($"/v2/bot/richmenu/{richMenuId}/content", httpContent);

            ////使用本機1
            //var image = System.IO.File.ReadAllBytes(@"C:\MyStorage\TempProject\nopCommerce_book\Presentation\Nop.Web\wwwroot\images\uploaded\main_menu_20220303_01.jpg");
            //HttpContent httpContent = new ByteArrayContent(image);
            //var response = await client.PostAsync($"/v2/bot/richmenu/{richMenuId}/content", httpContent);

            //FileStream fs = new FileStream(@"C:\MyStorage\TempProject\nopCommerce_book\Presentation\Nop.Web\wwwroot\images\uploaded\main_menu_20220303_01.jpg", FileMode.Open);
            //HttpContent fc = new StreamContent(fs);
            //fc.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
            ////var mpContent = new MultipartFormDataContent();
            ////mpContent.Add(fc);
            ////var response = await client.PostAsync($"/v2/bot/richmenu/{richMenuId}/content", mpContent);
            //var response = await client.PostAsync($"/v2/bot/richmenu/{richMenuId}/content", fc);

            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, response.Content.ReadAsStringAsync());
            }

            return Ok(response.Content.ReadAsStringAsync());

        }

        /// <summary>
        /// Set default rich menu
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> SetRichMenuDefault(string richMenuId)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.line.me");
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");

            var response = await client.PostAsync($"/v2/bot/user/all/richmenu/{richMenuId}", null);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, response.Content.ReadAsStringAsync());
            }

            return Ok(response.Content.ReadAsStringAsync());
        }


        /// <summary>
        /// Get default rich menu ID
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> GetRichMenuDefault()
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://api.line.me");
                //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
                //string result = await client.GetStringAsync("/v2/bot/user/all/richmenu");
                //return Ok(result);

                var response = await client.GetAsync("/v2/bot/user/all/richmenu");

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(((int)response.StatusCode), response.Content.ReadAsStringAsync());
                }

                return Ok(response.Content.ReadAsStringAsync());

            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }

            
        }

        /// <summary>
        /// Cancel default rich menu
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public async Task<IActionResult> DelRichMenuDefault(string richMenuId)
        {
            var client = _httpClientFactory.CreateClient();
            client.BaseAddress = new Uri("https://api.line.me");
            //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
            client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
            var response = await client.DeleteAsync($"/v2/bot/user/all/richmenu");
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode(500, response.Content.ReadAsStringAsync());
            }

            return Ok(response.Content.ReadAsStringAsync());
        }

        //[Route("/image/{width}/{height}/{*url}")]
        //[Route("/ResizeImage/{fn}/{width}")]
        public IActionResult ResizeImagePath(string fn, int width)
        {
            var sourcrImagePath = PathString.FromUriComponent($"/images/line/{fn}.jpg");
            var targetImagePath = PathString.FromUriComponent($"/images/line/{fn}_{width}.jpg");
            var sourcrFileInfo = _fileProvider.GetFileInfo(sourcrImagePath);
            var targetFileInfo = _fileProvider.GetFileInfo(targetImagePath);

            return Ok(new { p1 = sourcrImagePath, pp1 = sourcrFileInfo.Exists, p2 = targetImagePath, pp2 = targetFileInfo.Exists, p3 = $@"{_env.ContentRootPath}/wwwroot/images/line/{fn}_{width}.jpg" });


        }

        //[Route("/image/{width}/{height}/{*url}")]
        [Route("/ResizeImage/{fn}/{width}")]
        public IActionResult ResizeImage(string fn, int width)
        {
            if (width <= 0)
            {
                return BadRequest();
            }

            var sourcrImagePath = PathString.FromUriComponent($"/images/line/{fn}.jpg");
            var sourcrImagePathIO = $@"{_env.ContentRootPath}/wwwroot/images/line/{fn}.jpg";
            var sourcrFileInfo = _fileProvider.GetFileInfo(sourcrImagePath);
            if (!sourcrFileInfo.Exists)
            {
                return NotFound();
            }

            var targetImagePath = PathString.FromUriComponent($"/images/line/{fn}_{width}.jpg");
            var targetImagePathIO = $@"{_env.ContentRootPath}/wwwroot/images/line/{fn}_{width}.jpg";
            var targetFileInfo = _fileProvider.GetFileInfo(targetImagePath);


            if (!targetFileInfo.Exists)
            {
                using (var image = Image.Load<Rgba32>(sourcrImagePathIO))
                {
                    image.Mutate(x => x.Resize(width, 0));
                    image.Save(targetImagePathIO);
                }
            }

            using (var ms = new MemoryStream())
            {
                using (var stream = sourcrFileInfo.CreateReadStream())
                {
                    var content = System.IO.File.ReadAllBytes(targetImagePathIO);
                    return File(content, "image/jpeg");

                }
            }

        }




        #region 新增Rich Menu的JSON資料

        public IActionResult RichMenuCreateData()
        {
            //JObject richMenuBody = new JObject();

            //JObject size = new JObject();
            //size.Add("width", 2500);
            //size.Add("height", 1686);
            //richMenuBody.Add("size", size);

            //richMenuBody.Add("selected", true);
            //richMenuBody.Add("name", $"service menu");
            //richMenuBody.Add("chatBarText", $"鴿友服務選單");

            //JArray areas = new JArray();

            //for (int i = 0; i < 11; i++)
            //{
            //    JObject area = new JObject();
            //    JObject bounds = new JObject();
            //    JObject action = new JObject();

            //    if (i == 0)
            //    {
            //        bounds.Add("x", 0);
            //        bounds.Add("y", 0);
            //        bounds.Add("width", 2500);
            //        bounds.Add("height", 286);

            //        action.Add("type", "postback");
            //        action.Add("data", "version0720richmenu01");

            //    }
            //    else if (i == 1)
            //    {
            //        bounds.Add("x", 0);
            //        bounds.Add("y", 286);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);

            //        action.Add("type", "uri");
            //        action.Add("uri", "http://www.087788000.tw/about4.php");

            //    }
            //    else if (i == 2)
            //    {
            //        bounds.Add("x", 500);
            //        bounds.Add("y", 286);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);

            //        action.Add("type", "uri");
            //        action.Add("uri", "http://www.087788000.tw/viewforum.php?f=12");

            //    }
            //    else if (i == 3)
            //    {
            //        bounds.Add("x", 1000);
            //        bounds.Add("y", 286);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);

            //        action.Add("type", "uri");
            //        action.Add("uri", "http://www.087788000.tw/viewtopic.php?f=3&t=1133&p=1270#p1270");

            //    }
            //    else if (i == 4)
            //    {
            //        bounds.Add("x", 1500);
            //        bounds.Add("y", 286);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);

            //        action.Add("type", "uri");
            //        action.Add("uri", "http://www.087780212.tw/msg/login_pre.asp");

            //    }
            //    else if (i == 5)
            //    {
            //        bounds.Add("x", 2000);
            //        bounds.Add("y", 286);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);

            //        //https://line.me/R/ti/p/@linenotify
            //        //action.Add("type", "postback");
            //        //action.Add("data", "flymessage");//即時飛訊

            //        action.Add("type", "uri");
            //        action.Add("uri", "https://line.me/R/ti/p/@linenotify");

            //    }
            //    else if (i == 6)
            //    {
            //        bounds.Add("x", 0);
            //        bounds.Add("y", 986);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);

            //        action.Add("type", "uri");
            //        action.Add("uri", "http://www.530520.com.tw/game_index.asp");

            //    }
            //    else if (i == 7)
            //    {
            //        bounds.Add("x", 500);
            //        bounds.Add("y", 986);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);


            //        action.Add("type", "uri");
            //        action.Add("uri", "http://www.087788000.tw/about3.php");

            //    }
            //    else if (i == 8)
            //    {
            //        bounds.Add("x", 1000);
            //        bounds.Add("y", 986);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);

            //        action.Add("type", "uri");
            //        action.Add("uri", "http://www.087788000.tw/viewforum.php?f=9");

            //    }
            //    else if (i == 9)
            //    {
            //        bounds.Add("x", 1500);
            //        bounds.Add("y", 986);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);


            //        action.Add("type", "uri");
            //        action.Add("uri", "http://www.087788000.tw/viewtopic.php?f=7&t=1129&p=1264#p1264");

            //    }
            //    else if (i == 10)
            //    {
            //        bounds.Add("x", 2000);
            //        bounds.Add("y", 986);
            //        bounds.Add("width", 500);
            //        bounds.Add("height", 700);

            //        action.Add("type", "postback");
            //        action.Add("data", "version0720product");

            //    }

            //    area.Add("bounds", bounds);
            //    area.Add("action", action);
            //    areas.Add(area);
            //}

            //richMenuBody.Add("areas", areas);



            //return Ok(JsonConvert.SerializeObject(richMenuBody));

            JObject main = new JObject(
                                new JProperty("type", "carousel"),
                                new JProperty("contents",
                                    new JArray()
                                    {
                                        new JObject(
                                            new JProperty("type", "bubble"),
                                            new JProperty("body",
                                                new JObject(
                                                    new JProperty("type", "box"),
                                                    new JProperty("layout", "vertical"),
                                                    new JProperty("contents",
                                                        new JArray()
                                                        {
                                                            new JObject(
                                                                new JProperty("type", "image"),
                                                                new JProperty("url", "https://scdn.line-apps.com/n/channel_devcenter/img/flexsnapshot/clip/clip1.jpg"),
                                                                new JProperty("size", "full"),
                                                                new JProperty("aspectMode", "cover"),
                                                                new JProperty("aspectRatio", "2:3"),
                                                                new JProperty("gravity", "top")
                                                            ),
                                                            new JObject(
                                                                new JProperty("type", "box"),
                                                                new JProperty("layout", "vertical"),
                                                                new JProperty("contents",
                                                                    new JArray()
                                                                    {
                                                                        new JObject(
                                                                            new JProperty("type", "box"),
                                                                            new JProperty("layout", "vertical"),
                                                                            new JProperty("contents",
                                                                                new JArray()
                                                                                {
                                                                                    new JObject(
                                                                                        new JProperty("type", "text"),
                                                                                        new JProperty("text", "Brown's T-shirts"),
                                                                                        new JProperty("size", "xl"),
                                                                                        new JProperty("color", "#ffffff"),
                                                                                        new JProperty("weight", "bold")
                                                                                    )
                                                                                }
                                                                            )
                                                                        ),
                                                                        new JObject(
                                                                            new JProperty("type", "box"),
                                                                            new JProperty("layout", "baseline"),
                                                                            new JProperty("contents",
                                                                                new JArray()
                                                                                {
                                                                                    new JObject(
                                                                                        new JProperty("type", "text"),
                                                                                        new JProperty("text", "35,800"),
                                                                                        new JProperty("size", "sm"),
                                                                                        new JProperty("color", "#ebebeb"),
                                                                                        new JProperty("flex", 0)
                                                                                    ),
                                                                                    new JObject(
                                                                                        new JProperty("type", "text"),
                                                                                        new JProperty("text", "35,800"),
                                                                                        new JProperty("color", "#ffffffcc"),
                                                                                        new JProperty("decoration", "line-through"),
                                                                                        new JProperty("gravity", "bottom"),
                                                                                        new JProperty("flex", 0),
                                                                                        new JProperty("size", "sm")
                                                                                    )
                                                                                }
                                                                            ),
                                                                            new JProperty("spacing", "lg")
                                                                        ),
                                                                        new JObject(
                                                                            new JProperty("type", "box"),
                                                                            new JProperty("layout", "vertical"),
                                                                            new JProperty("contents",
                                                                                new JArray()
                                                                                {
                                                                                    new JObject(
                                                                                        new JProperty("type", "filler")
                                                                                    ),
                                                                                    new JObject(
                                                                                        new JProperty("type", "box"),
                                                                                        new JProperty("layout", "baseline"),
                                                                                        new JProperty("contents",
                                                                                            new JArray()
                                                                                            {
                                                                                                new JObject(
                                                                                                    new JProperty("type", "filler")
                                                                                                ),
                                                                                                new JObject(
                                                                                                    new JProperty("type", "icon"),
                                                                                                    new JProperty("url", "https://scdn.line-apps.com/n/channel_devcenter/img/flexsnapshot/clip/clip14.png")
                                                                                                ),
                                                                                                new JObject(
                                                                                                    new JProperty("type", "text"),
                                                                                                    new JProperty("text", "Add to cart"),
                                                                                                    new JProperty("color", "#ffffff"),
                                                                                                    new JProperty("flex", 0),
                                                                                                    new JProperty("offsetTop", "-2px")
                                                                                                ),
                                                                                                new JObject(
                                                                                                    new JProperty("type", "filler")
                                                                                                )
                                                                                            }
                                                                                        ),
                                                                                        new JProperty("spacing", "sm")
                                                                                    ),
                                                                                    new JObject(
                                                                                        new JProperty("type", "filler")
                                                                                    )
                                                                                }
                                                                            ),
                                                                            new JProperty("borderWidth", "1px"),
                                                                            new JProperty("cornerRadius", "4px"),
                                                                            new JProperty("spacing", "sm"),
                                                                            new JProperty("borderColor", "#ffffff"),
                                                                            new JProperty("margin", "xxl"),
                                                                            new JProperty("height", "40px")
                                                                        )

                                                                    }
                                                                ),
                                                                new JProperty("position", "absolute"),
                                                                new JProperty("offsetBottom", "0px"),
                                                                new JProperty("offsetStart", "0px"),
                                                                new JProperty("offsetEnd", "0px"),
                                                                new JProperty("backgroundColor", "#03303Acc"),
                                                                new JProperty("paddingAll", "20px"),
                                                                new JProperty("paddingTop", "18px")
                                                            ),
                                                            new JObject(
                                                                new JProperty("type", "box"),
                                                                new JProperty("layout", "vertical"),
                                                                new JProperty("contents",
                                                                    new JArray()
                                                                    {
                                                                        new JObject(
                                                                            new JProperty("type", "text"),
                                                                            new JProperty("text", "SALE"),
                                                                            new JProperty("color", "#ffffff"),
                                                                            new JProperty("align", "center"),
                                                                            new JProperty("size", "xs"),
                                                                            new JProperty("offsetTop", "3px")
                                                                        )
                                                                    }
                                                                ),
                                                                new JProperty("position", "absolute"),
                                                                new JProperty("cornerRadius", "20px"),
                                                                new JProperty("offsetTop", "18px"),
                                                                new JProperty("backgroundColor", "#ff334b"),
                                                                new JProperty("offsetStart", "18px"),
                                                                new JProperty("height", "25px"),
                                                                new JProperty("width", "53px")
                                                            )
                                                        }
                                                    ),
                                                    new JProperty("paddingAll", "0px")
                                                )
                                            )
                                        ),
                                        new JObject(
                                            new JProperty("type", "bubble"),
                                            new JProperty("body",
                                                new JObject(
                                                    new JProperty("type", "0px"),
                                                    new JProperty("layout", "vertical"),
                                                    new JProperty("contents",
                                                        new JArray()
                                                        {
                                                            new JObject(
                                                                new JProperty("type", "image"),
                                                                new JProperty("url", "https://scdn.line-apps.com/n/channel_devcenter/img/flexsnapshot/clip/clip2.jpg"),
                                                                new JProperty("size", "full"),
                                                                new JProperty("aspectMode", "cover"),
                                                                new JProperty("aspectRatio", "2:3"),
                                                                new JProperty("gravity", "top")
                                                            ),
                                                            new JObject(
                                                                new JProperty("type", "box"),
                                                                new JProperty("layout", "vertical"),
                                                                new JProperty("contents",
                                                                    new JArray()
                                                                    {
                                                                        new JObject(
                                                                            new JProperty("type", "box"),
                                                                            new JProperty("layout", "vertical"),
                                                                            new JProperty("contents",
                                                                                new JArray()
                                                                                {
                                                                                    new JObject(
                                                                                        new JProperty("type", "text"),
                                                                                        new JProperty("text", "Cony's T-shirts"),
                                                                                        new JProperty("size", "xl"),
                                                                                        new JProperty("color", "#ffffff"),
                                                                                        new JProperty("weight", "bold")
                                                                                    )
                                                                                }
                                                                            )
                                                                        ),
                                                                        new JObject(
                                                                            new JProperty("type", "box"),
                                                                            new JProperty("layout", "baseline"),
                                                                            new JProperty("contents",
                                                                                new JArray()
                                                                                {
                                                                                    new JObject(
                                                                                        new JProperty("type", "text"),
                                                                                        new JProperty("text", "35,800"),
                                                                                        new JProperty("size", "sm"),
                                                                                        new JProperty("color", "#ebebeb"),
                                                                                        new JProperty("flex", 0)
                                                                                    ),
                                                                                    new JObject(
                                                                                        new JProperty("type", "text"),
                                                                                        new JProperty("text", "35,800"),
                                                                                        new JProperty("color", "#ffffffcc"),
                                                                                        new JProperty("decoration", "line-through"),
                                                                                        new JProperty("gravity", "bottom"),
                                                                                        new JProperty("flex", 0),
                                                                                        new JProperty("size", "sm")
                                                                                    )
                                                                                }
                                                                            ),
                                                                            new JProperty("spacing", "lg")
                                                                        ),
                                                                        new JObject(
                                                                            new JProperty("type", "box"),
                                                                            new JProperty("layout", "vertical"),
                                                                            new JProperty("contents",
                                                                                new JArray()
                                                                                {
                                                                                    new JObject(
                                                                                        new JProperty("type", "filler")
                                                                                    ),
                                                                                    new JObject(
                                                                                        new JProperty("type", "box"),
                                                                                        new JProperty("layout", "baseline"),
                                                                                        new JProperty("contents",
                                                                                            new JArray()
                                                                                            {
                                                                                                new JObject(
                                                                                                    new JProperty("type", "filler")
                                                                                                ),
                                                                                                new JObject(
                                                                                                    new JProperty("type", "icon"),
                                                                                                    new JProperty("url", "https://scdn.line-apps.com/n/channel_devcenter/img/flexsnapshot/clip/clip14.png")
                                                                                                ),
                                                                                                new JObject(
                                                                                                    new JProperty("type", "text"),
                                                                                                    new JProperty("text", "Add to cart"),
                                                                                                    new JProperty("color", "#ffffff"),
                                                                                                    new JProperty("flex", 0),
                                                                                                    new JProperty("offsetTop", "-2px")
                                                                                                ),
                                                                                                new JObject(
                                                                                                    new JProperty("type", "filler")
                                                                                                )
                                                                                            }
                                                                                        ),
                                                                                        new JProperty("spacing", "sm")
                                                                                    ),
                                                                                    new JObject(
                                                                                        new JProperty("type", "filler")
                                                                                    )
                                                                                }
                                                                            ),
                                                                            new JProperty("borderWidth", "1px"),
                                                                            new JProperty("cornerRadius", "4px"),
                                                                            new JProperty("spacing", "sm"),
                                                                            new JProperty("borderColor", "#ffffff"),
                                                                            new JProperty("margin", "xxl"),
                                                                            new JProperty("height", "40px")
                                                                        )

                                                                    }
                                                                ),
                                                                new JProperty("position", "absolute"),
                                                                new JProperty("offsetBottom", "0px"),
                                                                new JProperty("offsetStart", "0px"),
                                                                new JProperty("offsetEnd", "0px"),
                                                                new JProperty("backgroundColor", "#9C8E7Ecc"),
                                                                new JProperty("paddingAll", "20px"),
                                                                new JProperty("paddingTop", "18px")
                                                            ),
                                                            new JObject(
                                                                new JProperty("type", "box"),
                                                                new JProperty("layout", "vertical"),
                                                                new JProperty("contents",
                                                                    new JArray()
                                                                    {
                                                                        new JObject(
                                                                            new JProperty("type", "text"),
                                                                            new JProperty("text", "SALE"),
                                                                            new JProperty("color", "#ffffff"),
                                                                            new JProperty("align", "center"),
                                                                            new JProperty("size", "xs"),
                                                                            new JProperty("offsetTop", "3px")
                                                                        )
                                                                    }
                                                                ),
                                                                new JProperty("position", "absolute"),
                                                                new JProperty("cornerRadius", "20px"),
                                                                new JProperty("offsetTop", "18px"),
                                                                new JProperty("backgroundColor", "#ff334b"),
                                                                new JProperty("offsetStart", "18px"),
                                                                new JProperty("height", "25px"),
                                                                new JProperty("width", "53px")
                                                            )
                                                        }
                                                    ),
                                                    new JProperty("paddingAll", "0px")
                                                )
                                            )
                                        )
                                    }
                                )
                            );

            JObject flex = new JObject();
            flex.Add("type", "flex");
            flex.Add("altText", "this is a flex message");
            flex.Add("contents", main);

            return Ok(JsonConvert.SerializeObject(flex));

        }

        #endregion

        [NonAction]
        private async Task<string> Get_rich_menu_ID_of_user(string userId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://api.line.me");
                //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
                var response = await client.GetAsync($"/v2/bot/user/{userId}/richmenu");

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }
                else
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(responseContent);
                    return (string)json["richMenuId"];
                }
            }
            catch
            {
                return null;
            }

        }

        [NonAction]
        private async Task<bool> Link_rich_menu_to_user(string userId, string richMenuId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://api.line.me");
                //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                //JObject postBody = new JObject();
                //HttpContent httpContent = new StringContent(JsonConvert.SerializeObject(postBody), Encoding.UTF8, "application/json");
                //var response = await client.PostAsync($"/v2/bot/user/{userId}/richmenu/{richMenuId}", httpContent);

                var response = await client.PostAsync($"/v2/bot/user/{userId}/richmenu/{richMenuId}", null);

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

        }

        [NonAction]
        private async Task<bool> Unlink_rich_menu_from_user(string userId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.BaseAddress = new Uri("https://api.line.me");
                //client.DefaultRequestHeaders.Add("Authorization", "Bearer  " + token);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer  zrJbyjI4lSNKX1w2I/3mNjGdVBrOQ7OlRvJZp16IYmUSsnK2UlPZORm8+GIqWhyEQGBbhrZ0kZ6H1mDyarz6xmWqoTavhXNfhnmKKJFetSSgwEuZmxsgpAmzJwRGTVJJeJ33mXJA9xt2GxxESOFqWQdB04t89/1O/w1cDnyilFU=");
                var response = await client.DeleteAsync($"/v2/bot/user/{userId}/richmenu");

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

        }


    }
}
