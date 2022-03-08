<br />
<div align="center">
	<img src="https://github.com/xyzyxJP/GakujoGUI-WPF/blob/main/GakujoGUI/Resources/GakujoGUI.png?raw=true" width="128">
	<h2 style="font-weight: 600">GakujoGUI</h2>
	<p align="center">
	WPF Version<br>
	静岡大学 学務情報システム
	</p>
  	<img src="https://github.com/xyzyxJP/GakujoGUI-WPF/blob/main/GakujoGUI/Resources/MainForm.png?raw=true" width="640">
</div>

## ✨ Feature

- ✅ 学務情報システムのアプリケーション
- 💾 オフライン起動可能なローカル保存
- 🚧 メンテナンス / サーバーダウン時に閲覧可能 (事前に更新したデータに限る)
- 🕰️ 読み込み時間を大幅短縮
- 📅 時間割に未提出の課題を強調表示
- 💬 授業連絡などの内容を検索可能
- 🔄 バックグラウンドで自動更新
- 🛎️ 新着通知をDiscordでお知らせ
- 📝 未提出の課題をTodoistで管理
- 🌙 ライト / ダークテーマに対応

## 📦️ Installation

[Release](https://github.com/xyzyxJP/GakujoGUI-WPF/releases)

Zipファイルをダウンロードし，すべて展開を行ったあとに，`GakujoGUI.exe`を実行してください．  
ランタイムエラーが表示される場合は，[こちら](https://github.com/xyzyxJP/GakujoGUI-WPF#%EF%B8%8F-troubleshooting)を参照してください．

## ⚙️ Usage

### ログイン

静大ID，パスワードをもとに学務情報システムへログインします．  
CSS，画像ファイルなどを取得しないため，ブラウザ上のログインより高速です．

### 起動オプション

| 引数      | オプション | デフォルト値 |                                            | 
| :-------: | :--------: | :----------: | :----------------------------------------: | 
| -trace | なし       | なし         | ログ出力レベルをTraceに変更する                   | 

### 保存データ

全てのデータはローカル内に保存され，外部にアップロードされることはありません．

#### アカウント

<details>
<summary>詳細</summary>

- 静大ID
- パスワード
- 学生氏名
- 学籍番号
- トークン
- 更新日時
</details>

#### 授業連絡

<img src="https://user-images.githubusercontent.com/8305330/155840055-42d9f776-0ead-4550-89f5-69b3bcd50669.png" width="640">

<details>
<summary>詳細</summary>

- 授業科目 学期/曜日時限
- 担当教員氏名
- 連絡種別
- タイトル
- 内容
- ファイル
- ファイルリンク公開
- 参考URL
- 重要度
- 連絡日時
- WEB返信要求
</details>

#### レポート

<img src="https://user-images.githubusercontent.com/8305330/155840075-97eba8ee-8688-499c-a98b-354d4761625a.png" width="640">

<details>
<summary>詳細</summary>

- 授業科目 学期/曜日時限
- タイトル
- 状態
- 提出期間
- 最終提出日時
- 実施形式
- 操作
- `ReportId`
- `SchoolYear`
- `SubjectCode`
- `ClassCode`
</details>

#### 小テスト

<img src="https://user-images.githubusercontent.com/8305330/155840084-70c474f6-34d0-4a03-8672-e1919ec60ba1.png" width="640">

<details>
<summary>詳細</summary>

- 授業科目 学期/曜日時限
- タイトル
- 状態
- 提出期間
- 提出状況
- 実施形式
- 操作
- `QuizId`
- `SchoolYear`
- `SubjectCode`
- `ClassCode`
</details>

#### 授業共有ファイル

<img src="https://user-images.githubusercontent.com/8305330/155840102-ba833a9d-4a44-4d7a-81ec-510d7401dbed.png" width="640">

<details>
<summary>詳細</summary>

- 授業科目 学期/曜日時限
- タイトル
- サイズ
- ファイル
- ファイル説明
- 公開期間
- 更新日時
</details>

#### 成績情報

<img src="https://user-images.githubusercontent.com/8305330/155840121-d9d5b2b3-3a59-4a71-ab94-c00df1cba112.png" width="640">

<details>
<summary>詳細</summary>

- 科目名
- 担当教員名
- 科目区分
- 必修選択区分
- 単位
- 評価
- 得点
- 科目GP
- 取得年度
- 報告日
- 試験種別

<br />

- 評価別単位
- 学部内GPA
- 学科等内GPA
- 年別単位
</details>

### 外部連携

#### Todoist

<details>
<summary>連携方法</summary>

[https://todoist.com/app/settings/integrations](https://todoist.com/app/settings/integrations)

<img src="https://user-images.githubusercontent.com/8305330/155987366-b9ea2412-4bfd-4bb1-9aec-81aab80a1a0f.png" width="640">

`APIトークン`を`Todoistトークン`へ貼り付けてください．

</details>

#### Discord

<details>
<summary>連携方法</summary>

[https://discord.com/developers/applications](https://discord.com/developers/applications)

<img src="https://user-images.githubusercontent.com/8305330/155987712-ae77b2f0-6be2-4125-ab4c-f0fe5e9dff26.png" width="640">

<img src="https://user-images.githubusercontent.com/8305330/155987840-6400fcae-bd1c-4b16-9510-bc34521bf1f6.png" width="320">

`New Application`をクリックし，名前を入力して`Create`をクリックしてください．

<img src="https://user-images.githubusercontent.com/8305330/155988096-0cd08506-7331-4a32-b61a-49f29f7a74f6.png" width="640">

`Bot`をクリックし，`TOKEN`を`Discordトークン`へ貼り付けてください．

<img src="https://user-images.githubusercontent.com/8305330/155988479-6203c14e-b964-4fb4-b08a-df7852c07f86.png" width="640">

`OAuth2`の`URL Generator`をクリックし，`bot`にチェックを入れ`GENERATED URL`にアクセスしてください．

<img src="https://user-images.githubusercontent.com/8305330/155988809-9fcb503d-8174-446b-8026-b247f32f7ca8.png" width="320">

連携するチャンネルがあるサーバーを選択して，認証してください．  

Discordを開き，`設定`の`詳細設定`より`開発者モード`を有効にしてください．  
連携するチャンネルを右クリックし，`IDをコピー`をクリックし，`Discordチャンネル`へ貼り付けてください．  

</details>

## ⚠️ Troubleshooting

### ランタイムエラー

<details>
<summary>解決方法</summary>

<img src="https://user-images.githubusercontent.com/8305330/155836834-c9401ec2-e2c2-4d54-991a-30edfe8c314a.png" width="320">

上記画像のエラーが表示された場合は，実行に必要なランタイムが不足しています．

[https://dotnet.microsoft.com/ja-jp/download/dotnet/6.0/runtime](https://dotnet.microsoft.com/ja-jp/download/dotnet/6.0/runtime)  
上記リンクよりランタイムをインストールしてください．

<img src="https://user-images.githubusercontent.com/8305330/155836978-a9be93bb-2636-47bc-9ac6-b73293c4bb22.png" width="640">

ページ中央にある`デスクトップアプリを実行する`の`X64のダウンロード`をクリックしてください．

<img src="https://user-images.githubusercontent.com/8305330/155836991-0ec624ee-5141-4dcb-9451-7d1d6b9a8385.png" width="320">

インストールをクリックし，下記画像のメッセージが表示されれば完了です．

<img src="https://user-images.githubusercontent.com/8305330/155837022-cde00c30-ee15-4d1c-ac85-11dc61c3f036.png" width="320">

</details>

### ログインメール

<details>
<summary>解決方法</summary>

自動的にアーカイブ，既読もしくは削除することで，すべてのログインメールを非表示にすることが可能です．  
ログインメール以外の重要なメールが削除されることはありません．  
Gmailのフィルタ機能を使用するため，他のメールサービスの場合はできない可能性があります．

[https://mail.google.com/mail/u/0/#settings/filters](https://mail.google.com/mail/u/0/#settings/filters)  
上記リンクより新しいフィルタを作成します．



</details>

## ☑️ Todo

[Projects](https://github.com/xyzyxJP/GakujoGUI-WPF/projects/2)

## 📜 License

[MIT License](https://opensource.org/licenses/MIT)
