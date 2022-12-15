namespace GakujoGUI.Models
{
    public class LotteryRegistrationEntry
    {
        public string SubjectsName { get; set; } = "";
        public string ClassName { get; set; } = "";
        public int AspirationOrder { get; set; }

        public override string ToString() => $"{SubjectsName} {ClassName} [{AspirationOrder}]";
    }
}
