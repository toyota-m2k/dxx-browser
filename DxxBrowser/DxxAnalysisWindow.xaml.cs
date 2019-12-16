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

        public ReactiveProperty<HtmlNode> SelectedNode { get; } = new ReactiveProperty<HtmlNode>();
        public ReactiveProperty<string> OuterHtml { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<string> InnerText { get; } = new ReactiveProperty<string>();
        public ReactiveProperty<HtmlAttributeCollection> Attributes { get; } = new ReactiveProperty<HtmlAttributeCollection>();

        private void InitializeProperties() {
            SelectedNode.Subscribe((v) => {
                OuterHtml.Value = v?.OuterHtml;
                InnerText.Value = v?.InnerText;
                Attributes.Value = v?.Attributes;
            });
        }

        #endregion

        #region Commands

        public ReactiveCommand<string> BeginAnalysis { get; } = new ReactiveCommand<string>();

        public ReactiveCommand<string> ApplyXPath { get; } = new ReactiveCommand<string>();

        private void InitializeCommands() {
            BeginAnalysis.Subscribe((v) => {
                BaseUrl.Value = v;
                Load();
            });

            ApplyXPath.Subscribe((v) => {
                SetXPath(v);
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

        public DxxAnalysysViewModel ViewModel {
            get => DataContext as DxxAnalysysViewModel;
            private set { DataContext = value; }
        }

        private void OnNodeSelected(object sender, RoutedPropertyChangedEventArgs<object> e) {
            ViewModel.SelectedNode.Value = (e.NewValue as DxxHtmlNode)?.Node;
        }


    }
}
