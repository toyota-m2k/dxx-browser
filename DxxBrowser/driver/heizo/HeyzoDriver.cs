using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace DxxBrowser.driver.heizo {
    public class HeyzoDriver : IDxxDriver {
        #region IDxxDriver i/f

        public string Name => "Heyzo";

        public string ID => "heizoDBBasedDriver";

        public bool HasSettings => true;

        public IDxxLinkExtractor LinkExtractor { get; } = new Extractor();

        public IDxxStorageManager StorageManager { get; } = new Storage();

        private const string KEY_STORAGE_PATH = "StoragePath";

        public bool LoadSettins(XmlElement settings) {
            Storage.StoragePath = settings.GetAttribute(KEY_STORAGE_PATH);
            return true;
        }

        public bool SaveSettings(XmlElement settings) {
            settings.SetAttribute(KEY_STORAGE_PATH, Storage.StoragePath);
            return true;
        }

        public bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("heyzo.com");
        }

        public string GetNameFromUri(Uri uri, string defName = "") {
            // ToDo:
            return DxxUrl.TrimName(DxxUrl.GetFileName(uri));
        }

        public bool Setup(XmlElement settings, Window owner) {
            var dlg = new DxxStorageFolderDialog(Name, Storage.StoragePath);
            dlg.Owner = owner;
            if (dlg.ShowDialog() ?? false) {
                Storage.StoragePath = dlg.Path;
                if (null != settings) {
                    SaveSettings(settings);
                }
                return true;
            }
            return false;
        }

        #endregion

        public HeyzoDriver() {
        }

        private const string LOG_CAT = "HYZ";

        private class Extractor : IDxxLinkExtractor {
            /**
             * コンテナ用のTargetInfoを作成する
             */
            private DxxTargetInfo CreateContainerInfo(Uri baseUri, string url, HtmlNode node) {
                if(Uri.TryCreate(baseUri, url, out var uri)) {
                    var desc = DxxUrl.TrimText(node.InnerText);
                    if(string.IsNullOrWhiteSpace(desc)) {
                        GetIndexStringFromUrl(url);
                    }
                    var name = DxxUrl.TrimName(DxxUrl.GetFileName(uri));
                    return new DxxTargetInfo(uri, name, desc);
                }
                return null;
            }

            /**
             * ダウンロードターゲットの TargetInfoを作成する
             */
            private DxxTargetInfo CreateTargetInfo(Uri baseUri, string url, HtmlNode node) {
                if (Uri.TryCreate(baseUri, url, out var uri)) {
                    var desc = DxxUrl.TrimText(node.Attributes["name"].Value);
                    if (string.IsNullOrWhiteSpace(desc)) {
                        GetIndexStringFromUrl(url);
                    }
                    var name = DxxUrl.TrimName(DxxUrl.GetFileName(uri));
                    return new DxxTargetInfo(uri, name, desc);
                }
                return null;
            }

            /**
             * urlから、.../xxxx/hogehoge の xxxx部分を取り出す。
             */
            private string GetIndexStringFromUrl(string url) {
                var s = url.Split('/');
                var c = s.Count();
                if (c >= 2) {
                    return s[c - 2];
                } else {
                    return url;
                }
            }

            /**
             * href が同じ Anchorノードを同一視するための比較クラス
             */
            private class AnchorNodeComparator : IEqualityComparer<HtmlNode> {
                public bool Equals(HtmlNode x, HtmlNode y) {
                    return x.Attributes["href"].Value == y.Attributes["href"].Value;
                }

                public int GetHashCode(HtmlNode obj) {
                    return obj.Attributes["href"].Value?.GetHashCode() ?? 0;
                }
            }
            private AnchorNodeComparator mAnchorNodeComparator = new AnchorNodeComparator();

            /**
             * コンテナのリストを抽出する
             */
            public async Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx urx) {
                if (!IsContainerList(urx)) {
                    return null;
                }
                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                    var web = new HtmlWeb();
                    var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);
                    return html.DocumentNode.SelectNodes("//a[contains(@href, '/moviepages/') or contains(@href,'/listpages')]")?
                                    .Distinct(mAnchorNodeComparator)?
                                    .Select((v) => CreateContainerInfo(urx.Uri, v.Attributes["href"].Value, v))?
                                    .Where((v) => v != null)?
                                    .ToList();
                });
            }

            /**
             * コンテナからターゲットを抽出する
             */
            public async Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx urx) {
                if(!IsContainer(urx)) {
                    return null;
                }
                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                    var web = new HtmlWeb();
                    var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);
                    return html.DocumentNode.SelectNodes("//a[contains(@href, '.mp') or contains(@href, '.wmv') or contains(@href,'.mov') or contains(@href,'.qt')]")?
                                    .Distinct(mAnchorNodeComparator)?
                                    .Select((v) => CreateTargetInfo(urx.Uri, v.Attributes["href"].Value, v))?
                                    .Where((v) => v != null)?
                                    .ToList();
                });
            }

            public bool IsContainer(DxxUriEx urx) {
                if (!urx.Uri.Host.Contains("heyzo.com")) {
                    return false;
                }
                return urx.Url.Contains("/moviepages/");
            }

            public bool IsContainerList(DxxUriEx urx) {
                if (!urx.Uri.Host.Contains("heyzo.com")) {
                    return false;
                }
                //return urx.Url.Contains("/listpages/");
                return true;
            }

            public bool IsTarget(DxxUriEx urx) {
                return DxxUrl.GetFileName(urx.Uri).EndsWith(".mp4");
            }
        }

        private class Storage : IDxxStorageManager {
            public static string StoragePath { get; set; } = null;

            public void Download(DxxTargetInfo target, Action<bool> onCompleted = null) {
                DxxDBStorage.Instance.Download(target, StoragePath, onCompleted);
            }

            public string GetSavedFile(Uri url) {
                return DxxDBStorage.Instance.GetSavedFile(url);
            }

            public bool IsDownloaded(Uri url) {
                return DxxDBStorage.Instance.IsDownloaded(url);
            }
        }
    }
}
