using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using ModernWpf.Controls;
using ModernWpf.Controls.Primitives;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using MessageBox = ModernWpf.MessageBox;
using Path = System.IO.Path;
using Microsoft.Web.WebView2.Core;

namespace GakujoGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Logger logger = LogManager.GetCurrentClassLogger();

        private readonly GakujoAPI gakujoAPI;
        private readonly NotifyAPI notifyAPI;
        private readonly Settings settings = new();
        private readonly DispatcherTimer autoLoadTimer = new();
        private bool shutdownFlag = false;

        private static string GetJsonPath(string value)
        {
            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI")))
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI"));
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI\{value}.json");
        }

#pragma warning disable CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
        public MainWindow()
#pragma warning restore CS8618 // null 非許容のフィールドには、コンストラクターの終了時に null 以外の値が入っていなければなりません。Null 許容として宣言することをご検討ください。
        {
            InitializeComponent();
            Process[] processes = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Where(x => x.Id != Environment.ProcessId).ToArray();

            if (processes.Length != 0 && !Environment.GetCommandLineArgs().Contains("-force"))
            {
                foreach (Process process in processes)
                {
                    MessageBox.Show("GakujoGUIはすでに起動しています．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                    logger.Warn("Shutdown by double activation.");
                    shutdownFlag = true;
                    Application.Current.Shutdown();
                    return;
                }
            }
            else
            {
                foreach (Process process in processes)
                {
                    process.Kill();
                    logger.Warn($"Kill other GakujoGUI process processId={process.Id}.");
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
                    //new ToastContentBuilder().AddText("GakujoGUI").AddText("最小化した状態で起動しました．").Show();
                    logger.Info("Startup minimized.");
                }
                notifyAPI = new();
                gakujoAPI = new(settings.SchoolYear.ToString(), settings.SemesterCode, settings.UserAgent);
                UserIdTextBox.Text = gakujoAPI.Account.UserId;
                PassWordPasswordBox.Password = gakujoAPI.Account.PassWord;
                TodoistTokenPasswordBox.Password = notifyAPI.Tokens.TodoistToken;
                DiscordChannelTextBox.Text = notifyAPI.Tokens.DiscordChannel.ToString();
                DiscordTokenPasswordBox.Password = notifyAPI.Tokens.DiscordToken;
                RefreshClassTablesDataGrid();
                RefreshClassContactsDataGrid();
                RefreshReportsDataGrid();
                RefreshQuizzesDataGrid();
                RefreshClassSharedFilesDataGrid();
                RefreshLotteryRegistrationsDataGrid();
                RefreshLotteryRegistrationsResultDataGrid();
                RefreshGeneralRegistrationsDataGrid();
                RefreshClassResultsDataGrid();
                AutoLoadEnableCheckBox.IsChecked = settings.AutoLoadEnable;
                AutoLoadSpanNumberBox.Value = settings.AutoLoadSpan;
                StartUpEnableCheckBox.IsChecked = settings.StartUpEnable;
                StartUpMinimizeCheckBox.IsChecked = settings.StartUpMinimize;
                SchoolYearNumberBox.Value = settings.SchoolYear;
                SchoolSemesterComboBox.SelectedIndex = settings.SemesterCode;
                UserAgentTextBox.Text = settings.UserAgent;
                UpdateBetaEnableCheckBox.IsChecked = settings.UpdateBetaEnable;
                VersionLabel.Content = $"{Assembly.GetExecutingAssembly().GetName().Version}";
                Task.Run(() =>
                {
                    gakujoAPI.LoadJson();
                    Login();
                    Load();
                    Dispatcher.Invoke(() =>
                    {
                        autoLoadTimer.Interval = TimeSpan.FromMinutes(AutoLoadSpanNumberBox.Value);
                        autoLoadTimer.Tick += new EventHandler(LoadEvent);
                        if (settings.AutoLoadEnable)
                        {
                            autoLoadTimer.Start();
                            logger.Info("Start AutoLoadTimer.");
                        }
                    });
                });
            }
        }

        #region ログイン

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => { Login(); Load(); });
        }

        private void Login()
        {
            Dispatcher.Invoke(() => { gakujoAPI.SetAccount(UserIdTextBox.Text, PassWordPasswordBox.Password); });
            if (string.IsNullOrEmpty(gakujoAPI.Account.UserId) || string.IsNullOrEmpty(gakujoAPI.Account.PassWord)) { return; }
            Dispatcher.Invoke(() =>
            {
                LoginButtonFontIcon.Visibility = Visibility.Collapsed;
                LoginButtonProgressRing.Visibility = Visibility.Visible;
            });
            if (!gakujoAPI.Login()) { Dispatcher.Invoke(() => { MessageBox.Show("自動ログインに失敗しました．静大IDまたはパスワードが正しくありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); }); }
            else
            {
                gakujoAPI.GetClassTables();
                Dispatcher.Invoke(() => { RefreshClassTablesDataGrid(); });
            }
            notifyAPI.Login();
            Dispatcher.Invoke(() =>
            {
                LoginButtonFontIcon.Visibility = Visibility.Visible;
                LoginButtonProgressRing.Visibility = Visibility.Collapsed;
            });
        }

        private void LoadEvent(object? sender, EventArgs? e)
        {
            Task.Run(() => Load());
        }

        private void Load()
        {
            if (!gakujoAPI.LoginStatus) { return; }
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
            gakujoAPI.GetClassContacts(out int classContactsDiffCount);
            gakujoAPI.GetReports(out List<Report> diffReports);
            gakujoAPI.GetQuizzes(out List<Quiz> diffQuizzes);
            gakujoAPI.GetClassSharedFiles(out int classSharedFilesDiffCount);
            gakujoAPI.GetLotteryRegistrations(out _);
            gakujoAPI.GetLotteryRegistrationsResult();
            gakujoAPI.GetGeneralRegistrations();
            //抽選履修登録
            //gakujoAPI.SetLotteryRegistrations(new List<LotteryRegistrationEntry>() { new LotteryRegistrationEntry() { SubjectsName = "心理と行動Ａ", ClassName = "情工１", AspirationOrder = 1 } });
            //一般履修登録
            //gakujoAPI.SetGeneralRegistrations(new List<GeneralRegistrationEntry>() { new GeneralRegistrationEntry() { WeekdayPeriod = "水3･4", SubjectsName = "科学と技術", ClassName = "" } }, true);
            gakujoAPI.GetClassResults(out List<ClassResult> diffClassResults);
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
                if (classContactsDiffCount != gakujoAPI.ClassContacts.Count)
                {
                    for (int i = 0; i < classContactsDiffCount; i++)
                    {
                        NotifyToast(gakujoAPI.ClassContacts[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.ClassContacts[i]);
                    }
                }
                if (diffReports.Count != gakujoAPI.Reports.Count)
                {
                    foreach (Report report in diffReports)
                    {
                        NotifyToast(report);
                        notifyAPI.NotifyDiscord(report);
                    }
                }
                if (diffQuizzes.Count != gakujoAPI.Quizzes.Count)
                {
                    foreach (Quiz quiz in diffQuizzes)
                    {
                        NotifyToast(quiz);
                        notifyAPI.NotifyDiscord(quiz);
                    }
                }
                if (classSharedFilesDiffCount != gakujoAPI.ClassSharedFiles.Count)
                {
                    for (int i = 0; i < classSharedFilesDiffCount; i++)
                    {
                        NotifyToast(gakujoAPI.ClassSharedFiles[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.ClassSharedFiles[i]);
                    }
                }
                if (diffClassResults.Count != gakujoAPI.SchoolGrade.ClassResults.Count)
                {
                    foreach (ClassResult classResult in diffClassResults)
                    {
                        NotifyToast(classResult);
                        notifyAPI.NotifyDiscord(classResult, true);
                    }
                }
                notifyAPI.SetTodoistTask(gakujoAPI.Reports);
                notifyAPI.SetTodoistTask(gakujoAPI.Quizzes);
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

        #endregion

        #region 授業連絡

        private void ClassContactsSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            RefreshClassContactsDataGrid();
        }

        private void LoadClassContactsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadClassContactsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassContactsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassContacts(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassContactsDataGrid();
                    for (int i = 0; i < diffCount; i++)
                    {
                        NotifyToast(gakujoAPI.ClassContacts[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.ClassContacts[i]);
                    }
                    LoadClassContactsButtonFontIcon.Visibility = Visibility.Visible;
                    LoadClassContactsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void RefreshClassContactsDataGrid()
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.ClassContacts }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassContact)item).Subjects.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Title.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Content.Contains(ClassContactsSearchAutoSuggestBox.Text));
            ClassContactsDateTimeLabel.Content = $"最終更新 {gakujoAPI.Account.ClassContactDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassContactsDataGrid.ItemsSource = collectionView;
            ClassContactsDataGrid.Items.Refresh();
            logger.Info("Refresh ClassContactsDataGrid.");
        }

        private void ClassContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassContactsDataGrid.SelectedIndex != -1)
            {
                if (!((ClassContact)ClassContactsDataGrid.SelectedItem).IsAcquired)
                {
                    if (MessageBox.Show("授業連絡の詳細を取得しますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                        int index = ClassContactsDataGrid.SelectedIndex;
                        Task.Run(() => gakujoAPI.GetClassContact(index));
                    }
                }
                ClassContactContactDateTimeLabel.Content = ((ClassContact)ClassContactsDataGrid.SelectedItem).ContactDateTime.ToString("yyyy/MM/dd HH:mm");
                ClassContactContentTextBox.Text = ((ClassContact)ClassContactsDataGrid.SelectedItem).Content;
                if (((ClassContact)ClassContactsDataGrid.SelectedItem).Files.Length == 0)
                {
                    ClassContactFilesComboBox.ItemsSource = null;
                    ClassContactFilesStackPanel.Visibility = Visibility.Hidden;
                }
                else
                {
                    ClassContactFilesComboBox.ItemsSource = ((ClassContact)ClassContactsDataGrid.SelectedItem).Files!.Select(x => Path.GetFileName(x));
                    ClassContactFilesComboBox.SelectedIndex = 0;
                    ClassContactFilesStackPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void OpenClassContactFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassContactFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo(((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]) { UseShellExecute = true });
                    logger.Info($"Start Process {((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]}");
                }
            }
        }

        private void OpenClassContactFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassContactFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/e,/select,\"{((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]}\"", UseShellExecute = true });
                    logger.Info($"Start Process explorer.exe /e,/select,\"{((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]}\"");
                }
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
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadReportsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadReportsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetReports(out List<Report> diffReports);
                notifyAPI.SetTodoistTask(gakujoAPI.Reports);
                Dispatcher.Invoke(() =>
                {
                    RefreshReportsDataGrid();
                    foreach (Report report in diffReports)
                    {
                        NotifyToast(report);
                        notifyAPI.NotifyDiscord(report);
                    }
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
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.Reports }.View;
            collectionView.Filter = new Predicate<object>(item => (((Report)item).Subjects.Contains(ReportsSearchAutoSuggestBox.Text) || ((Report)item).Title.Contains(ReportsSearchAutoSuggestBox.Text)) && (!(bool)FilterReportsCheckBox.IsChecked! || ((Report)item).Unsubmitted));
            ReportsDateTimeLabel.Content = $"最終更新 {gakujoAPI.Account.ReportDateTime:yyyy/MM/dd HH:mm:ss}";
            ReportsDataGrid.ItemsSource = collectionView;
            ReportsDataGrid.Items.Refresh();
            logger.Info("Refresh ReportsDataGrid.");
        }

        private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReportsDataGrid.SelectedIndex != -1)
            {
                if (!((Report)ReportsDataGrid.SelectedItem).IsAcquired)
                {
                    if (MessageBox.Show("レポートの詳細を取得しますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                        Task.Run(() => gakujoAPI.GetReport((Report)ReportsDataGrid.SelectedItem));
                    }
                }
                ReportStartEndDateTimeLabel.Content = $"{((Report)ReportsDataGrid.SelectedItem).StartDateTime:yyyy/MM/dd HH:mm} -> {((Report)ReportsDataGrid.SelectedItem).EndDateTime:yyyy/MM/dd HH:mm}" + ((DateTime.Now < ((Report)ReportsDataGrid.SelectedItem).EndDateTime) ? $" (残り{((Report)ReportsDataGrid.SelectedItem).EndDateTime - DateTime.Now:hh'時間'mm'分'})" : " (締切)");
                ReportDescriptionTextBox.Text = ((Report)ReportsDataGrid.SelectedItem).Description;
                ReportMessageTextBox.Text = ((Report)ReportsDataGrid.SelectedItem).Message;
                if (((Report)ReportsDataGrid.SelectedItem).Files.Length == 0)
                {
                    ReportFilesComboBox.ItemsSource = null;
                    ReportFilesStackPanel.Visibility = Visibility.Hidden;
                }
                else
                {
                    ReportFilesComboBox.ItemsSource = ((Report)ReportsDataGrid.SelectedItem).Files!.Select(x => Path.GetFileName(x));
                    ReportFilesComboBox.SelectedIndex = 0;
                    ReportFilesStackPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void OpenReportFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReportFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((Report)ReportsDataGrid.SelectedItem).Files![ReportFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo(((Report)ReportsDataGrid.SelectedItem).Files![ReportFilesComboBox.SelectedIndex]) { UseShellExecute = true });
                    logger.Info($"Start Process {((Report)ReportsDataGrid.SelectedItem).Files![ReportFilesComboBox.SelectedIndex]}");
                }
            }
        }

        private void OpenReportFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ReportFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((Report)ReportsDataGrid.SelectedItem).Files![ReportFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/e,/select,\"{((Report)ReportsDataGrid.SelectedItem).Files![ReportFilesComboBox.SelectedIndex]}\"", UseShellExecute = true });
                    logger.Info($"Start Process explorer.exe /e,/select,\"{((Report)ReportsDataGrid.SelectedItem).Files![ReportFilesComboBox.SelectedIndex]}\"");
                }
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
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadQuizzesButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadQuizzesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetQuizzes(out List<Quiz> diffQuizzes);
                notifyAPI.SetTodoistTask(gakujoAPI.Quizzes);
                Dispatcher.Invoke(() =>
                {
                    RefreshQuizzesDataGrid();
                    foreach (Quiz quiz in diffQuizzes)
                    {
                        NotifyToast(quiz);
                        notifyAPI.NotifyDiscord(quiz);
                    }
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
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.Quizzes }.View;
            collectionView.Filter = new Predicate<object>(item => (((Quiz)item).Subjects.Contains(QuizzesSearchAutoSuggestBox.Text) || ((Quiz)item).Title.Contains(QuizzesSearchAutoSuggestBox.Text)) && (!(bool)FilterQuizzesCheckBox.IsChecked! || ((Quiz)item).Unsubmitted));
            QuizzesDateTimeLabel.Content = $"最終更新 {gakujoAPI.Account.QuizDateTime:yyyy/MM/dd HH:mm:ss}";
            QuizzesDataGrid.ItemsSource = collectionView;
            QuizzesDataGrid.Items.Refresh();
            logger.Info("Refresh QuizzesDataGrid.");
        }

        private void QuizzesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QuizzesDataGrid.SelectedIndex != -1)
            {
                if (!((Quiz)QuizzesDataGrid.SelectedItem).IsAcquired)
                {
                    if (MessageBox.Show("小テストの詳細を取得しますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                        Task.Run(() => gakujoAPI.GetQuiz((Quiz)QuizzesDataGrid.SelectedItem));
                    }
                }
                QuizStartEndDateTimeLabel.Content = $"{((Quiz)QuizzesDataGrid.SelectedItem).StartDateTime:yyyy/MM/dd HH:mm} -> {((Quiz)QuizzesDataGrid.SelectedItem).EndDateTime:yyyy/MM/dd HH:mm}" + ((DateTime.Now < ((Quiz)QuizzesDataGrid.SelectedItem).EndDateTime) ? $" (残り{((Quiz)QuizzesDataGrid.SelectedItem).EndDateTime - DateTime.Now:hh'時間'mm'分'})" : " (締切)");
                QuizDescriptionTextBox.Text = ((Quiz)QuizzesDataGrid.SelectedItem).Description;
                if (((Quiz)QuizzesDataGrid.SelectedItem).Files.Length == 0)
                {
                    QuizFilesComboBox.ItemsSource = null;
                    QuizFilesStackPanel.Visibility = Visibility.Hidden;
                }
                else
                {
                    QuizFilesComboBox.ItemsSource = ((Quiz)QuizzesDataGrid.SelectedItem).Files!.Select(x => Path.GetFileName(x));
                    QuizFilesComboBox.SelectedIndex = 0;
                    QuizFilesStackPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void OpenQuizFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (QuizFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((Quiz)QuizzesDataGrid.SelectedItem).Files![QuizFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo(((Quiz)QuizzesDataGrid.SelectedItem).Files![QuizFilesComboBox.SelectedIndex]) { UseShellExecute = true });
                    logger.Info($"Start Process {((Quiz)QuizzesDataGrid.SelectedItem).Files![QuizFilesComboBox.SelectedIndex]}");
                }
            }
        }

        private void OpenQuizFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (QuizFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((Quiz)QuizzesDataGrid.SelectedItem).Files![QuizFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/e,/select,\"{((Quiz)QuizzesDataGrid.SelectedItem).Files![QuizFilesComboBox.SelectedIndex]}\"", UseShellExecute = true });
                    logger.Info($"Start Process explorer.exe /e,/select,\"{((Quiz)QuizzesDataGrid.SelectedItem).Files![QuizFilesComboBox.SelectedIndex]}\"");
                }
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
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassSharedFiles(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassSharedFilesDataGrid();
                    for (int i = 0; i < diffCount; i++)
                    {
                        NotifyToast(gakujoAPI.ClassSharedFiles[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.ClassSharedFiles[i]);
                    }
                    LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Visible;
                    LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void RefreshClassSharedFilesDataGrid()
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.ClassSharedFiles }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassSharedFile)item).Subjects.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Title.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Description.Contains(ClassSharedFilesSearchAutoSuggestBox.Text));
            ClassSharedFilesDateTimeLabel.Content = $"最終更新 {gakujoAPI.Account.ClassSharedFileDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassSharedFilesDataGrid.ItemsSource = collectionView;
            ClassSharedFilesDataGrid.Items.Refresh();
            logger.Info("Refresh ClassSharedFilesDataGrid.");
        }

        private void ClassSharedFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSharedFilesDataGrid.SelectedIndex != -1)
            {
                if (!((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).IsAcquired)
                {
                    if (MessageBox.Show("授業共有ファイルの詳細を取得しますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                        int index = ClassSharedFilesDataGrid.SelectedIndex;
                        Task.Run(() => gakujoAPI.GetClassSharedFile(index));
                    }
                }
                ClassSharedFileUpdateDateTimeLabel.Content = ((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).UpdateDateTime.ToString("yyyy/MM/dd HH:mm");
                ClassSharedFileDescriptionTextBox.Text = ((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Description;
                if (((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files.Length == 0)
                {
                    ClassSharedFileFilesComboBox.ItemsSource = null;
                }
                else
                {
                    ClassSharedFileFilesComboBox.ItemsSource = ((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files!.Select(x => Path.GetFileName(x));
                    ClassSharedFileFilesComboBox.SelectedIndex = 0;
                }
            }
        }

        private void OpenClassSharedFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSharedFileFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo(((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]) { UseShellExecute = true });
                    logger.Info($"Start Process {((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]}");
                }
            }
        }

        private void OpenClassSharedFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSharedFileFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe") { Arguments = $"/e,/select,\"{((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]}\"", UseShellExecute = true });
                    logger.Info("Start Process explorer.exe /e,/select,\"{((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]}\"");
                }
            }
        }

        #endregion

        #region 抽選履修登録

        private void LoadLotteryRegistrationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadLotteryRegistrationsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadLotteryRegistrationsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetLotteryRegistrations(out _);
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
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.LotteryRegistrations.SelectMany(_ => _) }.View;
            collectionView.Filter = new Predicate<object>(item => (!(bool)FilterLotteryRegistrationsCheckBox.IsChecked! || ((LotteryRegistration)item).IsRegisterable));
            LotteryRegistrationsDateTimeLabel.Content = $"最終更新 {gakujoAPI.Account.LotteryRegistrationDateTime:yyyy/MM/dd HH:mm:ss}";
            LotteryRegistrationsDataGrid.ItemsSource = collectionView;
            LotteryRegistrationsDataGrid.Items.Refresh();
            logger.Info("Refresh LotteryRegistrationsDataGrid.");
        }

        #endregion

        #region 抽選履修登録結果

        private void LoadLotteryRegistrationsResultButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadLotteryRegistrationsResultButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadLotteryRegistrationsResultButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetLotteryRegistrationsResult();
                Dispatcher.Invoke(() =>
                {
                    RefreshLotteryRegistrationsResultDataGrid();
                    LoadLotteryRegistrationsResultButtonFontIcon.Visibility = Visibility.Visible;
                    LoadLotteryRegistrationsResultButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void RefreshLotteryRegistrationsResultDataGrid()
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.LotteryRegistrationsResult.SelectMany(_ => _) }.View;
            LotteryRegistrationsResultDateTimeLabel.Content = $"最終更新 {gakujoAPI.Account.LotteryRegistrationResultDateTime:yyyy/MM/dd HH:mm:ss}";
            LotteryRegistrationsResultDataGrid.ItemsSource = collectionView;
            LotteryRegistrationsResultDataGrid.Items.Refresh();
            logger.Info("Refresh LotteryRegistrationsResultDataGrid.");
        }

        #endregion

        #region 一般履修登録

        private void LoadGeneralRegistrationsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadGeneralRegistrationsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadGeneralRegistrationsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetGeneralRegistrations();
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
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.RegisterableGeneralRegistrations.SelectMany(_ => _) }.View;
            GeneralRegistrationsDateTimeLabel.Content = $"最終更新 {gakujoAPI.Account.GeneralRegistrationDateTime:yyyy/MM/dd HH:mm:ss}";
            GeneralRegistrationsDataGrid.ItemsSource = collectionView;
            GeneralRegistrationsDataGrid.Items.Refresh();
            logger.Info("Refresh GeneralRegistrationsDataGrid.");
        }

        private void SetGeneralRegistrationsButton_Click(object sender, RoutedEventArgs e)
        {
            SetGeneralRegistrationsDataGrid.ItemsSource = new List<GeneralRegistrationEntry>(gakujoAPI.GeneralRegistrationEntries);
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void GeneralRegistrationsAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                List<string> suitableItems = new();
                string[] splitText = GeneralRegistrationsAutoSuggestBox.Text.Split(" ");
                foreach (GeneralRegistration generalRegistration in gakujoAPI.RegisterableGeneralRegistrations.SelectMany(_ => _))
                {
                    if (splitText.All((key) => { return generalRegistration.SubjectsName.Contains(key); }) && generalRegistration.SubjectsName != "") { suitableItems.Add(generalRegistration.SubjectsName); }
                }
                GeneralRegistrationsAutoSuggestBox.ItemsSource = suitableItems.Distinct();
            }
        }

