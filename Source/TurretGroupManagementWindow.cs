using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    public class TurretGroupManagementWindow : Window
    {
        private const float WindowWidth = 860f;
        private const float WindowHeight = 620f;
        private const float LeftPanelWidth = 260f;
        private const float Gap = 10f;
        private const float RowHeight = 32f;
        private const float ButtonWidth = 110f;
        private const float SmallButtonWidth = 80f;

        private readonly Map map;
        private readonly TurretGroupManager manager;
        private int selectedGroupId = -1;
        private string renameBuffer = string.Empty;
        private Vector2 groupScrollPosition;
        private Vector2 memberScrollPosition;
        private Vector2 availableScrollPosition;

        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public TurretGroupManagementWindow(Map map)
        {
            this.map = map;
            manager = TurretGroupUtility.GetManager(map);
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (manager == null || map == null)
            {
                Widgets.Label(inRect, "TurretGroupControl_NoManager".Translate());
                return;
            }

            manager.CleanupAllGroups();
            EnsureSelectedGroupIsValid();

            Text.Font = GameFont.Medium;
            var titleRect = new Rect(inRect.x, inRect.y, inRect.width, 34f);
            Widgets.Label(titleRect, "TurretGroupControl_ManagementTitle".Translate());
            Text.Font = GameFont.Small;

            var contentRect = new Rect(inRect.x, titleRect.yMax + Gap, inRect.width, inRect.height - titleRect.height - Gap);
            var leftRect = new Rect(contentRect.x, contentRect.y, LeftPanelWidth, contentRect.height);
            var rightRect = new Rect(leftRect.xMax + Gap, contentRect.y, contentRect.width - LeftPanelWidth - Gap, contentRect.height);

            DrawGroupList(leftRect);
            DrawGroupDetails(rightRect);
        }

        private void DrawGroupList(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(8f);

            var headerRect = new Rect(rect.x, rect.y, rect.width, 28f);
            Text.Font = GameFont.Medium;
            Widgets.Label(headerRect, "TurretGroupControl_GroupList".Translate());
            Text.Font = GameFont.Small;

            var newGroupRect = new Rect(rect.x, headerRect.yMax + 4f, rect.width, 32f);
            if (Widgets.ButtonText(newGroupRect, "TurretGroupControl_NewEmptyGroup".Translate()))
            {
                var group = manager.CreateEmptyGroup();
                selectedGroupId = group.id;
                renameBuffer = group.name;
                Messages.Message("TurretGroupControl_CreatedGroup".Translate(group.name, group.members.Count), MessageTypeDefOf.TaskCompletion, false);
            }

            var groups = manager.AllGroups().ToList();
            var listRect = new Rect(rect.x, newGroupRect.yMax + Gap, rect.width, rect.yMax - newGroupRect.yMax - Gap);
            if (groups.Count == 0)
            {
                Widgets.Label(listRect, "TurretGroupControl_NoGroups".Translate());
                return;
            }

            var viewRect = new Rect(0f, 0f, listRect.width - 16f, groups.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref groupScrollPosition, viewRect);
            for (int i = 0; i < groups.Count; i++)
            {
                var group = groups[i];
                var rowRect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f);
                DrawGroupRow(rowRect, group);
            }
            Widgets.EndScrollView();
        }

        private void DrawGroupRow(Rect rect, TurretGroupData group)
        {
            if (selectedGroupId == group.id)
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            var label = "TurretGroupControl_GroupSummary".Translate(group.name, group.members?.Count ?? 0, group.holdFire ? "TurretGroupControl_HoldFireState".Translate() : "TurretGroupControl_FireAtWillState".Translate());
            if (Widgets.ButtonInvisible(rect))
            {
                selectedGroupId = group.id;
                renameBuffer = group.name;
            }

            Widgets.Label(rect.ContractedBy(4f), label);
        }

        private void DrawGroupDetails(Rect rect)
        {
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(8f);

            var group = manager.GetGroup(selectedGroupId);
            if (group == null)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(rect, "TurretGroupControl_NoGroupSelected".Translate());
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            DrawSelectedGroupActions(new Rect(rect.x, rect.y, rect.width, 118f), group);

            float listTop = rect.y + 128f;
            float listHeight = (rect.yMax - listTop - Gap) / 2f;
            DrawMemberList(new Rect(rect.x, listTop, rect.width, listHeight), group);
            DrawAvailableTurretList(new Rect(rect.x, listTop + listHeight + Gap, rect.width, listHeight), group);
        }

        private void DrawSelectedGroupActions(Rect rect, TurretGroupData group)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 28f), "TurretGroupControl_GroupDetails".Translate());
            Text.Font = GameFont.Small;

            var nameRect = new Rect(rect.x, rect.y + 34f, rect.width - ButtonWidth - Gap, 32f);
            var renameRect = new Rect(nameRect.xMax + Gap, nameRect.y, ButtonWidth, 32f);
            renameBuffer = Widgets.TextField(nameRect, renameBuffer ?? string.Empty);
            if (Widgets.ButtonText(renameRect, "TurretGroupControl_RenameGroup".Translate()))
            {
                if (manager.RenameGroup(group.id, renameBuffer))
                {
                    renameBuffer = group.name;
                    Messages.Message("TurretGroupControl_RenamedGroup".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
                }
            }

            float buttonY = nameRect.yMax + Gap;
            var selectRect = new Rect(rect.x, buttonY, ButtonWidth, 32f);
            var holdRect = new Rect(selectRect.xMax + Gap, buttonY, ButtonWidth, 32f);
            var fireRect = new Rect(holdRect.xMax + Gap, buttonY, ButtonWidth, 32f);
            var deleteRect = new Rect(rect.xMax - ButtonWidth, buttonY, ButtonWidth, 32f);

            if (Widgets.ButtonText(selectRect, "TurretGroupControl_SelectGroup".Translate()))
            {
                manager.SelectGroup(group);
            }
            if (Widgets.ButtonText(holdRect, "TurretGroupControl_GroupHoldFire".Translate()))
            {
                manager.ToggleHoldFire(group.id, true);
                Messages.Message("TurretGroupControl_GroupHoldFireSet".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
            }
            if (Widgets.ButtonText(fireRect, "TurretGroupControl_GroupFireAtWill".Translate()))
            {
                manager.ToggleHoldFire(group.id, false);
                Messages.Message("TurretGroupControl_GroupFireAtWillSet".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
            }
            if (Widgets.ButtonText(deleteRect, "TurretGroupControl_DeleteGroup".Translate()))
            {
                ConfirmDeleteGroup(group);
            }
        }

        private void DrawMemberList(Rect rect, TurretGroupData group)
        {
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(8f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "TurretGroupControl_Members".Translate());

            var members = group.members?.Where(t => t != null && !t.DestroyedOrNull()).ToList() ?? new List<Thing>();
            var listRect = new Rect(rect.x, rect.y + 28f, rect.width, rect.height - 28f);
            if (members.Count == 0)
            {
                Widgets.Label(listRect, "TurretGroupControl_NoMembers".Translate());
                return;
            }

            var viewRect = new Rect(0f, 0f, listRect.width - 16f, members.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref memberScrollPosition, viewRect);
            for (int i = 0; i < members.Count; i++)
            {
                DrawMemberRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f), group, members[i]);
            }
            Widgets.EndScrollView();
        }

        private void DrawMemberRow(Rect rect, TurretGroupData group, Thing turret)
        {
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            var buttonRect = new Rect(rect.xMax - SmallButtonWidth, rect.y, SmallButtonWidth, rect.height);
            var labelRect = new Rect(rect.x + 4f, rect.y + 4f, buttonRect.x - rect.x - 8f, rect.height - 8f);
            Widgets.Label(labelRect, TurretLabel(turret));
            if (Widgets.ButtonText(buttonRect, "TurretGroupControl_RemoveTurret".Translate()))
            {
                manager.RemoveMember(group.id, turret);
                Messages.Message("TurretGroupControl_RemovedTurretFromGroup".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
            }
        }

        private void DrawAvailableTurretList(Rect rect, TurretGroupData group)
        {
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(8f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "TurretGroupControl_AvailableTurrets".Translate());

            var turrets = AvailableTurrets().ToList();
            var listRect = new Rect(rect.x, rect.y + 28f, rect.width, rect.height - 28f);
            if (turrets.Count == 0)
            {
                Widgets.Label(listRect, "TurretGroupControl_NoAvailableTurrets".Translate());
                return;
            }

            var viewRect = new Rect(0f, 0f, listRect.width - 16f, turrets.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref availableScrollPosition, viewRect);
            for (int i = 0; i < turrets.Count; i++)
            {
                DrawAvailableTurretRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f), group, turrets[i]);
            }
            Widgets.EndScrollView();
        }

        private void DrawAvailableTurretRow(Rect rect, TurretGroupData group, Thing turret)
        {
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            var currentGroup = manager.FindGroupFor(turret);
            var buttonRect = new Rect(rect.xMax - SmallButtonWidth, rect.y, SmallButtonWidth, rect.height);
            var labelRect = new Rect(rect.x + 4f, rect.y + 4f, buttonRect.x - rect.x - 8f, rect.height - 8f);
            string label = currentGroup == null ? TurretLabel(turret) : "TurretGroupControl_TurretInOtherGroup".Translate(TurretLabel(turret), currentGroup.name).ToString();
            Widgets.Label(labelRect, label);

            string buttonLabel = currentGroup == null ? "TurretGroupControl_AddTurret".Translate() : "TurretGroupControl_MoveToGroup".Translate();
            if (Widgets.ButtonText(buttonRect, buttonLabel))
            {
                manager.MoveMember(turret, group.id);
                Messages.Message("TurretGroupControl_AddedTurretToGroup".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
            }
        }

        private IEnumerable<Thing> AvailableTurrets()
        {
            if (map?.listerThings?.AllThings == null)
            {
                yield break;
            }

            foreach (var thing in map.listerThings.AllThings)
            {
                if (thing != null && thing.Spawned && thing.Faction == Faction.OfPlayer && TurretGroupManager.IsSupportedTurret(thing))
                {
                    yield return thing;
                }
            }
        }

        private void ConfirmDeleteGroup(TurretGroupData group)
        {
            Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                "TurretGroupControl_ConfirmDeleteGroup".Translate(group.name),
                delegate
                {
                    if (manager.DeleteGroup(group.id))
                    {
                        selectedGroupId = -1;
                        renameBuffer = string.Empty;
                        Messages.Message("TurretGroupControl_DeletedGroup".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
                    }
                },
                true
            ));
        }

        private void EnsureSelectedGroupIsValid()
        {
            var group = manager.GetGroup(selectedGroupId);
            if (group != null)
            {
                if (renameBuffer.NullOrEmpty())
                {
                    renameBuffer = group.name;
                }
                return;
            }

            var firstGroup = manager.AllGroups().FirstOrDefault();
            if (firstGroup == null)
            {
                selectedGroupId = -1;
                renameBuffer = string.Empty;
                return;
            }

            selectedGroupId = firstGroup.id;
            renameBuffer = firstGroup.name;
        }

        private static string TurretLabel(Thing turret)
        {
            if (turret == null)
            {
                return string.Empty;
            }

            return "TurretGroupControl_TurretLabel".Translate(turret.LabelCap, turret.Position).ToString();
        }
    }
}
