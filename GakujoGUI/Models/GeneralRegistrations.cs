using System.Collections.Generic;

namespace GakujoGUI.Models
{
    public class GeneralRegistrations
    {
        public GeneralRegistration EntriedGeneralRegistration { get; set; } = new();

        public List<GeneralRegistration> RegisterableGeneralRegistrations { get; set; } = new();

        public override string ToString() => $"{EntriedGeneralRegistration}";
    }
}
