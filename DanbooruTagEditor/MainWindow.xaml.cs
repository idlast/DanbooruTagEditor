using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json;

namespace DanbooruTagEditor
{
    public partial class MainWindow : Window
    {
        private Border _previousSelectedBorder;
        private ImageItem _selectedItem;
        private string _currentFolder;
        private Dictionary<string, string> _enToJpMap = new Dictionary<string, string>();
        private List<ImageItem> _imageItemList = new List<ImageItem>();
        private bool _isJapaneseMode = true;  // デフォルトは日本語モード
        private Dictionary<string, string> _categoryColorMap = new Dictionary<string, string>();
        private Dictionary<string, string> _tagCategoryMap = new Dictionary<string, string>();

        private string _textBeforeLastDeletion = null;

        // カテゴリの並び順を定義
        private readonly List<string> _categoryOrder = new List<string>
        {
            "一般",
            "人数",
            "キャラ名",
            "作品名",
            "髪型",
            "髪色",
            "目の色",
            "表情",
            "眉",
            "衣装・服",
            "アクセサリ",
            "部位",
            "胸",
            "ポーズ・動作",
            "その他・未設定・デフォルト",
            "クオリティ"
        };

        /// <summary>
        /// コンストラクタ: 翻訳マップ・カテゴリマップの読込後に InitializeComponent を呼び出す
        /// </summary>
        public MainWindow()
        {
            LoadTranslateMap();
            LoadCategoryAndTagMaps();
            InitializeComponent();
        }

