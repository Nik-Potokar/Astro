using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Astro.Helper;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ImGuiNET;

namespace Astro;

public interface IUi
{
    bool Visible { set; }
    void Draw();
}

public class Ui : IUi
{
    private bool visible;
    bool IUi.Visible { set => visible = value; }
    
    private static readonly Vector4 Red = new(1, 0, 0, 1);

    void IUi.Draw()
    {
        if (!visible || !ImGui.Begin("Astro", ref visible))
            return;

        Checkbox("Toggle Astro status", ref DalamudApi.Configuration.AstroStatus);
        Checkbox("Show Debug Message", ref DalamudApi.Configuration.ShowDebugMessage);
        Tooltip("ON to enable, OFF to disable.");
        ImGui.Separator();
        if (Checkbox("Enable auto play", ref DalamudApi.Configuration.EnableAutoPlay))
        {
            DalamudApi.Configuration.EnableBurstCard = false;
            DalamudApi.Configuration.Save();
        }
        ImGui.PushStyleColor(ImGuiCol.Text, Red);
        Tooltip("Conflict: \"Deal three cards at burst\"");
        ImGui.PopStyleColor();
        Checkbox("Enable auto redraw", ref DalamudApi.Configuration.EnableAutoRedraw);
        ImGui.Separator();
            
        Checkbox("Enable manual play", ref DalamudApi.Configuration.EnableManualPlay);
        Tooltip("When a play is executed manually, the target is automatically selected and performed.");
        Checkbox("Enable manual redraw", ref DalamudApi.Configuration.EnableManualRedraw);
        Tooltip("When a play is executed when the seals are not aligned, it is converted to redraw and executed.");
        ImGui.Separator();

        if (Checkbox("Deal three cards at burst", ref DalamudApi.Configuration.EnableBurstCard))
        {
            DalamudApi.Configuration.EnableAutoPlay = false;
            DalamudApi.Configuration.Save();
        }
        ImGui.PushStyleColor(ImGuiCol.Text, Red);
        Tooltip("Auto play will not work except when the card charge count reaches 2 or while executing Divination!\nConflict: \"Enable auto play\"");
        ImGui.PopStyleColor();
        if (Checkbox("Deal the cards n seconds before the burst", ref DalamudApi.Configuration.EnableNSecBeforeBurst))
            if (ImGui.SliderInt("Seconds ago##Burst", ref DalamudApi.Configuration.BurstRange, 1, 10))
                DalamudApi.Configuration.Save();
        
        if (Checkbox("Deal the cards n seconds before the mini burst", ref DalamudApi.Configuration.EnableNSecMiniBurst))
            if (ImGui.SliderInt("Seconds ago##MiniBurst", ref DalamudApi.Configuration.MiniBurstRange, 1, 10))
                DalamudApi.Configuration.Save();
        
        ImGui.BeginTabBar("tab-burst");
        if (ImGui.BeginTabItem("In Burst"))
        {
            ImGui.Text("Melee card priority (drag and drop to re-order)");
            ReordableList(DalamudApi.Configuration.MeleeBurstPriority);
            ImGui.Separator();
            ImGui.Text("Range card priority (drag and drop to re-order)");
            ReordableList(DalamudApi.Configuration.RangeBurstPriority);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Mini Burst"))
        {
            ImGui.Text("Melee card priority (drag and drop to re-order)");
            ReordableList(DalamudApi.Configuration.MeleeMiniBurstPriority);
            ImGui.Separator();
            ImGui.Text("Range card priority (drag and drop to re-order)");
            ReordableList(DalamudApi.Configuration.RangeMiniBurstPriority);
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Outside of Burst"))
        {
            ImGui.Text("Melee card priority (drag and drop to re-order)");
            ReordableList(DalamudApi.Configuration.MeleePriority);
            ImGui.Separator();
            ImGui.Text("Range card priority (drag and drop to re-order)");
            ReordableList(DalamudApi.Configuration.RangePriority);   
            ImGui.EndTabItem();
        }
        ImGui.TextWrapped("If you are missing a Class Job, please add the Abbreviation of the desired job to the \"(Melee|Range)Priority\" in %%appdata%%\\XIVLauncher\\pluginConfigs\\Astro.json");
        ImGui.EndTabBar();
        ImGui.End();
    }
    
    private static unsafe void ReordableList(IList<string> list)
    {
        for (var i = 0; i < list.Count; i++)
        {
            ImGui.Text($"{i + 1}.");
            ImGui.SameLine();
            ImGui.SetNextItemWidth(ImGui.GetWindowWidth());
            ImGui.Selectable($"{list[i]}");

            if (ImGui.BeginDragDropSource())
            {
                ImGui.Text($"Selecting {list[i]}");
                ImGui.SetDragDropPayload("Index", (IntPtr)(&i), sizeof(int));
                ImGui.EndDragDropSource();
            }

            if (!ImGui.BeginDragDropTarget())
                continue;

            var payload = ImGui.AcceptDragDropPayload("Index");
            if (payload.NativePtr != null)
            {
                var dataPtr = (int*)payload.Data;
                if (dataPtr != null)
                {
                    var srcIndex = dataPtr[0];
                    (list[srcIndex], list[i]) = (list[i], list[srcIndex]);
                    DalamudApi.Configuration.Save();
                }
            }

            ImGui.EndDragDropTarget();
        }
    }

    private bool Checkbox(string label, ref bool value)
    {
        if (ImGui.Checkbox(label, ref value))
            DalamudApi.Configuration.Save();

        return value;
    }

    private static void Tooltip(string tooltip)
    {
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }
}