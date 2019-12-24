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
    public partial class DxxPlayer : Window {
        public DxxPlayer() {
            InitializeComponent();
            player.AutoPlay = true;
            var src = player.Source;
            var s = player.MediaPlayer.Source;

        }
    }
}
