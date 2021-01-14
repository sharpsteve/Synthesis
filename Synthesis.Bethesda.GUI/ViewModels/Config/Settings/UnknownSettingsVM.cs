using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Text.Json;

namespace Synthesis.Bethesda.GUI
{
    public class UnknownSettingsNodeVM : SettingsNodeVM
    {
        public UnknownSettingsNodeVM(string memberName)
            : base(memberName)
        {
        }

        public override void Import(JsonElement property, ILogger logger)
        {
            logger.Error($"Tried to import for unknown setting: {this.MemberName}");
        }

        public override void Persist(JObject obj, ILogger logger)
        {
        }
    }
}
