namespace GakujoGUI.Models
{
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

        public override string ToString() => $"{SubjectsName} {ClassName} {AttendingCapacity} 1:{FirstApplicantNumber} 2:{SecondApplicantNumber} 3:{ThirdApplicantNumber}";

        public string ToChoiceNumberString() => $"&{ChoiceNumberKey}={ChoiceNumberValue}";
    }
}
