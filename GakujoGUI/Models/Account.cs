using System;

namespace GakujoGUI.Models
{
    public class Account
    {
        public string UserId { get; set; } = "";
        public string PassWord { get; set; } = "";
        public string StudentName { get; set; } = "";
        public string StudentCode { get; set; } = "";
        public string ApacheToken { get; set; } = "";
        public string AccessEnvironmentKey { get; set; } = "";
        public string AccessEnvironmentValue { get; set; } = "";

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
}
