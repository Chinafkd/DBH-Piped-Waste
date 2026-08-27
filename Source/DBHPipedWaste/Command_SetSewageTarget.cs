using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace DBHPipedWaste
{
    public class Command_SetSewageTarget : Command
    {
        public CompUndergroundSewageStorage storage;
        private List<CompUndergroundSewageStorage> storages;

        public override void ProcessInput(Event ev)
        {
            base.ProcessInput(ev);
            if (storage == null)
            {
                return;
            }
            if (storages == null)
            {
                storages = new List<CompUndergroundSewageStorage>();
            }
            if (!storages.Contains(storage))
            {
                storages.Add(storage);
            }
            Find.WindowStack.Add(new Dialog_Slider(
                value => "DBHPW_TargetSlider".Translate(value),
                0,
                100,
                value =>
                {
                    foreach (CompUndergroundSewageStorage selected in storages)
                    {
                        if (selected != null)
                        {
                            selected.SetTargetPercent(value / 100f);
                        }
                    }
                },
                Mathf.RoundToInt(storage.TargetPercent * 100f)));
        }

        public override bool InheritInteractionsFrom(Gizmo other)
        {
            Command_SetSewageTarget command = other as Command_SetSewageTarget;
            if (command?.storage == null)
            {
                return false;
            }
            if (storages == null)
            {
                storages = new List<CompUndergroundSewageStorage>();
            }
            if (!storages.Contains(command.storage))
            {
                storages.Add(command.storage);
            }
            return false;
        }
    }
}
