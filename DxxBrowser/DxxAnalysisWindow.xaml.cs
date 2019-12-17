using Microsoft.WindowsAPICodePack.Dialogs;
using DxxBrowser.driver;
using HtmlAgilityPack;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace DxxBrowser {
    public class DxxAnalysysViewModel : DxxViewModelBase, IDisposable {
        #region Properties

        public ReactiveProperty<string> BaseUrl { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> XPath { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<bool> DocumentAvailable { get; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<IEnumerable<DxxHtmlNode>> Nodes { get; } = new ReactiveProperty<IEnumerable<DxxHtmlNode>>();
        public ReactiveProperty<DxxHtmlNode> CurrentNode { get; } = new ReactiveProperty<DxxHtmlNode>();

        public ReactiveProperty<DxxHtmlNode> SelectedNode { get; } = new ReactiveProperty<DxxHtmlNode>();

        private void InitializeProperties() {
        }

        #endregion

        #region Commands

        public ReactiveCommand<string> BeginAnalysis { get; } = new ReactiveCommand<string>();

        public ReactiveCommand<string> ApplyXPath { get; } = new ReactiveCommand<string>();

        public ReactiveCommand<DxxLink> CopyLinkUrl { get; } = new ReactiveCommand<DxxLink>();
        public ReactiveCommand<DxxLink> AnalizeLinkUrl { get; } = new ReactiveCommand<DxxLink>();
        public ReactiveCommand<DxxLink> AnalizeNewLinkUrl { get; } = new ReactiveCommand<DxxLink>();
        public ReactiveCommand<DxxLink> ExecuteLinkUrl { get; } = new ReactiveCommand<DxxLink>();
        public ReactiveCommand<DxxLink> DownloadLinkUrl { get; } = new ReactiveCommand<DxxLink>();

        public ReactiveCommand<DxxHtmlNode> SelectParent { get; } = new ReactiveCommand<DxxHtmlNode>();
        public ReactiveCommand<DxxHtmlNode> SelectThisNode { get; } = new ReactiveCommand<DxxHtmlNode>();
        public ReactiveCommand<HtmlAttribute> CopyAttrValue { get; } = new ReactiveCommand<HtmlAttribute>();
        public ReactiveCommand<HtmlAttribute> CopyAttrName { get; } = new ReactiveCommand<HtmlAttribute>();


        private void InitializeCommands() {
            BeginAnalysis.Subscribe((v) => {
                BaseUrl.Value = v;
                Load();
            });

            ApplyXPath.Subscribe((v) => {
                SetXPath(v);
            });

            CopyLinkUrl.Subscribe((v) => {
                Debug.WriteLine(v.Value);
                Clipboard.SetData(DataFormats.Text, v.Value);
            });
            ExecuteLinkUrl.Subscribe((v) => {
                Debug.WriteLine(v);
                Process.Start(v.Value);
            });
            AnalizeLinkUrl.Subscribe((v) => {
                Debug.WriteLine(v);
                BeginAnalysis.Execute(v.Value);
            });
            AnalizeNewLinkUrl.Subscribe((v) => {
                Debug.WriteLine(v);
                var aw = new DxxAnalysisWindow(v.Value);
                aw.Show();
            });
            DownloadLinkUrl.Subscribe((v) => {
                using (var dlg = new CommonSaveFileDialog("Download to file.")) {
                    var uri = new Uri(v.Value);
                    var ti = new DxxTargetInfo(uri, DxxUrl.GetFileName(uri), "");
                    dlg.OverwritePrompt = true;
                    dlg.DefaultFileName = ti.Name;
                    if (dlg.ShowDialog() == CommonFileDialogResult.Ok) {
                        DxxDownloader.Instance.Download(ti, dlg.FileName);
                    }
                }
            });

            SelectParent.Subscribe((v) => {
                var parent = v.Node.ParentNode;
                if(null!=parent) {
                    ApplyXPath.Execute(parent.XPath);
                }
            });

            SelectThisNode.Subscribe((v) => {
                //Debug.WriteLine(v);
                ApplyXPath.Execute(v.Node.XPath);
            });

            CopyAttrName.Subscribe((v) => {
                Clipboard.SetData(DataFormats.Text, v.Name);
            });
            CopyAttrValue.Subscribe((v) => {
                Clipboard.SetData(DataFormats.Text, v.Value??"");
            });
        }

        #endregion

        #region Initialize

        public DxxAnalysysViewModel(string initialUrl) {
            if (!string.IsNullOrEmpty(initialUrl)) {
                BaseUrl.Value = initialUrl;
                Load();
            }

            InitializeCommands();
            InitializeProperties();
        }

        #endregion

        #region Analyzing

        HtmlWeb Web = new HtmlWeb();
        HtmlDocument HtmlRoot = null;

        public static string LOG_CAT = "ANALYZER";

        private async void Load() {
            DocumentAvailable.Value = false;
            try {
                Nodes.Value = null;
                CurrentNode.Value = null;
                HtmlRoot = await Web.LoadFromWebAsync(BaseUrl.Value);
                if(null!=HtmlRoot) {
                    DocumentAvailable.Value = true;
                    CurrentNode.Value = new DxxHtmlNode(HtmlRoot.DocumentNode);
                    Nodes.Value = CurrentNode.Value.ChildNodes;
                }
            } catch(Exception e) {
                Debug.WriteLine(e.ToString());
                DxxLogger.Instance.Error(LOG_CAT, e.Message);
            }
        }

        public void SetXPath(string xpath) {
            if(string.IsNullOrEmpty(xpath)) {
                XPath.Value = "";
                CurrentNode.Value = new DxxHtmlNode(HtmlRoot.DocumentNode);
                Nodes.Value = CurrentNode.Value.ChildNodes;
                return;
            }
            var v = CurrentNode.Value.SelectNodes(xpath);
            if(Utils.IsNullOrEmpty(v)) {
                return;
            }
            XPath.Value = xpath;
            Nodes.Value = v;
        }

        #endregion
    }


    /// <summary>
    /// DxxAnalysisWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxAnalysisWindow : Window {
        public DxxAnalysisWindow(string url) {
            ViewModel = new DxxAnalysysViewModel(url);
            InitializeComponent();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
        }

        public DxxAnalysysViewModel ViewModel {
            get => DataContext as DxxAnalysysViewModel;
            private set { DataContext = value; }
        }

        private void OnNodeSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            ViewModel.SelectedNode.Value = e.NewValue as DxxHtmlNode;
        }

        private void OnLinkDoubleClick(object sender, MouseButtonEventArgs e) {
            var v = (sender as ListViewItem)?.Content as DxxLink;

            if(v !=null) {
                Debug.WriteLine(v.Value);
                System.Diagnostics.Process.Start(v.Value);
            }
        }

    }
}
