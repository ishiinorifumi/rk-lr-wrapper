// ffmpeg origin ラッパー
//
// RadiKool が呼ぶ ffmpeg.exe になりすまし、入力URLが LR
// (mtist.as.smartstream.ne.jp) のときだけ「-headers "Origin: https://listenradio.jp"」を
// -i の前に挿入して、本物の ffmpeg（既定: ffmpeg.origin.exe）へ丸投げする。
//
// それ以外（radiko / NHK など）の引数は一切変更せずに素通しする。
// 標準入出力と終了コードは完全に透過するので、RadiKool からの録音停止（stdin への 'q'）も
// そのまま本物の ffmpeg に届く。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

internal static class FfmpegOriginWrapper
{
    // 本物の ffmpeg のファイル名（このラッパーと同じフォルダに置く）
    private const string RealExeName = "ffmpeg.origin.exe";

    // Origin 注入の対象となる入力URLのホスト（LR の配信CDN）
    private const string TargetHost = "mtist.as.smartstream.ne.jp";

    // 挿入するヘッダー（末尾 CRLF はヘッダー行の終端）
    private const string OriginHeaderValue = "Origin: https://listenradio.jp\r\n";

    private static int Main(string[] args)
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;
        string realPath = Path.Combine(exeDir, RealExeName);

        if (!File.Exists(realPath))
        {
            Console.Error.WriteLine(
                "[ffmpeg-origin-wrapper] 本物の ffmpeg が見つかりません: " + realPath);
            return 2;
        }

        string[] finalArgs = BuildArgs(args);
        string commandLine = ArgvToCommandLine(finalArgs);

        var psi = new ProcessStartInfo
        {
            FileName = realPath,
            Arguments = commandLine,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = Directory.GetCurrentDirectory(),
        };

        using (var child = new Process { StartInfo = psi })
        {
            child.Start();

            // 親(RadiKool) stdin -> 子 stdin（'q' による停止などを透過）
            var stdinThread = new Thread(() => PumpStdin(child)) { IsBackground = true };
            // 子 stdout/stderr -> 親（RadiKool がログ・進捗を読む）
            var stdoutThread = new Thread(() => PumpOut(child.StandardOutput.BaseStream,
                Console.OpenStandardOutput())) { IsBackground = true };
            var stderrThread = new Thread(() => PumpOut(child.StandardError.BaseStream,
                Console.OpenStandardError())) { IsBackground = true };

            stdinThread.Start();
            stdoutThread.Start();
            stderrThread.Start();

            child.WaitForExit();

            // 出力の取りこぼしを防ぐため、出力ポンプの終了を少し待つ
            stdoutThread.Join(2000);
            stderrThread.Join(2000);

            return child.ExitCode;
        }
    }

    // 必要なときだけ Origin ヘッダーを挿入した引数列を返す。
    private static string[] BuildArgs(string[] args)
    {
        int iIndex = -1;
        bool hasHeaders = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "-headers")
            {
                hasHeaders = true;
            }
            if (iIndex < 0 && args[i] == "-i")
            {
                iIndex = i;
            }
        }

        // -i が無い / 既に -headers 付き / 入力URLが対象ホストでない 場合は素通し
        if (iIndex < 0 || hasHeaders || iIndex + 1 >= args.Length)
        {
            return args;
        }

        string inputUrl = args[iIndex + 1];
        if (inputUrl.IndexOf(TargetHost, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return args;
        }

        // -i の直前に -headers "Origin: ..." を挿入
        var result = new List<string>(args.Length + 2);
        for (int i = 0; i < iIndex; i++)
        {
            result.Add(args[i]);
        }
        result.Add("-headers");
        result.Add(OriginHeaderValue);
        for (int i = iIndex; i < args.Length; i++)
        {
            result.Add(args[i]);
        }
        return result.ToArray();
    }

    // 親 stdin を子 stdin へ中継。親側が EOF になったら子 stdin を閉じる。
    private static void PumpStdin(Process child)
    {
        try
        {
            using (Stream input = Console.OpenStandardInput())
            using (Stream childIn = child.StandardInput.BaseStream)
            {
                var buf = new byte[4096];
                int n;
                while ((n = input.Read(buf, 0, buf.Length)) > 0)
                {
                    childIn.Write(buf, 0, n);
                    childIn.Flush();
                }
            }
        }
        catch
        {
            // 親が stdin を渡さない/閉じた場合などは無視
        }
    }

    // 子の出力ストリームを親の出力へ逐次中継（フラッシュしながら）。
    private static void PumpOut(Stream from, Stream to)
    {
        try
        {
            var buf = new byte[4096];
            int n;
            while ((n = from.Read(buf, 0, buf.Length)) > 0)
            {
                to.Write(buf, 0, n);
                to.Flush();
            }
        }
        catch
        {
            // 中継先が閉じられた場合などは無視
        }
    }

    // 引数配列を Windows のコマンドライン文字列へ正しくクオートして連結する
    // （CommandLineToArgvW の規則に対応）。
    private static string ArgvToCommandLine(string[] args)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }
            AppendQuoted(sb, args[i]);
        }
        return sb.ToString();
    }

    private static void AppendQuoted(StringBuilder sb, string arg)
    {
        // 空白・タブ・クオートを含まなければそのまま
        if (arg.Length > 0 && arg.IndexOfAny(new[] { ' ', '\t', '\n', '\v', '"' }) < 0)
        {
            sb.Append(arg);
            return;
        }

        sb.Append('"');
        for (int i = 0; ; i++)
        {
            int backslashes = 0;
            while (i < arg.Length && arg[i] == '\\')
            {
                i++;
                backslashes++;
            }

            if (i == arg.Length)
            {
                // 末尾のバックスラッシュは閉じクオート前で 2 倍にする
                sb.Append('\\', backslashes * 2);
                break;
            }
            else if (arg[i] == '"')
            {
                sb.Append('\\', backslashes * 2 + 1);
                sb.Append('"');
            }
            else
            {
                sb.Append('\\', backslashes);
                sb.Append(arg[i]);
            }
        }
        sb.Append('"');
    }
}
