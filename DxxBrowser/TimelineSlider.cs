using Reactive.Bindings;
using System;
using System.Diagnostics;
using System.Reactive.Subjects;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace DxxBrowser {
    public interface ITimelineOwnerPlayer {
        IObservable<bool> IsPlayingProperty { get; }
        IObservable<double> DurationProperty { get; }
        double SeekPosition { get; set; }
        void Play();
        void Pause();
    }

    public class TimelineViewModel : DxxViewModelBase {
        public ReadOnlyReactiveProperty<bool> IsPlaying { get; set; }

        private WeakReference<Slider> mSlider;
        private Slider OwnerSlider => mSlider?.GetValue();
        private WeakReference<ITimelineOwnerPlayer> mPlayer;
        private ITimelineOwnerPlayer OwnerPlayer => mPlayer?.GetValue();

        public void Initialize(Slider slider, ITimelineOwnerPlayer owner) {
            mPlayer = new WeakReference<ITimelineOwnerPlayer>(owner);
            mSlider = new WeakReference<Slider>(slider);

            IsPlaying = owner.IsPlayingProperty.ToReadOnlyReactiveProperty();

            owner.DurationProperty.Subscribe((v) => {
                OwnerSlider.Maximum = v;
                OwnerSlider.LargeChange = v / 10;
                OwnerSlider.SmallChange = v / 100;
                Debug.WriteLine($"Duration = {v} ms");
            });
        }

        public void Play() {
            OwnerPlayer?.Play();
        }
        public void Pause() {
            OwnerPlayer?.Pause();
        }
        
        public void PlayerSeek() {
            var (player,slider) = (OwnerPlayer,OwnerSlider);
            if(player!=null&&slider!=null) { 
                var pos = slider.Value;
                player.SeekPosition = pos;
                Debug.WriteLine($"Player Seek: {pos} ms");
            }
        }

        public bool SliderSeekingAfterPlayer = false;

        public void SliderSeek() {
            var (player, slider) = (OwnerPlayer, OwnerSlider);
            if (player != null && slider != null) {
                var pos = player.SeekPosition;
                SliderSeekingAfterPlayer = true;
                slider.Value = pos;
                SliderSeekingAfterPlayer = false;
                Debug.WriteLine($"Slider Seek: {pos} ms");
            }
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
            Minimum = 0;
            Loaded -= OnLoaded;
            ValueChanged += OnValueChanged;
            Unloaded += OnUnloaded;
        }

        private void OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if(!ViewModel.SliderSeekingAfterPlayer) {
                ViewModel.PlayerSeek();
            }
            //Debug.WriteLine(e.ToString());
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            Unloaded -= OnUnloaded;
            mTimer.Stop();
            ViewModel.Dispose();
        }


        public void Initialize(ITimelineOwnerPlayer owner) {
            ViewModel.Initialize(this, owner);
            ViewModel.IsPlaying.Subscribe((v) => {
                if(v) {
                    Debug.WriteLine($"Seek Timer started");
                    mTimer.Start();
                } else {
                    Debug.WriteLine($"Seek Timer stopped");
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
            //ViewModel.PlayerSeek();
        }

        protected override void OnThumbDragCompleted(DragCompletedEventArgs e) {
            base.OnThumbDragCompleted(e);
            //ViewModel.PlayerSeek();
            if (mOrgPlaying) {
                ViewModel.Play();
            }
        }
    }
}
