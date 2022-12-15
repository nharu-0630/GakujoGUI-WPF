namespace GakujoGUI.Models
{
    public class GeneralRegistrationEntry
    {
        public string WeekdayPeriod { get; set; } = "";
        public string SubjectsName { get; set; } = "";
        public string ClassName { get; set; } = "";

        public string EntriedKamokuCode { get; set; } = "";
        public string EntriedClassCode { get; set; } = "";

        public override string ToString() => $"{WeekdayPeriod} {SubjectsName} {ClassName}";
    }
}
