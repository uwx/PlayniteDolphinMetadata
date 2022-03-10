using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Xml;
using Playnite.SDK;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;

namespace PlayniteDolphinMetadata
{
    public class DolphinMetadataProvider : OnDemandMetadataProvider
    {
        private static readonly ILogger Logger = LogManager.GetLogger();
        
        private readonly MetadataRequestOptions _options;
        private readonly IPlayniteAPI _playniteApi;
        private readonly DolphinMetadataSettings _settings;
        private readonly DolphinMetadataPlugin _plugin;
        private readonly string _pluginUserDataPath;
        private readonly XmlDocument _wiiDb;

        private List<MetadataField>? _availableFields;

        private GameTdbData? _gameData;

        public DolphinMetadataProvider(MetadataRequestOptions options, DolphinMetadataPlugin plugin)
        {
            _options = options;
            _playniteApi = plugin.PlayniteApi;
            _plugin = plugin;
            _pluginUserDataPath = plugin.GetPluginUserDataPath(); 
            _settings = plugin.LoadPluginSettings<DolphinMetadataSettings>();
            //DolphinMetadataSettings.MigrateSettingsVersion(_settings, plugin);
            _wiiDb = plugin.GetWiiDb();
        }

        public override void Dispose() {
            base.Dispose();
            _plugin.ReleaseWiiDb();
        }

        public override List<MetadataField> AvailableFields => _availableFields ??= GetAvailableFields();

        private List<MetadataField> GetAvailableFields()
        {
            if (_gameData == null)
                if (!LoadGameData())
                    return new List<MetadataField>();

            var fields = new List<MetadataField>();

            if (_gameData != null)
            {
                fields.Add(MetadataField.CoverImage);

                if (_gameData.GetTitle(_settings.LanguagePreference) != null)
                    fields.Add(MetadataField.Name);
                if (_gameData.GetSynopsis(_settings.LanguagePreference) != null)
                    fields.Add(MetadataField.Description);

                // TODO: Everything else.
            }

            return fields;
        }

        private bool LoadGameData()
        {
            if (_gameData != null) return true;

            // TODO what is this
            //if (_options.IsBackgroundDownload) return false;

            var gamePath = FindGamePath(_options.GameData.Roms);
            if (gamePath == null)
            {
                return false; // Not a ROM at all!
            }
            
            var dllPath = typeof(DolphinMetadataProvider).Assembly.Location;

            Logger.Debug("Getting metadata for " + gamePath);

            switch (Path.GetExtension(gamePath).ToLowerInvariant())
            {
                case ".rvz":
                    try
                    {
                        return LoadGameDataRvz(gamePath);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to get game data from RVZ file: {gamePath}");
                        return false;
                    }
                case ".wad":
                    try
                    {
                        return LoadGameDataWad(gamePath);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to get game data from WAD file: {gamePath}");
                        return false;
                    }
                case ".iso":
                case ".ciso":
                case ".wbi":
                case ".wbfs":
                case ".wdf":
                case ".wia":
                case ".gcz":
                case ".fst":
                    try
                    {
                        return LoadGameDataWit(gamePath, dllPath);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Failed to get game data from WIT-compatible file: {gamePath}");
                        return false;
                    }
                default:
                    Logger.Warn($"Unsupported ROM extension {Path.GetExtension(gamePath).ToLowerInvariant()}");
                    return false;
            }
        }

        // Find the best ROM 
        private static string? FindGamePath(IEnumerable<GameRom> gameRoms)
        {
            return gameRoms.OrderBy(static rom =>
            {
                switch (Path.GetExtension(rom.Path).ToLowerInvariant())
                {
                    case ".rvz":
                    case ".wad":
                        return -1;
                    case ".iso":
                    case ".ciso":
                    case ".wbi":
                    case ".wbfs":
                    case ".wdf":
                    case ".wia":
                    case ".gcz":
                    case ".fst":
                        return 1;
                    default:
                        return -2;
                }
            }).FirstOrDefault()?.Path;
        }

