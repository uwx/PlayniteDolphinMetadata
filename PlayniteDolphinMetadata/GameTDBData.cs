using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Linq;
using Playnite.SDK;

namespace PlayniteDolphinMetadata
{
    public class GameTDBData
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        public string Id { get; set; }
        public string Platform { get; set; } // "type"
        public string Region { get; set; }
        public string[] Languages { get; set; } // comma-separated
        public IDictionary<string, string> Titles { get; set; } = new Dictionary<string, string>(); // key is the language key
        public IDictionary<string, string> Synopses { get; set; } = new Dictionary<string, string>(); // key is the language key
        public string Developer { get; set; }
        public string Publisher { get; set; }
        public Date ReleaseDate { get; set; }
        public string[] Genre { get; set; } // comma-separated
        public Rating Rating { get; set; }
        public int? Players { get; set; } // input players
        public string[] RequiredAccessories { get; set; } // control type required="true"
        public string[] Accessories { get; set; } // control type required="false"
        public int? OnlinePlayers { get; set; } // wi-fi players
        public string[] OnlineFeatures { get; set; } // <wi-fi><feature>
        public int? SaveBlocks { get; set; }
        public string Version { get; set; } // <rom version
        public ulong? Size { get; set; } // <rom size
        public string RomName { get; set; } // <rom name
        public string Crc { get; set; } // <rom crc
        public string Md5 { get; set; } // <rom md5
        public string Sha1 { get; set; } // <rom sha1

        public string GetCoverUrl(string targetLanguage, string coverType = "cover") // targetLanguage: one of DE FR ES IT NL EN
        {
            string regionCode;

            switch (Region)
            {
                case "NTSC-J": regionCode = "JA"; break;
                case "NTSC-U": regionCode = "US"; break;
                case "NTSC-K": regionCode = "KO"; break;
                case "PAL":
                    var lang = Languages.FirstOrDefault(e => e == targetLanguage) ?? Languages.FirstOrDefault(e => e == "EN") ?? Languages.FirstOrDefault() ?? "EN";
                    regionCode = lang;
                    break;
                default:
                    regionCode = "EN";
                    break;
            }

            Logger.Debug($"Found GameTDB cover image: https://art.gametdb.com/wii/{coverType}/{regionCode}/{Id}.png");
            return $"https://art.gametdb.com/wii/{coverType}/{regionCode}/{Id}.png";
        }

        public string GetTitle(string targetLanguage)
        {
            return Titles.TryGetValue(targetLanguage, out var title)
                ? title
                : Titles.TryGetValue("EN", out title)
                ? title
                : Titles.Values.FirstOrDefault();
            // ?? RomName;
        }

        public string GetSynopsis(string targetLanguage)
        {
            return Synopses.TryGetValue(targetLanguage, out var synopsis)
                ? synopsis
                : Synopses.TryGetValue("EN", out synopsis)
                ? synopsis
                : Synopses.Values.FirstOrDefault();
        }

