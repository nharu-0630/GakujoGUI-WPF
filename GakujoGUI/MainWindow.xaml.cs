using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using ModernWpf.Controls;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

        private static string GetJsonPath(string value)
        {
            return Path.Combine(Environment.CurrentDirectory, @$"Json\{value}.json");
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public MainWindow()
        {
            InitializeComponent();
            Process[] processes = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
            int processId = Environment.ProcessId;
            foreach (Process process in processes)
            {
                if (process.Id != processId)
                {
                    _ = ShowWindow(process.MainWindowHandle, 1);
                    SetForegroundWindow(process.MainWindowHandle);
                    Application.Current.Shutdown();
                }
            }
            ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
            if (File.Exists(GetJsonPath("Settings")))
            {
                settings = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(GetJsonPath("Settings")))!;
            }
            if (settings.StartUpMinimize)
            {
                WindowState = WindowState.Minimized;
                ShowInTaskbar = false;
                TaskBarIcon.Visibility = Visibility.Visible;
                Visibility = Visibility.Hidden;
                Hide();
                new ToastContentBuilder().AddText("GakujoGUI").AddText("最小化した状態で起動しました．").Show();
            }
            gakujoAPI = new(settings.SchoolYear.ToString(), settings.SemesterCode, settings.UserAgent);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UserIdTextBox.Text = gakujoAPI.account.UserId;
            PassWordPasswordBox.Password = gakujoAPI.account.PassWord;
            LoginDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.LoginDateTime:yyyy/MM/dd HH:mm:ss}";
            TodoistTokenPasswordBox.Password = notifyAPI.tokens.TodoistToken;
            DiscordChannelTextBox.Text = notifyAPI.tokens.DiscordChannel.ToString();
            DiscordTokenPasswordBox.Password = notifyAPI.tokens.DiscordToken;
            if (gakujoAPI.classTables != null)
            {
                ClassTablesDataGrid.ItemsSource = gakujoAPI.classTables[0..5];
                ClassTablesDataGrid.Items.Refresh();
            }
            ClassContactsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassContactDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
            ClassContactsDataGrid.Items.Refresh();
            ReportsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ReportDateTime:yyyy/MM/dd HH:mm:ss}";
            ReportsDataGrid.ItemsSource = gakujoAPI.reports;
            ReportsDataGrid.Items.Refresh();
            QuizzesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.QuizDateTime:yyyy/MM/dd HH:mm:ss}";
            QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
            QuizzesDataGrid.Items.Refresh();
            ClassSharedFilesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassSharedFileDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
            ClassSharedFilesDataGrid.Items.Refresh();
            ClassResultsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassResultDateTime:yyyy/MM/dd HH:mm:ss}";
            ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
            ClassResultsDataGrid.Items.Refresh();
            AutoLoadEnableCheckBox.IsChecked = settings.AutoLoadEnable;
            AutoLoadSpanNumberBox.Value = settings.AutoLoadSpan;
            StartUpEnableCheckBox.IsChecked = settings.StartUpEnable;
            StartUpMinimizeCheckBox.IsChecked = settings.StartUpMinimize;
            SchoolYearNumberBox.Value = settings.SchoolYear;
            SchoolSemesterComboBox.SelectedIndex = settings.SemesterCode;
            UserAgentTextBox.Text = settings.UserAgent;
            VersionLabel.Content = Assembly.GetExecutingAssembly().GetName().Version;
            Task.Run(() =>
            {
                gakujoAPI.LoadJson();
                Login();
                Load();
                Dispatcher.Invoke(() =>
                {
                    autoLoadTimer.Interval = TimeSpan.FromMinutes(AutoLoadSpanNumberBox.Value);
                    autoLoadTimer.Tick += new EventHandler(LoadEvent);
                    autoLoadTimer.Start();
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
            Dispatcher.Invoke(() =>
            {
                gakujoAPI.SetAccount(UserIdTextBox.Text, PassWordPasswordBox.Password);
            });
            if (gakujoAPI.account.UserId == "" || gakujoAPI.account.PassWord == "")
            {
                return;
            }
            Dispatcher.Invoke(() =>
            {
                LoginButtonFontIcon.Visibility = Visibility.Collapsed;
                LoginButtonProgressRing.Visibility = Visibility.Visible;
            });
            if (!gakujoAPI.Login())
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show("自動ログインに失敗しました．静大IDまたはパスワードが正しくありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
            else
            {
                gakujoAPI.GetClassTables();
                Dispatcher.Invoke(() =>
                {
                    LoginDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.LoginDateTime:yyyy/MM/dd HH:mm:ss}";
                    ClassTablesDataGrid.ItemsSource = gakujoAPI.classTables![0..5];
                    ClassTablesDataGrid.Items.Refresh();
                });
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
            if (!gakujoAPI.loginStatus)
            {
                return;
            }
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
                ClassContactsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassContactDateTime:yyyy/MM/dd HH:mm:ss}";
                ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
                ClassContactsDataGrid.Items.Refresh();
                ReportsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ReportDateTime:yyyy/MM/dd HH:mm:ss}";
                ReportsDataGrid.ItemsSource = gakujoAPI.reports;
                ReportsDataGrid.Items.Refresh();
                QuizzesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.QuizDateTime:yyyy/MM/dd HH:mm:ss}";
                QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
                QuizzesDataGrid.Items.Refresh();
                ClassSharedFilesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassSharedFileDateTime:yyyy/MM/dd HH:mm:ss}";
                ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
                ClassSharedFilesDataGrid.Items.Refresh();
                ClassResultsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassResultDateTime:yyyy/MM/dd HH:mm:ss}";
                ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
                ClassResultsDataGrid.Items.Refresh();
                for (int i = 0; i < classContactsDiffCount; i++)
                {
                    NotifyToast(gakujoAPI.classContacts[i]);
                    notifyAPI.NotifyDiscord(gakujoAPI.classContacts[i]);
                }
                foreach (Report report in diffReports)
                {
                    NotifyToast(report);
                    notifyAPI.NotifyDiscord(report);
                }
                foreach (Quiz quiz in diffQuizzes)
                {
                    NotifyToast(quiz);
                    notifyAPI.NotifyDiscord(quiz);
                }
                for (int i = 0; i < classSharedFilesDiffCount; i++)
                {
                    NotifyToast(gakujoAPI.classSharedFiles[i]);
                    notifyAPI.NotifyDiscord(gakujoAPI.classSharedFiles[i]);
                }
                foreach (ClassResult classResult in diffClassResults)
                {
                    NotifyToast(classResult);
                    notifyAPI.NotifyDiscord(classResult, true);
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
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classContacts }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassContact)item).Subjects.Contains(sender.Text) || ((ClassContact)item).Title.Contains(sender.Text) || ((ClassContact)item).Content.Contains(sender.Text));
            ClassContactsDataGrid.ItemsSource = collectionView;
        }

        private void LoadClassContactsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadClassContactsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassContactsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassContacts(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    ClassContactsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassContactDateTime:yyyy/MM/dd HH:mm:ss}";
                    ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
                    ClassContactsDataGrid.Items.Refresh();
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

        private void ClassContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassContactsDataGrid.SelectedIndex != -1)
            {
                if (((ClassContact)ClassContactsDataGrid.SelectedItem).Content == "")
                {
                    if (MessageBox.Show("授業連絡の詳細を取得しますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!gakujoAPI.loginStatus)
                        {
                            MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
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
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.reports }.View;
            collectionView.Filter = new Predicate<object>(item => ((Report)item).Subjects.Contains(sender.Text) || ((Report)item).Title.Contains(sender.Text));
            ReportsDataGrid.ItemsSource = collectionView;
        }

        private void LoadReportsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadReportsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadReportsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetReports(out List<Report> diffReports);
                notifyAPI.SetTodoistTask(gakujoAPI.reports);
                Dispatcher.Invoke(() =>
                {
                    ReportsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ReportDateTime:yyyy/MM/dd HH:mm:ss}";
                    ReportsDataGrid.ItemsSource = gakujoAPI.reports;
                    ReportsDataGrid.Items.Refresh();
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

        private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 小テスト

        private void QuizzesSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.quizzes }.View;
            collectionView.Filter = new Predicate<object>(item => ((Quiz)item).Subjects.Contains(sender.Text) || ((Quiz)item).Title.Contains(sender.Text));
            QuizzesDataGrid.ItemsSource = collectionView;
        }

        private void LoadQuizzesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadQuizzesButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadQuizzesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetQuizzes(out List<Quiz> diffQuizzes);
                notifyAPI.SetTodoistTask(gakujoAPI.quizzes);
                Dispatcher.Invoke(() =>
                {
                    QuizzesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.QuizDateTime:yyyy/MM/dd HH:mm:ss}";
                    QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
                    QuizzesDataGrid.Items.Refresh();
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

        private void QuizzesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 授業共有ファイル

        private void ClassSharedFilesSearchAutoSuggestBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classSharedFiles }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassSharedFile)item).Subjects.Contains(sender.Text) || ((ClassSharedFile)item).Title.Contains(sender.Text) || ((ClassSharedFile)item).Description.Contains(sender.Text));
            ClassSharedFilesDataGrid.ItemsSource = collectionView;
        }

        private void LoadClassSharedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadClassSharedFilesButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassSharedFilesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassSharedFiles(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    ClassSharedFilesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassSharedFileDateTime:yyyy/MM/dd HH:mm:ss}";
                    ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
                    ClassSharedFilesDataGrid.Items.Refresh();
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

        private void ClassSharedFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSharedFilesDataGrid.SelectedIndex != -1)
            {
                if (((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Description == "")
                {
                    if (MessageBox.Show("授業共有ファイルの詳細を取得しますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                    {
                        if (!gakujoAPI.loginStatus)
                        {
                            MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                            return;
                        }
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

        private void LoadClassResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadClassResultsButtonFontIcon.Visibility = Visibility.Collapsed;
            LoadClassResultsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassResults(out List<ClassResult> diffClassResults);
                Dispatcher.Invoke(() =>
                {
                    ClassResultsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassResultDateTime:yyyy/MM/dd HH:mm:ss}";
                    ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
                    ClassResultsDataGrid.Items.Refresh();
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

        #endregion

        #region 個人時間割

        private string GetClassTablesCellSubjectsName()
        {
            string suggestText = "";
            if (gakujoAPI.classTables != null)
            {
                switch (ClassTablesDataGrid.SelectedCells[0].Column.DisplayIndex)
                {
                    case 0:
                        suggestText = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Monday.SubjectsName;
                        break;
                    case 1:
                        suggestText = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Tuesday.SubjectsName;
                        break;
                    case 2:
                        suggestText = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Wednesday.SubjectsName;
                        break;
                    case 3:
                        suggestText = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Thursday.SubjectsName;
                        break;
                    case 4:
                        suggestText = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Friday.SubjectsName;
                        break;
                }
            }
            return suggestText;
        }

        private void ClassTablesCell_ClassContactButtonClick(object sender, RoutedEventArgs e)
        {
            if (GetClassTablesCellSubjectsName() != "")
            {
                ClassContactsSearchAutoSuggestBox.Text = GetClassTablesCellSubjectsName();
                ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classContacts }.View;
                collectionView.Filter = new Predicate<object>(item => ((ClassContact)item).Subjects.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Title.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Content.Contains(ClassContactsSearchAutoSuggestBox.Text));
                ClassContactsDataGrid.ItemsSource = collectionView;
                e.Handled = true;
                ClassContactsTabItem.IsSelected = true;
            }
        }

        private void ClassTablesCell_ReportButtonClick(object sender, RoutedEventArgs e)
        {
            if (GetClassTablesCellSubjectsName() != "")
            {
                ReportsSearchAutoSuggestBox.Text = GetClassTablesCellSubjectsName();
                ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.reports }.View;
                collectionView.Filter = new Predicate<object>(item => ((Report)item).Subjects.Contains(ReportsSearchAutoSuggestBox.Text) || ((Report)item).Title.Contains(ReportsSearchAutoSuggestBox.Text));
                ReportsDataGrid.ItemsSource = collectionView;
                e.Handled = true;
                ReportsTabItem.IsSelected = true;
            }
        }

        private void ClassTablesCell_QuizButtonClick(object sender, RoutedEventArgs e)
        {
            if (GetClassTablesCellSubjectsName() != "")
            {
                QuizzesSearchAutoSuggestBox.Text = GetClassTablesCellSubjectsName();
                ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.quizzes }.View;
                collectionView.Filter = new Predicate<object>(item => ((Quiz)item).Subjects.Contains(QuizzesSearchAutoSuggestBox.Text) || ((Quiz)item).Title.Contains(QuizzesSearchAutoSuggestBox.Text));
                QuizzesDataGrid.ItemsSource = collectionView;
                e.Handled = true;
                QuizzesTabItem.IsSelected = true;
            }
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

        private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            ToastArguments toastArguments = ToastArguments.Parse(e.Argument);
            Dispatcher.Invoke(() =>
            {
                ShowInTaskbar = true;
                TaskBarIcon.Visibility = Visibility.Collapsed;
                Visibility = Visibility.Visible;
                Topmost = true;
                SystemCommands.RestoreWindow(this);
                Topmost = false;
                if (!toastArguments.Contains("Type") || !toastArguments.Contains("Index"))
                {
                    return;
                }
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
        }

        private void NotifyToast(Report report)
        {
            new ToastContentBuilder().AddArgument("Type", "Report").AddArgument("Index", gakujoAPI.reports.IndexOf(report)).AddText(report.Title).AddText($"{report.StartDateTime} -> {report.EndDateTime}").AddCustomTimeStamp(report.StartDateTime).AddAttributionText(report.Subjects).Show();
        }

        private void NotifyToast(Quiz quiz)
        {
            new ToastContentBuilder().AddArgument("Type", "Quiz").AddArgument("Index", gakujoAPI.quizzes.IndexOf(quiz)).AddText(quiz.Title).AddText($"{quiz.StartDateTime} -> {quiz.EndDateTime}").AddCustomTimeStamp(quiz.StartDateTime).AddAttributionText(quiz.Subjects).Show();
        }

        private void NotifyToast(ClassSharedFile classSharedFile)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassSharedFile").AddArgument("Index", gakujoAPI.classSharedFiles.IndexOf(classSharedFile)).AddText(classSharedFile.Title).AddText(classSharedFile.Description).AddCustomTimeStamp(classSharedFile.UpdateDateTime).AddAttributionText(classSharedFile.Subjects).Show();
        }

        private void NotifyToast(ClassResult classResult)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassResult").AddArgument("Index", gakujoAPI.classResults.IndexOf(classResult)).AddText(classResult.Subjects).AddText($"{classResult.Score} ({classResult.Evaluation})   {classResult.GP:F1}").AddCustomTimeStamp(classResult.ReportDate).AddAttributionText(classResult.ReportDate.ToString()).Show();
        }

        #endregion

        #region タスクバー

        private void OpenMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ShowInTaskbar = true;
            TaskBarIcon.Visibility = Visibility.Collapsed;
            Visibility = Visibility.Visible;
            Topmost = true;
            SystemCommands.RestoreWindow(this);
            Topmost = false;
        }

        private void LoadMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => Load());
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void TaskBarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowInTaskbar = true;
            TaskBarIcon.Visibility = Visibility.Collapsed;
            Visibility = Visibility.Visible;
            Topmost = true;
            SystemCommands.RestoreWindow(this);
            Topmost = false;
        }

        private void Window_Deactivated(object sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                ShowInTaskbar = false;
                TaskBarIcon.Visibility = Visibility.Visible;
                Visibility = Visibility.Hidden;
                Hide();
            }
        }

        #endregion

        #region 設定

        private void SaveJson()
        {
            try
            {
                File.WriteAllText(GetJsonPath("Settings"), JsonConvert.SerializeObject(settings, Formatting.Indented));
            }
            catch { }
        }

        private void SaveTokensButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    notifyAPI.SetTokens(TodoistTokenPasswordBox.Password, DiscordChannelTextBox.Text, DiscordTokenPasswordBox.Password);
                });
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
            }
            else
            {
                autoLoadTimer.Stop();
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
            }
            else
            {
                registryKey.DeleteValue("GakujoGUI", false);
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
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadAllClassContactsButtonLabel.Visibility = Visibility.Collapsed;
            LoadAllClassContactsButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassContacts(out _, -1);
                Dispatcher.Invoke(() =>
                {
                    ClassContactsDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassContactDateTime:yyyy/MM/dd HH:mm:ss}";
                    ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
                    ClassContactsDataGrid.Items.Refresh();
                    LoadAllClassContactsButtonLabel.Visibility = Visibility.Visible;
                    LoadAllClassContactsButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void LoadAllClassSharedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            LoadAllClassSharedFilesButtonLabel.Visibility = Visibility.Collapsed;
            LoadAllClassSharedFilesButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassSharedFiles(out _, -1);
                Dispatcher.Invoke(() =>
                {
                    ClassSharedFilesDateTimeLabel.Content = $"最終更新 {gakujoAPI.account.ClassSharedFileDateTime:yyyy/MM/dd HH:mm:ss}";
                    ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
                    ClassSharedFilesDataGrid.Items.Refresh();
                    LoadAllClassSharedFilesButtonLabel.Visibility = Visibility.Visible;
                    LoadAllClassSharedFilesButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        #endregion

    }

    public class Settings
    {
        public bool AutoLoadEnable { get; set; } = true;

        public int AutoLoadSpan { get; set; } = 10;

        public bool StartUpEnable { get; set; } = false;

        public bool StartUpMinimize { get; set; } = false;

        public int SchoolYear { get; set; } = 2021;

        public int SemesterCode { get; set; } = 2;

        public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.164 Safari/537.36 Edg/91.0.864.71";
    }
}
