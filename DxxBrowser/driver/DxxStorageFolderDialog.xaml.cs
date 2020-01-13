using Common;
using Microsoft.WindowsAPICodePack.Dialogs;
using Reactive.Bindings;
using System.Windows;

namespace DxxBrowser.driver {
    /// <summary>
    /// DxxStorageFolderDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxStorageFolderDialog : Window {
        public class DxxSettingsViewModel : MicViewModelBase {
            public ReactiveProperty<string> Path { get; }
            public string DriverName { get; }
            public DxxSettingsViewModel(string driverName, string initialPath) {
                DriverName = driverName;
                Path = new ReactiveProperty<string>(initialPath);
            }
        }

        public string Path => ViewModel.Path.Value;

        DxxSettingsViewModel ViewModel {
            get => DataContext as DxxSettingsViewModel;
            set { DataContext = value; }
        }

        public DxxStorageFolderDialog(string driverName, string initialPath) {
            ViewModel = new DxxSettingsViewModel(driverName, initialPath);
            InitializeComponent();
        }

        private void OnSelectFolder(object sender, RoutedEventArgs e) {
            using (var dlg = new CommonOpenFileDialog("Select Folder")) {
                dlg.IsFolderPicker = true;
                dlg.Multiselect = false;
                dlg.RestoreDirectory = true;
                if (dlg.ShowDialog(GetWindow(this)) == CommonFileDialogResult.Ok) {
                    ViewModel.Path.Value = dlg.FileName;
                }
            }
        }

        private void OnOK(object sender, RoutedEventArgs e) {
            this.DialogResult = true;
            this.Close();
        }

        private void OnCancel(object sender, RoutedEventArgs e) {
            this.DialogResult = false;
            this.Close();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            ViewModel.Dispose();
        }
    }
}
