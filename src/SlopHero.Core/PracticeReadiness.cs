namespace SlopHero.Core;

public static class PracticeReadiness
{
    public static bool CanSelect(bool sceneInitialized, bool managerChanged,
        bool gameReady, bool menuReady, bool rendererReady, int coroutinesRunning)
        => sceneInitialized && managerChanged && gameReady && menuReady
            && rendererReady && coroutinesRunning == 0;
}
