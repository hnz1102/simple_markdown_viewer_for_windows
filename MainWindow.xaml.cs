using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Markdig;

namespace markdown_viewer;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    // 開いたウィンドウを管理するためのディクショナリ (ファイルパス → ウィンドウ)
    private Dictionary<string, MarkdownViewWindow> _openedWindows = new();
    private bool _launchedWithFile = false;
    
    public MainWindow()
    {
        InitializeComponent();
    }

    // ファイルを開くための公開メソッド（App.xaml.csから呼び出される）
    public void OpenFile(string filePath)
    {
        _launchedWithFile = true;
        OpenMarkdownWindow(filePath);
        // ファイルを開いた場合、MainWindowを非表示にしない
        // this.Hide();
    }

    // 最後のMarkdownViewWindowが閉じられたときに呼び出すメソッド
    private void CheckWindowsAndShowMainIfNeeded()
    {
        if (_openedWindows.Count == 0)
        {
            // すべてのMarkdownViewWindowが閉じられた場合は
            // 起動方法に関わらずメインウィンドウを表示する
            this.Show();
            this.Activate();
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && System.IO.Path.GetExtension(files[0]).ToLower() == ".md")
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length > 0 && System.IO.Path.GetExtension(files[0]).ToLower() == ".md")
            {
                OpenMarkdownWindow(files[0]);
            }
        }
    }

    private void OpenMarkdownWindow(string filePath)
    {
        // 絶対パスに変換
        string absolutePath = System.IO.Path.GetFullPath(filePath);
        
        // 既に開いているウィンドウがあるか確認
        if (_openedWindows.TryGetValue(absolutePath, out var existingWindow) && existingWindow.IsLoaded)
        {
            // 既に開いているウィンドウがある場合はそちらをアクティブにする
            existingWindow.Activate();
            return;
        }

        string markdown = System.IO.File.ReadAllText(absolutePath);
        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        string style = "<style>table,th,td{border:1px solid #888;border-collapse:collapse;padding:4px;}</style>";

        // Mermaidブロックを一時的にプレースホルダに置換
        var mermaidBlocks = new List<string>();
        string mermaidPattern = @"```mermaid\s*([\s\S]*?)```";
        string replaced = System.Text.RegularExpressions.Regex.Replace(
            markdown,
            mermaidPattern,
            m => {
                mermaidBlocks.Add(m.Groups[1].Value.Trim());
                return $"[MERMAID_BLOCK_{mermaidBlocks.Count - 1}]";
            },
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        string htmlBody = Markdown.ToHtml(replaced, pipeline);

        // プレースホルダを<div class="mermaid">...</div>で復元
        for (int i = 0; i < mermaidBlocks.Count; i++)
        {
            string div = $"<div class=\"mermaid\">{mermaidBlocks[i]}</div>";
            htmlBody = htmlBody.Replace($"[MERMAID_BLOCK_{i}]", div);
        }

        string mermaidScript = "<script src=\"https://cdn.jsdelivr.net/npm/mermaid@10.9.0/dist/mermaid.min.js\"></script><script>mermaid.initialize({startOnLoad:true});</script>";
        string html = $"<html><head><meta charset=\"UTF-8\">{style}</head><body>{htmlBody}{mermaidScript}</body></html>";
        string fileName = System.IO.Path.GetFileName(absolutePath);
        
        // 新しいウィンドウを作成（マークダウンファイルのパスも渡す）
        var newWindow = new MarkdownViewWindow(html, fileName, absolutePath);
        newWindow.Width = 900; // 適切な幅
        newWindow.Height = 700; // 適切な高さ
        newWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen; // 画面中央に表示
        
        // ウィンドウが閉じられたときに辞書から削除して状態をチェック
        newWindow.Closed += (s, e) => 
        {
            if (_openedWindows.ContainsKey(absolutePath))
            {
                _openedWindows.Remove(absolutePath);
            }
            CheckWindowsAndShowMainIfNeeded();
        };
        
        // 辞書に追加して表示
        _openedWindows[absolutePath] = newWindow;
        newWindow.Show();
    }
}