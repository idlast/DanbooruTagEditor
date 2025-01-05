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

namespace DanbooruTagEditor
{
    public partial class TranslateFixWindow : Window
    {
        public string EnglishTag { get; private set; }
        public string NewTranslation { get; private set; }

        public TranslateFixWindow(string englishTag, string currentJp)
        {
            InitializeComponent();

            // コンストラクタ引数で「どの英語タグか」「現状の日本語（あれば）」を渡せるように
            EnglishTag = englishTag;

            // 初期テキストは今の翻訳が入ってたらそれを表示
            TranslationTextBox.Text = currentJp ?? "";
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // テキストボックスにフォーカス
            TranslationTextBox.Focus();
            // 全選択しておくと便利
            TranslationTextBox.SelectAll();
        }

        private void TranslationTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                // 入力が確定
                NewTranslation = TranslationTextBox.Text.Trim();

                // ここでClose
                this.DialogResult = true; // ShowDialog() 側で受け取れる
                this.Close();
            }
        }
    }

}
