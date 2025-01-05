using System.Windows;
using System.Windows.Media;

namespace DanbooruTagEditor // 名前空間が一致していることを確認
{
    public partial class CategorySelectWindow : Window // partial キーワードがあることを確認
    {
        private Dictionary<string, string> _categoryColorMap;
        private string _tag;

        public event Action<string, string> CategorySelected;

        public CategorySelectWindow(Dictionary<string, string> categoryColorMap, string tag)
        {
            InitializeComponent(); // エラーが解消されるはず
            _categoryColorMap = categoryColorMap;
            _tag = tag;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            foreach (var kvp in _categoryColorMap)
            {
                var categoryName = kvp.Key;
                var colorCode = kvp.Value;
                if (string.IsNullOrEmpty(colorCode))
                {
                    colorCode = "#FF808080";
                }

                var catButton = new System.Windows.Controls.Button
                {
                    Content = categoryName,
                    Margin = new Thickness(5),
                    Width = 80,
                    Height = 40
                };

                catButton.Background = new SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorCode));

                System.Windows.Media.Color col = ((SolidColorBrush)catButton.Background).Color;
                catButton.Foreground = DecideForeground(col);

                catButton.Click += (s, e2) =>
                {
                    CategorySelected?.Invoke(_tag, categoryName);
                    this.Close();
                };

                CategoryWrapPanel.Children.Add(catButton);
            }
        }

        private System.Windows.Media.Brush DecideForeground(System.Windows.Media.Color bgColor)
        {
            double brightness = 0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B;
            return (brightness < 128) ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Black;
        }
    }
}