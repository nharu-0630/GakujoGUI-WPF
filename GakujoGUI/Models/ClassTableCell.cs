using System.Collections.Generic;

namespace GakujoGUI.Models
{
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
        public string SyllabusUrl { get; set; } = "";
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
            var objClassTableCell = (ClassTableCell)obj;
            return SubjectsName == objClassTableCell.SubjectsName && SubjectsId == objClassTableCell.SubjectsId;
        }

        public override int GetHashCode() => SubjectsName.GetHashCode() ^ SubjectsId.GetHashCode();
    }
}
