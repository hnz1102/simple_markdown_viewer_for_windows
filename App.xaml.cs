using System;
using System.Configuration;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace markdown_viewer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    // アプリケーションインスタンスの参照を保持
    private static MainWindow? _mainWindow;
    private static Mutex? _singleInstanceMutex;
    private static string _mutexName = "markdown_viewer_single_instance_mutex";
    private static string _pipeServerName = "markdown_viewer_pipe";
    private static bool _pipeServerRunning = false;
    private static CancellationTokenSource? _pipeServerCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // シングルインスタンスを確認
        bool isNewInstance = false;
        _singleInstanceMutex = new Mutex(true, _mutexName, out isNewInstance);

        if (!isNewInstance)
        {
            // 既に実行中のインスタンスがある場合、コマンドライン引数を送信して終了
            if (e.Args.Length > 0)
            {
                SendArgumentsToExistingInstance(e.Args[0]);
            }
            else
            {
                // 引数がない場合は、既存のインスタンスをアクティブにする
                ActivateExistingInstance();
            }
            Shutdown();
            return;
        }

        // 新しいインスタンス - 名前付きパイプサーバーを開始
        StartPipeServer();

        // メインウィンドウの作成
        _mainWindow = new MainWindow();
        
        // コマンドライン引数があればファイルを開く
        if (e.Args.Length > 0)
        {
            string filePath = e.Args[0];
            if (File.Exists(filePath) && Path.GetExtension(filePath).ToLower() == ".md")
            {
                // ファイルを開く（MainWindowは非表示のまま）
                _mainWindow.OpenFile(filePath);
            }
            else
            {
                // 有効なマークダウンファイルでない場合はメインウィンドウを表示
                _mainWindow.Show();
            }
        }
        else
        {
            // 引数がない場合はメインウィンドウを表示
            _mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // パイプサーバーを停止
        StopPipeServer();

        // ミューテックスを解放
        if (_singleInstanceMutex != null && !_singleInstanceMutex.SafeWaitHandle.IsClosed)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Close();
        }

        base.OnExit(e);
    }

    // 名前付きパイプサーバーを開始
    private void StartPipeServer()
    {
        if (_pipeServerRunning)
            return;

        _pipeServerRunning = true;
        _pipeServerCts = new CancellationTokenSource();
        var token = _pipeServerCts.Token;

        Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using (var pipeServer = new NamedPipeServerStream(_pipeServerName, PipeDirection.In))
                    {
                        // クライアントからの接続を待機
                        pipeServer.WaitForConnection();

                        // メッセージを読み取る
                        using (var reader = new StreamReader(pipeServer))
                        {
                            string message = reader.ReadToEnd();
                            
                            // UI スレッドでファイルを開く
                            Dispatcher.Invoke(() =>
                            {
                                if (message.StartsWith("OPEN:"))
                                {
                                    string filePath = message.Substring(5);
                                    if (File.Exists(filePath) && Path.GetExtension(filePath).ToLower() == ".md")
                                    {
                                        if (_mainWindow != null)
                                        {
                                            _mainWindow.OpenFile(filePath);
                                            _mainWindow.Activate();
                                        }
                                    }
                                }
                                else if (message == "ACTIVATE")
                                {
                                    if (_mainWindow != null)
                                    {
                                        _mainWindow.Activate();
                                    }
                                }
                            });
                        }
                    }
                }
                catch (Exception) when (token.IsCancellationRequested)
                {
                    // キャンセルされた場合は何もしない
                    break;
                }
                catch (Exception)
                {
                    // その他のエラーが発生した場合は少し待ってから再試行
                    Thread.Sleep(1000);
                }
            }
        }, token);
    }

    // パイプサーバーを停止
    private void StopPipeServer()
    {
        if (!_pipeServerRunning)
            return;

        _pipeServerCts?.Cancel();
        _pipeServerRunning = false;
    }

    // 既存のインスタンスに引数を送信
    private void SendArgumentsToExistingInstance(string filePath)
    {
        try
        {
            using (var pipeClient = new NamedPipeClientStream(".", _pipeServerName, PipeDirection.Out))
            {
                pipeClient.Connect(3000); // 3秒でタイムアウト

                using (var writer = new StreamWriter(pipeClient))
                {
                    writer.Write($"OPEN:{filePath}");
                    writer.Flush();
                }
            }
        }
        catch (Exception)
        {
            // 接続に失敗した場合、既存のインスタンスが応答しない可能性があるので、新しいインスタンスを起動する
        }
    }

    // 既存のインスタンスをアクティブにする
    private void ActivateExistingInstance()
    {
        try
        {
            using (var pipeClient = new NamedPipeClientStream(".", _pipeServerName, PipeDirection.Out))
            {
                pipeClient.Connect(3000); // 3秒でタイムアウト

                using (var writer = new StreamWriter(pipeClient))
                {
                    writer.Write("ACTIVATE");
                    writer.Flush();
                }
            }
        }
        catch (Exception)
        {
            // 接続に失敗した場合、既存のインスタンスが応答しない可能性があるので無視
        }
    }
}

