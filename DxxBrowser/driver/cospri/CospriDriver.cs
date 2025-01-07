using Common;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace DxxBrowser.driver.cospri {
    internal class CospriDriver : DxxDriverBaseStoragePathSupport {
        public override string Name => "Cospri";

        public override string ID => "cospriDriver";

        public override IDxxLinkExtractor LinkExtractor { get; }

        public override IDxxStorageManager StorageManager { get; }

        public CospriDriver() {
            LinkExtractor = new Extractor(this);
            StorageManager = new Storage();
        }


        public override void Download(DxxTargetInfo target, Action<bool> onCompleted = null) {
            StorageManager.Download(target, this, onCompleted);
        }

        public override string GetNameFromUri(Uri uri, string defName = "") {
            var regex = new Regex(@"/(?<name>[0-9a-z]+)/sample.mp4");
            var match = regex.Match(uri.ToString());
            string name = null;
            if (match.Success) {
                name = match.Groups["name"]?.Value;
            }
            return DxxUrl.TrimName(name ?? DxxUrl.GetFileName(uri));
        }

        public override bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("cospuri.com") || uri.Host.Contains("spermmania.com");
        }

        public override string ReserveFilePath(Uri uri) {
            return null;
        }

        public override void HandleLinkedResource(string url, string refererUrl, string refererTitle) {
            if (url.EndsWith("hover.mp4")) {
                return; // hover.mp4は無視
            }
            base.HandleLinkedResource(url, refererUrl, refererTitle);
        }

        class Extractor : IDxxLinkExtractor {
            WeakReference<CospriDriver> driverWeakReference;
            CospriDriver Driver => driverWeakReference.GetValue();
            public Extractor(CospriDriver driver) {
                driverWeakReference = new WeakReference<CospriDriver>(driver);
            }


            private const string LOG_CAT = "CSP";
            public async Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx urx, string htmlString) {
                if (!IsContainerList(urx)) {
                    return null;
                }
                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    var web = new HtmlWeb();
                    var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);

                    // まずリストページとして解釈
                    var list = html.DocumentNode.SelectNodes("//div[contains(@class, 'scene') and not(contains(@class, 'scene-'))]")?
                    .Select((target) => {
                        var link = AbsoluteUri(urx.Uri, target.SelectSingleNode(".//a[contains(@href, '/sample?id=')]")?.Attributes["href"]?.Value);
                        if (link == null) return null;
                        var desc = target.SelectSingleNode(".//a[contains(@href, '/model/')]")?.InnerText ?? "noname";
                        return new DxxTargetInfo(link, desc, desc);
                    })?
                    .Where((v) => v != null)?
                    .ToList();
                    return list;

                    //DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                    //var web = new HtmlWeb();
                    //var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);
                    //var xpath = "//a[contains(@href,'/model/')]";
                    //return html.DocumentNode.SelectNodes(xpath)?
                    //                .Distinct(AnchorNodeComparator.Instance)?
                    //                .Select((v) => CreateContainerInfo(urx.Uri, v.Attributes["href"].Value, v))?
                    //                .Where((v) => v != null)?
                    //                .ToList();
                });
            }

            private Uri AbsoluteUri(Uri baseUrl, string relativeUrl) {
                if(relativeUrl == null) return null;
                if (Uri.TryCreate(baseUrl, relativeUrl, out var uri)) {
                    return uri;
                }
                return null;
            }

            //private string IDFromIDQueryUrl(string idQueryUrl) {
            //    if(idQueryUrl == null) return null;
            //    Regex regex = new Regex(@"/sample\?id=(?<id>[0-9a-z]+)");
            //    var match = regex.Match(idQueryUrl);
            //    if (match.Success) {
            //        return match.Groups["id"].Value;
            //    }
            //    return null;
            //}

            //private string UrlFromId(string id) {
            //    if (id == null) {
            //        return null;
            //    }
            //    return $"https://img.cospuri.com/preview/{id}/sample.mp4";
            //}


            public async Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx urx) {
                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                    var web = new HtmlWeb();
                    var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);

                    // サンプルページとして解釈
                    var name = html.DocumentNode.SelectNodes("//div[@class='sample-model']/a")?
                                        .FirstOrDefault()?
                                        .InnerText ?? "noname";
                    var script = html.DocumentNode.SelectNodes("//script[contains(text(), 'sample.mp4')]");
                    if (script == null || script.Count == 0) {
                        return null;
                    }
                    return script.Select((v) => {
                        var match = Regex.Match(v.InnerText, @"https://.*/preview/(?<id>[0-9a-z]+)/sample.mp4");
                        if (match.Success) {
                            var url = match.Value;
                            var id = match.Groups["id"].Value;
                            return new DxxTargetInfo(url, name, name);
                        }
                        return null;
                    })?
                    .Where((v) => v != null)?
                    .ToList();
                });

            }

            private Regex regexContainerPage = new Regex(@".*\.com/sample\?id=(?<id>[0-9a-z]+)");
            public bool IsContainer(DxxUriEx url) {
                return regexContainerPage.IsMatch(url.Url);
            }

            public bool IsContainerList(DxxUriEx url) {
                return Driver.IsSupported(url.Url) && !IsContainer(url);
            }

            public bool IsTarget(DxxUriEx url) {
                return DxxUrl.GetFileName(url.Uri).EndsWith(".mp4");
            }

            private DxxTargetInfo CreateContainerInfo(Uri baseUri, string url, HtmlNode node) {
                if (Uri.TryCreate(baseUri, url, out var uri)) {
                    if (!IsContainer(new DxxUriEx(uri))) {
                        return null;
                    }
                    var desc = uri.Segments.Length > 0 ? uri.Segments[uri.Segments.Length - 1] : "";
                    var name = Driver.GetNameFromUri(uri);
                    return new DxxTargetInfo(uri, name, desc);
                }
                return null;
            }

        }
        private class Storage : IDxxStorageManager {

            public void Download(DxxTargetInfo target, IDxxDriver driver, Action<bool> onCompleted = null) {
                DxxDBStorage.Instance.Download(target, driver, onCompleted);
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