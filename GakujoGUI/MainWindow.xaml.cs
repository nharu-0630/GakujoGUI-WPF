using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Path = System.IO.Path;
using System.Diagnostics;
using System.Threading.Tasks;
using MessageBox = ModernWpf.MessageBox;
using System.ComponentModel;
using System.Windows.Data;
using System.Collections.Generic;

namespace GakujoGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly GakujoAPI gakujoAPI = new();

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            gakujoAPI.SetAccount(UserIdTextBox.Text, PassWordTextBox.Password);
            Task.Run(() =>
            {
                if (!gakujoAPI.Login())
                {
                    Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show("自動ログインに失敗しました．静大IDまたはパスワードが正しくありません．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Error);
                    });
                    return;
                }
                gakujoAPI.GetClassTables();
                gakujoAPI.GetClassResults();
                Dispatcher.Invoke(() =>
                {
                    ClassTablesDataGrid.ItemsSource = gakujoAPI.classTables;
                    MessageBox.Show("自動ログインに成功しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        private void ClassTablesDataGrid_SelectedCellsChanged(object sender, SelectedCellsChangedEventArgs e)
        {
            bool enable = false;
            switch (ClassTablesDataGrid.SelectedCells[0].Column.DisplayIndex)
            {
                case 0:
                    enable = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Monday.SubjectsName != "";
                    break;
                case 1:
                    enable = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Tuesday.SubjectsName != "";
                    break;
                case 2:
                    enable = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Wednesday.SubjectsName != "";
                    break;
                case 3:
                    enable = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Thursday.SubjectsName != "";
                    break;
                case 4:
                    enable = gakujoAPI.classTables[ClassTablesDataGrid.Items.IndexOf(ClassTablesDataGrid.CurrentItem)].Friday.SubjectsName != "";
                    break;
            }


        }

        #region 授業連絡

        private void ClassContactsSearchAutoSuggestBox_QuerySubmitted(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ICollectionView collectionView = (new CollectionViewSource() { Source = gakujoAPI.classContacts }).View;
            collectionView.Filter = new Predicate<object>(item => ((ClassContact)item).Subjects.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Title.Contains(ClassContactsSearchAutoSuggestBox.Text) || ((ClassContact)item).Content.Contains(ClassContactsSearchAutoSuggestBox.Text));
            ClassContactsDataGrid.ItemsSource = collectionView;
        }

        private void ClassContactsSearchAutoSuggestBox_SuggestionChosen(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            ClassContactsSearchAutoSuggestBox.Text = args.SelectedItem.ToString();
        }

        private void ClassContactsSearchAutoSuggestBox_TextChanged(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxTextChangedEventArgs args)
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

        private void ClassContactsLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                if (MessageBox.Show("ログイン状態ではありません．自動ログインしますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
                LoginButton_Click(sender, e);
            }
            Task.Run(() =>
            {
                gakujoAPI.GetClassContacts(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    ClassContactsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
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
                    Process process = new()
                    {
                        StartInfo = new ProcessStartInfo(((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]) { UseShellExecute = true }
                    };
                    process.Start();
                }
            }
        }

        private void ClassContactOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassContactFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex]))
                {
                    Process process = new()
                    {
                        StartInfo = new ProcessStartInfo("explorer.exe")
                        {
                            Arguments = "/e,/select,\"" + ((ClassContact)ClassContactsDataGrid.SelectedItem).Files![ClassContactFilesComboBox.SelectedIndex] + "\"",
                            UseShellExecute = true
                        }
                    };
                    process.Start();
                }
            }
        }

        #endregion

        #region レポート

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                if (MessageBox.Show("ログイン状態ではありません．自動ログインしますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
                LoginButton_Click(sender, e);
            }
            Task.Run(() =>
            {
                gakujoAPI.GetReports(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    ReportsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ReportsDataGrid.ItemsSource = gakujoAPI.reports;
                    MessageBox.Show(diffCount + "件のレポートを更新しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 小テスト

        private void QuizzesButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                if (MessageBox.Show("ログイン状態ではありません．自動ログインしますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
                LoginButton_Click(sender, e);
            }
            Task.Run(() =>
            {
                gakujoAPI.GetQuizzes(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    QuizzesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
                    MessageBox.Show(diffCount + "件の小テストを更新しました．", "GakujoGUI", MessageBoxButton.OK, MessageBoxImage.Information);
                });
            });
        }

        private void QuizzesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 授業共有ファイル


        private void ClassSharedFilesLoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (!gakujoAPI.loginStatus)
            {
                if (MessageBox.Show("ログイン状態ではありません．自動ログインしますか．", "GakujoGUI", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
                {
                    return;
                }
                LoginButton_Click(sender, e);
            }
            Task.Run(() =>
            {
                gakujoAPI.GetClassSharedFiles(out int diffCount);
                Dispatcher.Invoke(() =>
                {
                    ClassSharedFilesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
                    ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
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
                    Process process = new()
                    {
                        StartInfo = new ProcessStartInfo(((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]) { UseShellExecute = true }
                    };
                    process.Start();
                }
            }
        }

        private void ClassSharedOpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSharedFileFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex]))
                {
                    Process process = new();
                    process.StartInfo = new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = "/e,/select,\"" + ((ClassSharedFile)ClassSharedFilesDataGrid.SelectedItem).Files![ClassSharedFileFilesComboBox.SelectedIndex] + "\"",
                        UseShellExecute = true
                    };
                    process.Start();
                }
            }
        }

        #endregion

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            UserIdTextBox.Text = gakujoAPI.account.UserId;
            PassWordTextBox.Password = gakujoAPI.account.PassWord;
            LoginDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.LoginDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassTablesDataGrid.ItemsSource = gakujoAPI.classTables.Take(5);
            ClassContactsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
            ReportsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ReportsDataGrid.ItemsSource = gakujoAPI.reports;
            QuizzesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
            ClassSharedFilesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
        }

        private void ClassSharedFilesSearchAutoSuggestBox_QuerySubmitted(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxQuerySubmittedEventArgs args)
        {
            ICollectionView collectionView = (new CollectionViewSource() { Source = gakujoAPI.classSharedFiles }).View;
            collectionView.Filter = new Predicate<object>(item => ((ClassSharedFile)item).Subjects.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Title.Contains(ClassSharedFilesSearchAutoSuggestBox.Text) || ((ClassSharedFile)item).Description.Contains(ClassSharedFilesSearchAutoSuggestBox.Text));
            ClassSharedFilesDataGrid.ItemsSource = collectionView;
        }

        private void ClassSharedFilesSearchAutoSuggestBox_SuggestionChosen(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxSuggestionChosenEventArgs args)
        {
            ClassSharedFilesSearchAutoSuggestBox.Text = args.SelectedItem.ToString();
        }

        private void ClassSharedFilesSearchAutoSuggestBox_TextChanged(ModernWpf.Controls.AutoSuggestBox sender, ModernWpf.Controls.AutoSuggestBoxTextChangedEventArgs args)
        {
            if (args.Reason == ModernWpf.Controls.AutoSuggestionBoxTextChangeReason.UserInput)
            {
                List<String> suitableItems = new();
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

    }
}
