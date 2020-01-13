using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Common;
using DxxBrowser.driver;
using Reactive.Bindings;

namespace DxxBrowser {
    /// <summary>
    /// DxxPlayer.xaml の相互作用ロジック
    /// </summary>
    /// 
    public partial class DxxPlayer : Window {
        public interface IPlayerOwner {
            Window OwnerWindow { get; }
            void PlayerClosed(DxxPlayer player);
            IDxxPlayList PlayList { get; }
        }

        private WeakReference<IPlayerOwner> mOwner;
        public IPlayerOwner PlayerOwner {
            get => mOwner?.GetValue();
            set => mOwner = new WeakReference<IPlayerOwner>(value);
        }

        private DxxPlayer(IPlayerOwner owner) {
            PlayerOwner = owner;
            Owner = owner.OwnerWindow; ;
            InitializeComponent();
        }

        //private static SourceReserver sReserver = null;
        //private static DxxPlayer sPlayer = null;

        //public static IDxxPlayList PlayList => sReserver;

        public static DxxPlayer ShowPlayer(IPlayerOwner owner) {
            var player = new DxxPlayer(owner);
            player.Show();
            return player;
        }

        // 現在再生中のアイテム（ウィンドウタイトルに表示）
        private ReadOnlyReactiveProperty<IDxxPlayItem> Current;

        private void OnLoaded(object sender, RoutedEventArgs e) {

            var playList = PlayerOwner.PlayList;
            mPlayer.Initialize(playList);
            Current = playList.Current.ToReadOnlyReactiveProperty();
            Current.Subscribe((v) => {
                if (null != v) {
                    Title = v.Description;
                } else {
                    Title = "No Sources";
                }
            });
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            PlayerOwner?.PlayerClosed(this);
            Current?.Dispose();
            Current = null;
        }
    }
}
