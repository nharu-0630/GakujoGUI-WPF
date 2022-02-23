using HtmlAgilityPack;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;
using System.Web;

namespace GakujoGUI
{
    internal class GakujoAPI
    {
        public Account account = new();
        public List<Report> reports = new();
        public List<Quiz> quizzes = new();
        public List<ClassContact> classContacts = new() { };
        public List<ClassSharedFile> classSharedFiles = new() { };
        public List<ClassResult> classResults = new() { };
        public List<ClassResultCredit> classResultsCredit
        {
            get
            {
                List<ClassResultCredit> classResultCredits = new();
                classResultCredits.Add(new ClassResultCredit() { Evaluation = "秀", SchoolCredit = classResults.FindAll(classResult => classResult.Evaluation == "秀").Select(classResult => classResult.SchoolCredit).Sum() });
                classResultCredits.Add(new ClassResultCredit() { Evaluation = "優", SchoolCredit = classResults.FindAll(classResult => classResult.Evaluation == "優").Select(classResult => classResult.SchoolCredit).Sum() });
                classResultCredits.Add(new ClassResultCredit() { Evaluation = "良", SchoolCredit = classResults.FindAll(classResult => classResult.Evaluation == "良").Select(classResult => classResult.SchoolCredit).Sum() });
                classResultCredits.Add(new ClassResultCredit() { Evaluation = "可", SchoolCredit = classResults.FindAll(classResult => classResult.Evaluation == "可").Select(classResult => classResult.SchoolCredit).Sum() });
                classResultCredits.Add(new ClassResultCredit() { Evaluation = "合", SchoolCredit = classResults.FindAll(classResult => classResult.Evaluation == "合").Select(classResult => classResult.SchoolCredit).Sum() });
                classResultCredits.Add(new ClassResultCredit() { Evaluation = "認定", SchoolCredit = classResults.FindAll(classResult => classResult.Evaluation == "認定").Select(classResult => classResult.SchoolCredit).Sum() });
                classResultCredits.Add(new ClassResultCredit() { Evaluation = "合計", SchoolCredit = classResults.Select(classResult => classResult.SchoolCredit).Sum() });
                return classResultCredits;
            }
        }
        public double classResultsGPA
        {
            get { return 1.0 * classResults.FindAll(classResult => classResult.Score != 0).Select(classResult => classResult.GP * classResult.SchoolCredit).Sum() / classResults.FindAll(classResult => classResult.Score != 0).Select(classResult => classResult.SchoolCredit).Sum(); }
        }
        public ClassTableRow[]? classTables = null;

        private CookieContainer cookieContainer = new();
        private HttpClientHandler httpClientHandler = new();
        private HttpClient httpClient = new();
        private HttpRequestMessage httpRequestMessage = new();
        private HttpResponseMessage httpResponse = new();
        // private readonly HtmlDocument htmlDocument = new();

        public bool loginStatus = false;

        private readonly string cookiesPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, "Cookies");
        private readonly string downloadPath = Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, @"Download\");

        private readonly string schoolYear = "";
        private readonly int semesterCode;
        private string SchoolYearSemesterCodeSuffix
        {
            get { return $"_{schoolYear}_{(semesterCode < 2 ? 1 : 2)}"; }
        }
        private string ReportDateStart
        {
            get { return $"{schoolYear}/0{(semesterCode < 2 ? 3 : 8)}/01"; }
        }
        private readonly string userAgent = "";

        private static string GetJsonPath(string value)
        {
            return Path.Combine(Path.GetDirectoryName(Environment.ProcessPath!)!, @$"Json\{value}.json");
        }

        public GakujoAPI(string schoolYear, int semesterCode, string userAgent)
        {
            this.schoolYear = schoolYear;
            this.semesterCode = semesterCode;
            this.userAgent = userAgent;
            LoadCookies();
            LoadJson();
        }

        public void SetAccount(string userId, string passWord)
        {
            account.UserId = userId;
            account.PassWord = passWord;
            SaveJson();
        }

        private void SaveCookies()
        {
            using Stream stream = File.Create(cookiesPath);
            BinaryFormatter binaryFormatter = new();
#pragma warning disable SYSLIB0011 // 型またはメンバーが旧型式です
            binaryFormatter.Serialize(stream, cookieContainer);
#pragma warning restore SYSLIB0011 // 型またはメンバーが旧型式です
        }

