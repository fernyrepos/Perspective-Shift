using RimWorld;
using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    public class Dialog_StorageSettings : Window
    {
        private Building_Storage storage;
        private ThingFilterUI.UIState thingFilterState = new ThingFilterUI.UIState();

        public override Vector2 InitialSize => new Vector2(400f, 500f);
        public Dialog_StorageSettings(Building_Storage s) { storage = s; doCloseX = true; }

        public override void PreOpen()
        {
            base.PreOpen();
            thingFilterState.quickSearch.Reset();
        }

        public override void DoWindowContents(Rect inRect)
        {
            ThingFilterUI.DoThingFilterConfigWindow(inRect, thingFilterState, storage.settings.filter, storage.GetParentStoreSettings()?.filter);
        }
    }
}