        private bool LoadGameDataRvz(string gamePath)
        {
            using var stream = File.OpenRead(gamePath);
            var bytes = new byte[6];
            
            // the disc header *should* be in a constant position so hardcoding it here is fine
            stream.Position = 0x58;
            stream.Read(bytes, 0, 6);

            if (!ValidateId6(bytes))
            {
                return false;
            }

            var id6 = Encoding.ASCII.GetString(bytes);
            return ParseGameData(id6);
        }

        private bool LoadGameDataWad(string gamePath)
        {
            using var stream = File.OpenRead(gamePath);
            var bytes = new byte[6];
            
            // don't know if this is correct, but it's consistent in all the wad files i've checked
            stream.Position = 0x490;
            stream.Read(bytes, 0, 4); // read id4
            stream.Position = 0x498;
            stream.Read(bytes, 4, 2);

            if (!ValidateId6(bytes))
            {
                return false;
            }

            var id6 = Encoding.ASCII.GetString(bytes);
            return ParseGameData(id6);
        }

        // ID6 must be alphanumeric, and exactly 6 characters (none empty)
        private static bool ValidateId6(IEnumerable<byte> candidateId6)
        {
            return candidateId6.All(static b => (b >= 'A' && b <= 'Z') || (b >= '0' && b <= '9'));
        }

        private bool LoadGameDataWit(string gamePath, string dllPath)
        {
            var witPath = Path.Combine(Path.GetDirectoryName(dllPath) ?? ".", "wit/bin/wit.exe");

            if (!File.Exists(witPath))
            {
                Logger.Error($"WIT: Serious issue! wit.exe not found at {witPath}");
                return false;
            }

            Logger.Debug($"WIT: Launching WIT");

            var sb = new StringBuilder();
            using (var process = new Process())
            {
                process.StartInfo.FileName = witPath;
                process.StartInfo.Arguments = $@"id6 ""{gamePath}""";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.OutputDataReceived += (_, args) => sb.Append(args.Data);
                process.ErrorDataReceived += (_, args) => Logger.Error($"WIT StdErr: {args.Data}");
                process.Start();
                process.BeginOutputReadLine();
                process.WaitForExit();
            }

            var id6 = sb.ToString().Trim();
            return ParseGameData(id6);
        }

        private bool ParseGameData(string id6)
        {
            var games = _wiiDb.GetElementsByTagName("game")
                .OfType<XmlElement>();

            var foundGameElement = games
                .FirstOrDefault(e =>
                {
                    var id = e.GetElementsByTagName("id");
                    return id.Count > 0 && id[0].InnerText == id6;
                });

            if (foundGameElement == null)
            {
                // The Wiidb, for some reason, doesn't like the publisher codes in some games, so try the first 4 chars
                // (This may be a WiiWare thing)
                foundGameElement = games
                    .FirstOrDefault(e =>
                    {
                        var id = e.GetElementsByTagName("id");
                        return id.Count > 0 && id[0].InnerText == id6.Substring(0, 4);
                    });

                if (foundGameElement == null)
                {
                    Logger.Debug($"Did not find ID6 game match in database: " + id6);
                    return false;
                }
            }

            _gameData = GameTdbData.Parse(foundGameElement);
            Logger.Debug($"Found ID6 game match in database: {_gameData.GetTitle("EN") ?? _gameData.RomName}");
            return true;
        }

        public override string? GetName(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Name) && _gameData != null) 
                return _gameData.GetTitle(_settings.LanguagePreference);

