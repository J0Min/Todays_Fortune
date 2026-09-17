#if UNITY_STANDALONE_WIN && !UNITY_EDITOR
using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Prevents more than one copy of the Windows player from running per user session.
/// </summary>
internal static class WindowsSingleInstanceGuard
{
    private const string MutexName = @"Local\TodaysFortune_5F94C982_SingleInstance";

    private static Mutex instanceMutex;
    private static bool ownsMutex;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    private static void Initialize()
    {
        if (instanceMutex != null)
        {
            return;
        }

        Mutex candidateMutex = new Mutex(false, MutexName);

        try
        {
            ownsMutex = candidateMutex.WaitOne(0, false);
        }
        catch (AbandonedMutexException)
        {
            // The previous process ended abnormally. Windows transferred ownership to us.
            ownsMutex = true;
        }

        if (!ownsMutex)
        {
            candidateMutex.Dispose();
            Debug.LogWarning("[SingleInstance] The application is already running. Closing this instance.");
            Application.Quit();
            return;
        }

        instanceMutex = candidateMutex;
        Application.quitting += ReleaseMutex;
    }

    private static void ReleaseMutex()
    {
        Application.quitting -= ReleaseMutex;

        if (instanceMutex == null)
        {
            return;
        }

        if (ownsMutex)
        {
            try
            {
                instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The mutex is no longer owned, so only disposal remains.
            }
        }

        instanceMutex.Dispose();
        instanceMutex = null;
        ownsMutex = false;
    }
}
#endif
