using HtmlAgilityPack;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using ModernWpf.Controls;
using ModernWpf.Controls.Primitives;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.Targets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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
        private readonly GakujoAPI gakujoAPI;
        private readonly NotifyAPI notifyAPI = new();
        private readonly Settings settings = new();
        private readonly DispatcherTimer autoLoadTimer = new();
        private readonly Logger logger = LogManager.GetCurrentClassLogger();
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
            LoggingConfiguration loggingConfiguration = new();
            FileTarget fileTarget = new();
            loggingConfiguration.AddTarget("file", fileTarget);
            fileTarget.Name = "fileTarget";
            fileTarget.FileName = "${basedir}/Logs/${shortdate}.log";
            fileTarget.Layout = "${longdate} [${uppercase:${level}}] ${message}"; ;
            LoggingRule loggingRule = new("*", LogLevel.Debug, fileTarget);
            if (Environment.GetCommandLineArgs().Contains("-trace"))
            { loggingRule = new("*", LogLevel.Trace, fileTarget); }
            loggingConfiguration.LoggingRules.Add(loggingRule);
            LogManager.Configuration = loggingConfiguration;
            logger.Info("Start Logging.");
            Process[] processes = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).Where(x => x.Id != Environment.ProcessId).ToArray();
            if (processes.Length != 0)
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
                ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
                if (File.Exists(GetJsonPath("Settings")))
                {
                    settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(GetJsonPath("Settings")))!;
                    logger.Info("Load Settings.");
                }
                if (settings.StartUpMinimize)
                {
                    ChangeVisibility(Visibility.Hidden);
                    new ToastContentBuilder().AddText("GakujoGUI").AddText("最小化した状態で起動しました．").Show();
                    logger.Info("Startup minimized");
                }
                gakujoAPI = new(settings.SchoolYear.ToString(), settings.SemesterCode, settings.UserAgent);
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UserIdTextBox.Text = gakujoAPI.account.UserId;
            PassWordPasswordBox.Password = gakujoAPI.account.PassWord;
            TodoistTokenPasswordBox.Password = notifyAPI.tokens.TodoistToken;
            DiscordChannelTextBox.Text = notifyAPI.tokens.DiscordChannel.ToString();
            DiscordTokenPasswordBox.Password = notifyAPI.tokens.DiscordToken;
            RefreshClassTablesDataGrid();
            RefreshClassContactsDataGrid();
            RefreshReportsDataGrid();
            RefreshQuizzesDataGrid();
            RefreshClassSharedFilesDataGrid();
            RefreshClassResultsDataGrid();
            AutoLoadEnableCheckBox.IsChecked = settings.AutoLoadEnable;
            AutoLoadSpanNumberBox.Value = settings.AutoLoadSpan;
            StartUpEnableCheckBox.IsChecked = settings.StartUpEnable;
            StartUpMinimizeCheckBox.IsChecked = settings.StartUpMinimize;
            SchoolYearNumberBox.Value = settings.SchoolYear;
            SchoolSemesterComboBox.SelectedIndex = settings.SemesterCode;
            UserAgentTextBox.Text = settings.UserAgent;
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
            Dispatcher.Invoke(() => { gakujoAPI.SetAccount(UserIdTextBox.Text, PassWordPasswordBox.Password); });
            if (string.IsNullOrEmpty(gakujoAPI.account.UserId) || string.IsNullOrEmpty(gakujoAPI.account.PassWord)) { return; }
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

        private void LoadEvent(object sender, EventArgs e)
        {
            Task.Run(() => Load());
        }

        private void Load()
        {
            if (!gakujoAPI.loginStatus) { return; }
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
                LoadClassResultsButtonFontIcon.Visibility = Visibility.Collapsed;
                LoadClassResultsButtonProgressRing.Visibility = Visibility.Visible;
            });
            gakujoAPI.GetClassContacts(out int classContactsDiffCount);
            gakujoAPI.GetReports(out List<Report> diffReports);
            gakujoAPI.GetQuizzes(out List<Quiz> diffQuizzes);
            gakujoAPI.GetClassSharedFiles(out int classSharedFilesDiffCount);
            gakujoAPI.GetClassResults(out List<ClassResult> diffClassResults);
            Dispatcher.Invoke(() =>
            {
                RefreshClassContactsDataGrid();
                RefreshReportsDataGrid();
                RefreshQuizzesDataGrid();
                RefreshClassSharedFilesDataGrid();
                RefreshClassResultsDataGrid();
                if (classContactsDiffCount != gakujoAPI.classContacts.Count)
                {
                    for (int i = 0; i < classContactsDiffCount; i++)
                    {
                        NotifyToast(gakujoAPI.classContacts[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.classContacts[i]);
                    }
                }
                if (diffReports.Count != gakujoAPI.reports.Count)
                {
                    foreach (Report report in diffReports)
                    {
                        NotifyToast(report);
                        notifyAPI.NotifyDiscord(report);
                    }
                }
                if (diffQuizzes.Count != gakujoAPI.quizzes.Count)
                {
                    foreach (Quiz quiz in diffQuizzes)
                    {
                        NotifyToast(quiz);
                        notifyAPI.NotifyDiscord(quiz);
                    }
                }
                if (classSharedFilesDiffCount != gakujoAPI.classSharedFiles.Count)
                {
                    for (int i = 0; i < classSharedFilesDiffCount; i++)
                    {
                        NotifyToast(gakujoAPI.classSharedFiles[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.classSharedFiles[i]);
                    }
                }
                if (diffClassResults.Count != gakujoAPI.schoolGrade.ClassResults.Count)
                {
                    foreach (ClassResult classResult in diffClassResults)
                    {
                        NotifyToast(classResult);
                        notifyAPI.NotifyDiscord(classResult, true);
                    }
                }
                notifyAPI.SetTodoistTask(gakujoAPI.reports);
                notifyAPI.SetTodoistTask(gakujoAPI.quizzes);
                LoadClassContactsButtonFontIcon.Visibility = Visibility.Visible;
                LoadClassContactsButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadReportsButtonFontIcon.Visibility = Visibility.Visible;
                LoadReportsButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadQuizzesButtonFontIcon.Visibility = Visibility.Visible;
                LoadQuizzesButtonProgressRing.Visibility = Visibility.Collapsed;
                LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Visible;
                LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Collapsed;
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
            if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
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
                        NotifyToast(gakujoAPI.classContacts[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.classContacts[i]);
                    }
                    LoadClassContactsButtonFontIcon.Visibility = Visibility.Visible;
                    LoadClassContactsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void RefreshClassContactsDataGrid()
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classContacts }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassContact)item).Subjects.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Title.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Content.Contains(ClassContactsSearchAutoSuggestBox.Text));
            ClassContactsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassContactDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassContactsDataGrid.ItemsSource = collectionView;
            ClassContactsDataGrid.Items.Refresh();
            logger.Info("Refresh ClassContactsDataGrid.");
        }

        private void ClassContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassContactsDataGrid.SelectedIndex != -1)
            {
                if (((ClassContact)ClassContactsDataGrid.SelectedItem).Content == "")
                {
                    if (MessageBox.Show("授業連絡の詳細を取得しますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                        int index = ClassContactsDataGrid.SelectedIndex;
                        Task.Run(() => gakujoAPI.GetClassContact(index));
                    }
                }
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
                    Process.Start(new ProcessStartInfo(((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex])
                    {
                        UseShellExecute = true
                    });
                }
            }
        }

        private void OpenClassContactFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassContactFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = $"/e,/select,\"{((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]}\"",
                        UseShellExecute = true
                    });
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
            if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadReportsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadReportsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetReports(out List<Report> diffReports);
                notifyAPI.SetTodoistTask(gakujoAPI.reports);
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
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.reports }.View;
            collectionView.Filter = new Predicate<object>(item => (((Report)item).Subjects.Contains(ReportsSearchAutoSuggestBox.Text) || ((Report)item).Title.Contains(ReportsSearchAutoSuggestBox.Text)) && (!(bool)FilterReportsCheckBox.IsChecked! || ((Report)item).Unsubmitted));
            ReportsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ReportDateTime:yyyy/MM/dd HH:mm:ss}";
            ReportsDataGrid.ItemsSource = collectionView;
            ReportsDataGrid.Items.Refresh();
            logger.Info("Refresh ReportsDataGrid.");
        }

        private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 小テスト

        private void QuizzesSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            RefreshQuizzesDataGrid();
        }

        private void LoadQuizzesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
            LoadQuizzesButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadQuizzesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetQuizzes(out List<Quiz> diffQuizzes);
                notifyAPI.SetTodoistTask(gakujoAPI.quizzes);
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
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.quizzes }.View;
            collectionView.Filter = new Predicate<object>(item => (((Quiz)item).Subjects.Contains(QuizzesSearchAutoSuggestBox.Text) || ((Quiz)item).Title.Contains(QuizzesSearchAutoSuggestBox.Text)) && (!(bool)FilterQuizzesCheckBox.IsChecked! || ((Quiz)item).Unsubmitted));
            QuizzesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.QuizDateTime:yyyy/MM/dd HH:mm:ss}";
            QuizzesDataGrid.ItemsSource = collectionView;
            QuizzesDataGrid.Items.Refresh();
            logger.Info("Refresh QuizzesDataGrid.");
        }

        private void QuizzesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 授業共有ファイル

        private void ClassSharedFilesSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            RefreshClassSharedFilesDataGrid();
        }

        private void LoadClassSharedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
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
                        NotifyToast(gakujoAPI.classSharedFiles[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.classSharedFiles[i]);
                    }
                    LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Visible;
                    LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void RefreshClassSharedFilesDataGrid()
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classSharedFiles }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassSharedFile)item).Subjects.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Title.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Description.Contains(ClassSharedFilesSearchAutoSuggestBox.Text));
            ClassSharedFilesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassSharedFileDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassSharedFilesDataGrid.ItemsSource = collectionView;
            ClassSharedFilesDataGrid.Items.Refresh();
        }

        private void ClassSharedFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSharedFilesDataGrid.SelectedIndex != -1)
            {
                if (((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Description == "")
                {
                    if (MessageBox.Show("授業共有ファイルの詳細を取得しますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
                        int index = ClassSharedFilesDataGrid.SelectedIndex;
                        Task.Run(() => gakujoAPI.GetClassSharedFile(index));
                    }
                }
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
                    Process.Start(new ProcessStartInfo(((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex])
                    {
                        UseShellExecute = true
                    });
                }
            }
        }

        private void OpenClassSharedFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSharedFileFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]))
                {
                    Process.Start(new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = $"/e,/select,\"{((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]}\"",
                        UseShellExecute = true
                    });
                }
            }
        }

        #endregion

        #region 成績情報

        private void RefreshClassResultsDataGrid()
        {
            ClassResultsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassResultDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassResultsDataGrid.ItemsSource = gakujoAPI.schoolGrade.ClassResults;
            ClassResultsDataGrid.Items.Refresh();
            ClassResultsGPALabel.Content = $"推定GPA {gakujoAPI.schoolGrade.PreliminaryGPA:N3}";
        }

        private void LoadClassResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
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
            EvaluationCreditsDataGrid.ItemsSource = gakujoAPI.schoolGrade.EvaluationCredits;
            EvaluationCreditsDataGrid.Items.Refresh();
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void DepartmentGPAButton_Click(object sender, RoutedEventArgs e)
        {
            DepartmentGPALabel.Content = gakujoAPI.schoolGrade.DepartmentGPA;
            DepartmentGPAImage.Source = Base64ToBitmapImage(gakujoAPI.schoolGrade.DepartmentGPA.DepartmentImage);
            CourseGPAImage.Source = Base64ToBitmapImage(gakujoAPI.schoolGrade.DepartmentGPA.CourseImage);
            FlyoutBase.ShowAttachedFlyout(sender as FrameworkElement);
        }

        private void YearCreditsButton_Click(object sender, RoutedEventArgs e)
        {
            YearCreditsDataGrid.ItemsSource = gakujoAPI.schoolGrade.YearCredits;
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
            ClassTableCell? classTableCell = null;
            if (gakujoAPI.classTables != null)
            {
                switch (ClassTablesDataGrid.SelectedCells[0].Column.DisplayIndex)
                {
                    case 0:
                        classTableCell = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Monday;
                        break;
                    case 1:
                        classTableCell = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Tuesday;
                        break;
                    case 2:
                        classTableCell = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Wednesday;
                        break;
                    case 3:
                        classTableCell = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Thursday;
                        break;
                    case 4:
                        classTableCell = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Friday;
                        break;
                }
            }
            return classTableCell;
        }

        private void ClassTableCellControl_ClassContactButtonClick(object sender, RoutedEventArgs e)
        {
            if (GetSelectedClassTableCell() == null) { return; }
            ClassContactsSearchAutoSuggestBox.Text = GetSelectedClassTableCell()!.SubjectsName;
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classContacts }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassContact)item).Subjects.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Title.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Content.Contains(ClassContactsSearchAutoSuggestBox.Text));
            ClassContactsDataGrid.ItemsSource = collectionView;
            e.Handled = true;
            ClassContactsTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_ReportButtonClick(object sender, RoutedEventArgs e)
        {
            if (GetSelectedClassTableCell() == null) { return; }
            ReportsSearchAutoSuggestBox.Text = GetSelectedClassTableCell()!.SubjectsName;
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.reports }.View;
            collectionView.Filter = new Predicate<object>(item => ((Report)item).Subjects.Contains(ReportsSearchAutoSuggestBox.Text) || ((Report)item).Title.Contains(ReportsSearchAutoSuggestBox.Text));
            ReportsDataGrid.ItemsSource = collectionView;
            e.Handled = true;
            ReportsTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_QuizButtonClick(object sender, RoutedEventArgs e)
        {
            if (GetSelectedClassTableCell() == null) { return; }
            QuizzesSearchAutoSuggestBox.Text = GetSelectedClassTableCell()!.SubjectsName;
            RefreshQuizzesDataGrid();
            e.Handled = true;
            QuizzesTabItem.IsSelected = true;
        }

        private void ClassTableCellControl_SyllabusMenuItemClick(object sender, RoutedEventArgs e)
        {
            if (GetSelectedClassTableCell() == null) { return; }
            logger.Info("Start Get syllabus token.");
            HttpClient httpClient = new();
            HttpRequestMessage httpRequestMessage = new(new("GET"), "https://syllabus.shizuoka.ac.jp/ext_syllabus/syllabusSearchDirect.do?nologin=on");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", settings.UserAgent);
            HttpResponseMessage httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://syllabus.shizuoka.ac.jp/ext_syllabus/syllabusSearchDirect.do?nologin=on");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            string syllabusToken = htmlDocument.DocumentNode.SelectSingleNode("//*[@name=\"syllabusTitleID\"]").Attributes["onchange"].Value.Split("'")[1];
            logger.Info($"syllabusToken={syllabusToken}");
            logger.Info("End Get syllabus token.");
            Process.Start(new ProcessStartInfo($"https://gakujo.shizuoka.ac.jp/syllabus2/rishuuSyllabusSearch.do;jsessionid={syllabusToken}?schoolYear={settings.SchoolYear}&subjectCD={GetSelectedClassTableCell()!.SubjectsId}&classCD={GetSelectedClassTableCell()!.ClassCode}") { UseShellExecute = true });
            logger.Info($"Start Process https://gakujo.shizuoka.ac.jp/syllabus2/rishuuSyllabusSearch.do;jsessionid={syllabusToken}?schoolYear={settings.SchoolYear}&subjectCD={GetSelectedClassTableCell()!.SubjectsId}&classCD={GetSelectedClassTableCell()!.ClassCode}");
            e.Handled = true;
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
            LoginDateTimeLabel.Content = $"最終ログイン\n{gakujoAPI.account.LoginDateTime:yyyy/MM/dd HH:mm:ss}";
            if (gakujoAPI.classTables != null)
            {
                ClassTablesDataGrid.ItemsSource = gakujoAPI.classTables[0..5];
                ClassTablesDataGrid.Items.Refresh();
            }
            logger.Info("Refresh ClassTablesDataGrid.");
        }

