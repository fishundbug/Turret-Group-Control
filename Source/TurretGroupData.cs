using System;
using System.Collections.Generic;
using Verse;

namespace TurretGroupControl
{
    public class TurretGroupData : IExposable
    {
        public int id;
        public string name;
        public List<Thing> members = new List<Thing>();
        public bool holdFire;
        public bool powerOff;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id", 0);
            Scribe_Values.Look(ref name, "name", string.Empty);
            Scribe_Values.Look(ref holdFire, "holdFire", false);
            Scribe_Values.Look(ref powerOff, "powerOff", false);
            Scribe_Collections.Look(ref members, "members", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                members ??= new List<Thing>();
                members.RemoveAll(t => t == null || t.DestroyedOrNull());
            }
        }

        public void CleanupMembers()
        {
            if (members == null)
            {
                members = new List<Thing>();
                return;
            }

            members.RemoveAll(t => t == null || t.DestroyedOrNull());
        }

        public bool Contains(Thing thing)
        {
            if (thing == null || members == null)
            {
                return false;
            }

            for (int i = 0; i < members.Count; i++)
            {
                if (members[i] == thing)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
