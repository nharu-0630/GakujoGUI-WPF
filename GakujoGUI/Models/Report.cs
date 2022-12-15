using System;

namespace GakujoGUI.Models
{
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
        public bool IsSubmit => SubmittedDateTime != new DateTime();
        public bool IsSubmittable => Status == "受付中" && SubmittedDateTime == new DateTime();

        public override string ToString() => $"[{Status}] {GakujoApi.ReplaceSubjectsShort(Subjects)} {Title} -> {EndDateTime}";

        public string ToShortString() => $"{GakujoApi.ReplaceSubjectsShort(Subjects)} {Title}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            var objReport = (Report)obj;
            return SubjectCode == objReport.SubjectCode && ClassCode == objReport.ClassCode && Id == objReport.Id;
        }

        public override int GetHashCode() => SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ Id.GetHashCode();
    }
}
