// LR origin ラッパー（RadiKool 向け・ffmpeg / ffplay 兼用）
//
// RadiKool が呼ぶ ffmpeg.exe（録音）/ ffplay.exe（再生）になりすまし、
// 入力URLが LR (mtist.as.smartstream.ne.jp) のときだけ
// 「-headers "Origin: https://listenradio.jp"」を挿入して、本物の実行ファイルへ丸投げする。
//
//   ffmpeg.exe  → 本物 ffmpeg.origin.exe を呼ぶ（-i の前に -headers を挿入）
//   ffplay.exe  → 本物 ffplay.origin.exe を呼ぶ（位置引数URL の前に -headers を挿入）
//
// 本物の名前は「自分自身のファイル名 + .origin.exe」で自動決定する。
// そのため、ビルドした 1 つの exe を ffmpeg.exe / ffplay.exe の両方にコピーするだけで両対応できる。
//
// radiko / NHK など対象ホスト以外、または既に -headers が付いている引数は一切変更せず素通しする。
// 標準入出力と終了コードは完全に透過するので、RadiKool の録音停止（stdin への 'q'）もそのまま届く。

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

internal static class LrOriginWrapper
{
    // Origin 注入の対象となる入力URLのホスト（LR の配信CDN）
    private const string TargetHost = "mtist.as.smartstream.ne.jp";

    // 挿入するヘッダー（末尾 CRLF はヘッダー行の終端）
    private const string OriginHeaderValue = "Origin: https://listenradio.jp\r\n";

    private static int Main(string[] args)
    {
        string exeDir = AppDomain.CurrentDomain.BaseDirectory;

        // 本物の実行ファイル名 = 自分自身の名前 + ".origin.exe"
        //   ffmpeg.exe → ffmpeg.origin.exe / ffplay.exe → ffplay.origin.exe
        string selfBase;
        try
        {
            selfBase = Path.GetFileNameWithoutExtension(
                Process.GetCurrentProcess().MainModule.FileName);
        }
        catch
        {
            selfBase = "ffmpeg";
        }
        string realName = selfBase + ".origin.exe";
        string realPath = Path.Combine(exeDir, realName);

        if (!File.Exists(realPath))
        {
            Console.Error.WriteLine(
                "[lr-origin-wrapper] 本物の実行ファイルが見つかりません: " + realPath);
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
    //   ffmpeg: 「-i」の前に挿入。  ffplay: 入力URL（位置引数）の前に挿入。
    private static string[] BuildArgs(string[] args)
    {
        int iIndex = -1;
        int hostIndex = -1;
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
            if (hostIndex < 0 &&
                args[i].IndexOf(TargetHost, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                hostIndex = i;
            }
        }

        // 対象ホストを含まない / 既に -headers 付き なら素通し
        if (hostIndex < 0 || hasHeaders)
        {
            return args;
        }

        // 挿入位置: -i があれば -i の前（ffmpeg）、無ければ入力URLの前（ffplay の位置引数）
        int insertPos = (iIndex >= 0) ? iIndex : hostIndex;

        var result = new List<string>(args.Length + 2);
        for (int i = 0; i < insertPos; i++)
        {
            result.Add(args[i]);
        }
        result.Add("-headers");
        result.Add(OriginHeaderValue);
        for (int i = insertPos; i < args.Length; i++)
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
