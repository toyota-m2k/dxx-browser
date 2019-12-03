using System.Windows;
using System.Windows.Controls;

namespace DxxBrowser {
    /**
     * Button に IsChecked属性を追加したもの。
     * ToggleButtonに似ているが、Click操作でIsCheckedが自動的に変化しない。
     * MediaElementのPlayなどがDependencyPropertyではないために直接バインドできず、ボタンのClickイベントで操作する必要があるのだが、
     * 再生中などの状態を示すために、ToggleButtonを使うと、トグルボタンの内部状態(IsChecked)とプレーヤーのPlayingが一致しなくなる。
     * いろいろ頑張ってみたが、うまくいかないのでカスタムクラスとして実装。
     */
    public class CheckButton : Button {
        public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register("IsChecked", typeof(bool), typeof(CheckButton), new PropertyMetadata(false));
        public bool IsChecked {
            get => (bool)GetValue(IsCheckedProperty);
            set => SetValue(IsCheckedProperty, value);
        }
    }
}