        private bool LoadCookies()
        {
            if (File.Exists(cookiesPath))
            {
                using (Stream stream = File.Open(cookiesPath, FileMode.Open))
                {
                    BinaryFormatter binaryFormatter = new();
#pragma warning disable SYSLIB0011 // 型またはメンバーが旧型式です
                    cookieContainer = (CookieContainer)binaryFormatter.Deserialize(stream);
#pragma warning restore SYSLIB0011 // 型またはメンバーが旧型式です
                }
                httpClientHandler = new HttpClientHandler
                {
                    AutomaticDecompression = ~DecompressionMethods.None,
                    CookieContainer = cookieContainer
                };
                httpClient = new HttpClient(httpClientHandler);
                return CheckConnection();
            }
            cookieContainer = new CookieContainer();
            return false;
        }

        public bool LoadJson()
        {
            if (File.Exists(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix)))
            {
                reports = JsonConvert.DeserializeObject<List<Report>>(File.ReadAllText(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix)))!;
            }
            if (File.Exists(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix)))
            {
                quizzes = JsonConvert.DeserializeObject<List<Quiz>>(File.ReadAllText(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix)))!;
            }
            if (File.Exists(GetJsonPath($"ClassContacts" + SchoolYearSemesterCodeSuffix)))
            {
                classContacts = JsonConvert.DeserializeObject<List<ClassContact>>(File.ReadAllText(GetJsonPath("ClassContacts" + SchoolYearSemesterCodeSuffix)))!;
            }
            if (File.Exists(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix)))
            {
                classSharedFiles = JsonConvert.DeserializeObject<List<ClassSharedFile>>(File.ReadAllText(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix)))!;
            }
            if (File.Exists(GetJsonPath("ClassResults")))
            {
                classResults = JsonConvert.DeserializeObject<List<ClassResult>>(File.ReadAllText(GetJsonPath("ClassResults")))!;
            }
            if (File.Exists(GetJsonPath("ClassTables")))
            {
                classTables = JsonConvert.DeserializeObject<ClassTableRow[]>(File.ReadAllText(GetJsonPath("ClassTables")))!;
            }
            ApplyReportsClassTables();
            ApplyQuizzesClassTables();
            if (File.Exists(GetJsonPath("Account")))
            {
                account = JsonConvert.DeserializeObject<Account>(File.ReadAllText(GetJsonPath("Account")))!;
                return true;
            }
            return false;
        }

        private void SaveJson()
        {
            try { File.WriteAllText(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(reports, Formatting.Indented)); }
            catch { }
            try { File.WriteAllText(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(quizzes, Formatting.Indented)); }
            catch { }
            try
            { File.WriteAllText(GetJsonPath("ClassContacts" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(classContacts, Formatting.Indented)); }
            catch { }
            try
            { File.WriteAllText(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(classSharedFiles, Formatting.Indented)); }
            catch { }
            try
            { File.WriteAllText(GetJsonPath("ClassResults"), JsonConvert.SerializeObject(classResults, Formatting.Indented)); }
            catch { }
            try
            { File.WriteAllText(GetJsonPath("ClassTables"), JsonConvert.SerializeObject(classTables, Formatting.Indented)); }
            catch
            { }
            try
            { File.WriteAllText(GetJsonPath("Account"), JsonConvert.SerializeObject(account, Formatting.Indented)); }
            catch
            { }
        }

        public bool Login()
        {
            if (3 <= DateTime.Now.Hour && DateTime.Now.Hour < 5)
            {
                return false;
            }
            cookieContainer = new CookieContainer();
            httpClientHandler = new HttpClientHandler
            {
                AutomaticDecompression = ~DecompressionMethods.None,
                CookieContainer = cookieContainer
            };
            httpClient = new HttpClient(httpClientHandler);
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/portal/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/login/preLogin/preLogin");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("mistakeChecker=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("selectLocale=ja&mistakeChecker=0&EXCLUDE_SET=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://idp.shizuoka.ac.jp/idp/profile/SAML2/Redirect/SSO?execution=e1s1");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"j_username={account.UserId}&j_password={account.PassWord}&_eventId_proceed=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            if (HttpUtility.HtmlDecode(httpResponse.Content.ReadAsStringAsync().Result).Contains("ユーザ名またはパスワードが正しくありません。"))
            {
                return false;
            }
            else
            {
                HtmlDocument htmlDocument = new();
                htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
                string relayState = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[1]").Attributes["value"].Value;
                relayState = relayState.Replace("&#x3a;", ":");
                string SAMLResponse = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[2]").Attributes["value"].Value;
                relayState = Uri.EscapeDataString(relayState);
                SAMLResponse = Uri.EscapeDataString(SAMLResponse);
                httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent($"RelayState={relayState}&SAMLResponse={SAMLResponse}");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent("EXCLUDE_SET=");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
                account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
                account.StudentName = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/div/div/div/ul[2]/li/a/span/span").InnerText;
                account.StudentName = account.StudentName[0..^2];
            }
            account.LoginDateTime = DateTime.Now;
            SaveJson();
            SaveCookies();
            loginStatus = true;
            return true;
        }

        public void GetReports(out List<Report> diffReports)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&headTitle=授業サポート&menuCode=A02&nextPath=/report/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/search");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&reportId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            if (htmlDocument.GetElementbyId("searchList") == null)
            {
                diffReports = new();
                return;
            }
            diffReports = new(reports);
            reports.Clear();
            int limitCount = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            for (int i = 0; i < limitCount; i++)
            {
                Report report = new();
                report.Subjects = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                report.Subjects = Regex.Replace(report.Subjects, @"\s+", " ");
                report.Title = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText.Trim();
                report.ReportId = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                report.SchoolYear = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[3].Replace("'", "").Trim();
                report.SubjectCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[4].Replace("'", "").Trim();
                report.ClassCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[5].Replace("'", "").Replace(");", "").Trim();
                report.Status = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                report.StartDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[0]);
                report.EndDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[1]);
                if (htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText.Trim() != "")
                {
                    report.SubmittedDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText.Trim());
                }
                report.ImplementationFormat = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim();
                report.Operation = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[6].InnerText.Trim();
                reports.Add(report);
            }
            diffReports = reports.Except(diffReports).ToList();
            account.ReportDateTime = DateTime.Now;
            ApplyReportsClassTables();
            SaveJson();
            SaveCookies();
        }

        private void ApplyReportsClassTables()
        {
            if (classTables == null) { return; }
            foreach (ClassTableRow classTableRow in classTables)
            {
                classTableRow.Monday.ReportCount = 0;
                classTableRow.Tuesday.ReportCount = 0;
                classTableRow.Wednesday.ReportCount = 0;
                classTableRow.Thursday.ReportCount = 0;
                classTableRow.Friday.ReportCount = 0;
            }
            foreach (Report report in reports)
            {
                //if (report.Status == "受付中" && report.SubmittedDateTime == new DateTime())
                //{
                foreach (ClassTableRow classTableRow in classTables)
                {
                    if (report.Subjects.Contains($"{classTableRow.Monday.SubjectsName}（{classTableRow.Monday.ClassName}）")) { classTableRow.Monday.ReportCount++; }
                    if (report.Subjects.Contains($"{classTableRow.Tuesday.SubjectsName}（{classTableRow.Tuesday.ClassName}）")) { classTableRow.Tuesday.ReportCount++; }
                    if (report.Subjects.Contains($"{classTableRow.Wednesday.SubjectsName}（{classTableRow.Wednesday.ClassName}）")) { classTableRow.Wednesday.ReportCount++; }
                    if (report.Subjects.Contains($"{classTableRow.Thursday.SubjectsName}（{classTableRow.Thursday.ClassName}）")) { classTableRow.Thursday.ReportCount++; }
                    if (report.Subjects.Contains($"{classTableRow.Friday.SubjectsName}（{classTableRow.Friday.ClassName}）")) { classTableRow.Friday.ReportCount++; }
                }
                //}
            }
        }

        public void GetQuizzes(out List<Quiz> diffQuizzes)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&headTitle=小テスト一覧&menuCode=A03&nextPath=/test/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/search");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&testId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            if (htmlDocument.GetElementbyId("searchList") == null)
            {
                diffQuizzes = new();
                return;
            }
            diffQuizzes = new(quizzes);
            quizzes.Clear();
            int limitCount = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            for (int i = 0; i < limitCount; i++)
            {
                Quiz quiz = new();
                quiz.Subjects = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                quiz.Subjects = Regex.Replace(quiz.Subjects, @"\s+", " ");
                quiz.Title = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText.Trim();
                quiz.QuizId = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                quiz.SchoolYear = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[3].Replace("'", "").Trim();
                quiz.SubjectCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[4].Replace("'", "").Trim();
                quiz.ClassCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[5].Replace("'", "").Replace(");", "").Trim();
                quiz.Status = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                quiz.StartDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[0]);
                quiz.EndDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[1]);
                quiz.SubmissionStatus = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText.Trim();
                quiz.ImplementationFormat = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim();
                quiz.Operation = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[6].InnerText.Trim();
                quizzes.Add(quiz);
            }
            diffQuizzes = quizzes.Except(diffQuizzes).ToList();
            account.QuizDateTime = DateTime.Now;
            ApplyQuizzesClassTables();
            SaveJson();
            SaveCookies();
        }

        private void ApplyQuizzesClassTables()
        {
            if (classTables == null) { return; }
            foreach (ClassTableRow classTableRow in classTables)
            {
                if (classTableRow == null) { continue; }
                classTableRow.Monday.QuizCount = 0;
                classTableRow.Tuesday.QuizCount = 0;
                classTableRow.Wednesday.QuizCount = 0;
                classTableRow.Thursday.QuizCount = 0;
                classTableRow.Friday.QuizCount = 0;
            }
            foreach (Quiz quiz in quizzes)
            {
                //if (quiz.Status == "受付中" && quiz.SubmissionStatus == "未提出")
                //{
                foreach (ClassTableRow classTableRow in classTables)
                {
                    if (classTableRow == null) { continue; }
                    if (quiz.Subjects.Contains($"{classTableRow.Monday.SubjectsName}（{classTableRow.Monday.ClassName}）")) { classTableRow.Monday.QuizCount++; }
                    if (quiz.Subjects.Contains($"{classTableRow.Tuesday.SubjectsName}（{classTableRow.Tuesday.ClassName}）")) { classTableRow.Tuesday.QuizCount++; }
                    if (quiz.Subjects.Contains($"{classTableRow.Wednesday.SubjectsName}（{classTableRow.Wednesday.ClassName}）")) { classTableRow.Wednesday.QuizCount++; }
                    if (quiz.Subjects.Contains($"{classTableRow.Thursday.SubjectsName}（{classTableRow.Thursday.ClassName}）")) { classTableRow.Thursday.QuizCount++; }
                    if (quiz.Subjects.Contains($"{classTableRow.Friday.SubjectsName}（{classTableRow.Friday.ClassName}）")) { classTableRow.Friday.QuizCount++; }
                }
                //}
            }
        }

        public void GetClassContacts(out int diffCount, int maxCount = 10)
        {
            ClassContact? lastClassContact = classContacts.Count > 0 ? classContacts[0] : null;
            List<ClassContact> diffClassContacts = new() { };
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&headTitle=授業連絡一覧&menuCode=A01&nextPath=/classcontact/classContactList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={ReportDateStart}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            if (htmlDocument.GetElementbyId("tbl_A01_01") == null)
            {
                diffCount = 0;
                return;
            }
            int limitCount = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr").Count;
            for (int i = 0; i < limitCount; i++)
            {
                ClassContact classContact = new();
                classContact.Subjects = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                classContact.Subjects = Regex.Replace(classContact.Subjects, @"\s+", " ");
                classContact.TeacherName = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                classContact.Title = HttpUtility.HtmlDecode(htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].SelectSingleNode("a").InnerText).Trim();
                if (htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim() != "")
                {
                    classContact.TargetDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim());
                }
                classContact.ContactDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[6].InnerText.Trim());
                if (classContact.Equals(lastClassContact))
                {
                    break;
                }
                diffClassContacts.Add(classContact);
            }
            diffCount = diffClassContacts.Count;
            for (int i = 0; i < diffCount; i++)
            {
                classContacts.Insert(i, diffClassContacts[i]);
            }
            maxCount = maxCount == -1 ? diffCount : maxCount;
            for (int i = 0; i < Math.Min(diffCount, maxCount); i++)
            {
                GetClassContact(i);
            }
            account.ClassContactDateTime = DateTime.Now;
            SaveJson();
            SaveCookies();
        }

        public void GetClassContact(int indexCount)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&headTitle=授業連絡一覧&menuCode=A01&nextPath=/classcontact/classContactList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={ReportDateStart}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/goDetail/" + indexCount);
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            string content = $"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={schoolYear}/01/01&reportDateEnd=&requireResponse=&studentCode=&studentName=&tbl_A01_01_length=-1&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A01_01&_screenInfoDisp=true&_scrollTop=0";
            httpRequestMessage.Content = new StringContent(content);
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            classContacts[indexCount].ContactType = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[0].SelectSingleNode("td").InnerText;
            classContacts[indexCount].Content = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[2].SelectSingleNode("td").InnerText);
            classContacts[indexCount].Content = Regex.Replace(classContacts[indexCount].Content, "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline);
            classContacts[indexCount].FileLinkRelease = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            classContacts[indexCount].ReferenceURL = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            classContacts[indexCount].Severity = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[6].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            classContacts[indexCount].WebReplyRequest = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[8].SelectSingleNode("td").InnerText;
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div") != null)
            {
                classContacts[indexCount].Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div")[i];
                    string prefix = htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("fileDownLoad('", "").Replace("'", "");
                    string no = htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim();
                    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                    Stream stream = httpResponse.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    classContacts[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            SaveJson();
            SaveCookies();
        }

        public void GetClassSharedFiles(out int diffCount, int maxCount = 10)
        {
            ClassSharedFile? lastClassSharedFile = (classSharedFiles.Count > 0) ? classSharedFiles[0] : null;
            List<ClassSharedFile> diffClassSharedFiles = new() { };
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&headTitle=授業共有ファイル&menuCode=A08&nextPath=/classfile/classFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            if (htmlDocument.GetElementbyId("tbl_classFile") == null)
            {
                diffCount = 0;
                return;
            }
            int limitCount = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr").Count;
            for (int i = 0; i < limitCount; i++)
            {
                ClassSharedFile classSharedFile = new();
                classSharedFile.Subjects = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                classSharedFile.Subjects = Regex.Replace(classSharedFile.Subjects, @"\s+", " ");
                classSharedFile.Title = HttpUtility.HtmlDecode(htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].SelectSingleNode("a").InnerText).Trim();
                classSharedFile.Size = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText;
                classSharedFile.UpdateDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText);
                if (classSharedFile.Equals(lastClassSharedFile))
                {
                    break;
                }
                diffClassSharedFiles.Add(classSharedFile);
            }
            diffCount = diffClassSharedFiles.Count;
            for (int i = 0; i < diffCount; i++)
            {
                classSharedFiles.Insert(i, diffClassSharedFiles[i]);
            }
            maxCount = maxCount == -1 ? diffCount : maxCount;
            for (int i = 0; i < Math.Min(diffCount, maxCount); i++)
            {
                GetClassSharedFile(i);
            }
            account.ClassSharedFileDateTime = DateTime.Now;
            SaveJson();
            SaveCookies();
        }

        public void GetClassSharedFile(int indexCount)
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&headTitle=授業共有ファイル&menuCode=A08&nextPath=/classfile/classFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/showClassFileDetail?EXCLUDE_SET=&org.apache.struts.taglib.html.TOKEN=" + $"{account.ApacheToken}&selectIndex={indexCount}&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_searchConditionDisp.accordionSearchCondition=false&_scrollTop=0");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            classSharedFiles[indexCount].Description = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[2].SelectSingleNode("td").InnerText);
            classSharedFiles[indexCount].PublicPeriod = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[3].SelectSingleNode("td").InnerText.Replace("\n", "").Replace("\t", "").Replace("&nbsp;", "");
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div") != null)
            {
                classSharedFiles[indexCount].Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div")[i];
                    string prefix = htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("fileDownLoad('", "").Replace("'", "");
                    string no = htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim();
                    httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
                    Stream stream = httpResponse.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    classSharedFiles[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            SaveJson();
            SaveCookies();
        }

        private bool CheckConnection()
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={account.ApacheToken}&headTitle=ホーム&menuCode=Z07&nextPath=/home/home/initialize&_screenIdentifier=&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input") == null)
            {
                cookieContainer = new CookieContainer();
                httpClientHandler = new HttpClientHandler
                {
                    AutomaticDecompression = ~DecompressionMethods.None,
                    CookieContainer = cookieContainer
                };
                httpClient = new HttpClient(httpClientHandler);
                return false;
            }
            account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form[1]/div/input").Attributes["value"].Value;
            SaveJson();
            SaveCookies();
            loginStatus = true;
            return true;
        }

        private bool SetAcademicSystem()
        {
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/preLogin.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/systemCooperationLink/initializeShibboleth?renkeiType=kyoumu");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Headers.TryAddWithoutValidation("Origin", "https://gakujo.shizuoka.ac.jp");
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/sso/loginStudent.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("loginID=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            if (httpResponse.Content.ReadAsStringAsync().Result.Contains("Since your browser does not support JavaScript,"))
            {
                HtmlDocument htmlDocument = new();
                htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
                string relayState = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[1]").Attributes["value"].Value;
                relayState = relayState.Replace("&#x3a;", ":");
                string SAMLResponse = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[2]").Attributes["value"].Value;
                relayState = Uri.EscapeDataString(relayState);
                SAMLResponse = Uri.EscapeDataString(SAMLResponse);
                httpRequestMessage = new HttpRequestMessage(new HttpMethod("POST"), "https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent($"RelayState={relayState}&SAMLResponse={SAMLResponse}");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            }
            SaveJson();
            SaveCookies();
            return true;
        }

        public void GetClassResults(out List<ClassResult> diffClassResults)
        {
            SetAcademicSystem();
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/seisekiSearchStudentInit.do?mainMenuCode=008&parentMenuCode=007");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/table[5]/tr/td/table") == null)
            {
                diffClassResults = new();
                return;
            }
            diffClassResults = new(classResults);
            classResults.Clear();
            for (int i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[5]/tr/td/table").SelectNodes("tr").Count; i++)
            {
                HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[5]/tr/td/table").SelectNodes("tr")[i];
                ClassResult classResult = new();
                classResult.Subjects = htmlNode.SelectNodes("td")[0].InnerText.Trim();
                classResult.TeacherName = htmlNode.SelectNodes("td")[1].InnerText.Trim();
                classResult.SubjectsSection = htmlNode.SelectNodes("td")[2].InnerText.Trim();
                classResult.SelectionSection = htmlNode.SelectNodes("td")[3].InnerText.Trim();
                classResult.SchoolCredit = int.Parse(htmlNode.SelectNodes("td")[4].InnerText.Trim());
                classResult.Evaluation = htmlNode.SelectNodes("td")[5].InnerText.Trim();
                if (htmlNode.SelectNodes("td")[6].InnerText.Trim() != "")
                {
                    classResult.Score = double.Parse(htmlNode.SelectNodes("td")[6].InnerText.Trim());
                }
                if (htmlNode.SelectNodes("td")[7].InnerText.Trim() != "")
                {
                    classResult.GP = double.Parse(htmlNode.SelectNodes("td")[7].InnerText.Trim());
                }
                classResult.AcquisitionYear = htmlNode.SelectNodes("td")[8].InnerText.Trim();
                classResult.ReportDate = DateTime.Parse(htmlNode.SelectNodes("td")[9].InnerText.Trim());
                classResults.Add(classResult);
            }
            diffClassResults = classResults.Except(diffClassResults).ToList();
            account.ClassResultDateTime = DateTime.Now;
            SaveJson();
            SaveCookies();
        }

        public void GetClassTables()
        {
            SetAcademicSystem();
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=005&parentMenuCode=004");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/table[4]") == null)
            {
                return;
            }
            classTables = new ClassTableRow[7];
            for (int i = 0; i < 7; i++)
            {
                classTables[i] = new ClassTableRow();
                for (int j = 0; j < 5; j++)
                {
                    ClassTableCell classTableCell = new();
                    if (htmlDocument.DocumentNode.SelectNodes($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table/tr[2]/td/a") != null)
                    {
                        string detailKamokuCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table/tr[2]/td/a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                        string detailClassCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table/tr[2]/td/a").Attributes["onclick"].Value.Split(',')[2].Replace("'", "").Trim();
                        classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                        string classRoom = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table/tr[2]/td").InnerHtml;
                        classTableCell.ClassRoom = classRoom[(classRoom.LastIndexOf("<br>") + 4)..].Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ').Replace("&nbsp;", "");
                    }
                    else if (htmlDocument.DocumentNode.SelectNodes($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[1]/tr/td/a") != null && (semesterCode == 0 || semesterCode == 2))
                    {
                        string detailKamokuCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[1]/tr/td/a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                        string detailClassCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[1]/tr/td/a").Attributes["onclick"].Value.Split(',')[2].Replace("'", "").Trim();
                        classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                        string classRoom = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[1]/tr/td/a").InnerHtml;
                        classTableCell.ClassRoom = classRoom[(classRoom.LastIndexOf("<br>") + 4)..].Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ').Replace("&nbsp;", "");
                    }
                    else if (htmlDocument.DocumentNode.SelectNodes($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[2]/tr/td/a") != null && (semesterCode == 1 || semesterCode == 3))
                    {
                        string detailKamokuCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[2]/tr/td/a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                        string detailClassCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[2]/tr/td/a").Attributes["onclick"].Value.Split(',')[2].Replace("'", "").Trim();
                        classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                        string classRoom = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[2]/tr/td/a").InnerHtml;
                        classTableCell.ClassRoom = classRoom[(classRoom.LastIndexOf("<br>") + 4)..].Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ').Replace("&nbsp;", "");
                    }
                    else
                    {
                        continue;
                    }

                    switch (j)
                    {
                        case 0:
                            classTables[i].Monday = classTableCell;
                            break;
                        case 1:
                            classTables[i].Tuesday = classTableCell;
                            break;
                        case 2:
                            classTables[i].Wednesday = classTableCell;
                            break;
                        case 3:
                            classTables[i].Thursday = classTableCell;
                            break;
                        case 4:
                            classTables[i].Friday = classTableCell;
                            break;
                    }
                }
            }
            ApplyReportsClassTables();
            ApplyQuizzesClassTables();
            SaveJson();
            SaveCookies();
        }

        private ClassTableCell GetClassTableCell(string detailKamokuCode, string detailClassCode)
        {
            ClassTableCell classTableCell = new();
            httpRequestMessage = new HttpRequestMessage(new HttpMethod("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/detailKamoku.do?detailKamokuCode=" + $"{detailKamokuCode}&detailClassCode={detailClassCode}&gamen=jikanwari");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponse = httpClient.SendAsync(httpRequestMessage).Result;
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponse.Content.ReadAsStringAsync().Result);
            classTableCell.SubjectsName = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目名\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.SubjectsId = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目番号\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.ClassName = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"クラス名\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.TeacherName = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"担当教員\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.SubjectsSection = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目区分\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.SelectionSection = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"必修選択区分\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.SchoolCredit = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"単位数\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Replace("単位", ""));
            return classTableCell;
        }
    }

    public class Account
    {
        public string UserId { get; set; } = "";
        public string PassWord { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string StudentCode { get; set; } = "";
        public string ApacheToken { get; set; } = "";

        public DateTime LoginDateTime { get; set; }
        public DateTime ReportDateTime { get; set; }
        public DateTime QuizDateTime { get; set; }
        public DateTime ClassContactDateTime { get; set; }
        public DateTime SchoolContactDateTime { get; set; }
        public DateTime ClassSharedFileDateTime { get; set; }
        public DateTime SchoolSharedFileDateTime { get; set; }
        public DateTime ClassResultDateTime { get; set; }
    }

    public class Report
    {
        public string Subjects { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public DateTime SubmittedDateTime { get; set; }
        public string ImplementationFormat { get; set; } = "";
        public string Operation { get; set; } = "";
        public string ReportId { get; set; } = "";
        public string SchoolYear { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";

        public override string ToString()
        {
            return $"[{Status}] {Subjects.Split(' ')[0]} {Title} -> {EndDateTime}";
        }

        public string ToShortString()
        {
            return $"{Subjects.Split(' ')[0]} {Title}";
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
            return SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ ReportId.GetHashCode();
        }
    }

    public class Quiz
    {
        public string Subjects { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime StartDateTime { get; set; }
        public DateTime EndDateTime { get; set; }
        public string SubmissionStatus { get; set; } = "";
        public string ImplementationFormat { get; set; } = "";
        public string Operation { get; set; } = "";
        public string QuizId { get; set; } = "";
        public string SchoolYear { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";

        public override string ToString()
        {
            return $"[{SubmissionStatus}] {Subjects.Split(' ')[0]} {Title} -> {EndDateTime}";
        }

        public string ToShortString()
        {
            return $"{Subjects.Split(' ')[0]} {Title}";
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
            return SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ QuizId.GetHashCode();
        }

        //public int CompareTo(Object obj)
        //{
        //    if (obj == null || GetType() != obj.GetType())
        //    {
        //        return 1;
        //    }
        //    Quiz objQuiz = (Quiz)obj;
        //    if (EndDateTime > objQuiz.EndDateTime)
        //    {
        //        return 1;
        //    }
        //    else
        //    {
        //        if (Status == "受付中")
        //        {
        //            return 1;
        //        }
        //        if (Status == "提出済")
        //        {
        //            return -1;
        //        }
        //        return SubjectCode.CompareTo(objQuiz.SubjectCode);
        //    }
        //}
    }

    public class ClassContact
    {
        public string Subjects { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string ContactType { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string FileLinkRelease { get; set; } = "";
        public string ReferenceURL { get; set; } = "";
        public string Severity { get; set; } = "";
        public DateTime TargetDateTime { get; set; }
        public DateTime ContactDateTime { get; set; }
        public string WebReplyRequest { get; set; } = "";

        public override string ToString()
        {
            return $"{Subjects.Split(' ')[0]} {Title} {ContactDateTime.ToShortDateString()}";
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
            return Subjects.GetHashCode() ^ Title.GetHashCode() ^ ContactDateTime.GetHashCode();
        }
    }

    public class SchoolContact
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string ContactSource { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string FileLinkRelease { get; set; } = "";
        public string ReferenceURL { get; set; } = "";
        public string Severity { get; set; } = "";
        public DateTime ContactDateTime { get; set; }
        public string WebReplyRequest { get; set; } = "";
        public string ManagementAffiliation { get; set; } = "";
        public string SchoolContactId { get; set; } = "";

        public override string ToString()
        {
            return $"{Category} {Title} {ContactDateTime.ToShortDateString()}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            SchoolContact objSchoolContact = (SchoolContact)obj;
            return ContactSource == objSchoolContact.ContactSource && Title == objSchoolContact.Title && ContactDateTime == objSchoolContact.ContactDateTime;
        }

        public override int GetHashCode()
        {
            return ContactSource.GetHashCode() ^ Title.GetHashCode() ^ ContactDateTime.GetHashCode();
        }
    }

    public class ClassSharedFile
    {
        public string Subjects { get; set; } = "";
        public string Title { get; set; } = "";
        public string Size { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = "";
        public string PublicPeriod { get; set; } = "";
        public DateTime UpdateDateTime { get; set; }

        public override string ToString()
        {
            return $"{Subjects.Split(' ')[0]} {Title} {UpdateDateTime.ToShortDateString()}";
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
            return Subjects.GetHashCode() ^ Title.GetHashCode() ^ UpdateDateTime.GetHashCode();
        }
    }

    public class SchoolSharedFile
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public int DownloadCount { get; set; }
        public string Size { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string Description { get; set; } = "";
        public string PublicPeriod { get; set; } = "";
        public DateTime UpdateDateTime { get; set; }
        public string SchoolSharedFileId { get; set; } = "";

        public override string ToString()
        {
            return $"{Category} {Title} {UpdateDateTime.ToShortDateString()}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            SchoolSharedFile objSchoolSharedFile = (SchoolSharedFile)obj;
            return Category == objSchoolSharedFile.Category && Title == objSchoolSharedFile.Title && UpdateDateTime == objSchoolSharedFile.UpdateDateTime;
        }

        public override int GetHashCode()
        {
            return Category.GetHashCode() ^ Title.GetHashCode() ^ UpdateDateTime.GetHashCode();
        }
    }

    public class ClassResult
    {
        public string Subjects { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string SubjectsSection { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int SchoolCredit { get; set; }
        public string Evaluation { get; set; } = "";
        public double Score { get; set; }
        public double GP { get; set; }
        public string AcquisitionYear { get; set; } = "";
        public DateTime ReportDate { get; set; }
        public string TestType { get; set; } = "";

        public override string ToString()
        {
            return $"{Subjects} {Score} ({Evaluation}) {GP} {ReportDate.ToShortDateString()}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            ClassResult objClassResult = (ClassResult)obj;
            return Subjects == objClassResult.Subjects && AcquisitionYear == objClassResult.AcquisitionYear;
        }

        public override int GetHashCode()
        {
            return Subjects.GetHashCode() ^ AcquisitionYear.GetHashCode();
        }
    }

    public class ClassResultCredit
    {
        public string Evaluation { get; set; } = "";
        public int SchoolCredit { get; set; }

        public override string ToString()
        {
            return $"{Evaluation} {SchoolCredit}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            ClassResultCredit objClassResultCredit = (ClassResultCredit)obj;
            return Evaluation == objClassResultCredit.Evaluation && SchoolCredit == objClassResultCredit.SchoolCredit;
        }

        public override int GetHashCode()
        {
            return Evaluation.GetHashCode() ^ SchoolCredit.GetHashCode();
        }
    }

    public class ClassTableRow
    {
        public ClassTableCell Monday { get; set; } = new ClassTableCell();
        public ClassTableCell Tuesday { get; set; } = new ClassTableCell();
        public ClassTableCell Wednesday { get; set; } = new ClassTableCell();
        public ClassTableCell Thursday { get; set; } = new ClassTableCell();
        public ClassTableCell Friday { get; set; } = new ClassTableCell();

        public override string ToString()
        {
            return $"{Monday.SubjectsName} {Tuesday.SubjectsName} {Wednesday.SubjectsName} {Thursday.SubjectsName} {Friday.SubjectsName}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            ClassTableRow objClassTableRow = (ClassTableRow)obj;
            return Monday.GetHashCode() == objClassTableRow.Monday.GetHashCode() && Tuesday.GetHashCode() == objClassTableRow.Tuesday.GetHashCode() && Wednesday.GetHashCode() == objClassTableRow.Wednesday.GetHashCode() && Thursday.GetHashCode() == objClassTableRow.Thursday.GetHashCode() && Friday.GetHashCode() == objClassTableRow.Friday.GetHashCode();
        }

        public override int GetHashCode()
        {
            return Monday.GetHashCode() ^ Tuesday.GetHashCode() ^ Wednesday.GetHashCode() ^ Thursday.GetHashCode() ^ Friday.GetHashCode();
        }
    }

    public class ClassTableCell
    {
        public string SubjectsName { get; set; } = "";
        public string SubjectsId { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string SubjectsSection { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int SchoolCredit { get; set; }
        public string ClassName { get; set; } = "";
        public string ClassRoom { get; set; } = "";
        public string SyllabusURL { get; set; } = "";

        public bool ButtonsVisible => SubjectsName != "" && SubjectsId != "";
        public bool ReportBadgeVisible => ReportCount > 0;
        public bool ReportBadgeOneDigits => ReportCount < 10;
        public int ReportCount { get; set; }
        public bool QuizBadgeVisible => QuizCount > 0;
        public bool QuizBadgeOneDigits => QuizCount < 10;
        public int QuizCount { get; set; }

        public override string ToString()
        {
            if (SubjectsId == "")
            {
                return "";
            }
            if (ClassRoom == "")
            {
                return $"{SubjectsName} ({ClassName})\n{TeacherName}";
            }
            return $"{SubjectsName} ({ClassName})\n{TeacherName}\n{ClassRoom}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }
            ClassTableCell objClassTableCell = (ClassTableCell)obj;
            return SubjectsName == objClassTableCell.SubjectsName && SubjectsId == objClassTableCell.SubjectsId;
        }

        public override int GetHashCode()
        {
            return SubjectsName.GetHashCode() ^ SubjectsId.GetHashCode();
        }
    }
}
