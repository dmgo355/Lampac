﻿using Microsoft.AspNetCore.Mvc;
using Lampac.Engine;
using Lampac.Engine.CORE;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using System;
using IO = System.IO;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Linq;

namespace Lampac.Controllers
{
    public class ApiController : BaseController
    {
        [Route("/")]
        public ActionResult Index()
        {
            if (!string.IsNullOrWhiteSpace(AppInit.conf.LampaWeb.index) && IO.File.Exists($"wwwroot/{AppInit.conf.LampaWeb.index}"))
                return LocalRedirect($"/{AppInit.conf.LampaWeb.index}");

            return Content("api work", contentType: "text/plain; charset=utf-8");
        }

        #region app.min.js
        [Route("lampa-{type}/app.min.js")]
        public ActionResult LampaApp(string type)
        {
            if (!memoryCache.TryGetValue($"ApiController:{type}:{host}:app.min.js", out string file))
            {
                file = IO.File.ReadAllText($"wwwroot/lampa-{type}/app.min.js");

                file = file.Replace("http://lite.lampa.mx", $"{host}/lampa-{type}");
                file = file.Replace("https://yumata.github.io/lampa-lite", $"{host}/lampa-{type}");

                file = file.Replace("http://lampa.mx", $"{host}/lampa-{type}");
                file = file.Replace("https://yumata.github.io/lampa", $"{host}/lampa-{type}");

                memoryCache.Set($"ApiController:{type}:app.min.js", file, DateTime.Now.AddMinutes(5));
            }

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region samsung.wgt
        [HttpGet]
        [Route("samsung.wgt")]
        public ActionResult SamsWgt(string overwritehost)
        {
            if (!IO.File.Exists("widgets/samsung/loader.js"))
                return Content(string.Empty);

            string wgt = $"widgets/{CrypTo.md5(overwritehost ?? host + "v2")}.wgt";
            if (IO.File.Exists(wgt))
                return File(IO.File.OpenRead(wgt), "application/octet-stream");

            string loader = IO.File.ReadAllText("widgets/samsung/loader.js");
            IO.File.WriteAllText("widgets/samsung/publish/loader.js", loader.Replace("{localhost}", overwritehost ?? host));

            string app = IO.File.ReadAllText("widgets/samsung/app.js");
            IO.File.WriteAllText("widgets/samsung/publish/app.js", app.Replace("{localhost}", overwritehost ?? host));

            IO.File.Copy("widgets/samsung/icon.png", "widgets/samsung/publish/icon.png", overwrite: true);
            IO.File.Copy("widgets/samsung/logo_appname_fg.png", "widgets/samsung/publish/logo_appname_fg.png", overwrite: true);
            IO.File.Copy("widgets/samsung/config.xml", "widgets/samsung/publish/config.xml", overwrite: true);

            string gethash(string file)
            {
                using (SHA512 sha = SHA512.Create())
                {
                    return Convert.ToBase64String(sha.ComputeHash(IO.File.ReadAllBytes(file)));
                    //digestValue = hash.Remove(76) + "\n" + hash.Remove(0, 76);
                }
            }

            string loaderhashsha512 = gethash("widgets/samsung/publish/loader.js");
            string apphashsha512 = gethash("widgets/samsung/publish/app.js");
            string confighashsha512 = gethash("widgets/samsung/publish/config.xml");
            string iconhashsha512 = gethash("widgets/samsung/publish/icon.png");
            string logohashsha512 = gethash("widgets/samsung/publish/logo_appname_fg.png");

            string author_sigxml = IO.File.ReadAllText("widgets/samsung/author-signature.xml");
            author_sigxml = author_sigxml.Replace("loaderhashsha512", loaderhashsha512).Replace("apphashsha512", apphashsha512)
                                         .Replace("iconhashsha512", iconhashsha512).Replace("logohashsha512", logohashsha512)
                                         .Replace("confighashsha512", confighashsha512);
            IO.File.WriteAllText("widgets/samsung/publish/author-signature.xml", author_sigxml);

            string authorsignaturehashsha512 = gethash("widgets/samsung/publish/author-signature.xml");
            string sigxml1 = IO.File.ReadAllText("widgets/samsung/signature1.xml");
            sigxml1 = sigxml1.Replace("loaderhashsha512", loaderhashsha512).Replace("apphashsha512", apphashsha512)
                             .Replace("confighashsha512", confighashsha512).Replace("authorsignaturehashsha512", authorsignaturehashsha512)
                             .Replace("iconhashsha512", iconhashsha512).Replace("logohashsha512", logohashsha512);
            IO.File.WriteAllText("widgets/samsung/publish/signature1.xml", sigxml1);

            ZipFile.CreateFromDirectory("widgets/samsung/publish/", wgt);

            return File(IO.File.OpenRead(wgt), "application/octet-stream");
        }
        #endregion

        #region MSX
        [HttpGet]
        [Route("msx/start.json")]
        public ActionResult MSX()
        {
            if (!memoryCache.TryGetValue("ApiController:msx.json", out string file))
            {
                file = IO.File.ReadAllText("msx.json");
                memoryCache.Set("ApiController:msx.json", file, DateTime.Now.AddMinutes(5));
            }

            file = file.Replace("{localhost}", host);
            return Content(file, contentType: "application/json; charset=utf-8");
        }
        #endregion

        #region tmdbproxy.js
        [HttpGet]
        [Route("tmdbproxy.js")]
        async public Task<ActionResult> TmdbProxy()
        {
            if (!memoryCache.TryGetValue("ApiController:tmdbproxy.js", out string file))
            {
                file = await IO.File.ReadAllTextAsync("plugins/tmdbproxy.js");
                memoryCache.Set("ApiController:tmdbproxy.js", file, DateTime.Now.AddMinutes(5));
            }

            return Content(file.Replace("{localhost}", host), contentType: "application/javascript; charset=utf-8");
        }
        #endregion

        #region lampainit.js
        [HttpGet]
        [Route("lampainit.js")]
        public ActionResult LamInit(bool lite)
        {
            if (!memoryCache.TryGetValue($"ApiController:lampainit.js:{lite}", out string file))
            {
                file = IO.File.ReadAllText($"plugins/{(lite ? "liteinit" : "lampainit")}.js");
                memoryCache.Set($"ApiController:lampainit.js:{lite}", file, DateTime.Now.AddMinutes(5));
            }

            string initiale = string.Empty;

            if (AppInit.modules != null)
            {
                if (lite)
                {
                    if (AppInit.conf.LampaWeb.initPlugins.online && AppInit.modules.FirstOrDefault(i => i.dll == "Online.dll" && i.enable) != null)
                        initiale += "\"{localhost}/lite.js\",";

                    if (AppInit.conf.LampaWeb.initPlugins.sisi && AppInit.modules.FirstOrDefault(i => i.dll == "SISI.dll" && i.enable) != null)
                        initiale += "\"{localhost}/sisi.js?lite=true\",";
                }
                else
                {
                    if (AppInit.conf.LampaWeb.initPlugins.dlna && AppInit.modules.FirstOrDefault(i => i.dll == "DLNA.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/dlna.js\",\"status\": 1,\"name\": \"DLNA\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.tracks && AppInit.modules.FirstOrDefault(i => i.dll == "Tracks.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/tracks.js\",\"status\": 1,\"name\": \"Tracks.js\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.tmdbProxy)
                        initiale += "{\"url\": \"{localhost}/tmdbproxy.js\",\"status\": 1,\"name\": \"TMDB Proxy\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.online && AppInit.modules.FirstOrDefault(i => i.dll == "Online.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/online.js\",\"status\": 1,\"name\": \"Онлайн\",\"author\": \"lampac\"},";

                    if (AppInit.conf.LampaWeb.initPlugins.sisi && AppInit.modules.FirstOrDefault(i => i.dll == "SISI.dll" && i.enable) != null)
                        initiale += "{\"url\": \"{localhost}/sisi.js\",\"status\": 1,\"name\": \"Клубничка\",\"author\": \"lampac\"},";
                }
            }

            file = file.Replace("{initiale}", Regex.Replace(initiale, ",$", ""));
            file = file.Replace("{localhost}", host);

            if (AppInit.modules != null && (AppInit.modules.FirstOrDefault(i => i.dll == "Jackett.dll" && i.enable) != null || AppInit.modules.FirstOrDefault(i => i.dll == "JacRed.dll" && i.enable) != null))
                file = file.Replace("{jachost}", Regex.Replace(host, "^https?://", ""));
            else
                file = file.Replace("{jachost}", string.Empty);

            return Content(file, contentType: "application/javascript; charset=utf-8");
        }
        #endregion
    }
}