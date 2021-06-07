using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows.Controls;
using System.Xml;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Plugins;

namespace PlayniteDolphinMetadata
{
    public class DolphinMetadataPlugin : MetadataPlugin
    {
        private static readonly ILogger Logger = LogManager.GetLogger();

        private XmlDocument _wiiDb;
        private byte[] _gamelist;
        private readonly object _wiiDbLock = new object();
        private volatile int _wiiDbReferenceCount;
        
        private DolphinMetadataSettings _settings;

        public DolphinMetadataPlugin(IPlayniteAPI playniteApi) : base(playniteApi)
        {
            _settings = CreateSettingsIfNotExists();
        }

        public (XmlDocument wiiDb, byte[] gamelist) GetWiiDb()
        {
            if (_wiiDb == null)
            {
                lock (_wiiDbLock)
                {
                    if (_wiiDb == null)
                    {
                        // Load wiidb.xml
                        var wiiDbPath = DownloadWiiDb(false);

                        _wiiDb = new XmlDocument();
                        _wiiDb.Load(wiiDbPath);

                        // Load gamelist.cache
                        if (!string.IsNullOrWhiteSpace(_settings.PathToDolphinUserFolder))
                        {
                            var gamelistCachePath = Path.Combine(_settings.PathToDolphinUserFolder, "Cache", "gamelist.cache");
                            if (File.Exists(gamelistCachePath))
                            {
                                Logger.Debug("Loading gamelist.cache into memory");
                                _gamelist = File.ReadAllBytes(gamelistCachePath);
                                Logger.Debug("Loaded gamelist.cache into memory");
                            }
                        }
                    }
                }
            }

            Interlocked.Increment(ref _wiiDbReferenceCount);

            return (_wiiDb, _gamelist);
        }

        public void ReleaseWiiDb()
        {
            var refCount = Interlocked.Decrement(ref _wiiDbReferenceCount);
            if (refCount <= 0)
            {
                lock (_wiiDbLock)
                {
                    _wiiDb = null;
                    _gamelist = null;
                }
            }
        }

        public string DownloadWiiDb(bool forceDownload)
        {
            var wiiTdbFile = $"{GetPluginUserDataPath()}/wiitdb.xml";
            if (forceDownload || !File.Exists(wiiTdbFile) || DateTime.Now.Subtract(_settings.LastWiiTdbUpdate).Days > 7)
            {
                var archiveDownloadPath = $"{GetPluginUserDataPath()}/wiitdb.zip";

                Logger.Debug("Downloading new wiitdb.zip from GameTDB");

                var startTime = DateTimeOffset.Now;

                using (var webClient = new WebClient())
                {
                    webClient.DownloadFile("https://www.gametdb.com/wiitdb.zip", archiveDownloadPath);
                }

                if (File.Exists(wiiTdbFile))
                {
                    File.Delete(wiiTdbFile);
                }

                Logger.Debug("Unzipping wiitdb.zip to " + wiiTdbFile);

                using (var input = File.OpenRead(archiveDownloadPath))
                using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    zip.GetEntry("wiitdb.xml").ExtractToFile(wiiTdbFile);
                }

                _settings.LastWiiTdbUpdate = DateTime.Now;
                SavePluginSettings(_settings);

                File.Delete(archiveDownloadPath);

                Logger.Debug("Finished WiiDB download process in " + (DateTimeOffset.Now - startTime));
            }

            return wiiTdbFile;
        }

        public override Guid Id { get; } = Guid.Parse("b7761d6c-2b05-44e7-9046-9ed3da7d8d68");
        public override string Name { get; } = "GameTDB (Dolphin)";

        public override List<MetadataField> SupportedFields { get; } = new List<MetadataField>
        {
            MetadataField.Name,
            MetadataField.Description,
            MetadataField.CoverImage,
            // MetadataField.BackgroundImage,
            // MetadataField.ReleaseDate,
            // MetadataField.Developers,
            // MetadataField.Publishers,
            // MetadataField.Genres,
            // MetadataField.Links,
            // MetadataField.Tags,
            // MetadataField.CriticScore,
            // MetadataField.CommunityScore
        };

        private DolphinMetadataSettings CreateSettingsIfNotExists()
        {
            var settings = LoadPluginSettings<DolphinMetadataSettings>();
            if (settings == null)
            {
                settings = new DolphinMetadataSettings();
                SavePluginSettings(settings);
            }

            return settings;
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new DolphinMetadataProvider(options, this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return new DolphinMetadataSettings(this);
        }

        public override UserControl GetSettingsView(bool firstRunView)
        {
            return new DolphinMetadataSettingsView();
        }
    }
}