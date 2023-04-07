﻿using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Web;
using Lampac.Engine;
using Lampac.Engine.CORE;
using Lampac.Models.LITE.Kinobase;
using System.Linq;
using Shared.Engine.reCAPTCHA;

namespace Lampac.Controllers.LITE
{
    public class Kinobase : BaseController
    {
        [HttpGet]
        [Route("lite/kinobase")]
        async public Task<ActionResult> Index(string title, int year, int s = -1)
        {
            if (!AppInit.conf.Kinobase.enable)
                return Content(string.Empty);

            string content = await embed(title, year);
            if (content == null)
                return Content(string.Empty);

            bool firstjson = true;
            string html = "<div class=\"videos__line\">";

            #region getSubtitle
            string getSubtitle(string _sub)
            {
                if (string.IsNullOrWhiteSpace(_sub))
                    return string.Empty;

                string subtitles = string.Empty;
                var match = new Regex("\\[([^\\]]+)\\](https?://[^\\,\\[\\| ]+\\.vtt)").Match(_sub);
                while (match.Success)
                {
                    if (!string.IsNullOrWhiteSpace(match.Groups[1].Value) && !string.IsNullOrWhiteSpace(match.Groups[2].Value))
                    {
                        string suburl = HostStreamProxy(AppInit.conf.Kinobase.streamproxy, match.Groups[2].Value);
                        subtitles += "{\"label\": \"" + match.Groups[1].Value + "\",\"url\": \"" + suburl + "\"},";
                    }

                    match = match.NextMatch();
                }

                subtitles = Regex.Replace(subtitles, ",$", "");
                return subtitles;
            }
            #endregion

            #region getStreamLink
            string getStreamLink(string _data)
            {
                foreach (var quality in new List<string> { "2160", "2060", "1440", "1080", "720", "480", "360", "240" })
                {
                    string file = new Regex($"\\[{quality}p?\\]" + "(\\{[^\\}]+\\})?([^\\[\\|,\n\r\t ]+.m3u8)").Match(_data).Groups[2].Value;
                    if (string.IsNullOrEmpty(file))
                        continue;

                    return HostStreamProxy(AppInit.conf.Kinobase.streamproxy, file);
                }

                return _data;
            }
            #endregion

            if (content.Contains("file|"))
            {
                #region Фильм
                string subtitles = getSubtitle(content);

                if (content.Contains("]{") && content.Contains(";"))
                {
                    foreach (var quality in new List<string> { "2160", "2060", "1440", "1080", "720", "480", "360", "240" })
                    {
                        var g = new Regex($"\\[{quality}p?\\]([^\\[\\|\n\r,]+)").Match(content).Groups;
                        if (string.IsNullOrEmpty(g[1].Value))
                            continue;

                        bool end = false;
                        var smatch = new Regex("\\{([^\\}]+)\\}(https?://[^\\[\\|;\n\r\t ]+.m3u8)").Match(g[1].Value);
                        while (smatch.Success)
                        {
                            if (!string.IsNullOrWhiteSpace(smatch.Groups[1].Value) && !string.IsNullOrWhiteSpace(smatch.Groups[2].Value))
                            {
                                string url = HostStreamProxy(AppInit.conf.Kinobase.streamproxy, smatch.Groups[2].Value);
                                html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + url + "\",\"title\":\"" + title + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + smatch.Groups[1].Value + "</div></div>";
                                end = true;
                                firstjson = true;
                            }

                            smatch = smatch.NextMatch();
                        }

                        if (end)
                            break;
                    }
                }
                else
                {
                    foreach (var quality in new List<string> { "2160", "2060", "1440", "1080", "720", "480", "360", "240" })
                    {
                        string hls = new Regex($"\\[{quality}p?\\]" + "(\\{[^\\}]+\\})?(https?://[^\\[\\|,;\n\r\t ]+.m3u8)").Match(content).Groups[2].Value;
                        if (!string.IsNullOrEmpty(hls))
                        {
                            hls = HostStreamProxy(AppInit.conf.Kinobase.streamproxy, hls);
                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" data-json='{\"method\":\"play\",\"url\":\"" + hls + "\",\"title\":\"" + title + "\", \"subtitles\": [" + subtitles + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + quality + "p</div></div>";
                            firstjson = true;
                        }
                    }
                }
                #endregion
            }
            else
            {
                #region Сериал
                try
                {
                    var root = JsonConvert.DeserializeObject<List<Season>>(Regex.Match(content, "^pl\\|(\\[[^\n\r]+\\])").Groups[1].Value);

                    if (s == -1)
                    {
                        for (int i = 0; i < root.Count; i++)
                        {
                            var season = root[i];  
                            if (season?.playlist != null && season.playlist.Count > 0)
                            {
                                string link = $"{host}/lite/kinobase?title={HttpUtility.UrlEncode(title)}&year={year}&s={i}";

                                html += "<div class=\"videos__item videos__season selector " + (firstjson ? "focused" : "") + "\" data-json='{\"method\":\"link\",\"url\":\"" + link + "\"}'><div class=\"videos__season-layers\"></div><div class=\"videos__item-imgbox videos__season-imgbox\"><div class=\"videos__item-title videos__season-title\">" + (season.comment ?? season.title) + "</div></div></div>";
                                firstjson = false;
                            }
                            else
                            {
                                if (season.file == null)
                                    continue;

                                html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"1\" e=\"" + Regex.Match(season.comment ?? season.title, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + getStreamLink(season.file) + "\",\"title\":\"" + title + "\", \"subtitles\": [" + getSubtitle(season.subtitle) + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + (season.comment ?? season.title) + "</div></div>";
                                firstjson = false;
                            }
                        }
                    }
                    else
                    {
                        string nameseason = Regex.Match(root[s].comment ?? root[s].title, "^([0-9]+)").Groups[1].Value;

                        foreach (var episode in root[s].playlist)
                        {
                            html += "<div class=\"videos__item videos__movie selector " + (firstjson ? "focused" : "") + "\" media=\"\" s=\"" + nameseason + "\" e=\"" + Regex.Match(episode.comment ?? episode.title, "^([0-9]+)").Groups[1].Value + "\" data-json='{\"method\":\"play\",\"url\":\"" + getStreamLink(episode.file) + "\",\"title\":\"" + $"{title} ({episode.comment ?? episode.title})" + "\", \"subtitles\": [" + getSubtitle(episode.subtitle) + "]}'><div class=\"videos__item-imgbox videos__movie-imgbox\"></div><div class=\"videos__item-title\">" + (episode.comment ?? episode.title) + "</div></div>";
                            firstjson = false;
                        }
                    }
                }
                catch 
                {
                    return Content(string.Empty);
                }
                #endregion
            }

            return Content(html + "</div>", "text/html; charset=utf-8");
        }


        [HttpGet]
        [Route("lite/kinobase/check")]
        async public Task<ActionResult> Check()
        {
            if (string.IsNullOrWhiteSpace(AppInit.conf.anticaptchakey))
                return Content("anticaptchakey");

            string location = await HttpClient.GetLocation("https://kinobase.org/films");
            if (location == null || !location.Contains("/check"))
                return Content("location ok");

            reCAPTCHAv2 api = new reCAPTCHAv2
            {
                ClientKey = AppInit.conf.anticaptchakey,
                WebsiteUrl = new Uri($"{AppInit.conf.Kinobase.host}/check"),
                WebsiteKey = "6Ld5MCUTAAAAALXvmUFVUdqxSgy9a8Kf3zeVvGEJ"
            };

            if (!api.CreateTask())
                return Content("API v2 send failed. " + api.ErrorMessage);
            else if (!api.WaitForResult())
                return Content("Could not solve the captcha.");
            else
            {
                string result = null;
                string googleReCaptchaResponse = api.GetTaskSolution().GRecaptchaResponse;
                if (googleReCaptchaResponse != null)
                    result = await HttpClient.Post($"{AppInit.conf.Kinobase.host}/check", $"g-recaptcha-response={googleReCaptchaResponse}&return_uri=%2F", timeoutSeconds: 8);

                return Content(result ?? "null");
            }
        }


        #region embed
        async ValueTask<string> embed(string title, int year)
        {
            if (string.IsNullOrWhiteSpace(title) || year == 0)
                return null;

            string memKey = $"kinobase:view:{title}:{year}";

            if (!memoryCache.TryGetValue(memKey, out string content))
            {
                System.Net.WebProxy proxy = null;
                if (AppInit.conf.Kinobase.useproxy)
                    proxy = HttpClient.webProxy();

                content = await HttpClient.Get($"{AppInit.conf.Kinobase.host}/search?query={HttpUtility.UrlEncode(title)}", timeoutSeconds: 8, proxy: proxy);

                string link = null, reservedlink = null;
                foreach (string row in content.Split("<div class=\"col-xs-2 item\">").Skip(1))
                {
                    if (row.Contains(">Трейлер</span>"))
                        continue;

                    var g = Regex.Match(row, "class=\"link\" alt=\"([^\"]+) \\(([0-9]{4})\\)\"").Groups;

                    if (g[1].Value.ToLower().Trim() == title.ToLower())
                    {
                        reservedlink = Regex.Match(row, "href=\"/([^\"]+)\"").Groups[1].Value;
                        if (string.IsNullOrWhiteSpace(reservedlink))
                            continue;

                        if (g[2].Value == year.ToString())
                        {
                            link = reservedlink;
                            break;
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(link))
                {
                    if (string.IsNullOrWhiteSpace(reservedlink))
                        return null;

                    link = reservedlink;
                }

                string news = await HttpClient.Get($"{AppInit.conf.Kinobase.host}/{link}", timeoutSeconds: 8, proxy: proxy);
                if (news == null)
                    return null;

                string MOVIE_ID = new Regex("var MOVIE_ID = ([0-9]+)").Match(news).Groups[1].Value;
                string IDENTIFIER = new Regex("var IDENTIFIER = \"([^\"]+)").Match(news).Groups[1].Value;
                string PLAYER_CUID = new Regex("var PLAYER_CUID = \"([^\"]+)").Match(news).Groups[1].Value;

                string userdata = await HttpClient.Get($"{AppInit.conf.Kinobase.host}/user_data?page=movie&movie_id={MOVIE_ID}&cuid={PLAYER_CUID}&device=DESKTOP&_=1656153006095", timeoutSeconds: 8, proxy: proxy);
                if (userdata == null)
                    return null;

                string VOD_HASH = new Regex("\"vod_hash\":\"([^\"]+)").Match(userdata).Groups[1].Value;
                string VOD_TIME = new Regex("\"vod_time\":([0-9]+)").Match(userdata).Groups[1].Value;

                content = await HttpClient.Get($"{AppInit.conf.Kinobase.host}/vod/{MOVIE_ID}?identifier={IDENTIFIER}&player_type=new&file_type=hls&st={VOD_HASH}&e={VOD_TIME}", timeoutSeconds: 8, proxy: proxy);
                if (content == null)
                    return null;

                memoryCache.Set(memKey, content, DateTime.Now.AddMinutes(AppInit.conf.multiaccess ? 20 : 10));
            }

            return content;
        }
        #endregion
    }
}