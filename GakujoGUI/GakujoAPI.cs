using HtmlAgilityPack;
using Newtonsoft.Json;
using NLog;
using ReverseMarkdown;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
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
        private List<List<LotteryRegistration>> lotteryRegistrations = new() { };
        private List<List<LotteryRegistrationResult>> lotteryRegistrationsResult = new() { };
        private List<List<GeneralRegistration>> generalRegistrations = new() { };
        private SchoolGrade schoolGrade = new();
        private List<ClassTableRow> classTables = new();
        private bool loginStatus = false;

        public Account Account { get => account; set => account = value; }
        public List<Report> Reports { get => reports; set => reports = value; }
        public List<Quiz> Quizzes { get => quizzes; set => quizzes = value; }
        public List<ClassContact> ClassContacts { get => classContacts; set => classContacts = value; }
        public List<ClassSharedFile> ClassSharedFiles { get => classSharedFiles; set => classSharedFiles = value; }
        public List<List<LotteryRegistration>> LotteryRegistrations { get => lotteryRegistrations; set => lotteryRegistrations = value; }
        public List<List<LotteryRegistrationResult>> LotteryRegistrationsResult { get => lotteryRegistrationsResult; set => lotteryRegistrationsResult = value; }
        public List<List<GeneralRegistration>> GeneralRegistrations { get => generalRegistrations; set => generalRegistrations = value; }
        public SchoolGrade SchoolGrade { get => schoolGrade; set => schoolGrade = value; }
        public List<ClassTableRow> ClassTables { get => classTables; set => classTables = value; }
        public bool LoginStatus => loginStatus;

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

        private string lastCallerMemberName = "";

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
            if (File.Exists(GetJsonPath("LotteryRegistrations")))
            {
                LotteryRegistrations = JsonConvert.DeserializeObject<List<List<LotteryRegistration>>>(File.ReadAllText(GetJsonPath("LotteryRegistrations")))!;
            }
            if (File.Exists(GetJsonPath("LotteryRegistrationsResult")))
            {
                LotteryRegistrationsResult = JsonConvert.DeserializeObject<List<List<LotteryRegistrationResult>>>(File.ReadAllText(GetJsonPath("LotteryRegistrationsResult")))!;
            }
            if (File.Exists(GetJsonPath("GeneralRegistrations")))
            {
                GeneralRegistrations = JsonConvert.DeserializeObject<List<List<GeneralRegistration>>>(File.ReadAllText(GetJsonPath("GeneralRegistrations")))!;
            }
            if (File.Exists(GetJsonPath("SchoolGrade")))
            {
                SchoolGrade = JsonConvert.DeserializeObject<SchoolGrade>(File.ReadAllText(GetJsonPath("SchoolGrade")))!;
                logger.Info("Load SchoolGrade.");
            }
            if (File.Exists(GetJsonPath("ClassTables")))
            {
                ClassTables = JsonConvert.DeserializeObject<List<ClassTableRow>>(File.ReadAllText(GetJsonPath("ClassTables")))!;
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
            try { File.WriteAllText(GetJsonPath("LotteryRegistrations"), JsonConvert.SerializeObject(LotteryRegistrations, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save LotteryRegistrations."); }
            try { File.WriteAllText(GetJsonPath("LotteryRegistrationsResult"), JsonConvert.SerializeObject(LotteryRegistrationsResult, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save LotteryRegistrationsResult."); }
            try { File.WriteAllText(GetJsonPath("GeneralRegistrations"), JsonConvert.SerializeObject(GeneralRegistrations, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save GeneralRegistrations."); }
            try { File.WriteAllText(GetJsonPath("SchoolGrade"), JsonConvert.SerializeObject(SchoolGrade, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save SchoolGrade."); }
            try { File.WriteAllText(GetJsonPath("ClassTables"), JsonConvert.SerializeObject(ClassTables, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save ClassTables."); }
            try { File.WriteAllText(GetJsonPath("Account"), JsonConvert.SerializeObject(Account, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save Account."); }
        }

#pragma warning disable IDE0051 // 使用されていないプライベート メンバーを削除する
        private bool CheckDuplicateTransition([CallerMemberName] string callerMemberName = "")
#pragma warning restore IDE0051 // 使用されていないプライベート メンバーを削除する
        {
            if (lastCallerMemberName == callerMemberName) { return true; }
            else { lastCallerMemberName = callerMemberName; return false; }
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
            diffReports = new();
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
                if (!Reports.Contains(report)) { diffReports.Add(report); }
            }
            Reports.AddRange(diffReports);
            logger.Info($"Found {diffReports.Count} new Reports.");
            Reports.Where(x => !x.IsAcquired).ToList().ForEach(x => GetReport(x));
            Account.ReportDateTime = DateTime.Now;
            logger.Info("End Get Reports.");
            ApplyReportsClassTables();
            SaveJsons();
            SaveCookies();
        }

        public void GetReport(Report report)
        {
            logger.Info($"Start Get Report reportId={report.Id}.");
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
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/forwardSubmitRef");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&reportId={report.Id}&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear={schoolYear}&listSubjectCode={report.SubjectCode}&listClassCode=L0&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/forwardSubmitRef");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            report.EvaluationMethod = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[2].SelectSingleNode("td").InnerText;
            report.Description = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td").InnerHtml).Replace("<br>", " \r\n").Trim('\r').Trim('\n');
            report.Description = Regex.Replace(report.Description, "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline);
            report.Message = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").InnerHtml).Replace("<br>", " \r\n").Trim('\r').Trim('\n');
            report.Message = Regex.Replace(report.Message, "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").SelectNodes("a") != null)
            {
                report.Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").SelectNodes("a").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").SelectNodes("a").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").SelectNodes("a")[i];
                    string selectedKey = htmlNode.Attributes["onclick"].Value.Split(',')[0].Replace("fileDownload('", "").Replace("'", "");
                    string prefix = htmlNode.Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim();
                    httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classsupport/fileDownload/temporaryFileDownload?EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&selectedKey={selectedKey}&prefix={prefix}&EXCLUDE_SET=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                    logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classsupport/fileDownload/temporaryFileDownload?EXCLUDE_SET=");
                    logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                    Stream stream = httpResponseMessage.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath)) { Directory.CreateDirectory(downloadPath); }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    report.Files[i] = Path.Combine(downloadPath, htmlNode.InnerText.Trim());
                }
            }
            logger.Info($"End Get Report reportId={report.Id}.");
            SaveJsons();
            SaveCookies();
        }

        private void ApplyReportsClassTables()
        {
            logger.Info("Start Apply Reports to ClassTables.");
            if (ClassTables == null) { logger.Warn("Return Apply Reports to ClassTables by ClassTables is null."); return; }
            foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
            {
                for (int i = 0; i < 5; i++) { classTableRow[i].ReportCount = 0; }
            }
            foreach (Report report in Reports.Where(x => x.Unsubmitted))
            {
                foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
                {
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
            diffQuizzes = new();
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
                if (!Quizzes.Contains(quiz)) { diffQuizzes.Add(quiz); }
            }
            Quizzes.AddRange(diffQuizzes);
            logger.Info($"Found {diffQuizzes.Count} new Quizzes.");
            Quizzes.Where(x => !x.IsAcquired).ToList().ForEach(x => GetQuiz(x));
            Account.QuizDateTime = DateTime.Now;
            logger.Info("End Get Quizzes.");
            ApplyQuizzesClassTables();
            SaveJsons();
            SaveCookies();
        }

        public void GetQuiz(Quiz quiz)
        {
            logger.Info($"Start Get Quiz quizId={quiz.Id}.");
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
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/forwardSubmitRef");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&testId={quiz.Id}&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear={schoolYear}&listSubjectCode={quiz.SubjectCode}&listClassCode={quiz.ClassCode}&schoolYear={schoolYear}&semesterCode={(semesterCode < 2 ? 1 : 2)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/forwardSubmitRef");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            quiz.QuestionsCount = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[2].SelectSingleNode("td").InnerText.Replace("問", "").Trim());
            quiz.EvaluationMethod = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td").InnerText;
            quiz.Description = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").InnerHtml).Replace("<br>", " \r\n").Trim('\r').Trim('\n');
            quiz.Description = Regex.Replace(quiz.Description, "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline);
            quiz.Message = HttpUtility.HtmlDecode(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[6].SelectSingleNode("td").InnerHtml).Replace("<br>", " \r\n").Trim('\r').Trim('\n');
            quiz.Message = Regex.Replace(quiz.Message, "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").SelectNodes("a") != null)
            {
                quiz.Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").SelectNodes("a").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").SelectNodes("a").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").SelectNodes("a")[i];
                    string selectedKey = htmlNode.Attributes["onclick"].Value.Split(',')[0].Replace("fileDownload('", "").Replace("'", "");
                    string prefix = htmlNode.Attributes["onclick"].Value.Split(',')[1].Replace("');", "").Replace("'", "").Trim();
                    httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classsupport/fileDownload/temporaryFileDownload?EXCLUDE_SET=");
                    httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                    httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&selectedKey={selectedKey}&prefix={prefix}&EXCLUDE_SET=");
                    httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                    httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                    logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classsupport/fileDownload/temporaryFileDownload?EXCLUDE_SET=");
                    logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                    Stream stream = httpResponseMessage.Content.ReadAsStreamAsync().Result;
                    if (!Directory.Exists(downloadPath)) { Directory.CreateDirectory(downloadPath); }
                    using (FileStream fileStream = File.Create(Path.Combine(downloadPath, htmlNode.InnerText.Trim())))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fileStream);
                    }
                    quiz.Files[i] = Path.Combine(downloadPath, htmlNode.InnerText.Trim());
                }
            }
            logger.Info($"End Get Quiz quizId={quiz.Id}.");
            SaveJsons();
            SaveCookies();
        }

        private void ApplyQuizzesClassTables()
        {
            logger.Info("Start Apply Quizzes to ClassTables.");
            if (ClassTables == null) { logger.Warn("Return Apply Quizzes to ClassTables by ClassTables is null."); return; }
            foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
            {
                for (int i = 0; i < 5; i++) { classTableRow[i].QuizCount = 0; }
            }
            foreach (Quiz quiz in Quizzes.Where(x => x.Unsubmitted))
            {
                foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
                {
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

        private bool SetAcademicSystem(out bool lotteryRegistrationEnabled, out bool lotteryRegistrationResultEnabled, out bool generalRegistrationEnabled)
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
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            lotteryRegistrationEnabled = htmlDocument.DocumentNode.SelectNodes("//a[contains(@onclick,\"mainMenuCode=019&parentMenuCode=001\")]") != null;
            lotteryRegistrationResultEnabled = htmlDocument.DocumentNode.SelectNodes("//a[contains(@onclick,\"mainMenuCode=020&parentMenuCode=001\")]") != null;
            generalRegistrationEnabled = false;
            logger.Info("End Set AcademicSystem.");
            SaveJsons();
            SaveCookies();
            return true;
        }

        public void GetLotteryRegistrations(out string jikanwariVector)
        {
            logger.Info("Start Get LotteryRegistrations.");
            SetAcademicSystem(out bool lotteryRegistrationEnabled, out _, out _);
            jikanwariVector = "AA";
            if (!lotteryRegistrationEnabled) { logger.Warn("Not found LotteryRegistrations by overtime."); return; }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=019&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=019&parentMenuCode=001");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form") == null) { logger.Warn("Not found LotteryRegistrations."); return; }
            LotteryRegistrations = new();
            jikanwariVector = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/input").Attributes["value"].Value;
            for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table").Count; i++)
            {
                List<LotteryRegistration> lotteryRegistrations = new();
                HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table")[i].SelectSingleNode("tr/td/table");
                if (htmlNode == null) { continue; }
                for (int j = 2; j < htmlNode.SelectNodes("tr").Count; j++)
                {
                    LotteryRegistration lotteryRegistration = new();
                    lotteryRegistration.WeekdayPeriod = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[0].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistration.SubjectsName = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[1].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistration.SubjectsName = Regex.Replace(lotteryRegistration.SubjectsName, @" +", " ");
                    lotteryRegistration.ClassName = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[2].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistration.SubjectsSection = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[3].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistration.SelectionSection = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[4].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistration.Credit = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[5].InnerText.Trim());
                    lotteryRegistration.IsRegisterable = !htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].SelectSingleNode("input").Attributes.Contains("disabled");
                    lotteryRegistration.AttendingCapacity = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[10].InnerText.Replace("&nbsp;", "").Trim());
                    lotteryRegistration.FirstApplicantNumber = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[11].InnerText.Replace("&nbsp;", "").Trim());
                    lotteryRegistration.SecondApplicantNumber = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[12].InnerText.Replace("&nbsp;", "").Trim());
                    lotteryRegistration.ThirdApplicantNumber = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[13].InnerText.Replace("&nbsp;", "").Trim());
                    lotteryRegistration.ChoiceNumberKey = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].SelectSingleNode("input").Attributes["name"].Value;
                    if (htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].SelectSingleNode("input").Attributes.Contains("checked")) { lotteryRegistration.ChoiceNumberValue = 0; }
                    else if (htmlNode.SelectNodes("tr")[j].SelectNodes("td")[7].SelectSingleNode("input").Attributes.Contains("checked")) { lotteryRegistration.ChoiceNumberValue = 1; }
                    else if (htmlNode.SelectNodes("tr")[j].SelectNodes("td")[8].SelectSingleNode("input").Attributes.Contains("checked")) { lotteryRegistration.ChoiceNumberValue = 2; }
                    else if (htmlNode.SelectNodes("tr")[j].SelectNodes("td")[9].SelectSingleNode("input").Attributes.Contains("checked")) { lotteryRegistration.ChoiceNumberValue = 3; }
                    lotteryRegistrations.Add(lotteryRegistration);
                }
                switch (lotteryRegistrations[0].WeekdayPeriod[..1])
                {
                    case "月": ClassTables[(int.Parse(lotteryRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][0].LotteryRegistrations = lotteryRegistrations; break;
                    case "火": ClassTables[(int.Parse(lotteryRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][1].LotteryRegistrations = lotteryRegistrations; break;
                    case "水": ClassTables[(int.Parse(lotteryRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][2].LotteryRegistrations = lotteryRegistrations; break;
                    case "木": ClassTables[(int.Parse(lotteryRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][3].LotteryRegistrations = lotteryRegistrations; break;
                    case "金": ClassTables[(int.Parse(lotteryRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][4].LotteryRegistrations = lotteryRegistrations; break;
                }
                LotteryRegistrations.Add(lotteryRegistrations);
            }
            logger.Info("End Get LotteryRegistrations.");
            Account.LotteryRegistrationDateTime = DateTime.Now;
            SaveJsons();
            SaveCookies();
        }

        public void SetLotteryRegistrations(List<LotteryRegistrationEntry> lotteryRegistrationEntries, bool notifyMail = false)
        {
            logger.Info("Start Set LotteryRegistrations.");
            SetAcademicSystem(out bool lotteryRegistrationEnabled, out _, out _);
            if (!lotteryRegistrationEnabled) { logger.Warn("Return Set LotteryRegistrations by overtime."); return; }
            GetLotteryRegistrations(out string jikanwariVector);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuRegist.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            string choiceNumbers = "";
            foreach (LotteryRegistrationEntry lotteryRegistrationEntry in lotteryRegistrationEntries)
            {
                foreach (List<LotteryRegistration> lotteryRegistrations in LotteryRegistrations)
                {
                    if (lotteryRegistrations.Where(x => x.SubjectsName == lotteryRegistrationEntry.SubjectsName && x.ClassName == lotteryRegistrationEntry.ClassName && x.IsRegisterable).Count() == 1)
                    {
                        lotteryRegistrations.Where(x => x.ChoiceNumberValue == lotteryRegistrationEntry.AspirationOrder).ToList().ForEach(x => x.ChoiceNumberValue = 0);
                        lotteryRegistrations.Where(x => x.SubjectsName == lotteryRegistrationEntry.SubjectsName && x.ClassName == lotteryRegistrationEntry.ClassName && x.IsRegisterable).First().ChoiceNumberValue = lotteryRegistrationEntry.AspirationOrder;
                    }
                }
            }
            LotteryRegistrations.SelectMany(_ => _).Where(x => x.IsRegisterable).ToList().ForEach(x => { choiceNumbers += x.ToChoiceNumberString(); logger.Info($"ChoiceNumber {x.ToChoiceNumberString()}"); });
            httpRequestMessage.Content = new StringContent($"x=0&y=0&RishuuForm.jikanwariVector={jikanwariVector}{choiceNumbers}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuRegist.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            string selectedSemesterCode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[1]/tbody/tr/td[2]/a").Attributes["href"].Value.Split(',')[1].Replace("'", "").Replace(")", "").Trim();
            if (notifyMail)
            {
                httpRequestMessage = new(new("GET"), $"https://gakujo.shizuoka.ac.jp/kyoumu/sendChuusenRishuuMailInit.do?selectedSemesterCode={selectedSemesterCode}");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                logger.Info($"GET https://gakujo.shizuoka.ac.jp/kyoumu/sendChuusenRishuuMailInit.do?selectedSemesterCode={selectedSemesterCode}");
                logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
                htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
                string mailAddress = htmlDocument.DocumentNode.SelectSingleNode("//input[@name='mailAddress' and @checked]").Attributes["value"].Value;
                httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/sendChuusenRishuuMail.do");
                httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
                httpRequestMessage.Content = new StringContent($"{mailAddress}&button_changePassword.changePassword.x=0&button_changePassword.changePassword.y=0");
                httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
                httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
                logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/sendChuusenRishuuMail.do");
                logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            }
            logger.Info("End Set LotteryRegistrations.");
            SaveJsons();
            SaveCookies();
        }

        public void GetLotteryRegistrationsResult()
        {
            logger.Info("Start Get LotteryRegistrationsResult.");
            SetAcademicSystem(out _, out bool lotteryRegistrationResultEnabled, out _);
            if (!lotteryRegistrationResultEnabled) { logger.Warn("Not found LotteryRegistrationsResult by overtime."); return; }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=020&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=020&parentMenuCode=001");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form") == null) { logger.Warn("Not found LotteryRegistrationsResult."); return; }
            LotteryRegistrationsResult = new();
            for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table").Count; i++)
            {
                List<LotteryRegistrationResult> lotteryRegistrationsResult = new();
                HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table")[i].SelectSingleNode("tr/td/table");
                if (htmlNode == null) { continue; }
                for (int j = 1; j < htmlNode.SelectNodes("tr").Count; j++)
                {
                    LotteryRegistrationResult lotteryRegistrationResult = new();
                    lotteryRegistrationResult.WeekdayPeriod = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[0].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistrationResult.SubjectsName = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[1].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistrationResult.SubjectsName = Regex.Replace(lotteryRegistrationResult.SubjectsName, @" +", " ");
                    lotteryRegistrationResult.ClassName = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[2].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistrationResult.SubjectsSection = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[3].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistrationResult.SelectionSection = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[4].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
                    lotteryRegistrationResult.Credit = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[5].InnerText.Trim());
                    lotteryRegistrationResult.ChoiceNumberValue = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].InnerText.Trim().Replace("&nbsp;", ""));
                    lotteryRegistrationResult.IsWinning = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[7].InnerText.Contains("当選");
                    lotteryRegistrationsResult.Add(lotteryRegistrationResult);
                }
                switch (lotteryRegistrationsResult[0].WeekdayPeriod[..1])
                {
                    case "月": ClassTables[(int.Parse(lotteryRegistrationsResult[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][0].LotteryRegistrationsResult = lotteryRegistrationsResult; break;
                    case "火": ClassTables[(int.Parse(lotteryRegistrationsResult[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][1].LotteryRegistrationsResult = lotteryRegistrationsResult; break;
                    case "水": ClassTables[(int.Parse(lotteryRegistrationsResult[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][2].LotteryRegistrationsResult = lotteryRegistrationsResult; break;
                    case "木": ClassTables[(int.Parse(lotteryRegistrationsResult[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][3].LotteryRegistrationsResult = lotteryRegistrationsResult; break;
                    case "金": ClassTables[(int.Parse(lotteryRegistrationsResult[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][4].LotteryRegistrationsResult = lotteryRegistrationsResult; break;
                }
                LotteryRegistrationsResult.Add(lotteryRegistrationsResult);
            }
            logger.Info("End Get LotteryRegistrationsResult.");
            Account.LotteryRegistrationResultDateTime = DateTime.Now;
            SaveJsons();
            SaveCookies();
        }

        public void GetGeneralRegistrations()
        {
            //logger.Info("Start Get GeneralRegistrations.");
            //SetAcademicSystem(out _, out _, out bool generalRegistrationEnabled);
            //if (!generalRegistrationEnabled) { logger.Warn("Not found GeneralRegistrations by overtime."); return; }
            ////httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=020&parentMenuCode=001");
            //httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            //httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            ////logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/chuusenRishuuInit.do?mainMenuCode=020&parentMenuCode=001");
            //logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            //HtmlDocument htmlDocument = new();
            //htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            //if (htmlDocument.DocumentNode.SelectNodes("/html/body/form") == null) { logger.Warn("Not found GeneralRegistrations."); return; }
            //GeneralRegistrations = new();
            //for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table").Count; i++)
            //{
            //    List<GeneralRegistration> generalRegistrations = new();
            //    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table")[i].SelectSingleNode("tr/td/table");
            //    if (htmlNode == null) { continue; }
            //    for (int j = 1; j < htmlNode.SelectNodes("tr").Count; j++)
            //    {
            //        GeneralRegistration generalRegistration = new();
            //        //generalRegistration.WeekdayPeriod = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[0].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            //        //generalRegistration.SubjectsName = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[1].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            //        //generalRegistration.SubjectsName = Regex.Replace(generalRegistration.SubjectsName, @" +", " ");
            //        //generalRegistration.ClassName = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[2].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            //        //generalRegistration.SubjectsSection = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[3].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            //        //generalRegistration.SelectionSection = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[4].InnerText.Replace("\n", "").Replace("\t", "").Trim('　').Trim(' ');
            //        //generalRegistration.Credit = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[5].InnerText.Trim()); ;
            //        generalRegistrations.Add(generalRegistration);
            //    }
            //    switch (generalRegistrations[0].WeekdayPeriod[..1])
            //    {
            //        case "月": ClassTables[(int.Parse(generalRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][0].GeneralRegistrations = generalRegistrations; break;
            //        case "火": ClassTables[(int.Parse(generalRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][1].GeneralRegistrations = generalRegistrations; break;
            //        case "水": ClassTables[(int.Parse(generalRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][2].GeneralRegistrations = generalRegistrations; break;
            //        case "木": ClassTables[(int.Parse(generalRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][3].GeneralRegistrations = generalRegistrations; break;
            //        case "金": ClassTables[(int.Parse(generalRegistrations[0].WeekdayPeriod.Substring(1, 1)) + 1) / 2][4].GeneralRegistrations = generalRegistrations; break;
            //    }
            //    GeneralRegistrations.Add(generalRegistrations);
            //}
            //logger.Info("End Get GeneralRegistrations.");
            //Account.GeneralRegistrationDateTime = DateTime.Now;
            //SaveJsons();
            //SaveCookies();
        }

        public void GetClassResults(out List<ClassResult> diffClassResults)
        {
            logger.Info("Start Get ClassResults.");
            SetAcademicSystem(out _, out _, out _);
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
            SetAcademicSystem(out _, out _, out _);
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
                if (ClassTables.Count < i + 1) { ClassTables.Add(new()); }
                for (int j = 0; j < 5; j++)
                {
                    ClassTableCell classTableCell = (ClassTables[i] != null) ? ClassTables[i][j] : new();
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
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/detailKamoku.do?detailKamokuCode=" + $"{detailKamokuCode}&detailClassCode={detailClassCode}&gamen=jikanwari");
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
            logger.Info($"Start Get Syllabus schoolYear={schoolYear}, subjectCD={classTableCell.SubjectsId}, classCD={classTableCell.ClassCode}.");
            httpRequestMessage = new(new("GET"), $"https://gakujo.shizuoka.ac.jp/syllabus2/rishuuSyllabusSearch.do?schoolYear={schoolYear}&subjectCD={classTableCell.SubjectsId}&classCD={classTableCell.ClassCode}");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info($"GET https://gakujo.shizuoka.ac.jp/syllabus2/rishuuSyllabusSearch.do?schoolYear={schoolYear}&subjectCD={classTableCell.SubjectsId}&classCD={classTableCell.ClassCode}");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            classTableCell.Syllabus.SubjectsName = GetSyllabusValue(htmlDocument, "授業科目名");
            classTableCell.Syllabus.TeacherName = GetSyllabusValue(htmlDocument, "担当教員名");
            classTableCell.Syllabus.Affiliation = GetSyllabusValue(htmlDocument, "所属等");
            classTableCell.Syllabus.ResearchRoom = GetSyllabusValue(htmlDocument, "研究室");
            classTableCell.Syllabus.SharingTeacherName = GetSyllabusValue(htmlDocument, "分担教員名");
            classTableCell.Syllabus.ClassName = GetSyllabusValue(htmlDocument, "クラス");
            classTableCell.Syllabus.SemesterName = GetSyllabusValue(htmlDocument, "学期");
            classTableCell.Syllabus.SelectionSection = GetSyllabusValue(htmlDocument, "必修選択区分");
            classTableCell.Syllabus.TargetGrade = GetSyllabusValue(htmlDocument, "対象学年");
            classTableCell.Syllabus.Credit = GetSyllabusValue(htmlDocument, "単位数");
            classTableCell.Syllabus.WeekdayPeriod = GetSyllabusValue(htmlDocument, "曜日・時限");
            classTableCell.Syllabus.ClassRoom = GetSyllabusValue(htmlDocument, "教室");
            classTableCell.Syllabus.Keyword = GetSyllabusValue(htmlDocument, "キーワード");
            classTableCell.Syllabus.ClassTarget = GetSyllabusValue(htmlDocument, "授業の目標", true);
            classTableCell.Syllabus.LearningDetail = GetSyllabusValue(htmlDocument, "学習内容", true);
            classTableCell.Syllabus.ClassPlan = GetSyllabusValue(htmlDocument, "授業計画", true);
            classTableCell.Syllabus.Textbook = GetSyllabusValue(htmlDocument, "テキスト");
            classTableCell.Syllabus.ReferenceBook = GetSyllabusValue(htmlDocument, "参考書");
            classTableCell.Syllabus.PreparationReview = GetSyllabusValue(htmlDocument, "予習・復習について");
            classTableCell.Syllabus.EvaluationMethod = GetSyllabusValue(htmlDocument, "成績評価の方法･基準");
            classTableCell.Syllabus.OfficeHour = GetSyllabusValue(htmlDocument, "オフィスアワー");
            classTableCell.Syllabus.Message = GetSyllabusValue(htmlDocument, "担当教員からのメッセージ");
            classTableCell.Syllabus.ActiveLearning = GetSyllabusValue(htmlDocument, "アクティブ・ラーニング");
            classTableCell.Syllabus.TeacherPracticalExperience = GetSyllabusValue(htmlDocument, "実務経験のある教員の有無");
            classTableCell.Syllabus.TeacherCareerClassDetail = GetSyllabusValue(htmlDocument, "実務経験のある教員の経歴と授業内容");
            classTableCell.Syllabus.TeachingProfessionSection = GetSyllabusValue(htmlDocument, "教職科目区分");
            classTableCell.Syllabus.RelatedClassSubjects = GetSyllabusValue(htmlDocument, "関連授業科目");
            classTableCell.Syllabus.Other = GetSyllabusValue(htmlDocument, "その他");
            classTableCell.Syllabus.HomeClassStyle = GetSyllabusValue(htmlDocument, "在宅授業形態");
            classTableCell.Syllabus.HomeClassStyleDetail = GetSyllabusValue(htmlDocument, "在宅授業形態（詳細）");
            logger.Info($"End Get Syllabus schoolYear={schoolYear}, subjectCD={classTableCell.SubjectsId}, classCD={classTableCell.ClassCode}.");
            logger.Info($"End Get ClassTableCell detailKamokuCode={detailKamokuCode}, detailClassCode={detailClassCode}.");
            return classTableCell;
        }

        private static string GetSyllabusValue(HtmlDocument htmlDocument, string key, bool convert = false)
        {
            if (htmlDocument.DocumentNode.SelectSingleNode($"//font[contains(text(), \"{key}\")]/../following-sibling::td") == null) { return ""; }
            string value;
            if (!convert) { value = htmlDocument.DocumentNode.SelectSingleNode($"//font[contains(text(), \"{key}\")]/../following-sibling::td").InnerText.Replace("\n", "").Replace("\t", "").Replace("&nbsp;", " ").Trim('　').Trim(' '); }
            else
            {
                Config config = new()
                {
                    UnknownTags = Config.UnknownTagsOption.Bypass,
                    GithubFlavored = true,
                    RemoveComments = true,
                    SmartHrefHandling = true,
                };
                value = new Converter(config).Convert(htmlDocument.DocumentNode.SelectSingleNode($"//font[contains(text(), \"{key}\")]/../following-sibling::td").InnerHtml);
            }
            return Regex.Replace(Regex.Replace(value, @" +", " ").Replace("|\r\n\n \n |", "|\r\n|"), "(?<=[^|])\\r\\n(?=[^|])", "  \r\n");
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
        public DateTime LotteryRegistrationDateTime { get; set; }
        public DateTime LotteryRegistrationResultDateTime { get; set; }
        public DateTime GeneralRegistrationDateTime { get; set; }
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
        public string EvaluationMethod { get; set; } = "";
        public string Description { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string Message { get; set; } = "";
        public bool IsAcquired => EvaluationMethod != "";

        public bool Unsubmitted => Status == "受付中" && SubmittedDateTime == new DateTime();

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
            if (obj == null || GetType() != obj.GetType()) { return false; }
            Report objReport = (Report)obj;
            return SubjectCode == objReport.SubjectCode && ClassCode == objReport.ClassCode && Id == objReport.Id;
        }

        public override int GetHashCode()
        {
            return SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ Id.GetHashCode();
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
        public string Id { get; set; } = "";
        public string SchoolYear { get; set; } = "";
        public string SubjectCode { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public int QuestionsCount { get; set; }
        public string EvaluationMethod { get; set; } = "";
        public string Description { get; set; } = "";
        public string[] Files { get; set; } = Array.Empty<string>();
        public string Message { get; set; } = "";
        public bool IsAcquired => EvaluationMethod != "";

        public bool Unsubmitted => Status == "受付中" && SubmissionStatus == "未提出";

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
            if (obj == null || GetType() != obj.GetType()) { return false; }
            Quiz objQuiz = (Quiz)obj;
            return SubjectCode == objQuiz.SubjectCode && ClassCode == objQuiz.ClassCode && Id == objQuiz.Id;
        }

        public override int GetHashCode()
        {
            return SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ Id.GetHashCode();
        }
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
        public bool IsAcquired => Content != "";

        public override string ToString()
        {
            return $"{Subjects.Split(' ')[0]} {Title} {ContactDateTime.ToShortDateString()}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassContact objClassContact = (ClassContact)obj;
            return Subjects == objClassContact.Subjects && Title == objClassContact.Title && ContactDateTime == objClassContact.ContactDateTime;
        }

        public override int GetHashCode()
        {
            return Subjects.GetHashCode() ^ Title.GetHashCode() ^ ContactDateTime.GetHashCode();
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
        public bool IsAcquired => Files.Length != 0;

        public override string ToString()
        {
            return $"{Subjects.Split(' ')[0]} {Title} {UpdateDateTime.ToShortDateString()}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassSharedFile objClassSharedFile = (ClassSharedFile)obj;
            return Subjects == objClassSharedFile.Subjects && Title == objClassSharedFile.Title && UpdateDateTime == objClassSharedFile.UpdateDateTime;
        }

        public override int GetHashCode()
        {
            return Subjects.GetHashCode() ^ Title.GetHashCode() ^ UpdateDateTime.GetHashCode();
        }
    }

    public class LotteryRegistration
    {
        public string WeekdayPeriod { get; set; } = "";
        public string SubjectsName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string SubjectsSection { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int Credit { get; set; }
        public bool IsRegisterable { get; set; }
        public int AttendingCapacity { get; set; }
        public int FirstApplicantNumber { get; set; }
        public int SecondApplicantNumber { get; set; }
        public int ThirdApplicantNumber { get; set; }
        public string ChoiceNumberKey { get; set; } = "";
        public int ChoiceNumberValue { get; set; }

        public override string ToString()
        {
            return $"{SubjectsName} {ClassName} {AttendingCapacity} 1:{FirstApplicantNumber} 2:{SecondApplicantNumber} 3:{ThirdApplicantNumber}";
        }

        public string ToChoiceNumberString()
        {
            return $"&{ChoiceNumberKey}={ChoiceNumberValue}";
        }
    }

    public class LotteryRegistrationEntry
    {
        public string SubjectsName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public int AspirationOrder { get; set; }

        public override string ToString()
        {
            return $"{SubjectsName} {ClassName} [{AspirationOrder}]";
        }
    }

    public class LotteryRegistrationResult
    {
        public string WeekdayPeriod { get; set; } = "";
        public string SubjectsName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string SubjectsSection { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int Credit { get; set; }
        public int ChoiceNumberValue { get; set; }
        public bool IsWinning { get; set; }

        public override string ToString()
        {
            return $"{SubjectsName} {ClassName} {ChoiceNumberValue} {(IsWinning ? "*" : "")}";
        }
    }

    public class GeneralRegistration
    {
        //public string WeekdayPeriod { get; set; } = "";
        //public string SubjectsName { get; set; } = "";
        //public string ClassName { get; set; } = "";
        //public string SubjectsSection { get; set; } = "";
        //public string SelectionSection { get; set; } = "";
        //public int Credit { get; set; }
        //public bool IsRegisterable { get; set; }

        //public override string ToString()
        //{
        //    return $"{SubjectsName} {ClassName} {(IsRegisterable ? "*" : "")}";
        //}
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

        public override string ToString()
        {
            return $"{Subjects} {Score} ({Evaluation}) {GP} {ReportDate.ToShortDateString()}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassResult objClassResult = (ClassResult)obj;
            return Subjects == objClassResult.Subjects && AcquisitionYear == objClassResult.AcquisitionYear;
        }

        public override int GetHashCode()
        {
            return Subjects.GetHashCode() ^ AcquisitionYear.GetHashCode();
        }
    }

    public class EvaluationCredit
    {
        public string Evaluation { get; set; } = "";
        public int Credit { get; set; }

        public override string ToString()
        {
            return $"{Evaluation} {Credit}";
        }
    }

    public class YearCredit
    {
        public string Year { get; set; } = "";
        public int Credit { get; set; }

        public override string ToString()
        {
            return $"{Year} {Credit}";
        }
    }

    public class SemesterGPA
    {
        public string Year { get; set; } = "";
        public string Semester { get; set; } = "";
        public double GPA { get; set; }

        public override string ToString()
        {
            return $"{Year}{Semester} {GPA}";
        }
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
            SemesterGPAs.ForEach(x => value += $"\n{x}");
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
        public double PreliminaryGPA => 1.0 * ClassResults.Where(x => x.Score != 0).Select(x => x.GP * x.Credit).Sum() / ClassResults.Where(x => x.Score != 0).Select(x => x.Credit).Sum();
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
            get => index switch
            {
                0 => Monday,
                1 => Tuesday,
                2 => Wednesday,
                3 => Thursday,
                4 => Friday,
                _ => new(),
            };
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

        public override string ToString()
        {
            return $"{this[0].SubjectsName} {this[1].SubjectsName} {this[2].SubjectsName} {this[3].SubjectsName} {this[4].SubjectsName}";
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            ClassTableRow objClassTableRow = (ClassTableRow)obj;
            return this[0].GetHashCode() == objClassTableRow[0].GetHashCode() && this[1].GetHashCode() == objClassTableRow[1].GetHashCode() && this[2].GetHashCode() == objClassTableRow[2].GetHashCode() && this[3].GetHashCode() == objClassTableRow[3].GetHashCode() && this[4].GetHashCode() == objClassTableRow[4].GetHashCode();
        }

        public override int GetHashCode()
        {
            return this[0].GetHashCode() ^ this[1].GetHashCode() ^ this[2].GetHashCode() ^ this[3].GetHashCode() ^ this[4].GetHashCode();
        }
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

        public Syllabus Syllabus { get; set; } = new();

        public List<LotteryRegistration> LotteryRegistrations { get; set; } = new();
        public List<LotteryRegistrationResult> LotteryRegistrationsResult { get; set; } = new();
        public List<GeneralRegistration> GeneralRegistrations { get; set; } = new();

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

        public override int GetHashCode()
        {
            return SubjectsName.GetHashCode() ^ SubjectsId.GetHashCode();
        }
    }

    public class Syllabus
    {
        public string SubjectsName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string Affiliation { get; set; } = "";
        public string ResearchRoom { get; set; } = "";
        public string SharingTeacherName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string SemesterName { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public string TargetGrade { get; set; } = "";
        public string Credit { get; set; } = "";
        public string WeekdayPeriod { get; set; } = "";
        public string ClassRoom { get; set; } = "";
        public string Keyword { get; set; } = "";
        public string ClassTarget { get; set; } = "";
        public string LearningDetail { get; set; } = "";
        public string ClassPlan { get; set; } = "";
        public string ClassRequirement { get; set; } = "";
        public string Textbook { get; set; } = "";
        public string ReferenceBook { get; set; } = "";
        public string PreparationReview { get; set; } = "";
        public string EvaluationMethod { get; set; } = "";
        public string OfficeHour { get; set; } = "";
        public string Message { get; set; } = "";
        public string ActiveLearning { get; set; } = "";
        public string TeacherPracticalExperience { get; set; } = "";
        public string TeacherCareerClassDetail { get; set; } = "";
        public string TeachingProfessionSection { get; set; } = "";
        public string RelatedClassSubjects { get; set; } = "";
        public string Other { get; set; } = "";
        public string HomeClassStyle { get; set; } = "";
        public string HomeClassStyleDetail { get; set; } = "";

        public override string ToString()
        {
            string value = $"## {SubjectsName}\n";
            value += $"|担当教員名|所属等|研究室|分担教員名|\n";
            value += $"|-|-|-|-|\n";
            value += $"|{TeacherName}|{Affiliation}|{ResearchRoom}|{SharingTeacherName}|\n";
            value += "\n";
            value += $"|クラス|学期|必修選択区分|\n";
            value += $"|-|-|-|\n";
            value += $"|{ClassName}|{SemesterName}|{SelectionSection}|\n";
            value += "\n";
            value += $"|対象学年|単位数|曜日・時限|\n";
            value += $"|-|-|-|\n";
            value += $"|{TargetGrade}|{Credit}|{WeekdayPeriod}|\n";
            value += $"### 教室\n";
            value += $"{ClassRoom}  \n";
            value += $"### キーワード\n";
            value += $"{Keyword}  \n";
            value += $"### 授業の目標\n";
            value += $"{ClassTarget}  \n";
            value += $"### 学習内容\n";
            value += $"{LearningDetail}  \n";
            value += $"### 授業計画\n";
            value += $"{ClassPlan}  \n";
            value += $"### 受講要件\n";
            value += $"{ClassRequirement}  \n";
            value += $"### テキスト\n";
            value += $"{Textbook}  \n";
            value += $"### 参考書\n";
            value += $"{ReferenceBook}  \n";
            value += $"### 予習・復習について\n";
            value += $"{PreparationReview}  \n";
            value += $"### 成績評価の方法･基準\n";
            value += $"{EvaluationMethod}  \n";
            value += $"### オフィスアワー\n";
            value += $"{OfficeHour}  \n";
            value += $"### 担当教員からのメッセージ\n";
            value += $"{Message}  \n";
            value += $"### アクティブ・ラーニング\n";
            value += $"{ActiveLearning}  \n";
            value += $"### 実務経験のある教員の有無\n";
            value += $"{TeacherPracticalExperience}  \n";
            value += $"### 実務経験のある教員の経歴と授業内容\n";
            value += $"{TeacherCareerClassDetail}  \n";
            value += $"### 教職科目区分\n";
            value += $"{TeachingProfessionSection}  \n";
            value += $"### 関連授業科目\n";
            value += $"{RelatedClassSubjects}  \n";
            value += $"### その他\n";
            value += $"{Other}  \n";
            value += $"### 在宅授業形態\n";
            value += $"{HomeClassStyle}  \n";
            value += $"### 在宅授業形態(詳細)\n";
            value += $"{HomeClassStyleDetail}  \n";
            return value;
        }

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            Syllabus objSyllabus = (Syllabus)obj;
            return SubjectsName == objSyllabus.SubjectsName && TeacherName == objSyllabus.TeacherName;
        }

        public override int GetHashCode()
        {
            return SubjectsName.GetHashCode() ^ TeacherName.GetHashCode();
        }
    }
}
