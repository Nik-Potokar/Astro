using System;
using Dalamud.Hooking;

namespace Astro.Helper;

public static class HookHelper
{
    private static class Cache<T> where T : Delegate
    {
        internal static Hook<T> Value { get; set; }
    }

    public static T Get<T>() where T : Delegate => Cache<T>.Value == null ? null : Cache<T>.Value.Original;

    public static void Enable<T>(IntPtr address, T detour) where T : Delegate
    {
        Cache<T>.Value = new Hook<T>(address, detour);
        Cache<T>.Value.Enable();
    }
        
    public static void Enable<T>(string address, T detour) where T : Delegate => Enable(DalamudApi.SigScanner.ScanText(address), detour);

    public static void Disable<T>() where T : Delegate
    {
        if(Cache<T>.Value is not { IsEnabled: true })
            return;

        Cache<T>.Value.Disable();
    }
}