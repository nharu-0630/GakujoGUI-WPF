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
        private List<LotteryRegistrationEntry> lotteryRegistrationEntries = new();
        private List<GeneralRegistrationEntry> generalRegistrationEntries = new();
        private SchoolGrade schoolGrade = new();
        private List<ClassTableRow> classTables = new();
        private bool loginStatus = false;

        public Account Account { get => account; set => account = value; }
        public List<Report> Reports { get => reports; set => reports = value; }
        public List<Quiz> Quizzes { get => quizzes; set => quizzes = value; }
        public List<ClassContact> ClassContacts { get => classContacts; set => classContacts = value; }
        public List<ClassSharedFile> ClassSharedFiles { get => classSharedFiles; set => classSharedFiles = value; }
        public List<List<LotteryRegistration>> LotteryRegistrations => ClassTables.Select(x => x.LotteryRegistrations).ToList();
        public List<List<LotteryRegistrationResult>> LotteryRegistrationsResult => ClassTables.Select(x => x.LotteryRegistrationsResult).ToList();
        public List<List<GeneralRegistration>> RegisterableGeneralRegistrations => ClassTables.Select(x => x.RegisterableGeneralRegistrations).ToList();
        public List<LotteryRegistrationEntry> LotteryRegistrationEntries { get => lotteryRegistrationEntries; set => lotteryRegistrationEntries = value; }
        public List<GeneralRegistrationEntry> GeneralRegistrationEntries { get => generalRegistrationEntries; set => generalRegistrationEntries = value; }
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
        private string SchoolYearSemesterCodeSuffix => $"_{schoolYear}_{ReplaceSemesterCode(semesterCode)}";
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

        private static string ReplaceColon(string value) => value.Replace("&#x3a;", ":").Trim();

        private static string ReplaceSpace(string value) => Regex.Replace(Regex.Replace(value.Replace("\r", "").Replace("\n", "").Replace("\t", "").Replace("&nbsp;", "").Trim(), @"\s+", " "), @" +", " ").Trim();

        private static string ReplaceJSArgs(string value, int index) => value.Split(',')[index].Replace("'", "").Replace("(", "").Replace(")", "").Replace(";", "").Trim();

        private static DateTime ReplaceTimeSpan(string value, int index) => DateTime.Parse(value.Trim().Split('～')[index]);

        private static string ReplaceHtmlNewLine(string value) => Regex.Replace(HttpUtility.HtmlDecode(value).Replace("<br>", " \r\n").Trim('\r').Trim('\n'), "[\\r\\n]+", Environment.NewLine, RegexOptions.Multiline).Trim();

        private static int ReplaceSemesterCode(int value) => (value < 2 ? 1 : 2);

        private static int ReplaceWeekday(string value)
        {
            return value[..1] switch
            {
                "月" => 0,
                "火" => 1,
                "水" => 2,
                "木" => 3,
                "金" => 4,
                _ => -1,
            };
        }

        private static int ReplacePeriod(string value) => (int.Parse(value.Substring(1, 1)) + 1) / 2;

        private static string ReplaceWeekday(int index) => (new string[] { "月", "火", "水", "木", "金" })[index];

        private static string ReplacePeriod(int index) => (new string[] { "1･2", "3･4", "5･6", "7･8", "9･10", "11･12", "13･14" })[index];

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
                try
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
                }
                catch (Exception exception) { logger.Error(exception, "Error Load Cookies."); }
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
            if (File.Exists(GetJsonPath("lotteryRegistrationEntries")))
            {
                lotteryRegistrationEntries = JsonConvert.DeserializeObject<List<LotteryRegistrationEntry>>(File.ReadAllText(GetJsonPath("lotteryRegistrationEntries")))!;
                logger.Info("Load lotteryRegistrationEntries.");
            }
            if (File.Exists(GetJsonPath("GeneralRegistrationEntries")))
            {
                GeneralRegistrationEntries = JsonConvert.DeserializeObject<List<GeneralRegistrationEntry>>(File.ReadAllText(GetJsonPath("GeneralRegistrationEntries")))!;
                logger.Info("Load GeneralRegistrationEntries.");
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
            try { File.WriteAllText(GetJsonPath("LotteryRegistrationEntries"), JsonConvert.SerializeObject(LotteryRegistrationEntries, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save LotteryRegistrationEntries."); }
            try { File.WriteAllText(GetJsonPath("GeneralRegistrationEntries"), JsonConvert.SerializeObject(GeneralRegistrationEntries, Formatting.Indented)); }
            catch (Exception exception) { logger.Error(exception, "Error Save GeneralRegistrationEntries."); }
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
                string relayState = ReplaceColon(htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[1]").Attributes["value"].Value);
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
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&reportId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
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
                report.Subjects = ReplaceSpace(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText);
                report.Title = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText.Trim();
                report.Id = ReplaceJSArgs(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value, 1);
                report.SchoolYear = ReplaceJSArgs(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value, 3);
                report.SubjectCode = ReplaceJSArgs(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value, 4);
                report.ClassCode = ReplaceJSArgs(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value, 5);
                report.Status = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                report.StartDateTime = ReplaceTimeSpan(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText, 0);
                report.EndDateTime = ReplaceTimeSpan(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText, 1);
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
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&reportId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/search");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/forwardSubmitRef");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&reportId={report.Id}&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear={schoolYear}&listSubjectCode={report.SubjectCode}&listClassCode=L0&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A02_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/report/student/searchList/forwardSubmitRef");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            report.EvaluationMethod = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[2].SelectSingleNode("td").InnerText;
            report.Description = ReplaceHtmlNewLine(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td").InnerHtml);
            report.Message = ReplaceHtmlNewLine(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").InnerHtml);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").SelectNodes("a") != null)
            {
                report.Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").SelectNodes("a").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").SelectNodes("a").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").SelectNodes("a")[i];
                    string selectedKey = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 0).Replace("fileDownload", "");
                    string prefix = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 1);
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
                foreach (ClassTableCell classTableCell in classTableRow) { classTableCell.ReportCount = 0; }
            }
            foreach (Report report in Reports.Where(x => x.Unsubmitted))
            {
                foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
                {
                    foreach (ClassTableCell classTableCell in classTableRow) { if (report.Subjects.Contains($"{classTableCell.SubjectsName}（{classTableCell.ClassName}）")) { classTableCell.ReportCount++; } }
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
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&testId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
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
                quiz.Subjects = ReplaceSpace(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[0].InnerText);
                quiz.Title = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").InnerText.Trim();
                quiz.Id = ReplaceJSArgs(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value, 1);
                quiz.SchoolYear = ReplaceJSArgs(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value, 3);
                quiz.SubjectCode = ReplaceJSArgs(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value, 4);
                quiz.ClassCode = ReplaceJSArgs(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].SelectSingleNode("a").Attributes["onclick"].Value, 5);
                quiz.Status = htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[2].InnerText.Trim();
                quiz.StartDateTime = ReplaceTimeSpan(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText, 0);
                quiz.EndDateTime = ReplaceTimeSpan(htmlDocument.GetElementbyId("searchList").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[3].InnerText, 1);
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
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&testId=&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear=&listSubjectCode=&listClassCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=-1&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/search");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/forwardSubmitRef");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&testId={quiz.Id}&hidSchoolYear=&hidSemesterCode=&hidSubjectCode=&hidClassCode=&entranceDiv=&backPath=&listSchoolYear={schoolYear}&listSubjectCode={quiz.SubjectCode}&listClassCode={quiz.ClassCode}&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&operationFormat=1&operationFormat=2&searchList_length=10&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A03_01_G&_screenInfoDisp=&_scrollTop=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/test/student/searchList/forwardSubmitRef");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            quiz.QuestionsCount = int.Parse(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[2].SelectSingleNode("td").InnerText.Replace("問", "").Trim());
            quiz.EvaluationMethod = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td").InnerText;
            quiz.Description = ReplaceHtmlNewLine(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").InnerHtml);
            quiz.Message = ReplaceHtmlNewLine(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[6].SelectSingleNode("td").InnerHtml);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").SelectNodes("a") != null)
            {
                quiz.Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").SelectNodes("a").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").SelectNodes("a").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form/div[3]/div/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").SelectNodes("a")[i];
                    string selectedKey = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 0).Replace("fileDownload", "");
                    string prefix = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 1);
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
                foreach (ClassTableCell classTableCell in classTableRow) { classTableCell.QuizCount = 0; }
            }
            foreach (Quiz quiz in Quizzes.Where(x => x.Unsubmitted))
            {
                foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
                {
                    foreach (ClassTableCell classTableCell in classTableRow) { if (quiz.Subjects.Contains($"{classTableCell.SubjectsName}（{classTableCell.ClassName}）")) { classTableCell.QuizCount++; } }
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
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={ReportDateStart}");
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
                classContact.Subjects = ReplaceSpace(htmlDocument.GetElementbyId("tbl_A01_01").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText);
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
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={ReportDateStart}");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/selectClassContactList");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/goDetail/" + indexCount);
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            string content = $"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&teacherCode=&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&checkSearchKeywordTeacherUserName=on&checkSearchKeywordSubjectName=on&checkSearchKeywordTitle=on&contactKindCode=&targetDateStart=&targetDateEnd=&reportDateStart={schoolYear}/01/01&reportDateEnd=&requireResponse=&studentCode=&studentName=&tbl_A01_01_length=-1&_searchConditionDisp.accordionSearchCondition=false&_screenIdentifier=SC_A01_01&_screenInfoDisp=true&_scrollTop=0";
            httpRequestMessage.Content = new StringContent(content);
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/portal/classcontact/classContactList/goDetail/" + indexCount);
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            Account.ApacheToken = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[1]/form[1]/div/input").Attributes["value"].Value;
            ClassContacts[indexCount].ContactType = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[0].SelectSingleNode("td").InnerText;
            ClassContacts[indexCount].Content = ReplaceHtmlNewLine(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[2].SelectSingleNode("td").InnerText);
            ClassContacts[indexCount].FileLinkRelease = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[4].SelectSingleNode("td").InnerText);
            ClassContacts[indexCount].ReferenceURL = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[5].SelectSingleNode("td").InnerText);
            ClassContacts[indexCount].Severity = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[6].SelectSingleNode("td").InnerText);
            ClassContacts[indexCount].WebReplyRequest = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[8].SelectSingleNode("td").InnerText;
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div") != null)
            {
                ClassContacts[indexCount].Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div/div/form/div[3]/div/div/div/table").SelectNodes("tr")[3].SelectSingleNode("td/div").SelectNodes("div")[i];
                    string prefix = ReplaceJSArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 0).Replace("fileDownLoad", "");
                    string no = ReplaceJSArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 1);
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
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
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
                classSharedFile.Subjects = ReplaceSpace(htmlDocument.GetElementbyId("tbl_classFile").SelectSingleNode("tbody").SelectNodes("tr")[i].SelectNodes("td")[1].InnerText);
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
            httpRequestMessage.Content = new StringContent($"org.apache.struts.taglib.html.TOKEN={Account.ApacheToken}&schoolYear={schoolYear}&semesterCode={ReplaceSemesterCode(semesterCode)}&subjectDispCode=&searchKeyWord=&searchScopeTitle=Y&lastUpdateDate=&tbl_classFile_length=-1&linkDetailIndex=0&selectIndex=&prevPageId=backToList&confirmMsg=&_searchConditionDisp.accordionSearchCondition=true&_screenIdentifier=SC_A08_01&_screenInfoDisp=true&_scrollTop=0");
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
            ClassSharedFiles[indexCount].PublicPeriod = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[3].SelectSingleNode("td").InnerText);
            if (htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div") != null)
            {
                ClassSharedFiles[indexCount].Files = new string[htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div").Count];
                for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/div[2]/div[1]/div/form[2]/div[2]/div[2]/div/div/div/table[1]").SelectNodes("tr")[1].SelectSingleNode("td/div").SelectNodes("div")[i];
                    string prefix = ReplaceJSArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 0).Replace("fileDownLoad", "");
                    string no = ReplaceJSArgs(htmlNode.SelectSingleNode("a").Attributes["onclick"].Value, 1);
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
                string relayState = ReplaceColon(htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/div/input[1]").Attributes["value"].Value);
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
            generalRegistrationEnabled = htmlDocument.DocumentNode.SelectNodes("//a[contains(@onclick,\"mainMenuCode=002&parentMenuCode=001\")]") != null;
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
            foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
            {
                foreach (ClassTableCell classTableCell in classTableRow) { classTableCell.LotteryRegistrations.Clear(); }
            }
            jikanwariVector = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/input").Attributes["value"].Value;
            for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table").Count; i++)
            {
                List<LotteryRegistration> lotteryRegistrations = new();
                HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table")[i].SelectSingleNode("tr/td/table");
                if (htmlNode == null) { continue; }
                for (int j = 2; j < htmlNode.SelectNodes("tr").Count; j++)
                {
                    LotteryRegistration lotteryRegistration = new();
                    lotteryRegistration.WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[0].InnerText);
                    lotteryRegistration.SubjectsName = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[1].InnerText);
                    lotteryRegistration.ClassName = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[2].InnerText);
                    lotteryRegistration.SubjectsSection = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[3].InnerText);
                    lotteryRegistration.SelectionSection = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[4].InnerText);
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
                ClassTables[ReplacePeriod(lotteryRegistrations[0].WeekdayPeriod)][ReplaceWeekday(lotteryRegistrations[0].WeekdayPeriod)].LotteryRegistrations = lotteryRegistrations;
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
            string selectedSemesterCode = ReplaceJSArgs(htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[1]/tbody/tr/td[2]/a").Attributes["href"].Value, 1);
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
            foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
            {
                foreach (ClassTableCell classTableCell in classTableRow) { classTableCell.LotteryRegistrationsResult.Clear(); }
            }
            for (int i = 0; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table").Count; i++)
            {
                List<LotteryRegistrationResult> lotteryRegistrationsResult = new();
                HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form").SelectNodes("table")[i].SelectSingleNode("tr/td/table");
                if (htmlNode == null) { continue; }
                for (int j = 1; j < htmlNode.SelectNodes("tr").Count; j++)
                {
                    LotteryRegistrationResult lotteryRegistrationResult = new();
                    lotteryRegistrationResult.WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[0].InnerText);
                    lotteryRegistrationResult.SubjectsName = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[1].InnerText);
                    lotteryRegistrationResult.ClassName = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[2].InnerText);
                    lotteryRegistrationResult.SubjectsSection = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[3].InnerText);
                    lotteryRegistrationResult.SelectionSection = ReplaceSpace(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[4].InnerText);
                    lotteryRegistrationResult.Credit = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[5].InnerText.Trim());
                    lotteryRegistrationResult.ChoiceNumberValue = int.Parse(htmlNode.SelectNodes("tr")[j].SelectNodes("td")[6].InnerText.Replace("&nbsp;", "").Trim());
                    lotteryRegistrationResult.IsWinning = htmlNode.SelectNodes("tr")[j].SelectNodes("td")[7].InnerText.Contains("当選");
                    lotteryRegistrationsResult.Add(lotteryRegistrationResult);
                }
                ClassTables[ReplacePeriod(lotteryRegistrationsResult[0].WeekdayPeriod)][ReplaceWeekday(lotteryRegistrationsResult[0].WeekdayPeriod)].LotteryRegistrationsResult = lotteryRegistrationsResult;
            }
            logger.Info("End Get LotteryRegistrationsResult.");
            Account.LotteryRegistrationResultDateTime = DateTime.Now;
            SaveJsons();
            SaveCookies();
        }

        public void GetGeneralRegistrations()
        {
            logger.Info("Start Get GeneralRegistrations.");
            SetAcademicSystem(out _, out _, out bool generalRegistrationEnabled);
            if (!generalRegistrationEnabled) { logger.Warn("Not found GeneralRegistrations by overtime."); return; }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/table[4]") == null) { logger.Warn("Not found GeneralRegistrations."); return; }
            foreach (ClassTableRow classTableRow in ClassTables.Where(x => x != null))
            {
                foreach (ClassTableCell classTableCell in classTableRow) { classTableCell.GeneralRegistrations = new(); }
            }
            for (int i = 0; i < 7; i++)
            {
                HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1];
                for (int j = 0; j < 5; j++)
                {
                    GeneralRegistrations generalRegistrations = new();
                    generalRegistrations.EntriedGeneralRegistration.WeekdayPeriod = ReplaceWeekday(j) + ReplacePeriod(i);
                    if (htmlNode.SelectNodes("td")[j + 1].SelectSingleNode("a") != null)
                    {
                        generalRegistrations.EntriedGeneralRegistration.SubjectsName = ReplaceSpace(htmlNode.SelectNodes("td")[j + 1].SelectSingleNode("a").InnerText);
                        generalRegistrations.EntriedGeneralRegistration.TeacherName = ReplaceSpace(htmlNode.SelectNodes("td")[j + 1].SelectNodes("text()")[0].InnerText);
                        generalRegistrations.EntriedGeneralRegistration.SelectionSection = ReplaceSpace(htmlNode.SelectNodes("td")[j + 1].SelectNodes("font")[0].InnerText);
                        generalRegistrations.EntriedGeneralRegistration.Credit = int.Parse(htmlNode.SelectNodes("td")[j + 1].SelectNodes("text()")[1].InnerText.Trim().Replace("単位", ""));
                        generalRegistrations.EntriedGeneralRegistration.ClassRoom = ReplaceSpace(htmlNode.SelectNodes("td")[j + 1].SelectNodes("text()")[2].InnerText);
                    }
                    ClassTables[i][j].GeneralRegistrations = generalRegistrations;
                }
            }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuNameInit.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuNameInit.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            string faculty = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[0].Attributes["value"].Value;
            string department = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[1].Attributes["value"].Value;
            string course = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[2].Attributes["value"].Value;
            string grade = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[3].Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuName.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"faculty={faculty}&department={department}&course={course}&grade={grade}&kamokuKbnCode=&req=&kamokuName=&button_kind.search.x=0&button_kind.search.y=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuName.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form/table[4]") == null) { logger.Warn("Not found RegisterableGeneralRegistrations."); }
            else
            {
                for (int i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]/tr/td/table").SelectNodes("tr").Count; i++)
                {
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]/tr/td/table").SelectNodes("tr")[i];
                    GeneralRegistration generalRegistration = new();
                    generalRegistration.SubjectsName = ReplaceSpace(htmlNode.SelectNodes("td")[1].InnerText);
                    generalRegistration.TeacherName = ReplaceSpace(htmlNode.SelectNodes("td")[2].InnerText.Replace("\n", ""));
                    generalRegistration.Credit = int.Parse(htmlNode.SelectNodes("td")[3].InnerText.Trim().Replace("単位", ""));
                    if (htmlNode.SelectNodes("td")[4].Attributes["colspan"] != null)
                    {
                        generalRegistration.WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("td")[4].InnerText);
                        generalRegistration.ClassRoom = ReplaceSpace(htmlNode.SelectNodes("td")[5].InnerText);
                    }
                    else
                    {
                        generalRegistration.WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("td")[4].InnerText);
                        generalRegistration.WeekdayPeriod += ReplaceSpace(htmlNode.SelectNodes("td")[5].InnerText).Replace("限", "");
                        generalRegistration.ClassRoom = ReplaceSpace(htmlNode.SelectNodes("td")[6].InnerText);
                    }
                    generalRegistration.KamokuCode = ReplaceJSArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 0).Replace("javascript:checkKamoku", "");
                    generalRegistration.ClassCode = ReplaceJSArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 1);
                    generalRegistration.Unit = ReplaceJSArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 2);
                    generalRegistration.SelectKamoku = ReplaceJSArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 3);
                    generalRegistration.Radio = htmlNode.SelectNodes("td")[0].SelectSingleNode("a/input").Attributes["value"].Value;
                    if (generalRegistration.WeekdayPeriod != "時間割外")
                    {
                        ClassTables[ReplacePeriod(generalRegistration.WeekdayPeriod)][ReplaceWeekday(generalRegistration.WeekdayPeriod)].GeneralRegistrations.RegisterableGeneralRegistrations.Add(generalRegistration);
                    }
                }
            }
            logger.Info("End Get GeneralRegistrations.");
            Account.GeneralRegistrationDateTime = DateTime.Now;
            SaveJsons();
            SaveCookies();
        }

        private List<GeneralRegistration> GetRegisterableGeneralRegistrations(string youbi, string jigen, out string faculty, out string department, out string course, out string grade)
        {
            logger.Info($"Start Get RegisterableGeneralRegistrations youbi={youbi}, jigen={jigen}.");
            List<GeneralRegistration> registerableGeneralRegistrations = new();
            httpRequestMessage = new(new("GET"), $"https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuInit.do?youbi={youbi}&jigen={jigen}");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info($"GET https://gakujo.shizuoka.ac.jp/kyoumu/searchKamokuInit.do?youbi={youbi}&jigen={jigen}");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            faculty = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[0].Attributes["value"].Value;
            department = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[1].Attributes["value"].Value;
            course = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[2].Attributes["value"].Value;
            grade = htmlDocument.DocumentNode.SelectNodes("//option[@selected]")[3].Attributes["value"].Value;
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/searchKamoku.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"faculty={faculty}&departmen={department}&course={course}&grade={grade}&kamokuKbnCode=&req=&button_kind.search.x=0&button_kind.search.y=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info($"POST https://gakujo.shizuoka.ac.jp/kyoumu/searchKamoku.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/form/table[4]") == null) { logger.Warn("Not found RegisterableGeneralRegistrations."); return registerableGeneralRegistrations; }
            for (int i = 1; i < htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]/tr/td/table").SelectNodes("tr").Count; i++)
            {
                HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/form/table[4]/tr/td/table").SelectNodes("tr")[i];
                GeneralRegistration generalRegistration = new();
                generalRegistration.SubjectsName = ReplaceSpace(htmlNode.SelectNodes("td")[1].InnerText);
                generalRegistration.TeacherName = ReplaceSpace(htmlNode.SelectNodes("td")[2].InnerText.Replace("\n", ""));
                generalRegistration.Credit = int.Parse(htmlNode.SelectNodes("td")[3].InnerText.Trim().Replace("単位", ""));
                generalRegistration.WeekdayPeriod = ReplaceSpace(htmlNode.SelectNodes("td")[4].InnerText);
                generalRegistration.WeekdayPeriod += ReplaceSpace(htmlNode.SelectNodes("td")[5].InnerText).Replace("限", "");
                generalRegistration.ClassRoom = ReplaceSpace(htmlNode.SelectNodes("td")[6].InnerText);
                generalRegistration.KamokuCode = ReplaceJSArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 0).Replace("javascript:checkKamoku", "");
                generalRegistration.ClassCode = ReplaceJSArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 1);
                generalRegistration.Unit = ReplaceJSArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 2);
                generalRegistration.SelectKamoku = ReplaceJSArgs(htmlNode.SelectNodes("td")[0].SelectSingleNode("a").Attributes["onclick"].Value, 3);
                generalRegistration.Radio = htmlNode.SelectNodes("td")[0].SelectSingleNode("a/input").Attributes["value"].Value;
                registerableGeneralRegistrations.Add(generalRegistration);
            }
            logger.Info($"End Get RegisterableGeneralRegistrations youbi={youbi}, jigen={jigen}.");
            return registerableGeneralRegistrations;

        }

        private bool SetGeneralRegistration(GeneralRegistrationEntry generalRegistrationEntry, bool restore, out int result)
        {
            result = -1;
            string youbi = (ReplaceWeekday(generalRegistrationEntry.WeekdayPeriod) + 1).ToString();
            string jigen = ReplacePeriod(generalRegistrationEntry.WeekdayPeriod).ToString();
            List<GeneralRegistration> suggestGeneralRegistrationEntries = GetRegisterableGeneralRegistrations(youbi, jigen, out string faculty, out string department, out string course, out string grade).Where(x => (!restore && x.SubjectsName.Contains(generalRegistrationEntry.SubjectsName) && x.SubjectsName.Contains(generalRegistrationEntry.ClassName)) || (restore && x.KamokuCode == generalRegistrationEntry.EntriedKamokuCode && x.ClassCode == generalRegistrationEntry.EntriedClassCode)).ToList();
            if (suggestGeneralRegistrationEntries.Count != 1) { logger.Warn("Not found GeneralRegistration by count not 1."); return false; }
            GeneralRegistration generalRegistration = suggestGeneralRegistrationEntries[0];
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/searchKamoku.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpRequestMessage.Content = new StringContent($"faculty={faculty}&department={department}&course={course}&grade={grade}&kamokuKbnCode=&req=&kamokuCode={generalRegistration.KamokuCode}&classCode={generalRegistration.ClassCode}&unit={generalRegistration.Unit}&radio={generalRegistration.Radio}&selectKamoku={generalRegistration.SelectKamoku}&button_kind.registKamoku.x=0&button_kind.registKamoku.y=0");
            httpRequestMessage.Content.Headers.ContentType = MediaTypeHeaderValue.Parse("application/x-www-form-urlencoded");
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/searchKamoku.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/font[1]/b") != null)
            {
                string errorMessage = htmlDocument.DocumentNode.SelectSingleNode("/html/body/font[2]/ul/li").InnerText;
                if (errorMessage.Contains("他の科目を取り消して、半期履修制限単位数以内で履修登録してください。"))
                {
                    logger.Error($"Error Set GeneralRegistration {generalRegistration} by credits limit.");
                    result = 1;
                }
                else if (errorMessage.Contains("を取り消してから、履修登録してください。"))
                {
                    logger.Error($"Error Set GeneralRegistration {generalRegistration} by duplicate class.");
                    result = 2;
                }
                else if (errorMessage.Contains("定員数を超えているため、登録できません。"))
                {
                    logger.Error($"Error Set GeneralRegistration {generalRegistration} by attending capacity.");
                    result = 3;
                }
                return false;
            }
            logger.Info($"Set GeneralRegistration {generalRegistration}");
            SaveJsons();
            SaveCookies();
            result = 0;
            return true;
        }

        private void SetGeneralRegistrationClear(string youbi, string jigen)
        {
            logger.Info("Start Set GeneralRegistrationClear.");
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/table[4]") == null) { logger.Warn("Not found GeneralRegistrations."); return; }
            HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[int.Parse(jigen)].SelectNodes("td")[int.Parse(youbi)].SelectSingleNode("table/tr[2]/td/a");
            if (htmlNode == null) { logger.Warn("Not found class in GeneralRegistrations."); return; }
            string kamokuCode = ReplaceJSArgs(htmlNode.Attributes["href"].Value, 1);
            string classCode = ReplaceJSArgs(htmlNode.Attributes["href"].Value, 2);
            httpRequestMessage = new(new("GET"), $"https://gakujo.shizuoka.ac.jp/kyoumu/removeKamokuInit.do?kamokuCode={kamokuCode}&classCode={classCode}&youbi={youbi}&jigen={jigen}");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info($"GET https://gakujo.shizuoka.ac.jp/kyoumu/removeKamokuInit.do?kamokuCode={kamokuCode}&classCode={classCode}&youbi={youbi}&jigen={jigen}");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("POST"), "https://gakujo.shizuoka.ac.jp/kyoumu/removeKamoku.do");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("POST https://gakujo.shizuoka.ac.jp/kyoumu/removeKamoku.do");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            logger.Info("End Set GeneralRegistrationClear.");
            SaveJsons();
            SaveCookies();
        }

        public void SetGeneralRegistrations(List<GeneralRegistrationEntry> generalRegistrationEntries, bool overwrite = false)
        {
            logger.Info("Start Set GeneralRegistrations.");
            SetAcademicSystem(out _, out _, out bool generalRegistrationEnabled);
            if (!generalRegistrationEnabled) { logger.Warn("Return Set GeneralRegistration by overtime."); return; }
            httpRequestMessage = new(new("GET"), "https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info("GET https://gakujo.shizuoka.ac.jp/kyoumu/rishuuInit.do?mainMenuCode=002&parentMenuCode=001");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            if (htmlDocument.DocumentNode.SelectNodes("/html/body/table[4]") == null) { logger.Warn("Not found GeneralRegistrations."); return; }
            foreach (GeneralRegistrationEntry generalRegistrationEntry in generalRegistrationEntries)
            {
                string youbi = (ReplaceWeekday(generalRegistrationEntry.WeekdayPeriod) + 1).ToString();
                string jigen = ReplacePeriod(generalRegistrationEntry.WeekdayPeriod).ToString();
                HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode("/html/body/table[4]/tr/td/table").SelectNodes("tr")[int.Parse(jigen)].SelectNodes("td")[int.Parse(youbi)].SelectSingleNode("table/tr[2]/td/a");
                if (htmlNode == null) { logger.Warn("Not found class in GeneralRegistrations."); continue; }
                generalRegistrationEntry.EntriedKamokuCode = ReplaceJSArgs(htmlNode.Attributes["href"].Value, 1);
                generalRegistrationEntry.EntriedClassCode = ReplaceJSArgs(htmlNode.Attributes["href"].Value, 2);
            }
            foreach (GeneralRegistrationEntry generalRegistrationEntry in generalRegistrationEntries)
            {
                SetGeneralRegistration(generalRegistrationEntry, false, out int result);
                if (result == 2 && overwrite)
                {
                    string youbi = (ReplaceWeekday(generalRegistrationEntry.WeekdayPeriod) + 1).ToString();
                    string jigen = ReplacePeriod(generalRegistrationEntry.WeekdayPeriod).ToString();
                    SetGeneralRegistrationClear(youbi, jigen);
                    SetGeneralRegistration(generalRegistrationEntry, false, out result);
                    if (result != 0) { SetGeneralRegistration(generalRegistrationEntry, true, out _); }
                }
            }
            logger.Info("End Set GeneralRegistrations.");
            SaveJsons();
            SaveCookies();
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
                    HtmlNode htmlNode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table/tr[2]/td/a");
                    if (htmlNode != null)
                    {
                        string detailKamokuCode = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 1);
                        string detailClassCode = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 2);
                        if (classTableCell.KamokuCode != detailKamokuCode || classTableCell.ClassCode != detailClassCode)
                        {
                            classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                            string classRoom = htmlNode.InnerHtml;
                            classTableCell.ClassRoom = ReplaceSpace(classRoom[(classRoom.LastIndexOf("<br>") + 4)..]);
                        }
                    }
                    else if (htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table[1]/tr/td/a") != null && (semesterCode == 0 || semesterCode == 2))
                    {
                        htmlNode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table[1]/tr/td/a");
                        string detailKamokuCode = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 1);
                        string detailClassCode = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 2);
                        if (classTableCell.KamokuCode != detailKamokuCode || classTableCell.ClassCode != detailClassCode)
                        {
                            classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                            string classRoom = htmlNode.InnerHtml;
                            classTableCell.ClassRoom = ReplaceSpace(classRoom[(classRoom.LastIndexOf("<br>") + 4)..]);
                        }
                    }
                    else if (htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table[2]/tr/td/a") != null && (semesterCode == 1 || semesterCode == 3))
                    {
                        htmlNode = htmlDocument.DocumentNode.SelectSingleNode($"/html/body/table[4]/tr/td/table").SelectNodes("tr")[i + 1].SelectNodes("td")[j + 1].SelectSingleNode("table[2]/tr/td/a");
                        string detailKamokuCode = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 1);
                        string detailClassCode = ReplaceJSArgs(htmlNode.Attributes["onclick"].Value, 2);
                        if (classTableCell.KamokuCode != detailKamokuCode || classTableCell.ClassCode != detailClassCode)
                        {
                            classTableCell = GetClassTableCell(detailKamokuCode, detailClassCode);
                            string classRoom = htmlNode.InnerHtml;
                            classTableCell.ClassRoom = ReplaceSpace(classRoom[(classRoom.LastIndexOf("<br>") + 4)..]);
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
            httpRequestMessage = new(new("GET"), $"https://gakujo.shizuoka.ac.jp/kyoumu/detailKamoku.do?detailKamokuCode={detailKamokuCode}&detailClassCode={detailClassCode}&gamen=jikanwari");
            httpRequestMessage.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            httpResponseMessage = httpClient.SendAsync(httpRequestMessage).Result;
            logger.Info($"GET https://gakujo.shizuoka.ac.jp/kyoumu/detailKamoku.do?detailKamokuCode={detailKamokuCode}&detailClassCode={detailClassCode}&gamen=jikanwari");
            logger.Trace(httpResponseMessage.Content.ReadAsStringAsync().Result);
            HtmlDocument htmlDocument = new();
            htmlDocument.LoadHtml(httpResponseMessage.Content.ReadAsStringAsync().Result);
            classTableCell.SubjectsName = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目名\")]/following-sibling::td").InnerText);
            classTableCell.SubjectsId = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目番号\")]/following-sibling::td").InnerText);
            classTableCell.ClassName = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"クラス名\")]/following-sibling::td").InnerText);
            classTableCell.TeacherName = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"担当教員\")]/following-sibling::td").InnerText);
            classTableCell.SubjectsSection = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"科目区分\")]/following-sibling::td").InnerText);
            classTableCell.SelectionSection = ReplaceSpace(htmlDocument.DocumentNode.SelectSingleNode("//td[contains(text(), \"必修選択区分\")]/following-sibling::td").InnerText);
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

    public class GeneralRegistrations
    {
        public GeneralRegistration EntriedGeneralRegistration { get; set; } = new();

        public List<GeneralRegistration> RegisterableGeneralRegistrations { get; set; } = new();

        public override string ToString()
        {
            return $"{EntriedGeneralRegistration}";
        }
    }

    public class GeneralRegistration
    {
        public string WeekdayPeriod { get; set; } = "";
        public string SubjectsName { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public string ClassRoom { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int Credit { get; set; }

        public string KamokuCode { get; set; } = "";
        public string ClassCode { get; set; } = "";
        public string Unit { get; set; } = "";
        public string Radio { get; set; } = "";
        public string SelectKamoku { get; set; } = "";

        public override string ToString()
        {
            return $"{SubjectsName} {ClassName}";
        }
    }

    public class GeneralRegistrationEntry
    {
        public string WeekdayPeriod { get; set; } = "";
        public string SubjectsName { get; set; } = "";
        public string ClassName { get; set; } = "";

        public string EntriedKamokuCode { get; set; } = "";
        public string EntriedClassCode { get; set; } = "";

        public override string ToString()
        {
            return $"{WeekdayPeriod} {SubjectsName} {ClassName}";
        }
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

        public List<LotteryRegistration> LotteryRegistrations => new List<List<LotteryRegistration>> { this[0].LotteryRegistrations, this[1].LotteryRegistrations, this[2].LotteryRegistrations, this[3].LotteryRegistrations, this[4].LotteryRegistrations }.SelectMany(_ => _).ToList();

        public List<LotteryRegistrationResult> LotteryRegistrationsResult => new List<List<LotteryRegistrationResult>> { this[0].LotteryRegistrationsResult, this[1].LotteryRegistrationsResult, this[2].LotteryRegistrationsResult, this[3].LotteryRegistrationsResult, this[4].LotteryRegistrationsResult }.SelectMany(_ => _).ToList();

        public List<GeneralRegistration> RegisterableGeneralRegistrations => new List<List<GeneralRegistration>> { this[0].GeneralRegistrations.RegisterableGeneralRegistrations, this[1].GeneralRegistrations.RegisterableGeneralRegistrations, this[2].GeneralRegistrations.RegisterableGeneralRegistrations, this[3].GeneralRegistrations.RegisterableGeneralRegistrations, this[4].GeneralRegistrations.RegisterableGeneralRegistrations }.SelectMany(_ => _).Where(x => x.KamokuCode != "" && x.ClassCode != "").ToList();

        public IEnumerator<ClassTableCell> GetEnumerator()
        {
            yield return this[0];
            yield return this[1];
            yield return this[2];
            yield return this[3];
            yield return this[4];
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
        public GeneralRegistrations GeneralRegistrations { get; set; } = new();

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
