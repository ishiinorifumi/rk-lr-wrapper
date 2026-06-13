# rk-lr-wrapper

**RadiKool で LR 系の放送局を「録音」も「再生」もできるようにするラッパー**です。

LR 経由のコミュニティFMを RadiKool で扱うと、録音も再生も
`403 Forbidden` で失敗する——という問題を、RadiKool 本体を改変せずに解決します。

## 背景：なぜ標準では録音・再生できないのか

RadiKool は LR 局（タイプ `lr`）を次のように扱います。

- **録音**: `ffmpeg.exe -i <URL> ...`
- **再生**: `ffplay.exe -nodisp ... "<URL>"`

ところが LR の配信サーバー（`mtist.as.smartstream.ne.jp`）は現在、HTTP リクエストに

```
Origin: https://listenradio.jp
```

ヘッダーが付いていないと **403 Forbidden** を返します。RadiKool は radiko 用の認証ヘッダーは付けますが、
`lr` には何も付けないため、**録音（ffmpeg）も再生（ffplay）も失敗**します。

## 仕組み

このツールは RadiKool が呼ぶ `ffmpeg.exe`（録音）と `ffplay.exe`（再生）の両方になりすます
小さな中継プログラムです。**入力URLが LR（`mtist.as.smartstream.ne.jp`）のときだけ**
`-headers "Origin: https://listenradio.jp"` を挿入し、本物（`*.origin.exe`）へそのまま渡します。

```
RadiKool ─録音─▶ ffmpeg.exe（ラッパー）─┐ 入力が mtist.as.smartstream.ne.jp なら
RadiKool ─再生─▶ ffplay.exe（ラッパー）─┤   -headers "Origin: https://listenradio.jp" を挿入
                                          ▼
                          ffmpeg.origin.exe / ffplay.origin.exe（本物・リネーム済み）
```

- 挿入位置は、ffmpeg なら `-i` の前、ffplay なら入力URL（位置引数）の前。
- 本物の名前は「自分自身のファイル名 + `.origin.exe`」で自動判定（`ffmpeg.exe`→`ffmpeg.origin.exe`、`ffplay.exe`→`ffplay.origin.exe`）。1つのビルド済み exe を両方にコピーするだけで動きます。
- radiko・NHK など他局（別ホスト）の引数は **一切変更しません**。
- 既に `-headers` が付いている引数には **二重付与しません**。
- 標準入出力（stdin/stdout/stderr）と終了コードを **完全に透過**するため、
  RadiKool の録音停止（`q` 送信）もそのまま機能します。

## 動作環境

- Windows / .NET Framework 4.x（RadiKool が要求するため通常は導入済み）
- RadiKool 本体

## 導入

1. [`rk-lr-wrapper.zip`](rk-lr-wrapper.zip) を解凍します。
2. 中のファイル（`ffmpeg.wrapper.exe` / `wrap_n_rename_ffmpeg.bat` / `unwrap.bat`）を、
   **RadiKool の `libs` フォルダ**にコピーします。
   （`libs` は本物の `ffmpeg.exe` / `ffplay.exe` が入っているフォルダです。例: `…\radikool\libs`）
3. **RadiKool を終了**してから `wrap_n_rename_ffmpeg.bat` を実行します。

書き込みに失敗する場合は、bat を右クリック →「管理者として実行」してください。

`wrap_n_rename_ffmpeg.bat` は、自分が置かれたフォルダで次の入れ替えを行います。

| before | after | 意味 |
|---|---|---|
| `ffmpeg.exe`（本物） | `ffmpeg.origin.exe` | 本物を退避（録音用） |
| `ffplay.exe`（本物） | `ffplay.origin.exe` | 本物を退避（再生用） |
| `ffmpeg.wrapper.exe` | → `ffmpeg.exe` にコピー | 録音ラッパー有効化 |
| `ffmpeg.wrapper.exe` | → `ffplay.exe` にコピー | 再生ラッパー有効化 |

導入後、RadiKool で LR 局の **再生・録音** がそのまま行えます。

## 取り外し（元に戻す）

`unwrap.bat` を実行すると、`*.origin.exe` を本物の `ffmpeg.exe` / `ffplay.exe` に戻します
（RadiKool を終了してから実行）。

## ソースからビルドする場合

`build.bat` を実行すると、`.NET Framework` の `csc.exe` で
[`ffmpeg_origin_wrapper.cs`](ffmpeg_origin_wrapper.cs) をコンパイルし、`ffmpeg.wrapper.exe` を生成します。

## ファイル一覧

| ファイル | 役割 |
|---|---|
| `ffmpeg_origin_wrapper.cs` | ラッパー本体のソース（C#・ffmpeg/ffplay 兼用） |
| `ffmpeg.wrapper.exe` | ビルド済みラッパー（ffmpeg.exe / ffplay.exe の両方にコピーして使う） |
| `wrap_n_rename_ffmpeg.bat` | 導入（本物退避＋ラッパー有効化） |
| `unwrap.bat` | 取り外し（本物へ復元） |
| `build.bat` | ソースからビルド |
| `rk-lr-wrapper.zip` | 配布用一式 |

## カスタマイズ

別の Origin やホストに対応させたい場合は、ソース冒頭の定数を変更して `build.bat` で再ビルドします。

- `TargetHost` … ヘッダーを挿入する入力URLのホスト（既定 `mtist.as.smartstream.ne.jp`）
- `OriginHeaderValue` … 挿入するヘッダー（既定 `Origin: https://listenradio.jp`）

（本物の実行ファイル名は固定の定数ではなく、ラッパー自身のファイル名から自動導出します。）

## 注意事項

- 私的利用の範囲でご利用ください。
- 配信側の仕様変更により、将来動作しなくなる可能性があります。
- `ffmpeg` / `ffplay` 本体は同梱していません（RadiKool 同梱のものを利用します）。
- `ffmpeg.wrapper.exe` は署名されていないため、初回実行時に Windows SmartScreen や
  ウイルス対策ソフトが警告を出す場合があります。気になる場合は `build.bat` で
  自分でビルドしてください。
- 本プログラムは「現状のまま（As-Is）」提供されます。制作者は、本プログラムの動作保証、バグの修正、または特定の目的への適合性について一切保証いたしません。本プログラムの使用に関連して生じた直接的、間接的、付随的、派生的な損害について、制作者は一切の責任を負わないものとします。

## License

[MIT](LICENSE) © 2026 ISHII Norifumi
