# rk-lr-wrapper

**RadiKool で LR 系の放送局を録音できるようにする ffmpeg ラッパー**です。

LR 経由のコミュニティFMを RadiKool で予約録音すると
`403 Forbidden` で失敗する——という問題を、RadiKool 本体を改変せずに解決します。

## 背景：なぜ標準では録音できないのか

RadiKool は LR 局（タイプ `lr`）を ffmpeg の ` -i <URL> ...` で録音します。
ところが LR の配信サーバー（`mtist.as.smartstream.ne.jp`）は現在、HTTP リクエストに

```
Origin: https://listenradio.jp
```

ヘッダーが付いていないと **403 Forbidden** を返します。RadiKool はこのヘッダーを付けないため、
LR 局の録音が失敗します（radiko 用の認証ヘッダーは付ける作りですが、`lr` には付きません）。

## 仕組み

このツールは RadiKool が呼ぶ `ffmpeg.exe` になりすます小さな中継プログラムです。
**入力URLが LR（`mtist.as.smartstream.ne.jp`）のときだけ**
`-headers "Origin: https://listenradio.jp"` を `-i` の前に挿入し、本物の ffmpeg
（`ffmpeg.origin.exe`）へそのまま渡します。

```
RadiKool ──呼ぶ──▶ ffmpeg.exe（このラッパー）
                      │ 入力が mtist.as.smartstream.ne.jp なら
                      │   -headers "Origin: https://listenradio.jp" を -i の前に挿入
                      │ それ以外（radiko / NHK 等）は引数そのまま
                      ▼
                   ffmpeg.origin.exe（本物・リネーム済み）
```

- radiko・NHK など他局の引数は **一切変更しません**。
- 既に `-headers` が付いている引数には **二重付与しません**。
- 標準入出力（stdin/stdout/stderr）と終了コードを **完全に透過**するため、
  RadiKool の録音停止（停止時に ffmpeg の stdin へ送る `q`）もそのまま機能します。

## 動作環境

- Windows
- .NET Framework 4.x（RadiKool が要求するため通常は導入済み）
- RadiKool 本体

## 導入

1. [`rk-lr-wrapper.zip`](rk-lr-wrapper.zip) を解凍します。
2. 中の2つのファイル（`ffmpeg.wrapper.exe` と `wrap_n_rename_ffmpeg.bat`）を、
   **RadiKool の `libs` フォルダ**にコピーします。
   （`libs` は本物の `ffmpeg.exe` が入っているフォルダです。例: `…\radikool\libs`）
3. **RadiKool を終了**してから `wrap_n_rename_ffmpeg.bat` を実行します。

これだけです。書き込みに失敗する場合は、bat を右クリック →「管理者として実行」してください。

`wrap_n_rename_ffmpeg.bat` は、自分が置かれたフォルダで次のリネームを行います。

| before | after | 意味 |
|---|---|---|
| `ffmpeg.exe`（本物） | `ffmpeg.origin.exe` | 本物を退避 |
| `ffmpeg.wrapper.exe` | `ffmpeg.exe` | ラッパーを有効化 |

導入後、RadiKool で LR 局をそのまま録音できます。

## ソースからビルドする場合

`build.bat` を実行すると、同梱の `.NET Framework` の `csc.exe` で
[`ffmpeg_origin_wrapper.cs`](ffmpeg_origin_wrapper.cs) をコンパイルし、`ffmpeg.wrapper.exe` を生成します。

## ファイル一覧

| ファイル | 役割 |
|---|---|
| `ffmpeg_origin_wrapper.cs` | ラッパー本体のソース（C#） |
| `ffmpeg.wrapper.exe` | ビルド済みラッパー |
| `wrap_n_rename_ffmpeg.bat` | 導入（本物退避＋ラッパー有効化、その場で実行） |
| `build.bat` | ソースからビルド |
| `rk-lr-wrapper.zip` | 配布用（`ffmpeg.wrapper.exe` ＋ `wrap_n_rename_ffmpeg.bat`） |

## カスタマイズ

別の Origin やホストに対応させたい場合は、ソース冒頭の定数を変更して `build.bat` で再ビルドします。

- `TargetHost` … ヘッダーを挿入する入力URLのホスト（既定 `mtist.as.smartstream.ne.jp`）
- `OriginHeaderValue` … 挿入するヘッダー（既定 `Origin: https://listenradio.jp`）
- `RealExeName` … 本物の実行ファイル名（既定 `ffmpeg.origin.exe`）

## 注意事項

- 私的利用の範囲でご利用ください。
- 配信側の仕様変更により、将来動作しなくなる可能性があります。
- `ffmpeg` 本体は同梱していません（RadiKool 同梱のものを利用します）。
- `ffmpeg.wrapper.exe` は署名されていないため、初回実行時に Windows SmartScreen や
  ウイルス対策ソフトが警告を出す場合があります。気になる場合は `build.bat` で
  自分でビルドしてください。
- 本プログラムは「現状のまま（As-Is）」提供されます。制作者は、本プログラムの動作保証、バグの修正、または特定の目的への適合性について一切保証いたしません。本プログラムの使用に関連して生じた直接的、間接的、付随的、派生的な損害について、制作者は一切の責任を負わないものとします。

## License

[MIT](LICENSE) © 2026 ISHII Norifumi
