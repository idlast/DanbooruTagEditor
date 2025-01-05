using System.IO;
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
        private bool _isJapaneseMode = true; // デフォルトは日本語モード
        private Dictionary<string, string> _categoryColorMap = new Dictionary<string, string>();
        private Dictionary<string, string> _tagCategoryMap = new Dictionary<string, string>();
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

        public MainWindow()
        {
            LoadTranslateMap(); // ここで辞書を構築しておく
            LoadCategoryAndTagMaps();      // ← カテゴリ関連のマップ読み込み
            InitializeComponent();
        }

        private void SelectFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = "画像とTXTファイルが入ったフォルダを選択してください";
                dialog.RootFolder = Environment.SpecialFolder.Desktop;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    _currentFolder = dialog.SelectedPath;   // ここで保持

                    LoadImagesAndTextFiles(_currentFolder);
                    DisplayThumbnails(_imageItemList);  // ← 全件表示（引数付き版）
                }
            }
        }

        /// <summary>
        /// 指定フォルダ内の画像ファイルとTXT対応を読み込む
        /// </summary>
        private void LoadImagesAndTextFiles(string folderPath)
        {
            _imageItemList.Clear();

            string[] validExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp" };
            var allFiles = Directory.GetFiles(folderPath);

            var imageFiles = allFiles
                .Where(file => validExtensions.Contains(Path.GetExtension(file).ToLower()))
                .ToList();

            foreach (var imgPath in imageFiles)
            {
                var baseName = Path.GetFileNameWithoutExtension(imgPath);
                var txtPath = Path.Combine(folderPath, baseName + ".txt");
                bool txtExists = File.Exists(txtPath);

                // 必要であればTXTの中身をキャッシュしておくのもアリ
                // string tagContent = txtExists ? File.ReadAllText(txtPath) : null;

                var item = new ImageItem
                {
                    ImagePath = imgPath,
                    TextPath = txtExists ? txtPath : null
                    // TagContent = tagContent; // ← キャッシュするならこういう感じ
                };
                _imageItemList.Add(item);
            }
        }

        /// <summary>
        /// すべてのサムネイルを表示 (旧DisplayThumbnails() の中身)
        /// ※ 本体は下の「DisplayThumbnails(List<ImageItem>)」に集約
        /// </summary>
        private void DisplayThumbnails()
        {
            DisplayThumbnails(_imageItemList);
        }

        /// <summary>
        /// 引数に指定した画像リストだけ、サムネイル一覧として表示する
        /// </summary>
        private void DisplayThumbnails(IEnumerable<ImageItem> items)
        {
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
                    Height = 110,
                };

                var thumb = new System.Windows.Controls.Image
                {
                    Source = new BitmapImage(new Uri(item.ImagePath)),
                    Stretch = Stretch.UniformToFill,
                    Width = 100,
                    Height = 100,
                };

                // クリック時の選択処理
                thumb.MouseLeftButtonUp += (s, e) =>
                {
                    // 前回枠を青色に戻す
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
        /// 検索ボタン (4つの検索テキストボックスに対応した AND 部分一致)
        /// </summary>
        private void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            // フォルダ未選択や画像リスト0件なら何もしない
            if (string.IsNullOrEmpty(_currentFolder) || _imageItemList.Count == 0)
            {
                System.Windows.MessageBox.Show("まずはフォルダを選択して画像を読み込んでくださいませ～！！");
                return;
            }

            // 4つの検索ボックスの内容を取得
            var input1 = SearchTextBox1.Text.Trim();
            var input2 = SearchTextBox2.Text.Trim();
            var input3 = SearchTextBox3.Text.Trim();
            var input4 = SearchTextBox4.Text.Trim();

            // 空でないものだけリストに詰める
            List<string> searchKeywords = new List<string>();
            if (!string.IsNullOrEmpty(input1))
                searchKeywords.Add(input1);
            if (!string.IsNullOrEmpty(input2))
                searchKeywords.Add(input2);
            if (!string.IsNullOrEmpty(input3))
                searchKeywords.Add(input3);
            if (!string.IsNullOrEmpty(input4))
                searchKeywords.Add(input4);

            // もし4つとも空欄 → 全件表示
            if (searchKeywords.Count == 0)
            {
                DisplayThumbnails(_imageItemList);
                return;
            }

            // 絞り込み
            var filteredList = new List<ImageItem>();

            // 全画像を走査
            foreach (var item in _imageItemList)
            {
                // TXTファイルがなければスキップ
                if (string.IsNullOrEmpty(item.TextPath) || !File.Exists(item.TextPath))
                {
                    continue;
                }

                // TXTファイルの中身を全部読み込む
                string rawText = File.ReadAllText(item.TextPath);

                // すべてのキーワードが「部分一致」するかどうか(AND)
                bool matchedAll = true;
                foreach (var kw in searchKeywords)
                {
                    // 大文字小文字を区別しない検索
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

            // 絞り込み結果をサムネイル表示
            DisplayThumbnails(filteredList);
        }


        /// <summary>
        /// 画像プレビューを右上に表示＋タグ一覧更新＋ファイル名更新
        /// </summary>
        private void ShowPreview(ImageItem item)
        {
            PreviewImage.Source = new BitmapImage(new Uri(item.ImagePath));

            // ファイル名
            var fileName = Path.GetFileName(item.ImagePath);
            SelectedFileNameTextBlock.Text = fileName;

            // タグを表示
            DisplayTags(item);
        }
        private System.Windows.Media.Brush DecideForegroundBrush(System.Windows.Media.Color bgColor)
        {
            // 標準的な輝度計算 (Luma近似)
            double brightness = 0.299 * bgColor.R + 0.587 * bgColor.G + 0.114 * bgColor.B;
            // 明度が小さい → 背景が暗い → 文字色を白に
            if (brightness < 128)
            {
                return System.Windows.Media.Brushes.White;
            }
            else
            {
                return System.Windows.Media.Brushes.Black;
            }
        }

        private void TagButton_Click(object sender, RoutedEventArgs e)
        {
            // どのボタンが押されたか
            var clickedButton = sender as System.Windows.Controls.Button;
            if (clickedButton == null) return;

            // Button.Tag に入れた「タグ文字列」を取り出す
            string tagToRemove = clickedButton.Tag as string;
            if (string.IsNullOrEmpty(tagToRemove)) return;

            // 現在の選択画像がなければ終了
            if (_selectedItem == null) return;
            if (string.IsNullOrEmpty(_selectedItem.TextPath) || !File.Exists(_selectedItem.TextPath))
            {
                return;
            }

            // テキストファイルを読み込み
            string rawText = File.ReadAllText(_selectedItem.TextPath);
            var allTags = rawText.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t) && t != "|||")
                .ToList();

            // リストから削除
            bool removed = allTags.Remove(tagToRemove);
            if (!removed)
            {
                // もし見つからなければ何もしない
                return;
            }

            // 削除後にリストが空になったらどうするか…空文字にしてもいいし、ファイル削除してもいいし
            // とりあえず "," で再結合してファイルに書き戻す
            string updatedText = string.Join(", ", allTags);
            File.WriteAllText(_selectedItem.TextPath, updatedText);

            // 再表示 (DisplayTags) でもいいし、clickedButtonを直接UIから消すでもOK
            // ここでは画面をリフレッシュ
            DisplayTags(_selectedItem);
        }


        private void DisplayTags(ImageItem item)
        {
            // まずTagAreaPanelを一旦クリア
            TagAreaPanel.Children.Clear();

            // テキストファイル存在チェック
            if (string.IsNullOrEmpty(item.TextPath) || !File.Exists(item.TextPath))
            {
                return;
            }

            // ファイルを読み込み、カンマ区切りでタグを取得 (|||を外す場合はお兄様の意図に応じてWhereを修正)
            var rawText = File.ReadAllText(item.TextPath);
            var tags = rawText.Split(',')
                .Select(t => t.Trim())
                .Where(t => !string.IsNullOrEmpty(t))  // 既存の条件
                .ToList();

            for (int i = 0; i < tags.Count; i++)
            {
                var t = tags[i];
                if (!_tagCategoryMap.ContainsKey(t))
                {
                    _tagCategoryMap[t] = "その他・未設定・デフォルト";
                }
            }
            SaveTagCategoryMap();

            // ★★★ カテゴリ順でソート ★★★
            var sortedTags = tags.OrderBy(tag =>
            {
                if (_tagCategoryMap.TryGetValue(tag, out var category))
                {
                    int idx = _categoryOrder.IndexOf(category);
                    return (idx >= 0) ? idx : int.MaxValue;
                }
                else
                {
                    return int.MaxValue;
                }
            }).ToList();

            // タグごとにボタン生成
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
                        foreColor = System.Windows.Media.Brushes.Red; // 未登録なら赤文字
                    }
                }

                var tagButton = new System.Windows.Controls.Button
                {
                    Content = displayedTag,
                    Tag = tag // 英語タグ
                };

                // ★ カテゴリから色を決める
                if (_tagCategoryMap.TryGetValue(tag, out var category))
                {
                    // カテゴリが見つかったら、その色
                    if (!string.IsNullOrEmpty(category)
                        && _categoryColorMap.TryGetValue(category, out var colorCode)
                        && !string.IsNullOrEmpty(colorCode))
                    {
                        // colorCode = "#FFA500" など
                        tagButton.Background = new SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorCode));
                    }
                    else
                    {
                        // カラーコードが空 or カテゴリがcolorMapに無い → デフォルト色
                        tagButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x48, 0xF9));
                    }
                }
                else
                {
                    // カテゴリが無い → デフォルト色
                    tagButton.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x6A, 0x48, 0xF9));
                }

                // 文字色
                var c = ((SolidColorBrush)tagButton.Background).Color;
                var decidedColor = DecideForegroundBrush(c);
                if (foreColor == null)
                {
                    foreColor = decidedColor;
                }
                tagButton.Foreground = foreColor;

                // 幅計算
                if (!_isJapaneseMode)
                {
                    tagButton.Width = displayedTag.Length * 7.5 + 15;
                }
                else
                {
                    tagButton.Width = displayedTag.Length * 12 + 15;
                }

                // スタイル適用
                tagButton.Style = (Style)FindResource("TagButtonStyle");

                // 左クリック→削除
                tagButton.Click += TagButton_Click;

                // ★ 右クリック→カテゴリ設定 or 翻訳修正
                tagButton.MouseRightButtonUp += (s, e) =>
                {
                    // ここでフローティングメニュー(コンテキストメニュー)を表示する
                    ShowTagContextMenu(tagButton, tag);
                    e.Handled = true; // イベントバブリング防止
                };

                TagAreaPanel.Children.Add(tagButton);
            }
        }


        private void MarkButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedItem == null)
            {
                System.Windows.MessageBox.Show("画像が選択されていませんわ～！！");
                return;
            }

            // 例: 実際のパスはお兄様がご自由に変えてください
            string markedFile = "marked.txt";

            var fileName = Path.GetFileName(_selectedItem.ImagePath);

            // すでに書いてあるかチェック
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

        private void AddTagFromTextbox(string newTag)
        {
            // 1. 画像が選択されていない場合
            if (_selectedItem == null)
            {
                System.Windows.MessageBox.Show("画像が選択されていませんわ！！");
                return;
            }

            // 2. 追加タグが空文字か
            newTag = " " + newTag.Trim();
            if (string.IsNullOrEmpty(newTag))
            {
                // 空なら何もしない
                return;
            }

            // 3. 対応するtxtがない場合は新規作成するかどうか
            if (string.IsNullOrEmpty(_selectedItem.TextPath))
            {
                // ここでは「新規作成する」パターンとしましょう
                // 例: 同名.txtを新規に用意
                // ただしフォルダが存在しない可能性もあるので要注意。
                var baseName = System.IO.Path.GetFileNameWithoutExtension(_selectedItem.ImagePath);
                var folder = System.IO.Path.GetDirectoryName(_selectedItem.ImagePath);
                var newTxtPath = System.IO.Path.Combine(folder, baseName + ".txt");
                _selectedItem.TextPath = newTxtPath;

                // ファイルを作る
                File.WriteAllText(newTxtPath, newTag);
            }
            else
            {
                // 4. テキストファイルを読み込む
                var rawText = File.Exists(_selectedItem.TextPath)
                    ? File.ReadAllText(_selectedItem.TextPath)
                    : "";

                // 既存タグをカンマ区切りで分割
                var tags = rawText.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();

                // 5. 同じタグが既にあるかどうか
                bool alreadyExists = tags
                    .Any(t => t.Equals(newTag, StringComparison.OrdinalIgnoreCase));

                // 6. なければ追加
                if (!alreadyExists)
                {
                    tags.Add(newTag);
                    // 改めてカンマ連結して書き込み
                    var joined = string.Join(",", tags);
                    File.WriteAllText(_selectedItem.TextPath, joined);
                }
            }

            // 7. タグ表示をリフレッシュ
            DisplayTags(_selectedItem);
        }


        // =====================
        // 1つ目ボタン押下
        // =====================
        private void TagAddButton_Click1(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox1.Text);
            TagAddTextBox1.Clear();
        }

        // =====================
        // 2つ目
        // =====================
        private void TagAddButton_Click2(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox2.Text);
            TagAddTextBox2.Clear();
        }

        // =====================
        // 3つ目
        // =====================
        private void TagAddButton_Click3(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox3.Text);
            TagAddTextBox3.Clear();
        }

        // =====================
        // 4つ目
        // =====================
        private void TagAddButton_Click4(object sender, RoutedEventArgs e)
        {
            AddTagFromTextbox(TagAddTextBox4.Text);
            TagAddTextBox4.Clear();
        }

        private void LanguageButton_Click(object sender, RoutedEventArgs e)
        {
            // モードをひっくり返す
            _isJapaneseMode = !_isJapaneseMode;

            // もしすでに画像を選択しているならタグを再表示
            if (_selectedItem != null)
            {
                DisplayTags(_selectedItem);
            }
        }


        private void LoadTranslateMap()
        {
            _enToJpMap.Clear();

            string exeDir = Environment.CurrentDirectory;
            string mapPath = Settings.Default.translateCsvPath;
            if (!File.Exists(mapPath))
            {
                System.Windows.MessageBox.Show("translateMap.csv が見つかりませんでした。");
                return;
            }

            // CSVの各行: "english,日本語" の形式
            // 例: "highres,高画質"
            var lines = File.ReadAllLines(mapPath);
            foreach (var line in lines)
            {
                // 空行やコメント行があればスキップ
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

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
        private void LoadCategoryAndTagMaps()
        {
            // 例: Settings.Default.categoryColorMapJsonPath
            //     Settings.Default.tagCategoryMapJsonPath
            string categoryColorMapPath = Settings.Default.categoryColorMapJsonPath;
            string tagCategoryMapPath = Settings.Default.tagCategoryMapJsonPath;

            // categoryColorMap.json
            if (File.Exists(categoryColorMapPath))
            {
                string json1 = File.ReadAllText(categoryColorMapPath);
                // Newtonsoft.Json, System.Text.Json などを使ってパース
                _categoryColorMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(json1)
                    ?? new Dictionary<string, string>();
            }
            else
            {
                System.Windows.Forms.MessageBox.Show("categoryColorMap.jsonが見つかりませんでした。");
                _categoryColorMap = new Dictionary<string, string>();
            }

            // tagCategoryMap.json
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

        private void ShowTagContextMenu(System.Windows.Controls.Button tagButton, string englishTag)
        {
            var menu = new ContextMenu
            {
                Style = (Style)this.FindResource("FancyContextMenuStyle")
            };
            var style = (Style)this.FindResource("FancyMenuItemStyle");
            var categoryItem = new MenuItem
            {
                Header = "カテゴリ設定",
                Style = (Style)this.FindResource("FancyMenuItemStyle")
            };
            categoryItem.Click += (s, e) =>
            {
                ShowCategoryWindow(englishTag);
            };
            menu.Items.Add(categoryItem);

            var translateItem = new MenuItem
            {
                Header = "翻訳修正",
                Style = (Style)this.FindResource("FancyMenuItemStyle")
            };
            translateItem.Click += (s, e) =>
            {
                ShowTranslateFixWindow(englishTag);
            };
            menu.Items.Add(translateItem);

            menu.PlacementTarget = tagButton;
            menu.IsOpen = true;
        }

        private void ShowTranslateFixWindow(string englishTag)
        {
            // いま辞書に登録されている翻訳を取り出す (あれば表示したい)
            string currentJp = null;
            if (_enToJpMap.TryGetValue(englishTag, out var jp))
            {
                currentJp = jp;
            }

            // ウィンドウ作成
            var fixWin = new TranslateFixWindow(englishTag, currentJp);
            fixWin.Owner = this;

            // ダイアログを開く
            var dialogResult = fixWin.ShowDialog();

            // Enterが押されて閉じられた場合 → fixWin.DialogResult == true
            if (dialogResult == true)
            {
                // 新しい翻訳を取得
                string newJp = fixWin.NewTranslation;
                if (!string.IsNullOrEmpty(newJp))
                {
                    // CSVに書き込みor更新
                    UpdateTranslationCsv(englishTag, newJp);

                    // _enToJpMap を再読み込み
                    LoadTranslateMap();

                    // タグ一覧再描画
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

        private void UpdateTranslationCsv(string englishTag, string newJp)
        {
            string csvPath = Settings.Default.translateCsvPath;
            if (!File.Exists(csvPath))
            {
                // ファイルが無い場合、新規作成して "englishTag,newJp" 書く
                File.WriteAllText(csvPath, $"{englishTag},{newJp}{Environment.NewLine}");
                return;
            }

            var lines = File.ReadAllLines(csvPath).ToList();
            bool found = false;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                var parts = line.Split(',');
                if (parts.Length >= 2)
                {
                    string en = parts[0].Trim();
                    if (en.Equals(englishTag, StringComparison.OrdinalIgnoreCase))
                    {
                        // 既存の行を上書き
                        lines[i] = $"{englishTag},{newJp}";
                        found = true;
                        break;
                    }
                }
            }

            if (!found)
            {
                // 既存になかった → 末尾に追加
                lines.Add($"{englishTag},{newJp}");
            }

            File.WriteAllLines(csvPath, lines);
        }

        private void ShowCategoryWindow(string englishTag)
        {
            // サブウィンドウ生成
            var catWin = new CategorySelectWindow(_categoryColorMap, englishTag);
            catWin.Owner = this;
            catWin.CategorySelected += (tag, category) =>
            {
                // 上書きロジック
                _tagCategoryMap[tag] = category;

                // 2. JSON再書き込み
                SaveTagCategoryMap();

                // 3. タグ一覧再表示
                if (_selectedItem != null)
                {
                    DisplayTags(_selectedItem);
                }
            };

            catWin.ShowDialog();
        }

        private void SaveTagCategoryMap()
        {
            var path = Settings.Default.tagCategoryMapJsonPath;
            var json = JsonConvert.SerializeObject(_tagCategoryMap, Formatting.Indented);
            File.WriteAllText(path, json);
        }
    }

    public class ImageItem
    {
        public string ImagePath { get; set; }
        public string TextPath { get; set; }

        // もしタグの中身を先にキャッシュしたいなら↓
        // public string TagContent { get; set; }
    }
}
