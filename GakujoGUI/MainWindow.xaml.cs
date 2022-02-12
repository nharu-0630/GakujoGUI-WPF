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
        private readonly GakujoAPI gakujoAPI = new();
        private readonly NotifyAPI notifyAPI = new();

        private Settings settings = new();

        private readonly DispatcherTimer autoLoadTimer = new();

        private static string GetJsonPath(string value)
        {
            return Path.Combine(Environment.CurrentDirectory, @"Json\" + value + ".json");
        }

        public MainWindow()
        {
            InitializeComponent();
            ToastNotificationManagerCompat.OnActivated += ToastNotificationManagerCompat_OnActivated;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
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
            }
            UserIdTextBox.Text = gakujoAPI.account.UserId;
            PassWordPasswordBox.Password = gakujoAPI.account.PassWord;
            LoginDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.LoginDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            TodoistTokenPasswordBox.Password = notifyAPI.tokens.TodoistToken;
            DiscordChannelTextBox.Text = notifyAPI.tokens.DiscordChannel.ToString();
            DiscordTokenPasswordBox.Password = notifyAPI.tokens.DiscordToken;
            ClassTablesDataGrid.ItemsSource = gakujoAPI.classTables[0..5];
            ClassContactsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
            ReportsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ReportsDataGrid.ItemsSource = gakujoAPI.reports;
            QuizzesDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
            ClassSharedFilesDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
            ClassResultsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassResultDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
            AutoLoadEnableCheckBox.IsChecked = settings.AutoLoadEnable;
            AutoLoadSpanNumberBox.Value = settings.AutoLoadSpan;
            StartUpEnableCheckBox.IsChecked = settings.StartUpEnable;
            StartUpMinimizeCheckBox.IsChecked = settings.StartUpMinimize;
            SchoolYearNumberBox.Value = settings.SchoolYear;
            SchoolSemesterComboBox.SelectedIndex = settings.SemesterCode == 1 ? 0 : 2;
            UserAgentTextBox.Text = settings.UserAgent;
            VersionLabel.Content = Assembly.GetExecutingAssembly().GetName().Version;
            Task.Run(() =>
            {
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
            Task.Run(() => Login());
        }

        private void Login(bool messageBox = true)
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
                    if (messageBox) { MessageBox.Show("自動ログインに失敗しました．静大IDまたはパスワードが正しくありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); }
                });
            }
            else
            {
                gakujoAPI.GetClassTables();
                gakujoAPI.GetClassResults(out _);
                Dispatcher.Invoke(() =>
                {
                    ClassTablesDataGrid.ItemsSource = gakujoAPI.classTables;
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

        private void Load(bool messageBox = true)
        {
            if (!gakujoAPI.loginStatus)
            {
                Dispatcher.Invoke(() =>
                {
                    if (messageBox) { MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error); }
                });
                return;
            }
            Dispatcher.Invoke(() =>
            {
                ClassContactsLoadButtonFontIcon.Visibility = Visibility.Collapsed;
                ClassContactsLoadButtonProgressRing.Visibility = Visibility.Visible;
                ReportsLoadButtonFontIcon.Visibility = Visibility.Collapsed;
                ReportsLoadButtonProgressRing.Visibility = Visibility.Visible;
                QuizzesLoadButtonFontIcon.Visibility = Visibility.Collapsed;
                QuizzesLoadButtonProgressRing.Visibility = Visibility.Visible;
                ClassSharedFilesLoadButtonFontIcon.Visibility = Visibility.Collapsed;
                ClassSharedFilesLoadButtonProgressRing.Visibility = Visibility.Visible;
                ClassResultsLoadButtonFontIcon.Visibility = Visibility.Collapsed;
                ClassResultsLoadButtonProgressRing.Visibility = Visibility.Visible;
            });
            gakujoAPI.GetClassContacts(out int classContactsDiffCount);
            gakujoAPI.GetReports(out int reportsDiffCount);
            gakujoAPI.GetQuizzes(out int quizzesDiffCount);
            gakujoAPI.GetClassSharedFiles(out int classSharedFilesDiffCount);
            gakujoAPI.GetClassResults(out int classResultsDiffCount);
            Dispatcher.Invoke(() =>
            {
                ClassContactsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
                ReportsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                ReportsDataGrid.ItemsSource = gakujoAPI.reports;
                QuizzesDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
                ClassSharedFilesDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
                ClassResultsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassResultDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
                for (int i = 0; i < classContactsDiffCount; i++)
                {
                    NotifyToast(gakujoAPI.classContacts[i]);
                    notifyAPI.NotifyDiscord(gakujoAPI.classContacts[i]);
                }
                for (int i = 0; i < reportsDiffCount; i++)
                {
                    NotifyToast(gakujoAPI.reports[i]);
                    notifyAPI.NotifyDiscord(gakujoAPI.reports[i]);
                }
                for (int i = 0; i < quizzesDiffCount; i++)
                {
                    NotifyToast(gakujoAPI.quizzes[i]);
                    notifyAPI.NotifyDiscord(gakujoAPI.quizzes[i]);
                }
                for (int i = 0; i < classSharedFilesDiffCount; i++)
                {
                    NotifyToast(gakujoAPI.classSharedFiles[i]);
                    notifyAPI.NotifyDiscord(gakujoAPI.classSharedFiles[i]);
                }
                for (int i = 0; i < classResultsDiffCount; i++)
                {
                    NotifyToast(gakujoAPI.classResults[i]);
                    notifyAPI.NotifyDiscord(gakujoAPI.classResults[i], true);
                }
                notifyAPI.SetTodoistTask(gakujoAPI.reports);
                notifyAPI.SetTodoistTask(gakujoAPI.quizzes);
                ClassContactsLoadButtonFontIcon.Visibility = Visibility.Visible;
                ClassContactsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                ReportsLoadButtonFontIcon.Visibility = Visibility.Visible;
                ReportsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                QuizzesLoadButtonFontIcon.Visibility = Visibility.Visible;
                QuizzesLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                ClassSharedFilesLoadButtonFontIcon.Visibility = Visibility.Visible;
                ClassSharedFilesLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                ClassResultsLoadButtonFontIcon.Visibility = Visibility.Visible;
                ClassResultsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
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

        private void ClassContactsLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ClassContactsLoadButtonFontIcon.Visibility = Visibility.Collapsed;
            ClassContactsLoadButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassContacts(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    ClassContactsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
                    for (int i = 0; i < diffCount; i++)
                    {
                        NotifyToast(gakujoAPI.classContacts[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.classContacts[i]);
                    }
                    ClassContactsLoadButtonFontIcon.Visibility = Visibility.Visible;
                    ClassContactsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void ClassContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassContactsDataGrid.SelectedIndex != -1)
            {
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
                        Arguments = "/e,/select,\"" + ((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex] + "\"",
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

        private void ReportsLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ReportsLoadButtonFontIcon.Visibility = Visibility.Collapsed;
            ReportsLoadButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetReports(out int diffCount);
                notifyAPI.SetTodoistTask(gakujoAPI.reports);
                Dispatcher.Invoke(() =>
                {
                    ReportsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ReportsDataGrid.ItemsSource = gakujoAPI.reports;
                    for (int i = 0; i < diffCount; i++)
                    {
                        NotifyToast(gakujoAPI.reports[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.reports[i]);
                    }
                    ReportsLoadButtonFontIcon.Visibility = Visibility.Visible;
                    ReportsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
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

        private void QuizzesLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            QuizzesLoadButtonFontIcon.Visibility = Visibility.Collapsed;
            QuizzesLoadButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetQuizzes(out int diffCount);
                notifyAPI.SetTodoistTask(gakujoAPI.quizzes);
                Dispatcher.Invoke(() =>
                {
                    QuizzesDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
                    for (int i = 0; i < diffCount; i++)
                    {
                        NotifyToast(gakujoAPI.quizzes[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.quizzes[i]);
                    }
                    QuizzesLoadButtonFontIcon.Visibility = Visibility.Visible;
                    QuizzesLoadButtonProgressRing.Visibility = Visibility.Collapsed;
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

        private void ClassSharedFilesLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ClassSharedFilesLoadButtonFontIcon.Visibility = Visibility.Collapsed;
            ClassSharedFilesLoadButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassSharedFiles(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    ClassSharedFilesDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
                    for (int i = 0; i < diffCount; i++)
                    {
                        NotifyToast(gakujoAPI.classSharedFiles[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.classSharedFiles[i]);
                    }
                    ClassSharedFilesLoadButtonFontIcon.Visibility = Visibility.Visible;
                    ClassSharedFilesLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void ClassSharedFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSharedFilesDataGrid.SelectedIndex != -1)
            {
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
                        Arguments = "/e,/select,\"" + ((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex] + "\"",
                        UseShellExecute = true
                    });
                }
            }
        }

        #endregion

        #region 成績情報

        private void ClassResultsLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            ClassResultsLoadButtonFontIcon.Visibility = Visibility.Collapsed;
            ClassResultsLoadButtonProgressRing.Visibility = Visibility.Visible;
            Task.Run(() =>
            {
                gakujoAPI.GetClassResults(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    ClassResultsDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.ClassResultDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
                    for (int i = 0; i < diffCount; i++)
                    {
                        NotifyToast(gakujoAPI.classResults[i]);
                        notifyAPI.NotifyDiscord(gakujoAPI.classResults[i], true);
                    }
                    ClassResultsLoadButtonFontIcon.Visibility = Visibility.Visible;
                    ClassResultsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        #endregion

        #region 個人時間割

        private string GetClassTablesCellSubjectsName()
        {
            string suggestText = "";
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

        private void SearchAutoSuggestBox_SuggestionChosen(AutoSuggestBox sender, AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.Text = args.SelectedItem.ToString();
        }

        private void SearchAutoSuggestBox_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == AutoSuggestionBoxTextChangeReason.UserInput)
            {
                List<string> suitableItems = new();
                string[] splitText = sender.Text.Split(" ");
                foreach (ClassTableRow classTableRow in gakujoAPI.classTables)
                {
                    if (splitText.All((key) => { return classTableRow.Monday.SubjectsName.Contains(key); }) && classTableRow.Monday.SubjectsName != "") { suitableItems.Add(classTableRow.Monday.SubjectsName); }
                    if (splitText.All((key) => { return classTableRow.Tuesday.SubjectsName.Contains(key); }) && classTableRow.Tuesday.SubjectsName != "") { suitableItems.Add(classTableRow.Tuesday.SubjectsName); }
                    if (splitText.All((key) => { return classTableRow.Wednesday.SubjectsName.Contains(key); }) && classTableRow.Wednesday.SubjectsName != "") { suitableItems.Add(classTableRow.Wednesday.SubjectsName); }
                    if (splitText.All((key) => { return classTableRow.Thursday.SubjectsName.Contains(key); }) && classTableRow.Thursday.SubjectsName != "") { suitableItems.Add(classTableRow.Thursday.SubjectsName); }
                    if (splitText.All((key) => { return classTableRow.Friday.SubjectsName.Contains(key); }) && classTableRow.Friday.SubjectsName != "") { suitableItems.Add(classTableRow.Friday.SubjectsName); }
                }
                sender.ItemsSource = suitableItems.Distinct();
            }
        }

        #endregion

        #region 通知

        private void ToastNotificationManagerCompat_OnActivated(ToastNotificationActivatedEventArgsCompat e)
        {
            ToastArguments toastArguments = ToastArguments.Parse(e.Argument);
            if (!toastArguments.Contains("Type") || !toastArguments.Contains("Index"))
            {
                return;
            }
            Dispatcher.Invoke(() =>
            {
                ShowInTaskbar = true;
                TaskBarIcon.Visibility = Visibility.Collapsed;
                Visibility = Visibility.Visible;
                Topmost = true;
                SystemCommands.RestoreWindow(this);
                Topmost = false;
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
            new ToastContentBuilder().AddArgument("Type", "Report").AddArgument("Index", gakujoAPI.reports.IndexOf(report)).AddText(report.Title).AddText(report.StartDateTime + " -> " + report.EndDateTime).AddCustomTimeStamp(report.StartDateTime).AddAttributionText(report.Subjects).Show();
        }

        private void NotifyToast(Quiz quiz)
        {
            new ToastContentBuilder().AddArgument("Type", "Quiz").AddArgument("Index", gakujoAPI.quizzes.IndexOf(quiz)).AddText(quiz.Title).AddText(quiz.StartDateTime + " -> " + quiz.EndDateTime).AddCustomTimeStamp(quiz.StartDateTime).AddAttributionText(quiz.Subjects).Show();
        }

        private void NotifyToast(ClassSharedFile classSharedFile)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassSharedFile").AddArgument("Index", gakujoAPI.classSharedFiles.IndexOf(classSharedFile)).AddText(classSharedFile.Title).AddText(classSharedFile.Description).AddCustomTimeStamp(classSharedFile.UpdateDateTime).AddAttributionText(classSharedFile.Subjects).Show();
        }

        private void NotifyToast(ClassResult classResult)
        {
            new ToastContentBuilder().AddArgument("Type", "ClassResult").AddArgument("Index", gakujoAPI.classResults.IndexOf(classResult)).AddText(classResult.Subjects).AddText(classResult.Score + " (" + classResult.Evaluation + ")   " + classResult.GP.ToString("F1")).AddCustomTimeStamp(classResult.ReportDate).AddAttributionText(classResult.ReportDate.ToString()).Show();
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

        private void SchoolYearNumberBox_ValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            settings.SchoolYear = (int)SchoolYearNumberBox.Value;
            SaveJson();
            gakujoAPI.schoolYear = settings.SchoolYear.ToString();
        }

        private void SchoolSemesterComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            settings.SemesterCode = SchoolSemesterComboBox.SelectedIndex < 2 ? 1 : 2;
            SaveJson();
            gakujoAPI.semesterCode = settings.SemesterCode;
        }

        private void UserAgentTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            settings.UserAgent = UserAgentTextBox.Text;
            SaveJson();
            gakujoAPI.userAgent = settings.UserAgent;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            e.Handled = true;
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
