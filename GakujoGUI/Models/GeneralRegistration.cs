namespace GakujoGUI.Models
{
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

        public override string ToString() => $"{SubjectsName} {ClassName}";
    }
}
