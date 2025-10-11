using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

[DisallowMultipleComponent]
public class ChangingTimeGravity : MonoBehaviour
{
    [Tooltip("Seconds between each time-scale change")]
    [SerializeField] private float secondsPerStep = 10f;

    [Tooltip("Downward gravity magnitude to apply (positive). Gravity will be set to (0, -magnitude).")]
    [SerializeField] private float gravityMagnitude = 3f;

    [Tooltip("Sequence of time scales to cycle through. Edit this list in the Inspector.")]
    [SerializeField] private List<float> timeScaleSequence = new List<float> { 1f, 0.5f, 1.2f, 2f, 1f };

    [Tooltip("Text component that will display current time scale")]
    [SerializeField] private TMP_Text currentTimeScaleText;

    [Tooltip("Text component that will display current gravity")]
    [SerializeField] private TMP_Text currentGravityText;

    [Tooltip("Text component that will display time left until the next change (real seconds)")]
    [SerializeField] private TMP_Text timeLeftText;

    private Coroutine cycleCoroutine;

    void Start()
    {
        ApplyGravity();
        if (timeScaleSequence == null || timeScaleSequence.Count == 0)
        {
            Debug.LogError("timeScaleSequence must contain at least one value.");
            return;
        }
        cycleCoroutine = StartCoroutine(CycleTimeScaleCoroutine());

        if (currentTimeScaleText != null)
        {
            currentTimeScaleText.text = $"Current Time Scale: {TimeController.Instance.TimeScale:F2}";
        }
        if (currentGravityText != null)
        {
            currentGravityText.text = $"Current Gravity: {GravityController.Instance.Gravity:F2}";
        }
    }

    void Update()
    {
        if (currentTimeScaleText != null)
        {
            currentTimeScaleText.text = $"Current Time Scale: {TimeController.Instance.TimeScale}";
        }
        if (currentGravityText != null)
        {
            currentGravityText.text = $"Current Gravity: {GravityController.Instance.Gravity}";
        }
    }

    private IEnumerator CycleTimeScaleCoroutine()
    {
        int index = 0;

        while (true)
        {
            float targetTimeScale = timeScaleSequence[index];
            TimeController.Instance.TimeScale = targetTimeScale;
            ApplyGravity();

            // countdown using unscaled time so it's not affected by TimeScale
            float remainingUnscaledSeconds = secondsPerStep;

            // update UI at frame rate while counting down
            while (remainingUnscaledSeconds > 0f)
            {
                UpdateTimeLeftText(remainingUnscaledSeconds);
                // subtract the real elapsed time
                remainingUnscaledSeconds -= Time.unscaledDeltaTime;
                yield return null;
            }

            // ensure shows 0.0s right at switch
            UpdateTimeLeftText(0f);

            // move to next scale
            index = (index + 1) % timeScaleSequence.Count;
        }
    }



    private void ApplyGravity()
    {
        GravityController.Instance.Gravity = gravityMagnitude;
    }

    private void UpdateTimeLeftText(float remainingUnscaledSeconds)
    {
        if (timeLeftText == null) return;
        remainingUnscaledSeconds = Mathf.Max(0f, remainingUnscaledSeconds);
        timeLeftText.text = $"Next change in: {remainingUnscaledSeconds:0.0}s";
    }
}