            return base.GetName(args);
        }

        public override string? GetDescription(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.Description) && _gameData != null)
                return _gameData.GetSynopsis(_settings.LanguagePreference);

            return base.GetDescription(args);
        }

        public override MetadataFile? GetCoverImage(GetMetadataFieldArgs args)
        {
            if (AvailableFields.Contains(MetadataField.CoverImage) && _gameData != null)
            {
                if (_settings.CoverDownloadPreference.StartsWith("cropped_"))
                {
                    try
                    {
                        FindGoodCover(out var data, out var isInRequestedFormat);
                        if (data == null) return null;

                        return isInRequestedFormat
                            ? new MetadataFile($"{_gameData.Id}.png", CropBoxart(data))
                            : new MetadataFile($"{_gameData.Id}.png", data);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, $"Could not crop image for ID: {_gameData.Id}");
                    }
                }
                else
                {
                    FindGoodCover(out var data, out _);
                    if (data == null) return null;

                    return new MetadataFile($"{_gameData.Id}.png", data);
                }
            }

            return base.GetCoverImage(args);
        }

        /// <summary>
        /// Downloads covers for the current game until a valid one is found.
        /// </summary>
        /// <param name="data">Byte array data for the cover, or <code>null</code> if none found</param>
        /// <param name="isInRequestedFormat"><code>false</code> if the only cover found is not in the format specified in <see cref="DolphinMetadataSettings.CoverDownloadPreference"/></param>
        private void FindGoodCover(out byte[]? data, out bool isInRequestedFormat)
        {
            isInRequestedFormat = true;

            using (var webClient = new WebClient())
            {
                if (TryDownloadCover(_settings.LanguagePreference, webClient, out data)) return;

                if (_settings.LanguagePreference != "EN" && TryDownloadCover("EN", webClient, out data)) return;
                if (_settings.LanguagePreference != "AU" && TryDownloadCover("AU", webClient, out data)) return;

                // Attempt to download regular cover, this usually exists for all entries
                if (_settings.CoverDownloadPreference != "cover" && TryDownloadCover(_settings.LanguagePreference, webClient, out data, "cover"))
                {
                    isInRequestedFormat = false;
                    return;
                }

                // Alternatively: Try in any language
                if (_gameData != null)
                {
                    foreach (var language in _gameData.Languages)
                    {
                        if (TryDownloadCover(language, webClient, out data)) return;
                    }
                }
            }

            data = null;
        }

        /// <summary>
        /// Attempts to download a cover, fails gracefully on 404s.
        /// </summary>
        /// <param name="language">The language to download the cover in</param>
        /// <param name="webClient">WebClient to use to download the cover</param>
        /// <param name="result">Where the downloaded data should be stored (null if return value is false)</param>
        /// <param name="format">What format the cover should be downloaded in, defaults to <see cref="DolphinMetadataSettings.CoverDownloadPreference"/></param>
        /// <returns><code>true</code> if the download was successful, <code>false</code> on 404 errors</returns>
        private bool TryDownloadCover(string language, WebClient webClient, out byte[]? result, string? format = null)
        {
            if (_gameData == null)
            {
                throw new ArgumentException($"{nameof(_gameData)} must not be null");
            }
            try
            {
                result = webClient.DownloadData(_gameData.GetCoverUrl(language, format ?? _settings.CoverDownloadPreference.Substring("cropped_".Length)));
                return true;
            }
            catch (WebException e)
            {
                if (e.Response is not HttpWebResponse { StatusCode: HttpStatusCode.NotFound })
                {
                    throw;
                }

                result = null;
                return false;
            }
        }

        /// <summary>
        /// Takes in an image file's data containing full boxart for a game, and crops out only the front cover.
        /// </summary>
        /// <param name="data">The image data</param>
        /// <returns>Cropped image data (in PNG)</returns>
        private static byte[] CropBoxart(byte[] data)
        {
            const double cropRatio = 483.0 / 1024.0; // for a 1024-wide image, the (about) 483 rightmost pixels are the front cover

            MemoryStream dataStream;
            using (var inBitmap = (Bitmap)Image.FromStream(new MemoryStream(data)))
            {
                var width = (int)Math.Round(inBitmap.Width * cropRatio);

                using (var outBitmap = new Bitmap(width, inBitmap.Height, PixelFormat.Format32bppArgb))
                using (var g = Graphics.FromImage(outBitmap))
                {
                    g.DrawImage(inBitmap,
                        new Rectangle(0, 0, width, outBitmap.Height), //destRect
                        new Rectangle(inBitmap.Width - width, 0, width, inBitmap.Height), // srcRect
                        GraphicsUnit.Pixel
                    );

                    dataStream = new MemoryStream();
                    outBitmap.Save(dataStream, ImageFormat.Png);
                }
            }

            return dataStream.ToArray();
        }
    }
}