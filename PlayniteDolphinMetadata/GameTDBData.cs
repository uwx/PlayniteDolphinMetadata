using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Xml;
using System.Linq;
using JetBrains.Annotations;
using Playnite.SDK;

namespace System.Runtime.CompilerServices
{
    [ExcludeFromCodeCoverage, DebuggerNonUserCode, UsedImplicitly]
    internal static class IsExternalInit {}
}

namespace PlayniteDolphinMetadata
{
    public class GameTdbData
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        public string? Id { get; init; }
        public string? Platform { get; init; } // "type"
        public string? Region { get; init; }
        public string[] Languages { get; init; } = null!; // comma-separated
        public IDictionary<string, string?> Titles { get; init; } = new Dictionary<string, string?>(); // key is the language key
        public IDictionary<string, string?> Synopses { get; init; } = new Dictionary<string, string?>(); // key is the language key
        public string? Developer { get; init; }
        public string? Publisher { get; init; }
        public Date? ReleaseDate { get; init; }
        public string[] Genre { get; init; } = null!; // comma-separated
        public Rating? Rating { get; init; }
        public int? Players { get; init; } // input players
        public string[] RequiredAccessories { get; init; } = null!; // control type required="true"
        public string[] Accessories { get; init; } = null!; // control type required="false"
        public int? OnlinePlayers { get; init; } // wi-fi players
        public string[] OnlineFeatures { get; init; } = null!; // <wi-fi><feature>
        public int? SaveBlocks { get; init; }
        public string? Version { get; init; } // <rom version
        public ulong? Size { get; init; } // <rom size
        public string? RomName { get; init; } // <rom name
        public string? Crc { get; init; } // <rom crc
        public string? Md5 { get; init; } // <rom md5
        public string? Sha1 { get; init; } // <rom sha1

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

        public string? GetTitle(string targetLanguage)
        {
            return Titles.TryGetValue(targetLanguage, out var title)
                ? title
                : Titles.TryGetValue("EN", out title)
                ? title
                : Titles.Values.FirstOrDefault();
            // ?? RomName;
        }

        public string? GetSynopsis(string targetLanguage)
        {
            return Synopses.TryGetValue(targetLanguage, out var synopsis)
                ? synopsis
                : Synopses.TryGetValue("EN", out synopsis)
                ? synopsis
                : Synopses.Values.FirstOrDefault();
        }

        public static GameTdbData Parse(XmlElement rootElement)
        {
            var locales = rootElement.GetElementsByTagName("locale").OfType<XmlElement>().ToArray();
            var wifi = GetElementOrNull(rootElement, "wi-fi");
            var input = GetElementOrNull(rootElement, "input");
            var control = input?.GetElementsByTagName("control")
                    .OfType<XmlElement>()
                    .ToArray()
                    ?? Array.Empty<XmlElement>();
            var rom = GetElementOrNull(rootElement, "rom");

            return new GameTdbData
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
                    .Select(e => (lang: e.GetAttribute("lang"), title: GetElementText(e, "title")))
                    .Where(e => e.title != null)
                    .ToDictionary(e => e.lang, e => e.title),
                Synopses = locales
                    .Select(e => (lang: e.GetAttribute("lang"), title: GetElementText(e, "synopsis")))
                    .Where(e => e.title != null)
                    .ToDictionary(e => e.lang, e => e.title),

                // <developer>
                Developer = GetElementText(rootElement, "developer"),

                // <publisher>
                Publisher = GetElementText(rootElement, "publisher"),

                // <date>
                ReleaseDate = ParseOrNull(Date.Parse, GetElementOrNull(rootElement, "date")),

                // <genre>
                Genre = GetElementText(rootElement, "genre")?.Split(',') ?? Array.Empty<string>(),

                // <rating>
                Rating = ParseOrNull(Rating.Parse, GetElementOrNull(rootElement, "rating")),

                // <input>
                Players = TryParseOrNull<int>(int.TryParse, input?.GetAttribute("players")),
                RequiredAccessories = control
                    .Where(e => e.GetAttribute("required") == "true")
                    .Select(e => e.GetAttribute("type"))
                    .ToArray(),
                Accessories = control
                    .Where(e => e.GetAttribute("required") != "true")
                    .Select(e => e.GetAttribute("type"))
                    .ToArray(),

                // <wi-fi>
                OnlinePlayers = TryParseOrNull<int>(int.TryParse, wifi?.GetAttribute("players")),
                OnlineFeatures = wifi?.GetElementsByTagName("feature")
                    .OfType<XmlElement>()
                    .Select(e => e.InnerText)
                    .ToArray()
                    ?? Array.Empty<string>(),

                // <save>
                SaveBlocks = TryParseOrNull<int>(int.TryParse, GetElementOrNull(rootElement, "save")?.GetAttribute("blocks")),

                // <rom>
                Version = rom?.GetAttribute("version"),
                Size = TryParseOrNull<ulong>(ulong.TryParse, rom?.GetAttribute("size")),
                RomName = rom?.GetAttribute("name"),
                Crc = rom?.GetAttribute("crc"),
                Md5 = rom?.GetAttribute("md5"),
                Sha1 = rom?.GetAttribute("sha1"),
            };
        }

        private static TResult? ParseOrNull<TInput, TResult>(Func<TInput, TResult> parser, TInput? input)
        {
            return input != null ? parser(input) : default;
        }

        private delegate bool TryParse<TOut>(string s, out TOut result);
        private static TResult? TryParseOrNull<TResult>(TryParse<TResult> parser, string? input)
        {
            return input != null && parser(input, out var output) ? output : default;
        }

        internal static string? GetElementText(XmlElement el, string tagName)
        {
            var res = el.GetElementsByTagName(tagName);
            return res.Count > 0 ? res[0].InnerText : null;
        }

        internal static XmlElement? GetElementOrNull(XmlElement el, string tagName)
        {
            var res = el.GetElementsByTagName(tagName);
            return res.Count > 0 ? res[0] as XmlElement : null;
        }
    }

    public class Rating
    {
        public string Type { get; init; } = null!;
        public string TheRating { get; init; } = null!; // "value"
        public string? Descriptor { get; init; }

        public static Rating Parse(XmlElement xmlElement)
        {
            return new Rating
            {
                Type = xmlElement.GetAttribute("type"),
                TheRating = xmlElement.GetAttribute("value"),
                Descriptor = GameTdbData.GetElementText(xmlElement, "descriptor")
            };
        }
    }

    public class Date
    {
        public int? Year { get; init; }
        public int? Month { get; init; }
        public int? Day { get; init; }

        public static Date Parse(XmlElement xmlElement)
        {
            return new Date
            {
                Year = int.TryParse(xmlElement.GetAttribute("year"), out var year) ? year : null,
                Month = int.TryParse(xmlElement.GetAttribute("month"), out var month) ? month : null,
                Day = int.TryParse(xmlElement.GetAttribute("day"), out var day) ? day : null,
            };
        }
    }
}