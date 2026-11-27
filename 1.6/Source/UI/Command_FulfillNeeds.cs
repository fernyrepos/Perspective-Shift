using UnityEngine;
using Verse;

namespace PerspectiveShift
{
    [HotSwappable]
    public class Command_FulfillNeeds : Command_Toggle
    {
        public override void ProcessInput(Event ev)
        {
            if (ev != null && ev.button == 1)
            {
                Find.WindowStack.Add(new Dialog_FulfillNeeds());
                return;
            }

            base.ProcessInput(ev);
        }
    }
}
