using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() => Login());
        }

        private bool Login(bool messageBox = true)
        {
            Dispatcher.Invoke(() =>
            {
                gakujoAPI.SetAccount(UserIdTextBox.Text, PassWordPasswordBox.Password);
                LoginButtonFontIcon.Visibility = Visibility.Collapsed;
                LoginButtonProgressRing.Visibility = Visibility.Visible;
            });
            if (!gakujoAPI.Login())
            {
                Dispatcher.Invoke(() =>
                {
                    LoginButtonFontIcon.Visibility = Visibility.Visible;
                    LoginButtonProgressRing.Visibility = Visibility.Collapsed;
                    if (messageBox)
                    {
                        MessageBox.Show("自動ログインに失敗しました．静大IDまたはパスワードが正しくありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                });
                return false;
            }
            gakujoAPI.GetClassTables();
            gakujoAPI.GetClassResults(out _);
            Dispatcher.Invoke(() =>
            {
                ClassTablesDataGrid.ItemsSource = gakujoAPI.classTables;
                LoginButtonFontIcon.Visibility = Visibility.Visible;
                LoginButtonProgressRing.Visibility = Visibility.Collapsed;
                if (messageBox)
                {
                    MessageBox.Show("自動ログインに成功しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });
            return true;
        }

        #region 授業連絡

        private void ClassContactsSearchAutoSuggestBox_QuerySubmitted(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classContacts }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassContact)item).Subjects.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Title.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Content.Contains(ClassContactsSearchAutoSuggestBox.Text));
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
                    ClassContactsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
                    ClassContactsLoadButtonFontIcon.Visibility = Visibility.Visible;
                    ClassContactsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                    MessageBox.Show(diffCount + "件の授業連絡を新しく取得しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void ClassContactOpenFileButton_Click(object sender, RoutedEventArgs e)
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

        private void ClassContactOpenFolderButton_Click(object sender, RoutedEventArgs e)
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
                    ReportsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ReportsDataGrid.ItemsSource = gakujoAPI.reports;
                    ReportsLoadButtonFontIcon.Visibility = Visibility.Visible;
                    ReportsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                    MessageBox.Show(diffCount + "件のレポートを新しく取得しました．．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 小テスト

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
                    QuizzesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
                    QuizzesLoadButtonFontIcon.Visibility = Visibility.Visible;
                    QuizzesLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                    MessageBox.Show(diffCount + "件の小テストを新しく取得しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        private void QuizzesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 授業共有ファイル

        private void ClassSharedFilesSearchAutoSuggestBox_QuerySubmitted(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classSharedFiles }.View;
            collectionView.Filter = new Predicate<object>(item => ((ClassSharedFile)item).Subjects.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Title.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Description.Contains(ClassSharedFilesSearchAutoSuggestBox.Text));
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
                    ClassSharedFilesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
                    ClassSharedFilesLoadButtonFontIcon.Visibility = Visibility.Visible;
                    ClassSharedFilesLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                    MessageBox.Show(diffCount + "件の授業共有ファイルを新しく取得しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
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

        private void ClassSharedOpenFileButton_Click(object sender, RoutedEventArgs e)
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

        private void ClassSharedOpenFolderButton_Click(object sender, RoutedEventArgs e)
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
                    ClassResultsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassResultDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
                    ClassResultsLoadButtonFontIcon.Visibility = Visibility.Visible;
                    ClassResultsLoadButtonProgressRing.Visibility = Visibility.Collapsed;
                    MessageBox.Show(diffCount + "件の成績情報を更新しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        #endregion

        #region 個人時間割

        private void ClassTablesCell_ClassContactButtonClick(object sender, RoutedEventArgs e)
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
            if (suggestText != "")
            {
                ClassContactsSearchAutoSuggestBox.Text = suggestText;
                ICollectionView collectionView = new CollectionViewSource() { Source = gakujoAPI.classContacts }.View;
                collectionView.Filter = new Predicate<object>(item => ((ClassContact)item).Subjects.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Title.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Content.Contains(ClassContactsSearchAutoSuggestBox.Text));
                ClassContactsDataGrid.ItemsSource = collectionView;
                e.Handled = true;
                ClassContactsTabItem.IsSelected = true;
            }
        }

        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UserIdTextBox.Text = gakujoAPI.account.UserId;
            PassWordPasswordBox.Password = gakujoAPI.account.PassWord;
            LoginDateTimeLabel.Content = "最終更新 " + gakujoAPI.account.LoginDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            TodoistTokenPasswordBox.Password = notifyAPI.token.TodoistToken;
            DiscordChannelTextBox.Text = notifyAPI.token.DiscordChannel.ToString();
            DiscordTokenPasswordBox.Password = notifyAPI.token.DiscordToken;
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
        }

        private void SearchAutoSuggestBox_SuggestionChosen(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            sender.Text = args.SelectedItem.ToString();
        }

        private void SearchAutoSuggestBox_TextChanged(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == ModernWpf.Controls.AutoSuggestionBoxTextChangeReason.UserInput)
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
            if (!gakujoAPI.loginStatus)
            {
                MessageBox.Show("ログイン状態ではありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            Task.Run(() =>
            {
                gakujoAPI.GetClassContacts(out int classContactsDiffCount);
                gakujoAPI.GetReports(out int reportsDiffCount);
                gakujoAPI.GetQuizzes(out int quizzesDiffCount);
                gakujoAPI.GetClassSharedFiles(out int classSharedFilesDiffCount);
                gakujoAPI.GetClassResults(out int classResultsDiffCount);
                Dispatcher.Invoke(() =>
                {
                    ClassResultsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassResultDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
                    ClassContactsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
                    ReportsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ReportsDataGrid.ItemsSource = gakujoAPI.reports;
                    QuizzesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
                    ClassSharedFilesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
                    ClassResultsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassResultDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassResultsDataGrid.ItemsSource = gakujoAPI.classResults;
                    MessageBox.Show(classContactsDiffCount + "件の授業連絡を新しく取得しました．\n" + reportsDiffCount + "件のレポートを新しく取得しました．\n" + quizzesDiffCount + "件の小テストを新しく取得しました．\n" + classSharedFilesDiffCount + "件の授業共有ファイルを新しく取得しました．\n" + classResultsDiffCount + "件の成績情報を更新しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        private void CloseMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Close();
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

        private void TaskBarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
        {
            ShowInTaskbar = true;
            TaskBarIcon.Visibility = Visibility.Collapsed;
            Visibility = Visibility.Visible;
            Topmost = true;
            SystemCommands.RestoreWindow(this);
            Topmost = false;
        }

        private void TokenSaveButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    notifyAPI.SetToken(TodoistTokenPasswordBox.Password, DiscordChannelTextBox.Text, DiscordTokenPasswordBox.Password);
                    TokenSaveButtonFontIcon.Visibility = Visibility.Collapsed;
                    TokenSaveButtonProgressRing.Visibility = Visibility.Visible;
                });
                notifyAPI.Login();
                notifyAPI.SetTodoistTask(gakujoAPI.reports);
                notifyAPI.SetTodoistTask(gakujoAPI.quizzes);
                Dispatcher.Invoke(() =>
                {
                    TokenSaveButtonFontIcon.Visibility = Visibility.Visible;
                    TokenSaveButtonProgressRing.Visibility = Visibility.Collapsed;
                });
            });
        }

        private void ClassTablesCell_ReportButtonClick(object sender, RoutedEventArgs e)
        {

        }

        private void ClassTablesCell_QuizButtonClick(object sender, RoutedEventArgs e)
        {

        }
    }
}
