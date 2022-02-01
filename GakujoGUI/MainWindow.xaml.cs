using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

using Newtonsoft.Json;
using Path = System.IO.Path;
using System.Diagnostics;

namespace GakujoGUI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public List<Report> ReportsList = new List<Report>();
        public List<Quiz> QuizzesList = new List<Quiz>();
        public List<ClassContact> ClassContactsList = new List<ClassContact> { };
        public List<ClassSharedFile> ClassSharedFilesList = new List<ClassSharedFile> { };

        private string downloadPath = Path.Combine(Environment.CurrentDirectory, @"Download\");
        public static string GetJsonPath(string value)
        {
            return Path.Combine(Environment.CurrentDirectory, @"Json\" + value + ".json");
        }


        public MainWindow()
        {
            InitializeComponent();
        }


        private void Login_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ClassContacts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassContacts.SelectedIndex != -1)
            {
                ClassContactContent.Text = ClassContactsList[ClassContacts.SelectedIndex].Content;
                if (ClassContactsList[ClassContacts.SelectedIndex].Files == null)
                {
                    ClassContactFiles.ItemsSource = null;
                }
                else
                {
                    ClassContactFiles.ItemsSource = ClassContactsList[ClassContacts.SelectedIndex].Files!.Select(x => Path.GetFileName(x));
                    ClassContactFiles.SelectedIndex = 0;
                }
            }
        }

        private void OpenClassContactFile_Click(object sender, RoutedEventArgs e)
        {
            if (ClassContactFiles.SelectedIndex != -1)
            {
                if (File.Exists(ClassContactsList[ClassContacts.SelectedIndex].Files![ClassContactFiles.SelectedIndex]))
                {
                    Process process = new Process();
                    process.StartInfo = new ProcessStartInfo(ClassContactsList[ClassContacts.SelectedIndex].Files![ClassContactFiles.SelectedIndex]) { UseShellExecute = true };
                    process.Start();
                }
            }
        }

        private void OpenClassContactFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ClassContactFiles.SelectedIndex != -1)
            {
                if (File.Exists(ClassContactsList[ClassContacts.SelectedIndex].Files![ClassContactFiles.SelectedIndex]))
                {
                    Process process = new Process();
                    process.StartInfo = new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = "/e,/select,\"" + ClassContactsList[ClassContacts.SelectedIndex].Files![ClassContactFiles.SelectedIndex] + "\"",
                        UseShellExecute = true
                    };
                    process.Start();
                }
            }
        }


        private void Reports_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void Quizzes_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ClassSharedFiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ClassSharedFiles.SelectedIndex != -1)
            {
                ClassSharedFileDescription.Text = ClassSharedFilesList[ClassSharedFiles.SelectedIndex].Description;
                if (ClassSharedFilesList[ClassSharedFiles.SelectedIndex].Files == null)
                {
                    ClassSharedFileFiles.ItemsSource = null;
                }
                else
                {
                    ClassSharedFileFiles.ItemsSource = ClassSharedFilesList[ClassSharedFiles.SelectedIndex].Files!.Select(x => Path.GetFileName(x));
                    ClassSharedFileFiles.SelectedIndex = 0;
                }
            }
        }

        private void OpenClassSharedFile_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSharedFiles.SelectedIndex != -1)
            {
                if (File.Exists(ClassSharedFilesList[ClassSharedFiles.SelectedIndex].Files![ClassSharedFileFiles.SelectedIndex]))
                {
                    Process process = new Process();
                    process.StartInfo = new ProcessStartInfo(ClassSharedFilesList[ClassSharedFiles.SelectedIndex].Files![ClassSharedFileFiles.SelectedIndex]) { UseShellExecute = true };
                    process.Start();
                }
            }
        }

        private void OpenClassSharedFolder_Click(object sender, RoutedEventArgs e)
        {
            if (ClassSharedFiles.SelectedIndex != -1)
            {
                if (File.Exists(ClassSharedFilesList[ClassSharedFiles.SelectedIndex].Files![ClassSharedFileFiles.SelectedIndex]))
                {
                    Process process = new Process();
                    process.StartInfo = new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = "/e,/select,\"" + ClassSharedFilesList[ClassSharedFiles.SelectedIndex].Files![ClassSharedFileFiles.SelectedIndex] + "\"",
                        UseShellExecute = true
                    };
                    process.Start();
                }
            }
        }

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

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            Task.Run(() =>
            {
                if (File.Exists(GetJsonPath("ClassContacts")))
                {
                    ClassContactsList = JsonConvert.DeserializeObject<List<ClassContact>>(File.ReadAllText(GetJsonPath("ClassContacts")))!;
                }
                if (File.Exists(GetJsonPath("Reports")))
                {
                    ReportsList = JsonConvert.DeserializeObject<List<Report>>(File.ReadAllText(GetJsonPath("Reports")))!;
                }
                if (File.Exists(GetJsonPath("Quizzes")))
                {
                    QuizzesList = JsonConvert.DeserializeObject<List<Quiz>>(File.ReadAllText(GetJsonPath("Quizzes")))!;
                }
                if (File.Exists(GetJsonPath("ClassSharedFiles")))
                {
                    ClassSharedFilesList = JsonConvert.DeserializeObject<List<ClassSharedFile>>(File.ReadAllText(GetJsonPath("ClassSharedFiles")))!;
                }
                this.Dispatcher.Invoke((Action)(() =>
                {
                    ClassContacts.ItemsSource = ClassContactsList;
                    Reports.ItemsSource = ReportsList;
                    Quizzes.ItemsSource = QuizzesList;
                    ClassSharedFiles.ItemsSource = ClassSharedFilesList;
                }));
            });
        }

        private void ClassTable_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }
    }
}
