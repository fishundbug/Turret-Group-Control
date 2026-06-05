using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace TurretGroupControl
{
    public class TurretGroupManagementWindow : Window
    {
        private const float WindowWidth = 980f;
        private const float WindowHeight = 740f;
        private const float LeftPanelWidth = 300f;
        private const float Gap = 10f;
        private const float RowHeight = 32f;
        private const float ButtonWidth = 110f;
        private const float SmallButtonWidth = 80f;
        private const float DoubleClickInterval = 0.45f;
        private const int AvailableTurretRefreshFrameInterval = 120;

        private readonly Map map;
        private readonly TurretGroupGameComponent manager;
        private int selectedGroupId = -1;
        private int renameBufferGroupId = -1;
        private string renameBuffer = string.Empty;
        private string memberSearchBuffer = string.Empty;
        private string availableSearchBuffer = string.Empty;
        private Thing lastClickedTurret;
        private float lastTurretClickTime = -1f;
        private Vector2 groupScrollPosition;
        private Vector2 memberScrollPosition;
        private Vector2 availableScrollPosition;

        private List<TurretGroupData> cachedGroups;
        private List<Thing> cachedMembers;
        private List<Thing> cachedAvailableTurrets;
        private readonly Dictionary<Thing, string> cachedTurretLabels = new Dictionary<Thing, string>();
        private bool groupCacheDirty = true;
        private bool memberCacheDirty = true;
        private bool availableCacheDirty = true;
        private int cachedMemberGroupId = -1;
        private string cachedMemberSearch = string.Empty;
        private string cachedAvailableSearch = string.Empty;
        private int nextAvailableTurretRefreshFrame;

        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public TurretGroupManagementWindow(Map map)
        {
            this.map = map;
            manager = TurretGroupUtility.GetManager(map);
            manager?.CleanupAllGroups();
            layer = WindowLayer.GameUI;
            doCloseX = true;
            absorbInputAroundWindow = false;
            preventCameraMotion = false;
            forcePause = false;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (manager == null || map == null)
            {
                Widgets.Label(inRect, "TurretGroupControl_NoManager".Translate());
                return;
            }

            RefreshGroupCacheIfNeeded();
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
                renameBufferGroupId = group.id;
                renameBuffer = group.name;
                InvalidateCaches();
                Messages.Message("TurretGroupControl_CreatedGroup".Translate(group.name, group.members.Count), MessageTypeDefOf.TaskCompletion, false);
            }

            RefreshGroupCacheIfNeeded();
            var groups = cachedGroups;
            var listRect = new Rect(rect.x, newGroupRect.yMax + Gap, rect.width, rect.yMax - newGroupRect.yMax - Gap);
            if (groups.Count == 0)
            {
                Widgets.Label(listRect, "TurretGroupControl_NoGroups".Translate());
                return;
            }

            var viewRect = new Rect(0f, 0f, listRect.width - 16f, groups.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref groupScrollPosition, viewRect);
            DrawVisibleRows(groups.Count, groupScrollPosition, listRect, viewRect.width, i =>
            {
                var group = groups[i];
                var rowRect = new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f);
                DrawGroupRow(rowRect, group);
            });
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
                renameBufferGroupId = group.id;
                renameBuffer = group.name;
                memberCacheDirty = true;
                availableCacheDirty = true;
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

            DrawSelectedGroupActions(new Rect(rect.x, rect.y, rect.width, 154f), group);

            float listTop = rect.y + 164f;
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
            if (renameBufferGroupId != group.id)
            {
                renameBufferGroupId = group.id;
                renameBuffer = group.name;
            }
            renameBuffer = Widgets.TextField(nameRect, renameBuffer ?? string.Empty);
            if (Widgets.ButtonText(renameRect, "TurretGroupControl_RenameGroup".Translate()))
            {
                if ((renameBuffer ?? string.Empty).Trim().NullOrEmpty())
                {
                    Messages.Message("TurretGroupControl_InvalidGroupName".Translate(), MessageTypeDefOf.RejectInput, false);
                }
                else if (manager.RenameGroup(group.id, renameBuffer))
                {
                    renameBuffer = group.name;
                    groupCacheDirty = true;
                    Messages.Message("TurretGroupControl_RenamedGroup".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
                }
            }

            float firstButtonY = nameRect.yMax + Gap;
            var selectRect = new Rect(rect.x, firstButtonY, ButtonWidth, 32f);
            var holdRect = new Rect(selectRect.xMax + Gap, firstButtonY, ButtonWidth, 32f);
            var fireRect = new Rect(holdRect.xMax + Gap, firstButtonY, ButtonWidth, 32f);

            float secondButtonY = firstButtonY + 32f + Gap;
            var powerRect = new Rect(rect.x, secondButtonY, ButtonWidth, 32f);
            var deleteRect = new Rect(rect.xMax - ButtonWidth, secondButtonY, ButtonWidth, 32f);

            if (Widgets.ButtonText(selectRect, "TurretGroupControl_SelectGroup".Translate()))
            {
                manager.SelectGroup(group, map);
            }
            if (Widgets.ButtonText(holdRect, "TurretGroupControl_GroupHoldFire".Translate()))
            {
                manager.ToggleHoldFire(group.id, true);
                groupCacheDirty = true;
                Messages.Message("TurretGroupControl_GroupHoldFireSet".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
            }
            if (Widgets.ButtonText(fireRect, "TurretGroupControl_GroupFireAtWill".Translate()))
            {
                manager.ToggleHoldFire(group.id, false);
                groupCacheDirty = true;
                Messages.Message("TurretGroupControl_GroupFireAtWillSet".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
            }
            if (Widgets.ButtonText(powerRect, group.powerOff ? "TurretGroupControl_GroupPowerOn".Translate() : "TurretGroupControl_GroupPowerOff".Translate()))
            {
                bool newPowerOff = !group.powerOff;
                manager.TogglePowerOff(group.id, newPowerOff);
                groupCacheDirty = true;
                Messages.Message(newPowerOff ? "TurretGroupControl_GroupPowerOffSet".Translate(group.name) : "TurretGroupControl_GroupPowerOnSet".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
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

            DrawSearchField(new Rect(rect.x, rect.y + 28f, rect.width, 28f), ref memberSearchBuffer);
            var members = GetCachedMembers(group);
            var listRect = new Rect(rect.x, rect.y + 60f, rect.width, rect.height - 60f);
            if (members.Count == 0)
            {
                Widgets.Label(listRect, "TurretGroupControl_NoMembers".Translate());
                return;
            }

            var viewRect = new Rect(0f, 0f, listRect.width - 16f, members.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref memberScrollPosition, viewRect);
            DrawVisibleRows(members.Count, memberScrollPosition, listRect, viewRect.width, i =>
            {
                DrawMemberRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f), group, members[i]);
            });
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
            HandleTurretRowClick(labelRect, turret);
            if (Widgets.ButtonText(buttonRect, "TurretGroupControl_RemoveTurret".Translate()))
            {
                manager.RemoveMember(group.id, turret);
                InvalidateCaches();
                Messages.Message("TurretGroupControl_RemovedTurretFromGroup".Translate(group.name), MessageTypeDefOf.TaskCompletion, false);
            }
        }

        private void DrawAvailableTurretList(Rect rect, TurretGroupData group)
        {
            Widgets.DrawMenuSection(rect);
            rect = rect.ContractedBy(8f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 24f), "TurretGroupControl_AvailableTurrets".Translate());

            DrawSearchField(new Rect(rect.x, rect.y + 28f, rect.width, 28f), ref availableSearchBuffer);
            var turrets = GetCachedAvailableTurrets();
            var listRect = new Rect(rect.x, rect.y + 60f, rect.width, rect.height - 60f);
            if (turrets.Count == 0)
            {
                Widgets.Label(listRect, "TurretGroupControl_NoAvailableTurrets".Translate());
                return;
            }

            var viewRect = new Rect(0f, 0f, listRect.width - 16f, turrets.Count * RowHeight);
            Widgets.BeginScrollView(listRect, ref availableScrollPosition, viewRect);
            DrawVisibleRows(turrets.Count, availableScrollPosition, listRect, viewRect.width, i =>
            {
                DrawAvailableTurretRow(new Rect(0f, i * RowHeight, viewRect.width, RowHeight - 2f), group, turrets[i]);
            });
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
            HandleTurretRowClick(labelRect, turret);

            string buttonLabel = currentGroup == null ? "TurretGroupControl_AddTurret".Translate() : "TurretGroupControl_MoveToGroup".Translate();
            if (Widgets.ButtonText(buttonRect, buttonLabel))
            {
                manager.MoveMember(turret, group.id);
                InvalidateCaches();
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
                if (thing != null && thing.Spawned && thing.Faction == Faction.OfPlayer && TurretGroupUtility.IsSupportedTurret(thing))
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
                        renameBufferGroupId = -1;
                        renameBuffer = string.Empty;
                        InvalidateCaches();
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
                if (renameBufferGroupId != group.id)
                {
                    renameBufferGroupId = group.id;
                    renameBuffer = group.name;
                }
                return;
            }

            RefreshGroupCacheIfNeeded();
            var firstGroup = cachedGroups.FirstOrDefault();
            if (firstGroup == null)
            {
                selectedGroupId = -1;
                renameBufferGroupId = -1;
                renameBuffer = string.Empty;
                return;
            }

            selectedGroupId = firstGroup.id;
            renameBufferGroupId = firstGroup.id;
            renameBuffer = firstGroup.name;
            memberCacheDirty = true;
            availableCacheDirty = true;
        }

        private void InvalidateCaches()
        {
            groupCacheDirty = true;
            memberCacheDirty = true;
            availableCacheDirty = true;
            nextAvailableTurretRefreshFrame = 0;
        }

        private void RefreshGroupCacheIfNeeded()
        {
            if (!groupCacheDirty && cachedGroups != null)
            {
                return;
            }

            cachedGroups = manager.AllGroups().ToList();
            groupCacheDirty = false;
        }

        private List<Thing> GetCachedMembers(TurretGroupData group)
        {
            string search = NormalizeSearch(memberSearchBuffer);
            if (!memberCacheDirty && cachedMembers != null && cachedMemberGroupId == group.id && cachedMemberSearch == search)
            {
                return cachedMembers;
            }

            cachedMemberGroupId = group.id;
            cachedMemberSearch = search;
            cachedMembers = new List<Thing>();
            if (group.members != null)
            {
                for (int i = 0; i < group.members.Count; i++)
                {
                    var turret = group.members[i];
                    if (IsValidTurretForWindow(turret) && MatchesSearch(turret, search))
                    {
                        cachedMembers.Add(turret);
                    }
                }
            }

            memberCacheDirty = false;
            return cachedMembers;
        }

        private List<Thing> GetCachedAvailableTurrets()
        {
            string search = NormalizeSearch(availableSearchBuffer);
            if (!availableCacheDirty && cachedAvailableTurrets != null && cachedAvailableSearch == search && Time.frameCount < nextAvailableTurretRefreshFrame)
            {
                return cachedAvailableTurrets;
            }

            cachedAvailableSearch = search;
            cachedAvailableTurrets = new List<Thing>();
            foreach (var turret in AvailableTurrets())
            {
                if (MatchesSearch(turret, search))
                {
                    cachedAvailableTurrets.Add(turret);
                }
            }

            availableCacheDirty = false;
            nextAvailableTurretRefreshFrame = Time.frameCount + AvailableTurretRefreshFrameInterval;
            return cachedAvailableTurrets;
        }

        private static void DrawSearchField(Rect rect, ref string searchBuffer)
        {
            var labelRect = new Rect(rect.x, rect.y, 64f, rect.height);
            var fieldRect = new Rect(labelRect.xMax + 6f, rect.y, rect.width - labelRect.width - 6f, rect.height);
            Widgets.Label(labelRect, "TurretGroupControl_Search".Translate());
            searchBuffer = Widgets.TextField(fieldRect, searchBuffer ?? string.Empty);
        }

        private static string NormalizeSearch(string searchText)
        {
            return (searchText ?? string.Empty).Trim();
        }

        private bool MatchesSearch(Thing turret, string searchText)
        {
            return searchText.NullOrEmpty() || TurretLabel(turret).IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsValidTurretForWindow(Thing turret)
        {
            return turret != null && !turret.DestroyedOrNull() && turret.Spawned;
        }

        private static void DrawVisibleRows(int count, Vector2 scrollPosition, Rect listRect, float width, Action<int> drawRow)
        {
            int startIndex = Mathf.Max(0, Mathf.FloorToInt(scrollPosition.y / RowHeight));
            int endIndex = Mathf.Min(count - 1, Mathf.CeilToInt((scrollPosition.y + listRect.height) / RowHeight));
            for (int i = startIndex; i <= endIndex; i++)
            {
                drawRow(i);
            }
        }

        private void HandleTurretRowClick(Rect rect, Thing turret)
        {
            if (Widgets.ButtonInvisible(rect))
            {
                float now = Time.realtimeSinceStartup;
                if (lastClickedTurret == turret && now - lastTurretClickTime <= DoubleClickInterval)
                {
                    lastClickedTurret = null;
                    lastTurretClickTime = -1f;
                    SelectAndJumpToTurret(turret);
                    return;
                }

                lastClickedTurret = turret;
                lastTurretClickTime = now;
            }
        }

        private static void SelectAndJumpToTurret(Thing turret)
        {
            if (turret == null || turret.DestroyedOrNull())
            {
                return;
            }

            Find.Selector.ClearSelection();
            Find.Selector.Select(turret);
            CameraJumper.TryJumpAndSelect(turret, CameraJumper.MovementMode.Cut);
        }

        private string TurretLabel(Thing turret)
        {
            if (turret == null)
            {
                return string.Empty;
            }

            if (!cachedTurretLabels.TryGetValue(turret, out var label))
            {
                label = "TurretGroupControl_TurretLabel".Translate(turret.LabelCap, turret.Position).ToString();
                cachedTurretLabels[turret] = label;
            }

            return label;
        }
    }
}
