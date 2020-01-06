using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using Common;

namespace DxxBrowser.driver.dmm
{
    public class DmmDriver : IDxxFileBasedDriver {
        public string StoragePath { get; set; }

        public string Name => "DMM";

        public string ID => "dmmFileBasedDriver";

        public bool HasSettings => true;

        public IDxxLinkExtractor LinkExtractor { get; private set; }

        public IDxxStorageManager StorageManager { get; private set; }

        private const string KEY_STORAGE_PATH = "StoragePath";

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
            return uri.Host.Contains("dmm.co.jp");
        }

        public string GetNameFromUri(Uri uri, string defName) {
            return DxxUrl.GetFileName(uri);
        }

        public bool Setup(XmlElement settings, Window owner) {
            var dlg = new DxxStorageFolderDialog(Name, StoragePath);
            dlg.Owner = owner;
            if(dlg.ShowDialog() ?? false) {
                StoragePath = dlg.Path;
                if (null!=settings) {
                    SaveSettings(settings);
                }
                return true;
            }
            return false;
        }

        public DmmDriver() {
            LinkExtractor = new Extractor(this);
            StorageManager = new DxxFileBasedStorage(this);
        }

        //class Storage : IDxxStorageManager {
        //    WeakReference<DmmDriver> mDriver;
        //    DmmDriver Driver => mDriver?.GetValue();

        //    public Storage(DmmDriver driver) {
        //        mDriver = new WeakReference<DmmDriver>(driver);
        //    }

        //    public void Download(Uri uri, string description, Action<bool> onCompleted) {
        //        if(!Driver.LinkExtractor.IsTarget(uri)) {
        //            onCompleted?.Invoke(false);
        //            return;
        //        }
        //        var path = GetPath(uri);
        //        if(File.Exists(path)) {
        //            onCompleted?.Invoke(false);
        //            return;
        //        }
        //        DxxDownloader.Instance.Download(uri, path, description, onCompleted);
        //    }

        //    private string GetPath(Uri uri) {
        //        var filename = DxxUrl.GetFileName(uri);
        //        return Path.Combine(Driver.StoragePath, filename);
        //    }

        //    public string GetSavedFile(Uri uri) {
        //        var path = GetPath(uri);
        //        return File.Exists(path) ? path : null;
        //    }

        //    public bool IsDownloaded(Uri uri) {
        //        var path = GetPath(uri);
        //        return File.Exists(path);
        //    }
        //}

        class Extractor : IDxxLinkExtractor {
            WeakReference<DmmDriver> mDriver;
            DmmDriver Driver => mDriver?.GetValue();

            private const string LOG_CAT = "DMM";

            public Extractor(DmmDriver driver) {
                mDriver = new WeakReference<DmmDriver>(driver);
            }

