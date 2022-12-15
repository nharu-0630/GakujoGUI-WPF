using System;

namespace GakujoGUI.Models
{
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
        public bool IsSubmit => SubmissionStatus != "未提出";
        public bool IsSubmittable => Status == "受付中" && SubmissionStatus == "未提出";

        public override string ToString() => $"[{SubmissionStatus}] {GakujoApi.ReplaceSubjectsShort(Subjects)} {Title} -> {EndDateTime}";

        public string ToShortString() => $"{GakujoApi.ReplaceSubjectsShort(Subjects)} {Title}";

        public override bool Equals(object? obj)
        {
            if (obj == null || GetType() != obj.GetType()) { return false; }
            var objQuiz = (Quiz)obj;
            return SubjectCode == objQuiz.SubjectCode && ClassCode == objQuiz.ClassCode && Id == objQuiz.Id;
        }

        public override int GetHashCode() => SubjectCode.GetHashCode() ^ ClassCode.GetHashCode() ^ Id.GetHashCode();
    }
}
