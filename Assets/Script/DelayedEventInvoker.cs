using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public sealed class DelayedEventInvoker : MonoBehaviour
{
    [Min(0f)]
    [SerializeField] private float delaySeconds = 1f;
    [SerializeField] private UnityEvent onDelayElapsed;

    private Coroutine invokeCoroutine;

    private void OnEnable()
    {
        invokeCoroutine = StartCoroutine(InvokeAfterDelay());
    }

    private void OnDisable()
    {
        if (invokeCoroutine == null)
        {
            return;
        }

        StopCoroutine(invokeCoroutine);
        invokeCoroutine = null;
    }

    private IEnumerator InvokeAfterDelay()
    {
        if (delaySeconds > 0f)
        {
            yield return new WaitForSeconds(delaySeconds);
        }

        invokeCoroutine = null;
        onDelayElapsed?.Invoke();
    }
}
