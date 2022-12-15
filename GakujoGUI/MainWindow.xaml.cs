using GakujoGUI.Models;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;
using ModernWpf.Controls;
using ModernWpf.Controls.Primitives;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using MessageBox = ModernWpf.MessageBox;
using Path = System.IO.Path;

namespace GakujoGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly GakujoApi gakujoApi;
        private readonly NotifyApi notifyApi;
        private readonly Settings settings = new();
        private readonly DispatcherTimer autoLoadTimer = new();
        private readonly bool settingsFlag;
        private bool shutdownFlag;

        private static readonly string AssemblyName = Assembly.GetExecutingAssembly().GetName().Name!;

        private static string GetJsonPath(string value)
        {
            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyName)))
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyName));
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @$"{AssemblyName}\{value}.json");
        }
        private static readonly string SetupFilePath = Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, $"{AssemblyName}_Setup.exe");

        public MainWindow()
        {
            InitializeComponent();
            if (GetLatestVersions(out var latestVersion))
            {
                MessageBoxResult? messageBoxResult = MessageBoxResult.No;
                Dispatcher.Invoke(() => { messageBoxResult = MessageBox.Show($"更新があります．\nv{Assembly.GetExecutingAssembly().GetName().Version} -> v{latestVersion}", AssemblyName, MessageBoxButton.YesNo, MessageBoxImage.Information); });
                if (messageBoxResult == MessageBoxResult.Yes && File.Exists(SetupFilePath))
                    Process.Start("https://github.com/xyzyxJP/GakujoGUI-WPF/releases");
            }
            ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
            if (File.Exists(GetJsonPath("Settings")))
            {
                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(GetJsonPath("Settings")))!;
                logger.Info("Load Settings.");
            }
            if (settings.StartUpMinimize)
            {
                SetVisibility(Visibility.Hidden);
                logger.Info("Startup minimized.");
            }
            notifyApi = new();
            gakujoApi = new(settings.SchoolYear.ToString(), settings.SemesterCode, settings.UserAgent);
            UserIdTextBox.Text = GakujoApi.Unprotect(gakujoApi.Account.UserId, null!, DataProtectionScope.CurrentUser);
            PassWordPasswordBox.Password = GakujoApi.Unprotect(gakujoApi.Account.PassWord, null!, DataProtectionScope.CurrentUser);
            TodoistTokenPasswordBox.Password = GakujoApi.Unprotect(notifyApi.Tokens.TodoistToken, null!, DataProtectionScope.CurrentUser);
            DiscordChannelTextBox.Text = notifyApi.Tokens.DiscordChannel.ToString();
            DiscordTokenPasswordBox.Password = GakujoApi.Unprotect(notifyApi.Tokens.DiscordToken, null!, DataProtectionScope.CurrentUser);
            AutoLoadEnableCheckBox.IsChecked = settings.AutoLoadEnable;
            AutoLoadSpanNumberBox.Value = settings.AutoLoadSpan;
            StartUpEnableCheckBox.IsChecked = settings.StartUpEnable;
            StartUpMinimizeCheckBox.IsChecked = settings.StartUpMinimize;
            AlwaysVisibleTrayIconCheckBox.IsChecked = settings.AlwaysVisibleTrayIcon;
            SchoolYearNumberBox.Value = settings.SchoolYear;
            SchoolSemesterComboBox.SelectedIndex = settings.SemesterCode;
            UserAgentTextBox.Text = settings.UserAgent;
            UpdateBetaEnableCheckBox.IsChecked = settings.UpdateBetaEnable;
            ClassContactsTabVisibilityCheckBox.IsChecked = settings.ClassContactsTabVisibility;
            ReportsTabVisibilityCheckBox.IsChecked = settings.ReportsTabVisibility;
            QuizzesTabVisibilityCheckBox.IsChecked = settings.QuizzesTabVisibility;
            ClassSharedFilesTabVisibilityCheckBox.IsChecked = settings.ClassSharedFilesTabVisibility;
            LotteryRegistrationsTabVisibilityCheckBox.IsChecked = settings.LotteryRegistrationsTabVisibility;
            GeneralRegistrationsTabVisibilityCheckBox.IsChecked = settings.GeneralRegistrationsTabVisibility;
            ClassResultsTabVisibilityCheckBox.IsChecked = settings.ClassResultsTabVisibility;
            SyllabusSearchTabVisibilityCheckBox.IsChecked = settings.SyllabusSearchTabVisibility;
            ClassContactsTabItem.Visibility = settings.ClassContactsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            ReportsTabItem.Visibility = settings.ReportsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            QuizzesTabItem.Visibility = settings.QuizzesTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            ClassSharedFilesTabItem.Visibility = settings.ClassSharedFilesTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            LotteryRegistrationsTabItem.Visibility = settings.LotteryRegistrationsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            GeneralRegistrationsTabItem.Visibility = settings.GeneralRegistrationsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            ClassResultsTabItem.Visibility = settings.ClassResultsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            SyllabusSearchTabItem.Visibility = settings.SyllabusSearchTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            VersionLabel.Content = $"{Assembly.GetExecutingAssembly().GetName().Version}";
            autoLoadTimer.Interval = TimeSpan.FromMinutes(AutoLoadSpanNumberBox.Value);
            autoLoadTimer.Tick += LoadEvent;
            RefreshClassTablesDataGrid();
            RefreshClassContactsDataGrid();
            RefreshReportsDataGrid();
            RefreshQuizzesDataGrid();
            RefreshClassSharedFilesDataGrid();
            RefreshLotteryRegistrationsDataGrid();
            RefreshLotteryRegistrationsResultDataGrid();
            RefreshGeneralRegistrationsDataGrid();
            RefreshClassResultsDataGrid();
            settingsFlag = true;
            RefreshBackgroundImage();
            Task.Run(() =>
            {
                Login();
                Load();
                Dispatcher.Invoke(() =>
                {
                    if (!settings.AutoLoadEnable)
                        return;
                    autoLoadTimer.Start();
                    logger.Info("Start AutoLoadTimer.");
                });
            });
        }


        #region ログイン

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                Login();
                Load();
            });
        }

        private void Login()
        {
            Dispatcher.Invoke(() => { gakujoApi.SetAccount(UserIdTextBox.Text, PassWordPasswordBox.Password); });
            if (string.IsNullOrEmpty(gakujoApi.Account.UserId) || string.IsNullOrEmpty(gakujoApi.Account.PassWord))
                return;
            Dispatcher.Invoke(() =>
            {
                LoginButtonFontIcon.Visibility = Visibility.Collapsed;
                LoginButtonProgressRing.Visibility = Visibility.Visible;
            });
            if (!gakujoApi.Login(out var networkAvailable))
                Dispatcher.Invoke(() => MessageBox.Show(networkAvailable ? "自動ログインに失敗しました．静大IDまたはパスワードが正しくありません．" : "自動ログインに失敗しました．インターネット接続に問題があります．", AssemblyName, MessageBoxButton.OK, MessageBoxImage.Error));
            else
            {
                gakujoApi.GetClassTables();
                notifyApi.Login();
            }
            Dispatcher.Invoke(() =>
            {
                RefreshClassTablesDataGrid();
                LoginButtonFontIcon.Visibility = Visibility.Visible;
                LoginButtonProgressRing.Visibility = Visibility.Collapsed;
            });
        }

        private void LoadEvent(object? sender, EventArgs? e)
        {
            Task.Run(Load);
        }

        private void Load()
        {
            if (!gakujoApi.LoginStatus)
                return;
            Dispatcher.Invoke(() =>
            {
                LoadClassContactsButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadClassContactsButtonProgressRing.Visibility = Visibility.Visible;
                LoadReportsButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadReportsButtonProgressRing.Visibility = Visibility.Visible;
                LoadQuizzesButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadQuizzesButtonProgressRing.Visibility = Visibility.Visible;
                LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Visible;
                LoadLotteryRegistrationsButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadLotteryRegistrationsButtonProgressRing.Visibility = Visibility.Visible;
                LoadLotteryRegistrationsResultButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadLotteryRegistrationsResultButtonProgressRing.Visibility = Visibility.Visible;
                LoadGeneralRegistrationsButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadGeneralRegistrationsButtonProgressRing.Visibility = Visibility.Visible;
                LoadClassResultsButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadClassResultsButtonProgressRing.Visibility = Visibility.Visible;
            });
            gakujoApi.GetClassContacts(out var classContactsDiffCount);
            gakujoApi.GetReports(out var diffReports);
            gakujoApi.GetQuizzes(out var diffQuizzes);
            gakujoApi.GetClassSharedFiles(out var classSharedFilesDiffCount);
            gakujoApi.GetLotteryRegistrations(out _);
            gakujoApi.GetLotteryRegistrationsResult();
            gakujoApi.GetGeneralRegistrations();
            //抽選履修登録
            //gakujoApi.SetLotteryRegistrations(new() { new() { SubjectsName = "心理と行動Ａ", ClassName = "情工１", AspirationOrder = 1 } });
            //一般履修登録
            //gakujoApi.SetGeneralRegistrations(new() { new() { WeekdayPeriod = "木1･2", SubjectsName = "上級英語Ｄ", ClassName = "情工（Ｍ）１" } }, true);
            gakujoApi.GetClassResults(out var diffClassResults);
            Dispatcher.Invoke(() =>
            {
                RefreshClassContactsDataGrid();
                RefreshReportsDataGrid();
                RefreshQuizzesDataGrid();
                RefreshClassSharedFilesDataGrid();
                RefreshLotteryRegistrationsDataGrid();
                RefreshLotteryRegistrationsResultDataGrid();
                RefreshGeneralRegistrationsDataGrid();
                RefreshClassResultsDataGrid();
                if (classContactsDiffCount != gakujoApi.ClassContacts.Count)
                    gakujoApi.ClassContacts.GetRange(0, classContactsDiffCount).ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x);
                    });
                if (diffReports.Count != gakujoApi.Reports.Count)
                    diffReports.ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x);
                    });
                if (diffQuizzes.Count != gakujoApi.Quizzes.Count)
                    diffQuizzes.ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x);
                    });
                if (classSharedFilesDiffCount != gakujoApi.ClassSharedFiles.Count)
                    gakujoApi.ClassSharedFiles.GetRange(0, classSharedFilesDiffCount).ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x);
                    });
                if (diffClassResults.Count != gakujoApi.SchoolGrade.ClassResults.Count)
                    diffClassResults.ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x, true);
                    });
                notifyApi.SetTodoistTask(gakujoApi.Reports);
                notifyApi.SetTodoistTask(gakujoApi.Quizzes);
                LoadClassContactsButtonFontIcon.Visibility = Visibility.Visible;
                LoadClassContactsButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadReportsButtonFontIcon.Visibility = Visibility.Visible;
                LoadReportsButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadQuizzesButtonFontIcon.Visibility = Visibility.Visible;
                LoadQuizzesButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Visible;
                LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadLotteryRegistrationsButtonFontIcon.Visibility = Visibility.Visible;
                LoadLotteryRegistrationsButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadLotteryRegistrationsResultButtonFontIcon.Visibility = Visibility.Visible;
                LoadLotteryRegistrationsResultButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadGeneralRegistrationsButtonFontIcon.Visibility = Visibility.Visible;
                LoadGeneralRegistrationsButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadClassResultsButtonFontIcon.Visibility = Visibility.Visible;
                LoadClassResultsButtonProgressRing.Visibility = Visibility.Collapsed;
            });
        }

        private bool LoginStatusCheck()
        {
            if (gakujoApi.LoginStatus)
                return true;
            MessageBox.Show("ログイン状態ではありません．", AssemblyName, MessageBoxButton.OK, MessageBoxImage.Error); return false;
        }

        private void StartProcessFile(string path)
        {
            if (!File.Exists(path))
                return;
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            logger.Info($"Start Process {path}");
        }

        private void StartProcessExplorer(string path)
        {
            if (!File.Exists(path))
                return;
            Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/e,/select,\"{path}\"", UseShellExecute = true });
            logger.Info($"Start Process explorer.exe /e,/select,\"{path}\"");
        }

        private void OpenFileButton_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
                OpenFolderButton_Click(sender);
            else
            {
                DataObject dataObject = new(DataFormats.FileDrop, new[] { (string)((Button)sender).Tag });
                DragDrop.DoDragDrop((Button)sender, dataObject, DragDropEffects.Copy);
            }
        }

        private void OpenFileButton_Click(object sender, RoutedEventArgs e)
        {
            StartProcessFile((string)((Button)sender).Tag);
        }

        private void OpenFolderButton_Click(object sender)
        {
            StartProcessExplorer((string)((Button)sender).Tag);
        }

        #endregion

        #region 授業連絡

        private void ClassContactsSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            RefreshClassContactsDataGrid();
        }

        private void LoadClassContactsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadClassContactsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassContactsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetClassContacts(out var diffCount);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassContactsDataGrid();
                    gakujoApi.ClassContacts.GetRange(0, diffCount).ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x);
                    });
                    LoadClassContactsButtonFontIcon.Visibility = Visibility.Visible;
                    LoadClassContactsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void RefreshClassContactsDataGrid()
        {
            var collectionView = new CollectionViewSource { Source = gakujoApi.ClassContacts }.View;
            collectionView.Filter = item => ((ClassContact)item).Subjects.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Title.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Content.Contains(ClassContactsSearchAutoSuggestBox.Text);
            ClassContactsDateTimeLabel.Content = $"最終更新 {gakujoApi.Account.ClassContactDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassContactsDataGrid.ItemsSource = collectionView;
            ClassContactsDataGrid.Items.Refresh();
            logger.Info("Refresh ClassContactsDataGrid.");
        }

        private void RefreshFilesStackPanel(StackPanel stackPanel, string[] files)
        {
            if (files.Length == 0)
            {
                stackPanel.Children.Clear();
                stackPanel.Visibility = Visibility.Hidden;
            }
            else
            {
                stackPanel.Children.Clear();
                foreach (var item in files)
                {
                    StackPanel childStackPanel = new() { Orientation = Orientation.Horizontal };
                    if (!File.Exists(item))
                        childStackPanel.Children.Add(new FontIcon { FontFamily = new("Segoe MDL2 Assets"), Glyph = "\uE7BA", Margin = new(6, 0, 6, 0) });
                    childStackPanel.Children.Add(new Label { Content = Path.GetFileName(item) });
                    Button button = new()
                    {
                        AllowDrop = true,
                        Content = childStackPanel,
                        Tag = item,
                        Margin = new(6)
                    };
                    button.Click += OpenFileButton_Click;
                    button.MouseDown += OpenFileButton_MouseDown;
                    stackPanel.Children.Add(button);
                }
                stackPanel.Visibility = Visibility.Visible;
            }
        }

        private void ClassContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassContactsDataGrid.SelectedIndex != -1)
            {
                if (!((ClassContact)ClassContactsDataGrid.SelectedItem).IsAcquired || Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (MessageBox.Show("授業連絡の詳細を取得しますか．", AssemblyName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!LoginStatusCheck())
                            return;
                        var index = ClassContactsDataGrid.SelectedIndex;
                        Task.Run(() => gakujoApi.GetClassContact(index));
                    }
                }
                ClassContactContactDateTimeLabel.Content = ((ClassContact)ClassContactsDataGrid.SelectedItem).ContactDateTime.ToString("yyyy/MM/dd HH:mm");
                ClassContactContentTextBox.Text = ((ClassContact)ClassContactsDataGrid.SelectedItem).Content;
                RefreshFilesStackPanel(ClassContactFilesStackPanel, ((ClassContact)ClassContactsDataGrid.SelectedItem).Files);
            }
            else
            {
                ClassContactContactDateTimeLabel.Content = "連絡日時";
                ClassContactContentTextBox.Text = "内容";
                ClassContactFilesStackPanel.Children.Clear();
                ClassContactFilesStackPanel.Visibility = Visibility.Hidden;
            }
        }

        #endregion

        #region レポート

        private void ReportsSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            RefreshReportsDataGrid();
        }

        private void LoadReportsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadReportsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadReportsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetReports(out var diffReports);
                notifyApi.SetTodoistTask(gakujoApi.Reports);
                Dispatcher.Invoke(() =>
                {
                    RefreshReportsDataGrid();
                    diffReports.ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x);
                    });
                    LoadReportsButtonFontIcon.Visibility = Visibility.Visible;
                    LoadReportsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void FilterReportsCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            RefreshReportsDataGrid();
        }

        private void RefreshReportsDataGrid()
        {
            var collectionView = new CollectionViewSource { Source = gakujoApi.Reports }.View;
            collectionView.Filter = item => (((Report)item).Subjects.Contains(ReportsSearchAutoSuggestBox.Text) || ((Report)item).Title.Contains(ReportsSearchAutoSuggestBox.Text)) && (!(bool)FilterReportsCheckBox.IsChecked! || ((Report)item).IsSubmittable);
            ReportsDateTimeLabel.Content = $"最終更新 {gakujoApi.Account.ReportDateTime:yyyy/MM/dd HH:mm:ss}";
            ReportsDataGrid.ItemsSource = collectionView;
            ReportsDataGrid.Items.Refresh();
            logger.Info("Refresh ReportsDataGrid.");
        }

        private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportsDataGrid.SelectedIndex != -1)
            {
                if (!((Report)ReportsDataGrid.SelectedItem).IsAcquired || Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (MessageBox.Show("レポートの詳細を取得しますか．", AssemblyName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!LoginStatusCheck())
                            return;
                        Task.Run(() => gakujoApi.GetReport((Report)ReportsDataGrid.SelectedItem));
                    }
                }
                ReportStartEndDateTimeLabel.Content = $"{((Report)ReportsDataGrid.SelectedItem).StartDateTime:yyyy/MM/dd HH:mm} -> {((Report)ReportsDataGrid.SelectedItem).EndDateTime:yyyy/MM/dd HH:mm}" + (DateTime.Now < ((Report)ReportsDataGrid.SelectedItem).EndDateTime ? $" (残り{((Report)ReportsDataGrid.SelectedItem).EndDateTime - DateTime.Now:d'日'h'時間'm'分'})" : " (締切)");
                ReportDescriptionTextBox.Text = ((Report)ReportsDataGrid.SelectedItem).Description;
                ReportMessageTextBox.Text = ((Report)ReportsDataGrid.SelectedItem).Message;
                RefreshFilesStackPanel(ReportFilesStackPanel, ((Report)ReportsDataGrid.SelectedItem).Files);
            }
            else
            {
                ReportStartEndDateTimeLabel.Content = "提出期間";
                ReportDescriptionTextBox.Text = "説明";
                ReportMessageTextBox.Text = "伝達事項";
                ReportFilesStackPanel.Children.Clear();
                ReportFilesStackPanel.Visibility = Visibility.Hidden;
            }
        }

        #endregion

        #region 小テスト

        private void QuizzesSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            RefreshQuizzesDataGrid();
        }

        private void LoadQuizzesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadQuizzesButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadQuizzesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetQuizzes(out var diffQuizzes);
                notifyApi.SetTodoistTask(gakujoApi.Quizzes);
                Dispatcher.Invoke(() =>
                {
                    RefreshQuizzesDataGrid();
                    diffQuizzes.ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x);
                    });
                    LoadQuizzesButtonFontIcon.Visibility = Visibility.Visible;
                    LoadQuizzesButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void FilterQuizzesCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            RefreshQuizzesDataGrid();
        }

        private void RefreshQuizzesDataGrid()
        {
            var collectionView = new CollectionViewSource { Source = gakujoApi.Quizzes }.View;
            collectionView.Filter = item => (((Quiz)item).Subjects.Contains(QuizzesSearchAutoSuggestBox.Text) || ((Quiz)item).Title.Contains(QuizzesSearchAutoSuggestBox.Text)) && (!(bool)FilterQuizzesCheckBox.IsChecked! || ((Quiz)item).IsSubmittable);
            QuizzesDateTimeLabel.Content = $"最終更新 {gakujoApi.Account.QuizDateTime:yyyy/MM/dd HH:mm:ss}";
            QuizzesDataGrid.ItemsSource = collectionView;
            QuizzesDataGrid.Items.Refresh();
            logger.Info("Refresh QuizzesDataGrid.");
        }

        private void QuizzesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuizzesDataGrid.SelectedIndex != -1)
            {
                if (!((Quiz)QuizzesDataGrid.SelectedItem).IsAcquired || Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (MessageBox.Show("小テストの詳細を取得しますか．", AssemblyName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!LoginStatusCheck())
                            return;
                        Task.Run(() => gakujoApi.GetQuiz((Quiz)QuizzesDataGrid.SelectedItem));
                    }
                }
                QuizStartEndDateTimeLabel.Content = $"{((Quiz)QuizzesDataGrid.SelectedItem).StartDateTime:yyyy/MM/dd HH:mm} -> {((Quiz)QuizzesDataGrid.SelectedItem).EndDateTime:yyyy/MM/dd HH:mm}" + (DateTime.Now < ((Quiz)QuizzesDataGrid.SelectedItem).EndDateTime ? $" (残り{((Quiz)QuizzesDataGrid.SelectedItem).EndDateTime - DateTime.Now:d'日'h'時間'm'分'})" : " (締切)");
                QuizDescriptionTextBox.Text = ((Quiz)QuizzesDataGrid.SelectedItem).Description;
                RefreshFilesStackPanel(QuizFilesStackPanel, ((Quiz)QuizzesDataGrid.SelectedItem).Files);
            }
            else
            {
                QuizStartEndDateTimeLabel.Content = "提出期間";
                QuizDescriptionTextBox.Text = "説明";
                QuizFilesStackPanel.Children.Clear();
                QuizFilesStackPanel.Visibility = Visibility.Hidden;
            }
        }

        #endregion

        #region 授業共有ファイル

        private void ClassSharedFilesSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            RefreshClassSharedFilesDataGrid();
        }

        private void LoadClassSharedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetClassSharedFiles(out var diffCount);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassSharedFilesDataGrid();
                    gakujoApi.ClassSharedFiles.GetRange(0, diffCount).ForEach(x =>
                    {
                        NotifyToast(x);
                        notifyApi.NotifyDiscord(x);
                    });
                    LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Visible;
                    LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void RefreshClassSharedFilesDataGrid()
        {
            var collectionView = new CollectionViewSource { Source = gakujoApi.ClassSharedFiles }.View;
            collectionView.Filter = item => ((ClassSharedFile)item).Subjects.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Title.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Description.Contains(ClassSharedFilesSearchAutoSuggestBox.Text);
            ClassSharedFilesDateTimeLabel.Content = $"最終更新 {gakujoApi.Account.ClassSharedFileDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassSharedFilesDataGrid.ItemsSource = collectionView;
            ClassSharedFilesDataGrid.Items.Refresh();
            logger.Info("Refresh ClassSharedFilesDataGrid.");
        }

        private void ClassSharedFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSharedFilesDataGrid.SelectedIndex != -1)
            {
                if (!((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).IsAcquired || Keyboard.Modifiers == ModifierKeys.Shift)
                {
                    if (MessageBox.Show("授業共有ファイルの詳細を取得しますか．", AssemblyName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!LoginStatusCheck())
                            return;
                        var index = ClassSharedFilesDataGrid.SelectedIndex;
                        Task.Run(() => gakujoApi.GetClassSharedFile(index, (ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem));
                    }
                }
                ClassSharedFileUpdateDateTimeLabel.Content = ((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).UpdateDateTime.ToString("yyyy/MM/dd HH:mm");
                ClassSharedFileDescriptionTextBox.Text = ((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Description;
                RefreshFilesStackPanel(ClassSharedFilesStackPanel, ((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files);
            }
            else
            {
                ClassSharedFileUpdateDateTimeLabel.Content = "更新日時";
                ClassSharedFileDescriptionTextBox.Text = "ファイル説明";
                ClassSharedFilesStackPanel.Children.Clear();
            }
        }

        #endregion

        #region 抽選履修登録

        private void LoadLotteryRegistrationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadLotteryRegistrationsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadLotteryRegistrationsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetLotteryRegistrations(out _);
                Dispatcher.Invoke(() =>
                {
                    RefreshLotteryRegistrationsDataGrid();
                    LoadLotteryRegistrationsButtonFontIcon.Visibility = Visibility.Visible;
                    LoadLotteryRegistrationsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void FilterLotteryRegistrationsCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            RefreshLotteryRegistrationsDataGrid();
        }

        private void RefreshLotteryRegistrationsDataGrid()
        {
            var collectionView = new CollectionViewSource { Source = gakujoApi.LotteryRegistrations.SelectMany(_ => _) }.View;
            collectionView.Filter = item => !(bool)FilterLotteryRegistrationsCheckBox.IsChecked! || ((LotteryRegistration)item).IsRegisterable;
            LotteryRegistrationsDateTimeLabel.Content = $"最終更新 {gakujoApi.Account.LotteryRegistrationDateTime:yyyy/MM/dd HH:mm:ss}";
            LotteryRegistrationsDataGrid.ItemsSource = collectionView;
            LotteryRegistrationsDataGrid.Items.Refresh();
            logger.Info("Refresh LotteryRegistrationsDataGrid.");
        }

        #endregion

        #region 抽選履修登録結果

        private void LoadLotteryRegistrationsResultButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadLotteryRegistrationsResultButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadLotteryRegistrationsResultButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetLotteryRegistrationsResult();
                Dispatcher.Invoke(() =>
                {
                    RefreshLotteryRegistrationsResultDataGrid();
                    LoadLotteryRegistrationsResultButtonFontIcon.Visibility = Visibility.Visible;
                    LoadLotteryRegistrationsResultButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void FilterLotteryRegistrationsResultCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            RefreshLotteryRegistrationsResultDataGrid();
        }

        private void RefreshLotteryRegistrationsResultDataGrid()
        {
            var collectionView = new CollectionViewSource { Source = gakujoApi.LotteryRegistrationsResult.SelectMany(_ => _) }.View;
            collectionView.Filter = item => !(bool)FilterLotteryRegistrationsResultCheckBox.IsChecked! || ((LotteryRegistrationResult)item).IsWinning;
            LotteryRegistrationsResultDateTimeLabel.Content = $"最終更新 {gakujoApi.Account.LotteryRegistrationResultDateTime:yyyy/MM/dd HH:mm:ss}";
            LotteryRegistrationsResultDataGrid.ItemsSource = collectionView;
            LotteryRegistrationsResultDataGrid.Items.Refresh();
            logger.Info("Refresh LotteryRegistrationsResultDataGrid.");
        }

        #endregion

        #region 一般履修登録

        private void LoadGeneralRegistrationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadGeneralRegistrationsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadGeneralRegistrationsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetGeneralRegistrations();
                Dispatcher.Invoke(() =>
                {
                    RefreshGeneralRegistrationsDataGrid();
                    LoadGeneralRegistrationsButtonFontIcon.Visibility = Visibility.Visible;
                    LoadGeneralRegistrationsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void RefreshGeneralRegistrationsDataGrid()
        {
            var collectionView = new CollectionViewSource { Source = gakujoApi.RegisterableGeneralRegistrations.SelectMany(_ => _) }.View;
            GeneralRegistrationsDateTimeLabel.Content = $"最終更新 {gakujoApi.Account.GeneralRegistrationDateTime:yyyy/MM/dd HH:mm:ss}";
            GeneralRegistrationsDataGrid.ItemsSource = collectionView;
            GeneralRegistrationsDataGrid.Items.Refresh();
            logger.Info("Refresh GeneralRegistrationsDataGrid.");
        }

        #endregion

        #region 成績情報

        private void RefreshClassResultsDataGrid()
        {
            ClassResultsDateTimeLabel.Content = $"最終更新 {gakujoApi.Account.ClassResultDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassResultsDataGrid.ItemsSource = gakujoApi.SchoolGrade.ClassResults;
            ClassResultsDataGrid.Items.Refresh();
            ClassResultsGPALabel.Content = $"推定GPA {gakujoApi.SchoolGrade.PreliminaryGpa:N3}";
            logger.Info("Refresh ClassResultsDataGrid.");
        }

        private void LoadClassResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadClassResultsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassResultsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetClassResults(out var diffClassResults);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassResultsDataGrid();
                    if (diffClassResults.Count != gakujoApi.SchoolGrade.ClassResults.Count)
                        diffClassResults.ForEach(x =>
                        {
                            NotifyToast(x);
                            notifyApi.NotifyDiscord(x, true);
                        });
                    LoadClassResultsButtonFontIcon.Visibility = Visibility.Visible;
                    LoadClassResultsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void EvaluationCreditsButton_Click(object sender, RoutedEventArgs e)
        {
            EvaluationCreditsDataGrid.ItemsSource = gakujoApi.SchoolGrade.EvaluationCredits;
            EvaluationCreditsDataGrid.Items.Refresh();
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void DepartmentGPAButton_Click(object sender, RoutedEventArgs e)
        {
            DepartmentGPALabel.Content = gakujoApi.SchoolGrade.DepartmentGpa;
            DepartmentGPAImage.Source = Base64ToBitmapImage(gakujoApi.SchoolGrade.DepartmentGpa.DepartmentImage);
            CourseGPAImage.Source = Base64ToBitmapImage(gakujoApi.SchoolGrade.DepartmentGpa.CourseImage);
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void YearCreditsButton_Click(object sender, RoutedEventArgs e)
        {
            YearCreditsDataGrid.ItemsSource = gakujoApi.SchoolGrade.YearCredits;
            YearCreditsDataGrid.Items.Refresh();
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private static BitmapImage Base64ToBitmapImage(string base64)
        {
            BitmapImage bitmapImage = new();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = new MemoryStream(Convert.FromBase64String(base64));
            bitmapImage.EndInit();
            return bitmapImage;
        }

        #endregion

        #region 個人時間割

        private ClassTableCell? GetSelectedClassTableCell()
        {
            if (ClassTablesDataGrid.SelectedCells.Count != 1)
                return null;
            return ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem) == -1 ? null : gakujoApi.ClassTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)][ClassTablesDataGrid.SelectedCells[0].Column.DisplayIndex];
        }

        private void ClassTableCellControl_ClassContactButtonClick(object sender, RoutedEventArgs e)
        {
            var classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null)
                return;
            ClassContactsSearchAutoSuggestBox.Text = classTableCell.SubjectsName;
            RefreshClassContactsDataGrid();
            e.Handled = true;
            ClassContactsTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_ReportButtonClick(object sender, RoutedEventArgs e)
        {
            var classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null)
                return;
            ReportsSearchAutoSuggestBox.Text = classTableCell.SubjectsName;
            RefreshReportsDataGrid();
            e.Handled = true;
            ReportsTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_QuizButtonClick(object sender, RoutedEventArgs e)
        {
            var classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null)
                return;
            QuizzesSearchAutoSuggestBox.Text = classTableCell.SubjectsName;
            RefreshQuizzesDataGrid();
            e.Handled = true;
            QuizzesTabItem.IsSelected = true;
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SyllabusClassTablesTabItem == null)
                return;
            if (!SyllabusClassTablesTabItem.IsSelected)
                SyllabusClassTablesTabItem.Visibility = Visibility.Collapsed;
        }

        private void ClassTableCellControl_OnClassSharedFileMenuItemClick(object sender, RoutedEventArgs e)
        {
            var classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null)
                return;
            ClassSharedFilesSearchAutoSuggestBox.Text = classTableCell.SubjectsName;
            RefreshClassSharedFilesDataGrid();
            e.Handled = true;
            ClassSharedFilesTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_SyllabusMenuItemClick(object sender, RoutedEventArgs e)
        {
            var classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null)
                return;
            SyllabusMarkdownViewer.Markdown = classTableCell.Syllabus.ToString();
            SyllabusClassTablesTabItem.Visibility = Visibility.Visible;
            e.Handled = true;
            SyllabusClassTablesTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_VideoMenuItemClick(object sender, RoutedEventArgs e)
        {
            var classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null)
                return;
            Process.Start(new ProcessStartInfo($"https://gakujo.shizuoka.ac.jp/portal/educationalvideo/search/params/locale=ja&year={settings.SchoolYear}&faculty=&department=&course={classTableCell.SubjectsName}&instructor={classTableCell.TeacherName.Replace(" ", "")}") { UseShellExecute = true });
            logger.Info($"Start Process https://gakujo.shizuoka.ac.jp/portal/educationalvideo/search/params/locale=ja&year={settings.SchoolYear}&faculty=&department=&course={classTableCell.SubjectsName}&instructor={classTableCell.TeacherName.Replace(" ", "")}");
        }

        private void ClassTablesDataGrid_PreviewDrop(object sender, DragEventArgs e)
        {
            if (GetDataGridCell<ClassTableCellControl>(ClassTablesDataGrid, e.GetPosition(ClassTablesDataGrid))!.DataContext is not ClassTableCell classTableCell)
                return;
            logger.Info($"Start Add Favorites to {classTableCell.SubjectsName}.");
            List<string> favorites = new();
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                favorites.AddRange((string[])e.Data.GetData(DataFormats.FileDrop, false)!);
            if (e.Data.GetDataPresent(DataFormats.Text, false))
                if (Regex.IsMatch((string)e.Data.GetData(DataFormats.Text, false)!, @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)"))
                    favorites.Add((string)e.Data.GetData(DataFormats.Text, false)!);
            favorites = favorites.Except(classTableCell.Favorites).ToList();
            if (favorites.Count == 0)
            {
                logger.Warn("Return Add Favorites by already exists.");
                MessageBox.Show($"{classTableCell.SubjectsName}のお気に入りにすでに追加されています．", AssemblyName, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show($"{classTableCell.SubjectsName}のお気に入りに追加しますか．\n{string.Join('\n', favorites)}", AssemblyName, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                classTableCell.Favorites.AddRange(favorites);
                logger.Info($"Add {favorites.Count} Favorites to {classTableCell.SubjectsName}");
                gakujoApi.SaveJsons();
            }
            else
            {
                logger.Warn("Return Add Favorites by user rejection.");
                return;
            }
            logger.Info($"End Add Favorites to {classTableCell.SubjectsName}.");
            RefreshClassTablesDataGrid(true);
        }

        public void RefreshClassTablesDataGrid(bool saveJsons = false)
        {
            if (saveJsons)
                gakujoApi.SaveJsons();
            LoginDateTimeLabel.Content = $"最終ログイン\n{gakujoApi.Account.LoginDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassTablesDataGrid.ItemsSource = gakujoApi.ClassTables.GetRange(0, Math.Min(5, gakujoApi.ClassTables.Count));
            ClassTablesDataGrid.Items.Refresh();
            logger.Info("Refresh ClassTablesDataGrid.");
        }

        private void ClassTablesDataGrid_LoadingRow(object sender, DataGridRowEventArgs e)
        {
            e.Row.Header = (e.Row.GetIndex() + 1).ToString();
        }

#pragma warning disable IDE0051 // 使用されていないプライベート メンバーを削除する
        private DataGridCell? GetDataGridCell(DataGrid dataGrid, int rowIndex, int columnIndex)
#pragma warning restore IDE0051 // 使用されていないプライベート メンバーを削除する
        {
            if (dataGrid.Items.IsEmpty)
                return null;
            var dataGridRow = GetDataGridRow(dataGrid, rowIndex)!;
            var dataGridCellPresenter = GetVisualChild<DataGridCellsPresenter>(dataGridRow)!;
            var dataGridCell = (dataGridCellPresenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell)!;
            return dataGridCell;
        }

        private static DataGridRow? GetDataGridRow(DataGrid dataGrid, int index)
        {
            if (dataGrid.Items.IsEmpty)
                return null;
            var dataGridRow = (dataGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow)!;
            return dataGridRow;
        }

        private static T? GetVisualChild<T>(Visual parent) where T : Visual
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
            T? result = default;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var visual = (VisualTreeHelper.GetChild(parent, i) as Visual)!;
                if (visual is T)
                    break;
                result = GetVisualChild<T>((visual as T)!);
            }
            return result;
        }

        private static T? GetDataGridCell<T>(Visual dataGrid, Point point)
        {
            T? result = default;
            var hitTestResult = VisualTreeHelper.HitTest(dataGrid, point);
            if (hitTestResult == null)
                return result;
            var visualHit = hitTestResult.VisualHit;
            while (visualHit != null)
            {
                if (visualHit is T)
                {
                    result = (T)(object)visualHit;
                    break;
                }
                visualHit = VisualTreeHelper.GetParent(visualHit);
            }
            return result;
        }

        private void ClassTablesDataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text, false) || e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effects = DragDropEffects.All;
            else
                e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void SyllabusSearchWebView2_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            SyllabusSearchWebView2.ExecuteScriptAsync("dbLinkClick = function(url){ window.chrome.webview.postMessage(url); }");
        }

        private void SyllabusSearchWebView2_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            SyllabusViewWebView2.Source = new Uri("https://syllabus.shizuoka.ac.jp/" + e.TryGetWebMessageAsString());
            logger.Info($"Navigate SyllabusView https://syllabus.shizuoka.ac.jp/{e.TryGetWebMessageAsString()}");
        }

        #endregion

        #region サジェスト

#pragma warning disable CA1822 // メンバーを static に設定します
        private void SearchAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args) => sender.Text = args.SelectedItem.ToString();
#pragma warning restore CA1822 // メンバーを static に設定します

        private void SearchAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput) return;
            List<string> suitableItems = new();
            var splitText = sender.Text.Split(" ");
            foreach (var classTableRow in gakujoApi.ClassTables)
            {
                foreach (var classTableCell in classTableRow)
                {
                    if (splitText.All((key) => classTableCell.SubjectsName.Contains(key)) && classTableCell.SubjectsName != "")
                        suitableItems.Add(classTableCell.SubjectsName);
                }
            }
            sender.ItemsSource = suitableItems.Distinct();
        }

        #endregion

        #region 通知

        private void SetVisibility(Visibility visibility)
        {
            switch (visibility)
            {
                case Visibility.Visible:
                    Visibility = Visibility.Visible;
                    Activate();
                    ShowInTaskbar = true;
                    if (!settings.AlwaysVisibleTrayIcon) { TaskBarIcon.Visibility = Visibility.Collapsed; }
                    logger.Info("Set visibility to Visible.");
                    SyllabusSearchWebView2.Source = new("https://syllabus.shizuoka.ac.jp/ext_syllabus/syllabusSearchDirect.do?nologin=on");
                    logger.Info("Navigate SyllabusSearch https://syllabus.shizuoka.ac.jp/ext_syllabus/syllabusSearchDirect.do?nologin=on");
                    break;
                case Visibility.Hidden:
                    Visibility = Visibility.Hidden;
                    Hide();
                    ShowInTaskbar = false;
                    TaskBarIcon.Visibility = Visibility.Visible;
                    logger.Info("Set visibility to Hidden.");
                    break;
                case Visibility.Collapsed:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(visibility), visibility, null);
            }
        }

        private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            var toastArguments = ToastArguments.Parse(e.Argument);
            Dispatcher.Invoke(() =>
            {
                SetVisibility(Visibility.Visible);
                logger.Info("Activate MainForm by Toast.");
                if (!toastArguments.Contains("Type") || !toastArguments.Contains("Index")) { return; }
                logger.Info($"Click Toast Type={toastArguments.Get("Type")}, Index={toastArguments.GetInt("Index")}");
                switch (toastArguments.Get("Type"))
                {
                    case "ClassContact":
                        ClassContactsSearchAutoSuggestBox.Text = "";
                        RefreshClassContactsDataGrid();
                        ClassContactsDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        ClassContactsTabItem.IsSelected = true;
                        break;
                    case "Report":
                        ReportsSearchAutoSuggestBox.Text = "";
                        FilterReportsCheckBox.IsChecked = false;
                        RefreshReportsDataGrid();
                        ReportsDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        ReportsTabItem.IsSelected = true;
                        break;
                    case "Quiz":
                        QuizzesSearchAutoSuggestBox.Text = "";
                        FilterQuizzesCheckBox.IsChecked = false;
                        RefreshQuizzesDataGrid();
                        QuizzesDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        QuizzesTabItem.IsSelected = true;
                        break;
                    case "ClassSharedFile":
                        ClassSharedFilesSearchAutoSuggestBox.Text = "";
                        RefreshClassSharedFilesDataGrid();
                        ClassSharedFilesDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        ClassSharedFilesTabItem.IsSelected = true;
                        break;
                    case "ClassResult":
                        ClassResultsDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        ClassResultsTabItem.IsSelected = true;
                        break;
                }
            });
        }

        private void NotifyToast(ClassContact classContact)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassContact").AddArgument("Index", gakujoApi.ClassContacts.IndexOf(classContact)).AddText(classContact.Title).AddText(classContact.Content).AddCustomTimeStamp(classContact.ContactDateTime).AddAttributionText(classContact.Subjects).Show();
            logger.Info("Notiy Toast ClassContact.");
        }

        private void NotifyToast(Report report)
        {
            new ToastContentBuilder().AddArgument("Type", "Report").AddArgument("Index", gakujoApi.Reports.IndexOf(report)).AddText(report.Title).AddText($"{report.StartDateTime} -> {report.EndDateTime}").AddCustomTimeStamp(report.StartDateTime).AddAttributionText(report.Subjects).Show();
            logger.Info("Notiy Toast Report.");
        }

        private void NotifyToast(Quiz quiz)
        {
            new ToastContentBuilder().AddArgument("Type", "Quiz").AddArgument("Index", gakujoApi.Quizzes.IndexOf(quiz)).AddText(quiz.Title).AddText($"{quiz.StartDateTime} -> {quiz.EndDateTime}").AddCustomTimeStamp(quiz.StartDateTime).AddAttributionText(quiz.Subjects).Show();
            logger.Info("Notify Toast Quiz.");
        }

        private void NotifyToast(ClassSharedFile classSharedFile)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassSharedFile").AddArgument("Index", gakujoApi.ClassSharedFiles.IndexOf(classSharedFile)).AddText(classSharedFile.Title).AddText(classSharedFile.Description).AddCustomTimeStamp(classSharedFile.UpdateDateTime).AddAttributionText(classSharedFile.Subjects).Show();
            logger.Info("Notify Toast ClassSharedFile.");
        }

        private void NotifyToast(ClassResult classResult)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassResult").AddArgument("Index", gakujoApi.SchoolGrade.ClassResults.IndexOf(classResult)).AddText(classResult.Subjects).AddText($"{classResult.Score} ({classResult.Evaluation})   {classResult.Gp:F1}").AddCustomTimeStamp(classResult.ReportDate).AddAttributionText(classResult.ReportDate.ToString(CultureInfo.CurrentCulture)).Show();
            logger.Info("Notify Toast ClassResult.");
        }

        #endregion

        #region タスクバー

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetVisibility(Visibility.Visible);
            logger.Info("Activate MainForm by OpenMenuItem.");
        }

        private void LoadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(Load);
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            shutdownFlag = true;
            Application.Current.Shutdown();
            logger.Info("Shutdown by CloseMenuItem.");
        }

        private void TaskBarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            SetVisibility(Visibility.Visible);
            logger.Info("Activate MainForm by TaskBarIcon.");
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            ReportMenuItem.Header = $"レポート ({gakujoApi.Reports.Count(x => x.IsSubmittable)})";
            ReportMenuItem.Visibility = gakujoApi.Reports.Any(x => x.IsSubmittable) ? Visibility.Visible : Visibility.Collapsed;
            QuizMenuItem.Header = $"小テスト ({gakujoApi.Quizzes.Count(x => x.IsSubmittable)})";
            QuizMenuItem.Visibility = gakujoApi.Quizzes.Any(x => x.IsSubmittable) ? Visibility.Visible : Visibility.Collapsed;
            logger.Info("Opened ContextMenu.");
        }

        private void ReportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetVisibility(Visibility.Visible);
            ReportsTabItem.IsSelected = true;
            ReportsSearchAutoSuggestBox.Text = "";
            FilterReportsCheckBox.IsChecked = true;
            RefreshReportsDataGrid();
            logger.Info("Activate MainForm by ReportMenuItem.");
        }

        private void QuizMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetVisibility(Visibility.Visible);
            QuizzesTabItem.IsSelected = true;
            QuizzesSearchAutoSuggestBox.Text = "";
            FilterReportsCheckBox.IsChecked = true;
            RefreshQuizzesDataGrid();
            logger.Info("Activate MainForm by QuizMenuItem.");
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!shutdownFlag && Keyboard.Modifiers != ModifierKeys.Shift)
            {
                e.Cancel = true;
                SetVisibility(Visibility.Hidden);
                logger.Info("Minimized MainForm by window closing.");
            }
            else
                logger.Info("Continue window closing.");
        }

        #endregion

        #region 設定

        private void SaveJson()
        {
            try
            {
                File.WriteAllText(GetJsonPath("Settings"), JsonConvert.SerializeObject(settings, Formatting.Indented));
                logger.Info("Save Settings.");
            }
            catch (Exception exception)
            {
                logger.Error(exception, "Error Save Settings.");
            }
        }

        private void ResetUserAgentHyperlink_Click(object sender, RoutedEventArgs e)
        {
            UserAgentTextBox.Text = new Settings().UserAgent;
        }

        private void SaveTokensButton_Click(object sender, RoutedEventArgs e)
        {
            switch (MessageBox.Show($"適用するには{AssemblyName}を再起動する必要があります．\n再起動しますか．", AssemblyName, MessageBoxButton.YesNoCancel, MessageBoxImage.Information))
            {
                case MessageBoxResult.Yes:
                    notifyApi.SetTokens(TodoistTokenPasswordBox.Password, DiscordChannelTextBox.Text, DiscordTokenPasswordBox.Password);
                    settings.SchoolYear = (int)SchoolYearNumberBox.Value;
                    settings.SemesterCode = SchoolSemesterComboBox.SelectedIndex;
                    settings.UserAgent = UserAgentTextBox.Text;
                    SaveJson();
                    Process.Start(Environment.ProcessPath!);
                    logger.Info($"Start Process {Environment.ProcessPath!}");
                    logger.Info("Shutdown by apply Settings.");
                    shutdownFlag = true;
                    Application.Current.Shutdown();
                    break;
                case MessageBoxResult.No:
                    notifyApi.SetTokens(TodoistTokenPasswordBox.Password, DiscordChannelTextBox.Text, DiscordTokenPasswordBox.Password);
                    settings.SchoolYear = (int)SchoolYearNumberBox.Value;
                    settings.SemesterCode = SchoolSemesterComboBox.SelectedIndex;
                    settings.UserAgent = UserAgentTextBox.Text;
                    SaveJson();
                    break;
                case MessageBoxResult.Cancel:
                    TodoistTokenPasswordBox.Password = GakujoApi.Unprotect(notifyApi.Tokens.TodoistToken, null!, DataProtectionScope.CurrentUser);
                    DiscordChannelTextBox.Text = notifyApi.Tokens.DiscordChannel.ToString();
                    DiscordTokenPasswordBox.Password = GakujoApi.Unprotect(notifyApi.Tokens.DiscordToken, null!, DataProtectionScope.CurrentUser);
                    SchoolYearNumberBox.Value = settings.SchoolYear;
                    SchoolSemesterComboBox.SelectedIndex = settings.SemesterCode;
                    UserAgentTextBox.Text = settings.UserAgent;
                    break;
            }
        }

        private void AutoLoadEnableCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            if (!settingsFlag)
                return;
            settings.AutoLoadEnable = (bool)AutoLoadEnableCheckBox.IsChecked!;
            SaveJson();
            if (settings.AutoLoadEnable)
            {
                autoLoadTimer.Start();
                logger.Info("Start AutoLoadTimer.");
            }
            else
            {
                autoLoadTimer.Stop();
                logger.Info("Stop AutoLoadTimer.");
            }
        }

        private void AutoLoadSpanNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            if (!settingsFlag)
                return;
            settings.AutoLoadSpan = (int)AutoLoadSpanNumberBox.Value;
            logger.Info($"Set AutoLoadSpan {(int)AutoLoadSpanNumberBox.Value}.");
            SaveJson();
            autoLoadTimer.Interval = TimeSpan.FromMinutes(AutoLoadSpanNumberBox.Value);
        }

        private void StartUpEnableCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            if (!settingsFlag)
                return;
            settings.StartUpEnable = (bool)StartUpEnableCheckBox.IsChecked!;
            logger.Info($"Set StartUpEnable {(settings.StartUpEnable ? "enable" : "disable")}.");
            SaveJson();
            using var registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)!;
            if (settings.StartUpEnable) { registryKey.SetValue(AssemblyName, Environment.ProcessPath!); }
            else { registryKey.DeleteValue(AssemblyName, false); }
            registryKey.Close();
        }

        private void StartUpMinimizeCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            if (!settingsFlag)
                return;
            settings.StartUpMinimize = (bool)StartUpMinimizeCheckBox.IsChecked!;
            logger.Info($"Set StartUpMinimize {(settings.StartUpMinimize ? "enable" : "disable")}.");
            SaveJson();
        }

        private void AlwaysVisibleTrayIconCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            if (!settingsFlag)
                return;
            settings.AlwaysVisibleTrayIcon = (bool)AlwaysVisibleTrayIconCheckBox.IsChecked!;
            logger.Info($"Set AlwaysVisibleTrayIcon {(settings.AlwaysVisibleTrayIcon ? "enable" : "disable")}.");
            SaveJson();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            logger.Info($"Start Process {e.Uri.AbsoluteUri}");
            e.Handled = true;
        }

        private void LoadAllClassContactsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadAllClassContactsButtonLabel.Visibility = Visibility.Collapsed;
            LoadAllClassContactsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetClassContacts(out _, -1);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassContactsDataGrid();
                    LoadAllClassContactsButtonLabel.Visibility = Visibility.Visible;
                    LoadAllClassContactsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void LoadAllClassSharedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!LoginStatusCheck())
                return;
            LoadAllClassSharedFilesButtonLabel.Visibility = Visibility.Collapsed;
            LoadAllClassSharedFilesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoApi.GetClassSharedFiles(out _, -1);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassSharedFilesDataGrid();
                    LoadAllClassSharedFilesButtonLabel.Visibility = Visibility.Visible;
                    LoadAllClassSharedFilesButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void TabVisibilityCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            if (!settingsFlag)
                return;
            settings.ClassContactsTabVisibility = (bool)ClassContactsTabVisibilityCheckBox.IsChecked!;
            settings.ReportsTabVisibility = (bool)ReportsTabVisibilityCheckBox.IsChecked!;
            settings.QuizzesTabVisibility = (bool)QuizzesTabVisibilityCheckBox.IsChecked!;
            settings.ClassSharedFilesTabVisibility = (bool)ClassSharedFilesTabVisibilityCheckBox.IsChecked!;
            settings.LotteryRegistrationsTabVisibility = (bool)LotteryRegistrationsTabVisibilityCheckBox.IsChecked!;
            settings.GeneralRegistrationsTabVisibility = (bool)GeneralRegistrationsTabVisibilityCheckBox.IsChecked!;
            settings.ClassResultsTabVisibility = (bool)ClassResultsTabVisibilityCheckBox.IsChecked!;
            settings.SyllabusSearchTabVisibility = (bool)SyllabusSearchTabVisibilityCheckBox.IsChecked!;
            logger.Info($"Set ClassContactsTabVisibility {(settings.ClassContactsTabVisibility ? "visible" : "hidden")}.");
            logger.Info($"Set ReportsTabVisibility {(settings.ReportsTabVisibility ? "visible" : "hidden")}.");
            logger.Info($"Set QuizzesTabVisibility {(settings.QuizzesTabVisibility ? "visible" : "hidden")}.");
            logger.Info($"Set ClassSharedFilesTabVisibility {(settings.ClassSharedFilesTabVisibility ? "visible" : "hidden")}.");
            logger.Info($"Set LotteryRegistrationsTabVisibility {(settings.LotteryRegistrationsTabVisibility ? "visible" : "hidden")}.");
            logger.Info($"Set GeneralRegistrationsTabVisibility {(settings.GeneralRegistrationsTabVisibility ? "visible" : "hidden")}.");
            logger.Info($"Set ClassResultsTabVisibility {(settings.ClassResultsTabVisibility ? "visible" : "hidden")}.");
            logger.Info($"Set SyllabusSearchTabVisibility {(settings.SyllabusSearchTabVisibility ? "visible" : "hidden")}.");
            SaveJson();
            ClassContactsTabItem.Visibility = settings.ClassContactsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            ReportsTabItem.Visibility = settings.ReportsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            QuizzesTabItem.Visibility = settings.QuizzesTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            ClassSharedFilesTabItem.Visibility = settings.ClassSharedFilesTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            LotteryRegistrationsTabItem.Visibility = settings.LotteryRegistrationsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            GeneralRegistrationsTabItem.Visibility = settings.GeneralRegistrationsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            ClassResultsTabItem.Visibility = settings.ClassResultsTabVisibility ? Visibility.Visible : Visibility.Collapsed;
            SyllabusSearchTabItem.Visibility = settings.SyllabusSearchTabVisibility ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateBetaEnableCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            if (!settingsFlag)
                return;
            settings.UpdateBetaEnable = (bool)UpdateBetaEnableCheckBox.IsChecked!;
            logger.Info($"Set UpdateBetaEnable {(settings.UpdateBetaEnable ? "enable" : "disable")}.");
            SaveJson();
        }

        private void OpenSettingsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"\"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyName)}\"",
                UseShellExecute = true
            });
            logger.Info($"Start Process explorer.exe \"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AssemblyName)}\"");
        }

        private void OpenAssemblyFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"\"{Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!}\"",
                UseShellExecute = true
            });
            logger.Info($"Start Process explorer.exe \"{Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!}\"");
        }

        private void GetLatestVersionButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                if (!GetLatestVersions(out var latestVersion))
                    Dispatcher.Invoke(() => MessageBox.Show("最新の状態です．", AssemblyName, MessageBoxButton.OK, MessageBoxImage.Information));
                else
                {
                    MessageBoxResult? messageBoxResult = MessageBoxResult.No;
                    Dispatcher.Invoke(() => { messageBoxResult = MessageBox.Show($"更新があります．\nv{Assembly.GetExecutingAssembly().GetName().Version} -> v{latestVersion}", AssemblyName, MessageBoxButton.YesNo, MessageBoxImage.Information); });
                    if (messageBoxResult == MessageBoxResult.Yes && File.Exists(SetupFilePath))
                    {
                        Process.Start("https://github.com/xyzyxJP/GakujoGUI-WPF/releases");
                    }
                }
            });
        }

        private static void GetLatestVersion(Release[] releases, ref Version version)
        {
            if (!releases.Any())
                return;
            if (version >= Version.Parse(releases.First().tag_name.TrimStart('v'))) return;
            version = Version.Parse(releases.First().tag_name.TrimStart('v'));
        }

        private bool GetLatestVersions(out Version latestVersion)
        {
            logger.Info("Start Get latest versions.");
            HttpClient httpClient = new();
            HttpRequestMessage httpRequestMessage = new(new("GET"), "https://api.github.com/repos/xyzyxJP/GakujoGUI-WPF/releases");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", settings.UserAgent);
            var httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://api.github.com/repos/xyzyxJP/GakujoGUI-WPF/releases");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            var releases = JsonConvert.DeserializeObject<Release[]>(httpResponseMessage.Content.ReadAsStringAsync().Result)!;
            latestVersion = Assembly.GetExecutingAssembly().GetName().Version!;
            GetLatestVersion(releases.Where(x => !x.prerelease || (settings.UpdateBetaEnable && x.prerelease)).ToArray(), ref latestVersion);
            logger.Info($"latestVersion={latestVersion}");
            if (Assembly.GetExecutingAssembly().GetName().Version >= latestVersion)
            {
                logger.Info("Return Get latest versions by using same or newer version.");
                return false;
            }
            logger.Info("End Get latest versions.");
            return true;
        }

        private void BackgroundImageOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            settings.BackgroundImageOpacity = (int)BackgroundImageOpacitySlider.Value;
            RefreshBackgroundImage();
        }

        private void ClearBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        {
            settings.BackgroundImagePath = string.Empty;
            RefreshBackgroundImage();
        }

        private void OpenBackgroundImageButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new() { Filter = "画像 (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif" };
            if (openFileDialog.ShowDialog() != true)
                return;
            settings.BackgroundImagePath = openFileDialog.FileName;
            RefreshBackgroundImage();
        }

        private void RefreshBackgroundImage()
        {
            if (!settingsFlag)
                return;
            BackgroundImagePathLabel.Content = Path.GetFileName(settings.BackgroundImagePath);
            BackgroundImageOpacitySlider.Value = settings.BackgroundImageOpacity;
            logger.Info($"Set BackgroundImagePath {settings.BackgroundImagePath}.");
            logger.Info($"Set BackgroundImageOpacity {settings.BackgroundImageOpacity}.");
            SaveJson();
            if (File.Exists(settings.BackgroundImagePath) && !string.IsNullOrEmpty(settings.BackgroundImagePath))
            {
                ImageBrush imageBrush = new()
                {
                    ImageSource = new BitmapImage(new Uri(settings.BackgroundImagePath)),
                    Stretch = Stretch.UniformToFill,
                    Opacity = 1.0 * settings.BackgroundImageOpacity / 100,
                };
                Grid.Background = imageBrush;
                logger.Info("Set Background ImageBrush.");
            }
            else
            {
                Grid.ClearValue(BackgroundProperty);
                logger.Info("Set Background Default.");
            }
        }

        #endregion
    }

    public class Settings
    {
        public bool AutoLoadEnable { get; set; } = true;
        public int AutoLoadSpan { get; set; } = 30;
        public bool StartUpEnable { get; set; }
        public bool StartUpMinimize { get; set; }
        public bool AlwaysVisibleTrayIcon { get; set; } = true;
        public int SchoolYear { get; set; } = 2022;
        public int SemesterCode { get; set; } = 1;
        public string UserAgent { get; set; } = $"Chrome/105.0.5195.127 {Assembly.GetExecutingAssembly().GetName().Name}/{Assembly.GetExecutingAssembly().GetName().Version}";
        public bool UpdateBetaEnable { get; set; }
        public string BackgroundImagePath { get; set; } = "";
        public int BackgroundImageOpacity { get; set; } = 30;
        public bool ClassContactsTabVisibility { get; set; } = true;
        public bool ReportsTabVisibility { get; set; } = true;
        public bool QuizzesTabVisibility { get; set; } = true;
        public bool ClassSharedFilesTabVisibility { get; set; } = true;
        public bool LotteryRegistrationsTabVisibility { get; set; } = true;
        public bool GeneralRegistrationsTabVisibility { get; set; } = true;
        public bool ClassResultsTabVisibility { get; set; } = true;
        public bool SyllabusSearchTabVisibility { get; set; } = true;
    }