#pragma warning disable CA1822 // メンバーを static に設定します
        private void GeneralRegistrationsAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
#pragma warning restore CA1822 // メンバーを static に設定します
        {
            //GeneralRegistration generalRegistration = gakujoAPI.RegisterableGeneralRegistrations.SelectMany(_ => _).Where(x => x.SubjectsName == GeneralRegistrationsAutoSuggestBox.Text).First();
            //gakujoAPI.GeneralRegistrationEntries.Add(new GeneralRegistrationEntry() { WeekdayPeriod = generalRegistration.WeekdayPeriod, SubjectsName = generalRegistration.SubjectsName });
            //GeneralRegistrationsAutoSuggestBox.Text = "";
        }

        private void SaveGeneralRegistrationsButton_Click(object sender, RoutedEventArgs e)
        {
            //gakujoAPI.GeneralRegistrationEntries = SetGeneralRegistrationsDataGrid.Items.OfType<GeneralRegistrationEntry>().ToList();
            //gakujoAPI.SaveJsons();
        }

        #endregion

        #region 成績情報

        private void RefreshClassResultsDataGrid()
        {
            ClassResultsDateTimeLabel.Content = $"最終更新 {gakujoAPI.Account.ClassResultDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassResultsDataGrid.ItemsSource = gakujoAPI.SchoolGrade.ClassResults;
            ClassResultsDataGrid.Items.Refresh();
            ClassResultsGPALabel.Content = $"推定GPA {gakujoAPI.SchoolGrade.PreliminaryGPA:N3}";
            logger.Info("Refresh ClassResultsDataGrid.");
        }

        private void LoadClassResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadClassResultsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassResultsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassResults(out List<ClassResult> diffClassResults);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassResultsDataGrid();
                    foreach (ClassResult classResult in diffClassResults)
                    {
                        NotifyToast(classResult);
                        notifyAPI.NotifyDiscord(classResult, true);
                    }
                    LoadClassResultsButtonFontIcon.Visibility = Visibility.Visible;
                    LoadClassResultsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void EvaluationCreditsButton_Click(object sender, RoutedEventArgs e)
        {
            EvaluationCreditsDataGrid.ItemsSource = gakujoAPI.SchoolGrade.EvaluationCredits;
            EvaluationCreditsDataGrid.Items.Refresh();
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void DepartmentGPAButton_Click(object sender, RoutedEventArgs e)
        {
            DepartmentGPALabel.Content = gakujoAPI.SchoolGrade.DepartmentGPA;
            DepartmentGPAImage.Source = Base64ToBitmapImage(gakujoAPI.SchoolGrade.DepartmentGPA.DepartmentImage);
            CourseGPAImage.Source = Base64ToBitmapImage(gakujoAPI.SchoolGrade.DepartmentGPA.CourseImage);
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void YearCreditsButton_Click(object sender, RoutedEventArgs e)
        {
            YearCreditsDataGrid.ItemsSource = gakujoAPI.SchoolGrade.YearCredits;
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
            if (gakujoAPI.ClassTables != null)
            {
                return gakujoAPI.ClassTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)][ClassTablesDataGrid.SelectedCells[0].Column.DisplayIndex];
            }
            return null;
        }

        private void ClassTableCellControl_ClassContactButtonClick(object sender, RoutedEventArgs e)
        {
            ClassTableCell? classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null) { return; }
            ClassContactsSearchAutoSuggestBox.Text = classTableCell.SubjectsName;
            RefreshClassContactsDataGrid();
            e.Handled = true;
            ClassContactsTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_ReportButtonClick(object sender, RoutedEventArgs e)
        {
            ClassTableCell? classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null) { return; }
            ReportsSearchAutoSuggestBox.Text = classTableCell.SubjectsName;
            RefreshReportsDataGrid();
            e.Handled = true;
            ReportsTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_QuizButtonClick(object sender, RoutedEventArgs e)
        {
            ClassTableCell? classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null) { return; }
            QuizzesSearchAutoSuggestBox.Text = classTableCell.SubjectsName;
            RefreshQuizzesDataGrid();
            e.Handled = true;
            QuizzesTabItem.IsSelected = true;
        }

        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SyllabusClassTablesTabItem == null) { return; }
            if (!SyllabusClassTablesTabItem.IsSelected) { SyllabusClassTablesTabItem.Visibility = Visibility.Collapsed; }
        }

        private void ClassTableCellControl_SyllabusMenuItemClick(object sender, RoutedEventArgs e)
        {
            ClassTableCell? classTableCell = GetSelectedClassTableCell();
            if (classTableCell == null) { return; }
            SyllabusMarkdownViewer.Markdown = classTableCell.Syllabus.ToString();
            SyllabusClassTablesTabItem.Visibility = Visibility.Visible;
            e.Handled = true;
            SyllabusClassTablesTabItem.IsSelected = true;
        }

        private void ClassTablesDataGrid_PreviewDrop(object sender, DragEventArgs e)
        {
            if ((GetDataGridCell<ClassTableCellControl>(ClassTablesDataGrid, e.GetPosition(ClassTablesDataGrid))!).DataContext is not ClassTableCell classTableCell) { return; }
            logger.Info($"Start Add Favorites to {classTableCell.SubjectsName}.");
            List<string> favorites = new();
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false)) { favorites.AddRange((string[])e.Data.GetData(DataFormats.FileDrop, false)); }
            if (e.Data.GetDataPresent(DataFormats.Text, false))
            {
                if (Regex.IsMatch((string)e.Data.GetData(DataFormats.Text, false), @"https?:\/\/(www\.)?[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b([-a-zA-Z0-9()@:%_\+.~#?&//=]*)"))
                {
                    favorites.Add((string)e.Data.GetData(DataFormats.Text, false));
                }
            }
            favorites = favorites.Except(classTableCell.Favorites).ToList();
            if (favorites.Count == 0)
            {
                logger.Warn("Return Add Favorites by already exists.");
                MessageBox.Show($"{classTableCell.SubjectsName}のお気に入りにすでに追加されています．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            if (MessageBox.Show($"{classTableCell.SubjectsName}のお気に入りに追加しますか．\n{string.Join('\n', favorites)}", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                classTableCell.Favorites.AddRange(favorites);
                logger.Info($"Add {favorites.Count} Favorites to {classTableCell.SubjectsName}");
                gakujoAPI.SaveJsons();
            }
            else { logger.Warn("Return Add Favorites by user rejection."); return; }
            logger.Info($"End Add Favorites to {classTableCell.SubjectsName}.");
            RefreshClassTablesDataGrid(true);
        }

        public void RefreshClassTablesDataGrid(bool saveJsons = false)
        {
            if (saveJsons) { gakujoAPI.SaveJsons(); }
            LoginDateTimeLabel.Content = $"最終ログイン\n{gakujoAPI.Account.LoginDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassTablesDataGrid.ItemsSource = gakujoAPI.ClassTables.GetRange(0, Math.Min(5, gakujoAPI.ClassTables.Count));
            ClassTablesDataGrid.Items.Refresh();
            logger.Info("Refresh ClassTablesDataGrid.");
        }

        private DataGridCell? GetDataGridCell(DataGrid dataGrid, int rowIndex, int columnIndex)
        {
            if (dataGrid.Items == null || dataGrid.Items.IsEmpty) { return null; }
            DataGridRow dataGridRow = GetDataGridRow(dataGrid, rowIndex)!;
            if (dataGridRow == null) { return null; }
            DataGridCellsPresenter dataGridCellPresenter = GetVisualChild<DataGridCellsPresenter>(dataGridRow)!;
            if (dataGridCellPresenter == null) { return null; }
            DataGridCell dataGridCell = (dataGridCellPresenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell)!;
            if (dataGridCell == null)
            {
                dataGrid.UpdateLayout();
                dataGrid.ScrollIntoView(dataGridRow, dataGrid.Columns[columnIndex]);
                dataGridCell = (dataGridCellPresenter.ItemContainerGenerator.ContainerFromIndex(columnIndex) as DataGridCell)!;
            }
            return dataGridCell;
        }

        private static DataGridRow? GetDataGridRow(DataGrid dataGrid, int index)
        {
            if (dataGrid.Items == null || dataGrid.Items.IsEmpty) { return null; }
            DataGridRow dataGridRow = (dataGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow)!;
            if (dataGridRow == null)
            {
                dataGrid.UpdateLayout();
                dataGrid.ScrollIntoView(dataGrid.Items[index]);
                dataGridRow = (dataGrid.ItemContainerGenerator.ContainerFromIndex(index) as DataGridRow)!;
            }
            return dataGridRow;
        }

        private T? GetVisualChild<T>(Visual parent) where T : Visual
        {
            T? result = default;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                Visual visual = (VisualTreeHelper.GetChild(parent, i) as Visual)!;
                if ((visual as T) != null) { break; }
                result = GetVisualChild<T>((visual as T)!);
            }
            return result;
        }

        private static T? GetDataGridCell<T>(DataGrid dataGrid, Point point)
        {
            T? result = default;
            HitTestResult hitTestResult = VisualTreeHelper.HitTest(dataGrid, point);
            if (hitTestResult != null)
            {
                DependencyObject visualHit = hitTestResult.VisualHit;
                while (visualHit != null)
                {
                    if (visualHit is T)
                    {
                        result = (T)(object)visualHit;
                        break;
                    }
                    visualHit = VisualTreeHelper.GetParent(visualHit);
                }
            }
            return result;
        }

        private void ClassTablesDataGrid_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.Text, false) || e.Data.GetDataPresent(DataFormats.FileDrop, false)) { e.Effects = DragDropEffects.All; }
            else { e.Effects = DragDropEffects.None; }
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
        private void SearchAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
