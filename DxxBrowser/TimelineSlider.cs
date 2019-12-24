using Reactive.Bindings;
using System;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace DxxBrowser {
    public interface ITimelineOwnerPlayer {
        IObservable<bool> IsPlayingProperty { get; }
        double SeekPosition { get; set; }
        void Play();
        void Pause();
    }

    public class TimelineViewModel : DxxViewModelBase {
        public ReactiveProperty<double> SeekPosition { get; } = new ReactiveProperty<double>(0);
        public ReadOnlyReactiveProperty<bool> IsPlaying { get; set; }

        private WeakReference<ITimelineOwnerPlayer> mOwner;
        private ITimelineOwnerPlayer Owner => mOwner?.GetValue();

        public void Initialize(ITimelineOwnerPlayer owner) {
            mOwner = new WeakReference<ITimelineOwnerPlayer>(owner);
            IsPlaying = owner.IsPlayingProperty.ToReadOnlyReactiveProperty();
        }

        public void Play() {
            Owner?.Play();
        }
        public void Pause() {
            Owner?.Pause();
        }
        
        public void PlayerSeek() {
            Owner?.Apply((v) => {
                v.SeekPosition = SeekPosition.Value;
            });
        }

        public void SliderSeek() {
            Owner?.Apply((v) => {
                SeekPosition.Value = v.SeekPosition;
            });
        }
    }

    public class TimelineSlider : Slider {
        private DispatcherTimer mTimer;
        private TimelineViewModel ViewModel {
            get => DataContext as TimelineViewModel;
            set => DataContext = value;
        }

        public TimelineSlider() {
            ViewModel = new TimelineViewModel();
            mTimer = new DispatcherTimer();
            mTimer.Interval = TimeSpan.FromMilliseconds(50);
            mTimer.Tick += (s, e) => {
                ViewModel.SliderSeek();
            };
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Loaded -= OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Unloaded -= OnUnloaded;
            mTimer.Stop();
            ViewModel.Dispose();
        }


        public void Initialize(ITimelineOwnerPlayer owner) {
            ViewModel.Initialize(owner);
            ViewModel.IsPlaying.Subscribe((v) => {
                if(v) {
                    mTimer.Start();
                } else {
                    mTimer.Stop();
                }
            });
        }

        private bool mOrgPlaying = false;
        protected override void OnThumbDragStarted(DragStartedEventArgs e) {
            base.OnThumbDragStarted(e);
            mOrgPlaying = ViewModel.IsPlaying.Value;
            ViewModel.Pause();
        }

        protected override void OnThumbDragDelta(DragDeltaEventArgs e) {
            base.OnThumbDragDelta(e);
            ViewModel.PlayerSeek();
        }

        protected override void OnThumbDragCompleted(DragCompletedEventArgs e) {
            base.OnThumbDragCompleted(e);
            ViewModel.PlayerSeek();
            if (mOrgPlaying) {
                ViewModel.Play();
            }
        }
    }
}
