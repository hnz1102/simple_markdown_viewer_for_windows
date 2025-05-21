using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.IO;
using System.Windows;

namespace markdown_viewer
{
    public partial class MarkdownViewWindow : Window
    {
        private string _markdownHtml;
        private string _markdownFilePath; // マークダウンファイルのパスを保存するための変数を追加

        public MarkdownViewWindow(string htmlContent, string windowTitle = null, string markdownFilePath = null)
        {
            InitializeComponent();
            if (!string.IsNullOrEmpty(windowTitle))
            {
                this.Title = windowTitle;
            }
            _markdownHtml = "<meta charset=\"UTF-8\">" + htmlContent;
            _markdownFilePath = markdownFilePath; // マークダウンファイルのパスを保存
            
            // WebView2の初期化処理を強化
            Browser.Loaded += async (s, e) =>
            {
                try
                {
                    if (Browser.CoreWebView2 == null)
                    {
                        // ユーザーデータフォルダをアプリケーションインストールディレクトリの近くに設定
                        var options = new CoreWebView2EnvironmentOptions();
                        var appDataPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "MarkdownViewer", "WebView2Data");
                            
                        // ディレクトリが存在しない場合は作成
                        if (!Directory.Exists(appDataPath))
                        {
                            Directory.CreateDirectory(appDataPath);
                        }
                        
                        var env = await CoreWebView2Environment.CreateAsync(null, appDataPath, options);
                        await Browser.EnsureCoreWebView2Async(env);
                        
                        // マークダウンファイルのディレクトリをローカルコンテンツのルートとしてマッピング
                        if (!string.IsNullOrEmpty(_markdownFilePath))
                        {
                            string baseDir = Path.GetDirectoryName(_markdownFilePath);
                            if (!string.IsNullOrEmpty(baseDir))
                            {
                                // 仮想ホスト名をマークダウンのある場所にマッピング
                                Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                                    "mdview.local", baseDir, CoreWebView2HostResourceAccessKind.Allow);
                            }
                        }
                    }
                    
                    // 相対パスの画像URLを仮想ホストのURLに変換
                    string processedHtml = _markdownHtml;
                    if (!string.IsNullOrEmpty(_markdownFilePath))
                    {
                        // img srcの相対パスを仮想ホスト名を使用したURLに置換
                        processedHtml = processedHtml.Replace("src=\"doc/", "src=\"http://mdview.local/doc/");
                    }
                    
                    Browser.NavigateToString(processedHtml);
                    BackButton.Visibility = Visibility.Collapsed;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"WebView2の初期化中にエラーが発生しました。\n\n詳細: {ex.Message}", 
                                   "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            Browser.NavigationStarting += Browser_NavigationStarting;
        }

        // 既存のウィンドウの内容を更新するメソッド
        public void UpdateContent(string htmlContent, string windowTitle = null, string markdownFilePath = null)
        {
            if (!string.IsNullOrEmpty(windowTitle))
            {
                this.Title = windowTitle;
            }
            _markdownHtml = "<meta charset=\"UTF-8\">" + htmlContent;
            
            if (markdownFilePath != null)
            {
                _markdownFilePath = markdownFilePath;
            }
            
            // WebView2が初期化されている場合は直接コンテンツを更新
            if (Browser.CoreWebView2 != null)
            {
                // 相対パスの画像URLを仮想ホストのURLに変換
                string processedHtml = _markdownHtml;
                if (!string.IsNullOrEmpty(_markdownFilePath))
                {
                    // img srcの相対パスを仮想ホスト名を使用したURLに置換
                    processedHtml = processedHtml.Replace("src=\"doc/", "src=\"http://mdview.local/doc/");
                }
                
                Browser.NavigateToString(processedHtml);
                BackButton.Visibility = Visibility.Collapsed;
            }
        }

        private void Browser_NavigationStarting(object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs e)
        {
            // 外部リンクの場合のみ「戻る」ボタンを表示
            if (!e.Uri.StartsWith("data:") && !e.Uri.StartsWith("about:blank"))
            {
                BackButton.Visibility = Visibility.Visible;
            }
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            // マークダウンHTMLに戻す
            string processedHtml = _markdownHtml;
            if (!string.IsNullOrEmpty(_markdownFilePath))
            {
                // img srcの相対パスを仮想ホスト名を使用したURLに置換
                processedHtml = processedHtml.Replace("src=\"doc/", "src=\"http://mdview.local/doc/");
            }
            
            Browser.NavigateToString(processedHtml);
            BackButton.Visibility = Visibility.Collapsed;
        }
    }
}
