using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;

namespace DxxBrowser.driver.caribbean {
    public class CaribbeanDriver : IDxxFileBasedDriver {
        public string Name => "Caribbean";

        public string ID => "caribbeanFileBasedDriver";

        public bool HasSettings => true;

        private const string KEY_STORAGE_PATH = "StoragePath";
        public string StoragePath { get; set; }

        public IDxxLinkExtractor LinkExtractor { get; private set; }

        public IDxxStorageManager StorageManager { get; private set; }

        public bool LoadSettins(XmlElement settings) {
            StoragePath = settings.GetAttribute(KEY_STORAGE_PATH);
            return true;
        }

        public bool SaveSettings(XmlElement settings) {
            settings.SetAttribute(KEY_STORAGE_PATH, StoragePath);
            return true;
        }

        public bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("caribbeancom.com");
        }

        public string GetNameFromUri(Uri uri, string defName) {
            // var regex = new Regex("Movie = (?<json>{.*})");
            var regex = new Regex(@"/(?<name>\d+[-]d*)/");
            var match = regex.Match(uri.ToString());
            if(match.Success) {
                return match.Groups["name"]?.Value ?? defName;
            }
            return defName;
        }

        public bool Setup(XmlElement settings) {
            var dlg = new DxxStorageFolderDialog(Name, StoragePath);
            if (dlg.ShowDialog() ?? false) {
                StoragePath = dlg.Path;
                if (null != settings) {
                    SaveSettings(settings);
                }
                return true;
            }
            return false;
        }

        public CaribbeanDriver() {
            LinkExtractor = new Extractor(this);
            StorageManager = new Storage(this);
        }

        class Extractor : IDxxLinkExtractor {
            WeakReference<CaribbeanDriver> mDriver;
            CaribbeanDriver Driver => mDriver?.GetValue();

            private const string LOG_CAT = "CRB";

            public Extractor(CaribbeanDriver driver) {
                mDriver = new WeakReference<CaribbeanDriver>(driver);
            }

            public bool IsContainer(DxxUriEx urx) {
                return Driver.IsSupported(urx.Url) && urx.Url.Contains("/moviepages/");
            }

            public bool IsContainerList(DxxUriEx urx) {
                return Driver.IsSupported(urx.Url) && !urx.Url.Contains("/moviepages/");
            }

            public bool IsTarget(DxxUriEx urx) {
                return DxxUrl.GetFileName(urx.Uri).EndsWith(".mp4");
            }

            private string ensureUrl(string url) {
                if (null == url) {
                    return null;
                }
                if (!url.StartsWith("http")) {
                    if (url.StartsWith("/")) {
                        return $"https://www.caribbeancom.com{url}";
                    } else {
                        return null;
                    }
                }
                return url;
            }

            public async Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx urx) {
                if (!IsContainerList(urx)) {
                    return null;
                }

                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    try {
                        var web = new HtmlWeb();
                        DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                        var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);
                        var nodes = html.DocumentNode.SelectNodes("//a[@itemprop='url' and contains(@href, '/moviepages/')]");
                        if (null == html) {
                            DxxLogger.Instance.Error(LOG_CAT, $"Load Error (list):{urx.Url}");
                            return null;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Utils.IsNullOrEmpty(nodes)) {
                            var list = nodes.Select((v) => {
                                var href = ensureUrl(v.Attributes["href"]?.Value);
                                if (null == href) {
                                    return null;
                                }
                                var desc = v.InnerText;
                                var targetUri = new Uri(href);
                                var idx = targetUri.Segments.Count() - 2;
                                var name = idx >= 0 ? targetUri.Segments.ElementAt(idx) : "untitled";

                                return (href != null) ? new DxxTargetInfo(href, name, v.InnerText) : null;
                            }).Where((v) => v != null)?.ToList();
                            cancellationToken.ThrowIfCancellationRequested();
                            return list;
                        }
                        DxxLogger.Instance.Error(LOG_CAT, $"No List:{urx.Url}");
                        return null;
                    } catch (Exception e) {
                        if (e is OperationCanceledException) {
                            DxxLogger.Instance.Cancel(LOG_CAT, $"Cancelled (list):{urx.Url}");
                        } else {
                            DxxLogger.Instance.Error(LOG_CAT, $"Error (list):{urx.Url}");
                        }
                        return null;
                    }

                });
            }

            public async Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx urx) {
                if (!IsContainer(urx)) {
                    return null;
                }
                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    try {
                        DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                        var web = new HtmlWeb();
                        var html = await web.LoadFromWebAsync(urx.Url);
                        var nodes = html.DocumentNode.SelectNodes("//script[contains(text(),'Movie =')]");
                        var list = new List<DxxTargetInfo>();
                        if (!Utils.IsNullOrEmpty(nodes)) {
                            foreach (var node in nodes) {
                                var script = node.InnerText;
                                var regex = new Regex("Movie = (?<json>{.*})");
                                var v = regex.Match(script);
                                if (v.Success) {
                                    // var Movie = {
                                    //   is_vip: "0",
                                    //   is_supervip: "1",
                                    //   is_annual: "0",
                                    //   movie_type: "5",
                                    //   movie_id: "011015-780",
                                    //   movie_seq: "20290",
                                    //   has_gallery: "1",
                                    //   is_recurring: "0",
                                    //   sample_flash_exists: "1",
                                    //   sample_flash_url:
                                    //     "https://smovie.caribbeancom.com/sample/movies/011015-780/480p.mp4",
                                    //   sample_m_flash_exists: "1",
                                    //   sample_m_flash_url:
                                    //     "https://smovie.caribbeancom.com/sample/movies/011015-780/sample_m.mp4",
                                    //   is_movie_expired: "0",
                                    //   movie_streaming_type: "5",
                                    //   is_mp4: "1",
                                    //   sampleexclude_flag: "0"
                                    // };
                                    var js = JObject.Parse(v.Groups["json"].Value);
                                    if (null != js) {
                                        var url = js["sample_flash_url"].Value<string>();
                                        if (null == url) {
                                            url = js["sample_m_flash_url"].Value<string>();
                                        }
                                        var name = js["movie_id"].Value<string>() ?? "untitled";
                                        if (null != url) {
                                            var desc = html.DocumentNode.SelectNodes("//h1[@itemprop='name']")?[0]?.InnerText ?? "";
                                            list.Add(new DxxTargetInfo(url, name, desc));
                                        }
                                    }
                                }
                            }
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        return list;
                    } catch (Exception e) {
                        if (e is OperationCanceledException) {
                            DxxLogger.Instance.Cancel(LOG_CAT, $"Cancelled (Target):{urx.Url}");
                        } else {
                            DxxLogger.Instance.Error(LOG_CAT, $"Error (Target):{urx.Url}");
                        }
                        return null;
                    }
                });
            }
        }

        class Storage : DxxFileBasedStorage {
            public Storage(IDxxFileBasedDriver driver) : base(driver) {

            }
            protected override string GetPath(Uri uri) {
                var idx = uri.Segments.Count() - 2;
                var filename = idx >= 0 ? uri.Segments.ElementAt(idx) : "unknown";
                if(filename.EndsWith("/")) {
                    filename = filename.Substring(0, filename.Length - 1);
                }
                return Path.Combine(Driver.StoragePath, filename+".mp4");
            }
        }
    }
}