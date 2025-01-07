using Common;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace DxxBrowser.driver.heizo {
    public class HeyzoDriver : DxxDriverBaseStoragePathSupport {
        #region IDxxDriver i/f

        public override string Name => "Heyzo";
        public override string ID => "heizoDBBasedDriver";
        public override IDxxLinkExtractor LinkExtractor { get; } = new Extractor();
        public override IDxxStorageManager StorageManager { get; } = new Storage();

        public override string ReserveFilePath(Uri uri) {
            return null;
        }

        public override bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("heyzo.com");
        }

        public override string GetNameFromUri(Uri uri, string defName = "") {
            return DxxUrl.TrimName(DxxUrl.GetFileName(uri));
        }

        public override void Download(DxxTargetInfo target, Action<bool> onCompleted = null) {
            StorageManager.Download(target, this, onCompleted);
        }

        #endregion

        public HeyzoDriver() {
        }


        private class Extractor : IDxxLinkExtractor {
            private const string LOG_CAT = "HYZ";

            readonly Regex reg = new Regex("title\\s*=\\s*\"(?<d>.*)\"");
            string TryGetDescFromInnerHtml(string html) {
                var v = reg.Match(html);
                if(v.Success) {
                    var d = v.Groups["d"]?.Value;
                    if(d!=null) {
                        return SafeDescription(d);
                    }
                }
                return null;
            }
            /**
             * コンテナ用のTargetInfoを作成する
             */
            private DxxTargetInfo CreateContainerInfo(Uri baseUri, string url, HtmlNode node) {
                if(Uri.TryCreate(baseUri, url, out var uri)) {
                    if(!IsContainer(new DxxUriEx(uri))) {
                        return null;
                    }
                    var desc = SafeDescription(node.InnerText);
                    if(string.IsNullOrWhiteSpace(desc)) {
                        desc = TryGetDescFromInnerHtml(node.InnerHtml);
                        if (string.IsNullOrWhiteSpace(desc)) {
                            desc = GetIndexStringFromUrl(url);
                        }
                    }
                    var name = DxxUrl.TrimName(DxxUrl.GetFileName(uri));
                    return new DxxTargetInfo(uri, name, desc);
                }
                return null;
            }
            // <a name = ○○○○○○○○○○○○～&nbsp;&lt;a href = &quot; javascript:;&quot; onclick=&quot;linktoDetail(&#39;1184&#39;)&quot; &gt;詳細ページ&lt;/a&gt; rel=lightbox[external 704 396] href=//sample.heyzo.com/contents/3000/1184/sample.mp4 class=sampleMovie style=font-size: 80%;>
            //　　サンプル動画
            // </a>
            static readonly string[] DescDelimiter = new string[] { "&nbsp", "&lt", "&quot" };

            private static string SafeDescription(string src) {
                string result = src;
                var s = src.Split(DescDelimiter, StringSplitOptions.None);
                if(s.Count()>1) {
                    result = s[0];
                }
                result = DxxUrl.TrimText(result);
                result = Regex.Replace(result, "[\'\"]+", "");
                return result;
            }

            /**
             * ダウンロードターゲットの TargetInfoを作成する
             */
            private DxxTargetInfo CreateTargetInfo(Uri baseUri, string url, HtmlNode node) {
                if (Uri.TryCreate(baseUri, url, out var uri)) {
                    var desc = SafeDescription(node.Attributes["name"].Value);
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
             * コンテナのリストを抽出する
             */
            public async Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx urx, string htmlString) {
                if (!IsContainerList(urx)) {
                    return null;
                }
                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                    var web = new HtmlWeb();
                    var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);
                    //var xpath = "//a[contains(@href, '/moviepages/') or contains(@href,'/listpages')]";
                    var xpath = "//a[contains(@href, '/moviepages/')]";
                    return html.DocumentNode.SelectNodes(xpath)?
                                    .Distinct(AnchorNodeComparator.Instance)?
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
                    var mp4s = html.DocumentNode.SelectNodes("//a[contains(@href, '.mp') or contains(@href, '.wmv') or contains(@href,'.mov') or contains(@href,'.qt')]")?
                                    .Distinct(AnchorNodeComparator.Instance)?
                                    .Select((v) => CreateTargetInfo(urx.Uri, v.Attributes["href"].Value, v))?
                                    .Where((v) => v != null);
                    if(!Utils.IsNullOrEmpty(mp4s)) {
                        return mp4s.ToList();
                    }

                    // js でロードされる、sample_low.mp4 を列挙する
                    // ... sample_low は低解像度の動画で、同じ内容で高解像度のものが存在するようなので、こいつらは列挙しないことにする。
                    var scripts = html.DocumentNode.SelectNodes(".//script[contains(text(),'emvideo')]");
                    IEnumerable<DxxTargetInfo> embedded = null;
                    if(null!= scripts) {
                        embedded = scripts.Select((v) => {
                            var regex = new Regex("(?<=var\\s+emvideo\\s*=\\s*[\"\'])(?<path>.*\\.mp4)");
                            var m = regex.Match(v.InnerText);
                            if(m.Success) {
                                var emvideo = m.Groups["path"].Value;
                                if(Uri.TryCreate(urx.Uri, emvideo, out var uri)) {
                                    var desc = SafeDescription(html.DocumentNode.SelectSingleNode(".//title").InnerText);
                                    var name = DxxUrl.TrimName(DxxUrl.GetFileName(uri));
                                    return new DxxTargetInfo(uri, name, desc);
                                }
                            }
                            return null;
                        }).Where((v)=>v!=null);
                        if(null!=embedded) {
                            if(mp4s==null) {
                                mp4s = embedded;
                            } else {
                                mp4s = mp4s.Concat(embedded);
                            }
                        }
                    }
                    if(Utils.IsNullOrEmpty(mp4s)) {
                        DxxLogger.Instance.Comment(LOG_CAT, $"No Data: {DxxUrl.GetFileName(urx.Uri)}");
                    }
                    return mp4s?.ToList();
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
                return urx.Url.Contains("/listpages/");
                //return true;
            }

            public bool IsTarget(DxxUriEx urx) {
                return DxxUrl.GetFileName(urx.Uri).EndsWith(".mp4");
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
