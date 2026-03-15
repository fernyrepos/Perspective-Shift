using RimWorld;
using UnityEngine;
using Verse;
using Verse.Profile;

namespace PerspectiveShift
{
    [HotSwappable]
    public class Dialog_NameSaveAndExit : Window
    {
        private string saveName;
        private bool focused = false;
        private const string FieldName = "PS_SaveNameField";
        public override Vector2 InitialSize => new Vector2(420f, 150f);

        public Dialog_NameSaveAndExit()
        {
            if (Faction.OfPlayer.HasName)
            {
                saveName = Faction.OfPlayer.Name;
            }
            else
            {
                saveName = SaveGameFilesUtility.UnusedDefaultFileName(Faction.OfPlayer.def.LabelCap);
            }
            saveName = GenFile.SanitizedFileName(saveName);
        }

        public override void OnCancelKeyPressed() { }

        public override void DoWindowContents(Rect inRect)
        {
            inRect = inRect.ContractedBy(20f);
            GUI.SetNextControlName(FieldName);
            saveName = Widgets.TextField(new Rect(inRect.x, inRect.y, inRect.width, 32f), saveName);

            if (!focused)
            {
                UI.FocusControl(FieldName, this);
                focused = true;
            }

            bool valid = !saveName.NullOrEmpty() && GenText.IsValidFilename(saveName);
            var btnRect = new Rect(inRect.x, inRect.y + 50f, inRect.width, 38f);

            GUI.enabled = valid;
            if (Widgets.ButtonText(btnRect, "PS_SaveAndEndRun".Translate()))
            {
                LongEventHandler.QueueLongEvent(delegate
                {
                    GameDataSaveLoader.SaveGame(saveName);
                    MemoryUtility.ClearAllMapsAndWorld();
                }, "Entry", "SavingLongEvent", doAsynchronously: false, null, showExtraUIInfo: false);
            }
            GUI.enabled = true;
        }
    }
}
