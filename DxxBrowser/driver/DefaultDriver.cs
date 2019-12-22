using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;

namespace DxxBrowser.driver {
    public class DefaultDriver : IDxxDriver {
        public string Name => "Default";

        public string ID => "simpleDefaultDownloader";

        public bool HasSettings => true;

        public IDxxLinkExtractor LinkExtractor { get; } = new SimpleLinkExtractor();

        public IDxxStorageManager StorageManager => DxxDBStorage.Instance;

        public string GetNameFromUri(Uri uri, string defName = "") {
            return DxxUrl.TrimName(DxxUrl.GetFileName(uri));
        }

        public bool IsSupported(string url) {
            return true;
        }

        private const string KEY_STORAGE_PATH = "StoragePath";

        public bool LoadSettins(XmlElement settings) {
            DxxDBStorage.Instance.StoragePath = settings.GetAttribute(KEY_STORAGE_PATH);
            return true;
        }

        public bool SaveSettings(XmlElement settings) {
            settings.SetAttribute(KEY_STORAGE_PATH, DxxDBStorage.Instance.StoragePath);
            return true;
        }

        public bool Setup(XmlElement settings, Window owner) {
            var dlg = new DxxStorageFolderDialog(Name, DxxDBStorage.Instance.StoragePath);
            dlg.Owner = owner;
            if (dlg.ShowDialog() ?? false) {
                DxxDBStorage.Instance.StoragePath = dlg.Path;
                if (null != settings) {
                    SaveSettings(settings);
                }
                return true;
            }
            return false;
        }
        const string LOG_CAT = "DEF";
        public class SimpleLinkExtractor : IDxxLinkExtractor {
            public async Task<IList<DxxTargetInfo>> ExtractContainerList(DxxUriEx urx) {
                return await DxxActivityWatcher.Instance.Execute(async (cancellationToken) => {
                    try {
                        var web = new HtmlWeb();
                        DxxLogger.Instance.Comment(LOG_CAT, $"Analyzing: {DxxUrl.GetFileName(urx.Uri)}");
                        var html = await web.LoadFromWebAsync(urx.Url, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        var nodes1 = html.DocumentNode.SelectNodes("//a[contains(@href, '.mp') or contains(@href, '.wmv') or contains(@href,'.mov') or contains(@href,'.qt')]")
                                    ?.Select((v) => {
                                        return Uri.TryCreate(urx.Uri, v.Attributes["href"].Value, out Uri r)
                                                ? new DxxTargetInfo(r, DxxUrl.TrimName(DxxUrl.GetFileName(r)), "a/href") : null;
                                    })
                                    ?.Where((v) => v != null);
                        var nodes2 = html.DocumentNode.SelectNodes("//video")
                                    ?.Select((v) => {
                                        return Uri.TryCreate(urx.Uri, v.Attributes["src"].Value, out Uri r) ? new DxxTargetInfo(r, DxxUrl.TrimName(DxxUrl.GetFileName(r)), "video/src") : null;
                                    })
                                    ?.Where((v) => v != null);
                        IList<DxxTargetInfo> result = null;
                        if (nodes1 == null) {
                            if (nodes2 == null) {
                                DxxLogger.Instance.Comment(LOG_CAT, "No targets.");
                                return null;
                            } else {
                                result = nodes2.ToList();
                            }
                        } else if (nodes2 == null) {
                            result = nodes1.ToList();
                        } else {
                            result = nodes1.Concat(nodes2).ToList();
                        }
                        DxxLogger.Instance.Comment(LOG_CAT, $"{result.Count} target(s) detected.");
                        return result;
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

            public Task<IList<DxxTargetInfo>> ExtractTargets(DxxUriEx urx) {
                return Task.FromResult((IList<DxxTargetInfo>)null);
            }

            public bool IsContainerList(DxxUriEx url) {
                return true;
            }

            public bool IsContainer(DxxUriEx url) {
                return false;
            }

            public bool IsTarget(DxxUriEx urx) {
                var ext = System.IO.Path.GetExtension(DxxUrl.GetFileName(urx.Uri));
                switch (ext) {
                    case ".mp4":
                    case ".mpeg":
                    case ".mpg":
                    case "mov":
                    case "wmv":
                    case "qt":
                        return true;
                    default:
                        return false;
                }
            }
        }
    }
}
