using System;

namespace GakujoGUI.Models
{
    public class ClassResult
    {
        public string Subjects { get; set; } = "";
        public string TeacherName { get; set; } = "";
        public string SubjectsSection { get; set; } = "";
        public string SelectionSection { get; set; } = "";
        public int Credit { get; set; }
        public string Evaluation { get; set; } = "";
        public double Score { get; set; }
        public double Gp { get; set; }
        public string AcquisitionYear { get; set; } = "";
        public DateTime ReportDate { get; set; }
        public string TestType { get; set; } = "";

        public override string ToString() => $"{Subjects} {Score} ({Evaluation}) {Gp} {ReportDate.ToShortDateString()}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            var objClassResult = (ClassResult)obj;
            return Subjects == objClassResult.Subjects && AcquisitionYear == objClassResult.AcquisitionYear;
        }

        public override int GetHashCode() => Subjects.GetHashCode() ^ AcquisitionYear.GetHashCode();
    }
}