#pragma warning restore CA1822 // メンバーを static に設定します
        {
            sender.Text = args.SelectedItem.ToString();
        }

        private void SearchAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                List<string> suitableItems = new();
                string[] splitText = sender.Text.Split(" ");
                if (gakujoAPI.ClassTables != null)
                {
                    foreach (ClassTableRow classTableRow in gakujoAPI.ClassTables)
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (splitText.All((key) => { return classTableRow[i].SubjectsName.Contains(key); }) && classTableRow[i].SubjectsName != "") { suitableItems.Add(classTableRow[i].SubjectsName); }
                        }
                    }
                }
                sender.ItemsSource = suitableItems.Distinct();
            }
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
                    TaskBarIcon.Visibility = Visibility.Collapsed;
                    logger.Info("Set visibility to Visible.");
                    SyllabusSearchWebView2.Source = new Uri("https://syllabus.shizuoka.ac.jp/ext_syllabus/syllabusSearchDirect.do?nologin=on");
                    logger.Info($"Navigate SyllabusSearch https://syllabus.shizuoka.ac.jp/ext_syllabus/syllabusSearchDirect.do?nologin=on");
                    break;
                case Visibility.Hidden:
                    Visibility = Visibility.Hidden;
                    Hide();
                    ShowInTaskbar = false;
                    TaskBarIcon.Visibility = Visibility.Visible;
                    logger.Info("Set visibility to Hidden.");
                    break;
            }
        }

        private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            ToastArguments toastArguments = ToastArguments.Parse(e.Argument);
            Dispatcher.Invoke(() =>
            {
                SetVisibility(Visibility.Visible);
                logger.Info("Activate MainForm by Toast.");
                if (!toastArguments.Contains("Type") || !toastArguments.Contains("Index"))
                {
                    return;
                }
                logger.Info($"Click Toast Type={toastArguments.Get("Type")}, Index={toastArguments.GetInt("Index")}");
                switch (toastArguments.Get("Type"))
                {
                    case "ClassContact":
                        ClassContactsTabItem.IsSelected = true;
                        ClassContactsDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        break;
                    case "Report":
                        ReportsTabItem.IsSelected = true;
                        ReportsDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        break;
                    case "Quiz":
                        QuizzesTabItem.IsSelected = true;
                        QuizzesDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        break;
                    case "ClassSharedFile":
                        ClassSharedFilesTabItem.IsSelected = true;
                        ClassSharedFilesDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        break;
                    case "ClassResult":
                        ClassResultsTabItem.IsSelected = true;
                        ClassResultsDataGrid.SelectedIndex = toastArguments.GetInt("Index");
                        break;
                }
            });
        }

        private void NotifyToast(ClassContact classContact)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassContact").AddArgument("Index", gakujoAPI.ClassContacts.IndexOf(classContact)).AddText(classContact.Title).AddText(classContact.Content).AddCustomTimeStamp(classContact.ContactDateTime).AddAttributionText(classContact.Subjects).Show();
            logger.Info("Notiy Toast ClassContact.");
        }

        private void NotifyToast(Report report)
        {
            new ToastContentBuilder().AddArgument("Type", "Report").AddArgument("Index", gakujoAPI.Reports.IndexOf(report)).AddText(report.Title).AddText($"{report.StartDateTime} -> {report.EndDateTime}").AddCustomTimeStamp(report.StartDateTime).AddAttributionText(report.Subjects).Show();
            logger.Info("Notiy Toast Report.");
        }

        private void NotifyToast(Quiz quiz)
        {
            new ToastContentBuilder().AddArgument("Type", "Quiz").AddArgument("Index", gakujoAPI.Quizzes.IndexOf(quiz)).AddText(quiz.Title).AddText($"{quiz.StartDateTime} -> {quiz.EndDateTime}").AddCustomTimeStamp(quiz.StartDateTime).AddAttributionText(quiz.Subjects).Show();
            logger.Info("Notify Toast Quiz.");
        }

        private void NotifyToast(ClassSharedFile classSharedFile)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassSharedFile").AddArgument("Index", gakujoAPI.ClassSharedFiles.IndexOf(classSharedFile)).AddText(classSharedFile.Title).AddText(classSharedFile.Description).AddCustomTimeStamp(classSharedFile.UpdateDateTime).AddAttributionText(classSharedFile.Subjects).Show();
            logger.Info("Notify Toast ClassSharedFile.");
        }

        private void NotifyToast(ClassResult classResult)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassResult").AddArgument("Index", gakujoAPI.SchoolGrade.ClassResults.IndexOf(classResult)).AddText(classResult.Subjects).AddText($"{classResult.Score} ({classResult.Evaluation})   {classResult.GP:F1}").AddCustomTimeStamp(classResult.ReportDate).AddAttributionText(classResult.ReportDate.ToString()).Show();
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
            Task.Run(() => Load());
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
            ReportMenuItem.Header = $"レポート {gakujoAPI.Reports.Count(x => x.Unsubmitted)}";
            ReportMenuItem.Visibility = gakujoAPI.Reports.Any(x => x.Unsubmitted) ? Visibility.Visible : Visibility.Collapsed;
            QuizMenuItem.Header = $"小テスト {gakujoAPI.Quizzes.Count(x => x.Unsubmitted)}";
            QuizMenuItem.Visibility = gakujoAPI.Quizzes.Any(x => x.Unsubmitted) ? Visibility.Visible : Visibility.Collapsed;
            logger.Info("Opened ContextMenu.");
        }

        private void ReportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetVisibility(Visibility.Visible);
            ReportsTabItem.IsSelected = true;
            logger.Info("Activate MainForm by ReportMenuItem.");
        }

        private void QuizMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SetVisibility(Visibility.Visible);
            QuizzesTabItem.IsSelected = true;
            logger.Info("Activate MainForm by QuizMenuItem.");
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!shutdownFlag)
            {
                e.Cancel = true;
                SetVisibility(Visibility.Hidden);
                logger.Info("Minimized MainForm by window closing.");
            }
            else { logger.Info("Continue window closing."); }
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
            catch (Exception exception) { logger.Error(exception, "Error Save Settings."); }
        }

        private void SaveTokensButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                Dispatcher.Invoke(() => { notifyAPI.SetTokens(TodoistTokenPasswordBox.Password, DiscordChannelTextBox.Text, DiscordTokenPasswordBox.Password); });
                notifyAPI.Login();
            });
        }

        private void NotificationButton_Click(object sender, RoutedEventArgs e)
        {
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void AutoLoadEnableCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
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
            settings.AutoLoadSpan = (int)AutoLoadSpanNumberBox.Value;
            SaveJson();
            autoLoadTimer.Interval = TimeSpan.FromMinutes(AutoLoadSpanNumberBox.Value);
        }

        private void StartUpEnableCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            settings.StartUpEnable = (bool)StartUpEnableCheckBox.IsChecked!;
            SaveJson();
            SetStartUp();
        }

        private void StartUpMinimizeCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            settings.StartUpMinimize = (bool)StartUpMinimizeCheckBox.IsChecked!;
            SaveJson();
        }

        private void SetStartUp()
        {
            using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)!;
            if (settings.StartUpEnable)
            {
                registryKey.SetValue("GakujoGUI", Environment.ProcessPath!);
                logger.Info("Set RegistryKey enable.");
            }
            else
            {
                registryKey.DeleteValue("GakujoGUI", false);
                logger.Info("Set RegistryKey disable.");
            }
            registryKey.Close();
        }

        private void ResetUserAgentButton_Click(object sender, RoutedEventArgs e)
        {
            UserAgentTextBox.Text = new Settings().UserAgent;
        }

        private void SaveGakujoButton_Click(object sender, RoutedEventArgs e)
        {
            switch (MessageBox.Show("適用するにはGakujoGUIを再起動する必要があります．\n再起動しますか．", "GakujoGUI", MessageBoxButton.YesNoCancel, MessageBoxImage.Information))
            {
                case MessageBoxResult.Yes:
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
                    settings.SchoolYear = (int)SchoolYearNumberBox.Value;
                    settings.SemesterCode = SchoolSemesterComboBox.SelectedIndex;
                    settings.UserAgent = UserAgentTextBox.Text;
                    SaveJson();
                    break;
                case MessageBoxResult.Cancel:
                    SchoolYearNumberBox.Value = settings.SchoolYear;
                    SchoolSemesterComboBox.SelectedIndex = settings.SemesterCode;
                    UserAgentTextBox.Text = settings.UserAgent;
                    break;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            logger.Info($"Start Process {e.Uri.AbsoluteUri}");
            e.Handled = true;
        }

        private void LoadAllClassContactsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadAllClassContactsButtonLabel.Visibility = Visibility.Collapsed;
            LoadAllClassContactsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassContacts(out _, -1);
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
            if (!gakujoAPI.LoginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadAllClassSharedFilesButtonLabel.Visibility = Visibility.Collapsed;
            LoadAllClassSharedFilesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassSharedFiles(out _, -1);
                Dispatcher.Invoke(() =>
                {
                    RefreshClassSharedFilesDataGrid();
                    LoadAllClassSharedFilesButtonLabel.Visibility = Visibility.Visible;
                    LoadAllClassSharedFilesButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }
        private void UpdateBetaEnableCheckBox_CheckStateChanged(object sender, RoutedEventArgs e)
        {
            settings.UpdateBetaEnable = (bool)UpdateBetaEnableCheckBox.IsChecked!;
            SaveJson();
        }

        private void OpenJsonFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"\"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI")}\"",
                UseShellExecute = true
            });
            logger.Info($"Start Process explorer.exe \"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI")}\"");
        }

        private void GetLatestVersionButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                if (!GetLatestVersion(out string latestVersion)) { Dispatcher.Invoke(() => MessageBox.Show("最新の状態です．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information)); }
                else
                {
                    MessageBoxResult? messageBoxResult = MessageBoxResult.No;
                    Dispatcher.Invoke(() => { messageBoxResult = MessageBox.Show($"更新があります．\nv{Assembly.GetExecutingAssembly().GetName().Version} -> v{latestVersion}", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Information); });
                    if (messageBoxResult == MessageBoxResult.Yes && File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "Update.bat")))
                    {
                        Process.Start(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "Update.bat"));
                        logger.Info("Start Process update bat file.");
                        shutdownFlag = true;
                        Dispatcher.Invoke(() => Application.Current.Shutdown());
                    }
                    else
                    {
                        if (File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0.zip")))
                        {
                            File.Delete(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0.zip"));
                            logger.Info("Delete Download latest version.");
                        }
                    }
                }
            });
        }

        private bool GetLatestVersion(out string version)
        {
            logger.Info("Start Get latest version.");
            HttpClient httpClient = new();
            HttpRequestMessage httpRequestMessage = new(new("GET"), "https://api.github.com/repos/xyzyxJP/GakujoGUI-WPF/releases");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", settings.UserAgent);
            HttpResponseMessage httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://api.github.com/repos/xyzyxJP/GakujoGUI-WPF/releases");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Release[] releases = JsonConvert.DeserializeObject<Release[]>(httpResponseMessage.Content.ReadAsStringAsync().Result)!;
            string releaseVersion = releases[0].tag_name.TrimStart('v');
            string latestVersion = releases.Where(x => x.prerelease == false).ToArray()[0].tag_name.TrimStart('v');
            logger.Info($"releaseVersion={releaseVersion}");
            logger.Info($"latestVersion={latestVersion}");
            version = (settings.UpdateBetaEnable ? releaseVersion : latestVersion);
            if (Assembly.GetExecutingAssembly().GetName().Version!.ToString() == version)
            {
                logger.Info("Return Get latest version by the same version.");
                return false;
            }
            if (int.Parse(Assembly.GetExecutingAssembly().GetName().Version!.ToString().Replace(".", "")) > int.Parse(version.Replace(".", "")))
            {
                logger.Info("Return Get latest version by using newer version.");
                return false;
            }
            string latestZipUrl = (settings.UpdateBetaEnable ? releases[0].assets[0].browser_download_url : releases.Where(x => x.prerelease == false).ToArray()[0].assets[0].browser_download_url);
            logger.Info($"latestZipUrl={latestZipUrl}");
            logger.Info("Start Download latest version.");
            httpRequestMessage = new(new("GET"), latestZipUrl);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead).Result;
            logger.Info($"GET {latestZipUrl}");
            using (FileStream fileStream = new(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0.zip"), FileMode.Create, FileAccess.Write, FileShare.None))
            {
                httpResponseMessage.Content.ReadAsStreamAsync().Result.CopyTo(fileStream);
            }
            logger.Info("End Download latest version.");
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0.zip")))
            {
                logger.Warn("Return Get latest version by the file is missing.");
                return false;
            }
            logger.Info("End Get latest version.");
            return true;
        }

        #endregion
    }

    public class Settings
    {
        public bool AutoLoadEnable { get; set; } = true;
        public int AutoLoadSpan { get; set; } = 20;
        public bool StartUpEnable { get; set; } = false;
        public bool StartUpMinimize { get; set; } = false;
        public int SchoolYear { get; set; } = 2021;
        public int SemesterCode { get; set; } = 3;
        public string UserAgent { get; set; } = $"Chrome/100.0.4896.60 GakujoGUI/{Assembly.GetExecutingAssembly().GetName().Version}";
        public bool UpdateBetaEnable { get; set; } = false;
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