        /// <summary>
        /// 「フォルダを選択」ボタンクリック時の処理。  
        /// フォルダを選んで画像＆テキストファイルのリストを取得し、サムネイル一覧を表示する。
        /// </summary>
        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "画像とTXTファイルが入ったフォルダを選択してください";
                dialog.RootFolder = Environment.SpecialFolder.Desktop;

                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _currentFolder = dialog.SelectedPath;
                    LoadImagesAndTextFiles(_currentFolder);
                    DisplayThumbnails(_imageItemList);
                }
            }
        }

        /// <summary>
        /// 指定フォルダ内の画像ファイル(.jpg, .png など)と同名のテキストファイルがあれば記録して、_imageItemList にまとめる。
        /// </summary>
        private void LoadImagesAndTextFiles(string folderPath)
        {
            _imageItemList.Clear();

            string[] validExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var allFiles = Directory.GetFiles(folderPath);

            var imageFiles = allFiles
                .Where(file => validExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            foreach (var imgPath in imageFiles)
            {
                var baseName = Path.GetFileNameWithoutExtension(imgPath);
                var txtPath = Path.Combine(folderPath, baseName + ".txt");
                bool txtExists = File.Exists(txtPath);

                var item = new ImageItem
                {
                    ImagePath = imgPath,
                    TextPath = txtExists ? txtPath : null
                };
                _imageItemList.Add(item);
            }
        }

        /// <summary>
        /// すべてのサムネイルを表示する（_imageItemList 全件）。
        /// </summary>
        private void DisplayThumbnails()
        {
            DisplayThumbnails(_imageItemList);
        }

        /// <summary>
        /// 指定した画像リストをサムネイルとして UniformGrid に並べる。
        /// </summary>
        private void DisplayThumbnails(IEnumerable<ImageItem> items)
        {
            _textBeforeLastDeletion = null;
            if (UndoButton != null) // XAMLロード前に呼ばれる可能性を考慮
            {
                UndoButton.IsEnabled = false;
            }

            ThumbnailUniformGrid.Children.Clear();
            _previousSelectedBorder = null;
            _selectedItem = null;

            foreach (var item in items)
            {
                var border = new Border
                {
                    BorderBrush = System.Windows.Media.Brushes.Blue,
                    BorderThickness = new Thickness(2),
                    Margin = new Thickness(5),
                    Width = 110,
                    Height = 110
                };

                // サムネイル(中心の正方形を切り抜いて100×100に縮小)
                var thumb = CreateThumbnail(item.ImagePath);

                thumb.MouseLeftButtonUp += (s, e) =>
                {
                    // 選択したサムネの枠を黄色にして、_selectedItem を更新
                    if (_previousSelectedBorder != null)
                    {
                        _previousSelectedBorder.BorderBrush = System.Windows.Media.Brushes.Blue;
                    }
                    border.BorderBrush = System.Windows.Media.Brushes.Yellow;
                    _previousSelectedBorder = border;
                    _selectedItem = item;

                    ShowPreview(item);
                };

                border.Child = thumb;
                ThumbnailUniformGrid.Children.Add(border);
            }
        }

        /// <summary>
        /// 画像を読み込み、中央部分を最大限の正方形で切り出し、100×100に縮小して返す。
        /// </summary>
        private System.Windows.Controls.Image CreateThumbnail(string imagePath)
        {
            var src = new BitmapImage();
            src.BeginInit();
            src.UriSource = new Uri(imagePath, UriKind.Absolute);
            src.CacheOption = BitmapCacheOption.OnLoad;
            src.EndInit();

            int w = src.PixelWidth;
            int h = src.PixelHeight;

            // もっとも小さい辺を正方形の一辺に
            int side = Math.Min(w, h);
            int x = (w - side) / 2;
            int y = (h - side) / 2;

            var rect = new Int32Rect(x, y, side, side);
            var cropped = new CroppedBitmap(src, rect);

            // CroppedBitmap を 100×100 に表示するImage
            var thumb = new System.Windows.Controls.Image
            {
                Source = cropped,
                Width = 100,
                Height = 100,
                Stretch = Stretch.Uniform
            };
            return thumb;
        }

        /// <summary>
        /// 検索ボタン。 4つの検索キーワードを AND 条件で部分一致検索する。
        /// </summary>
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentFolder) || _imageItemList.Count == 0)
            {
                System.Windows.MessageBox.Show("まずはフォルダを選択して画像を読み込んでくださいませ～！！");
                return;
            }

            var input1 = SearchTextBox1.Text.Trim();
            var input2 = SearchTextBox2.Text.Trim();
            var input3 = SearchTextBox3.Text.Trim();
            var input4 = SearchTextBox4.Text.Trim();

            var searchKeywords = new List<string>();
            if (!string.IsNullOrEmpty(input1)) searchKeywords.Add(input1);
            if (!string.IsNullOrEmpty(input2)) searchKeywords.Add(input2);
            if (!string.IsNullOrEmpty(input3)) searchKeywords.Add(input3);
            if (!string.IsNullOrEmpty(input4)) searchKeywords.Add(input4);

            if (searchKeywords.Count == 0)
            {
                DisplayThumbnails(_imageItemList);
                return;
            }

            var filteredList = new List<ImageItem>();

            foreach (var item in _imageItemList)
            {
                if (string.IsNullOrEmpty(item.TextPath) || !File.Exists(item.TextPath))
                    continue;

                string rawText = File.ReadAllText(item.TextPath);
                bool matchedAll = true;

                foreach (var kw in searchKeywords)
                {
                    if (rawText.IndexOf(kw, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        matchedAll = false;
                        break;
                    }
                }

                if (matchedAll)
                {
                    filteredList.Add(item);
                }
            }

            DisplayThumbnails(filteredList);
        }

        /// <summary>
        /// 選択した画像を右上プレビューエリアに表示し、タグ一覧を表示、ファイル名を更新する。
        /// </summary>
        private void ShowPreview(ImageItem item)
        {
            _textBeforeLastDeletion = null;
            if (UndoButton != null) // XAMLロード前に呼ばれる可能性を考慮
            {
                UndoButton.IsEnabled = false;
            }

            PreviewImage.Source = new BitmapImage(new Uri(item.ImagePath));
            var fileName = Path.GetFileName(item.ImagePath);
            SelectedFileNameTextBlock.Text = fileName;

            DisplayTags(item);
        }

        /// <summary>
        /// 背景色(カラーコード)から、文字を白 or 黒に決定して返す。
        /// </summary>
        private System.Windows.Media.Brush DecideForegroundBrush(System.Windows.Media.Color bgColor)
        {
            double brightness = 0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B;
            return (brightness < 128) ? System.Windows.Media.Brushes.White : System.Windows.Media.Brushes.Black;
        }

        /// <summary>
        /// タグボタンをクリックしたときの削除処理。
        /// </summary>
        private void TagButton_Click(object sender, RoutedEventArgs e)
        {
            var clickedButton = sender as System.Windows.Controls.Button;
            if (clickedButton == null) return;

            string tagToRemove = clickedButton.Tag as string;
            if (string.IsNullOrEmpty(tagToRemove)) return;
            if (_selectedItem == null) return;
            if (string.IsNullOrEmpty(_selectedItem.TextPath) || !File.Exists(_selectedItem.TextPath)) return;

            string rawText = File.ReadAllText(_selectedItem.TextPath);
            var allTags = rawText.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t) && t != "|||")
                .ToList();

            bool removed = allTags.Remove(tagToRemove);
            if (!removed) return;

            _textBeforeLastDeletion = rawText;

            string updatedText = string.Join(", ", allTags);
            File.WriteAllText(_selectedItem.TextPath, updatedText);

            UndoButton.IsEnabled = true;

            DisplayTags(_selectedItem);
        }

        /// <summary>
        /// やり直しボタンクリック時の処理。直前のタグ削除操作を取り消す。
        /// </summary>
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            // 削除前のテキスト内容が記憶されていて、かつ画像が選択されている場合のみ実行
            if (_textBeforeLastDeletion != null && _selectedItem != null && !string.IsNullOrEmpty(_selectedItem.TextPath))
            {
                try
                {
                    // 記憶しておいた内容でファイルを元に戻す！
                    File.WriteAllText(_selectedItem.TextPath, _textBeforeLastDeletion);

                    // タグ表示を更新
                    DisplayTags(_selectedItem);

                    // 記憶をリセットし、やり直しボタンを無効化
                    _textBeforeLastDeletion = null;
                    UndoButton.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"あら大変！やり直し中にエラーが発生しましたの！\n{ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                    // エラーが起きても、とりあえずUndo状態はリセットしておく
                    _textBeforeLastDeletion = null;
                    UndoButton.IsEnabled = false;
                }
            }
            else
            {
                // 通常は IsEnabled=False なのでここには来ないはずですが、念のため
                System.Windows.MessageBox.Show("あら？元に戻せる操作がありませんわ。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
                UndoButton.IsEnabled = false; // 状態を確実に無効に
            }
        }

        /// <summary>
        /// タグ一覧を表示する。カテゴリマップがあれば色分けし、翻訳があれば日本語表示にする。
        /// </summary>
        private void DisplayTags(ImageItem item)
        {
            TagAreaPanel.Children.Clear();

            if (string.IsNullOrEmpty(item.TextPath) || !File.Exists(item.TextPath))
            {
                return;
            }

            var rawText = File.ReadAllText(item.TextPath);
            var tags = rawText.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList();

            // タグにカテゴリがなければ「その他・未設定・デフォルト」を割り当て
            for (int i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                if (!_tagCategoryMap.ContainsKey(t))
                {
                    _tagCategoryMap[t] = "その他・未設定・デフォルト";
                }
            }
            SaveTagCategoryMap();

            // カテゴリ順でソート
            var sortedTags = tags.OrderBy(tag =>
            {
                if (_tagCategoryMap.TryGetValue(tag, out var cat))
                {
                    int idx = _categoryOrder.IndexOf(cat);
                    return (idx >= 0) ? idx : int.MaxValue;
                }
                return int.MaxValue;
            }).ToList();

            foreach (var tag in sortedTags)
            {
                string displayedTag = tag;
                System.Windows.Media.Brush foreColor = null;

                if (_isJapaneseMode)
                {
                    if (_enToJpMap.TryGetValue(tag, out var jpText))
                    {
                        displayedTag = jpText;
                    }
                    else
                    {
                        displayedTag = tag;
                        foreColor = System.Windows.Media.Brushes.Red;
                    }
                }

                var tagButton = new System.Windows.Controls.Button
                {
                    Content = displayedTag,
                    Tag = tag
                };

                // カテゴリに応じた背景色
                if (_tagCategoryMap.TryGetValue(tag, out var category))
                {
                    if (!string.IsNullOrEmpty(category)
                        && _categoryColorMap.TryGetValue(category, out var colorCode)
                        && !string.IsNullOrEmpty(colorCode))
                    {
                        tagButton.Background = new SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorCode));
                    }
                    else
                    {
                        tagButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x48, 0xF9));
                    }
                }
                else
                {
                    tagButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x48, 0xF9));
                }

                var c = ((SolidColorBrush)tagButton.Background).Color;
                var decidedColor = DecideForegroundBrush(c);

                if (foreColor == null)
                {
                    foreColor = decidedColor;
                }
                tagButton.Foreground = foreColor;

                // 幅調整
                if (!_isJapaneseMode)
                {
                    tagButton.Width = displayedTag.Length * 7.5 + 15;
                }
                else
                {
                    tagButton.Width = displayedTag.Length * 12 + 15;
                }

                tagButton.Style = (Style)FindResource("TagButtonStyle");

                // 左クリックで削除
                tagButton.Click += TagButton_Click;

                // 右クリックでカテゴリ設定 or 翻訳修正
                tagButton.MouseRightButtonUp += (s, e) =>
                {
                    ShowTagContextMenu(tagButton, tag);
                    e.Handled = true;
                };

                TagAreaPanel.Children.Add(tagButton);
            }
        }

        /// <summary>
        /// マークボタン。選択中の画像ファイル名を marked.txt に追記する。
        /// </summary>
        private void MarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null)
            {
                System.Windows.MessageBox.Show("画像が選択されていませんわ～！！");
                return;
            }

            string markedFile = "marked.txt";
            var fileName = Path.GetFileName(_selectedItem.ImagePath);

            if (File.Exists(markedFile))
            {
                var lines = File.ReadAllLines(markedFile).ToList();
                if (!lines.Contains(fileName))
                {
                    lines.Add(fileName);
                    File.WriteAllLines(markedFile, lines);
                }
            }
            else
            {
                File.WriteAllText(markedFile, fileName + Environment.NewLine);
            }
        }

        /// <summary>
        /// テキストボックスからタグ文字列を取得し、テキストファイルに追加する。
        /// </summary>
        private void AddTagFromTextbox(string newTag)
        {
            if (_selectedItem == null)
            {
                System.Windows.MessageBox.Show("画像が選択されていませんわ！！");
                return;
            }

            // タグが空なら何もしない
            newTag = " " + newTag.Trim();
            if (string.IsNullOrEmpty(newTag))
            {
                return;
            }

            // 対応するtxtがない場合 → 新規作成
            if (string.IsNullOrEmpty(_selectedItem.TextPath))
            {
                var baseName = Path.GetFileNameWithoutExtension(_selectedItem.ImagePath);
                var folder = Path.GetDirectoryName(_selectedItem.ImagePath);
                var newTxtPath = Path.Combine(folder, baseName + ".txt");
                _selectedItem.TextPath = newTxtPath;

                File.WriteAllText(newTxtPath, newTag);
            }
            else
            {
                // 既存のタグリストを読み込む
                var rawText = File.Exists(_selectedItem.TextPath)
                    ? File.ReadAllText(_selectedItem.TextPath)
                    : "";

                var tags = rawText.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                // 5. 重複チェック (大小無視)
                bool alreadyExists = tags
                    .Any(t => t.Equals(newTag.Trim(), StringComparison.OrdinalIgnoreCase));

                if (!alreadyExists)
                {
                    // 追加
                    tags.Add(newTag);
                    tags = tags.Select(t => t.Trim()).ToList();
                    // カンマ+半角スペースで連結
                    var joined = string.Join(", ", tags);
                    File.WriteAllText(_selectedItem.TextPath, joined);
                }
                else
                {
                    System.Windows.MessageBox.Show("既に同じタグが存在しますわ～！！");
                }
            }

            DisplayTags(_selectedItem);

            _textBeforeLastDeletion = null;
            UndoButton.IsEnabled = false;
        }

        /// <summary>
        /// 1つ目の追加ボタン。テキストボックス1からタグを追加。
        /// </summary>
        private void TagAddButton_Click1(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox1.Text);
        }

        /// <summary>
        /// 2つ目の追加ボタン。テキストボックス2からタグを追加。
        /// </summary>
        private void TagAddButton_Click2(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox2.Text);
        }

        /// <summary>
        /// 3つ目の追加ボタン。テキストボックス3からタグを追加。
        /// </summary>
        private void TagAddButton_Click3(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox3.Text);
        }

        /// <summary>
        /// 4つ目の追加ボタン。テキストボックス4からタグを追加。
        /// </summary>
        private void TagAddButton_Click4(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox4.Text);
        }

        /// <summary>
        /// 5つ目の追加ボタン。テキストボックス4からタグを追加。
        /// </summary>
        private void TagAddButton_Click5(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox5.Text);
        }

        private void ClearSearch1_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox1.Clear();
        }

        private void ClearSearch2_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox2.Clear();
        }

        private void ClearSearch3_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox3.Clear();
        }

        private void ClearSearch4_Click(object sender, RoutedEventArgs e)
        {
            SearchTextBox4.Clear();
        }

        private void TagAddClearButton1_Click(object sender, RoutedEventArgs e)
        {
            TagAddTextBox1.Clear();
        }

        private void TagAddClearButton2_Click(object sender, RoutedEventArgs e)
        {
            TagAddTextBox2.Clear();
        }

        private void TagAddClearButton3_Click(object sender, RoutedEventArgs e)
        {
            TagAddTextBox3.Clear();
        }

        private void TagAddClearButton4_Click(object sender, RoutedEventArgs e)
        {
            TagAddTextBox4.Clear();
        }

        private void TagAddClearButton5_Click(object sender, RoutedEventArgs e)
        {
            TagAddTextBox5.Clear();
        }

        /// <summary>
        /// 言語切り替えボタン。_isJapaneseMode を反転し、タグ一覧を再表示。
        /// </summary>
        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            _isJapaneseMode = !_isJapaneseMode;

            if (_selectedItem != null)
            {
                DisplayTags(_selectedItem);
            }
        }

        /// <summary>
        /// 翻訳マップ(英語→日本語)を読み込み、_enToJpMap を更新する。
        /// </summary>
        private void LoadTranslateMap()
        {
            _enToJpMap.Clear();

            string mapPath = Settings.Default.translateCsvPath;
            if (!File.Exists(mapPath))
            {
                System.Windows.MessageBox.Show("translateMap.csv が見つかりませんでした。");
                return;
            }

            var lines = File.ReadAllLines(mapPath);
            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split(',');
                if (parts.Length < 2) continue;

                string en = parts[0].Trim();
                string jp = parts[1].Trim();

                if (!_enToJpMap.ContainsKey(en))
                {
                    _enToJpMap.Add(en, jp);
                }
            }
        }

        /// <summary>
        /// カテゴリ色マップ・タグカテゴリマップを読み込み、_categoryColorMap と _tagCategoryMap を更新する。
        /// </summary>
        private void LoadCategoryAndTagMaps()
        {
            string categoryColorMapPath = Settings.Default.categoryColorMapJsonPath;
            string tagCategoryMapPath = Settings.Default.tagCategoryMapJsonPath;

            // カテゴリ名→色コード
            if (File.Exists(categoryColorMapPath))
            {
                string json1 = File.ReadAllText(categoryColorMapPath);
                _categoryColorMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(json1)
                    ?? new Dictionary<string, string>();
            }
            else
            {
                System.Windows.MessageBox.Show("categoryColorMap.jsonが見つかりませんでした。");
                _categoryColorMap = new Dictionary<string, string>();
            }

            // タグ→カテゴリ名
            if (File.Exists(tagCategoryMapPath))
            {
                string json2 = File.ReadAllText(tagCategoryMapPath);
                _tagCategoryMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(json2)
                    ?? new Dictionary<string, string>();
            }
            else
            {
                System.Windows.MessageBox.Show("tagCategoryMap.jsonが見つかりませんでした。");
                _tagCategoryMap = new Dictionary<string, string>();
            }
        }

        /// <summary>
        /// 指定ボタン上で右クリックした際のコンテキストメニューを表示（カテゴリ設定 / 翻訳修正）。
        /// </summary>
        private void ShowTagContextMenu(System.Windows.Controls.Button tagButton, string englishTag)
        {
            var menu = new ContextMenu
            {
                Style = (Style)FindResource("FancyContextMenuStyle")
            };

            // カテゴリ設定
            var categoryItem = new MenuItem
            {
                Header = "カテゴリ設定",
                Style = (Style)FindResource("FancyMenuItemStyle")
            };
            categoryItem.Click += (s, e) =>
            {
                ShowCategoryWindow(englishTag);
            };
            menu.Items.Add(categoryItem);

            // 翻訳修正
            var translateItem = new MenuItem
            {
                Header = "翻訳修正",
                Style = (Style)FindResource("FancyMenuItemStyle")
            };
            translateItem.Click += (s, e) =>
            {
                ShowTranslateFixWindow(englishTag);
            };
            menu.Items.Add(translateItem);

            menu.PlacementTarget = tagButton;
            menu.IsOpen = true;
        }

        /// <summary>
        /// 「翻訳修正」用の小ウィンドウを表示。  
        /// 入力確定で CSV を更新し、再度 _enToJpMap を読み込み、タグ一覧を更新。
        /// </summary>
        private void ShowTranslateFixWindow(string englishTag)
        {
            string currentJp = null;
            if (_enToJpMap.TryGetValue(englishTag, out var jp))
            {
                currentJp = jp;
            }

            var fixWin = new TranslateFixWindow(englishTag, currentJp)
            {
                Owner = this
            };
            bool? dialogResult = fixWin.ShowDialog();

            if (dialogResult == true)
            {
                string newJp = fixWin.NewTranslation;
                if (!string.IsNullOrEmpty(newJp))
                {
                    UpdateTranslationCsv(englishTag, newJp);
                    LoadTranslateMap();

                    if (_selectedItem != null)
                    {
                        DisplayTags(_selectedItem);
                    }
                }
                else
                {
                    System.Windows.MessageBox.Show("空文字の翻訳は登録しませんでしたわ～！");
                }
            }
        }

        /// <summary>
        /// CSVにある英語タグの翻訳を上書き or 追記して、ファイルに書き戻す。
        /// </summary>
        private void UpdateTranslationCsv(string englishTag, string newJp)
        {
            string csvPath = Settings.Default.translateCsvPath;
            if (!File.Exists(csvPath))
            {
                File.WriteAllText(csvPath, $"{englishTag},{newJp}{Environment.NewLine}");
                return;
            }

            var lines = File.ReadAllLines(csvPath).ToList();
            bool found = false;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                    continue;

                var parts = line.Split(',');
                if (parts.Length >= 2)
                {
                    string en = parts[0].Trim();
                    if (en.Equals(englishTag, StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"{englishTag},{newJp}";
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                lines.Add($"{englishTag},{newJp}");
            }

            File.WriteAllLines(csvPath, lines);
        }

        /// <summary>
        /// カテゴリ設定用ウィンドウを表示。  
        /// ユーザーがカテゴリを選択したら tagCategoryMap を更新し、再表示。
        /// </summary>
        private void ShowCategoryWindow(string englishTag)
        {
            var catWin = new CategorySelectWindow(_categoryColorMap, englishTag)
            {
                Owner = this
            };

            catWin.CategorySelected += (tag, category) =>
            {
                _tagCategoryMap[tag] = category;
                SaveTagCategoryMap();

                if (_selectedItem != null)
                {
                    DisplayTags(_selectedItem);
                }
            };

            catWin.ShowDialog();
        }

        /// <summary>
        /// tagCategoryMap.json を保存する。  
        /// </summary>
        private void SaveTagCategoryMap()
        {
            var path = Settings.Default.tagCategoryMapJsonPath;
            var json = JsonConvert.SerializeObject(_tagCategoryMap, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }

    /// <summary>
    /// 画像とテキストファイルの対応を保持するクラス。
    /// </summary>
    public class ImageItem
    {
        public string ImagePath { get; set; }
        public string TextPath { get; set; }
        // 必要に応じて追加情報をキャッシュしてもよい
    }
}
