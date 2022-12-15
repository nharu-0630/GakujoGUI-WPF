namespace GakujoGUI.Models
{
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

        public override string ToString() => $"{SubjectsName} {ClassName} {ChoiceNumberValue} {(IsWinning ? "*" : "")}";
    }
}