#pragma warning disable IDE0051 // 使用されていないプライベート メンバーを削除する
        private DataGridCell? GetDataGridCell(DataGrid dataGrid, int rowIndex, int columnIndex)
#pragma warning restore IDE0051 // 使用されていないプライベート メンバーを削除する
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
                if (gakujoAPI.classTables != null)
                {
                    foreach (ClassTableRow classTableRow in gakujoAPI.classTables)
                    {
                        if (splitText.All((key) => { return classTableRow.Monday.SubjectsName.Contains(key); }) && classTableRow.Monday.SubjectsName != "") { suitableItems.Add(classTableRow.Monday.SubjectsName); }
                        if (splitText.All((key) => { return classTableRow.Tuesday.SubjectsName.Contains(key); }) && classTableRow.Tuesday.SubjectsName != "") { suitableItems.Add(classTableRow.Tuesday.SubjectsName); }
                        if (splitText.All((key) => { return classTableRow.Wednesday.SubjectsName.Contains(key); }) && classTableRow.Wednesday.SubjectsName != "") { suitableItems.Add(classTableRow.Wednesday.SubjectsName); }
                        if (splitText.All((key) => { return classTableRow.Thursday.SubjectsName.Contains(key); }) && classTableRow.Thursday.SubjectsName != "") { suitableItems.Add(classTableRow.Thursday.SubjectsName); }
                        if (splitText.All((key) => { return classTableRow.Friday.SubjectsName.Contains(key); }) && classTableRow.Friday.SubjectsName != "") { suitableItems.Add(classTableRow.Friday.SubjectsName); }
                    }
                }
                sender.ItemsSource = suitableItems.Distinct();
            }
        }

        #endregion

        #region 通知

        private void ChangeVisibility(Visibility visibility)
        {
            switch (visibility)
            {
                case Visibility.Visible:
                    //_ = ShowWindow(Process.GetCurrentProcess().MainWindowHandle, 9);
                    //SetForegroundWindow(Process.GetCurrentProcess().MainWindowHandle);
                    //WindowState = WindowState.Normal;
                    Visibility = Visibility.Visible;
                    Activate();
                    ShowInTaskbar = true;
                    TaskBarIcon.Visibility = Visibility.Collapsed;
                    logger.Info("Change visibility to Visible.");
                    break;
                case Visibility.Hidden:
                    //WindowState = WindowState.Minimized;
                    Visibility = Visibility.Hidden;
                    Hide();
                    ShowInTaskbar = false;
                    TaskBarIcon.Visibility = Visibility.Visible;
                    logger.Info("Change visibility to Hidden.");
                    break;
            }
        }

        private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            ToastArguments toastArguments = ToastArguments.Parse(e.Argument);
            Dispatcher.Invoke(() =>
            {
                ChangeVisibility(Visibility.Visible);
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
            new ToastContentBuilder().AddArgument("Type", "ClassContact").AddArgument("Index", gakujoAPI.classContacts.IndexOf(classContact)).AddText(classContact.Title).AddText(classContact.Content).AddCustomTimeStamp(classContact.ContactDateTime).AddAttributionText(classContact.Subjects).Show();
            logger.Info("Notiy Toast ClassContact.");
        }

        private void NotifyToast(Report report)
        {
            new ToastContentBuilder().AddArgument("Type", "Report").AddArgument("Index", gakujoAPI.reports.IndexOf(report)).AddText(report.Title).AddText($"{report.StartDateTime} -> {report.EndDateTime}").AddCustomTimeStamp(report.StartDateTime).AddAttributionText(report.Subjects).Show();
            logger.Info("Notiy Toast Report.");
        }

        private void NotifyToast(Quiz quiz)
        {
            new ToastContentBuilder().AddArgument("Type", "Quiz").AddArgument("Index", gakujoAPI.quizzes.IndexOf(quiz)).AddText(quiz.Title).AddText($"{quiz.StartDateTime} -> {quiz.EndDateTime}").AddCustomTimeStamp(quiz.StartDateTime).AddAttributionText(quiz.Subjects).Show();
            logger.Info("Notify Toast Quiz.");
        }

        private void NotifyToast(ClassSharedFile classSharedFile)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassSharedFile").AddArgument("Index", gakujoAPI.classSharedFiles.IndexOf(classSharedFile)).AddText(classSharedFile.Title).AddText(classSharedFile.Description).AddCustomTimeStamp(classSharedFile.UpdateDateTime).AddAttributionText(classSharedFile.Subjects).Show();
            logger.Info("Notify Toast ClassSharedFile.");
        }

        private void NotifyToast(ClassResult classResult)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassResult").AddArgument("Index", gakujoAPI.schoolGrade.ClassResults.IndexOf(classResult)).AddText(classResult.Subjects).AddText($"{classResult.Score} ({classResult.Evaluation})   {classResult.GP:F1}").AddCustomTimeStamp(classResult.ReportDate).AddAttributionText(classResult.ReportDate.ToString()).Show();
            logger.Info("Notify Toast ClassResult.");
        }

        #endregion

        #region タスクバー

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ChangeVisibility(Visibility.Visible);
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
            ChangeVisibility(Visibility.Visible);
            logger.Info("Activate MainForm by TaskBarIcon.");
        }

        private void ContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            ReportMenuItem.Header = $"レポート {gakujoAPI.reports.Count(report => report.Unsubmitted)}";
            QuizMenuItem.Header = $"小テスト {gakujoAPI.quizzes.Count(report => report.Unsubmitted)}";
        }

        private void ReportMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ChangeVisibility(Visibility.Visible);
            ReportsTabItem.IsSelected = true;
            logger.Info("Activate MainForm by ReportMenuItem.");
        }

        private void QuizMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ChangeVisibility(Visibility.Visible);
            QuizzesTabItem.IsSelected = true;
            logger.Info("Activate MainForm by QuizMenuItem.");
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (!shutdownFlag)
            {
                e.Cancel = true;
                ChangeVisibility(Visibility.Hidden);
                //new ToastContentBuilder().AddText("GakujoGUI").AddText("最小化した状態に移動しました．").Show();
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

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
        }

        private void LoadAllClassContactsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
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
            if (!gakujoAPI.loginStatus) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); return; }
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

        private void OpenJsonFolderButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo("explorer.exe")
            {
                Arguments = $"\"{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI")}\"",
                UseShellExecute = true
            });
        }

        private void GetLatestVersionButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                if (!GetLatestVersion(out string latestVersion)) { Dispatcher.Invoke(() => MessageBox.Show("最新の状態です．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information)); }
                else
                {
                    MessageBoxResult? messageBoxResult = MessageBoxResult.No;
                    Dispatcher.Invoke(() => { messageBoxResult = MessageBox.Show($"更新があります．\n{latestVersion}", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Information); });
                    if (messageBoxResult == MessageBoxResult.Yes && File.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "Update.bat")))
                    {
                        logger.Info("Start Process update bat file.");
                        Process.Start(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "Update.bat"));
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
                        if (Directory.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0")))
                        {
                            foreach (FileInfo fileInfo in new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0")).GetFiles())
                            {
                                fileInfo.Delete();
                            }
                            Directory.Delete(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0"));
                            logger.Info("Delete Extract latest version.");
                        }
                    }
                }
            });
        }

        private bool GetLatestVersion(out string latestVersion)
        {
            logger.Info("Start Get latest version.");
            HttpClient httpClient = new();
            HttpRequestMessage httpRequestMessage = new(new("GET"), "https://api.github.com/repos/xyzyxJP/GakujoGUI-WPF/releases/latest");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", settings.UserAgent);
            HttpResponseMessage httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://api.github.com/repos/xyzyxJP/GakujoGUI-WPF/releases/latest");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            ReleaseAPI releaseAPI = JsonConvert.DeserializeObject<ReleaseAPI>(httpResponseMessage.Content.ReadAsStringAsync().Result)!;
            latestVersion = releaseAPI.name.TrimStart('v');
            logger.Info($"latestVersion={latestVersion}");
            if (Assembly.GetExecutingAssembly().GetName().Version!.ToString() == latestVersion)
            {
                logger.Info("Return Get latest version by the same version.");
                return false;
            }
            string latestZipUrl = releaseAPI.assets[0].browser_download_url;
            logger.Info($"latestZipUrl={latestZipUrl}");
            logger.Info("Start Download latest version.");
            httpRequestMessage = new(new("GET"), latestZipUrl);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage, HttpCompletionOption.ResponseHeadersRead).Result;
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
            if (Directory.Exists(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0")))
            {
                foreach (FileInfo fileInfo in new DirectoryInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0")).GetFiles())
                {
                    fileInfo.Delete();
                }
                Directory.Delete(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0"));
            }
            ZipFile.ExtractToDirectory(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0.zip"), Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location)!, "net6.0-windows10.0.18362.0"));
            logger.Info("Extract latest version.");
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

        public int SemesterCode { get; set; } = 2;

        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.164 Safari/537.36 Edg/91.0.864.71";
    }

#pragma warning disable IDE1006 // 命名スタイル
    public class ReleaseAPI
    {
        public string url { get; set; } = "";
        public string assets_url { get; set; } = "";
        public string upload_url { get; set; } = "";
        public string html_url { get; set; } = "";
        public int id { get; set; }
        public Author author { get; set; } = new();
        public string node_id { get; set; } = "";
        public string tag_name { get; set; } = "";
        public string target_commitish { get; set; } = "";
        public string name { get; set; } = "";
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public DateTime created_at { get; set; }
        public DateTime published_at { get; set; }
        public Asset[] assets { get; set; } = Array.Empty<Asset>();
        public string tarball_url { get; set; } = "";
        public string zipball_url { get; set; } = "";
        public string body { get; set; } = "";
    }

    public class Author
    {
        public string login { get; set; } = "";
        public int id { get; set; }
        public string node_id { get; set; } = "";
        public string avatar_url { get; set; } = "";
        public string gravatar_id { get; set; } = "";
        public string url { get; set; } = "";
        public string html_url { get; set; } = "";
        public string followers_url { get; set; } = "";
        public string following_url { get; set; } = "";
        public string gists_url { get; set; } = "";
        public string starred_url { get; set; } = "";
        public string subscriptions_url { get; set; } = "";
        public string organizations_url { get; set; } = "";
        public string repos_url { get; set; } = "";
        public string events_url { get; set; } = "";
        public string received_events_url { get; set; } = "";
        public string type { get; set; } = "";
        public bool site_admin { get; set; }
    }

    public class Asset
    {
        public string url { get; set; } = "";
        public int id { get; set; }
        public string node_id { get; set; } = "";
        public string name { get; set; } = "";
        public object label { get; set; } = new();
        public Uploader uploader { get; set; } = new();
        public string content_type { get; set; } = "";
        public string state { get; set; } = "";
        public int size { get; set; }
        public int download_count { get; set; }
        public DateTime created_at { get; set; }
        public DateTime updated_at { get; set; }
        public string browser_download_url { get; set; } = "";
    }

    public class Uploader
    {
        public string login { get; set; } = "";
        public int id { get; set; }
        public string node_id { get; set; } = "";
        public string avatar_url { get; set; } = "";
        public string gravatar_id { get; set; } = "";
        public string url { get; set; } = "";
        public string html_url { get; set; } = "";
        public string followers_url { get; set; } = "";
        public string following_url { get; set; } = "";
        public string gists_url { get; set; } = "";
        public string starred_url { get; set; } = "";
        public string subscriptions_url { get; set; } = "";
        public string organizations_url { get; set; } = "";
        public string repos_url { get; set; } = "";
        public string events_url { get; set; } = "";
        public string received_events_url { get; set; } = "";
        public string type { get; set; } = "";
        public bool site_admin { get; set; }
    }
#pragma warning restore IDE1006 // 命名スタイル
}
