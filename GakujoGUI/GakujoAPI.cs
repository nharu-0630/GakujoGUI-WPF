using HtmlAgilityPack;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        private Account account = new();
        private List<Report> reports = new();
        private List<Quiz> quizzes = new();
        private List<ClassContact> classContacts = new() { };
        private List<ClassSharedFile> classSharedFiles = new() { };
        private SchoolGrade schoolGrade = new();
        private ClassTableRow[] classTables = new ClassTableRow[7];
        private bool loginStatus = false;

        public Account Account { get => account; set => account = value; }
        public List<Report> Reports { get => reports; set => reports = value; }
        public List<Quiz> Quizzes { get => quizzes; set => quizzes = value; }
        public List<ClassContact> ClassContacts { get => classContacts; set => classContacts = value; }
        public List<ClassSharedFile> ClassSharedFiles { get => classSharedFiles; set => classSharedFiles = value; }
        public SchoolGrade SchoolGrade { get => schoolGrade; set => schoolGrade = value; }
        public ClassTableRow[] ClassTables { get => classTables; set => classTables = value; }
        public bool LoginStatus { get => loginStatus; }

        private CookieContainer cookieContainer = new();
        private HttpClientHandler httpClientHandler = new();
        private HttpClient httpClient = new();
        private HttpRequestMessage httpRequestMessage = new();
        private HttpResponseMessage httpResponseMessage = new();

        private readonly string cookiesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI\Cookies");
        private readonly string downloadPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI\Download\");

        private readonly string schoolYear = "";
        private readonly int semesterCode;
        private readonly string userAgent = "";
        private string SchoolYearSemesterCodeSuffix => $"_{schoolYear}_{(semesterCode < 2 ? 1 : 2)}";
        private string ReportDateStart => $"{schoolYear}/0{(semesterCode < 2 ? 3 : 8)}/01";

        private static string GetJsonPath(string value)
        {
            if (!Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI")))
            {
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI"));
            }
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData!), @$"GakujoGUI\{value}.json");
        }

        public GakujoAPI(string schoolYear, int semesterCode, string userAgent)
        {
            this.schoolYear = schoolYear;
            this.semesterCode = semesterCode;
            this.userAgent = userAgent;
            logger.Info($"Initialize GakujoAPI schoolYear={schoolYear}, semesterCode={semesterCode}, userAgent={userAgent}.");
            LoadCookies();
            LoadJson();
        }

        public void SetAccount(string userId, string passWord)
        {
            Account.UserId = userId;
            Account.PassWord = passWord;
            logger.Info($"Set Account.");
            SaveJsons();
        }

        private void SaveCookies()
        {
            using Stream stream = File.Create(cookiesPath);
            BinaryFormatter binaryFormatter = new();
#pragma warning disable SYSLIB0011 // 型またはメンバーが旧型式です
            binaryFormatter.Serialize(stream, cookieContainer);
#pragma warning restore SYSLIB0011 // 型またはメンバーが旧型式です
            logger.Info("Save Cookies.");
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
                logger.Info("Load Cookies.");
                return CheckConnection();
            }
            cookieContainer = new CookieContainer();
            return false;
        }

        public bool LoadJson()
        {
            if (File.Exists(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix)))
            {
                Reports = JsonConvert.DeserializeObject<List<Report>>(File.ReadAllText(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix)))!;
                logger.Info("Load Reports.");
            }
            if (File.Exists(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix)))
            {
                Quizzes = JsonConvert.DeserializeObject<List<Quiz>>(File.ReadAllText(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix)))!;
                logger.Info("Load Quizzes.");
            }
            if (File.Exists(GetJsonPath($"ClassContacts" + SchoolYearSemesterCodeSuffix)))
            {
                ClassContacts = JsonConvert.DeserializeObject<List<ClassContact>>(File.ReadAllText(GetJsonPath("ClassContacts" + SchoolYearSemesterCodeSuffix)))!;
                logger.Info("Load ClassContacts.");
            }
            if (File.Exists(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix)))
            {
                ClassSharedFiles = JsonConvert.DeserializeObject<List<ClassSharedFile>>(File.ReadAllText(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix)))!;
                logger.Info("Load ClassSharedFiles.");
            }
            if (File.Exists(GetJsonPath("SchoolGrade")))
            {
                SchoolGrade = JsonConvert.DeserializeObject<SchoolGrade>(File.ReadAllText(GetJsonPath("SchoolGrade")))!;
                logger.Info("Load SchoolGrade.");
            }
            if (File.Exists(GetJsonPath("ClassTables")))
            {
                ClassTables = JsonConvert.DeserializeObject<ClassTableRow[]>(File.ReadAllText(GetJsonPath("ClassTables")))!;
                logger.Info("Load ClassTables.");
            }
            ApplyReportsClassTables();
            ApplyQuizzesClassTables();
            if (File.Exists(GetJsonPath("Account")))
            {
                Account = JsonConvert.DeserializeObject<Account>(File.ReadAllText(GetJsonPath("Account")))!;
                logger.Info("Load Account.");
                return true;
            }
            return false;
        }

        public void SaveJsons()
        {
            logger.Info("Save Jsons.");
            try { File.WriteAllText(GetJsonPath("Reports" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(Reports, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save Reports."); }
            try { File.WriteAllText(GetJsonPath("Quizzes" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(Quizzes, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save Quizzes."); }
            try { File.WriteAllText(GetJsonPath("ClassContacts" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(ClassContacts, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save ClassContacts."); }
            try { File.WriteAllText(GetJsonPath("ClassSharedFiles" + SchoolYearSemesterCodeSuffix), JsonConvert.SerializeObject(ClassSharedFiles, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save ClassSharedFiles."); }
            try { File.WriteAllText(GetJsonPath("SchoolGrade"), JsonConvert.SerializeObject(SchoolGrade, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save SchoolGrade."); }
            try { File.WriteAllText(GetJsonPath("ClassTables"), JsonConvert.SerializeObject(ClassTables, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save ClassTables."); }
            try { File.WriteAllText(GetJsonPath("Account"), JsonConvert.SerializeObject(Account, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save Account."); }
        }

        public bool Login()
        {
            logger.Info("Start Login.");
            if (3 <= DateTime.Now.Hour && DateTime.Now.Hour < 5) { logger.Warn("Return Login by overtime."); return false; }
            cookieContainer = new();
            httpClientHandler = new() { AutomaticDecompression = ~DecompressionMethods.None, CookieContainer = cookieContainer };
            httpClient = new(httpClientHandler);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/portal/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/portal/");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/login/preLogin/preLogin");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("mistakeChecker=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/login/preLogin/preLogin");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("selectLocale=ja&mistakeChecker=0&EXCLUDE_SET=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://idp.shizuoka.ac.jp/idp/profile/SAML2/Redirect/SSO?execution=e1s1");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"j_username={Account.UserId}&j_password={Account.PassWord}&_eventId_proceed=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://idp.shizuoka.ac.jp/idp/profile/SAML2/Redirect/SSO?execution=e1s1");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (HttpUtility.HtmlDecode(httpResponseMessage.Content.ReadAsStringAsync().Result).Contains("ユーザ名またはパスワードが正しくありません。"))
            {
                logger.Warn("Return Login by wrong username or password.");
                return false;
            }
            else
            {
                HtmlDocument htmlDocument = new();
                htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
                string relayState = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[1]").Attributes["value"].Value;
                relayState = relayState.Replace("&#x3a;", ":");
                string SAMLResponse = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[2]").Attributes["value"].Value;
                relayState = Uri.EscapeDataString(relayState);
                SAMLResponse = Uri.EscapeDataString(SAMLResponse);
                httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent($"RelayState={relayState}&SAMLResponse={SAMLResponse}");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                logger.Info("POST https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                logger.Info("GET https://gakujo.shizuoka.ac.jp/portal/shibbolethlogin/shibbolethLogin/initLogin/sso");
                logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent("EXCLUDE_SET=");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
                logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
                Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
                Account.StudentName = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/div/div/div/ul[2]/li/a/span/span").InnerText;
                Account.StudentName = Account.StudentName[0..^2];
            }
            Account.LoginDateTime = DateTime.Now;
            logger.Info("End Login.");
            SaveJsons();
            SaveCookies();
            loginStatus = true;
            return true;
        }

        public void GetReports(out List<Report> diffReports)
        {
            logger.Info("Start Get Reports.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業サポート&menuCode=A02&nextPath=/report/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/search");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&reportId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/search");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            if (htmlDocument.GetElementbyId("searchList") == null)
            {
                diffReports = new();
                logger.Warn("Return Get Reports by not found list.");
                Account.ReportDateTime = DateTime.Now;
                logger.Info("End Get Reports.");
                ApplyReportsClassTables();
                SaveJsons();
                SaveCookies();
                return;
            }
            diffReports = new(Reports);
            Reports.Clear();
            int limitCount = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            logger.Info($"Found {limitCount} reports.");
            for (int i = 0; i < limitCount; i++)
            {
                Report report = new();
                report.Subjects = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                report.Subjects = Regex.Replace(report.Subjects, @"\s+", " ");
                report.Title = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText.Trim();
                report.Id = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
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
                Reports.Add(report);
            }
            diffReports = Reports.Except(diffReports).ToList();
            Account.ReportDateTime = DateTime.Now;
            logger.Info("End Get Reports.");
            ApplyReportsClassTables();
            SaveJsons();
            SaveCookies();
        }

        private void ApplyReportsClassTables()
        {
            logger.Info("Start Apply Reports to ClassTables.");
            if (ClassTables == null) { logger.Warn("Return Apply Reports to ClassTables by ClassTables is null."); return; }
            foreach (ClassTableRow classTableRow in ClassTables)
            {
                if (classTableRow == null) { continue; }
                for (int i = 0; i < 5; i++) { classTableRow[i].ReportCount = 0; }
            }
            foreach (Report report in Reports.FindAll(x => x.Unsubmitted)
            {
                foreach (ClassTableRow classTableRow in ClassTables)
                {
                    if (classTableRow == null) { continue; }
                    for (int i = 0; i < 5; i++) { if (report.Subjects.Contains($"{classTableRow[i].SubjectsName}（{classTableRow[i].ClassName}）")) { classTableRow[i].ReportCount++; } }
                }
            }
            logger.Info("End Apply Reports to ClassTables");
        }

        public void GetQuizzes(out List<Quiz> diffQuizzes)
        {
            logger.Info("Start Get Quizzes.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=小テスト一覧&menuCode=A03&nextPath=/test/student/searchList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/search");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&testId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/search");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            if (htmlDocument.GetElementbyId("searchList") == null)
            {
                diffQuizzes = new();
                logger.Warn("Return Get Quizzes by not found list.");
                Account.QuizDateTime = DateTime.Now;
                logger.Info("End Get Quizzes.");
                ApplyQuizzesClassTables();
                SaveJsons();
                SaveCookies();
                return;
            }
            diffQuizzes = new(Quizzes);
            Quizzes.Clear();
            int limitCount = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr").Count;
            logger.Info($"Found {limitCount} quizzes.");
            for (int i = 0; i < limitCount; i++)
            {
                Quiz quiz = new();
                quiz.Subjects = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                quiz.Subjects = Regex.Replace(quiz.Subjects, @"\s+", " ");
                quiz.Title = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText.Trim();
                quiz.Id = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                quiz.SchoolYear = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[3].Replace("'", "").Trim();
                quiz.SubjectCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[4].Replace("'", "").Trim();
                quiz.ClassCode = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[5].Replace("'", "").Replace(");", "").Trim();
                quiz.Status = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                quiz.StartDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[0]);
                quiz.EndDateTime = DateTime.Parse(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText.Trim().Split('～')[1]);
                quiz.SubmissionStatus = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText.Trim();
                quiz.ImplementationFormat = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[5].InnerText.Trim();
                quiz.Operation = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[6].InnerText.Trim();
                Quizzes.Add(quiz);
            }
            diffQuizzes = Quizzes.Except(diffQuizzes).ToList();
            Account.QuizDateTime = DateTime.Now;
            logger.Info("End Get Quizzes.");
            ApplyQuizzesClassTables();
            SaveJsons();
            SaveCookies();
        }

        private void ApplyQuizzesClassTables()
        {
            logger.Info("Start Apply Quizzes to ClassTables.");
            if (ClassTables == null) { logger.Warn("Return Apply Quizzes to ClassTables by ClassTables is null."); return; }
            foreach (ClassTableRow classTableRow in ClassTables)
            {
                if (classTableRow == null) { continue; }
                for (int i = 0; i < 5; i++) { classTableRow[i].QuizCount = 0; }
            }
            foreach (Quiz quiz in Quizzes.FindAll(x => x.Unsubmitted)
            {
                foreach (ClassTableRow classTableRow in ClassTables)
                {
                    if (classTableRow == null) { continue; }
                    for (int i = 0; i < 5; i++) { if (quiz.Subjects.Contains($"{classTableRow[i].SubjectsName}（{classTableRow[i].ClassName}）")) { classTableRow[i].QuizCount++; } }
                }
            }
            logger.Info("End Apply Quizzes to ClassTables.");
        }

        public void GetClassContacts(out int diffCount, int maxCount = 10)
        {
            logger.Info("Start Get ClassContacts.");
            ClassContact? lastClassContact = ClassContacts.Count > 0 ? ClassContacts[0] : null;
            List<ClassContact> diffClassContacts = new() { };
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業連絡一覧&menuCode=A01&nextPath=/classcontact/classContactList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={ReportDateStart}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            if (htmlDocument.GetElementbyId("tbl_A01_01") == null)
            {
                diffCount = 0;
                logger.Warn("Return Get ClassContacts by not found list.");
                Account.ClassContactDateTime = DateTime.Now;
                logger.Info("End Get ClassContacts.");
                SaveJsons();
                SaveCookies();
                return;
            }
            int limitCount = htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr").Count;
            logger.Info($"Found {limitCount} ClassContacts.");
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
                if (classContact.Equals(lastClassContact)) { logger.Info("Break by equals last ClassContact."); break; }
                diffClassContacts.Add(classContact);
            }
            diffCount = diffClassContacts.Count;
            logger.Info($"Found {diffCount} new ClassContacts.");
            ClassContacts.InsertRange(0, diffClassContacts);
            maxCount = maxCount == -1 ? diffCount : maxCount;
            for (int i = 0; i < Math.Min(diffCount, maxCount); i++) { GetClassContact(i); }
            Account.ClassContactDateTime = DateTime.Now;
            logger.Info("End Get ClassContacts.");
            SaveJsons();
            SaveCookies();
        }

        public void GetClassContact(int indexCount)
        {
            logger.Info($"Start Get ClassContact indexCount={indexCount}.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業連絡一覧&menuCode=A01&nextPath=/classcontact/classContactList/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={ReportDateStart}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/goDetail/" + indexCount);
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            string content = $"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={schoolYear}/01/01&reportDateEnd=&requireResponse=&studentCode=&studentName=&tbl_A01_01_length=-1&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A01_01&_screenInfoDisp=true&_scrollTop=0";
            httpRequestMessage.Content = new StringContent(content);
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/goDetail/" + indexCount);
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            ClassContacts[indexCount].ContactType = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[0].SelectSingleNode("td").InnerText;
            ClassContacts[indexCount].Content = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[2].SelectSingleNode("td").InnerText);
            ClassContacts[indexCount].Content = Regex.Replace(ClassContacts[indexCount].Content, "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline);
            ClassContacts[indexCount].FileLinkRelease = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            ClassContacts[indexCount].ReferenceURL = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            ClassContacts[indexCount].Severity = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[6].SelectSingleNode("td").InnerText.Replace("\r", "").Replace("\n", "").Replace("\t", "");
            ClassContacts[indexCount].WebReplyRequest = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[8].SelectSingleNode("td").InnerText;
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div") != null)
            {
                ClassContacts[indexCount].Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div")[i];
                    string prefix = htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("fileDownLoad('", "").Replace("'", "");
                    string no = htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim();
                    httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                    logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                    Stream stream = httpResponseMessage.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath)) { Directory.CreateDirectory(downloadPath); }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    ClassContacts[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            logger.Info($"End Get ClassContact indexCount={indexCount}.");
            SaveJsons();
            SaveCookies();
        }

        public void GetClassSharedFiles(out int diffCount, int maxCount = 10)
        {
            logger.Info("Start Get ClassSharedFiles.");
            ClassSharedFile? lastClassSharedFile = (ClassSharedFiles.Count > 0) ? ClassSharedFiles[0] : null;
            List<ClassSharedFile> diffClassSharedFiles = new() { };
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業共有ファイル&menuCode=A08&nextPath=/classfile/classFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            if (htmlDocument.GetElementbyId("tbl_classFile") == null)
            {
                diffCount = 0;
                logger.Warn("Return Get ClassSharedFiles by not found list.");
                Account.ClassSharedFileDateTime = DateTime.Now;
                logger.Info("End Get ClassSharedFiles.");
                SaveJsons();
                SaveCookies();
                return;
            }
            int limitCount = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr").Count;
            logger.Info($"Found {limitCount} ClassSharedFiles.");
            for (int i = 0; i < limitCount; i++)
            {
                ClassSharedFile classSharedFile = new();
                classSharedFile.Subjects = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText.Replace("\r", "").Replace("\n", "").Trim();
                classSharedFile.Subjects = Regex.Replace(classSharedFile.Subjects, @"\s+", " ");
                classSharedFile.Title = HttpUtility.HtmlDecode(htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].SelectSingleNode("a").InnerText).Trim();
                classSharedFile.Size = htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText;
                classSharedFile.UpdateDateTime = DateTime.Parse(htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[4].InnerText);
                if (classSharedFile.Equals(lastClassSharedFile)) { logger.Info("Break by equals last ClassSharedFile."); break; }
                diffClassSharedFiles.Add(classSharedFile);
            }
            diffCount = diffClassSharedFiles.Count;
            logger.Info($"Found {diffCount} new ClassSharedFiles.");
            ClassSharedFiles.InsertRange(0, diffClassSharedFiles);
            maxCount = maxCount == -1 ? diffCount : maxCount;
            for (int i = 0; i < Math.Min(diffCount, maxCount); i++) { GetClassSharedFile(i); }
            Account.ClassSharedFileDateTime = DateTime.Now;
            logger.Info("End Get ClassSharedFiles.");
            SaveJsons();
            SaveCookies();
        }

        public void GetClassSharedFile(int indexCount)
        {
            logger.Info($"Start Get ClassSharedFile indexCount={indexCount}.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=授業共有ファイル&menuCode=A08&nextPath=/classfile/classFile/initialize");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/selectClassFileList");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/showClassFileDetail?EXCLUDE_SET=&org.apache.struts.taglib.html.TOKEN=" + $"{Account.ApacheToken}&selectIndex={indexCount}&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_searchConditionDisp.accordionSearchCondition=false&_scrollTop=0");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/portal/classfile/classFile/showClassFileDetail?EXCLUDE_SET=&org.apache.struts.taglib.html.TOKEN=" + $"{Account.ApacheToken}&selectIndex={indexCount}&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_searchConditionDisp.accordionSearchCondition=false&_scrollTop=0");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            ClassSharedFiles[indexCount].Description = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[2].SelectSingleNode("td").InnerText);
            ClassSharedFiles[indexCount].PublicPeriod = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[3].SelectSingleNode("td").InnerText.Replace("\n", "").Replace("\t", "").Replace("&nbsp;", "");
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div") != null)
            {
                ClassSharedFiles[indexCount].Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div")[i];
                    string prefix = htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[0].Replace("fileDownLoad('", "").Replace("'", "");
                    string no = htmlNode.SelectSingleNode("a").Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim();
                    httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&prefix=default&sequence=&webspaceTabDisplayFlag=&screenName=&fileNameAutonumberFlag=&fileNameDisplayFlag=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                    logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/fileUploadDownload/fileDownLoad?EXCLUDE_SET=&prefix=" + $"{prefix}&no={no}&EXCLUDE_SET=");
                    logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                    Stream stream = httpResponseMessage.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath))
                    {
                        Directory.CreateDirectory(downloadPath);
                    }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    ClassSharedFiles[indexCount].Files[i] = Path.Combine(downloadPath, htmlNode.SelectSingleNode("a").InnerText.Trim());
                }
            }
            logger.Info($"End Get ClassSharedFile indexCount={indexCount}.");
            SaveJsons();
            SaveCookies();
        }

        private bool CheckConnection()
        {
            logger.Info("Start Check connection.");
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&headTitle=ホーム&menuCode=Z07&nextPath=/home/home/initialize&_screenIdentifier=&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/common/generalPurpose/");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form[1]/div/input") == null)
            {
                cookieContainer = new CookieContainer();
                httpClientHandler = new HttpClientHandler { AutomaticDecompression = ~DecompressionMethods.None, CookieContainer = cookieContainer };
                httpClient = new HttpClient(httpClientHandler);
                logger.Warn("Return Check connection by not found token.");
                return false;
            }
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form[1]/div/input").Attributes["value"].Value;
            logger.Info("End Check connection.");
            SaveJsons();
            SaveCookies();
            loginStatus = true;
            return true;
        }

        private bool SetAcademicSystem()
        {
            logger.Info("Start Set AcademicSystem.");
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/preLogin.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/preLogin.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/home/systemCooperationLink/initializeShibboleth?renkeiType=kyoumu");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Headers.TryAddWithoutValidation("Origin", "https://gakujo.shizuoka.ac.jp");
            httpRequestMessage.Headers.TryAddWithoutValidation("Referer", "https://gakujo.shizuoka.ac.jp/portal/home/home/initialize");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/home/systemCooperationLink/initializeShibboleth?renkeiType=kyoumu");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/sso/loginStudent.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent("loginID=");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/sso/loginStudent.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form/div/input[1]") != null && htmlDocument.DocumentNode.SelectNodes("/html/body/form/div/input[2]") != null)
            {
                logger.Warn("Additional transition.");
                string relayState = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[1]").Attributes["value"].Value;
                relayState = relayState.Replace("&#x3a;", ":");
                string SAMLResponse = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[2]").Attributes["value"].Value;
                relayState = Uri.EscapeDataString(relayState);
                SAMLResponse = Uri.EscapeDataString(SAMLResponse);
                httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent($"RelayState={relayState}&SAMLResponse={SAMLResponse}");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                logger.Info("POST https://gakujo.shizuoka.ac.jp/Shibboleth.sso/SAML2/POST");
                logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
            logger.Info("End Set AcademicSystem.");
            SaveJsons();
            SaveCookies();
            return true;
        }

        public void GetClassResults(out List<ClassResult> diffClassResults)
        {
            logger.Info("Start Get ClassResults.");
            SetAcademicSystem();
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/seisekiSearchStudentInit.do?mainMenuCode=008&parentMenuCode=007");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/seisekiSearchStudentInit.do?mainMenuCode=008&parentMenuCode=007");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("//table[@class=\"txt12\"]") == null) { diffClassResults = new(); logger.Warn("Not found ClassResults list."); }
            else
            {
                diffClassResults = new(SchoolGrade.ClassResults);
                SchoolGrade.ClassResults.Clear();
                logger.Info($"Found {htmlDocument.DocumentNode.SelectSingleNode("//table[@class=\"txt12\"]").SelectNodes("tr").Count - 1} ClassResults.");
                for (int i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("//table[@class=\"txt12\"]").SelectNodes("tr").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("//table[@class=\"txt12\"]").SelectNodes("tr")[i];
                    ClassResult classResult = new();
                    classResult.Subjects = htmlNode.SelectNodes("td")[0].InnerText.Trim();
                    classResult.TeacherName = htmlNode.SelectNodes("td")[1].InnerText.Trim();
                    classResult.SubjectsSection = htmlNode.SelectNodes("td")[2].InnerText.Trim();
                    classResult.SelectionSection = htmlNode.SelectNodes("td")[3].InnerText.Trim();
                    classResult.Credit = int.Parse(htmlNode.SelectNodes("td")[4].InnerText.Trim());
                    classResult.Evaluation = htmlNode.SelectNodes("td")[5].InnerText.Trim();
                    if (htmlNode.SelectNodes("td")[6].InnerText.Trim() != "") { classResult.Score = double.Parse(htmlNode.SelectNodes("td")[6].InnerText.Trim()); }
                    if (htmlNode.SelectNodes("td")[7].InnerText.Trim() != "") { classResult.GP = double.Parse(htmlNode.SelectNodes("td")[7].InnerText.Trim()); }
                    classResult.AcquisitionYear = htmlNode.SelectNodes("td")[8].InnerText.Trim();
                    classResult.ReportDate = DateTime.Parse(htmlNode.SelectNodes("td")[9].InnerText.Trim());
                    classResult.TestType = htmlNode.SelectNodes("td")[10].InnerText.Trim();
                    SchoolGrade.ClassResults.Add(classResult);
                }
                diffClassResults = SchoolGrade.ClassResults.Except(diffClassResults).ToList();
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/hyoukabetuTaniSearch.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/hyoukabetuTaniSearch.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.EvaluationCredits.Clear();
            for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr").Count; i++)
            {
                SchoolGrade.EvaluationCredits.Add(new() { Evaluation = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\n", "").Replace("\t", ""), Credit = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText) });
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/gpa.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/gpa.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.DepartmentGPA.Grade = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table/tr[1]/td[2]").InnerText.Replace("年", ""));
            SchoolGrade.DepartmentGPA.GPA = double.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table/tr[2]/td[2]").InnerText);
            SchoolGrade.DepartmentGPA.SemesterGPAs.Clear();
            for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr").Count - 3; i++)
            {
                SemesterGPA semesterGPA = new();
                semesterGPA.Year = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i + 2].SelectNodes("td")[0].InnerText.Split('　')[0].Replace("\n", "").Replace(" ", "");
                semesterGPA.Semester = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i + 2].SelectNodes("td")[0].InnerText.Split('　')[1].Replace("\n", "").Replace(" ", "");
                semesterGPA.GPA = double.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i + 2].SelectNodes("td")[1].InnerText);
                SchoolGrade.DepartmentGPA.SemesterGPAs.Add(semesterGPA);
            }
            SchoolGrade.DepartmentGPA.CalculationDate = DateTime.ParseExact(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr").Last().SelectNodes("td")[1].InnerText, "yyyy年 MM月 dd日", null);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/gpaImage.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/gpaImage.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.DepartmentGPA.DepartmentImage = Convert.ToBase64String(httpResponseMessage.Content.ReadAsByteArrayAsync().Result);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/departmentGpa.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/departmentGpa.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.DepartmentGPA.DepartmentRank[0] = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[^2].SelectNodes("td")[1].InnerText.Trim(' ').Split('　')[1].Replace("位", ""));
            SchoolGrade.DepartmentGPA.DepartmentRank[1] = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[^2].SelectNodes("td")[1].InnerText.Trim(' ').Split('　')[0].Replace("人中", ""));
            SchoolGrade.DepartmentGPA.CourseRank[0] = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[^1].SelectNodes("td")[1].InnerText.Trim(' ').Split('　')[1].Replace("位", ""));
            SchoolGrade.DepartmentGPA.CourseRank[1] = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[^1].SelectNodes("td")[1].InnerText.Trim(' ').Split('　')[0].Replace("人中", ""));
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/departmentGpaImage.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/departmentGpaImage.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.DepartmentGPA.CourseImage = Convert.ToBase64String(httpResponseMessage.Content.ReadAsByteArrayAsync().Result);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/nenbetuTaniSearch.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/nenbetuTaniSearch.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            SchoolGrade.YearCredits.Clear();
            for (int i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr").Count; i++)
            {
                SchoolGrade.YearCredits.Add(new() { Year = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText.Replace("\n", "").Trim(), Credit = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[2]/tr/td/table").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText) });
            }
            Account.ClassResultDateTime = DateTime.Now;
            logger.Info("End Get ClassResults.");
            SaveJsons();
            SaveCookies();
        }

        public void GetClassTables()
        {
            logger.Info("Start Get ClassTables.");
            SetAcademicSystem();
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=005&parentMenuCode=004");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=005&parentMenuCode=004");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/table[4]") == null) { logger.Warn("Return Get ClassTables by not found list."); return; }
            for (int i = 0; i < 7; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    ClassTableCell classTableCell = ClassTables[i][j];
                    if (htmlDocument.DocumentNode.SelectNodes($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table/tr[2]/td/a") != null)
                    {
                        string detailKamokuCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table/tr[2]/td/a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                        string detailClassCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table/tr[2]/td/a").Attributes["onclick"].Value.Split(',')[2].Replace("'", "").Trim();
                        if (classTableCell.KamokuCode != detailKamokuCode || classTableCell.ClassCode != detailClassCode)
                        {
                            classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                            string classRoom = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table/tr[2]/td").InnerHtml;
                            classTableCell.ClassRoom = classRoom[(classRoom.LastIndexOf("<br>") + 4)..].Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ').Replace("&nbsp;", "");
                        }
                    }
                    else if (htmlDocument.DocumentNode.SelectNodes($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[1]/tr/td/a") != null && (semesterCode == 0 || semesterCode == 2))
                    {
                        string detailKamokuCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[1]/tr/td/a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                        string detailClassCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[1]/tr/td/a").Attributes["onclick"].Value.Split(',')[2].Replace("'", "").Trim();
                        if (classTableCell.KamokuCode != detailKamokuCode || classTableCell.ClassCode != detailClassCode)
                        {
                            classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                            string classRoom = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[1]/tr/td/a").InnerHtml;
                            classTableCell.ClassRoom = classRoom[(classRoom.LastIndexOf("<br>") + 4)..].Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ').Replace("&nbsp;", "");
                        }
                    }
                    else if (htmlDocument.DocumentNode.SelectNodes($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[2]/tr/td/a") != null && (semesterCode == 1 || semesterCode == 3))
                    {
                        string detailKamokuCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[2]/tr/td/a").Attributes["onclick"].Value.Split(',')[1].Replace("'", "").Trim();
                        string detailClassCode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[2]/tr/td/a").Attributes["onclick"].Value.Split(',')[2].Replace("'", "").Trim();
                        if (classTableCell.KamokuCode != detailKamokuCode || classTableCell.ClassCode != detailClassCode)
                        {
                            classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                            string classRoom = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table/tr[{i + 2}]/td[{j + 2}]/table[2]/tr/td/a").InnerHtml;
                            classTableCell.ClassRoom = classRoom[(classRoom.LastIndexOf("<br>") + 4)..].Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ').Replace("&nbsp;", "");
                        }
                    }
                    else { classTableCell = new(); }
                    ClassTables[i][j] = classTableCell;
                }
            }
            logger.Info("End Get ClassTables.");
            ApplyReportsClassTables();
            ApplyQuizzesClassTables();
            SaveJsons();
            SaveCookies();
        }

        private ClassTableCell GetClassTableCell(string detailKamokuCode, string detailClassCode)
        {
            logger.Info($"Start Get ClassTableCell detailKamokuCode={detailKamokuCode}, detailClassCode={detailClassCode}.");
            ClassTableCell classTableCell = new();
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/detailKamoku.do?detailKamokuCode=" + $"{detailKamokuCode}&detailClassCode={detailClassCode}&gamen=jikanwari");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/detailKamoku.do?detailKamokuCode=" + $"{detailKamokuCode}&detailClassCode={detailClassCode}&gamen=jikanwari");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            classTableCell.SubjectsName = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目名\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.SubjectsId = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目番号\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.ClassName = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"クラス名\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.TeacherName = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"担当教員\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.SubjectsSection = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目区分\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.SelectionSection = htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"必修選択区分\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            classTableCell.Credit = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"単位数\")]/following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Replace("単位", ""));
            classTableCell.KamokuCode = detailKamokuCode;
            classTableCell.ClassCode = detailClassCode;
            logger.Info($"End Get ClassTableCell detailKamokuCode={detailKamokuCode}, detailClassCode={detailClassCode}.");
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
        public string Id { get; set; } = "";
        public string SchoolYear { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";

        public bool Unsubmitted => Status == "受付中" && SubmittedDateTime == new DateTime();

        public override string ToString() => $"[{Status}] {Subjects.Split(' ')[0]} {Title} -> {EndDateTime}";

        public string ToShortString() => $"{Subjects.Split(' ')[0]} {Title}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            Report objReport = (Report)obj;
            return SubjectCode == objReport.SubjectCode && ClassCode == objReport.ClassCode && Id == objReport.Id;
        }

        public override int GetHashCode() => SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ Id.GetHashCode();
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
        public string Id { get; set; } = "";
        public string SchoolYear { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";

        public bool Unsubmitted => Status == "受付中" && SubmissionStatus == "未提出";

        public override string ToString() => $"[{SubmissionStatus}] {Subjects.Split(' ')[0]} {Title} -> {EndDateTime}";

        public string ToShortString() => $"{Subjects.Split(' ')[0]} {Title}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            Quiz objQuiz = (Quiz)obj;
            return SubjectCode == objQuiz.SubjectCode && ClassCode == objQuiz.ClassCode && Id == objQuiz.Id;
        }

        public override int GetHashCode() => SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ Id.GetHashCode();
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

        public override string ToString() => $"{Subjects.Split(' ')[0]} {Title} {ContactDateTime.ToShortDateString()}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassContact objClassContact = (ClassContact)obj;
            return Subjects == objClassContact.Subjects && Title == objClassContact.Title && ContactDateTime == objClassContact.ContactDateTime;
        }

        public override int GetHashCode() => Subjects.GetHashCode() ^ Title.GetHashCode() ^ ContactDateTime.GetHashCode();
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

        public override string ToString() => $"{Subjects.Split(' ')[0]} {Title} {UpdateDateTime.ToShortDateString()}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassSharedFile objClassSharedFile = (ClassSharedFile)obj;
            return Subjects == objClassSharedFile.Subjects && Title == objClassSharedFile.Title && UpdateDateTime == objClassSharedFile.UpdateDateTime;
        }

        public override int GetHashCode() => Subjects.GetHashCode() ^ Title.GetHashCode() ^ UpdateDateTime.GetHashCode();
    }

    public class ClassResult
    {
        public string Subjects { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string SubjectsSection { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int Credit { get; set; }
        public string Evaluation { get; set; } = "";
        public double Score { get; set; }
        public double GP { get; set; }
        public string AcquisitionYear { get; set; } = "";
        public DateTime ReportDate { get; set; }
        public string TestType { get; set; } = "";

        public override string ToString() => $"{Subjects} {Score} ({Evaluation}) {GP} {ReportDate.ToShortDateString()}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassResult objClassResult = (ClassResult)obj;
            return Subjects == objClassResult.Subjects && AcquisitionYear == objClassResult.AcquisitionYear;
        }

        public override int GetHashCode() => Subjects.GetHashCode() ^ AcquisitionYear.GetHashCode();
    }

    public class EvaluationCredit
    {
        public string Evaluation { get; set; } = "";
        public int Credit { get; set; }

        public override string ToString() => $"{Evaluation} {Credit}";
    }

    public class YearCredit
    {
        public string Year { get; set; } = "";
        public int Credit { get; set; }

        public override string ToString() => $"{Year} {Credit}";
    }

    public class SemesterGPA
    {
        public string Year { get; set; } = "";
        public string Semester { get; set; } = "";
        public double GPA { get; set; }

        public override string ToString() => $"{Year}{Semester} {GPA}";
    }

    public class DepartmentGPA
    {
        public int Grade { get; set; }
        public double GPA { get; set; }
        public List<SemesterGPA> SemesterGPAs { get; set; } = new() { };
        public DateTime CalculationDate { get; set; }
        public int[] DepartmentRank { get; set; } = new int[2];
        public int[] CourseRank { get; set; } = new int[2];
        public string DepartmentImage { get; set; } = "";
        public string CourseImage { get; set; } = "";

        public override string ToString()
        {
            string value = $"学年 {Grade}年";
            value += $"\n累積GPA {GPA}";
            value += $"\n学期GPA";
            foreach (SemesterGPA semesterGPA in SemesterGPAs)
            {
                value += $"\n{semesterGPA}";
            }
            value += $"\n学科内順位 {DepartmentRank[0]}/{DepartmentRank[1]}";
            value += $"\nコース内順位 {CourseRank[0]}/{CourseRank[1]}";
            value += $"\n算出日 {CalculationDate:yyyy/MM/dd}";
            return value;
        }
    }

    public class SchoolGrade
    {
        public List<ClassResult> ClassResults { get; set; } = new() { };
        public List<EvaluationCredit> EvaluationCredits { get; set; } = new() { };
        public double PreliminaryGPA => 1.0 * ClassResults.FindAll(x => x.Score != 0).Select(x => x.GP * x.Credit).Sum() / ClassResults.FindAll(x => x.Score != 0).Select(x => x.Credit).Sum();
        public DepartmentGPA DepartmentGPA { get; set; } = new();
        public List<YearCredit> YearCredits { get; set; } = new() { };
    }

    public class ClassTableRow
    {
        public ClassTableCell Monday { get; set; } = new();
        public ClassTableCell Tuesday { get; set; } = new();
        public ClassTableCell Wednesday { get; set; } = new();
        public ClassTableCell Thursday { get; set; } = new();
        public ClassTableCell Friday { get; set; } = new();

        public ClassTableCell this[int index]
        {
            get
            {
                return index switch
                {
                    0 => Monday,
                    1 => Tuesday,
                    2 => Wednesday,
                    3 => Thursday,
                    4 => Friday,
                    _ => new(),
                };
            }
            set
            {
                switch (index)
                {
                    case 0:
                        Monday = value;
                        break;
                    case 1:
                        Tuesday = value;
                        break;
                    case 2:
                        Wednesday = value;
                        break;
                    case 3:
                        Thursday = value;
                        break;
                    case 4:
                        Friday = value;
                        break;
                    default:
                        break;
                }
            }
        }

        public override string ToString() => $"{this[0].SubjectsName} {this[1].SubjectsName} {this[2].SubjectsName} {this[3].SubjectsName} {this[4].SubjectsName}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassTableRow objClassTableRow = (ClassTableRow)obj;
            return this[0].GetHashCode() == objClassTableRow[0].GetHashCode() && this[1].GetHashCode() == objClassTableRow[1].GetHashCode() && this[2].GetHashCode() == objClassTableRow[2].GetHashCode() && this[3].GetHashCode() == objClassTableRow[3].GetHashCode() && this[4].GetHashCode() == objClassTableRow[4].GetHashCode();
        }

        public override int GetHashCode() => this[0].GetHashCode() ^ this[1].GetHashCode() ^ this[2].GetHashCode() ^ this[3].GetHashCode() ^ this[4].GetHashCode();
    }

    public class ClassTableCell
    {
        public string SubjectsName { get; set; } = "";
        public string SubjectsId { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string SubjectsSection { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int Credit { get; set; }
        public string ClassName { get; set; } = "";
        public string ClassRoom { get; set; } = "";
        public string SyllabusURL { get; set; } = "";
        public string KamokuCode { get; set; } = "";
        public string ClassCode { get; set; } = "";

        public List<string> Favorites { get; set; } = new();

        public bool StackPanelVisible => SubjectsName != "" && SubjectsId != "";
        public bool ReportBadgeVisible => ReportCount > 0;
        public bool ReportBadgeOneDigits => ReportCount < 10;
        public int ReportCount { get; set; }
        public bool QuizBadgeVisible => QuizCount > 0;
        public bool QuizBadgeOneDigits => QuizCount < 10;
        public int QuizCount { get; set; }

        public override string ToString()
        {
            if (SubjectsId == "") { return ""; }
            if (ClassRoom == "") { return $"{SubjectsName} ({ClassName})\n{TeacherName}"; }
            return $"{SubjectsName} ({ClassName})\n{TeacherName}\n{ClassRoom}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassTableCell objClassTableCell = (ClassTableCell)obj;
            return SubjectsName == objClassTableCell.SubjectsName && SubjectsId == objClassTableCell.SubjectsId;
        }

        public override int GetHashCode() => SubjectsName.GetHashCode() ^ SubjectsId.GetHashCode();
    }
}
