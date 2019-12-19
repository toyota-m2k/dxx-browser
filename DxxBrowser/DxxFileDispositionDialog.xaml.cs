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
    /// <summary>
    /// DxxFileDispositionDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxFileDispositionDialog : Window {
        public class DxxFileDispositionViewModel : DxxViewModelBase {
            public ReactiveProperty<string> Message { get; }
            public string FilePath { get; set; }

            public ReactiveCommand CommandOpenFile { get; } = new ReactiveCommand();
            public ReactiveCommand CommandOpenExplorer { get; } = new ReactiveCommand();
            public ReactiveCommand CommandClose { get; } = new ReactiveCommand();

            private WeakReference<Window> mOwner;
            private Window Owner => mOwner?.GetValue();

            public DxxFileDispositionViewModel(Window owner, string filePath) {
                mOwner = new WeakReference<Window>(owner);
                FilePath = filePath;
                var name = System.IO.Path.GetFileName(filePath);
                if(string.IsNullOrWhiteSpace(name)) {
                    name = filePath;
                }
                Message = new ReactiveProperty<string>($"{name} のダウンロードが完了しました。");
                CommandOpenFile.Subscribe(() => {
                    Process.Start(FilePath);
                });
                CommandOpenExplorer.Subscribe(() => {
                    Process.Start("EXPLORER.EXE", $"/select,\"{FilePath}\"");
                });
                CommandClose.Subscribe(() => {
                    Owner.Close();
                });
            }
        }

        private DxxFileDispositionViewModel ViewModel {
            get => DataContext as DxxFileDispositionViewModel;
            set => DataContext = value;
        }

        public DxxFileDispositionDialog(string filePath) {
            ViewModel = new DxxFileDispositionViewModel(this, filePath);
            InitializeComponent();
        }

        public static void Show(string filePath, Window owner) {
            var dlg = new DxxFileDispositionDialog(filePath);
            dlg.Owner = owner;
            dlg.ShowDialog();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
        }
    }
}
