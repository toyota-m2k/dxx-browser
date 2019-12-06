using Microsoft.WindowsAPICodePack.Dialogs;
using Reactive.Bindings;
using System;
using System.Collections.Generic;
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

namespace DxxBrowser.driver.dmm
{
    /// <summary>
    /// DmmSettingsDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class DmmSettingsDialog : Window
    {
        public class DmmSettingsViewModel : DxxViewModelBase {
            public ReactiveProperty<string> Path { get; }
            public DmmSettingsViewModel(string initialPath) {
                Path = new ReactiveProperty<string>(initialPath);
            }
        }

        public string Path => ViewModel.Path.Value;

        DmmSettingsViewModel ViewModel {
            get => DataContext as DmmSettingsViewModel;
            set { DataContext = value; }
        }


        public DmmSettingsDialog(string initialPath)
        {
            ViewModel = new DmmSettingsViewModel(initialPath);
            InitializeComponent();
        }

        private void OnSelectFolder(object sender, RoutedEventArgs e) {
            using (var dlg = new CommonOpenFileDialog("Select Folder")) {
                dlg.IsFolderPicker = true;
                dlg.Multiselect = false;
                if (dlg.ShowDialog() == CommonFileDialogResult.Ok) {
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
