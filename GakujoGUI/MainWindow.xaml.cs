using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Path = System.IO.Path;
using System.Diagnostics;
using System.Threading.Tasks;

namespace GakujoGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly GakujoAPI gakujoAPI = new();

        public static string GetJsonPath(string value)
        {
            return Path.Combine(Environment.CurrentDirectory, @"Json\" + value + ".json");
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void LoginButton_Click(object sender, RoutedEventArgs e)
        {
            gakujoAPI.SetAccount(UserIdTextBox.Text, PassWordTextBox.Password);
            Task.Run(() =>
            {
                gakujoAPI.Login();
            });
        }

        private void ClassTableDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #region 授業連絡

        public class ClassContact
        {
            public string? Subjects { get; set; }
            public string? TeacherName { get; set; }
            public string? ContactType { get; set; }
            public string? Title { get; set; }
            public string? Content { get; set; }
            public string[]? Files { get; set; }
            public string? FileLinkRelease { get; set; }
            public string? ReferenceURL { get; set; }
            public string? Severity { get; set; }
            public DateTime TargetDateTime { get; set; }
            public DateTime ContactDateTime { get; set; }
            public string? WebReplyRequest { get; set; }

            public override string ToString()
            {
                return Subjects!.Split(' ')[0] + " " + Title + " " + ContactDateTime.ToShortDateString();
            }

            public override bool Equals(object? obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                ClassContact objClassContact = (ClassContact)obj;
                return Subjects == objClassContact.Subjects && Title == objClassContact.Title && ContactDateTime == objClassContact.ContactDateTime;
            }

            public override int GetHashCode()
            {
                return Subjects!.GetHashCode() ^ Title!.GetHashCode() ^ ContactDateTime!.GetHashCode();
            }
        }

        private void ClassContactsButton_Click(object sender, RoutedEventArgs e)
        {
            gakujoAPI.GetClassContacts(out _);
            ClassContactsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
        }

        private void ClassContactsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassContactsDataGrid.SelectedIndex != -1)
            {
                ClassContactContentTextBox.Text = gakujoAPI.classContacts[ClassContactsDataGrid.SelectedIndex].Content;
                if (gakujoAPI.classContacts[ClassContactsDataGrid.SelectedIndex].Files.Length == 0)
                {
                    ClassContactFilesComboBox.ItemsSource = null;
                    ClassContactFilesStackPanel.Visibility = Visibility.Hidden;
                }
                else
                {
                    ClassContactFilesComboBox.ItemsSource = gakujoAPI.classContacts[ClassContactsDataGrid.SelectedIndex].Files!.Select(x => Path.GetFileName(x));
                    ClassContactFilesComboBox.SelectedIndex = 0;
                    ClassContactFilesStackPanel.Visibility = Visibility.Visible;
                }
            }
        }

        private void OpenClassContactFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassContactFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(gakujoAPI.classContacts[ClassContactsDataGrid.SelectedIndex].Files![ClassContactFilesComboBox.SelectedIndex]))
                {
                    Process process = new()
                    {
                        StartInfo = new ProcessStartInfo(gakujoAPI.classContacts[ClassContactsDataGrid.SelectedIndex].Files![ClassContactFilesComboBox.SelectedIndex]) { UseShellExecute = true }
                    };
                    process.Start();
                }
            }
        }

        private void OpenClassContactFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassContactFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(gakujoAPI.classContacts[ClassContactsDataGrid.SelectedIndex].Files![ClassContactFilesComboBox.SelectedIndex]))
                {
                    Process process = new()
                    {
                        StartInfo = new ProcessStartInfo("explorer.exe")
                        {
                            Arguments = "/e,/select,\"" + gakujoAPI.classContacts[ClassContactsDataGrid.SelectedIndex].Files![ClassContactFilesComboBox.SelectedIndex] + "\"",
                            UseShellExecute = true
                        }
                    };
                    process.Start();
                }
            }
        }

        #endregion

        #region レポート

        public class Report
        {
            public string? Subjects { get; set; }
            public string? Title { get; set; }
            public string? Status { get; set; }
            public DateTime StartDateTime { get; set; }
            public DateTime EndDateTime { get; set; }
            public DateTime SubmittedDateTime { get; set; }
            public string? ImplementationFormat { get; set; }
            public string? Operation { get; set; }
            public string? ReportId { get; set; }
            public string? SchoolYear { get; set; }
            public string? SubjectCode { get; set; }
            public string? ClassCode { get; set; }

            public override string ToString()
            {
                return "[" + Status + "] " + Subjects!.Split(' ')[0] + " " + Title + " -> " + EndDateTime.ToString();
            }

            public string ToShortString()
            {
                return Subjects!.Split(' ')[0] + " " + Title;
            }

            public override bool Equals(object? obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                Report objReport = (Report)obj;
                return SubjectCode == objReport.SubjectCode && ClassCode == objReport.ClassCode && ReportId == objReport.ReportId;
            }

            public override int GetHashCode()
            {
                return SubjectCode!.GetHashCode() ^ ClassCode!.GetHashCode() ^ ReportId!.GetHashCode();
            }
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            gakujoAPI.GetReports(out _);
            ReportsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ReportsDataGrid.ItemsSource = gakujoAPI.reports;
        }

        private void ReportsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 小テスト

        public class Quiz
        {
            public string? Subjects { get; set; }
            public string? Title { get; set; }
            public string? Status { get; set; }
            public DateTime StartDateTime { get; set; }
            public DateTime EndDateTime { get; set; }
            public string? SubmissionStatus { get; set; }
            public string? ImplementationFormat { get; set; }
            public string? Operation { get; set; }
            public string? QuizId { get; set; }
            public string? SchoolYear { get; set; }
            public string? SubjectCode { get; set; }
            public string? ClassCode { get; set; }

            public override string ToString()
            {
                return "[" + SubmissionStatus + "] " + Subjects!.Split(' ')[0] + " " + Title + " -> " + EndDateTime.ToString();
            }

            public string ToShortString()
            {
                return Subjects!.Split(' ')[0] + " " + Title;
            }

            public override bool Equals(object? obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                Quiz objQuiz = (Quiz)obj;
                return SubjectCode == objQuiz.SubjectCode && ClassCode == objQuiz.ClassCode && QuizId == objQuiz.QuizId;
            }

            public override int GetHashCode()
            {
                return SubjectCode!.GetHashCode() ^ ClassCode!.GetHashCode() ^ QuizId!.GetHashCode();
            }
        }

        private void QuizzesButton_Click(object sender, RoutedEventArgs e)
        {
            gakujoAPI.GetQuizzes(out _);
            QuizzesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
        }

        private void QuizzesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        #endregion

        #region 授業共有ファイル

        public class ClassSharedFile
        {
            public string? Subjects { get; set; }
            public string? Title { get; set; }
            public string? Size { get; set; }
            public string[]? Files { get; set; }
            public string? Description { get; set; }
            public string? PublicPeriod { get; set; }
            public DateTime UpdateDateTime { get; set; }

            public override string ToString()
            {
                return Subjects!.Split(' ')[0] + " " + Title + " " + UpdateDateTime.ToShortDateString();
            }

            public override bool Equals(object? obj)
            {
                if (obj == null || GetType() != obj.GetType())
                {
                    return false;
                }
                ClassSharedFile objClassSharedFile = (ClassSharedFile)obj;
                return Subjects == objClassSharedFile.Subjects && Title == objClassSharedFile.Title && UpdateDateTime == objClassSharedFile.UpdateDateTime;
            }

            public override int GetHashCode()
            {
                return Subjects!.GetHashCode() ^ Title!.GetHashCode() ^ UpdateDateTime!.GetHashCode();
            }
        }

        private void ClassSharedFilesButton_Click(object sender, RoutedEventArgs e)
        {
            gakujoAPI.GetClassSharedFiles(out _);
            ClassSharedFilesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
        }

        private void ClassSharedFilesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSharedFilesDataGrid.SelectedIndex != -1)
            {
                ClassSharedFileDescriptionTextBox.Text = gakujoAPI.classSharedFiles[ClassSharedFilesDataGrid.SelectedIndex].Description;
                if (gakujoAPI.classSharedFiles[ClassSharedFilesDataGrid.SelectedIndex].Files.Length == 0)
                {
                    ClassSharedFileFilesComboBox.ItemsSource = null;
                }
                else
                {
                    ClassSharedFileFilesComboBox.ItemsSource = gakujoAPI.classSharedFiles[ClassSharedFilesDataGrid.SelectedIndex].Files!.Select(x => Path.GetFileName(x));
                    ClassSharedFileFilesComboBox.SelectedIndex = 0;
                }
            }
        }

        private void OpenClassSharedFileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSharedFileFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(gakujoAPI.classSharedFiles[ClassSharedFilesDataGrid.SelectedIndex].Files![ClassSharedFileFilesComboBox.SelectedIndex]))
                {
                    Process process = new()
                    {
                        StartInfo = new ProcessStartInfo(gakujoAPI.classSharedFiles[ClassSharedFilesDataGrid.SelectedIndex].Files![ClassSharedFileFilesComboBox.SelectedIndex]) { UseShellExecute = true }
                    };
                    process.Start();
                }
            }
        }

        private void OpenClassSharedFolderButton_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSharedFileFilesComboBox.SelectedIndex != -1)
            {
                if (File.Exists(gakujoAPI.classSharedFiles[ClassSharedFilesDataGrid.SelectedIndex].Files![ClassSharedFileFilesComboBox.SelectedIndex]))
                {
                    Process process = new();
                    process.StartInfo = new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = "/e,/select,\"" + gakujoAPI.classSharedFiles[ClassSharedFilesDataGrid.SelectedIndex].Files![ClassSharedFileFilesComboBox.SelectedIndex] + "\"",
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
            ClassContactsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassContactDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassContactsDataGrid.ItemsSource = gakujoAPI.classContacts;
            ReportsDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ReportDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ReportsDataGrid.ItemsSource = gakujoAPI.reports;
            QuizzesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.QuizDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            QuizzesDataGrid.ItemsSource = gakujoAPI.quizzes;
            ClassSharedFilesDateTimeLabel.Content = "最終更新 : " + gakujoAPI.account.ClassSharedFileDateTime.ToString("yyyy/MM/dd HH:mm:ss");
            ClassSharedFilesDataGrid.ItemsSource = gakujoAPI.classSharedFiles;
        }

    }
}
