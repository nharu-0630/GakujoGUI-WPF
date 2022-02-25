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

<br />  

## 📦️ Installation

[Release](https://github.com/xyzyxJP/GakujoGUI-WPF/releases)

<br />  

## ⚙️ Usage

### ログイン

- 静大ID，パスワードをもとに学務情報システムへログインする
- CSS，画像ファイルなどを取得しないため，ブラウザ上のログインより高速である

<br />  

### 保存データ

- 全てのデータはローカル内に保存され，外部にアップロードされることはない

<br />  

#### アカウント

- 静大ID
- パスワード
- 学生氏名
- 学籍番号
- トークン
- 更新日時

<br />  

#### 授業連絡

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

<br />  

#### レポート

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

<br />  

#### 小テスト

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

<br />  

#### 授業共有ファイル

- 授業科目 学期/曜日時限
- タイトル
- サイズ
- ファイル
- ファイル説明
- 公開期間
- 更新日時

<br />  

#### 成績情報

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

<br />  

## ☑️ Todo

[Projects](https://github.com/xyzyxJP/GakujoGUI-WPF/projects/2)

<br />  

## 📜 License

[MIT License](https://opensource.org/licenses/MIT)