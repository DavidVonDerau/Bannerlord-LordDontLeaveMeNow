using TaleWorlds.Core;
using TaleWorlds.Localization;
using TaleWorlds.MountAndBlade;

namespace LordDontLeaveMeNow
{
    public class SubModule : MBSubModuleBase
    {
        protected override void OnSubModuleLoad()
        {
            base.OnSubModuleLoad();

            Module.CurrentModule.AddInitialStateOption(new InitialStateOption("Message",
                new TextObject("Message", null),
                9990,
                () => { InformationManager.DisplayMessage(new InformationMessage("Hello World!")); },
                false));
        }
    }
}
