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

namespace DxxBrowser {
    /// <summary>
    /// DxxPlayer.xaml の相互作用ロジック
    /// </summary>
    /// 
    public interface IDxxPlayer {
        void AddSource(Uri source);
    }

    public partial class DxxPlayer : Window, IDxxPlayer {
        private DxxPlayer() {
            InitializeComponent();
        }

        public void AddSource(Uri source) {
            mPlayer.AddSource(source);
        }
        public void AddSource(IEnumerable<Uri> source) {
            mPlayer.AddSource(source);
        }

        private class Reserver : IDxxPlayer {
            public List<Uri> List { get; } = new List<Uri>();
            public Reserver() {
            }
            public void AddSource(Uri source) {
                List.Add(source);
            }
        }

        private static Reserver sReserver = new Reserver();
        private static DxxPlayer sPlayer = null;
        public static IDxxPlayer GetInstance() {
            return (IDxxPlayer)sPlayer ?? sReserver;
        }

        public static IDxxPlayer ShowPlayer() {
            if(sPlayer==null) {
                sPlayer = new DxxPlayer();
                sPlayer.Show();
                sPlayer.AddSource(sReserver.List);
            }
            return sPlayer;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {

        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            sPlayer = null;
        }

    }
}
