using System.Reflection;
using HarmonyLib;
using MelonLoader;
using UnityEngine;

namespace SlopHero.Mod;

/// <summary>
/// Optional pause for Unity UI event processing during active offline solo
/// songs. The normal path disables the EventSystem component, removing its
/// per-frame BehaviourManager dispatch as well as its mouse raycasts. If the
/// live EventSystem cannot be resolved, a pass-through-safe Update prefix is
/// used instead. Instrument polling is independent and remains untouched.
/// </summary>
internal sealed class GameplayUiEventOverride
{
    private readonly HiddenHudDragCanvasOverride hiddenDragCanvases = new();
    private static GameplayUiEventOverride? active;
    private MethodInfo? updateMethod;
    private PropertyInfo? selectedObjectProperty;
    private MethodInfo? setSelectedObjectMethod;
    private Behaviour? eventSystem;
    private GameObject? selectedBeforeSuspend;
    private bool installed;
    private bool failed;
    private bool suspend;
    private bool usingPrefix;
    private bool originalEnabled;
    private bool disabledByUs;

    internal bool IsInstalled => installed;
    internal bool IsSuspended => installed && suspend;

    internal void Update(HarmonyLib.Harmony harmony, bool requested, bool gameplayActive)
    {
        if (!requested)
        {
            suspend = false;
            hiddenDragCanvases.Clear();
            RestoreComponent();
            Restore(harmony);
            failed = false;
            return;
        }

        if (!installed && !failed) Install(harmony);
        bool shouldSuspend = installed && gameplayActive;
        if (usingPrefix)
        {
            suspend = shouldSuspend;
            hiddenDragCanvases.Update(shouldSuspend);
            return;
        }

        SetComponentSuspended(shouldSuspend);
        hiddenDragCanvases.Update(shouldSuspend && disabledByUs);
    }

    // F8 and Escape use this immediate path so the settings/menu UI is live in
    // the same simulation tick instead of waiting for the 20 Hz state poll.
    internal void Disable()
    {
        suspend = false;
        hiddenDragCanvases.Restore();
        RestoreComponent();
    }

    internal void Clear(HarmonyLib.Harmony harmony)
    {
        suspend = false;
        hiddenDragCanvases.Clear();
        RestoreComponent();
        Restore(harmony);
        failed = false;
    }

    private void Install(HarmonyLib.Harmony harmony)
    {
        try
        {
            var uiAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, "UnityEngine.UI", StringComparison.Ordinal));
            var eventSystemType = uiAssembly?.GetType("UnityEngine.EventSystems.EventSystem", throwOnError: false);
            updateMethod = eventSystemType?.GetMethod("Update",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null, types: Type.EmptyTypes, modifiers: null);
            if (updateMethod == null)
                throw new MissingMethodException("UnityEngine.EventSystems.EventSystem.Update()");

            // Keep the component reference before disabling it: EventSystem
            // clears its static `current` pointer and deactivates its input
            // module from OnDisable. On restore we re-enable the same instance
            // and return its previous selection if that object still exists.
            var currentProperty = eventSystemType!.GetProperty("current",
                BindingFlags.Public | BindingFlags.Static);
            eventSystem = currentProperty?.GetValue(null) as Behaviour;
            if (eventSystem == null)
            {
                var findObjectMethod = typeof(UnityEngine.Object).GetMethod("FindObjectOfType",
                    BindingFlags.Public | BindingFlags.Static, binder: null,
                    types: new[] { typeof(Type), typeof(bool) }, modifiers: null);
                eventSystem = findObjectMethod?.Invoke(null, new object[] { eventSystemType, false }) as Behaviour;
            }

            if (eventSystem != null)
            {
                selectedObjectProperty = eventSystemType.GetProperty("currentSelectedGameObject",
                    BindingFlags.Public | BindingFlags.Instance);
                setSelectedObjectMethod = eventSystemType.GetMethod("SetSelectedGameObject",
                    BindingFlags.Public | BindingFlags.Instance, binder: null,
                    types: new[] { typeof(GameObject) }, modifiers: null);
                installed = true;
                MelonLogger.Msg("Sloptifine UI-event optimization ready; it disables the EventSystem component only during active offline solo gameplay.");
                return;
            }

            // Some custom scenes may not expose an active EventSystem during
            // scene initialization. Preserve the established safe behavior in
            // that case rather than leaving UI events permanently disabled.
            suspend = false;
            active = this;
            usingPrefix = true;
            var prefix = typeof(GameplayUiEventOverride).GetMethod(nameof(BeforeEventSystemUpdate),
                BindingFlags.Static | BindingFlags.NonPublic)!;
            harmony.Patch(updateMethod, prefix: new HarmonyMethod(prefix));
            installed = true;
            MelonLogger.Msg("Sloptifine UI-event optimization using the Update-prefix fallback; event processing pauses only during active solo gameplay.");
        }
        catch (Exception ex)
        {
            RestoreComponent();
            active = null;
            suspend = false;
            if (usingPrefix && updateMethod != null)
            {
                try { harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id); }
                catch { /* The inactive prefix passes through if cleanup fails. */ }
            }
            updateMethod = null;
            eventSystem = null;
            usingPrefix = false;
            installed = false;
            failed = true;
            MelonLogger.Warning($"Sloptifine UI-event optimization could not install and remains inactive: {ex.Message}");
        }
    }

    private void SetComponentSuspended(bool value)
    {
        suspend = value;
        if (eventSystem == null) return;

        if (value)
        {
            if (!disabledByUs)
            {
                originalEnabled = eventSystem.enabled;
                if (originalEnabled)
                {
                    try { selectedBeforeSuspend = selectedObjectProperty?.GetValue(eventSystem) as GameObject; }
                    catch { selectedBeforeSuspend = null; }
                    disabledByUs = true;
                }
            }
            if (disabledByUs && eventSystem.enabled) eventSystem.enabled = false;
            return;
        }

        RestoreComponent();
    }

    private void RestoreComponent()
    {
        if (disabledByUs)
        {
            try
            {
                if (eventSystem != null && originalEnabled && !eventSystem.enabled)
                    eventSystem.enabled = true;
                var selected = selectedBeforeSuspend;
                var currentSelection = eventSystem != null
                    ? selectedObjectProperty?.GetValue(eventSystem) as GameObject
                    : null;
                if (eventSystem != null && currentSelection == null && selected != null && selected.activeInHierarchy)
                    setSelectedObjectMethod?.Invoke(eventSystem, new object?[] { selected });
            }
            catch (Exception ex)
            {
                MelonLogger.Warning($"Sloptifine could not fully restore Unity UI selection: {ex.Message}");
            }
        }
        disabledByUs = false;
        originalEnabled = false;
        selectedBeforeSuspend = null;
    }

    private void Restore(HarmonyLib.Harmony harmony)
    {
        if (usingPrefix || updateMethod != null)
        {
            try
            {
                if (usingPrefix && updateMethod != null)
                    harmony.Unpatch(updateMethod, HarmonyPatchType.All, harmony.Id);
            }
            catch (Exception ex) { MelonLogger.Warning($"Sloptifine UI-event hook cleanup failed: {ex.Message}"); }
        }
        updateMethod = null;
        selectedObjectProperty = null;
        setSelectedObjectMethod = null;
        eventSystem = null;
        usingPrefix = false;
        installed = false;
        if (ReferenceEquals(active, this)) active = null;
    }

    private static bool BeforeEventSystemUpdate() => active?.suspend != true;
}
