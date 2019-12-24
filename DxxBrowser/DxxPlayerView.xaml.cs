using Reactive.Bindings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace DxxBrowser {
    /// <summary>
    /// DxxPlayerView.xaml の相互作用ロジック
    /// </summary>
    public partial class DxxPlayerView : UserControl {

        public class DxxPlayerViewModel : DxxViewModelBase, ITimelineOwner {
            public ReactiveProperty<bool> IsPlaying { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> IsReady { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> HasNext { get; } = new ReactiveProperty<bool>(false);
            public ReactiveProperty<bool> HasPrev { get; } = new ReactiveProperty<bool>(false);
            public Subject<bool> Ended { get; } = new Subject<bool>();

            private bool ToBePlay = false;
            private WeakReference<MediaElement> mPlayer;

            public DxxPlayerViewModel() {

            }

            public MediaElement Player {
                get => mPlayer?.GetValue();
                set => mPlayer = new WeakReference<MediaElement>(value);
            }

            public double SeekPosition {
                get => Player.Position.TotalMilliseconds;
                set => Player.Position = TimeSpan.FromMilliseconds(value);
            }

            public void Play() {
                ToBePlay = true;
                if (IsReady.Value) {
                    IsPlaying.Value = true;
                    Player?.Play();
                }
            }

            public void Pause() {
                ToBePlay = false;
                if (IsPlaying.Value) {
                    IsPlaying.Value = false;
                    Player?.Pause();
                }
            }

            public void Stop() {
                ToBePlay = false;
                if (IsReady.Value) {
                    Player?.Stop();
                    IsPlaying.Value = false;
                }
            }


        }

        public DxxPlayerViewModel ViewModel {
            get => DataContext as DxxPlayerViewModel;
            set => DataContext = value;
        }

        public DxxPlayerView() {
            ViewModel = new DxxPlayerViewModel();
            InitializeComponent();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            ViewModel.Player = mMediaElement;
            mTimelineSlider.Initialize(ViewModel);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {

        }


        public Uri Source {
            get => mMediaElement.Source;
            set {
                ViewModel.IsReady.Value = false;
                mMediaElement.Source = value;
            }
        }

        private void OnMediaOpened(object sender, RoutedEventArgs e) {
            ViewModel.IsReady.Value = true;
            if(ToBePlay) {
                Play();
            }
        }

        private void OnMediaEnded(object sender, RoutedEventArgs e) {
            ViewModel.IsPlaying.Value = false;
            mMediaElement.Stop();
            ViewModel.Ended.OnNext(true);
        }

        private void OnMediaFailed(object sender, ExceptionRoutedEventArgs e) {
            ViewModel.IsReady.Value = false;
            ViewModel.IsPlaying.Value = false;
            ViewModel.Ended.OnNext(false);
        }

        private void OnPositionChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            updateTimelinePosition(e.NewValue, slider: false, player: !mUpdatingPositionFromTimer);
        }

        private bool mDragging = false;
        private DispatcherTimer mPositionTimer = null;
        private bool mUpdatingPositionFromTimer = false;

        private void OnSliderDragStateChanged(object e) {
            var state = (TimelineSlider.DragState)e;
            switch (state) {
                case TimelineSlider.DragState.START:
                    mDragging = true;
                    mMediaElement.Pause();
                    break;
                case TimelineSlider.DragState.DRAGGING:
                    updateTimelinePosition(mPositionSlider.Value, slider: false, player: true);
                    break;
                case TimelineSlider.DragState.END:
                    updateTimelinePosition(mPositionSlider.Value, slider: false, player: true);
                    mDragging = false;
                    if (ViewModel.IsPlaying.Value) {
                        mMediaElement.Play();
                    }
                    break;
            }
        }

        private void updateTimelinePosition(double position, bool slider, bool player) {
            if (player) {
                mMediaElement.Position = TimeSpan.FromMilliseconds(position);
            }
            if (slider) {
                mPositionSlider.Value = position;
            }
            SeekPositionText = $"{FormatDuration(position)}";
        }

    }
}
