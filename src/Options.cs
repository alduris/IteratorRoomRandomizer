using Menu.Remix.MixedUI;
using RWCustom;
using UnityEngine;

namespace OracleRooms
{
    public class Options : OptionInterface
    {
        public enum RandomMode
        {
            EveryCycle,
            OnContinue,
            Once
        }

        public static Configurable<RandomMode> RandomModeConfig;

        public Options()
        {
            RandomModeConfig = config.Bind("IRR_RandomMode", RandomMode.EveryCycle, new ConfigurableInfo("Changes how often the locations randomize."));
        }

        public override void Initialize()
        {
            base.Initialize();
            Tabs = [new OpTab(this)];
            var title = new OpLabel(new Vector2(0f, 350f), new Vector2(600f, 30f), "ITERATOR ROOM RANDOMIZER", FLabelAlignment.Center, true);
            title.label.shader = Custom.rainWorld.Shaders["MenuText"];
            Tabs[0].AddItems(
                title,
                new OpLabel(new Vector2(0f, 280f), new Vector2(600f, 30f), "How often to randomize:", FLabelAlignment.Center, false),
                new OpResourceSelector(RandomModeConfig, new Vector2(200f, 250f), 200f)
                );
        }
    }
}