            public async Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx urx) {
                if(!IsContainerList(urx)) {
                    return null;
                }
                // <div>
                //< p class="tmb"><a href = "https://www.dmm.co.jp/litevideo/-/detail/=/cid=bf00392/" >
                // < span class="img"><img src = "https://pics.dmm.co.jp/digital/video/bf00392/bf00392pt.jpg" alt="美尻にぴったり密着タイトスカートSEX8時間"></span> 
                //<span class="txt">美尻にぴったり密着タイト...</span> 
                //<!--/tmb--></a></p>
                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    try {
                        DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                        var web = new HtmlWeb();
                        var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);
                        if (null == html) {
                            DxxLogger.Instance.Error(LOG_CAT, $"Load Error (list):{urx.Url}");
                            return null;
                        }
                        cancellationToken.ThrowIfCancellationRequested();
                        var para = html.DocumentNode.SelectNodes("//p[@class='tmb']");
                        if (para == null || para.Count == 0) {
                            DxxLogger.Instance.Error(LOG_CAT, $"No Targets:{urx.Url}");
                            return null;
                        }
                        var list = para.Select((p) => {
                            var href = p.SelectSingleNode("a")?.GetAttributeValue("href", null);
                            if (string.IsNullOrEmpty(href)) {
                                return null;
                            }
                            var desc = p.SelectSingleNode("a/span/img")?.GetAttributeValue("alt", null);
                            if (desc == null) {
                                desc = p.SelectSingleNode("a/span[@class='txt']")?.InnerText;
                            }
                            return new DxxTargetInfo(href, DxxUrl.GetFileName(href), desc);
                        }).Where((v) => v != null);
                        cancellationToken.ThrowIfCancellationRequested();
                        return list.ToList();
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

            private string ensureUrl(Uri baseUri, string url) {
                if (url.StartsWith("//")) {
                    return $"{baseUri.Scheme}:{url}";
                } else if (url.StartsWith("/")) {
                    return $"{baseUri.Scheme}://{baseUri.Host}{url}";
                } else { 
                    return url;
                }
            }

            public async Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx urx) {
                if(!IsContainer(urx)) {
                    return null;
                }
                return await DxxActivityWatcher.Instance.Execute(async(cancellationToken) => {
                    try {
                        DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                        var web = new HtmlWeb();
                        var outer = await web.LoadFromWebAsync(urx.Url, cancellationToken);
                        if (null == outer) {
                            DxxLogger.Instance.Error(LOG_CAT, $"Load Error (Target):{urx.Url}");
                            return null;
                        }

                        IEnumerable<string> anchors = null;
                        var iframes = outer.DocumentNode.SelectNodes("//iframe");
                        if (null != iframes) {
                            // カテゴリとか、そんなページ
                            anchors = iframes.Select((f) => {
                                return f.GetAttributeValue("src", null);
                            }).Where((u) => u != null);
                        } else {
                            // トップページ（新着とか）
                            do {
                                var a = outer.DocumentNode.SelectSingleNode("//a[contains(@onclick,'sampleplay')]");
                                if (a == null) {
                                    break;
                                }
                                var onclick = a.GetAttributeValue("onclick", null);
                                if (null == onclick) {
                                    break;
                                }
                                var regex = new Regex(@"sampleplay\(\'(?<url>.*)\'\)");
                                var v = regex.Match(onclick);
                                if (!v.Success) {
                                    break;
                                }
                                var onclickUrl = ensureUrl(urx.Uri, v.Groups["url"].Value);
                                var next = await web.LoadFromWebAsync(onclickUrl, cancellationToken);
                                if (null == next) {
                                    break;
                                }
                                var last = next.DocumentNode.SelectSingleNode("//iframe");
                                if (null == last) {
                                    break;
                                }
                                var anchor = last.GetAttributeValue("src", null);
                                if (anchor == null) {
                                    break;
                                }
                                anchors = new List<string>() { anchor };
                            } while (false);
                        } 
                        if (Utils.IsNullOrEmpty(anchors)) {
                            DxxLogger.Instance.Error(LOG_CAT, $"No Target: {urx.Url}");
                            return null;
                        }

                        var list = new List<DxxTargetInfo>();
                        foreach (var frame in anchors) {
                            cancellationToken.ThrowIfCancellationRequested();
                            var innerUrl = ensureUrl(urx.Uri, frame);
                            if (!Driver.IsSupported(innerUrl)) {
                                continue;
                            }
                            var inner = await web.LoadFromWebAsync(innerUrl, cancellationToken);
                            var txt = inner.DocumentNode.SelectSingleNode("//script[contains(text(), 'params')]");
                            if (null != txt) {
                                var regex = new Regex("{.*}");
                                var v = regex.Match(txt.InnerText);
                                if (v.Success) {
                                    //var params =
                                    //    {
                                    //                                "id":"dmmplayer","type":"litevideo","service":"litevideo","mode":"detail","cid":"ssni00529","eid":"FhJeUFYGVAAB",
                                    //        "gid":"Myd9eiAHMHFvN0Z5ZU5efCMCZ2k_","width":"560px","height":"360px","videoId":"video","videoType":"mp4",
                                    //        "src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_mhb_w.mp4",
                                    //        "title":"\u7f8e\u4eba\u4e0a\u53f8\u3068\u7ae5\u8c9e\u90e8\u4e0b\u304c\u51fa\u5f35\u5148\u306e\u76f8\u90e8\u5c4b\u30db\u30c6\u30eb\u3067\u2026\u3044\u305f\u305a\u3089\u8a98\u60d1\u3092\u771f\u306b\u53d7\u3051\u305f\u90e8\u4e0b\u304c10\u767a\u5c04\u7cbe\u306e\u7d76\u502b\u6027\u4ea4 \u5929\u4f7f\u3082\u3048","titleLink":"","titleLinkTarget":"_top","autoPlay":true,
                                    //        "poster":"\/\/pics.litevideo.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529.jpg",
                                    //        "replay":false,"playIconSize":"100%","loop":false,"muted":false,
                                    //        "bitrates":[
                                    //            {"bitrate":300,"src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_sm_w.mp4"},
                                    //            {"bitrate":1000,"src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_dm_w.mp4"},
                                    //            {"bitrate":1500,"src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_dmb_w.mp4"},
                                    //            {"bitrate":3000,"src":"\/\/cc3001.dmm.co.jp\/litevideo\/freepv\/s\/ssn\/ssni00529\/ssni00529_mhb_w.mp4"}],
                                    //        "affiliateId":"","controls":{"header":true,"panel":true,"title":true,"seek":true,"duration":true,"rewind60":false,"rewind10":true,"playpause":true,
                                    //        "forward10":true,"forward60":false,"bitrate":true,"volume":true,"fullscreen":true},"isDebug":false,"isVideoDebug":false,"isDisplayPlayCount":false
                                    //    }
                                    var js = JObject.Parse(v.Value);
                                    var ary = js["bitrates"];
                                    if (ary != null && ary.Type == JTokenType.Array) {
                                        int br = 0;
                                        string src = js["src"].Value<string>();
                                        foreach (var e in ary) {
                                            var i = e["bitrate"].Value<int>();
                                            if (i > br) {
                                                br = i;
                                                src = e["src"].Value<string>();
                                            }
                                        }
                                        if (src != null) {
                                            var targetUrl = ensureUrl(urx.Uri, src);
                                            list.Add(new DxxTargetInfo(targetUrl, DxxUrl.GetFileName(targetUrl), js["title"].Value<string>()));
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

            public bool IsContainerList(DxxUriEx urx) {
                if (!urx.Uri.Host.Contains("dmm.co.jp")) {
                    return false;
                }
                return urx.Url.Contains("/list/");
            }

            public bool IsContainer(DxxUriEx urx) {
                if (!urx.Uri.Host.Contains("dmm.co.jp")) {
                    return false;
                }
                return urx.Url.Contains("/cid=");
            }

            public bool IsTarget(DxxUriEx urx) {
                return DxxUrl.GetFileName(urx.Uri).EndsWith(".mp4");
            }
        }
    }
}