#pragma warning disable IDE1006 // 命名スタイル
#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。

    public class Release
    {
        public string url { get; set; }
        public string assets_url { get; set; }
        public string upload_url { get; set; }
        public string html_url { get; set; }
        public int id { get; set; }
        public Author author { get; set; }
        public string node_id { get; set; }
        public string tag_name { get; set; }
        public string target_commitish { get; set; }
        public string name { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public DateTime created_at { get; set; }
        public DateTime published_at { get; set; }
        public Asset[] assets { get; set; }
        public string tarball_url { get; set; }
        public string zipball_url { get; set; }
        public string body { get; set; }
    }

    public class Author
    {
        public string login { get; set; }
        public int id { get; set; }
        public string node_id { get; set; }
        public string avatar_url { get; set; }
        public string gravatar_id { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string followers_url { get; set; }
        public string following_url { get; set; }
        public string gists_url { get; set; }
        public string starred_url { get; set; }
        public string subscriptions_url { get; set; }
        public string organizations_url { get; set; }
        public string repos_url { get; set; }
        public string events_url { get; set; }
        public string received_events_url { get; set; }
        public string type { get; set; }
        public bool site_admin { get; set; }
    }

    public class Asset
    {
        public string url { get; set; }
        public int id { get; set; }
        public string node_id { get; set; }
        public string name { get; set; }
        public object label { get; set; }
        public Uploader uploader { get; set; }
        public string content_type { get; set; }
        public string state { get; set; }
        public int size { get; set; }
        public int download_count { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string browser_download_url { get; set; }
    }

    public class Uploader
    {
        public string login { get; set; }
        public int id { get; set; }
        public string node_id { get; set; }
        public string avatar_url { get; set; }
        public string gravatar_id { get; set; }
        public string url { get; set; }
        public string html_url { get; set; }
        public string followers_url { get; set; }
        public string following_url { get; set; }
        public string gists_url { get; set; }
        public string starred_url { get; set; }
        public string subscriptions_url { get; set; }
        public string organizations_url { get; set; }
        public string repos_url { get; set; }
        public string events_url { get; set; }
        public string received_events_url { get; set; }
        public string type { get; set; }
        public bool site_admin { get; set; }
    }

#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
#pragma warning restore IDE1006 // 命名スタイル
}
