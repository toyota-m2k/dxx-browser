using Common;
using HtmlAgilityPack;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Xml;

namespace DxxBrowser.driver.caribbean {
    public class CaribbeanDriver : DxxDriverBaseStoragePathSupport {
        public override string Name => "Caribbean";
        public override string ID => "caribbeanFileBasedDriver";

        public override string ReserveFilePath(Uri uri) {
            return ((DxxFileBasedStorage)StorageManager).GetPath(uri);
        }

        public override IDxxLinkExtractor LinkExtractor { get; }
        public override IDxxStorageManager StorageManager { get; }

        public override bool IsSupported(string url) {
            var uri = new Uri(url);
            return uri.Host.Contains("caribbeancom.com") || uri.Host.Contains("caribbeancompr.com");
        }

        public override string GetNameFromUri(Uri uri, string defName) {
            // var regex = new Regex("Movie = (?<json>{.*})");
            var regex = new Regex(@"/(?<name>\d+[-]\d+)/");
            var match = regex.Match(uri.ToString());
            if(match.Success) {
                return match.Groups["name"]?.Value ?? defName;
            }
            return defName;
        }

        public override void Download(DxxTargetInfo target, Action<bool> onCompleted = null) {
            StorageManager.Download(target, this, onCompleted);
        }

        Regex mShortVideoUrl = new Regex(@"https://shorts.jpornmarket.com/video/sites/caribbeancom.com/(?<name>\d+[-]\d+)/.+.mp4");
        public override void HandleLinkedResource(string url, string refererUrl, string refererTitle) {
            var m = mShortVideoUrl.Match(url);
            if(m.Success) {
                var name = m.Groups["name"].Value;
                var target = new DxxTargetInfo(url, name, refererTitle);
                Download(target);
                return;
            }
            return; // インライン再生される動画リンクはダウンロードしない。（ちゃんとしたリンクからダウンロードしないとタイトルが全部同じになってしまう）
            //if(url.Contains("/www.caribbeancom.com/")) {
            //    return;
            //}
            //base.HandleLinkedResource(url, refererUrl, refererTitle);
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

            private string ensureUrl(string scheme, string host, string url) {
                if (null == url) {
                    return null;
                }
                if (!url.StartsWith("http")) {
                    if (url.StartsWith("/")) {
                        return $"{scheme}://{host}{url}";
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
                        var nodes = html.DocumentNode.SelectNodes("//a[contains(@href, '/moviepages/')]");
                        if (null == html) {
                            DxxLogger.Instance.Error(LOG_CAT, $"Load Error (list):{urx.Url}");
                            return null;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        if (!Utils.IsNullOrEmpty(nodes)) {
                            var list = nodes.Select((v) => {
                                var href = ensureUrl(urx.Uri.Scheme, urx.Uri.Host, v.Attributes["href"]?.Value);
                                if (null == href) {
                                    return null;
                                }
                                string desc = v.InnerText.Trim();
                                if(string.IsNullOrEmpty(desc)) {
                                    var img = v.SelectSingleNode("img");
                                    if(null!=img) {
                                        desc = img.Attributes["alt"].Value;
                                    }
                                }
                                var targetUri = new Uri(href);
                                var idx = targetUri.Segments.Count() - 2;
                                var name = idx >= 0 ? targetUri.Segments.ElementAt(idx) : "untitled";

                                return (href != null) ? new DxxTargetInfo(href, name, desc) : null;
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
                        //Console.WriteLine($"AutoDetectEncoding={web.AutoDetectEncoding}");
                        // Carribean は EUC-JP が使われており、HtmlWebが時々文字化けするので、明示的に指定する
                        var html = await web.LoadFromWebAsync(urx.Url, Encoding.GetEncoding("euc-jp"));
                        //Console.WriteLine($"Encoding={html.Encoding.EncodingName}, DeclaredEncoding={html.DeclaredEncoding.EncodingName}, StreamEncoding={html.StreamEncoding?.EncodingName} ");
                        // 念のため、実際の文字コードをチェック
                        var metaCharset = html.DocumentNode.SelectSingleNode("//meta[@http-equiv='Content-Type']/@content");
                        if (metaCharset != null) {
                            var contentValue = metaCharset.Attributes["content"].Value;
                            var match = Regex.Match(contentValue, @"charset=([^;""]+)", RegexOptions.IgnoreCase);
                            if (match.Success) {
                                string charset = match.Groups[1].Value.Trim();  
                                if (!charset.Equals("euc-jp", StringComparison.OrdinalIgnoreCase)) {
                                    try {
                                        // charsetがeuc-jpじゃない --> 検出された文字コードでロードし直す
                                        Encoding encoding = Encoding.GetEncoding(charset);
                                        html = await web.LoadFromWebAsync(urx.Url, encoding);
                                    }
                                    catch (ArgumentException) {
                                        // 無効な文字コードの場合はデフォルトのエンコーディングを使用
                                        Console.WriteLine($"Invalid charset '{charset}'. Using default encoding.");
                                    }
                                }
                            }
                        }

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
                                            if(string.IsNullOrEmpty(desc)) {
                                                desc = html.DocumentNode.SelectNodes("//h1")?[0]?.InnerText ?? "";
                                            }
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
            public Storage(IDxxDriver driver) : base(driver) {

            }
            protected override string LOG_CAT => "CRB";
            public override string GetPath(Uri uri) {
                var idx = uri.Segments.Count() - 2;
                var filename = idx >= 0 ? uri.Segments.ElementAt(idx) : "unknown";
                if(filename.EndsWith("/")) {
                    filename = filename.Substring(0, filename.Length - 1);
                }
                if (uri.Host.Contains("caribbeancompr.com")) {
                    filename = "pr" + filename;
                }
                return Path.Combine(Driver.StoragePath, filename + ".mp4");
            }
        }
    }
}