        public static GameTDBData Parse(XmlElement rootElement)
        {
            var locales = rootElement.GetElementsByTagName("locale").OfType<XmlElement>().ToArray();
            var wifi = GetElementOrNull(rootElement, "wi-fi");
            var input = GetElementOrNull(rootElement, "input");
            var control = input?.GetElementsByTagName("control")
                    .OfType<XmlElement>()
                    .ToArray()
                    ?? Array.Empty<XmlElement>();
            var rom = GetElementOrNull(rootElement, "rom");

            return new GameTDBData
            {
                // <id>
                Id = GetElementText(rootElement, "id"),

                // <type>
                Platform = GetElementText(rootElement, "type"),

                // <region>
                Region = GetElementText(rootElement, "region"),

                // <languages>
                Languages = GetElementText(rootElement, "languages")?.Split(',') ?? Array.Empty<string>(),

                // <locale> (multiple elements)
                Titles = locales
                    .Select(e => {
                        var titleList = e.GetElementsByTagName("title");
                        return (lang: e.GetAttribute("lang"), title: titleList.Count > 0 ? titleList[0].InnerText : null);
                    })
                    .Where(e => e.title != null)
                    .ToDictionary(e => e.lang, e => e.title),
                Synopses = locales
                    .Select(e => {
                        var titleList = e.GetElementsByTagName("synopsis");
                        return (lang: e.GetAttribute("lang"), title: titleList.Count > 0 ? titleList[0].InnerText : null);
                    })
                    .Where(e => e.title != null)
                    .ToDictionary(e => e.lang, e => e.title),

                // <developer>
                Developer = GetElementText(rootElement, "developer"),

                // <publisher>
                Publisher = GetElementText(rootElement, "publisher"),

                // <date>
                ReleaseDate = GetElementOrNull(rootElement, "date") is XmlElement date ? Date.Parse(date) : null,

                // <genre>
                Genre = GetElementText(rootElement, "genre")?.Split(',') ?? Array.Empty<string>(),

                // <rating>
                Rating = GetElementOrNull(rootElement, "rating") is XmlElement rating ? Rating.Parse(rating) : null,

                // <input>
                Players = int.TryParse(input.GetAttribute("players"), out var players) ? (int?)players : null,
                RequiredAccessories = control
                    .Where(e => e.GetAttribute("required") == "true")
                    .Select(e => e.GetAttribute("type"))
                    .ToArray(),
                Accessories = control
                    .Where(e => e.GetAttribute("required") != "true")
                    .Select(e => e.GetAttribute("type"))
                    .ToArray(),

                // <wi-fi>
                OnlinePlayers = int.TryParse(wifi.GetAttribute("players"), out var playersWifi) ? (int?)playersWifi : null,
                OnlineFeatures = wifi?.GetElementsByTagName("feature")
                    .OfType<XmlElement>()
                    .Select(e => e.InnerText)
                    .ToArray()
                    ?? Array.Empty<string>(),

                // <save>
                SaveBlocks = int.TryParse(GetElementOrNull(rootElement, "save")?.GetAttribute("blocks"), out var blocks) ? (int?)blocks : null,

                // <rom>
                Version = rom?.GetAttribute("version"),
                Size = ulong.TryParse(rom?.GetAttribute("size"), out var size) ? (ulong?) size : null,
                RomName = rom?.GetAttribute("name"),
                Crc = rom?.GetAttribute("crc"),
                Md5 = rom?.GetAttribute("md5"),
                Sha1 = rom?.GetAttribute("sha1"),
            };
        }

        internal static string GetElementText(XmlElement el, string tagName)
        {
            var res = el.GetElementsByTagName(tagName);
            return res.Count > 0 ? res[0].InnerText : null;
        }

        internal static XmlElement GetElementOrNull(XmlElement el, string tagName)
        {
            var res = el.GetElementsByTagName(tagName);
            return res.Count > 0 ? res[0] as XmlElement : null;
        }
    }

    public class Rating
    {
        public string Type { get; set; }
        public string TheRating { get; set; } // "value"
        public string Descriptor { get; set; }

        public static Rating Parse(XmlElement xmlElement)
        {
            return new Rating
            {
                Type = xmlElement.GetAttribute("type"),
                TheRating = xmlElement.GetAttribute("value"),
                Descriptor = GameTDBData.GetElementText(xmlElement, "descriptor")
            };
        }
    }

    public class Date
    {
        public int? Year { get; set; }
        public int? Month { get; set; }
        public int? Day { get; set; }

        public static Date Parse(XmlElement xmlElement)
        {
            return new Date
            {
                Year = int.TryParse(xmlElement.GetAttribute("year"), out var year) ? (int?) year : null,
                Month = int.TryParse(xmlElement.GetAttribute("month"), out var month) ? (int?)month : null,
                Day = int.TryParse(xmlElement.GetAttribute("day"), out var day) ? (int?)day : null,
            };
        }
    }
}